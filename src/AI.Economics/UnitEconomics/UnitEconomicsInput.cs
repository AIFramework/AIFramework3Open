using AI.DataStructs.Algebraic;

namespace AI.Economics.UnitEconomics;

/// <summary>
/// Вход расчёта юнит-экономики одного сегмента (продукта, канала, когорты).
/// </summary>
/// <remarks>
/// Все денежные величины — в одной валюте и на один период (обычно месяц).
/// Период задаётся неявно: если ARPU месячный, то и отток, и ставка
/// дисконтирования должны быть месячными, а горизонт считается в месяцах.
/// </remarks>
public sealed record UnitEconomicsInput
{
    /// <summary>Затраты на маркетинг за период привлечения.</summary>
    public double MarketingSpend { get; init; }

    /// <summary>Затраты на продажи (ФОТ отдела, комиссии) за тот же период.</summary>
    public double SalesSpend { get; init; }

    /// <summary>Число привлечённых клиентов за период.</summary>
    public double NewCustomers { get; init; }

    /// <summary>
    /// Готовое значение CAC. Если задано (не <c>NaN</c>), затраты и число
    /// клиентов игнорируются — удобно, когда CAC получен извне.
    /// </summary>
    public double CacOverride { get; init; } = double.NaN;

    /// <summary>Средний доход с клиента за период (ARPU / ARPA).</summary>
    public double RevenuePerPeriod { get; init; }

    /// <summary>Доля валовой маржи в выручке, от 0 до 1.</summary>
    public double GrossMarginRate { get; init; } = 1.0;

    /// <summary>
    /// Дополнительные переменные затраты на клиента за период, не вошедшие
    /// в валовую маржу: поддержка, платёжные комиссии, инфраструктура.
    /// </summary>
    public double VariableCostPerPeriod { get; init; }

    /// <summary>Отток за период, от 0 до 1. Используется, если не задана <see cref="Survival"/>.</summary>
    public double ChurnRate { get; init; }

    /// <summary>Ставка дисконтирования за период. Ноль — считать без дисконтирования.</summary>
    public double DiscountRate { get; init; }

    /// <summary>
    /// Горизонт в периодах. Ноль означает бесконечный горизонт —
    /// допустим только при <c>ChurnRate &gt; 0</c> или затухающей кривой.
    /// </summary>
    public int Horizon { get; init; }

    /// <summary>
    /// Произвольная кривая удержания <c>S(0) = 1, S(1), S(2), ...</c>.
    /// Задана — используется вместо постоянного оттока: именно так подключается
    /// результат подгонки когортных кривых из <see cref="AI.Economics.Cohorts"/>.
    /// </summary>
    public Vector? Survival { get; init; }
}
