using AI.LLM.API.LLMAPI;
using AI.LLM.Infrastructure.Extensions;
using System.Net;
using System.Net.Http.Headers;

namespace AI.LLM.Infrastructure.Http;

/// <summary>
/// Прежний клиент с пулом прокси. Адаптер над <see cref="ProviderHttpClient"/>: маршрут, карантин и тайм-ауты — там.
/// </summary>
/// <remarks>
/// Сохранено прежнее поведение: неудачный ответ сервера — <see cref="HttpRequestException"/> с кодом, все прокси в
/// карантине — <see cref="InvalidOperationException"/>, прокси уходит в карантин на 24 часа после восьми сетевых
/// сбоев подряд. Изменилось одно: ответ сервера (429, 5xx) прокси больше не винит — маршрут меняет только сбой сети.
/// </remarks>
[Serializable]
[Obsolete("Используйте ProviderHttpClient: ключи пулом, прокси пулом, одно правило карантина.")]
public class ProxyHTTPClient : IWebAPIClient
{
    private readonly ProviderHttpClient _inner;
    private readonly List<WebProxy> _proxies;

    /// <summary>Дефолтный таймаут для LLM запросов (для долгих запросов: o1, reasoning).</summary>
    internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(18);

    /// <summary>Срабатывает когда прокси упал.</summary>
    public event EventHandler<ProxyErrorEventArgs> OnProxyError;

    /// <summary>Срабатывает при отладочных событиях.</summary>
    public event EventHandler<DebugLogEventArgs> OnDebugLog;

    /// <summary>Заголовок авторизации запросов.</summary>
    public AuthenticationHeaderValue Authentication
    {
        get => _inner.Authentication;
        set => _inner.Authentication = value;
    }

    /// <summary>Создаем клиент со списком прокси.</summary>
    public ProxyHTTPClient(IEnumerable<WebProxy> proxies, ProxyHTTPClientOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(proxies);

        _proxies = proxies.ToList();
        if (_proxies.Count == 0)
            throw new ArgumentException("Список прокси не может быть пустым.", nameof(proxies));
        if (_proxies.Any(p => p?.Address == null))
            throw new ArgumentException("Один из прокси имеет некорректный адрес (null).", nameof(proxies));

        options ??= new ProxyHTTPClientOptions();
        options.Cookie ??= new CookieContainer();

        _inner = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Proxies = _proxies,
            ProxyFailuresBeforeCooldown = 8,
            ProxyCooldown = TimeSpan.FromHours(24),
            MaxProxiesPerRequest = 5,
            RequireAvailableProxy = true,
            ConnectTimeout = options.ConnectTimeout,
            RequestTimeout = options.RequestTimeout,
            GlobalTimeout = options.GlobalTimeout,
            MaxConcurrentRequests = options.MaxConcurrentRequests,
            UserAgent = options.UserAgent,
            DefaultHeaders = options.DefaultHeaders,
            AllowAutoRedirect = options.AllowAutoRedirect,
            UseCookies = options.UseCookies,
            Cookie = options.Cookie,
            Decompression = options.DecompressionMethods,
            DisableCertificateValidation = options.DisableCertificateValidation,
            EnableDebugLogging = options.EnableDebugLogging,
        });
        _inner.OnProxyError += (_, e) => OnProxyError?.Invoke(this, e);
        _inner.OnDebugLog += (_, e) => OnDebugLog?.Invoke(this, e);
    }

    /// <summary>Создаем клиент со списком прокси и API ключом.</summary>
    public ProxyHTTPClient(IEnumerable<WebProxy> proxies, string apiKey, ProxyHTTPClientOptions options = null)
        : this(proxies, options)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API ключ не может быть пустым.", nameof(apiKey));

        Authentication = Resilience.ApiKeyPool.Bearer(apiKey);
    }

    /// <summary>Загружаем прокси из JSON файла.</summary>
    public ProxyHTTPClient(string proxyPath, ProxyHTTPClientOptions options = null)
        : this(ProxyList.FromJson(proxyPath), options)
    {
    }

    /// <summary>Загружаем прокси из файла и сразу добавляем API ключ.</summary>
    public ProxyHTTPClient(string proxyPath, string apiKey, ProxyHTTPClientOptions options = null)
        : this(ProxyList.FromJson(proxyPath), apiKey, options)
    {
    }

    /// <summary>Отправляем POST запрос через рабочий прокси; неудачный ответ — <see cref="HttpRequestException"/> с кодом.</summary>
    public async Task<HttpResponseMessage> PostAsJsonAsync(
        string apiUrl,
        SendDataLLM sendData,
        CancellationToken? cancellationToken = null)
    {
        HttpResponseMessage response;
        try
        {
            response = await _inner.PostAsJsonAsync(apiUrl, sendData, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or TimeoutException or HttpRequestException
                                       or InvalidOperationException or ArgumentException))
        {
            throw new HttpRequestException("An error occurred while sending the request", ex);
        }

        if (response.IsSuccessStatusCode)
            return response;

        HttpStatusCode statusCode = response.StatusCode;
        string errorContent;
        using (response)
            errorContent = (await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? "").TruncateForLogging();

        throw new HttpRequestException($"Ошибка {statusCode}: {errorContent}", null, statusCode);
    }

    /// <summary>Читаем список прокси из JSON файла.</summary>
    public static List<WebProxy> LoadProxiesFromJson(string jsonFilePath) => ProxyList.FromJson(jsonFilePath);

    /// <summary>Собираем WebProxy из данных.</summary>
    public static WebProxy GetWebProxy(ProxyData proxyData) => ProxyList.ToWebProxy(proxyData);

    /// <summary>Показываем статистику по всем прокси.</summary>
    public IEnumerable<ProxyStatistics> GetProxyStatistics() =>
        _inner.ProxyHealth().Select(h => new ProxyStatistics
        {
            ProxyAddress = $"{_inner.ProxyAt(h.Index).Address}",
            FailureCount = h.ConsecutiveFailures,
            LastSuccess = h.LastSuccess?.UtcDateTime,
            LastFailure = h.LastFailure?.UtcDateTime,
            IsBlacklisted = h.IsCooling,
            LastException = h.LastReason,
        }).ToList();

    /// <summary>Обнуляем всю статистику и даем всем прокси чистый лист.</summary>
    public void ResetProxyStatistics() => _inner.ResetProxies();

    /// <summary>Проверяем корректность всех прокси (диагностический метод).</summary>
    public List<string> ValidateAllProxies()
    {
        var errors = new List<string>();

        foreach (WebProxy proxy in _proxies)
        {
            Uri uri = proxy.Address;
            if (string.IsNullOrEmpty(uri.Host))
                errors.Add($"Прокси с пустым хостом: {uri}");
            if (uri.Port <= 0)
                errors.Add($"Прокси с некорректным портом: {uri}");
        }

        return errors;
    }

    public void Dispose() => _inner.Dispose();
}
