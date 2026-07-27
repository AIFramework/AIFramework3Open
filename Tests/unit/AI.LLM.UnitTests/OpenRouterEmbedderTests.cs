using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Services.Embeddings.OpenRouter;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Тесты эмбеддера OpenRouter: форма запроса, разбор ответа, пакетирование и обработка ошибок.
/// Сеть подменяется фейковым HttpMessageHandler.
/// </summary>
public class OpenRouterEmbedderTests
{
    /// <summary>
    /// Фейковый транспорт: записывает тела запросов и отдаёт заранее подготовленные ответы.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public List<string> Bodies { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string EmbeddingsBody(params (int Index, double[] Values)[] items) =>
        JsonSerializer.Serialize(new
        {
            model = "test",
            data = items.Select(i => new { embedding = i.Values, index = i.Index }).ToArray(),
            usage = new { prompt_tokens = 10, total_tokens = 10 }
        });

    private static (OpenRouterEmbedder Embedder, RecordingHandler Handler) Create(
        string model = OpenRouterEmbeddingModels.TextEmbedding3Small,
        params HttpResponseMessage[] responses)
    {
        var handler = new RecordingHandler(responses);
        var embedder = new OpenRouterEmbedder("test-key", model, httpClient: new HttpClient(handler));
        return (embedder, handler);
    }

    [Fact]
    public async Task EncodeAsync_SendsPlainStringArray_AndAuthHeader()
    {
        var (embedder, handler) = Create(
            responses: Json(EmbeddingsBody((0, [1.0, 2.0]), (1, [3.0, 4.0]))));

        var vectors = await embedder.EncodeAsync(new[] { "первый", "второй" });

        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal(OpenRouterEmbeddingModels.TextEmbedding3Small, request.GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Array, request.GetProperty("input").ValueKind);
        Assert.Equal("первый", request.GetProperty("input")[0].GetString());
        Assert.Equal("float", request.GetProperty("encoding_format").GetString());
        Assert.False(request.TryGetProperty("dimensions", out _));

        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("test-key", handler.Requests[0].Headers.Authorization?.Parameter);

        Assert.Equal(2, vectors.Length);
        Assert.Equal(new[] { 3.0, 4.0 }, vectors[1]);
    }

    [Fact]
    public async Task EncodeAsync_RestoresOrderByIndex()
    {
        var (embedder, _) = Create(
            responses: Json(EmbeddingsBody((1, [9.0]), (0, [1.0]))));

        var vectors = await embedder.EncodeAsync(new[] { "a", "b" });

        Assert.Equal(1.0, vectors[0][0]);
        Assert.Equal(9.0, vectors[1][0]);
    }

    [Fact]
    public async Task EncodeAsync_SplitsInputIntoBatches()
    {
        var (embedder, handler) = Create(
            responses:
            [
                Json(EmbeddingsBody((0, [1.0]), (1, [2.0]))),
                Json(EmbeddingsBody((0, [3.0])))
            ]);
        embedder.BatchSize = 2;

        var vectors = await embedder.EncodeAsync(new[] { "a", "b", "c" });

        Assert.Equal(2, handler.Bodies.Count);
        Assert.Equal(3, vectors.Length);
        Assert.Equal(3.0, vectors[2][0]);
    }

    [Fact]
    public async Task EncodeImageAsync_WrapsPartsIntoContentArray()
    {
        var (embedder, handler) = Create(
            model: OpenRouterEmbeddingModels.GeminiEmbedding2,
            responses: Json(EmbeddingsBody((0, [0.5, 0.5]))));

        await embedder.EncodeImageAsync("https://example.com/cat.jpg", "рыжий кот");

        var input = JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("input");
        var content = input[0].GetProperty("content");

        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("рыжий кот", content[0].GetProperty("text").GetString());
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        Assert.Equal("https://example.com/cat.jpg", content[1].GetProperty("image_url").GetProperty("url").GetString());
    }

    [Fact]
    public async Task EncodeImageAsync_OnTextOnlyModel_Throws()
    {
        var (embedder, handler) = Create(model: OpenRouterEmbeddingModels.TextEmbedding3Small);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => embedder.EncodeImageAsync("https://example.com/cat.jpg"));

        Assert.Empty(handler.Bodies); // запрос даже не уходит
    }

    [Fact]
    public async Task Dimensions_OnModelWithoutMatryoshka_Throws()
    {
        var (embedder, _) = Create(model: OpenRouterEmbeddingModels.BgeM3);
        embedder.Dimensions = 256;

        await Assert.ThrowsAsync<NotSupportedException>(() => embedder.EncodeAsync("текст"));
    }

    [Fact]
    public async Task Dimensions_OnSupportedModel_GoesIntoRequest()
    {
        var (embedder, handler) = Create(
            responses: Json(EmbeddingsBody((0, [3.0, 4.0]))));
        embedder.Dimensions = 2;
        embedder.NormalizeVectors = true;

        var vector = await embedder.EncodeAsync("текст");

        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal(2, request.GetProperty("dimensions").GetInt32());
        // Усечённый вектор нормируем: (3,4) -> (0.6,0.8)
        Assert.Equal(0.6, vector[0], 6);
        Assert.Equal(0.8, vector[1], 6);
    }

    [Fact]
    public async Task EncodeQuestionAsync_AppliesInstruction()
    {
        var (embedder, handler) = Create(
            model: OpenRouterEmbeddingModels.Qwen3Embedding8B,
            responses: Json(EmbeddingsBody((0, [1.0]))));
        embedder.QueryInstruction = "Найди релевантный документ";

        await embedder.EncodeQuestionAsync("что такое эмбеддинг");

        var input = JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("input")[0].GetString();
        Assert.Equal("Instruct: Найди релевантный документ\nQuery: что такое эмбеддинг", input);
    }

    [Fact]
    public async Task RateLimit_IsRetried()
    {
        var (embedder, handler) = Create(
            responses:
            [
                Json("{\"error\":\"rate limited\"}", HttpStatusCode.TooManyRequests),
                Json(EmbeddingsBody((0, [7.0])))
            ]);
        embedder.MaxRetries = 1;

        var vector = await embedder.EncodeAsync("текст");

        Assert.Equal(2, handler.Bodies.Count);
        Assert.Equal(7.0, vector[0]);
    }

    [Fact]
    public async Task BadRequest_IsNotRetried()
    {
        var (embedder, handler) = Create(
            responses: Json("{\"error\":\"unknown model\"}", HttpStatusCode.BadRequest));
        embedder.MaxRetries = 3;

        await Assert.ThrowsAsync<HttpRequestException>(() => embedder.EncodeAsync("текст"));

        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task EmptyInput_DoesNotCallApi()
    {
        var (embedder, handler) = Create();

        var vectors = await embedder.EncodeAsync(Array.Empty<string>());

        Assert.Empty(vectors);
        Assert.Empty(handler.Bodies);
    }
}
