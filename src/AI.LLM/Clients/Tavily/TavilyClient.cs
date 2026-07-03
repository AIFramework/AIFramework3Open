using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using AI.LLM.Clients.Tavily.Models;
using AI.LLM.Infrastructure.Extensions;

namespace AI.LLM.Clients.Tavily;

public class TavilyClient : IDisposable
{
    public const string Host = "https://api.tavily.com";

    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler _httpHandler;
    private readonly string _apiKey;
    private int _disposed; // 0 = not disposed, 1 = disposed (для Interlocked)

    public TavilyClient(string apiKey, WebProxy proxy = null)
    {
        _apiKey = apiKey;
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException($"{nameof(apiKey)} is missing");

        _httpHandler = new HttpClientHandler();
        if (proxy != null)
        {
            _httpHandler.UseProxy = true;
            _httpHandler.Proxy = proxy;
        }
        _httpClient = new HttpClient(_httpHandler)
        {
            BaseAddress = new Uri(Host),
            Timeout = TimeSpan.FromSeconds(60),
        };
    }

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

        const int maxAttempts = 2;
        Exception lastException = null;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                // Локальный таймаут 60 секунд для ReadFromJsonAsync
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                using var response = await _httpClient.PostAsJsonAsync("/search", new SearchArgs
                {
                    ApiKey = _apiKey,
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
                }, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<SearchResult>(cancellationToken: linkedCts.Token);

                if (result?.Results != null)
                {
                    result.Results = result.Results
                        .Where(r => !ContainsForbiddenContent(url: r.Url, rawContent: r.RawContent, excludeDomains: excludeDomains, requireRawContent: includeRawContent))
                        .ToArray();
                }

                return result;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Глобальная отмена - не делаем retry
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < maxAttempts - 1) // Только для первой попытки
                {
                    try { await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken); }
                    catch (OperationCanceledException) { throw lastException; }
                }
            }
        }

        throw lastException ?? new Exception("Tavily search failed after 2 attempts");
    }

    public async Task<ExtractResult> ExtractAsync(IEnumerable<string> urls, bool includeImages = false, ExtractDepth extractDepth = ExtractDepth.Basic, FormatType format = FormatType.Markdown, CancellationToken cancellationToken = default)
    {
        ExtractResult result = null;
        Exception lastException = null;
        const int maxAttempts = 4;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                // Локальный таймаут 60 секунд для ReadFromJsonAsync
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                using var response = await _httpClient.PostAsJsonAsync("/extract", new ExtractArgs
                {
                    ApiKey = _apiKey,
                    Urls = urls,
                    IncludeImages = includeImages,
                    ExtractDepth = extractDepth.GetDescription(),
                    Format = format.GetDescription(),
                }, cancellationToken);
                response.EnsureSuccessStatusCode();
                result = await response.Content.ReadFromJsonAsync<ExtractResult>(cancellationToken: linkedCts.Token);
                if (result != null && (result.FailedResults == null || !result.FailedResults.Any()))
                    return result;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            if (attempt != maxAttempts - 1)
                await Task.Delay(TimeSpan.FromSeconds(2 * (attempt+1)), cancellationToken); // 2, 4, 6
        }

        if (result != null)
            return result;

        throw lastException ?? new InvalidOperationException("Tavily extract failed after all attempts");
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
        {
            _httpClient?.Dispose();
            _httpHandler?.Dispose();
        }
    }
}
