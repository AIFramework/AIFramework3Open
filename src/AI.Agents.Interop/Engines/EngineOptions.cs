using System.Text.Json;
using System.Text.Json.Nodes;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Параметры адаптеров движков из <c>EngineInput.Options</c>. Неизвестный ключ или значение — явный отказ, а не
/// молчаливый пропуск.
/// </summary>
internal sealed class EngineOptions
{
    /// <summary>Ключ: предел шагов цикла ReAct (целое ≥ 1).</summary>
    public const string MaxIterationsKey = "maxIterations";

    /// <summary>Ключ: способ решений, <c>native</c> или <c>json</c>.</summary>
    public const string DecisionsKey = "decisions";

    /// <summary>Значение <see cref="DecisionsKey"/>: нативные вызовы инструментов.</summary>
    public const string Native = "native";

    /// <summary>Значение <see cref="DecisionsKey"/>: структурированный текст.</summary>
    public const string Json = "json";

    /// <summary>Предел шагов; <see langword="null"/>: как у <c>ReActConfig</c> по умолчанию.</summary>
    public int? MaxIterations { get; private init; }

    /// <summary>Способ решений; <see langword="null"/>: по <c>ModelCapabilities.ToolCalling</c> роли <c>decide</c>.</summary>
    public string? Decisions { get; private init; }

    /// <summary>Разбирает параметры.</summary>
    /// <param name="options">Параметры или <see langword="null"/>.</param>
    /// <param name="parsed">Разобранные параметры.</param>
    /// <param name="error">Почему не разобраны.</param>
    /// <returns><see langword="true"/>, если разобраны.</returns>
    public static bool TryParse(JsonObject? options, out EngineOptions parsed, out string? error)
    {
        parsed = new EngineOptions();
        error = null;
        if (options == null)
            return true;

        int? maxIterations = null;
        string? decisions = null;

        foreach ((string key, JsonNode? value) in options)
        {
            switch (key)
            {
                case MaxIterationsKey:
                    if (value is not JsonValue number || number.GetValueKind() != JsonValueKind.Number
                        || !number.TryGetValue(out int count) || count < 1)
                    {
                        error = $"{MaxIterationsKey}: нужно целое число не меньше 1";
                        return false;
                    }

                    maxIterations = count;
                    break;

                case DecisionsKey:
                    if (value is not JsonValue text || !text.TryGetValue(out string? name) || name is not (Native or Json))
                    {
                        error = $"{DecisionsKey}: допустимы «{Native}» и «{Json}»";
                        return false;
                    }

                    decisions = name;
                    break;

                default:
                    error = $"неизвестный параметр «{key}»; допустимы {MaxIterationsKey} и {DecisionsKey}";
                    return false;
            }
        }

        parsed = new EngineOptions { MaxIterations = maxIterations, Decisions = decisions };
        return true;
    }
}
