using System.Net;
using System.Net.Http.Headers;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Infrastructure.Resilience;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Ротация ключей и маршрутов — один механизм (<see cref="CooldownPool{T}"/>): отказ по ключу берёт следующий ключ,
/// сбойный уходит в карантин на срок и возвращается сам. Ключи в тестах синтетические.
/// </summary>
public class KeyRotationTests
{
    private const string Good = "key-good-0001";
    private const string Bad = "key-bad-0002";

    [Fact]
    public async Task ProviderHttpClient_RejectedKey_NextKeyAnswersAndRejectedOneCools()
    {
        var handler = new KeyHandler(key => key == Bad ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        var keys = new ApiKeyPool([Bad, Good]);
        using var http = new ProviderHttpClient(new ProviderHttpClientOptions { Keys = keys, Handler = handler });

        for (int i = 0; i < 5; i++)
        {
            using HttpResponseMessage response = await http.SendAsync(Get, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Неверный ключ пробовали не больше одного раза: после отказа он в карантине, запросы идут с живым.
        Assert.True(handler.Keys.Count(k => k == Bad) <= 1);
        if (handler.Keys.Contains(Bad))
            Assert.True(keys.Snapshot().Single(h => h.Mask == ApiKeyPool.Mask(Bad)).Health.IsCooling);
    }

    [Fact]
    public async Task ProviderHttpClient_ServerError_DoesNotBlameKey()
    {
        var keys = new ApiKeyPool([Good]);
        using var http = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Keys = keys,
            Handler = new KeyHandler(_ => HttpStatusCode.InternalServerError),
        });

        using HttpResponseMessage response = await http.SendAsync(Get, HttpCompletionOption.ResponseContentRead);

        // Ответ сервера — вызывающему, решение о повторе за правилом повторов; ключ ни при чём.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(keys.Snapshot()[0].Health.IsCooling);
        Assert.Equal(0, keys.Snapshot()[0].Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProviderHttpClient_AllKeysRejected_EachTriedOnceAndRefusalReturned()
    {
        var handler = new KeyHandler(_ => HttpStatusCode.PaymentRequired);
        using var http = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Keys = new ApiKeyPool(["key-a-1111", "key-b-2222", "key-c-3333"]),
            Handler = handler,
        });

        using HttpResponseMessage response = await http.SendAsync(Get, HttpCompletionOption.ResponseContentRead);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(3, handler.Keys.Count);
        Assert.Equal(3, handler.Keys.Distinct().Count());
    }

    [Fact]
    public void ApiKeyPool_RateLimit_CoolsForRetryAfter_ThenReturns()
    {
        var time = new ManualTime();
        var keys = new ApiKeyPool([Good], time: time);
        PoolLease<string> lease = keys.Lease();

        keys.ReportFailure(lease, KeyFailure.RateLimit, TimeSpan.FromSeconds(42));

        Assert.Equal(time.Now + TimeSpan.FromSeconds(42), keys.Snapshot()[0].Health.CoolingUntil);
        Assert.Equal(0, keys.AvailableCount);

        time.Now += TimeSpan.FromSeconds(43);

        Assert.Equal(1, keys.AvailableCount);
        Assert.Equal(0, keys.Snapshot()[0].Health.ConsecutiveFailures);
    }

    [Fact]
    public void ApiKeyPool_CooldownDependsOnReason()
    {
        var time = new ManualTime();
        var cooldowns = new KeyCooldowns(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30), TimeSpan.FromHours(6));
        var keys = new ApiKeyPool(["key-a-1111", "key-b-2222"], cooldowns, time);

        keys.ReportFailure(new PoolLease<string>(0, "key-a-1111"), KeyFailure.Unauthorized);
        keys.ReportFailure(new PoolLease<string>(1, "key-b-2222"), KeyFailure.Payment);

        Assert.Equal(time.Now + TimeSpan.FromHours(6), keys.Snapshot()[0].Health.CoolingUntil);
        Assert.Equal(time.Now + TimeSpan.FromMinutes(30), keys.Snapshot()[1].Health.CoolingUntil);
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.ReportFailure(keys.Lease(), KeyFailure.Unspecified));
    }

    [Fact]
    public void ApiKeyPool_FailureOf_BlamesOnlyKeyCodes()
    {
        Assert.Equal(KeyFailure.Unauthorized, ApiKeyPool.FailureOf(HttpStatusCode.Unauthorized));
        Assert.Equal(KeyFailure.Unauthorized, ApiKeyPool.FailureOf(HttpStatusCode.Forbidden));
        Assert.Equal(KeyFailure.Payment, ApiKeyPool.FailureOf(HttpStatusCode.PaymentRequired));
        Assert.Equal(KeyFailure.RateLimit, ApiKeyPool.FailureOf(HttpStatusCode.TooManyRequests));
        Assert.Null(ApiKeyPool.FailureOf(HttpStatusCode.BadRequest));
        Assert.Null(ApiKeyPool.FailureOf(HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public void ApiKeyPool_Snapshot_HidesKeyValues()
    {
        var keys = new ApiKeyPool([Good]);

        string shown = string.Join(";", keys.Snapshot().Select(h => h.ToString()));

        Assert.DoesNotContain(Good, shown);
        Assert.Equal("…0001", keys.Snapshot()[0].Mask);
    }

    [Fact]
    public void CooldownPool_SameRuleForRoutes_ThresholdNearestAndReset()
    {
        var time = new ManualTime();
        var pool = new CooldownPool<string>(["route-a", "route-b"], failuresBeforeCooldown: 2, time);
        var a = new PoolLease<string>(0, "route-a");
        var b = new PoolLease<string>(1, "route-b");

        pool.ReportFailure(a, TimeSpan.FromMinutes(1));
        Assert.Equal(2, pool.AvailableCount);

        pool.ReportFailure(a, TimeSpan.FromMinutes(1));
        pool.ReportFailure(b, TimeSpan.FromMinutes(2));
        pool.ReportFailure(b, TimeSpan.FromMinutes(2));

        // Все в карантине: строгий выбор отвечает «нет», обычный — ближайший к возвращению.
        Assert.False(pool.TryLeaseAvailable(out _));
        Assert.Equal("route-a", pool.Lease().Item);

        time.Now += TimeSpan.FromMinutes(1);
        Assert.True(pool.TryLeaseAvailable(out PoolLease<string> back));
        Assert.Equal("route-a", back.Item);
        Assert.Equal(0, pool.Snapshot()[0].ConsecutiveFailures);
    }

    [Fact]
    public void ProviderHttpClient_ReadyClientWithProxies_IsRejected()
    {
        using var client = new HttpClient();

        Assert.Throws<ArgumentException>(() => new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Client = client,
            Proxies = [new WebProxy("http://127.0.0.1:9")],
        }));
    }

    private static HttpRequestMessage Get() => new(HttpMethod.Get, "http://provider.test/v1/ping");

    /// <summary>Отвечает кодом по ключу из заголовка и запоминает ключи.</summary>
    private sealed class KeyHandler(Func<string?, HttpStatusCode> status) : HttpMessageHandler
    {
        public List<string?> Keys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthenticationHeaderValue? auth = request.Headers.Authorization;
            string? key = auth?.Scheme == "Bearer" ? auth.Parameter : null;
            lock (Keys)
                Keys.Add(key);
            return Task.FromResult(new HttpResponseMessage(status(key)) { Content = new StringContent("{}") });
        }
    }
}

/// <summary>Часы, которые двигает тест.</summary>
internal sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}
