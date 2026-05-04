using AI.LLM.Clients.Tavily;

namespace AI.LLM.Agents.Tools.Builtin;

/// <summary>
/// Готовый инструмент веб-поиска через Tavily API.
/// </summary>
public sealed class TavilySearchTool
{
    private readonly TavilyClient _client;

    /// <summary>
    /// Готовый инструмент веб-поиска через Tavily API.
    /// </summary>
    /// <param name="client">Настроенный экземпляр <see cref="TavilyClient"/>.</param>
    public TavilySearchTool(TavilyClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Ищет информацию в интернете через Tavily и возвращает краткий результат.
    /// </summary>
    [AgentTool("tavily_search", "Ищет информацию в интернете и возвращает релевантные результаты")]
    public async Task<string> SearchAsync(
        [ToolParameter("Поисковый запрос")] string query,
        [ToolParameter("Максимальное число результатов")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.SearchAsync(
            query, maxResults: maxResults,
            includeRawContent: false,
            cancellationToken: cancellationToken);

        if (result?.Results == null || !result.Results.Any())
            return "Результатов не найдено.";

        var sb = new System.Text.StringBuilder();
        foreach (var r in result.Results)
        {
            sb.AppendLine($"### {r.Title}");
            sb.AppendLine(r.Content);
            sb.AppendLine($"URL: {r.Url}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
