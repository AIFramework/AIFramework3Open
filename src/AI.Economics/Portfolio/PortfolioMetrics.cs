using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Portfolio;

/// <summary>Набор метрик доходности и риска портфеля.</summary>
public sealed record PerformanceMetrics : IInterpretable
{
    /// <summary>Название портфеля.</summary>
    public string Portfolio { get; init; } = "портфель";

    /// <summary>Годовая доходность.</summary>
    public double AnnualReturn { get; init; }

    /// <summary>Годовая волатильность.</summary>
    public double Volatility { get; init; }

    /// <summary>Волатильность отрицательных отклонений.</summary>
    public double DownsideDeviation { get; init; }

    /// <summary>Коэффициент Шарпа.</summary>
    public double Sharpe { get; init; }

    /// <summary>Коэффициент Сортино.</summary>
    public double Sortino { get; init; }

    /// <summary>Коэффициент Кальмара.</summary>
    public double Calmar { get; init; }

    /// <summary>Коэффициент Омега.</summary>
    public double Omega { get; init; }

    /// <summary>Максимальная просадка.</summary>
    public double MaxDrawdown { get; init; }

    /// <summary>Длительность максимальной просадки в периодах.</summary>
    public int MaxDrawdownLength { get; init; }

    /// <summary>Периодов до восстановления после максимальной просадки.</summary>
    public int RecoveryPeriods { get; init; }

    /// <summary>Ошибка следования за эталоном.</summary>
    public double TrackingError { get; init; }

    /// <summary>Информационный коэффициент.</summary>
    public double InformationRatio { get; init; }

    /// <summary>Бета относительно эталона.</summary>
    public double Beta { get; init; }

    /// <summary>Альфа относительно эталона.</summary>
    public double Alpha { get; init; }

    /// <summary>Доля прибыльных периодов.</summary>
    public double HitRate { get; init; }

