using AI.DataStructs.Algebraic;

using AI.Economics.Insights;

namespace AI.Economics.UnitEconomics;

/// <summary>
/// Результат расчёта юнит-экономики: CAC, LTV, их отношение и окупаемость.
/// </summary>
public sealed partial record UnitEconomicsResult
{
    /// <summary>Стоимость привлечения клиента.</summary>
    public double Cac { get; init; }

    /// <summary>Средний доход с клиента за период.</summary>
    public double Arpu { get; init; }

    /// <summary>Маржинальный вклад одного клиента за период.</summary>
    public double ContributionPerPeriod { get; init; }

    /// <summary>Доля маржинального вклада в выручке.</summary>
    public double ContributionMarginRate { get; init; }

    /// <summary>Пожизненная ценность клиента с учётом дисконтирования и горизонта.</summary>
    public double Ltv { get; init; }

    /// <summary>Недисконтированная пожизненная ценность — для сверки с «наивным» расчётом.</summary>
    public double UndiscountedLtv { get; init; }

    /// <summary>Отношение LTV к CAC. Ориентир для SaaS — не ниже 3.</summary>
    public double LtvToCac { get; init; }

    /// <summary>Прибыль на клиента за вычетом стоимости привлечения.</summary>
    public double NetContribution { get; init; }

    /// <summary>
    /// Срок окупаемости привлечения в периодах, дробный.
    /// <c>NaN</c>, если на заданном горизонте клиент не окупается.
    /// </summary>
    public double CacPaybackPeriods { get; init; }

    /// <summary>Ожидаемая длительность жизни клиента в периодах.</summary>
    public double ExpectedLifetimePeriods { get; init; }

    /// <summary>Число периодов, по которым фактически вёлся расчёт.</summary>
    public int HorizonUsed { get; init; }

    /// <summary>Кривая удержания, использованная в расчёте.</summary>
    public Vector Survival { get; init; } = new Vector(0);

    /// <summary>
    /// Накопленный дисконтированный вклад по периодам за вычетом CAC.
    /// Пересечение нуля — точка окупаемости привлечения.
    /// </summary>
    public Vector CumulativeNet { get; init; } = new Vector(0);
}
