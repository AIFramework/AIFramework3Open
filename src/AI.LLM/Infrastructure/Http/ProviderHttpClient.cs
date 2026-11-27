#nullable enable

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AI.LLM.API.LLMAPI;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Infrastructure.Resilience;

namespace AI.LLM.Infrastructure.Http;

/// <summary>
/// HTTP-клиент провайдера — один на все клиенты AI.LLM: ключ из <see cref="ApiKeyPool"/>, маршрут из пула прокси,
/// тайм-ауты, постоянные заголовки. Отказ по ключу (401/402/403/429) меняет ключ, сетевой сбой меняет прокси — оба
/// по одному правилу карантина (<see cref="CooldownPool{T}"/>).
/// </summary>
/// <remarks>
/// <para>
/// Ротация — не повтор: ключ и маршрут меняются внутри одного вызова и попытки <see cref="RetryPolicy"/> не тратят.
/// Код ответа, который ключ не винит (5xx, 400), клиент отдаёт вызывающему ответом, а не исключением: решение о
/// повторе — за правилом <see cref="RetryPolicy"/>. Ответ сервера прокси тоже не винит: сменить маршрут велит только
/// сбой сети.
/// </para>
/// <para>
/// Запрос строится заново на каждую попытку (<c>createRequest</c>): тело <see cref="HttpRequestMessage"/> одноразовое.
/// </para>
/// </remarks>
public sealed class ProviderHttpClient : IWebAPIClient
{
    private readonly ProviderHttpClientOptions _options;
    private readonly CooldownPool<WebProxy>? _proxies;
    private readonly ConcurrentDictionary<int, HttpClient> _proxyClients = new();
    private readonly HttpClient _direct;
    private readonly bool _ownsDirect;
    private readonly SemaphoreSlim? _semaphore;

    /// <summary>Клиент по настройкам.</summary>
    /// <param name="options">Настройки; <see langword="null"/>: прямой маршрут без ключа.</param>
    /// <exception cref="ArgumentException">Готовый клиент или обработчик вместе с прокси.</exception>
    public ProviderHttpClient(ProviderHttpClientOptions? options = null)
    {
        _options = options ?? new ProviderHttpClientOptions();
        WebProxy[] proxies = [.. _options.Proxies ?? []];
        if (proxies.Any(p => p?.Address == null))
            throw new ArgumentException("Один из прокси имеет некорректный адрес (null).", nameof(options));
        if (proxies.Length > 0 && (_options.Client != null || _options.Handler != null))
            throw new ArgumentException("Готовый клиент или обработчик задают прямой маршрут и с прокси не сочетаются.", nameof(options));

        if (proxies.Length > 0)
            _proxies = new CooldownPool<WebProxy>(proxies, _options.ProxyFailuresBeforeCooldown, _options.Time);

        _ownsDirect = _options.Client == null;
        _direct = _options.Client ?? Configure(_options.Handler != null
            ? new HttpClient(_options.Handler, disposeHandler: true)
            : new HttpClient(CreateHandler(proxy: null), disposeHandler: true));

        if (_options.MaxConcurrentRequests is { } max)
            _semaphore = new SemaphoreSlim(max, max);
    }

    /// <summary>Сетевой сбой прокси: запрос уйдёт другим маршрутом.</summary>
    public event EventHandler<ProxyErrorEventArgs>? OnProxyError;

    /// <summary>Отладочные события (при <see cref="ProviderHttpClientOptions.EnableDebugLogging"/>).</summary>
    public event EventHandler<DebugLogEventArgs>? OnDebugLog;

    /// <summary>Заголовок авторизации, если пула ключей нет (совместимость с <see cref="IWebAPIClient"/>).</summary>
    public AuthenticationHeaderValue? Authentication { get; set; }

    /// <summary>Пул ключей; <see langword="null"/>: ключей нет.</summary>
    public ApiKeyPool? Keys => _options.Keys;

    /// <summary>Здоровье прокси; пусто: прямой маршрут.</summary>
    public IReadOnlyList<PoolEntryHealth> ProxyHealth() => _proxies?.Snapshot() ?? [];

    /// <summary>Прокси по номеру из <see cref="ProxyHealth"/>.</summary>
    /// <param name="index">Номер.</param>
    public WebProxy ProxyAt(int index) => (_proxies ?? throw new InvalidOperationException("Прокси не заданы.")).ItemAt(index);

