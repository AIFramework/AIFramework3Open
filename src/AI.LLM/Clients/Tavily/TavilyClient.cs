using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using AI.LLM.Clients.Tavily.Models;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Infrastructure.Resilience;

namespace AI.LLM.Clients.Tavily;

public class TavilyClient : IDisposable
{
    public const string Host = "https://api.tavily.com";

    private readonly ProviderHttpClient _http;
    private int _disposed; // 0 = not disposed, 1 = disposed (для Interlocked)

    /// <summary>Правило повторов поиска: две попытки, пауза 2 с.</summary>
    public RetryPolicy SearchRetry { get; set; } = RetryPolicy.Default with { MaxAttempts = 2, InitialDelay = TimeSpan.FromSeconds(2) };

    /// <summary>Правило повторов извлечения: четыре попытки, пауза от 2 с.</summary>
    public RetryPolicy ExtractRetry { get; set; } = RetryPolicy.Default with { MaxAttempts = 4, InitialDelay = TimeSpan.FromSeconds(2) };

    public TavilyClient(string apiKey, WebProxy proxy = null)
        : this(KeyOf(apiKey, handler: null), proxy, handler: null)
    {
    }

    /// <summary>
    /// Клиент с пулом ключей: отказавший ключ (401/402/429) уходит в карантин, запрос идёт со следующим.
    /// </summary>
    /// <param name="keys">Пул ключей Tavily.</param>
    /// <param name="proxy">Прокси; <see langword="null"/>: прямой маршрут.</param>
    public TavilyClient(ApiKeyPool keys, WebProxy proxy = null)
        : this(keys ?? throw new ArgumentNullException(nameof(keys)), proxy, handler: null)
    {
    }

    /// <summary>
    /// Клиент поверх готового обработчика (подставного в тестах). Ключ уходит только заголовком
    /// <c>Authorization: Bearer</c>; в тело запросов он не попадает, а в поле объекта не хранится,
    /// чтобы не утечь в лог или текст исключения.
    /// </summary>
    internal TavilyClient(string apiKey, HttpMessageHandler handler)
        : this(KeyOf(apiKey, handler), proxy: null, handler ?? throw new ArgumentNullException(nameof(handler)))
    {
    }