    /// <summary>Ряд просадок.</summary>
    public Vector Drawdowns { get; init; } = new(0);

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Есть ли эталон для сравнения.</summary>
    public bool HasBenchmark { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool skewedDown = Sortino < Sharpe * 0.8;
        bool deepDrawdown = MaxDrawdown > 0.3;

        var builder = new InterpretationBuilder($"Метрики портфеля: {Portfolio}")
            .Summary($"Годовая доходность {Fmt.Pct(AnnualReturn, 2)} при волатильности " +
                     $"{Fmt.Pct(Volatility, 2)}: коэффициент Шарпа {Fmt.Num(Sharpe, 2)}, " +
                     $"Сортино {Fmt.Num(Sortino, 2)}, Кальмара {Fmt.Num(Calmar, 2)}. " +
                     $"Максимальная просадка {Fmt.Pct(MaxDrawdown, 1)} длилась " +
                     $"{MaxDrawdownLength} периодов" +
                     (RecoveryPeriods > 0 ? $", восстановление заняло {RecoveryPeriods}." : " и не восстановлена.") +
                     (HasBenchmark
                         ? $" Альфа {Fmt.Pct(Alpha, 2)}, бета {Fmt.Num(Beta, 2)}, " +
                           $"информационный коэффициент {Fmt.Num(InformationRatio, 2)}."
                         : ""))
            .Metric("Доходность", AnnualReturn, null, "в годовом выражении",
                AnnualReturn > 0 ? MetricQuality.Good : MetricQuality.Critical, 4)
            .Metric("Волатильность", Volatility, null, "годовое стандартное отклонение",
                MetricQuality.Neutral, 4)
            .Metric("Шарп", Sharpe, null, "избыточная доходность на единицу общего риска",
                Sharpe > 1 ? MetricQuality.Good : Sharpe > 0.5 ? MetricQuality.Neutral : MetricQuality.Warning, 3)
            .Metric("Сортино", Sortino, null, "то же, но риск считается только по падениям",
                Sortino > 1.5 ? MetricQuality.Good : MetricQuality.Neutral, 3)
            .Metric("Кальмар", Calmar, null, "доходность к максимальной просадке",
                Calmar > 0.5 ? MetricQuality.Good : MetricQuality.Neutral, 3)
            .Metric("Омега", Omega, null, "отношение выигрышей к потерям относительно порога",
                Omega > 1.5 ? MetricQuality.Good : MetricQuality.Neutral, 3)
            .Metric("Максимальная просадка", MaxDrawdown, null,
                $"{MaxDrawdownLength} периодов падения",
                deepDrawdown ? MetricQuality.Critical
                    : MaxDrawdown > 0.15 ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Доля прибыльных периодов", HitRate, null,
                "как часто портфель растёт", MetricQuality.Neutral, 3);

        if (HasBenchmark)
        {
            builder
                .Metric("Альфа", Alpha, null, "доходность сверх объяснённой эталоном",
                    Alpha > 0 ? MetricQuality.Good : MetricQuality.Warning, 4)
                .Metric("Бета", Beta, null, "чувствительность к движению эталона",
                    MetricQuality.Neutral, 3)
                .Metric("Ошибка следования", TrackingError, null,
                    "разброс отклонений от эталона", MetricQuality.Neutral, 4)
                .Metric("Информационный коэффициент", InformationRatio, null,
                    "избыточная доходность на единицу отклонения от эталона",
                    InformationRatio > 0.5 ? MetricQuality.Good : MetricQuality.Neutral, 3);
        }

        return builder
            .Finding("Шарп делит избыточную доходность на общую волатильность и потому " +
                     "наказывает за рост так же, как за падение. Сортино и Кальмар " +
                     "исправляют это: первый считает риск только по падениям, " +
                     "второй — по глубине просадки.")
            .FindingIf(skewedDown,
                $"Сортино {Fmt.Num(Sortino, 2)} заметно ниже Шарпа {Fmt.Num(Sharpe, 2)}: " +
                "волатильность создаётся преимущественно падениями. Для инвестора " +
                "это хуже, чем показывает Шарп.")
            .FindingIf(!skewedDown && Sortino > Sharpe * 1.3,
                "Сортино существенно выше Шарпа: колебания идут в основном вверх. " +
                "Шарп в этом случае недооценивает качество стратегии.")
            .FindingIf(HasBenchmark && Math.Abs(Beta - 1) < 0.15 && Math.Abs(Alpha) < 0.01,
                "Портфель практически повторяет эталон: бета близка к единице, " +
                "альфа неотличима от нуля. Активное управление здесь не окупает комиссий.")
            .WarningIf(deepDrawdown,
                $"Максимальная просадка {Fmt.Pct(MaxDrawdown, 1)} требует роста на " +
                $"{Fmt.Pct(MaxDrawdown / (1 - MaxDrawdown), 1)} для возврата к прежнему " +
                "уровню. Асимметрия восстановления — главная причина, по которой " +
                "просадка важнее волатильности.")
            .WarningIf(Observations < 36,
                $"Всего {Observations} наблюдений. Коэффициент Шарпа на таком горизонте " +
                "имеет широкий доверительный интервал, и различия между стратегиями " +
                "статистически неразличимы.")
            .WarningIf(RecoveryPeriods == 0 && MaxDrawdown > 0.05,
                "Портфель не восстановился после максимальной просадки до конца выборки. " +
                "Кальмар в этом случае оценивает риск оптимистично.")
            .Warning("Все метрики посчитаны на исторических данных и отражают один " +
                     "реализовавшийся путь. Стратегия, отобранная по максимуму Шарпа " +
                     "на истории, почти всегда показывает худший результат вне выборки.")
            .Recommendation("Сравнивайте стратегии по нескольким метрикам сразу: " +
                            "ранжирование по Шарпу и по Кальмару часто даёт разный порядок, " +
                            "и это различие содержательно.")
            .Recommendation("Смотрите на длительность просадки, а не только на глубину: " +
                            "три года под водой инвестор переносит хуже, чем короткое " +
                            "глубокое падение.")
            .Build();
    }
}

