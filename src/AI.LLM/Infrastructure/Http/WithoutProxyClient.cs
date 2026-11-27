using AI.LLM.API.LLMAPI;
using System.Net.Http.Headers;

namespace AI.LLM.Infrastructure.Http;

/// <summary>
/// Прежний клиент без прокси. Адаптер над <see cref="ProviderHttpClient"/> с прямым маршрутом: неудачный ответ
/// возвращается как есть, сбой отправки — <see cref="HttpRequestException"/>.
/// </summary>
[Serializable]
[Obsolete("Используйте ProviderHttpClient: ключи пулом, прокси пулом, одно правило карантина.")]
public class WithoutProxyClient : IWebAPIClient
{
    private readonly ProviderHttpClient _inner = new();

    /// <summary>Заголовок авторизации; ключ <see cref="ApiKey"/>, если задан, главнее.</summary>
    public AuthenticationHeaderValue Authentication { get; set; }

    /// <summary>Ключ запросов.</summary>
    public string ApiKey { get; set; }

    /// <summary>Клиент с ключом.</summary>
    /// <param name="apiKey">Ключ; пусто — без ключа.</param>
    public WithoutProxyClient(string apiKey)
    {
        ApiKey = apiKey;
        if (!string.IsNullOrEmpty(apiKey))
            Authentication = Resilience.ApiKeyPool.Bearer(apiKey);
    }

    /// <summary>Клиент без ключа.</summary>
    public WithoutProxyClient()
    {
    }

    /// <summary>Отправляет POST с JSON; неудачный ответ возвращается как есть.</summary>
    /// <exception cref="HttpRequestException">Сбой отправки.</exception>
    public async Task<HttpResponseMessage> PostAsJsonAsync(string apiUrl, SendDataLLM sendData, CancellationToken? cancellationToken = default)
    {
        _inner.Authentication = !string.IsNullOrEmpty(ApiKey) ? Resilience.ApiKeyPool.Bearer(ApiKey) : Authentication;
        try
        {
            return await _inner.PostAsJsonAsync(apiUrl, sendData, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or TimeoutException or ArgumentException))
        {
            throw new HttpRequestException("An error occurred while sending the request", ex);
        }
    }

    /// <summary>Асинхронная фабрика: то же, что конструктор.</summary>
    /// <param name="apiKey">Ключ.</param>
    public static Task<WithoutProxyClient> CreateAsync(string apiKey) => Task.FromResult(new WithoutProxyClient(apiKey));

    public void Dispose() => _inner.Dispose();
}
