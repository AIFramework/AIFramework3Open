using AI.Script.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AI.Script.Binding;

/// <summary>
/// Значения языка и JSON в обе стороны: аргументы функции хоста уходят объектом JSON, ответ
/// приходит записью.
/// </summary>
/// <remarks>
/// Нужен хостам, у которых функции описаны схемой JSON, а не методами C#: инструменты MCP,
/// операции над компьютером. Вектор становится массивом чисел, диапазон тоже: в JSON их нет.
/// </remarks>
public static class ScriptJson
{
    /// <summary>JSON в значение языка: объект становится записью, массив списком, простой текст строкой.</summary>
    public static ScriptValue FromNode(JsonNode? node) => node switch
    {
        null => ScriptValue.None,
        JsonObject record => ScriptValue.Record(ScriptRecord.From(record.Select(p => KeyValuePair.Create(p.Key, FromNode(p.Value))))),
        JsonArray list => ScriptValue.List(ScriptList.From(list.Select(FromNode))),
        JsonValue value => FromValue(value),
        _ => ScriptValue.None,
    };

    /// <summary>
    /// Ответ функции хоста: JSON-объект становится записью, иначе запись <c>{ text }</c>.
    /// </summary>
    /// <remarks>Одна форма ответа нужна скрипту: <c>r.text</c> работает у любой функции.</remarks>
    public static ScriptValue FromAnswer(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith('{'))
        {
            try
            {
                if (JsonNode.Parse(text) is JsonObject record) return FromNode(record);
            }
            catch (JsonException)
            {
            }
        }

        return ScriptValue.Record(ScriptRecord.From([KeyValuePair.Create("text", ScriptValue.Str(text ?? string.Empty))]));
    }

    /// <summary>Значение языка в JSON; функции и дескрипторы в JSON не переводятся.</summary>
    public static JsonNode? ToNode(ScriptValue value) => value.Type switch
    {
        ScriptType.None => null,
        ScriptType.Num => JsonValue.Create(value.RawNumber),
        ScriptType.Dec => JsonValue.Create(value.AsDecimal()),
        ScriptType.Bool => JsonValue.Create(value.AsBool()),
        ScriptType.Str => JsonValue.Create(value.AsString()),
        ScriptType.Date => JsonValue.Create(value.AsDate()),
        ScriptType.Dur => JsonValue.Create(value.AsDuration().TotalSeconds),
        ScriptType.Vec => new JsonArray([.. value.AsVector().Select(x => (JsonNode)JsonValue.Create(x))]),
        ScriptType.Range => new JsonArray([.. value.AsRange().Values().Select(x => (JsonNode)JsonValue.Create(x))]),
        ScriptType.List => new JsonArray([.. value.AsList().Select(ToNode)]),
        ScriptType.Record => ToObject(value.AsRecord()),
        ScriptType.Handle when value.AsHandle().IsDeferred => throw ScriptValue.Deferred("аргумент"),
        _ => throw new ScriptError(
            Semantics.DiagnosticCodes.BadOperand,
            $"значение типа {value.Type.ToName()} не передается функции хоста",
            "передайте число, строку, признак, список или запись"),
    };

    private static JsonObject ToObject(ScriptRecord record)
    {
        var result = new JsonObject();
        foreach (var pair in record.Pairs()) result[pair.Key] = ToNode(pair.Value);
        return result;
    }

    private static ScriptValue FromValue(JsonValue value) => value.GetValueKind() switch
    {
        JsonValueKind.String => ScriptValue.Str(value.GetValue<string>()),
        JsonValueKind.Number => ScriptValue.Num(value.GetValue<double>()),
        JsonValueKind.True => ScriptValue.True,
        JsonValueKind.False => ScriptValue.False,
        _ => ScriptValue.None,
    };
}
