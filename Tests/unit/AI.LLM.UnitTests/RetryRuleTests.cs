using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AI.LLM.API.LLMAPI;
using AI.LLM.Clients.Base;
using AI.LLM.Clients.Tavily;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Infrastructure.Resilience;
using AI.LLM.Services.Embeddings.OpenRouter;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Правило повторов одно на все клиенты (<see cref="RetryPolicy"/>): только временный сбой, пауза по Retry-After или
/// нарастающая с пределом, необратимый вызов и «результат неизвестен» не повторяются.
/// </summary>
public class RetryRuleTests
{
    private static readonly RetryPolicy Fast = RetryPolicy.Default with { InitialDelay = TimeSpan.Zero };

    [Fact]
    public async Task RunAsync_TransientRetried_RetryAfterHonored()
    {
        var pauses = new List<TimeSpan>();
        int calls = 0;

        string result = await Fast.RunAsync(
            (_, _) => ++calls < 2
                ? throw LlmRequestException.FromResponse(HttpStatusCode.TooManyRequests, "", new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(30)))
                : Task.FromResult("ok"),
            RepeatSafety.Safe,
            onRetry: (_, _, pause) => pauses.Add(pause));

        Assert.Equal("ok", result);
        Assert.Equal(2, calls);
        Assert.Equal([TimeSpan.FromMilliseconds(30)], pauses);
    }

    [Fact]
    public async Task RunAsync_FatalNotRetried()
    {
        int calls = 0;

        await Assert.ThrowsAsync<LlmRequestException>(() => Fast.RunAsync<string>(
            (_, _) => { calls++; throw LlmRequestException.FromResponse(HttpStatusCode.BadRequest, "", null); },
            RepeatSafety.Safe));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_UnsafeCallNeverRetried_EvenOnTransient()
    {
        int calls = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() => Fast.RunAsync<string>(
            (_, _) => { calls++; throw new HttpRequestException("перегрузка", null, HttpStatusCode.ServiceUnavailable); },
            RepeatSafety.Unsafe));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_RetryAfterBeyondLimit_NotWaited()
    {
        int calls = 0;
        var error = RetryPolicy.WithRetryAfter(new HttpRequestException("лимит", null, HttpStatusCode.TooManyRequests), TimeSpan.FromMinutes(10));

        await Assert.ThrowsAsync<HttpRequestException>(() => Fast.RunAsync<string>((_, _) => { calls++; throw error; }, RepeatSafety.Safe));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Classify_NetworkIsTransientForSafe_UnknownForUnsafe()
    {
        var broken = new IOException("обрыв после отправки");

        Assert.Equal(FailureKind.Transient, RetryPolicy.Classify(broken, RepeatSafety.Safe));
        Assert.Equal(FailureKind.Unknown, RetryPolicy.Classify(broken, RepeatSafety.Unsafe));
        Assert.False(Fast.ShouldRetry(FailureKind.Unknown, RepeatSafety.Safe, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.Classify(broken, RepeatSafety.Unspecified));
    }

    [Fact]
    public void DelayAfter_GrowsAndIsCapped()
    {
        var policy = RetryPolicy.Default with { InitialDelay = TimeSpan.FromSeconds(1), MaxDelay = TimeSpan.FromSeconds(5) };

        Assert.Equal(TimeSpan.FromSeconds(1), policy.DelayAfter(1));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.DelayAfter(2));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.DelayAfter(3));
        Assert.Equal(TimeSpan.FromSeconds(5), policy.DelayAfter(10));
        Assert.Equal(TimeSpan.FromSeconds(7), policy.DelayAfter(1, TimeSpan.FromSeconds(7)));
    }

    /// <summary>
    /// Прежде код ответа, пришедший исключением от клиента отправки (ProxyHTTPClient), терялся: запрос считался «нет
    /// связи» и повторялся три раза, даже при неверном ключе.
    /// </summary>
    [Fact]
    public async Task ChatLLMApi_StatusFromSenderException_KeptAndNotRetried()
    {
        var web = new ThrowingWebClient(new HttpRequestException("Ошибка Unauthorized: {}", null, HttpStatusCode.Unauthorized));
        var api = new ChatLLMApi(web, "model-test") { Retry = Fast };

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => api.SendWithContextAsync([LLMMessage.CreateMessage(Roles.User, "вопрос")]));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal(1, web.Calls);
    }

    /// <summary>Прежде эмбеддер ждал Retry-After любой длины (час — значит час); теперь предел правила повторов.</summary>
    [Fact]
    public async Task OpenRouterEmbedder_LongRetryAfter_FailsAtOnceInsteadOfWaiting()
    {
        var handler = new QueueHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return response;
        });
        using var embedder = new OpenRouterEmbedder("key-test-0003", httpClient: new HttpClient(handler)) { MaxRetries = 3 };
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => embedder.EncodeAsync(["текст"], guard.Token));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Прежде неверный ключ Tavily повторялся (две попытки с паузой).</summary>
    [Fact]
    public async Task Tavily_RejectedKey_NotRetried()
    {
        var handler = new QueueHandler(() => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") });
        using var client = new TavilyClient("tvly-test-0004", handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("запрос", includeRawContent: false));

        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// Прежде извлечение повторялось четыре раза, если хоть один адрес не извлёкся: это итог по существу, а не сбой
    /// сервера. Теперь удачный ответ с неизвлечёнными адресами возвращается сразу.
    /// </summary>
    [Fact]
    public async Task Tavily_PartialExtract_ReturnedWithoutRetry()
    {
        var handler = new QueueHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"results": [{"url": "https://site.test/a", "raw_content": "текст"}], "failed_results": [{"url": "https://site.test/b", "error": "нет доступа"}]}""",
                Encoding.UTF8,
                "application/json"),
        });
        using var client = new TavilyClient("tvly-test-0005", handler);

        var result = await client.ExtractAsync(["https://site.test/a", "https://site.test/b"]);

        Assert.Equal(1, handler.Calls);
        Assert.Single(result.FailedResults);
    }

    private sealed class ThrowingWebClient(Exception error) : IWebAPIClient
    {
        public int Calls { get; private set; }

        public AuthenticationHeaderValue? Authentication { get; set; }

        public Task<HttpResponseMessage> PostAsJsonAsync(string apiUrl, SendDataLLM sendData, CancellationToken? concelationToken = default)
        {
            Calls++;
            throw error;
        }

        public void Dispose()
        {
        }
    }

    private sealed class QueueHandler(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(answer());
        }
    }
}