/// <summary>
/// Метрики доходности и риска портфеля.
/// </summary>
/// <remarks>
/// <para>
/// Коэффициент Шарпа делит избыточную доходность на волатильность, но
/// волатильность наказывает и за рост. Альтернативы различаются тем, что
/// считают риском:
/// </para>
/// <code>
/// Sharpe  = (R - Rf) / sigma
/// Sortino = (R - Rf) / sigma_down
/// Calmar  = R / MaxDrawdown
/// Omega   = sum(max(r - t, 0)) / sum(max(t - r, 0))
/// </code>
/// <para>
/// Просадка заслуживает отдельного внимания из-за асимметрии восстановления:
/// падение на 50% требует роста на 100% для возврата. Поэтому глубина и
/// длительность просадки для инвестора важнее волатильности.
/// </para>
/// <para>
/// При наличии эталона добавляются метрики активного управления: бета и альфа
/// из регрессии на эталон, ошибка следования как разброс отклонений и
/// информационный коэффициент — избыточная доходность на единицу этого разброса.
/// </para>
/// </remarks>
public static class PortfolioMetrics
{
    /// <summary>Считает метрики портфеля.</summary>
    /// <param name="returns">Доходности портфеля по периодам.</param>
    /// <param name="benchmark">Доходности эталона; при <c>null</c> активные метрики не считаются.</param>
    /// <param name="riskFreeRate">Безрисковая ставка за период.</param>
    /// <param name="periodsPerYear">Число периодов в году.</param>
    /// <param name="omegaThreshold">Порог для коэффициента Омега.</param>
    /// <param name="portfolio">Название портфеля.</param>
    /// <returns>Полный набор метрик с интерпретацией.</returns>
    /// <exception cref="ArgumentNullException">Доходности не заданы.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно.</exception>
    public static PerformanceMetrics Compute(
        Vector returns, Vector? benchmark = null, double riskFreeRate = 0,
        int periodsPerYear = 12, double omegaThreshold = 0, string portfolio = "портфель")
    {
        ArgumentNullException.ThrowIfNull(returns);
        if (returns.Count < 6) throw new ArgumentException("Нужно минимум шесть наблюдений.", nameof(returns));

        int n = returns.Count;
        double mean = returns.Average();
        double variance = returns.Sum(r => (r - mean) * (r - mean)) / (n - 1);
        double sigma = Math.Sqrt(Math.Max(variance, 0));

        double downside = Math.Sqrt(
            returns.Where(r => r < riskFreeRate).Sum(r => (r - riskFreeRate) * (r - riskFreeRate)) / n);

        double annualReturn = (Math.Pow(returns.Aggregate(1.0, (acc, r) => acc * (1 + r)), (double)periodsPerYear / n)) - 1;
        double annualVolatility = sigma * Math.Sqrt(periodsPerYear);
        double annualDownside = downside * Math.Sqrt(periodsPerYear);
        double annualRiskFree = (Math.Pow(1 + riskFreeRate, periodsPerYear)) - 1;

        (Vector drawdowns, double maxDrawdown, int length, int recovery) = DrawdownProfile(returns);

        double gains = returns.Sum(r => Math.Max(r - omegaThreshold, 0));
        double losses = returns.Sum(r => Math.Max(omegaThreshold - r, 0));

        double trackingError = 0, informationRatio = 0, beta = 0, alpha = 0;
        bool hasBenchmark = benchmark is not null && benchmark.Count == n;

        if (hasBenchmark)
        {
            var active = new double[n];
            for (int i = 0; i < n; i++) active[i] = returns[i] - benchmark![i];

            double activeMean = active.Average();
            double activeVariance = active.Sum(a => (a - activeMean) * (a - activeMean)) / Math.Max(1, n - 1);

            trackingError = Math.Sqrt(Math.Max(activeVariance, 0)) * Math.Sqrt(periodsPerYear);
            informationRatio = trackingError > 0 ? activeMean * periodsPerYear / trackingError : 0;

            Vector reference = benchmark!;
            double benchmarkMean = reference.Average();
            double covariance = 0, benchmarkVariance = 0;

            for (int i = 0; i < n; i++)
            {
                covariance += (returns[i] - mean) * (reference[i] - benchmarkMean);
                benchmarkVariance += (reference[i] - benchmarkMean) * (reference[i] - benchmarkMean);
            }

            beta = benchmarkVariance > 0 ? covariance / benchmarkVariance : 0;
            alpha = (mean - riskFreeRate - (beta * (benchmarkMean - riskFreeRate))) * periodsPerYear;
        }

        return new PerformanceMetrics
        {
            Portfolio = portfolio,
            AnnualReturn = annualReturn,
            Volatility = annualVolatility,
            DownsideDeviation = annualDownside,
            Sharpe = annualVolatility > 0 ? (annualReturn - annualRiskFree) / annualVolatility : 0,
            Sortino = annualDownside > 0 ? (annualReturn - annualRiskFree) / annualDownside : 0,
            Calmar = maxDrawdown > 0 ? annualReturn / maxDrawdown : 0,
            Omega = losses > 0 ? gains / losses : gains > 0 ? 99 : 1,
            MaxDrawdown = maxDrawdown,
            MaxDrawdownLength = length,
            RecoveryPeriods = recovery,
            TrackingError = trackingError,
            InformationRatio = informationRatio,
            Beta = beta,
            Alpha = alpha,
            HitRate = (double)returns.Count(r => r > 0) / n,
            Drawdowns = drawdowns,
            Observations = n,
            HasBenchmark = hasBenchmark,
        };
    }

