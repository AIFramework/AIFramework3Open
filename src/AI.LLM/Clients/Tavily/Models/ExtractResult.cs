using System.Text.Json.Serialization;

namespace AI.LLM.Clients.Tavily.Models;

public class ExtractResult
{
    [JsonPropertyName("results")]
    public IEnumerable<ExtractItemResult> Results { get; set; }

    [JsonPropertyName("failed_results")]
    public IEnumerable<ExtractItemFailedResult> FailedResults { get; set; }
}

public class ExtractItemResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("raw_content")]
    public string RawContent { get; set; }

    /// <summary>Картинки страницы; приходят только при <c>include_images=true</c>.</summary>
    [JsonPropertyName("images")]
    public IEnumerable<TavilyImage> Images { get; set; }
}

public class ExtractItemFailedResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }
}