using AI.LLM.Clients.Base;
using AI.LLM.Clients.Replicate;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Тесты клиента Replicate: форма предсказания, ожидание и опрос, склейка ответа из фрагментов и ошибки.
/// Сеть подменяется фейковым HttpMessageHandler.
/// </summary>
public class ReplicateClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public List<string> Bodies { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];

        public Handler(params HttpResponseMessage[] responses) => _responses = new Queue<HttpResponseMessage>(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SendFullAsync_WaitsForPrediction_JoinsOutputChunks()
    {
        var handler = new Handler(Json("""{"id":"p1","status":"succeeded","output":["При","вет"]}"""));
        var client = new ReplicateClient("r8_test", "meta/meta-llama-3-70b-instruct", new HttpClient(handler));

        var response = await client.SendFullAsync(
            [LLMMessage.CreateMessage(Roles.System, "Отвечай кратко"), LLMMessage.CreateMessage(Roles.User, "Скажи привет")],
            new GenerateSettings { MaxTokens = 32, Temperature = 0 });

        Assert.Equal("Привет", response.Choices[0].Message.Content.ToString());
        HttpRequestMessage request = handler.Requests[0];
        Assert.Equal("https://api.replicate.com/v1/models/meta/meta-llama-3-70b-instruct/predictions", request.RequestUri!.ToString());
        Assert.Equal("wait=60", string.Join("", request.Headers.GetValues("Prefer")));
        Assert.Equal("r8_test", request.Headers.Authorization?.Parameter);
        JsonElement input = JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("input");
        Assert.Equal("Скажи привет", input.GetProperty("prompt").GetString());
        Assert.Equal("Отвечай кратко", input.GetProperty("system_prompt").GetString());
        Assert.Equal(32, input.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task SendFullAsync_PollsUnfinishedPrediction_AndLabelsDialog()
    {
        var handler = new Handler(
            Json("""{"id":"p2","status":"starting","urls":{"get":"https://api.replicate.com/v1/predictions/p2"}}"""),
            Json("""{"id":"p2","status":"processing","urls":{"get":"https://api.replicate.com/v1/predictions/p2"}}"""),
            Json("""{"id":"p2","status":"succeeded","output":"готово"}"""));
        var client = new ReplicateClient("r8_test", "owner/model", new HttpClient(handler)) { PollInterval = TimeSpan.Zero };

        string answer = await client.SendAsync([
            LLMMessage.CreateMessage(Roles.User, "вопрос"),
            LLMMessage.CreateMessage(Roles.Assistant, "ответ"),
            LLMMessage.CreateMessage(Roles.User, "ещё вопрос")]);

        Assert.Equal("готово", answer);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
        Assert.Equal("https://api.replicate.com/v1/predictions/p2", handler.Requests[2].RequestUri!.ToString());
        string prompt = JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("input").GetProperty("prompt").GetString();
        Assert.Equal("User: вопрос\n\nAssistant: ответ\n\nUser: ещё вопрос\n\nAssistant:", prompt.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task SendFullAsync_FailedPrediction_ThrowsWithModelError()
    {
        var handler = new Handler(Json("""{"id":"p3","status":"failed","error":"CUDA out of memory"}"""));
        var client = new ReplicateClient("r8_test", "owner/model", new HttpClient(handler));

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => client.SendAsync("x"));

        Assert.Contains("CUDA out of memory", error.Message);
    }

    [Fact]
    public async Task SendFullAsync_BadToken_ThrowsReadableException()
    {
        var handler = new Handler(Json("""{"detail":"Invalid token."}""", HttpStatusCode.Unauthorized));
        var client = new ReplicateClient("r8_bad", "owner/model", new HttpClient(handler));

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => client.SendAsync("x"));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.False(error.IsRetryable);
    }

    [Fact]
    public void Constructor_ModelWithoutOwner_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReplicateClient("r8_test", "llama"));
    }
}
