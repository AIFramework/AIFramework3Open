using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AI.LLM.API.LLMAPI;
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
    /// Потоковая генерация: отдаёт кадры ответа по мере их поступления от провайдера.
    /// </summary>
    /// <remarks>
    /// Отличие от <see cref="SendWithContextAsync"/> — в моменте выдачи. Там ответ возвращается
    /// собранным целиком (внутри тоже streaming, но только ради раннего обнаружения зависших
    /// запросов), здесь каждый кадр уходит потребителю сразу. Нужно тем, кто показывает генерацию
    /// пользователю в реальном времени: собранный ответ такой возможности не даёт, а
    /// <see cref="IStreamHandler"/> вручает сырой HTTP-ответ и оставляет разбор SSE на вызывающего.
    /// <para>
    /// Повторов здесь нет сознательно: они допустимы, только пока наружу не отдан ни один кадр, а
    /// после первого <c>yield</c> повтор запроса означал бы дубль текста у потребителя. Сбой
    /// отправки поэтому вылетает исключением до начала итерации.
    /// </para>
    /// </remarks>
    /// <param name="context">Контекст сообщений.</param>
    /// <param name="generateSettings">Настройки генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async IAsyncEnumerable<LLMStreamChunk> SendWithContextStreamAsync(
        IEnumerable<LLMMessage> context,
        GenerateSettings generateSettings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sendData = BuildStreamingSendData(context, generateSettings);

        using var response = await _webApi.PostAsJsonAsync(ApiUrl, sendData, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateHttpErrorException(0, response, context, cancellationToken, maxAttempts: 1);

        await foreach (var chunk in ReadStreamChunksAsync(response, cancellationToken))
            yield return chunk;
    }

    /// <summary>
    /// Собирает тело потокового запроса: валидирует настройки, принудительно включает streaming,
    /// подставляет сообщения и предпочтения провайдера.
    /// </summary>
    /// <remarks>
    /// Общее для собранного ответа (<see cref="SendWithContextAsync"/>) и покадровой выдачи
    /// (<see cref="SendWithContextStreamAsync"/>): запрос у них одинаковый, различается только
    /// обработка ответа.
    /// </remarks>
    private SendDataLLM BuildStreamingSendData(IEnumerable<LLMMessage> context, GenerateSettings generateSettings)
    {
        generateSettings = Validate(generateSettings);

        if (context == null)
            throw new ArgumentException("Контекст не может быть null.", nameof(context));

        // ВАЖНО: Принудительно включаем streaming для раннего обнаружения зависших запросов!
        // Даже если пользователь не указал streamId, мы создаем временный для внутреннего использования.
        // Копией, а не перечислением полей вручную: забытое в списке поле молча не доехало бы до
        // модели, а через этот путь идёт ВЕСЬ трафик клиента.
        if (string.IsNullOrEmpty(generateSettings.StreamId))
            generateSettings = generateSettings.CloneWithStream(Guid.NewGuid().ToString(), "StreamMessage");

        var sendData = new SendDataLLM(ModelName, generateSettings);
        sendData.StreamOptions = StreamOptions;
        sendData.SetMessages(context);

        // Установка провайдера если задан (для OpenRouter)
        if (PreferredProvider != null)
            sendData.Provider = PreferredProvider;

        return sendData;
    }

    /// <summary>
    /// Собирает кадры <see cref="ReadStreamChunksAsync"/> в полный ответ.
    /// Используется для автоматического streaming в <see cref="SendWithContextAsync"/>.
    /// </summary>
    private async Task<ChatCompletionsResponse> ProcessStreamResponseInternal(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fullContent = new StringBuilder();
        var fullReasoning = new StringBuilder();

        // Поддержка Vision моделей - собираем изображения
        var collectedImages = new List<ImageInfo>();
        string nativeFinishReason = null;
        string finishReason = null;
        string provider = null;

        // Сохраняем usage из чанка (обычно последний чанк с usage != null)
        Usage collectedUsage = null;

        // Function Calling: накопление tool_calls по индексу из дельт
        var toolCallBuilders = new Dictionary<int, ToolCall>();

        int chunksWithContent = 0;
        int chunksWithReasoning = 0;

        try
        {
            await foreach (var chunk in ReadStreamChunksAsync(response, cancellationToken))
            {
                // Сквозные поля кадра «липкие»: провайдер присылает их не в каждом,
                // и последнее известное значение остаётся в силе.
                if (!string.IsNullOrEmpty(chunk.Provider)) provider = chunk.Provider;
                if (chunk.Usage != null) collectedUsage = chunk.Usage;
                if (!string.IsNullOrEmpty(chunk.FinishReason)) finishReason = chunk.FinishReason;
                if (!string.IsNullOrEmpty(chunk.NativeFinishReason)) nativeFinishReason = chunk.NativeFinishReason;

                if (!string.IsNullOrEmpty(chunk.Reasoning))
                {
                    fullReasoning.Append(chunk.Reasoning);
                    chunksWithReasoning++;
                }

                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    fullContent.Append(chunk.Content);
                    chunksWithContent++;
                }

                // Изображения приходят набором целиком — берём самые свежие.
                if (chunk.Images != null)
                {
                    collectedImages.Clear();
                    collectedImages.AddRange(chunk.Images);
                }

                if (chunk.ToolCalls != null) ToolCallDelta.Merge(toolCallBuilders, chunk.ToolCalls);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"ChatLLMApi ProcessStreamResponseInternal Exception");
            throw;
        }

        Log.Debug($"ChatLLMApi ProcessStreamResponseInternal: длина контента: {fullContent.Length}, " +
                  $"длина reasoning: {fullReasoning.Length}, изображений: {collectedImages.Count}, " +
                  $"чанков с content: {chunksWithContent}, чанков с reasoning: {chunksWithReasoning}, " +
                  $"finish_reason: {finishReason}, native_finish_reason: {nativeFinishReason}");

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
            // Разрешены: native_finish_reason = "STOP"/"MAX_TOKENS"/"length" ИЛИ finish_reason = "stop"/"length"
            bool isValidFinishReason =
                string.Equals(nativeFinishReason, "STOP", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nativeFinishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nativeFinishReason, "length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nativeFinishReason, "MALFORMED_FUNCTION_CALL", StringComparison.OrdinalIgnoreCase) ||
                toolCallBuilders.Count > 0;

            if (!isValidFinishReason)
            {
                // Поток к этому моменту вычитан до конца (итератор дочитывает хвост сам),
                // поэтому «последней строки» здесь уже не осталось — раньше в сообщение
                // всегда попадал null.
                throw new InvalidOperationException(
                    $$"""
                    Генерация не завершилась корректно.
                    native_finish_reason='{{nativeFinishReason}}', finish_reason='{{finishReason}}'.
                    Ожидалось native_finish_reason='STOP' или 'MAX_TOKENS' или 'length', либо finish_reason='stop' или 'length'.
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
                    // Если провайдер не прислал finish_reason, но есть tool_calls - считаем что это tool_calls
                    FinishReason = finishReason ?? (toolCallBuilders.Count > 0 ? "tool_calls" : "stop"),
                    NativeFinishReason = nativeFinishReason
                }
            ],
            Model = ModelName,
            Provider = provider,
            Usage = collectedUsage,
        };
    }

    /// <summary>
    /// Читает SSE-поток ответа и отдаёт кадры по мере поступления.
    /// </summary>
    /// <remarks>
    /// Единственное место разбора SSE в библиотеке: через него идут и сборка полного ответа
    /// (<see cref="ProcessStreamResponseInternal"/>), и инкрементальная выдача наружу
    /// (<see cref="SendWithContextStreamAsync"/>). Пока разбора наружу не было, каждому
    /// потребителю, которому нужны токены по мере генерации, приходилось писать свой — со своим
    /// набором учтённых полей и своими ошибками.
    /// <para>
    /// Логирование сбоя разбора остаётся на потребителе: <c>yield return</c> нельзя поставить
    /// внутрь <c>try</c> с <c>catch</c>, поэтому здесь только <c>finally</c> с освобождением потока.
    /// </para>
    /// </remarks>
    private async IAsyncEnumerable<LLMStreamChunk> ReadStreamChunksAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Debug($"ChatLLMApi ReadStreamChunksAsync: Начинаем читать stream, StatusCode={response.StatusCode}");

        // Общий таймаут на весь метод - 18 минут
        using var methodTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(18));
        using var methodLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, methodTimeoutCts.Token);

        Stream stream = null;
        try
        {
            // Таймаут 80 секунд на получение stream
            using (var streamTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(80)))
            using (var streamLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(methodLinkedCts.Token, streamTimeoutCts.Token))
            {
                var baseStream = await response.Content.ReadAsStreamAsync(streamLinkedCts.Token);

                // Оборачиваем в idle timeout monitor если включено
                // baseStream НЕ используем через using — он будет освобождён через stream?.Dispose() в finally
                // (StreamWithTimeoutMonitor.Dispose() освобождает внутренний поток)
                if (IdleTimeoutSettings != null && IdleTimeoutSettings.Enabled)
                {
                    Log.Debug($"ChatLLMApi ReadStreamChunksAsync: Включаем мониторинг idle timeout ({IdleTimeoutSettings.IdleTimeout.TotalSeconds} сек)");
                    stream = new StreamWithTimeoutMonitor(baseStream, IdleTimeoutSettings.IdleTimeout, methodLinkedCts.Token);
                }
                else
                {
                    Log.Debug($"ChatLLMApi ReadStreamChunksAsync: Idle timeout ОТКЛЮЧЕН или не настроен");
                    stream = baseStream;
                }
            }

            using var reader = new StreamReader(stream);

            // Защита от зацикленного ответа (один и тот же токен повторяется слишком много раз)
            const int maxConsecutiveRepeats = 300;
            string lastToken = null;
            int consecutiveCount = 0;

            Log.Debug($"ChatLLMApi ReadStreamChunksAsync: Stream получен, начинаем читать строки...");

            int linesRead = 0;
            string line;
            // НЕ используем reader.EndOfStream - это синхронное свойство которое может заблокироваться!
            // Вместо этого читаем до null (конец stream)
            while ((line = await ReadLineWithTimeoutAsync(reader, methodLinkedCts.Token)) != null)
            {
                methodLinkedCts.Token.ThrowIfCancellationRequested();
                linesRead++;

                // Пустая строка в SSE — штатный разделитель событий, то есть приходит после
                // КАЖДОГО кадра. Задержки здесь быть не должно: на Windows Task.Delay(1) ждёт
                // около 15 мс, и на ответе в тысячу токенов это пятнадцать секунд на ровном
                // месте. Busy-loop она и не предотвращала — ReadLineAsync ждёт ввод-вывод.
                if (line.Length == 0)
                    continue;

                // Пропускаем SSE комментарии (начинаются с :)
                if (line.StartsWith(":"))
                    continue;

                // Обрабатываем SSE строки с данными
                // Спецификация SSE допускает "data:" без пробела после двоеточия
                if (!line.StartsWith("data:"))
                    continue;

                string jsonData = line.Substring(5).TrimStart(); // Убираем "data:" и ведущие пробелы

                // Маркер завершения - дочитываем оставшиеся данные
                if (jsonData == "[DONE]")
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

                var chunk = ParseStreamChunk(jsonData);
                if (chunk == null)
                    continue;

                // Проверяем на зацикленный ответ (один и тот же токен 300+ раз подряд)
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    if (chunk.Content == lastToken)
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
                        lastToken = chunk.Content;
                        consecutiveCount = 1;
                    }
                }

                yield return chunk;
            }

            Log.Debug($"ChatLLMApi ReadStreamChunksAsync: Закончили читать stream, всего строк: {linesRead}");

            // Дочитываем любые оставшиеся данные после выхода из цикла
            string finalLine;
            while ((finalLine = await ReadLineWithTimeoutAsync(reader, methodLinkedCts.Token)) != null)
            {
                if (!string.IsNullOrEmpty(finalLine))
                {
                    Log.Debug($"ChatLLMApi: После завершения цикла получена строка: {finalLine}");
                }
            }
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Разбирает один SSE-кадр. <c>null</c> — кадр не несёт данных (невалидный JSON или пустой объект).
    /// </summary>
    private static LLMStreamChunk ParseStreamChunk(string jsonData)
    {
        try
        {
            // Используем JsonDocument для низкоуровневого парсинга
            using var parsedJson = JsonDocument.Parse(jsonData);
            var root = parsedJson.RootElement;

            // Проверка на null/undefined
            if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
                return null;

            var chunk = new LLMStreamChunk();

            // ВАЖНО: provider и usage парсим ДО проверки choices,
            // т.к. при stream_options.include_usage=true финальный чанк с usage приходит с ПУСТЫМ choices
            if (root.TryGetProperty("provider", out var providerElement) &&
                providerElement.ValueKind == JsonValueKind.String)
            {
                chunk.Provider = providerElement.GetString();
            }

            if (root.TryGetProperty("model", out var modelElement) &&
                modelElement.ValueKind == JsonValueKind.String)
            {
                chunk.Model = modelElement.GetString();
            }

            // Парсим usage если есть (обычно в последнем чанке)
            if (root.TryGetProperty("usage", out var usageElement) &&
                usageElement.ValueKind == JsonValueKind.Object)
            {
                chunk.Usage = ParseUsageFromJson(usageElement);
                Log.Debug($"ChatLLMApi: Получен usage - prompt_tokens={chunk.Usage.PromptTokens}, " +
                          $"completion_tokens={chunk.Usage.CompletionTokens}, " +
                          $"total_tokens={chunk.Usage.TotalTokens}, " +
                          $"cost={chunk.Usage.Cost}");
            }

            // Получаем choices
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                // Кадр без вариантов ответа — это финальный кадр с usage, и он нужен потребителю.
                return chunk;
            }

            var firstChoice = choices[0];

            // Получаем finish_reason и native_finish_reason (могут быть в любом чанке, но обычно в последнем)
            if (firstChoice.TryGetProperty("finish_reason", out var finishReasonElement) &&
                finishReasonElement.ValueKind == JsonValueKind.String)
            {
                chunk.FinishReason = finishReasonElement.GetString();
            }

            if (firstChoice.TryGetProperty("native_finish_reason", out var nativeFinishElement) &&
                nativeFinishElement.ValueKind == JsonValueKind.String)
            {
                chunk.NativeFinishReason = nativeFinishElement.GetString();
            }

            // Получаем delta
            if (!firstChoice.TryGetProperty("delta", out var delta))
                return chunk;

            // Парсим reasoning (delta.reasoning) - размышления модели
            // ВАЖНО: reasoning может приходить БЕЗ content, и это нормально!
            if (delta.TryGetProperty("reasoning", out var reasoningElement) &&
                reasoningElement.ValueKind == JsonValueKind.String)
            {
                chunk.Reasoning = reasoningElement.GetString();
            }

            // Часть провайдеров называет то же поле reasoning_content
            if (string.IsNullOrEmpty(chunk.Reasoning) &&
                delta.TryGetProperty("reasoning_content", out var reasoningContentElement) &&
                reasoningContentElement.ValueKind == JsonValueKind.String)
            {
                chunk.Reasoning = reasoningContentElement.GetString();
            }

            // Парсим текстовый контент (delta.content)
            if (delta.TryGetProperty("content", out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.String)
            {
                chunk.Content = contentElement.GetString();
            }

            // Парсим tool_calls (delta.tool_calls) - Function Calling
            if (delta.TryGetProperty("tool_calls", out var toolCallsElement) &&
                toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                var deltas = new List<ToolCallDelta>();
                foreach (var tcEl in toolCallsElement.EnumerateArray())
                {
                    var toolDelta = new ToolCallDelta
                    {
                        Index = tcEl.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : 0,
                    };

                    if (tcEl.TryGetProperty("id", out var idEl))
                        toolDelta.Id = idEl.GetString();
                    if (tcEl.TryGetProperty("type", out var typeEl2))
                        toolDelta.Type = typeEl2.GetString();

                    if (tcEl.TryGetProperty("function", out var fnEl))
                    {
                        if (fnEl.TryGetProperty("name", out var nameEl))
                            toolDelta.FunctionName = nameEl.GetString();
                        if (fnEl.TryGetProperty("arguments", out var argsEl))
                            toolDelta.ArgumentsFragment = argsEl.GetString();
                    }

                    deltas.Add(toolDelta);
                }

                chunk.ToolCalls = deltas;
            }

            // Парсим изображения (delta.images) - для Vision моделей
            // Изображения приходят набором целиком, поэтому кадр несёт весь набор, а не прирост
            if (delta.TryGetProperty("images", out var imagesElement) &&
                imagesElement.ValueKind == JsonValueKind.Array)
            {
                var images = new List<ImageInfo>();

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
                        images.Add(imageInfo);
                        Log.Debug($"ChatLLMApi: Обновлено изображение, index={imageInfo.Index}, type={imageInfo.Type}");
                    }
                }

                chunk.Images = images;
            }

            return chunk;
        }
        catch (JsonException ex)
        {
            // Пропускаем невалидные JSON чанки
            Log.Warning(ex, $"ChatLLMApi ParseStreamChunk: невалидный JSON chunk");
            return null;
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

        // Парсим cached_tokens из prompt_tokens_details — по ним считается скидка за кеш промпта
        if (usageElement.TryGetProperty("prompt_tokens_details", out var promptDetails) &&
            promptDetails.ValueKind == JsonValueKind.Object)
        {
            if (promptDetails.TryGetProperty("cached_tokens", out var cachedTokens))
                usage.CachedTokens = cachedTokens.GetInt32();
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
