using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Infrastructure.Http;
using Serilog;

namespace AI.LLM.Clients.Base;

public partial class ChatLLMApi
{
    /// <summary>
    /// Обрабатывает потоковый ответ
    /// </summary>
    private async Task<ChatCompletionsResponse> ProcessStreamResponse(
        GenerateSettings generateSettings,
        HttpResponseMessage response)
    {
        var result = await _streamSender.StartAsync(
            streamId: generateSettings.StreamId,
            response: response,
            method: generateSettings.StreamMethod);

        // TODO: Подумать как обработать ошибки
        if (!string.IsNullOrEmpty(result))
        {
            return new ChatCompletionsResponse(result);
        }

        throw new InvalidOperationException("Потоковый ответ пуст.");
    }

    /// <summary>
    /// Обрабатывает потоковый ответ ВНУТРЕННЕ (без IStreamHandler).
    /// Используется для автоматического streaming в SendWithContextAsync.
    /// Читает SSE stream, накапливает токены и возвращает полный ответ.
    /// </summary>
    private async Task<ChatCompletionsResponse> ProcessStreamResponseInternal(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: Начинаем читать stream, StatusCode={response.StatusCode}");

        // Общий таймаут на весь метод - 18 минут
        using var methodTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(18));
        using var methodLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, methodTimeoutCts.Token);
        
        Stream stream = null;
        try
        {
            // Таймаут 80 секунд на получение stream
            using var streamTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(80));
            using var streamLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(methodLinkedCts.Token, streamTimeoutCts.Token);
            
            var baseStream = await response.Content.ReadAsStreamAsync(streamLinkedCts.Token);
            
            // Оборачиваем в idle timeout monitor если включено
            // baseStream НЕ используем через using — он будет освобождён через stream?.Dispose() в finally
            // (StreamWithTimeoutMonitor.Dispose() освобождает внутренний поток)
            if (IdleTimeoutSettings != null && IdleTimeoutSettings.Enabled)
            {
                Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: Включаем мониторинг idle timeout ({IdleTimeoutSettings.IdleTimeout.TotalSeconds} сек)");
                stream = new StreamWithTimeoutMonitor(baseStream, IdleTimeoutSettings.IdleTimeout, methodLinkedCts.Token);
            }
            else
            {
                Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: Idle timeout ОТКЛЮЧЕН или не настроен");
                stream = baseStream;
            }

            using var reader = new StreamReader(stream);
            var fullContent = new StringBuilder();
            var fullReasoning = new StringBuilder();
            
            // Поддержка Vision моделей - собираем изображения
            var collectedImages = new List<ImageInfo>();
            string nativeFinishReason = null;
            string finishReason = null;
            
            // Сохраняем usage из чанка (обычно последний чанк с usage != null)
            Usage collectedUsage = null;
            
            // Защита от зацикленного ответа (один и тот же токен повторяется слишком много раз)
            const int maxConsecutiveRepeats = 300;
            string lastToken = null;
            int consecutiveCount = 0;
            
            // Счетчики для отладки стриминговых данных
            int chunksWithContent = 0;
            int chunksWithReasoning = 0;

            string provider = null;

            // Function Calling: накопление tool_calls по индексу из дельт
            var toolCallBuilders = new Dictionary<int, ToolCall>();
            
            Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: Stream получен, начинаем читать строки...");
            
            int linesRead = 0;
            string line;
            // НЕ используем reader.EndOfStream - это синхронное свойство которое может заблокироваться!
            // Вместо этого читаем до null (конец stream)
            while ((line = await ReadLineWithTimeoutAsync(reader, methodLinkedCts.Token)) != null)
            {
                methodLinkedCts.Token.ThrowIfCancellationRequested();
                linesRead++;
                
                // Пропускаем пустые строки (с защитной задержкой от busy-loop)
                if (line.Length == 0)
                {
                    await Task.Delay(1, methodLinkedCts.Token);
                    continue;
                }
                
                // Пропускаем SSE комментарии (начинаются с :)
                if (line.StartsWith(":"))
                    continue;
                    
                // Маркер завершения - дочитываем оставшиеся данные
                if (line == "data: [DONE]")
                {
                    // Дочитываем оставшиеся данные (могут быть usage, метаданные)
                    string remainingLine;
                    while ((remainingLine = await ReadLineWithTimeoutAsync(reader, methodLinkedCts.Token)) != null)
                    {
                        if (remainingLine.Length > 0)
                        {
                            Log.Debug($"ChatLLMApi: После [DONE] получена строка: {remainingLine}");
                        }
                    }
                    break;
                }

                // Обрабатываем SSE строки с данными
                if (!line.StartsWith("data: "))
                    continue;

                string jsonData = line.Substring(6); // Убираем "data: "
                
                try
                {
                    // Используем JsonDocument для низкоуровневого парсинга
                    using var parsedJson = JsonDocument.Parse(jsonData);
                    var root = parsedJson.RootElement;

                    // Проверка на null/undefined
                    if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
                        continue;

                    // Получаем choices
                    if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                        continue;

                    var firstChoice = choices[0];
                    
                    // Получаем finish_reason и native_finish_reason (могут быть в любом чанке, но обычно в последнем)
                    if (firstChoice.TryGetProperty("finish_reason", out var finishReasonElement) && 
                        finishReasonElement.ValueKind == JsonValueKind.String)
                    {
                        finishReason = finishReasonElement.GetString();
                    }
                    
                    if (firstChoice.TryGetProperty("native_finish_reason", out var nativeFinishElement) && 
                        nativeFinishElement.ValueKind == JsonValueKind.String)
                    {
                        nativeFinishReason = nativeFinishElement.GetString();
                    }

                    if (root.TryGetProperty("provider", out var providerElement) &&
                        providerElement.ValueKind == JsonValueKind.String)
                    {
                        provider = providerElement.GetString();
                    }
                    
                    // Парсим usage если есть (обычно в последнем чанке)
                    if (root.TryGetProperty("usage", out var usageElement) && 
                        usageElement.ValueKind == JsonValueKind.Object)
                    {
                        collectedUsage = ParseUsageFromJson(usageElement);
                        Log.Debug($"ChatLLMApi: Получен usage - prompt_tokens={collectedUsage.PromptTokens}, " +
                                  $"completion_tokens={collectedUsage.CompletionTokens}, " +
                                  $"total_tokens={collectedUsage.TotalTokens}, " +
                                  $"cost={collectedUsage.Cost}");
                    }
                    
                    // Получаем delta
                    if (!firstChoice.TryGetProperty("delta", out var delta))
                        continue;
                    
                    // Парсим reasoning (delta.reasoning) - размышления модели
                    // ВАЖНО: reasoning может приходить БЕЗ content, и это нормально!
                    if (delta.TryGetProperty("reasoning", out var reasoningElement))
                    {
                        string reasoning = reasoningElement.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(reasoning))
                        {
                            fullReasoning.Append(reasoning);
                            chunksWithReasoning++;
                        }
                    }
                    
                    // Парсим текстовый контент (delta.content)
                    if (delta.TryGetProperty("content", out var contentElement))
                    {
                        string content = contentElement.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(content))
                        {
                            // Проверяем на зацикленный ответ (один и тот же токен 200+ раз подряд)
                            if (content == lastToken)
                            {
                                consecutiveCount++;
                                if (consecutiveCount >= maxConsecutiveRepeats)
                                {
                                    throw new InvalidOperationException(
                                        $"Обнаружен зацикленный ответ: токен \"{lastToken}\" повторяется {consecutiveCount} раз подряд. " +
                                        "Возможно модель зависла или генерирует некорректный вывод.");
                                }
                            }
                            else
                            {
                                lastToken = content;
                                consecutiveCount = 1;
                            }
                            
                            fullContent.Append(content);
                            chunksWithContent++;
                        }
                    }
                    
                    // Парсим tool_calls (delta.tool_calls) - Function Calling
                    if (delta.TryGetProperty("tool_calls", out var toolCallsElement) &&
                        toolCallsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tcEl in toolCallsElement.EnumerateArray())
                        {
                            int tcIndex = tcEl.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : 0;

                            if (!toolCallBuilders.TryGetValue(tcIndex, out var tc))
                            {
                                tc = new ToolCall { Function = new FunctionCall() };
                                toolCallBuilders[tcIndex] = tc;
                            }

                            if (tcEl.TryGetProperty("id", out var idEl))
                                tc.Id = idEl.GetString();
                            if (tcEl.TryGetProperty("type", out var typeEl2))
                                tc.Type = typeEl2.GetString();

                            if (tcEl.TryGetProperty("function", out var fnEl))
                            {
                                if (fnEl.TryGetProperty("name", out var nameEl))
                                    tc.Function.Name = (tc.Function.Name ?? "") + nameEl.GetString();
                                if (fnEl.TryGetProperty("arguments", out var argsEl))
                                    tc.Function.Arguments = (tc.Function.Arguments ?? "") + argsEl.GetString();
                            }
                        }
                    }

                    // Парсим изображения (delta.images) - для Vision моделей
                    // Изображения могут приходить в любом чанке, сохраняем последние полученные
                    if (delta.TryGetProperty("images", out var imagesElement) && 
                        imagesElement.ValueKind == JsonValueKind.Array)
                    {
                        // Проверяем что это финальный чанк с native_finish_reason == "STOP"
                        //bool isStopChunk = string.Equals(nativeFinishReason, "STOP", StringComparison.OrdinalIgnoreCase) ||
                        //                  string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase);

                        //if (!isStopChunk)
                        //{
                        //    Log.Debug($"ChatLLMApi: Получены изображения, но native_finish_reason != STOP " +
                        //               $"(finish_reason={finishReason}, native_finish_reason={nativeFinishReason}). Пропускаем.");
                        //    continue;
                        //}

                        // Очищаем список и сохраняем новые изображения (берем самые свежие)
                        collectedImages.Clear();
                        
                        foreach (var imageElement in imagesElement.EnumerateArray())
                        {
                            var imageInfo = new ImageInfo();
                            
                            if (imageElement.TryGetProperty("type", out var typeEl))
                                imageInfo.Type = typeEl.GetString();
                            
                            if (imageElement.TryGetProperty("index", out var indexEl))
                                imageInfo.Index = indexEl.GetInt32();
                            
                            if (imageElement.TryGetProperty("image_url", out var imageUrlEl))
                            {
                                imageInfo.ImageUrl = new ImageUrl();
                                if (imageUrlEl.TryGetProperty("url", out var urlEl))
                                    imageInfo.ImageUrl.Url = urlEl.GetString();
                            }
                            
                            if (imageInfo.ImageUrl?.Url != null)
                            {
                                collectedImages.Add(imageInfo);
                                Log.Debug($"ChatLLMApi: Обновлено изображение, index={imageInfo.Index}, type={imageInfo.Type}");
                            }
                        }
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    // Пропускаем невалидные JSON чанки
                    Log.Warning(ex, $"ChatLLMApi ProcessStreamResponseInternal: невалидный JSON chunk");
                    continue;
                }
            }
            
            Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: Закончили читать stream, всего строк: {linesRead}, " +
                      $"длина контента: {fullContent.Length}, длина reasoning: {fullReasoning.Length}, " +
                      $"изображений: {collectedImages.Count}, " +
                      $"чанков с content: {chunksWithContent}, чанков с reasoning: {chunksWithReasoning}, " +
                      $"finish_reason: {finishReason}, native_finish_reason: {nativeFinishReason}");
            
            // Дочитываем любые оставшиеся данные после выхода из цикла
            string finalLine;
            while ((finalLine = await ReadLineWithTimeoutAsync(reader, methodLinkedCts.Token)) != null)
            {
                if (!string.IsNullOrEmpty(finalLine))
                {
                    Log.Debug($"ChatLLMApi: После завершения цикла получена строка: {finalLine}");
                }
            }

            if (!string.IsNullOrEmpty(nativeFinishReason) &&
                (string.Equals(nativeFinishReason, "IMAGE_PROHIBITED_CONTENT", StringComparison.OrdinalIgnoreCase) ||
                nativeFinishReason.Contains("PROHIBITED_CONTENT")))
            {
                return new ChatCompletionsResponse(
                    $$"""
                    К сожалению, не могу выполнить этот запрос, так как он нарушает политику использования.
                    
                    Пожалуйста, попробуйте:
                    * Переформулировать запрос
                    * Изменить изображение
                    * Убедиться, что контент соответствует правилам безопасности
                    
                    Причина: {{nativeFinishReason}}
                    """);
            }

            // Проверяем что генерация завершилась корректно
            // ВАЖНО: Если получены данные (текст/reasoning/изображения), то finish_reason не обязателен
            // Некоторые провайдеры могут не возвращать finish_reason в промежуточных или финальных чанках
            bool hasAnyData = fullContent.Length > 0 || fullReasoning.Length > 0 || collectedImages.Count > 0;
            bool hasFinishReason = !string.IsNullOrEmpty(nativeFinishReason) || !string.IsNullOrEmpty(finishReason);
            
            if (hasFinishReason)
            {
                // Если finish_reason присутствует, проверяем что он корректный
                // Разрешены: native_finish_reason = "STOP"/"MAX_TOKENS"/"length" ИЛИ finish_reason = "stop"
                bool isValidFinishReason = 
                    string.Equals(nativeFinishReason, "STOP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nativeFinishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nativeFinishReason, "length", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nativeFinishReason, "MALFORMED_FUNCTION_CALL", StringComparison.OrdinalIgnoreCase) ||
                    toolCallBuilders.Count > 0;
                
                if (!isValidFinishReason)
                {
                    var lastLine = await ReadLineWithTimeoutAsync(reader, methodLinkedCts.Token);
                    
                    throw new InvalidOperationException(
                        $$"""
                        Генерация не завершилась корректно.
                        native_finish_reason='{{nativeFinishReason}}', finish_reason='{{finishReason}}'.
                        Ожидалось native_finish_reason='STOP' или 'MAX_TOKENS' или 'length', либо finish_reason='stop'.
                        Last Line: {{lastLine}}
                        """);
                }
            }
            else if (!hasAnyData)
            {
                // Если нет ни данных, ни finish_reason - это ошибка (возможно прервано соединение)
                throw new InvalidOperationException(
                    "Генерация завершилась без данных и без finish_reason. Возможно, соединение прервано или поток пуст.");
            }
            else
            {
                // Есть данные, но нет finish_reason - логируем предупреждение, но продолжаем
                Log.Warning($"ChatLLMApi ProcessStreamResponseInternal: Получены данные (content={fullContent.Length}, " +
                            $"reasoning={fullReasoning.Length}, images={collectedImages.Count}), но finish_reason отсутствует. " +
                            $"Это может быть нормально для некоторых провайдеров.");
            }
            
            // Проверяем что есть хоть какой-то результат (текст ИЛИ reasoning ИЛИ изображения)
            // Примечание: изображения сохраняются только при native_finish_reason == "STOP" (фильтрация выше)
            // ВАЖНО: reasoning может быть БЕЗ content - это нормально, модель размышляет перед ответом
            bool hasText = fullContent.Length > 0;
            bool hasReasoning = fullReasoning.Length > 0;
            bool hasImages = collectedImages.Count > 0;
            
            bool hasToolCalls = toolCallBuilders.Count > 0;

            if (!hasText && !hasReasoning && !hasImages && !hasToolCalls)
            {
                throw new InvalidOperationException("Потоковый ответ пуст - не получено ни текста, ни reasoning, ни изображений, ни tool_calls.");
            }

            // Формируем ответ
            // ВАЖНО: Если нет текста и нет изображений, но есть reasoning - используем reasoning как content
            string finalContent;
            string finalReasoning = null;
            
            if (!hasText && !hasImages && hasReasoning)
            {
                // Reasoning становится основным контентом, если нет текста и изображений
                finalContent = fullReasoning.ToString();
                Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: Нет content и изображений, используем reasoning ({fullReasoning.Length} символов) как content");
            }
            else
            {
                // Стандартный случай: content - это текст, reasoning - отдельно
                finalContent = hasText ? fullContent.ToString() : string.Empty;
                
                // Сохраняем reasoning отдельно только если есть content или изображения
                if (hasReasoning)
                    finalReasoning = fullReasoning.ToString();
            }
            
            var resultMessage = new LLMMessage("assistant", finalContent);
            
            // Добавляем reasoning если есть (и он не был использован как content)
            if (!string.IsNullOrEmpty(finalReasoning))
            {
                resultMessage.Reasoning = finalReasoning;
            }
            
            // Добавляем изображения если есть
            if (hasImages)
            {
                resultMessage.Images = collectedImages;
            }

            // Добавляем tool_calls если были получены
            if (toolCallBuilders.Count > 0)
            {
                resultMessage.ToolCalls = toolCallBuilders
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value)
                    .ToList();
            }
            
            return new ChatCompletionsResponse
            {
                Choices =
                [
                    new Choice
                    {
                        Message = resultMessage,
                        FinishReason = finishReason ?? "stop",
                        NativeFinishReason = nativeFinishReason
                    }
                ],
                Model = ModelName,
                Provider = provider,
                Usage = collectedUsage,
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"ChatLLMApi ProcessStreamResponseInternal Exception");
            throw;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Обрабатывает стандартный ответ
    /// </summary>
    private async Task<ChatCompletionsResponse> ProcessStandardResponse(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            // Локальный таймаут 60 секунд для ReadFromJsonAsync - операция должна быть быстрой
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var chatCompletionsResponse = await response.Content
                .ReadFromJsonAsync<ChatCompletionsResponse>(cancellationToken: linkedCts.Token);

            if (chatCompletionsResponse == null ||
                chatCompletionsResponse.Choices == null ||
                chatCompletionsResponse.Choices.Count == 0)
            {
                var content = (await response.Content.ReadAsStringAsync(linkedCts.Token) ?? "").TruncateForLogging();
                throw new InvalidOperationException($"Некорректный ответ от LLM API.\nContent={content}");
            }

            return chatCompletionsResponse;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Глобальная отмена - пробрасываем
            throw;
        }
        catch (Exception ex)
        {
            string content = "";
            try 
            { 
                using var errorCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                content = await response.Content.ReadAsStringAsync(errorCts.Token); 
            } 
            catch { }
            Log.Error(ex, $"ChatLLMApi ProcessStandardResponse, Content={content.TruncateForLogging()}");
            throw;
        }
    }

    /// <summary>
    /// Парсит объект usage из JSON элемента
    /// </summary>
    private static Usage ParseUsageFromJson(JsonElement usageElement)
    {
        var usage = new Usage();
        
        if (usageElement.TryGetProperty("prompt_tokens", out var promptTokens))
            usage.PromptTokens = promptTokens.GetInt32();
        
        if (usageElement.TryGetProperty("completion_tokens", out var completionTokens))
            usage.CompletionTokens = completionTokens.GetInt32();
        
        if (usageElement.TryGetProperty("total_tokens", out var totalTokens))
            usage.TotalTokens = totalTokens.GetInt32();
        
        // Парсим cost с использованием готовой утилиты
        if (usageElement.TryGetProperty("cost", out var costElement) && costElement.ValueKind != JsonValueKind.Null)
            usage.Cost = costElement.Clone();
        
        // Парсим reasoning_tokens из completion_tokens_details
        if (usageElement.TryGetProperty("completion_tokens_details", out var completionDetails) && 
            completionDetails.ValueKind == JsonValueKind.Object)
        {
            if (completionDetails.TryGetProperty("reasoning_tokens", out var reasoningTokens))
                usage.ReasoningTokens = reasoningTokens.GetInt32();
        }
        
        return usage;
    }

    /// <summary>
    /// Читает строку из StreamReader с таймаутом.
    /// Защита от зависания если сервер перестал отвечать.
    /// </summary>
    private static async Task<string> ReadLineWithTimeoutAsync(
        StreamReader reader, 
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(ReadLineTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        try
        {
            return await reader.ReadLineAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"ReadLineAsync таймаут ({ReadLineTimeout.TotalSeconds} сек). Сервер не отвечает.");
        }
    }
}
