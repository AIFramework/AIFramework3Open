using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Services.Reranking.OpenRouter;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Тесты реранкера OpenRouter: форма запроса, выравнивание оценок по исходным позициям,
/// top_n и обработка ошибок. Сеть подменяется фейковым HttpMessageHandler.
/// </summary>
public class OpenRouterRerankerTests
{
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

    private static string RerankBody(params (int Index, double Score)[] results) =>
        JsonSerializer.Serialize(new
        {
            id = "gen-1",
            model = "test",
            results = results.Select(r => new { index = r.Index, relevance_score = r.Score }).ToArray(),
            usage = new { search_units = 1, cost = 0.001 }
        });

    private static (OpenRouterReranker Reranker, RecordingHandler Handler) Create(
        string model = OpenRouterRerankModels.CohereRerankV35,
        params HttpResponseMessage[] responses)
    {
        var handler = new RecordingHandler(responses);
        var reranker = new OpenRouterReranker("test-key", model, new HttpClient(handler));
        return (reranker, handler);
    }

    [Fact]
    public async Task SimsAsync_SendsPlainStrings_AndAlignsScoresToInputOrder()
    {
        // Сервер отдаёт по убыванию релевантности: второй документ лучший
        var (reranker, handler) = Create(
            responses: Json(RerankBody((1, 0.9), (0, 0.2), (2, 0.05))));

        var scores = await reranker.SimsAsync("что такое RAG", new[] { "первый", "второй", "третий" });

        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal(OpenRouterRerankModels.CohereRerankV35, request.GetProperty("model").GetString());
        Assert.Equal("что такое RAG", request.GetProperty("query").GetString());
        Assert.Equal("первый", request.GetProperty("documents")[0].GetString());
        Assert.False(request.TryGetProperty("top_n", out _)); // за всеми оценками ходим без отсечения

        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);

        // Оценки вернулись в порядке входных документов, а не по убыванию
        Assert.Equal(new[] { 0.2, 0.9, 0.05 }, scores.ToArray());
    }

    [Fact]
    public async Task SimsAsync_WhenServerSkipsDocument_Throws()
    {
        var (reranker, _) = Create(responses: Json(RerankBody((0, 0.5))));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reranker.SimsAsync("запрос", new[] { "a", "b" }));
    }

    [Fact]
    public async Task TopKSimsAsync_PassesTopNToServer_AndSortsByScore()
    {
        var (reranker, handler) = Create(
            responses: Json(RerankBody((2, 0.7), (0, 0.9))));

        var top = await reranker.TopKSimsAsync("запрос", new[] { "a", "b", "c" }, k: 2);

        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal(2, request.GetProperty("top_n").GetInt32());

        Assert.Equal([(0, 0.9), (2, 0.7)], top);
    }

    [Fact]
    public async Task TopKSimsAsync_ClampsTopNToDocumentCount()
    {
        var (reranker, handler) = Create(
            responses: Json(RerankBody((0, 0.9), (1, 0.1))));

        await reranker.TopKSimsAsync("запрос", new[] { "a", "b" }, k: 50);

        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal(2, request.GetProperty("top_n").GetInt32());
    }

    [Fact]
    public async Task Instruct_IsPrependedToQuery()
    {
        var (reranker, handler) = Create(responses: Json(RerankBody((0, 0.5))));

        await reranker.SimsAsync("ноутбук для игр", new[] { "документ" }, "Найди товары по описанию");

        var query = JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("query").GetString();
        Assert.Equal("Найди товары по описанию\nноутбук для игр", query);
    }

    [Fact]
    public async Task MultimodalDocuments_AreSentAsObjects()
    {
        var (reranker, handler) = Create(
            model: OpenRouterRerankModels.NemotronRerankVL,
            responses: Json(RerankBody((0, 0.8), (1, 0.3))));

        var documents = new[]
        {
            RerankDocument.FromImage("https://example.com/page1.png", "страница 1"),
            new RerankDocument("обычный текст")
        };

        var scores = await reranker.SimsAsync("годовой отчёт", documents);

        var sent = JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("documents");
        Assert.Equal("https://example.com/page1.png", sent[0].GetProperty("image").GetString());
        Assert.Equal("страница 1", sent[0].GetProperty("text").GetString());
        Assert.Equal("обычный текст", sent[1].GetProperty("text").GetString());
        Assert.False(sent[1].TryGetProperty("image", out _)); // пустые поля не уходят

        Assert.Equal(new[] { 0.8, 0.3 }, scores.ToArray());
    }

    [Fact]
    public async Task ImageDocument_OnTextOnlyModel_Throws()
    {
        var (reranker, handler) = Create(model: OpenRouterRerankModels.CohereRerankV35);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => reranker.SimsAsync("запрос", new[] { RerankDocument.FromImage("https://example.com/p.png") }));

        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task EmptyDocument_Throws()
    {
        var (reranker, _) = Create(model: OpenRouterRerankModels.NemotronRerankVL);

        await Assert.ThrowsAsync<ArgumentException>(
            () => reranker.SimsAsync("запрос", new[] { new RerankDocument() }));
    }

    [Fact]
    public async Task RateLimit_IsRetried_AndBadRequestIsNot()
    {
        var (retried, retriedHandler) = Create(
            responses:
            [
                Json("{\"error\":\"rate limited\"}", HttpStatusCode.TooManyRequests),
                Json(RerankBody((0, 0.4)))
            ]);
        retried.MaxRetries = 1;

        var scores = await retried.SimsAsync("запрос", new[] { "документ" });
        Assert.Equal(2, retriedHandler.Bodies.Count);
        Assert.Equal(0.4, scores[0]);

        var (failed, failedHandler) = Create(
            responses: Json("{\"error\":\"unknown model\"}", HttpStatusCode.BadRequest));
        failed.MaxRetries = 3;

        await Assert.ThrowsAsync<HttpRequestException>(() => failed.SimsAsync("запрос", new[] { "документ" }));
        Assert.Single(failedHandler.Bodies);
    }

    [Fact]
    public async Task Usage_IsExposedAfterCall()
    {
        var (reranker, _) = Create(responses: Json(RerankBody((0, 0.5))));

        await reranker.SimsAsync("запрос", new[] { "документ" });

        Assert.Equal(1, reranker.LastUsage?.SearchUnits);
        Assert.Equal(0.001, reranker.LastUsage?.Cost);
    }

    [Fact]
    public async Task EmptyDocumentList_DoesNotCallApi()
    {
        var (reranker, handler) = Create();

        var scores = await reranker.SimsAsync("запрос", Array.Empty<string>());

        Assert.Empty(scores);
        Assert.Empty(handler.Bodies);
    }
}
