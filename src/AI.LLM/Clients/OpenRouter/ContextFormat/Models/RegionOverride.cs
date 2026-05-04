using System.Text.Json.Serialization;

namespace AI.LLM.Clients.OpenRouter.ContextFormat.Models;

public class RegionOverride
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }
}
