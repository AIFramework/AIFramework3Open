namespace AI.Script.Runtime;

/// <summary>
/// Константы, доступные скрипту без объявления.
/// </summary>
/// <remarks>
/// Список общий для проверяющего прохода и интерпретатора. Если бы каждый знал свой, скрипт с
/// <c>pi</c> либо не проходил проверку, либо падал при исполнении — в зависимости от того, кто
/// из двоих отстал.
/// </remarks>
public static class ScriptConstants
{
    /// <summary>Имя и значение каждой константы.</summary>
    public static IReadOnlyDictionary<string, ScriptValue> All { get; } = new Dictionary<string, ScriptValue>(StringComparer.Ordinal)
    {
        ["pi"] = ScriptValue.Num(Math.PI),
        ["e"] = ScriptValue.Num(Math.E),
        ["tau"] = ScriptValue.Num(Math.Tau),
        ["phi"] = ScriptValue.Num(1.618033988749895),
    };
}
