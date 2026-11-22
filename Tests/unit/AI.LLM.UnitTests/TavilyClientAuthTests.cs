using System.Net;
using System.Text;
using System.Text.Json;
using AI.LLM.Clients.Tavily;
using AI.LLM.Clients.Tavily.Models;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Авторизация клиента Tavily и разбор ответа. В спецификации Tavily ключ передаётся только
/// заголовком <c>Authorization: Bearer</c>; поле <c>api_key</c> в теле сервер пока ещё читает,
/// но неофициально, и отказ от него проявится молчаливым 401.
/// </summary>
public class TavilyClientAuthTests
{
    private const string Key = "tvly-test-0123456789";

    private sealed class Handler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _answer;

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        public Handler(string answer, HttpStatusCode status = HttpStatusCode.OK)
        {
            _answer = answer;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(_status) { Content = new StringContent(_answer, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Search_SendsBearerHeader_AndNoKeyInBody()
    {
        var handler = new Handler("""{"results": []}""");
        using var client = new TavilyClient(Key, handler);

        await client.SearchAsync("запрос", includeRawContent: false);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/search", request.RequestUri!.AbsolutePath);
        AssertBearer(request);
        AssertNoKey(handler.Bodies[0]);
    }

    [Fact]
    public async Task Extract_SendsBearerHeader_AndNoKeyInBody()
    {
        var handler = new Handler("""{"results": [{"url": "https://site/page", "raw_content": "текст"}], "failed_results": []}""");
        using var client = new TavilyClient(Key, handler);

        await client.ExtractAsync(["https://site/page"]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/extract", request.RequestUri!.AbsolutePath);
        AssertBearer(request);
        AssertNoKey(handler.Bodies[0]);
    }

    /// <summary>Ключ не должен попадать в текст исключения при отказе сервера.</summary>
    [Fact]
    public async Task Search_Unauthorized_DoesNotLeakKeyInException()
    {
        var handler = new Handler("""{"detail": {"error": "Unauthorized"}}""", HttpStatusCode.Unauthorized);
        using var client = new TavilyClient(Key, handler);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("запрос", includeRawContent: false));

        Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public void Constructor_WithoutKey_DoesNotMentionKeyValue()
    {
        var error = Assert.Throws<ArgumentException>(() => new TavilyClient("", new Handler("{}")));

        Assert.Contains("apiKey", error.Message);
    }

    /// <summary>
    /// С июля 2025 <c>search_depth=basic</c> отдаёт <c>content</c> фрагментами, склеенными через
    /// «[...]». Разбор не должен падать и не должен терять ни одного фрагмента.
    /// </summary>
    [Fact]
    public void SearchResult_KeepsContentFragments()
    {
        const string content = "Первый фрагмент. [...] Второй фрагмент [со скобками]. [...] Третий.";
        var json = JsonSerializer.Serialize(new { results = new[] { new { url = "https://site/page", title = "Т", content } } });

        var item = Assert.Single(JsonSerializer.Deserialize<SearchResult>(json)!.Results);

        Assert.Equal(content, item.Content);
    }

    [Fact]
    public async Task Search_KeepsContentFragments_ThroughClient()
    {
        const string content = "Начало [...] середина [...] конец";
        var answer = JsonSerializer.Serialize(new
        {
            results = new[] { new { url = "https://site/page", title = "Т", content, raw_content = "полный текст" } },
        });
        using var client = new TavilyClient(Key, new Handler(answer));

        var result = await client.SearchAsync("запрос");

        Assert.Equal(content, Assert.Single(result.Results).Content);
    }

    /// <summary>Храповик: ключ больше не уходит полем тела, и возвращать его некуда.</summary>
    [Fact]
    public void TavilyClientSources_DoNotMentionApiKeyField()
    {
        var directory = Path.Combine(FindRoot(), "src", "AI.LLM", "Clients", "Tavily");
        var offenders = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("api_key", StringComparison.Ordinal))
            .ToArray();

        Assert.True(Directory.Exists(directory), directory);
        Assert.Empty(offenders);
    }

    private static void AssertBearer(HttpRequestMessage request)
    {
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, request.Headers.Authorization.Parameter);
    }

    private static void AssertNoKey(string body)
    {
        Assert.DoesNotContain("api_key", body);
        Assert.DoesNotContain(Key, body);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AIFramework.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException($"Не найден корень репозитория (AIFramework.sln) выше {AppContext.BaseDirectory}.");
    }
}
