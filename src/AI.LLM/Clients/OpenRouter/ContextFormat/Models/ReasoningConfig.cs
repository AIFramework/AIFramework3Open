using System.Text.Json.Serialization;

namespace AI.LLM.Clients.OpenRouter.ContextFormat.Models;

// Конфиг reasoning
public class ReasoningConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
