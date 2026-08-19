using System.Text.Json.Serialization;

namespace AI.LLM.Clients.Tavily.Models;

public class SearchResult
{
    [JsonPropertyName("query")]
    public string Query { get; set; }

    [JsonPropertyName("response_time")]
    public double ResponseTime { get; set; }

    [JsonPropertyName("results")]
    public IEnumerable<SearchItemResult> Results { get; set; }

    /// <summary>Готовая выжимка по запросу; приходит только при <c>include_answer=true</c>.</summary>
    [JsonPropertyName("answer")]
    public string Answer { get; set; }

    /// <summary>Картинки выдачи; приходят только при <c>include_images=true</c>.</summary>
    [JsonPropertyName("images")]
    public IEnumerable<TavilyImage> Images { get; set; }
}

public class SearchItemResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("raw_content")]
    public string RawContent { get; set; }
}
