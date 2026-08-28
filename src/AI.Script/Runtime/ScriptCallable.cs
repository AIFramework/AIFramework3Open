namespace AI.Script.Runtime;

/// <summary>
/// Всё, что можно вызвать: лямбда, объявленная скриптом функция, функция модуля.
/// </summary>
/// <remarks>
/// Общий базовый тип нужен затем, чтобы функция была обычным значением: <c>xs |> core.map(zscore)</c>
/// не должно различать, чем именно является <c>zscore</c> — телом на языке или привязкой к C#.
/// </remarks>
public abstract class ScriptCallable
{
    /// <summary>Имя для печати и диагностики.</summary>
    public abstract string Name { get; }

    /// <summary>Имена параметров в порядке объявления.</summary>
    public abstract IReadOnlyList<string> ParameterNames { get; }

    /// <inheritdoc/>
    public override string ToString() => $"<fn {Name}({string.Join(", ", ParameterNames)})>";
}
