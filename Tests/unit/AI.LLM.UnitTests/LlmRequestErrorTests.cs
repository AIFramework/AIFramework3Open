using System.Net;
using System.Net.Http.Headers;
using AI.LLM.Agents.ReAct;
using AI.LLM.API.LLMAPI;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Services.LLM;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Ошибки запроса к модели. Здесь проверяется то, что раньше превращалось в «нет ответа»: неверный
/// ключ повторялся вслепую, а цикл агента считал отказ сервера неверным форматом и просил модель
/// ответить ещё раз.
/// </summary>
public class LlmRequestErrorTests
{
    [Fact]
    public async Task ChatLLMApi_SendWithContextAsync_RejectedKeyIsNotRetriedAndExplained()
    {
        var web = new ScriptedWebClient(Response(HttpStatusCode.Unauthorized, """{"error":{"message":"No auth credentials found","code":401}}"""));
        var api = new ChatLLMApi(web, "модель");

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => api.SendWithContextAsync([LLMMessage.CreateMessage(Roles.User, "вопрос")]));

        Assert.Equal(1, web.Calls);
        Assert.False(error.IsRetryable);
        Assert.Equal("Сервер модели отклонил ключ (401): No auth credentials found", error.Message);
    }

    [Fact]
    public async Task ChatLLMApi_SendWithContextAsync_RetriesOverloadAsServerSays()
    {
        var web = new ScriptedWebClient(
            Response(HttpStatusCode.ServiceUnavailable, "перегрузка", TimeSpan.Zero),
            Response(HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit exceeded"}}""", TimeSpan.Zero),
            Response(HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit exceeded"}}""", TimeSpan.Zero));
        var api = new ChatLLMApi(web, "модель");

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => api.SendWithContextAsync([LLMMessage.CreateMessage(Roles.User, "вопрос")]));

        Assert.Equal(3, web.Calls);
        Assert.True(error.IsRetryable);
        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.StartsWith("Поставщик модели ограничил частоту запросов (429)", error.Message);
    }

    [Fact]
    public void LlmRequestException_FromResponse_RecognizesContextOverflowAndLongWaits()
    {
        var overflow = LlmRequestException.FromResponse(HttpStatusCode.BadRequest,
            """{"error":{"message":"This model's maximum context length is 131072 tokens","code":"context_length_exceeded"}}""", null);
        var longWait = LlmRequestException.FromResponse(HttpStatusCode.TooManyRequests, "", new RetryConditionHeaderValue(TimeSpan.FromMinutes(10)));
        var balance = LlmRequestException.FromResponse(HttpStatusCode.PaymentRequired, """{"error":"Insufficient credits"}""", null);

        Assert.True(overflow.IsContextOverflow);
        Assert.False(overflow.IsRetryable);
        Assert.StartsWith("Запрос не помещается в окно модели (400)", overflow.Message);
        // Ждать десять минут внутри хода незачем — лучше сразу сказать человеку.
        Assert.False(longWait.IsRetryable);
        Assert.Equal("Не хватает средств на счёте у поставщика модели (402): Insufficient credits", balance.Message);
    }

    [Fact]
    public async Task ReActEngine_WithNativeToolCalling_ShowsRequestFailureInsteadOfRetryingFormat()
    {
        var web = new ScriptedWebClient(Response(HttpStatusCode.PaymentRequired, """{"error":{"message":"Insufficient credits"}}"""));
        var llm = new LLMBase(new ChatLLMApi(web, "модель"), new LLMOptions { ModelName = "модель" });
        ReActEngine engine = ReActAgentBuilder.Create()
            .WithNativeToolCalling(llm)
            .WithTool(new FakeReActTool("web_search"))
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // Один запрос и понятная причина — а не подсказки формата модели и «нет ответа».
        Assert.Equal(1, web.Calls);
        Assert.Equal(ReActStopReason.EngineFailure, result.StopReason);
        Assert.Contains("Не хватает средств", result.Error);
    }

    [Fact]
    public async Task ReActEngine_WithNativeToolCalling_EmptyAnswerGetsAnotherStepInsteadOfFailure()
    {
        // Модель закончила ответ молча (finish_reason=stop без текста): клиент повторяет запрос, а цикл после этого
        // не сдаётся с «нет связи», а подсказывает модели и спрашивает снова.
        const string empty = "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
        const string answer = "data: {\"choices\":[{\"delta\":{\"content\":\"Готово\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
        var web = new ScriptedWebClient(Response(HttpStatusCode.OK, empty), Response(HttpStatusCode.OK, empty), Response(HttpStatusCode.OK, empty),
            Response(HttpStatusCode.OK, answer));
        var llm = new LLMBase(new ChatLLMApi(web, "модель"), new LLMOptions { ModelName = "модель" });
        ReActEngine engine = ReActAgentBuilder.Create()
            .WithNativeToolCalling(llm)
            .WithTool(new FakeReActTool("web_search"))
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal(4, web.Calls);
        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
        Assert.Equal("Готово", result.Answer);
    }

    [Fact]
    public async Task LLMBase_SendFullAsync_PassesStreamChunksWhileCollectingAnswer()
    {
        const string sse = "data: {\"choices\":[{\"delta\":{\"content\":\"При\"}}]}\n\n"
            + "data: {\"choices\":[{\"delta\":{\"content\":\"вет\"}}]}\n\n"
            + "data: [DONE]\n\n";
        var web = new ScriptedWebClient(Response(HttpStatusCode.OK, sse));
        ILLMClient llm = new LLMBase(new ChatLLMApi(web, "модель"), new LLMOptions { ModelName = "модель" });
        var chunks = new List<string>();

        var response = await llm.SendFullAsync([LLMMessage.CreateMessage(Roles.User, "вопрос")], null, c => chunks.Add(c.Content));

        Assert.Equal(["При", "вет"], chunks.Where(c => c != null));
        Assert.Equal("Привет", response.Choices[0].Message.Content?.ToString());
    }

    private static HttpResponseMessage Response(HttpStatusCode code, string body, TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(code) { Content = new StringContent(body) };
        if (retryAfter is TimeSpan wait)
            response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
        return response;
    }

    /// <summary>HTTP-клиент с заготовленными ответами по порядку.</summary>
    private sealed class ScriptedWebClient(params HttpResponseMessage[] responses) : IWebAPIClient
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int Calls { get; private set; }

        public AuthenticationHeaderValue Authentication { get; set; }

        public Task<HttpResponseMessage> PostAsJsonAsync(string apiUrl, SendDataLLM sendData, CancellationToken? concelationToken = default)
        {
            Calls++;
            return Task.FromResult(_responses.Dequeue());
        }

        public void Dispose()
        {
        }
    }
}
