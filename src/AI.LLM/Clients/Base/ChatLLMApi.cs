using System.Net;
using System.Net.Http.Json;
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
using AI.LLM.Services.Prompts;
using Newtonsoft.Json;
using Serilog;

namespace AI.LLM.Clients.Base;

/// <summary>
/// Апи для отправки запросов на LLM по стандарту OpenAI (также поддерживается DeepSeek, VLLM, OpenRouter, Replicate и тп.)
/// </summary>
[Serializable]
public partial class ChatLLMApi
{
    private readonly IWebAPIClient _webApi;
    private readonly string _prompt;
    private readonly IStreamHandler _streamSender;

    public virtual string ModelName { get; set; }
    public virtual string ApiUrl { get; set; }
    public virtual string TokenizeApiUrl { get; set; }

    public StreamOptions StreamOptions { get; set; }

    /// <summary>
    /// Предпочтительный провайдер (используется в OpenRouter)
    /// </summary>
    public virtual ProviderPreference PreferredProvider { get; set; }

    /// <summary>
    /// Настройки для мониторинга таймаута простоя между чанками данных (по умолчанию 70 секунд)
    /// </summary>
    public IdleTimeoutSettings IdleTimeoutSettings { get; set; } = IdleTimeoutSettings.Default;

    /// <summary>
    /// Таймаут на одну операцию ReadLineAsync (по умолчанию 60 секунд)
    /// </summary>
    private static readonly TimeSpan ReadLineTimeout = TimeSpan.FromSeconds(60);

    public event Action<string> ProxyInfo;


    /// <summary>
    /// Апи для отправки запросов на LLM по стандарту OpenAI (также поддерживается DeepSeek, VLLM, OpenRouter, Replicate и тп.)
    /// </summary>
    public ChatLLMApi(string apiKey, string modelName, string prompt, IStreamHandler streamSender = null,
        IEnumerable<WebProxy> proxies = null)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentNullException(nameof(modelName), "Имя модели не может быть пустым");

        ModelName = modelName;
        _prompt = prompt;
        _streamSender = streamSender;
        // Возможно стоит заменить на логер
        ProxyInfo += ChatLLMApi_ProxyInfo;

