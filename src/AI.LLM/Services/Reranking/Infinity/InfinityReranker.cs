using AI.LLM.Core.Models.Providers.Infinity;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Infrastructure.Resilience;

namespace AI.LLM.Services.Reranking.Infinity;

/// <summary>
/// Класс для работы с API ранжирования документов
/// </summary>
public class InfinityReranker
{
    private readonly ProviderHttpClient _http;

    /// <summary>Правило повторов: две попытки, пауза 1 с; повторяется только временный сбой.</summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default with { MaxAttempts = 2 };

    /// <summary>
    /// Имя модели реранкера
    /// </summary>
    public string RerankerModelName { get; set; }

    /// <summary>
    /// Базовый URL API
    /// </summary>
    private readonly string _apiUrl;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="InfinityReranker"/>
    /// </summary>
    /// <param name="apiUrl">Базовый URL API</param>
    /// <param name="rerankerModelName"> Имя модели реранкера</param>
    public InfinityReranker(string apiUrl, string rerankerModelName)
    {
        _apiUrl = apiUrl;
        RerankerModelName = rerankerModelName;

        _http = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            BaseAddress = new Uri(_apiUrl),
            RequestTimeout = TimeSpan.FromMinutes(5),
        });
    }

    /// <summary>
    /// Отправляет запрос на ранжирование документов
    /// </summary>
    /// <param name="query">Запрос, относительно которого ранжируются документы</param>
    /// <param name="documents">Список документов для ранжирования</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ от сервера с результатами ранжирования</returns>
    public async Task<RerankResponse> RerankAsync(string query, IEnumerable<string> documents, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var request = new RerankRequest { Model = RerankerModelName, Query = query, Documents = documents };

        // Ранжирование ничего не меняет — повтор безопасен; повторяется только временный сбой.
        var result = await (Retry ?? RetryPolicy.Default).RunAsync(
            (_, token) => _http.PostJsonAsync<RerankResponse>("/v1/rerank", request, "Infinity rerank", token),
            RepeatSafety.Safe,
            linkedCts.Token);

        if (result?.Results == null)
            throw new InvalidOperationException("Rerank response or results are null.");
        result.Results = result.Results.OrderBy(t => t.Index).ToList();
        return result;
    }
}
