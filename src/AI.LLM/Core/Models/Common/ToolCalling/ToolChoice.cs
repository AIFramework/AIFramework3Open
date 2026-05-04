using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.ToolCalling;

/// <summary>
/// Управление выбором инструмента.
/// Сериализуется как строка ("auto", "none", "required") или как объект {"type":"function","function":{"name":"..."}}.
/// </summary>
[JsonConverter(typeof(ToolChoiceJsonConverter))]
public class ToolChoice
{
    private readonly string _mode;
    private readonly string _functionName;

    private ToolChoice(string mode, string functionName = null)
    {
        _mode = mode;
        _functionName = functionName;
    }

    public bool IsStringMode => _functionName == null;
    public string Mode => _mode;
    public string FunctionName => _functionName;

    public static ToolChoice Auto() => new("auto");
    public static ToolChoice None() => new("none");
    public static ToolChoice Required() => new("required");
    public static ToolChoice ForFunction(string name) => new("function", name);
}

public class ToolChoiceJsonConverter : JsonConverter<ToolChoice>
{
    public override ToolChoice Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return value switch
            {
                "none" => ToolChoice.None(),
                "required" => ToolChoice.Required(),
                _ => ToolChoice.Auto(),
            };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.TryGetProperty("function", out var fn) &&
                fn.TryGetProperty("name", out var name))
            {
                return ToolChoice.ForFunction(name.GetString());
            }
            return ToolChoice.Auto();
        }

        throw new JsonException("ToolChoice must be a string or object.");
    }

    public override void Write(Utf8JsonWriter writer, ToolChoice value, JsonSerializerOptions options)
    {
        if (value.IsStringMode)
        {
            writer.WriteStringValue(value.Mode);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WriteStartObject("function");
            writer.WriteString("name", value.FunctionName);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}