    /// <summary>Чистый лист всем прокси.</summary>
    public void ResetProxies() => _proxies?.Reset();

    /// <inheritdoc />
    public Task<HttpResponseMessage> PostAsJsonAsync(string apiUrl, SendDataLLM sendData, CancellationToken? cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new ArgumentException("apiUrl не может быть пустым.", nameof(apiUrl));
        ArgumentNullException.ThrowIfNull(sendData);

        string json = sendData.GetJson();
        Debug($"Отправляемый JSON: {json.TruncateForLogging()}");
        Debug($"URL: {apiUrl}");

        return SendAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, new Uri(apiUrl))
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("X-Version", "1");
                return request;
            },
            sendData.Stream == true ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            cancellationToken ?? CancellationToken.None);
    }

    /// <summary>
    /// Отправляет запрос с ротацией ключа и маршрута. Возвращает ответ сервера, в том числе неудачный, если ключ в нём
    /// не виноват или другого ключа нет.
    /// </summary>
    /// <param name="createRequest">Сборка запроса; вызывается на каждую попытку.</param>
    /// <param name="completion">Когда считать ответ полученным (поток — по заголовкам).</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <exception cref="TimeoutException">Сервер не прислал заголовков в срок, а другого маршрута нет.</exception>
    /// <exception cref="HttpRequestException">Сеть, а другого маршрута нет.</exception>
    /// <exception cref="InvalidOperationException">Все прокси в карантине при <see cref="ProviderHttpClientOptions.RequireAvailableProxy"/>.</exception>
    public async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> createRequest, HttpCompletionOption completion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createRequest);

        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(_options.GlobalTimeout is { } global && global < _options.RequestTimeout ? global : _options.RequestTimeout);
        CancellationToken token = total.Token;

        if (_semaphore != null)
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await RotateAsync(createRequest, completion, cancellationToken, token).ConfigureAwait(false);
        }
        finally
        {
            _semaphore?.Release();
        }
    }

    /// <summary>POST JSON и разбор ответа; неудачный ответ — <see cref="HttpRequestException"/> с кодом и сроком повтора.</summary>
    /// <typeparam name="T">Тип ответа.</typeparam>
    /// <param name="url">Адрес (относительный — от <see cref="ProviderHttpClientOptions.BaseAddress"/>).</param>
    /// <param name="body">Тело запроса.</param>
    /// <param name="what">Кто отвечает — для текста ошибки.</param>
    /// <param name="cancellationToken">Отмена.</param>
    public async Task<T?> PostJsonAsync<T>(string url, object body, string what, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        using HttpResponseMessage response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, body.GetType()) },
            HttpCompletionOption.ResponseContentRead,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, what, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Неудачный ответ — <see cref="HttpRequestException"/> с кодом и сроком <c>Retry-After</c> (для
    /// <see cref="RetryPolicy"/>); тело ответа — в тексте, обрезанным.
    /// </summary>
    /// <param name="response">Ответ.</param>
    /// <param name="what">Кто отвечает — для текста ошибки.</param>
    /// <param name="cancellationToken">Отмена чтения тела.</param>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.IsSuccessStatusCode)
            return;

        string body = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) ?? "").TruncateForLogging();
        throw RetryPolicy.WithRetryAfter(
            new HttpRequestException($"{what} вернул {(int)response.StatusCode}: {body}", null, response.StatusCode),
            RetryPolicy.RetryAfterOf(response.Headers.RetryAfter));
    }

    private async Task<HttpResponseMessage> RotateAsync(
        Func<HttpRequestMessage> createRequest, HttpCompletionOption completion, CancellationToken caller, CancellationToken token)
    {
        ApiKeyPool? keys = _options.Keys;
        PoolLease<string>? key = keys?.Lease();
        var triedKeys = new HashSet<int>();
        var triedProxies = new HashSet<int>();
        PoolLease<WebProxy>? proxy = FirstProxy();

        while (true)
        {
            using HttpRequestMessage request = createRequest();
            if (key is { } k)
                keys!.Authorize(request, k);
            else if (Authentication != null)
                request.Headers.Authorization = Authentication;

            HttpResponseMessage response;
            try
            {
                response = await SendOnceAsync(proxy, request, completion, caller, token).ConfigureAwait(false);
            }
            catch (Exception error) when (proxy is { } p && IsNetwork(error, caller))
            {
                triedProxies.Add(p.Index);
                _proxies!.ReportFailure(p, _options.ProxyCooldown, error.GetType().Name);
                OnProxyError?.Invoke(this, new ProxyErrorEventArgs(p.Item, error));
                if (triedProxies.Count < _options.MaxProxiesPerRequest && _proxies.TryLeaseAvailable(out PoolLease<WebProxy> next, triedProxies))
                {
                    proxy = next;
                    continue;
                }

                throw;
            }

            if (proxy is { } ok)
                _proxies!.ReportSuccess(ok);

            if (key is { } used)
            {
                if (ApiKeyPool.FailureOf(response.StatusCode) is { } failure)
                {
                    triedKeys.Add(used.Index);
                    keys!.ReportFailure(used, failure, RetryPolicy.RetryAfterOf(response.Headers.RetryAfter, _options.Time));
                    Debug($"Ключ {ApiKeyPool.Mask(used.Item)} отказал ({(int)response.StatusCode}), в карантине");
                    if (keys.TryLeaseAvailable(out PoolLease<string> nextKey, triedKeys))
                    {
                        response.Dispose();
                        key = nextKey;
                        continue;
                    }
                }
                else if (response.IsSuccessStatusCode)
                {
                    keys!.ReportSuccess(used);
                }
            }

            return response;
        }
    }

    private PoolLease<WebProxy>? FirstProxy()
    {
        if (_proxies == null)
            return null;
        if (_proxies.TryLeaseAvailable(out PoolLease<WebProxy> lease))
            return lease;
        if (_options.RequireAvailableProxy)
            throw new InvalidOperationException("Нет доступных прокси. Все прокси в карантине.");
        return _proxies.Lease();
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        PoolLease<WebProxy>? proxy, HttpRequestMessage request, HttpCompletionOption completion, CancellationToken caller, CancellationToken token)
    {
        HttpClient client = proxy is { } p
            ? _proxyClients.GetOrAdd(p.Index, _ => Configure(new HttpClient(CreateHandler(p.Item), disposeHandler: true)))
            : _direct;
        if (proxy is { } shown)
            Debug($"Proxy: {shown.Item.Address}");

        using var headers = new CancellationTokenSource(_options.ResponseHeadersTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, headers.Token);
        try
        {
            return await client.SendAsync(request, completion, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (headers.IsCancellationRequested && !caller.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Таймаут ожидания ответа от сервера ({_options.ResponseHeadersTimeout.TotalSeconds} сек). URL: {request.RequestUri}");
        }
    }

    /// <summary>Сбой сети: другой маршрут может помочь. Отмена вызывающим — не сбой.</summary>
    private static bool IsNetwork(Exception error, CancellationToken caller) =>
        !caller.IsCancellationRequested
        && error is HttpRequestException { StatusCode: null } or TimeoutException or IOException or OperationCanceledException;

    private HttpClient Configure(HttpClient client)
    {
        if (_options.BaseAddress != null)
            client.BaseAddress = _options.BaseAddress;
        client.Timeout = _options.RequestTimeout;
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _options.UserAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        foreach (KeyValuePair<string, string> header in _options.DefaultHeaders ?? [])
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        return client;
    }

    private SocketsHttpHandler CreateHandler(WebProxy? proxy)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = proxy != null,
            Proxy = proxy,
            AllowAutoRedirect = _options.AllowAutoRedirect,
            UseCookies = _options.UseCookies,
            CookieContainer = _options.Cookie ?? new CookieContainer(),
            MaxAutomaticRedirections = 3,
            ConnectTimeout = _options.ConnectTimeout,
            Expect100ContinueTimeout = TimeSpan.FromSeconds(5),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
            PooledConnectionLifetime = _options.RequestTimeout,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            MaxConnectionsPerServer = 20,
            ResponseDrainTimeout = TimeSpan.FromSeconds(30),
        };

        if (_options.DisableCertificateValidation)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        if (_options.Decompression is { } decompression)
            handler.AutomaticDecompression = decompression;
        return handler;
    }

    private void Debug(string message)
    {
        if (_options.EnableDebugLogging)
            OnDebugLog?.Invoke(this, new DebugLogEventArgs(message));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _semaphore?.Dispose();
        foreach (HttpClient client in _proxyClients.Values)
            client.Dispose();
        _proxyClients.Clear();
        if (_ownsDirect)
            _direct.Dispose();
    }
}
