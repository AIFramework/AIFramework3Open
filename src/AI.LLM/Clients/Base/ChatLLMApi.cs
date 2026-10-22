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
    public ChatLLMApi(string apiKey, string modelName, string prompt,
        IEnumerable<WebProxy> proxies = null)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentNullException(nameof(modelName), "Имя модели не может быть пустым");

        ModelName = modelName;
        _prompt = prompt;
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
    /// Апи поверх ГОТОВОГО http-клиента: ключом, маршрутами и повторами распоряжается вызывающий.
    /// </summary>
    /// <remarks>
    /// Зачем отдельный конструктор. Обычный принимает ключ строкой, а прокси — списком, и то и
    /// другое замирает на всю жизнь объекта: сменить отказавший ключ или увести запрос на другой
    /// маршрут внутри вызова уже нельзя. Приложению, у которого есть свой пул ключей и свой пул
    /// маршрутов (с карантином, ручным выключением и панелью здоровья), нужен именно этот вид —
    /// иначе рядом с его контуром появляется второй, ничего о первом не знающий.
    /// <para>
    /// Владение клиентом остаётся у вызывающего: он живёт дольше одного <see cref="ChatLLMApi"/>
    /// и обычно общий на процесс, поэтому <see cref="IDisposable"/> здесь не трогаем.
    /// </para>
    /// </remarks>
    /// <param name="webApi">Клиент отправки: ключи, прокси, ротация и повторы — на его стороне.</param>
    /// <param name="modelName">Имя модели у провайдера.</param>
    /// <param name="prompt">Системный промпт для запросов без контекста.</param>
    public ChatLLMApi(IWebAPIClient webApi, string modelName, string prompt = "")
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentNullException(nameof(modelName), "Имя модели не может быть пустым");

        _webApi = webApi ?? throw new ArgumentNullException(nameof(webApi));
        ModelName = modelName;
        _prompt = prompt;
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
    public Task<ChatCompletionsResponse> SendWithContextAsync(
    IEnumerable<LLMMessage> context,
    GenerateSettings generateSettings = null,
    CancellationToken cancellationToken = default) =>
        SendWithContextAsync(context, generateSettings, null, cancellationToken);

    /// <summary>
    /// То же, что <see cref="SendWithContextAsync(IEnumerable{LLMMessage}, GenerateSettings, CancellationToken)"/>,
    /// но фрагменты ответа отдаются ещё и по мере прихода — чтобы человек видел ответ, пока модель пишет.
    /// </summary>
    /// <param name="context">Контекст сообщений.</param>
    /// <param name="generateSettings">Настройки генерации.</param>
    /// <param name="onChunk">
    /// Фрагмент потока; <c>null</c> — не нужны. Если запрос оборвался посреди ответа и повторился,
    /// фрагменты придут заново с начала.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task<ChatCompletionsResponse> SendWithContextAsync(
    IEnumerable<LLMMessage> context,
    GenerateSettings generateSettings,
    Action<LLMStreamChunk> onChunk,
    CancellationToken cancellationToken = default)
    {
        var sendData = BuildStreamingSendData(context, generateSettings);

        // Повторяется только то, что повтор может вылечить: перегрузка, лимит частоты, сбой
        // сервера, сеть. Неверный ключ, пустой счёт и переполнение окна сразу уходят наверх —
        // раньше они повторялись вслепую, а Retry-After не учитывался.
        const int maxAttempts = 3;
        const int initialDelaySeconds = 1;
        LlmRequestException lastException = null;

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
                        cancellationToken,
                        maxAttempts);
                    Log.Warning(lastException.InnerException, "ChatLLMApi: {Message}", lastException.Message);

                    if (!lastException.IsRetryable)
                        throw lastException;

                    if (attempt < maxAttempts - 1)
                        await DelayAsync(lastException.RetryAfter, attempt, initialDelaySeconds, cancellationToken);

                    continue;
                }

                // ВСЕГДА обрабатываем как streaming: он включён принудительно в
                // BuildStreamingSendData ради раннего обнаружения зависших запросов.
                return await ProcessStreamResponseInternal(response, onChunk, cancellationToken);
            }
            catch (LlmRequestException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var sendDataRaw = JsonConvert.SerializeObject(sendData).TruncateForLogging();
                Log.Error(ex, $"ChatLLMApi SendWithContext Exception, ApiUrl={ApiUrl}, ModelName={ModelName}, SendData={sendDataRaw}");

                lastException = LlmRequestException.NoResponse(ex, await CreateProcessingErrorException(
                    attempt,
                    ex,
                    context,
                    sendData,
                    cancellationToken));

                // Переполнение окна повтором не лечится.
                if (!lastException.IsRetryable)
                    throw lastException;

                if (attempt < maxAttempts - 1)
                    await DelayWithExponentialBackoff(attempt, initialDelaySeconds, cancellationToken);
            }
        }

        throw lastException;
    }

    /// <summary>Пауза перед повтором: сколько сказал сервер (<c>Retry-After</c>), иначе экспоненциально.</summary>
    private static Task DelayAsync(TimeSpan? retryAfter, int attempt, int initialDelaySeconds, CancellationToken cancellationToken) =>
        retryAfter is TimeSpan wait
            ? Task.Delay(wait, cancellationToken)
            : DelayWithExponentialBackoff(attempt, initialDelaySeconds, cancellationToken);

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
