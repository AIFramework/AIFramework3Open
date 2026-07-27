using AI.DataStructs.Algebraic;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Services.Embeddings.Base;
using AI.LLM.Services.Embeddings.Infinity.Models;
using System.Net.Http.Json;

namespace AI.LLM.Services.Embeddings.Infinity;

/// <summary>
/// Эмбеддер поверх сервера Infinity (OpenAI-совместимый /v1/embeddings).
/// </summary>
public class BaseInfinityEmbedder : EmbedderServiceBase, IDisposable
{
    private readonly HttpClient _httpClient;
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
        _httpClient = new HttpClient()
        {
            BaseAddress = new Uri(host),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    /// <inheritdoc/>
    public override async Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        Exception lastException = new Exception();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var response = await _httpClient.PostAsJsonAsync("/v1/embeddings", new InfinityEmbeddingsArgs
                {
                    Model = ModelName,
                    Input = texts,
                }, linkedCts.Token);
                if (!response.IsSuccessStatusCode)
                    throw new Exception((await response.Content.ReadAsStringAsync(linkedCts.Token) ?? "").TruncateForLogging());

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadFromJsonAsync<InfinityEmbeddingsResult>(cancellationToken: linkedCts.Token);
                // Сервер не гарантирует порядок элементов, поэтому упорядочиваем по индексу
                return content?.Data?.OrderBy(d => d.Index).Select(t => t.Embedding).ToArray() ?? Array.Empty<Vector>();
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < 1) // Только для первой попытки
                {
                    try { await Task.Delay(1000, cancellationToken); }
                    catch (OperationCanceledException) { throw lastException; }
                }
            }
        }

        throw lastException;
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
            _httpClient?.Dispose();
        }
    }
}
