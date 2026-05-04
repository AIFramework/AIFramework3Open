using System.Text.Json.Serialization;

namespace AI.LLM.Clients.OpenRouter.ContextFormat.Models;

public class Icon
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}