        if (proxies != null && proxies.Any())
        {
            _webApi = new ProxyHTTPClient(proxies, apiKey);
            (_webApi as ProxyHTTPClient).OnProxyError += LLMApi_OnProxyError;
        }
        else 
        { 
            _webApi = new WithoutProxyClient(apiKey);
        }

    }

    

    /// <summary>
    /// Определяет число токенов в запросе
    /// </summary>
    /// <param name="messages">Запрос</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default)
    {
        SendDataLLM sendData = new SendDataLLM(ModelName);
        sendData.SetMessages(messages);

        // HTTP запрос использует глобальный cancellationToken (tokenize - быстрая операция)
        using var response = await _webApi.PostAsJsonAsync(TokenizeApiUrl, sendData, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        // Только чтение JSON ограничиваем 60 секундами
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var result = await response.Content.ReadFromJsonAsync<TokenizeResult>(linkedCts.Token);

        return result?.Count ?? 0;
    }

    /// <summary>
    /// Отправка сообщения без контекста (потокобезопасная версия)
    /// </summary>
    /// <param name="text">Текст запроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Возвращает текст ответа</returns>
    public async Task<string> SendWithoutContextTextAsync(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default) =>
        (await SendWithoutContextAsync(text, generateSettings, cancellationToken)).Choices[0].Message.Content.ToString();

    /// <summary>
    /// Отправка сообщения с учетом контекста (потокобезопасная версия).
    /// </summary>
    /// <param name="context">Контекст сообщений LLM.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Возвращает текст ответа.</returns>
    public async Task<string> SendWithContextTextAsync(IEnumerable<LLMMessage> context, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default) =>
        (await SendWithContextAsync(context, generateSettings, cancellationToken)).Choices[0].Message.Content.ToString();

    /// <summary>
    /// Отправка сообщения без учета контекста, с заданным началом ответа
    /// </summary>
    /// <param name="context">Контекст сообщений LLM.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Возвращает текст ответа.</returns>
    public async Task<string> SendWithoutContextWithStartReturnTextAsync(string text, string answerStart, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default) =>
        (await SendWithoutContextWithStartAsync(text, answerStart, generateSettings, cancellationToken)).Choices[0].Message.Content.ToString();

    /// <summary>
    /// Отправка сообщения без учета контекста
    /// (потокобезопасная версия)
    /// </summary>
    /// <param name="text"></param>
    /// <returns>Возвращает ChatCompletionsResponse с дополнительной информацией </returns>
    public async Task<ChatCompletionsResponse> SendWithoutContextAsync(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        List<LLMMessage> context = [
            LLMMessage.CreateMessage(Roles.System, _prompt),
            LLMMessage.CreateMessage(Roles.User, text)
            ];

        return await SendWithContextAsync(context, generateSettings, cancellationToken);

    }

    /// <summary>
    /// Отправка сообщения без учета контекста
    /// (потокобезопасная версия)
    /// </summary>
    /// <param name="text">Начало ответа</param>
    /// <param name="answerStart">Начало ответа</param>
    /// <returns>Возвращает ChatCompletionsResponse с дополнительной информацией </returns>
    public async Task<ChatCompletionsResponse> SendWithoutContextWithStartAsync(string text, string answerStart, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        List<LLMMessage> context = [
            LLMMessage.CreateMessage(Roles.System, _prompt),
            LLMMessage.CreateMessage(Roles.User, text),
            LLMMessage.CreateMessage(Roles.Assistant, answerStart)
            ];

        return await SendWithContextAsync(context, generateSettings, cancellationToken);

    }





    /// <summary>
    /// Отправка сообщения без учета контекста
    /// (потокобезопасная версия)
    /// ВНУТРИ ВСЕГДА ИСПОЛЬЗУЕТ STREAMING для раннего обнаружения зависших запросов!
    /// </summary>
    /// <param name="context">Контекст сообщений</param>
    /// <param name="generateSettings">Настройки генерации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Возвращает ChatCompletionsResponse с дополнительной информацией </returns>
    public async Task<ChatCompletionsResponse> SendWithContextAsync(
    IEnumerable<LLMMessage> context,
    GenerateSettings generateSettings = null,
    CancellationToken cancellationToken = default)
    {
        generateSettings = Validate(generateSettings);

        if (context == null)
            throw new ArgumentException("Контекст не может быть null.", nameof(context));

        // ВАЖНО: Принудительно включаем streaming для раннего обнаружения зависших запросов!
        // Даже если пользователь не указал streamId, мы создаем временный для внутреннего использования
        if (string.IsNullOrEmpty(generateSettings.StreamId))
        {
            // Создаем временный streamId для включения streaming
            generateSettings = new GenerateSettings(
                temperature: generateSettings.Temperature ?? 0.1,
                repetitionPenalty: generateSettings.RepetitionPenalty,
                topP: generateSettings.TopP,
                topK: generateSettings.TopK,
                minTokens: generateSettings.MinTokens,
                maxTokens: generateSettings.MaxTokens,
                streamId: Guid.NewGuid().ToString(), // <- Включаем streaming!
                reasoningEffort: generateSettings.ReasoningEffort,
                streamMethod: "StreamMessage"
            )
            {
                // Копируем дополнительные свойства через инициализатор
                ReasoningSettings = generateSettings.ReasoningSettings,
                LogProbs = generateSettings.LogProbs,
                TopLogprobs = generateSettings.TopLogprobs,
                ResponseFormat = generateSettings.ResponseFormat,
                Tools = generateSettings.Tools,
                ToolChoice = generateSettings.ToolChoice,
            };
        }

        var sendData = new SendDataLLM(ModelName, generateSettings);
        sendData.StreamOptions = StreamOptions;
        sendData.SetMessages(context);

        // Установка провайдера если задан (для OpenRouter)
        if (PreferredProvider != null)
            sendData.Provider = PreferredProvider;
        
        const int maxAttempts = 2;
        const int initialDelaySeconds = 1;
        Exception lastException = new Exception("Базовая ошибка");

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var response = await _webApi.PostAsJsonAsync(ApiUrl, sendData, cancellationToken);

                // Проверка успешности HTTP-запроса
                if (!response.IsSuccessStatusCode)
                {
                    lastException = await CreateHttpErrorException(
                        attempt,
                        response,
                        context,
                        cancellationToken);

                    // Задержка перед следующей попыткой
                    if (attempt < maxAttempts - 1)
                        await DelayWithExponentialBackoff(attempt, initialDelaySeconds, cancellationToken);

                    continue;
                }

                // Обработка успешного ответа
                // if (generateSettings.Stream)
                //    return await ProcessStreamResponse(generateSettings, response);
                // else
                //    return await ProcessStandardResponse(response, cancellationToken);

                // ВСЕГДА обрабатываем как streaming (т.к. мы принудительно его включили)
                // Но используем внутренний метод, не требующий IStreamHandler
                return await ProcessStreamResponseInternal(response, cancellationToken);
            }
            catch (Exception ex)
            {
                var sendDataRaw = JsonConvert.SerializeObject(sendData).TruncateForLogging();
                Log.Error(ex, $"ChatLLMApi SendWithContext Exception, ApiUrl={ApiUrl}, ModelName={ModelName}, SendData={sendDataRaw}");

                lastException = await CreateProcessingErrorException(
                    attempt,
                    ex,
                    context,
                    sendData,
                    cancellationToken);

                // Проверяем на ошибку превышения лимита контекста - это не ретрится
                var exceptionMessage = lastException.ToString();
                if (exceptionMessage.Contains("maximum context length", StringComparison.OrdinalIgnoreCase) ||
                    exceptionMessage.Contains("Please reduce the length", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Context length limit exceeded - no retries will be attempted");
                    throw lastException;
                }

                // Задержка для обработки исключений
                if (attempt < maxAttempts - 1)
                    await DelayWithExponentialBackoff(attempt, initialDelaySeconds, cancellationToken);
            }
        }

        throw lastException;
    }

    /// <summary>
    /// Выполняет задержку с экспоненциальным увеличением времени ожидания
    /// </summary>
    /// <param name="attempt">Номер текущей попытки (начиная с 0)</param>
    /// <param name="initialDelaySeconds">Начальная задержка в секундах</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private static async Task DelayWithExponentialBackoff(
        int attempt,
        int initialDelaySeconds,
        CancellationToken cancellationToken)
    {
        // Экспоненциальная задержка: 1, 2, 4, 8, 16 секунд
        int delaySeconds = initialDelaySeconds * (int)Math.Pow(2, attempt);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
    }
}
