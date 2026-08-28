using System.Collections.Generic;
using AI.DataStructs.Algebraic;

using AI.Economics.Insights;

namespace AI.Economics.Cohorts;

/// <summary>
/// Результат подгонки кривой удержания: параметры, качество и экстраполяция
/// хвоста с доверительным интервалом.
/// </summary>
public sealed partial record RetentionFitResult
{
    /// <summary>Подогнанное семейство кривых.</summary>
    public RetentionModel Model { get; init; }

    /// <summary>Оценки параметров модели.</summary>
    public IReadOnlyList<double> Parameters { get; init; } = [];

    /// <summary>Имена параметров в том же порядке.</summary>
    public IReadOnlyList<string> ParameterNames { get; init; } = [];

    /// <summary>Логарифм правдоподобия в точке оптимума.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Информационный критерий Акаике: <c>2k - 2 lnL</c>. Меньше — лучше.</summary>
    public double Aic { get; init; }

    /// <summary>Среднеквадратичное отклонение подогнанной кривой от наблюдённой.</summary>
    public double Rmse { get; init; }

    /// <summary>
    /// Кривая доживания <c>S(0..horizon)</c>: до конца наблюдений это подгонка,
    /// дальше — экстраполяция хвоста.
    /// </summary>
    public Vector Survival { get; init; } = new Vector(0);

    /// <summary>Нижняя граница доверительного интервала кривой.</summary>
    public Vector SurvivalLower { get; init; } = new Vector(0);

    /// <summary>Верхняя граница доверительного интервала кривой.</summary>
    public Vector SurvivalUpper { get; init; } = new Vector(0);

    /// <summary>Мгновенные доли удержания <c>r(t) = S(t) / S(t-1)</c>.</summary>
    public Vector RetentionRates { get; init; } = new Vector(0);

    /// <summary>Наблюдённая кривая доживания, по которой велась подгонка.</summary>
    public Vector Observed { get; init; } = new Vector(0);

    /// <summary>Число наблюдённых периодов (граница между подгонкой и экстраполяцией).</summary>
    public int ObservedPeriods { get; init; }

    /// <summary>Ожидаемое время жизни клиента на горизонте кривой, периодов.</summary>
    public double ExpectedLifetime { get; init; }

    /// <summary>Нижняя граница доверительного интервала ожидаемого времени жизни.</summary>
    public double ExpectedLifetimeLower { get; init; }

    /// <summary>Верхняя граница доверительного интервала ожидаемого времени жизни.</summary>
    public double ExpectedLifetimeUpper { get; init; }

    /// <summary>Уровень доверия интервалов, например 0,9.</summary>
    public double ConfidenceLevel { get; init; }
}
