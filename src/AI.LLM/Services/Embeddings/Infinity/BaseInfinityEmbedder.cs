using AI.DataStructs.Algebraic;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Infrastructure.Resilience;
using AI.LLM.Services.Embeddings.Base;
using AI.LLM.Services.Embeddings.Infinity.Models;

namespace AI.LLM.Services.Embeddings.Infinity;

/// <summary>
/// Эмбеддер поверх сервера Infinity (OpenAI-совместимый /v1/embeddings).
/// </summary>
public class BaseInfinityEmbedder : EmbedderServiceBase, IDisposable
{
    private readonly ProviderHttpClient _http;
    private int _disposed; // 0 = not disposed, 1 = disposed

    /// <summary>
    /// Оформление запроса под инструктивную модель. Зависит от конкретной модели,
    /// поэтому реализуется наследником.
    /// </summary>
    public override string GetDetailedInstruct(string question) => throw new NotImplementedException();

    /// <summary>
    /// The base URL of the local server.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseAPI"/> class with an optional host URL.
    /// </summary>
    /// <param name="host">The base URL of the local server.</param>
    public BaseInfinityEmbedder(string host = "http://172.17.0.1:11111/")
    {
        Host = host;
        _http = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            BaseAddress = new Uri(host),
            RequestTimeout = TimeSpan.FromMinutes(5),
        });
    }

    /// <summary>Правило повторов: две попытки, пауза 1 с; повторяется только временный сбой.</summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default with { MaxAttempts = 2 };

    /// <inheritdoc/>
    public override async Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var args = new InfinityEmbeddingsArgs { Model = ModelName, Input = texts };

        // Эмбеддинг ничего не меняет — повтор безопасен; повторяется только временный сбой.
        var content = await (Retry ?? RetryPolicy.Default).RunAsync(
            (_, token) => _http.PostJsonAsync<InfinityEmbeddingsResult>("/v1/embeddings", args, "Infinity embeddings", token),
            RepeatSafety.Safe,
            linkedCts.Token);

        // Сервер не гарантирует порядок элементов, поэтому упорядочиваем по индексу
        return content?.Data?.OrderBy(d => d.Index).Select(t => t.Embedding).ToArray() ?? Array.Empty<Vector>();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        if (disposing)
        {
            _http.Dispose();
        }
    }
}
