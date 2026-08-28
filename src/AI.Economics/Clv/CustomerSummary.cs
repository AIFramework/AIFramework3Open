namespace AI.Economics.Clv;

/// <summary>
/// Сводка транзакций одного клиента в формате RFM, принятом в моделях
/// «покупок без контракта» (BG/NBD, Pareto/NBD, Gamma-Gamma).
/// </summary>
/// <remarks>
/// Время измеряется в одних и тех же единицах (обычно неделях или месяцах)
/// и отсчитывается от <b>первой</b> покупки клиента, а не от начала выборки.
/// Отсюда и определение <see cref="Frequency"/> как числа <b>повторных</b>
/// покупок: клиент с единственной покупкой имеет <c>Frequency = 0</c>.
/// </remarks>
public sealed record CustomerSummary
{
    /// <summary>Идентификатор клиента.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Число повторных покупок <c>x</c> (без учёта первой).</summary>
    public double Frequency { get; init; }

    /// <summary>Момент последней покупки <c>t_x</c>, считая от первой.</summary>
    public double Recency { get; init; }

    /// <summary>Длительность наблюдения <c>T</c>, считая от первой покупки.</summary>
    public double Age { get; init; }

    /// <summary>Средний чек повторных покупок. Нужен только для Gamma-Gamma.</summary>
    public double MonetaryValue { get; init; }
}
