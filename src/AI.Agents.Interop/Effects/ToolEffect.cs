using AI.Agents.Contracts.Tools;

namespace AI.Agents.Interop.Effects;

/// <summary>
/// Вид действия и «повтор безопасен» инструмента одной записью: то, что гейт эффектов берет из описания
/// инструмента. Одна запись вместо двух словарей, чтобы оси не расходились.
/// </summary>
/// <param name="Action">Вид действия (<see cref="ToolSpec.Action"/>).</param>
/// <param name="Idempotency">Безопасен ли повтор (<see cref="ToolSpec.Idempotency"/>).</param>
internal readonly record struct ToolEffect(ToolAction Action, ToolIdempotency Idempotency)
{
    /// <summary>Эффект из описания; не объявленная повторяемость — <see cref="ToolIdempotency.Unknown"/>.</summary>
    /// <param name="spec">Описание инструмента.</param>
    public static ToolEffect Of(ToolSpec spec) => new(spec.Action, spec.Idempotency ?? ToolIdempotency.Unknown);

    /// <summary>Эффекты по имени инструмента; при повторе имени побеждает последнее описание.</summary>
    /// <param name="specs">Описания инструментов.</param>
    /// <param name="paramName">Имя параметра для ошибки.</param>
    public static Dictionary<string, ToolEffect> ByName(IEnumerable<ToolSpec> specs, string paramName)
    {
        ArgumentNullException.ThrowIfNull(specs, paramName);
        var effects = new Dictionary<string, ToolEffect>(ToolSpec.NameComparer);
        foreach (ToolSpec spec in specs)
        {
            ArgumentNullException.ThrowIfNull(spec, paramName);
            effects[spec.Name] = Of(spec);
        }

        return effects;
    }
}
