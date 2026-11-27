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
using AI.LLM.Infrastructure.Resilience;
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

        // Один клиент провайдера на оба случая: с прокси и без. Ключ — пулом из одного ключа, тем же путём,
        // что и у приложения с несколькими ключами (конструктор с IWebAPIClient и ProviderHttpClient).
        var client = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Keys = ApiKeyPool.SingleOrNone(apiKey),
            Proxies = proxies,
        });
        client.OnProxyError += LLMApi_OnProxyError;
        _webApi = client;
    }

    /// <summary>
    /// Правило повторов запроса к модели: по умолчанию <see cref="RetryPolicy.Default"/>. Запрос к модели ничего не
    /// меняет в мире, поэтому повтор безопасен (<see cref="RepeatSafety.Safe"/>).
    /// </summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default;

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
        RetryPolicy retry = Retry ?? RetryPolicy.Default;

        // Повторяет правило RetryPolicy: только временный сбой, пауза по Retry-After или нарастающая.
        // Неверный ключ, пустой счёт и переполнение окна сразу уходят наверх; смену ключа и маршрута
        // делает клиент провайдера, не этот цикл.
        return await retry.RunAsync(
            (attempt, token) => AttemptAsync(context, sendData, onChunk, attempt, retry.MaxAttempts, token),
            RepeatSafety.Safe,
            cancellationToken);
    }

    /// <summary>Одна попытка: ответ сервера или <see cref="LlmRequestException"/> с причиной.</summary>
    private async Task<ChatCompletionsResponse> AttemptAsync(
        IEnumerable<LLMMessage> context, SendDataLLM sendData, Action<LLMStreamChunk> onChunk,
        int attempt, int maxAttempts, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _webApi.PostAsJsonAsync(ApiUrl, sendData, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                LlmRequestException error = await CreateHttpErrorException(attempt - 1, response, context, cancellationToken, maxAttempts);
                Log.Warning(error.InnerException, "ChatLLMApi: {Message}", error.Message);
                throw error;
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
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            // Клиент отправки сообщил код ответа исключением (прежний ProxyHTTPClient): код не теряем.
            throw LlmRequestException.FromResponse(status, ex.Message, null, $"Attempt #{attempt}/{maxAttempts}");
        }
        catch (Exception ex)
        {
            var sendDataRaw = JsonConvert.SerializeObject(sendData).TruncateForLogging();
            Log.Error(ex, $"ChatLLMApi SendWithContext Exception, ApiUrl={ApiUrl}, ModelName={ModelName}, SendData={sendDataRaw}");

            throw LlmRequestException.NoResponse(ex, await CreateProcessingErrorException(
                attempt - 1,
                ex,
                context,
                sendData,
                cancellationToken));
        }
    }
}
