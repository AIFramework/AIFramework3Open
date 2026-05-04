using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.ToolCalling;

/// <summary>
/// Вызов инструмента, возвращаемый ассистентом в поле "tool_calls".
/// </summary>
[Serializable]
public class ToolCall
{
    /// <summary>
    /// Уникальный идентификатор вызова (например "call_abc123").
    /// Используется для сопоставления с ответом role=tool.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionCall Function { get; set; }

    /// <summary>
    /// Индекс в массиве tool_calls (используется при стриминге для накопления).
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }
}

/// <summary>
/// Имя и аргументы вызванной функции.
/// </summary>
[Serializable]
public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// JSON-строка с аргументами функции.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; }
}