    private TavilyClient(ApiKeyPool keys, WebProxy proxy, HttpMessageHandler handler)
    {
        _http = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Keys = keys,
            Proxies = proxy == null ? null : [proxy],
            Handler = handler,
            BaseAddress = new Uri(Host),
            RequestTimeout = TimeSpan.FromSeconds(60),
            ResponseHeadersTimeout = TimeSpan.FromSeconds(60),
        });
    }

    private static ApiKeyPool KeyOf(string apiKey, HttpMessageHandler handler)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            handler?.Dispose();
            throw new ArgumentException($"{nameof(apiKey)} is missing");
        }

        return ApiKeyPool.Single(apiKey);
    }

    /// <summary>POST JSON на путь Tavily; неудачный ответ — <see cref="HttpRequestException"/> с кодом и сроком повтора.</summary>
    private Task<T> PostAsync<T>(string path, object args, CancellationToken cancellationToken) =>
        _http.PostJsonAsync<T>(path, args, "Tavily " + path, cancellationToken);

    public async Task<SearchResult> SearchAsync(string query, int maxResults = 5, bool includeRawContent = true, bool includeAnswer = false, bool includeImages = false,
        bool includeImageDescriptions = false, SearchDepth searchDepth = SearchDepth.Basic, TopicType topic = TopicType.General, TimeRange timeRange = TimeRange.All,
        CountryType country = CountryType.All, IEnumerable<Uri> includeDomains = null, IEnumerable<Uri> excludeDomains = null, CancellationToken cancellationToken = default)
    {
        includeDomains ??= [];
        excludeDomains ??= [];
        if (includeDomains.Count() > 300)
            throw new ArgumentException("Maximum 300 domains for includeDomains");
        if (excludeDomains.Count() > 150)
            throw new ArgumentException("Maximum 150 domains for excludeDomains");

        var args = new SearchArgs
        {
            IncludeAnswer = includeAnswer,
            IncludeImages = includeImages,
            IncludeImageDescriptions = includeImageDescriptions,
            Query = query,
            MaxResults = maxResults,
            SearchDepth = searchDepth.GetDescription(),
            IncludeRawContent = includeRawContent,
            Topic = topic.GetDescription(),
            // All означает отсутствие фильтра — поле должно быть опущено в JSON (null), иначе API получит невалидное значение
            TimeRange = timeRange == Models.TimeRange.All ? null : timeRange.GetDescription(),
            Country = country == CountryType.All ? null : country.GetDescription(),
            IncludeDomains = includeDomains.Select(domain => domain.AbsoluteUri),
            ExcludeDomains = excludeDomains.Select(domain => domain.AbsoluteUri),
        };

        // Поиск ничего не меняет — повтор безопасен; повторяется только временный сбой.
        var result = await (SearchRetry ?? RetryPolicy.Default).RunAsync(
            (_, token) => PostAsync<SearchResult>("/search", args, token), RepeatSafety.Safe, cancellationToken);

        if (result?.Results != null)
        {
            result.Results = result.Results
                .Where(r => !ContainsForbiddenContent(url: r.Url, rawContent: r.RawContent, excludeDomains: excludeDomains, requireRawContent: includeRawContent))
                .ToArray();
        }

        return result;
    }

    public async Task<ExtractResult> ExtractAsync(IEnumerable<string> urls, bool includeImages = false, ExtractDepth extractDepth = ExtractDepth.Basic, FormatType format = FormatType.Markdown, CancellationToken cancellationToken = default)
    {
        var args = new ExtractArgs
        {
            Urls = urls,
            IncludeImages = includeImages,
            ExtractDepth = extractDepth.GetDescription(),
            Format = format.GetDescription(),
        };

        // Повторяется только временный сбой запроса. Адреса, которые Tavily не смог извлечь, приходят
        // в FailedResults удачного ответа: это итог по существу, а не сбой сервера, и повтором не лечится.
        return await (ExtractRetry ?? RetryPolicy.Default).RunAsync(
            (_, token) => PostAsync<ExtractResult>("/extract", args, token), RepeatSafety.Safe, cancellationToken);
    }

    /// <summary>
    /// Фильтрация результата поиска на наличие недопустимой/запрещенной/устаревшей/нерелевантной информации
    /// </summary>
    /// <param name="url">Адрес источника</param>
    /// <param name="rawContent">Контент источника</param>
    /// <param name="excludeDomains">Запрещенные домены</param>
    /// <returns>Возвращает true если результат содержит запрещенную информацию и false если допустимую информацию</returns>
    public virtual bool ContainsForbiddenContent(string url, string rawContent, IEnumerable<Uri> excludeDomains)
    {
        return ContainsForbiddenContent(url, rawContent, excludeDomains, requireRawContent: true);
    }

    /// <summary>
    /// Фильтрация результата поиска на наличие недопустимой/запрещенной/устаревшей/нерелевантной информации
    /// </summary>
    /// <param name="url">Адрес источника</param>
    /// <param name="rawContent">Контент источника</param>
    /// <param name="excludeDomains">Запрещенные домены</param>
    /// <param name="requireRawContent">Если true — результат без контента считается запрещенным (используется при include_raw_content=true)</param>
    /// <returns>Возвращает true если результат содержит запрещенную информацию и false если допустимую информацию</returns>
    public virtual bool ContainsForbiddenContent(string url, string rawContent, IEnumerable<Uri> excludeDomains, bool requireRawContent)
    {
        if (string.IsNullOrEmpty(url))
            return true;

        if (requireRawContent && string.IsNullOrEmpty(rawContent))
            return true;

        var uri = new Uri(url);
        if (excludeDomains != null && excludeDomains.Any() && excludeDomains.Any(excludeDomain => excludeDomain.Host == uri.Host))
            return true;

        if (Regex.IsMatch(url, @"\b(?:ua|\.ua)\b", RegexOptions.CultureInvariant))
            return true;

        return false;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;
        
        if (disposing)
            _http.Dispose();
    }
}
