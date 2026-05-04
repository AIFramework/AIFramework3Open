using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.ToolCalling;

/// <summary>
/// Описание инструмента для передачи в LLM (поле "tools" в запросе).
/// Соответствует формату OpenAI Chat Completions API.
/// </summary>
[Serializable]
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition Function { get; set; }

    /// <summary>
    /// Фабричный метод для создания определения функции.
    /// </summary>
    public static ToolDefinition Create(string name, string description, string parametersJson = null)
    {
        return new ToolDefinition
        {
            Function = new FunctionDefinition
            {
                Name = name,
                Description = description,
                Parameters = string.IsNullOrEmpty(parametersJson)
                    ? null
                    : JsonDocument.Parse(parametersJson).RootElement.Clone(),
            }
        };
    }
}

/// <summary>
/// Описание функции внутри <see cref="ToolDefinition"/>.
/// </summary>
[Serializable]
public class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// JSON Schema параметров функции. Может быть null если параметров нет.
    /// </summary>
    [JsonPropertyName("parameters")]
    public JsonElement? Parameters { get; set; }
}
