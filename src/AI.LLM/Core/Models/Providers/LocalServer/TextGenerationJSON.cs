using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Providers.LocalServer;

[Serializable]
public class TextGenerationJSON
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; }
}
