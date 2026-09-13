using System.Text.Json.Nodes;

namespace AI.LLM.Agents.Planning;

/// <summary>
/// Строгая схема ответа планировщика: поле <c>tool</c> закрыто перечнем имён инструментов на
/// декодере, и выдуманное имя невозможно физически, а не отлавливается проверкой после.
/// </summary>
/// <remarks>
/// <para>
/// Строгий режим провайдеров запрещает словари с произвольными ключами (<c>additionalProperties</c>
/// обязан быть <c>false</c>, все свойства перечислены), поэтому <c>args</c>, <c>outputs</c> и
/// <c>input_mapping</c> идут массивами пар <c>{"key", "value"}</c>; разбор
/// (<see cref="PlanGenerator"/>) принимает обе формы, свободный JSON по-прежнему пишет словари.
/// </para>
/// <para>
/// «Инструмента нет» это пустая строка <see cref="NoTool"/>, а не <c>null</c>: <c>null</c> в
/// перечне держат не все провайдеры, а пустую строку держат все.
/// </para>
/// </remarks>
public static class PlanSchema
{
    /// <summary>Имя схемы для провайдера.</summary>
    public const string Name = "plan";

    /// <summary>Значение <c>tool</c> у ручного шага: инструмента нет.</summary>
    public const string NoTool = "";

    /// <summary>Схема плана с закрытым перечнем инструментов.</summary>
    /// <param name="toolNames">Имена инструментов, доступных планировщику; повторы снимаются.</param>
    public static string Build(IEnumerable<string> toolNames)
    {
        var names = new JsonArray(NoTool);
        foreach (var name in toolNames.Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.Ordinal))
            names.Add(name);

        var step = Strict(new JsonObject
        {
            ["id"] = String(),
            ["description"] = String(),
            ["tool"] = new JsonObject { ["type"] = "string", ["enum"] = names },
            ["done_when"] = String(),
            ["depends_on"] = new JsonObject { ["type"] = "array", ["items"] = String() },
            ["args"] = Pairs(),
            ["outputs"] = Pairs(),
            ["input_mapping"] = Pairs(),
        });

        return Strict(new JsonObject { ["steps"] = new JsonObject { ["type"] = "array", ["items"] = step } }).ToJsonString();
    }

    /// <summary>Объект со всеми свойствами в <c>required</c> и без лишних: так требует строгий режим.</summary>
    private static JsonObject Strict(JsonObject properties)
    {
        var required = new JsonArray();
        foreach (var property in properties)
            required.Add(property.Key);

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject String() => new() { ["type"] = "string" };

    private static JsonObject Pairs() => new()
    {
        ["type"] = "array",
        ["items"] = Strict(new JsonObject { ["key"] = String(), ["value"] = String() }),
    };
}
