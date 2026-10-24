using System.Net;
using System.Net.Http.Headers;
using AI.LLM.API.LLMAPI;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Services.LLM;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Классификатор на LLM с рассуждающей моделью и сервером без вероятностей токенов: класс берётся из текста ответа,
/// предел ответа задаётся с запасом на рассуждение, пустое начало ответа не отправляется.
/// </summary>
public class ClassifierWithLLMTests
{
    [Fact]
    public async Task ClassifierWithLLM_TextClassifyAsync_TakesClassFromAnswerTextWithoutLogprobs()
    {
        var web = new RecordingWebClient(Sse("Ответ: 2."));
        var classifier = new ClassifierWithLLM(new ChatLLMApi(web, "модель", "Ответь одной цифрой."), ["0", "1", "2", "3"]) { AnswerTokens = 300 };

        List<ClassifyData> classes = await classifier.TextClassifyAsync("Как прочитать CSV в pandas?");

        ClassifyData best = Assert.Single(classes);
        Assert.Equal("2", best.ClToken);
        Assert.Equal(1.0, best.Prob, 6);
        Assert.Equal(300, web.Sent.MaxTokens);
        Assert.Null(web.Sent.MinTokens);
        Assert.DoesNotContain(web.Sent.Messages, m => m.Role == LLMMessage.AssistantRole);
    }

    [Fact]
    public async Task ClassifierWithLLM_TextClassifyAsync_KeepsOneTokenLimitByDefault()
    {
        var web = new RecordingWebClient(Sse("7"));
        var classifier = new ClassifierWithLLM(new ChatLLMApi(web, "модель", "шкала")) { AnswerStart = "Оценка:" };

        List<ClassifyData> classes = await classifier.TextClassifyAsync("текст");

        Assert.Equal("7", Assert.Single(classes).ClToken);
        Assert.Equal(1, web.Sent.MaxTokens);
        Assert.Equal(1, web.Sent.MinTokens);
        Assert.Equal("Оценка:", web.Sent.Messages[^1].Content?.ToString());
    }

    /// <summary>Ответ модели потоком (SSE), как его читает <see cref="ChatLLMApi"/>: без вероятностей токенов.</summary>
    private static string Sse(string content) =>
        "data: {\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":" + System.Text.Json.JsonSerializer.Serialize(content) +
        "},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";

    /// <summary>HTTP-клиент с одним заготовленным ответом; запоминает отправленный запрос.</summary>
    private sealed class RecordingWebClient(string body) : IWebAPIClient
    {
        public SendDataLLM Sent { get; private set; }

        public AuthenticationHeaderValue Authentication { get; set; }

        public Task<HttpResponseMessage> PostAsJsonAsync(string apiUrl, SendDataLLM sendData, CancellationToken? concelationToken = default)
        {
            Sent = sendData;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }

        public void Dispose()
        {
        }
    }
}