    /// <summary>Ряд просадок и характеристики максимальной.</summary>
    /// <param name="returns">Доходности по периодам.</param>
    /// <returns>Ряд просадок, максимальная просадка, её длительность и срок восстановления.</returns>
    /// <exception cref="ArgumentNullException">Доходности не заданы.</exception>
    public static (Vector Drawdowns, double MaxDrawdown, int Length, int Recovery) DrawdownProfile(Vector returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        int n = returns.Count;
        var drawdowns = new Vector(n);

        double equity = 1, peak = 1;
        double maxDrawdown = 0;
        int peakIndex = 0, troughIndex = 0, maxPeakIndex = 0, maxTroughIndex = 0;

        for (int i = 0; i < n; i++)
        {
            equity *= 1 + returns[i];

            if (equity > peak) { peak = equity; peakIndex = i; }

            double drawdown = peak > 0 ? (peak - equity) / peak : 0;
            drawdowns[i] = drawdown;

            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
                maxPeakIndex = peakIndex;
                maxTroughIndex = i;
            }

            troughIndex = i;
        }

        int recovery = 0;
        double troughEquity = 1;

        for (int i = 0; i <= maxTroughIndex && i < n; i++) troughEquity *= 1 + returns[i];

        double target = troughEquity / Math.Max(1 - maxDrawdown, 1e-12);
        double running = troughEquity;

        for (int i = maxTroughIndex + 1; i < n; i++)
        {
            running *= 1 + returns[i];
            recovery++;

            if (running >= target) break;
            if (i == n - 1) recovery = 0;
        }

        return (drawdowns, maxDrawdown, Math.Max(0, maxTroughIndex - maxPeakIndex), recovery);
    }

    /// <summary>Доходности портфеля по весам и доходностям активов.</summary>
    /// <param name="weights">Веса активов.</param>
    /// <param name="assetReturns">Доходности: строка — период, столбец — актив.</param>
    /// <returns>Ряд доходностей портфеля.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static Vector PortfolioReturns(Vector weights, Matrix assetReturns)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(assetReturns);

        if (weights.Count != assetReturns.Width)
            throw new ArgumentException("Число весов должно совпадать с числом активов.", nameof(weights));

        var returns = new Vector(assetReturns.Height);

        for (int t = 0; t < assetReturns.Height; t++)
        {
            double value = 0;
            for (int j = 0; j < assetReturns.Width; j++) value += weights[j] * assetReturns[t, j];
            returns[t] = value;
        }

        return returns;
    }
}
