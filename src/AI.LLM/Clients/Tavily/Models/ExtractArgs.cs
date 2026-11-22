using System.Text.Json.Serialization;

namespace AI.LLM.Clients.Tavily.Models;

public class ExtractArgs
{
    [Obsolete("Ключ передаётся заголовком Authorization: Bearer через конструктор TavilyClient; свойство игнорируется и будет удалено в следующей мажорной версии")]
    [JsonIgnore]
    public string ApiKey { get; set; }

    [JsonPropertyName("urls")]
    public IEnumerable<string> Urls { get; set; }

    [JsonPropertyName("include_images")]
    public bool IncludeImages { get; set; }

    [JsonPropertyName("extract_depth")]
    public string ExtractDepth { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; }
}
