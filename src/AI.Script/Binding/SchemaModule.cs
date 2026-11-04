using AI.Script.Runtime;
using System.Text.Json.Nodes;

namespace AI.Script.Binding;

/// <summary>Функция хоста, описанная схемой JSON: имя, описание и схема аргументов.</summary>
/// <param name="Name">Имя функции в пространстве модуля.</param>
/// <param name="Description">Описание для справки и подсказок.</param>
/// <param name="InputSchema">Схема аргументов (<c>properties</c>, <c>required</c>); <c>null</c> — без аргументов.</param>
public sealed record SchemaTool(string Name, string Description, JsonObject? InputSchema);

/// <summary>
/// Модуль языка из функций, описанных схемой JSON: каждая становится функцией с именованными
/// аргументами, а вызов приходит хосту объектом JSON.
/// </summary>
/// <remarks>
/// Один построитель для проверки и для исполнения: сервер проверяет скрипт модулем из того же
/// описания, по которому клиент его исполнит, и опечатка в имени аргумента ловится одинаково
/// с обеих сторон.
/// </remarks>
public static class SchemaModule
{
    /// <summary>Строит модуль.</summary>
    /// <param name="name">Пространство имен.</param>
    /// <param name="description">Описание модуля.</param>
    /// <param name="tools">Функции.</param>
    /// <param name="invoke">Исполнение вызова: имя функции и аргументы без пропущенных необязательных.</param>
    public static ScriptModule Build(
        string name,
        string description,
        IEnumerable<SchemaTool> tools,
        Func<string, JsonObject, IScriptContext, ValueTask<ScriptValue>> invoke) =>
        new(name, description, "1", [.. tools.Select(tool => Function(name, tool, invoke))]);

    /// <summary>Функции из списка JSON: <c>name</c>, <c>description</c> и схема в <c>input_schema</c> либо <c>inputSchema</c>, как у MCP.</summary>
    public static IEnumerable<SchemaTool> ToolsOf(JsonArray tools) =>
        tools.OfType<JsonObject>()
            .Where(t => t["name"] is not null)
            .Select(t => new SchemaTool(
                t["name"]!.GetValue<string>(),
                t["description"]?.GetValue<string>() ?? string.Empty,
                (t["input_schema"] ?? t["inputSchema"]) as JsonObject));

    private static ScriptFunction Function(string ns, SchemaTool tool, Func<string, JsonObject, IScriptContext, ValueTask<ScriptValue>> invoke)
    {
        var parameters = Parameters(tool.InputSchema);
        return new ScriptFunction
        {
            Namespace = ns,
            Name = tool.Name,
            Description = tool.Description,
            Parameters = parameters,
            ReturnType = ScriptType.Any,
            Invoke = (arguments, context) => invoke(tool.Name, Arguments(parameters, arguments), context),
        };
    }

    /// <summary>Обязательные идут первыми: так позиционный вызов не перепутает их с необязательными.</summary>
    private static List<ScriptParameter> Parameters(JsonObject? schema)
    {
        if (schema?["properties"] is not JsonObject properties) return [];

        var required = (schema["required"] as JsonArray)?.Select(r => r?.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        return [.. properties
            .Select(p => new ScriptParameter
            {
                Name = p.Key,
                Type = TypeOf(p.Value as JsonObject),
                Description = p.Value?["description"]?.GetValue<string>() ?? string.Empty,
                IsOptional = !required.Contains(p.Key),
            })
            .OrderBy(p => p.IsOptional)];
    }

    private static JsonObject Arguments(IReadOnlyList<ScriptParameter> parameters, ScriptValue[] values)
    {
        var args = new JsonObject();
        for (int i = 0; i < parameters.Count && i < values.Length; i++)
        {
            if (values[i].Type == ScriptType.None && parameters[i].IsOptional) continue;
            args[parameters[i].Name] = ScriptJson.ToNode(values[i]);
        }

        return args;
    }

    private static ScriptType TypeOf(JsonObject? property) => property?["type"]?.ToString() switch
    {
        "string" => ScriptType.Str,
        "integer" or "number" => ScriptType.Num,
        "boolean" => ScriptType.Bool,
        "array" => ScriptType.List,
        "object" => ScriptType.Record,
        _ => ScriptType.Any,
    };
}
