namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Трактовка оси «повтор безопасен»: одно место, где решается, как понимать незаданное значение.
/// </summary>
public static class ToolIdempotencies
{
    /// <summary>
    /// Значение, по которому решают повтор: <see cref="ToolIdempotency.Unknown"/> и любое необъявленное
    /// значение становятся <see cref="ToolIdempotency.NotIdempotent"/>, остальные остаются как есть.
    /// </summary>
    /// <param name="idempotency">Объявленное значение.</param>
    /// <returns>Значение для решения: никогда не <see cref="ToolIdempotency.Unknown"/>.</returns>
    public static ToolIdempotency Effective(this ToolIdempotency idempotency) => idempotency switch
    {
        ToolIdempotency.Idempotent => ToolIdempotency.Idempotent,
        ToolIdempotency.WithKey => ToolIdempotency.WithKey,
        _ => ToolIdempotency.NotIdempotent,
    };

    /// <summary>
    /// То же для необязательного поля <see cref="ToolSpec.Idempotency"/>: <see langword="null"/> (поле не задано)
    /// трактуется как <see cref="ToolIdempotency.Unknown"/>, то есть «повторять нельзя».
    /// </summary>
    /// <param name="idempotency">Объявленное значение или <see langword="null"/>.</param>
    /// <returns>Значение для решения: никогда не <see cref="ToolIdempotency.Unknown"/>.</returns>
    public static ToolIdempotency Effective(this ToolIdempotency? idempotency) =>
        (idempotency ?? ToolIdempotency.Unknown).Effective();
}
