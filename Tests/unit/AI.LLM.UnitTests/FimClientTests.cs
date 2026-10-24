using AI.LLM.Clients.Base;
using AI.LLM.Clients.Fim;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Тесты клиента дополнения FIM: форма запроса, шаблон для серверов без suffix, разбор ответа и ошибки.
/// Сеть подменяется фейковым HttpMessageHandler.
/// </summary>
public class FimClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _answer;

        public List<string> Bodies { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];

        public Handler(Func<CancellationToken, Task<HttpResponseMessage>> answer) => _answer = answer;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return await _answer(cancellationToken);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (FimClient Client, Handler Handler) Create(string answer, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Handler(_ => Task.FromResult(Json(answer, status)));
        return (new FimClient(FimClient.OllamaEndpoint, "qwen2.5-coder:1.5b", httpClient: new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task CompleteAsync_SendsPrefixSuffixAndStop_ReturnsChoiceText()
    {
        var (client, handler) = Create("""{"choices":[{"text":"a + b;","index":0}]}""");

        string text = await client.CompleteAsync("int Sum(int a, int b) => ", "\n}", ["\n"]);

        Assert.Equal("a + b;", text);
        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal("qwen2.5-coder:1.5b", request.GetProperty("model").GetString());
        Assert.Equal("int Sum(int a, int b) => ", request.GetProperty("prompt").GetString());
        Assert.Equal("\n}", request.GetProperty("suffix").GetString());
        Assert.Equal("\n", request.GetProperty("stop")[0].GetString());
        Assert.Equal(0, request.GetProperty("temperature").GetInt32());
        Assert.False(request.GetProperty("stream").GetBoolean());
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task CompleteAsync_WithTemplate_PutsMarkersIntoPromptWithoutSuffix()
    {
        var (client, handler) = Create("""{"choices":[{"text":"1"}]}""");
        client.Template = FimClient.QwenTemplate;
        client.ApiKey = "k";

        await client.CompleteAsync("x = ", "\nprint(x)");

        var request = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.Equal("<|fim_prefix|>x = <|fim_suffix|>\nprint(x)<|fim_middle|>", request.GetProperty("prompt").GetString());
        Assert.False(request.TryGetProperty("suffix", out _));
        Assert.False(request.TryGetProperty("stop", out _));
        Assert.Equal("k", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task CompleteAsync_NoChoices_ReturnsEmpty()
    {
        var (client, _) = Create("""{"choices":[]}""");

        Assert.Equal("", await client.CompleteAsync("a", "b"));
    }

    [Fact]
    public async Task CompleteAsync_ServerError_ThrowsReadableException()
    {
        var (client, _) = Create("""{"error":{"message":"model \"x\" not found"}}""", HttpStatusCode.NotFound);

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => client.CompleteAsync("a", "b"));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public async Task CompleteAsync_SlowServer_ThrowsNoResponseAfterTimeout()
    {
        var handler = new Handler(async token => { await Task.Delay(5000, token); return Json("{}"); });
        var client = new FimClient(FimClient.OllamaEndpoint, "m", httpClient: new HttpClient(handler)) { Timeout = TimeSpan.FromMilliseconds(100) };

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => client.CompleteAsync("a", "b"));

        Assert.Null(error.StatusCode);
    }

    [Fact]
    public async Task CompleteAsync_CancelledByCaller_ThrowsCancellation()
    {
        var handler = new Handler(async token => { await Task.Delay(5000, token); return Json("{}"); });
        var client = new FimClient(FimClient.OllamaEndpoint, "m", httpClient: new HttpClient(handler));
        using var cancel = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CompleteAsync("a", "b", cancellationToken: cancel.Token));
    }
}
