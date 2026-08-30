using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Portfolio;

/// <summary>Нагрузка портфеля на фактор.</summary>
/// <param name="Factor">Название фактора.</param>
/// <param name="Loading">Коэффициент чувствительности.</param>
/// <param name="StandardError">Стандартная ошибка.</param>
/// <param name="TStatistic">Статистика Стьюдента.</param>
/// <param name="Contribution">Вклад фактора в доходность.</param>
public sealed record FactorLoading(
    string Factor, double Loading, double StandardError, double TStatistic, double Contribution)
{
    /// <summary>Значима ли нагрузка на уровне 5%.</summary>
    public bool IsSignificant => Math.Abs(TStatistic) > 1.96;
}

/// <summary>Результат оценки факторной модели.</summary>
public sealed record FactorModelResult : IInterpretable
{
    /// <summary>Название портфеля.</summary>
    public string Portfolio { get; init; } = string.Empty;

    /// <summary>Нагрузки на факторы.</summary>
    public IReadOnlyList<FactorLoading> Loadings { get; init; } = [];

    /// <summary>Годовая альфа.</summary>
    public double Alpha { get; init; }

    /// <summary>Стандартная ошибка альфы.</summary>
    public double AlphaStandardError { get; init; }

    /// <summary>Статистика значимости альфы.</summary>
    public double AlphaTStatistic { get; init; }

    /// <summary>Доля дисперсии, объяснённая факторами.</summary>
    public double RSquared { get; init; }

    /// <summary>Годовая доходность портфеля.</summary>
    public double TotalReturn { get; init; }

    /// <summary>Доходность, объяснённая факторами.</summary>
    public double ExplainedReturn { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Значима ли альфа.</summary>
    public bool HasAlpha => Math.Abs(AlphaTStatistic) > 1.96;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        FactorLoading? dominant = Loadings.OrderByDescending(l => Math.Abs(l.Contribution)).FirstOrDefault();
        var significant = Loadings.Where(l => l.IsSignificant).ToList();

        var builder = new InterpretationBuilder($"Факторная модель: {Portfolio}")
            .Summary($"Факторы объясняют {Fmt.Pct(RSquared, 1)} дисперсии доходности. " +
                     $"Годовая альфа {Fmt.Pct(Alpha, 2)} " +
                     $"(t = {Fmt.Num(AlphaTStatistic, 2)}) — " +
                     $"{(HasAlpha ? "значима" : "статистически неотличима от нуля")}. " +
                     $"Из {Loadings.Count} факторов значимы {significant.Count}. " +
                     $"Доходность {Fmt.Pct(TotalReturn, 2)}, из неё факторами объяснено " +
                     $"{Fmt.Pct(ExplainedReturn, 2)}.")
            .Metric("Альфа", Alpha, null,
                $"t = {Fmt.Num(AlphaTStatistic, 2)}, ст. ошибка {Fmt.Pct(AlphaStandardError, 2)}",
                HasAlpha && Alpha > 0 ? MetricQuality.Good
                    : HasAlpha ? MetricQuality.Critical : MetricQuality.Neutral, 4)
            .Metric("R²", RSquared, null, "доля доходности, объяснённая факторами",
                RSquared > 0.8 ? MetricQuality.Good : MetricQuality.Neutral, 3)
            .Metric("Объяснено факторами", ExplainedReturn, null,
                $"из общей доходности {Fmt.Pct(TotalReturn, 2)}", MetricQuality.Neutral, 4)
            .Metric("Значимых факторов", significant.Count, null,
                $"из {Loadings.Count}", MetricQuality.Neutral, 0);

        foreach (FactorLoading loading in Loadings)
        {
            builder.Metric(loading.Factor, loading.Loading, null,
                $"t = {Fmt.Num(loading.TStatistic, 2)}, вклад в доходность " +
                $"{Fmt.Pct(loading.Contribution, 2)}",
                loading.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 3);
        }

        return builder
            .Finding("Факторная модель отвечает на вопрос, за что именно инвестор получает " +
                     "доходность. Высокая доходность при значимых нагрузках на известные " +
                     "факторы — это не мастерство управляющего, а плата за принятые риски, " +
                     "которую можно получить дешевле через индексные инструменты.")
            .FindingIf(dominant is not null,
                $"Больше всего доходности объясняет фактор «{dominant?.Factor}»: " +
                $"вклад {Fmt.Pct(dominant?.Contribution ?? 0, 2)} при нагрузке " +
                $"{Fmt.Num(dominant?.Loading ?? 0, 2)}.")
            .FindingIf(HasAlpha && Alpha > 0,
                $"Альфа {Fmt.Pct(Alpha, 2)} значима: доходность не объясняется факторами " +
                "целиком. Это единственная часть результата, которую можно приписать " +
                "самому управляющему.")
            .FindingIf(!HasAlpha,
                "Альфа статистически неотличима от нуля. Портфель воспроизводится " +
                "комбинацией факторов, и его результат достижим пассивно.")
            .WarningIf(RSquared < 0.5,
                $"Факторы объясняют лишь {Fmt.Pct(RSquared, 1)} дисперсии. Либо в модели " +
                "не хватает факторов, либо портфель содержит специфический риск, " +
                "не связанный с рынком.")
            .WarningIf(Observations < 36,
                $"Всего {Observations} наблюдений. Оценка альфы на таком горизонте " +
                "имеет широкий доверительный интервал: отличить мастерство от удачи " +
                "статистически невозможно.")
            .Warning("Значимая альфа на исторических данных не переносится в будущее " +
                     "автоматически. При проверке множества портфелей часть из них " +
                     "покажет значимую альфу случайно, и поправку на множественность " +
                     "модель не делает.")
            .Recommendation("Сравнивайте альфу с комиссией фонда: положительная альфа " +
                            "до комиссий и отрицательная после — обычная ситуация.")
            .Recommendation("Добавляйте факторы по одному и следите за изменением альфы: " +
                            "её исчезновение при добавлении фактора и есть объяснение " +
                            "источника доходности.")
            .Build();
    }
}

/// <summary>Вклад сегмента в активную доходность по разложению Бринсона.</summary>
/// <param name="Segment">Название сегмента.</param>
/// <param name="Allocation">Эффект распределения капитала.</param>
/// <param name="Selection">Эффект выбора инструментов внутри сегмента.</param>
/// <param name="Interaction">Совместный эффект.</param>
/// <param name="Total">Суммарный вклад.</param>
public sealed record BrinsonSegment(
    string Segment, double Allocation, double Selection, double Interaction, double Total);

/// <summary>Результат атрибуции активной доходности.</summary>
public sealed record AttributionResult : IInterpretable
{
    /// <summary>Название портфеля.</summary>
    public string Portfolio { get; init; } = string.Empty;

    /// <summary>Вклады сегментов.</summary>
    public IReadOnlyList<BrinsonSegment> Segments { get; init; } = [];

    /// <summary>Доходность портфеля.</summary>
    public double PortfolioReturn { get; init; }

    /// <summary>Доходность эталона.</summary>
    public double BenchmarkReturn { get; init; }

    /// <summary>Активная доходность.</summary>
    public double ActiveReturn => PortfolioReturn - BenchmarkReturn;

    /// <summary>Суммарный эффект распределения.</summary>
    public double TotalAllocation => Segments.Sum(s => s.Allocation);

    /// <summary>Суммарный эффект выбора.</summary>
    public double TotalSelection => Segments.Sum(s => s.Selection);

    /// <summary>Суммарный совместный эффект.</summary>
    public double TotalInteraction => Segments.Sum(s => s.Interaction);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        BrinsonSegment? best = Segments.OrderByDescending(s => s.Total).FirstOrDefault();
        BrinsonSegment? worst = Segments.OrderBy(s => s.Total).FirstOrDefault();

        bool allocationDriven = Math.Abs(TotalAllocation) > Math.Abs(TotalSelection);

        var builder = new InterpretationBuilder($"Атрибуция доходности: {Portfolio}")
            .Summary($"Активная доходность {Fmt.Pct(ActiveReturn, 2)} " +
                     $"({Fmt.Pct(PortfolioReturn, 2)} против {Fmt.Pct(BenchmarkReturn, 2)} " +
                     $"у эталона). Распределение капитала дало {Fmt.Pct(TotalAllocation, 2)}, " +
                     $"выбор инструментов {Fmt.Pct(TotalSelection, 2)}, совместный эффект " +
                     $"{Fmt.Pct(TotalInteraction, 2)}. Основной источник — " +
                     $"{(allocationDriven ? "распределение" : "выбор инструментов")}.")
            .Metric("Активная доходность", ActiveReturn, null,
                "превышение над эталоном",
                ActiveReturn > 0 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Эффект распределения", TotalAllocation, null,
                "вклад решений о весах сегментов",
                TotalAllocation > 0 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Эффект выбора", TotalSelection, null,
                "вклад выбора инструментов внутри сегментов",
                TotalSelection > 0 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Совместный эффект", TotalInteraction, null,
                "перевес в сегментах, где выбор оказался удачным", MetricQuality.Neutral, 4);

        foreach (BrinsonSegment segment in Segments)
        {
            builder.Metric(segment.Segment, segment.Total, null,
                $"распределение {Fmt.Pct(segment.Allocation, 3)}, выбор " +
                $"{Fmt.Pct(segment.Selection, 3)}, совместный {Fmt.Pct(segment.Interaction, 3)}",
                segment.Total > 0 ? MetricQuality.Good : MetricQuality.Warning, 4);
        }

        return builder
            .Finding("Разложение Бринсона отделяет два решения управляющего: сколько дать " +
                     "каждому сегменту и что купить внутри него. Это разные навыки, " +
                     "и их обычно демонстрируют разные люди в команде.")
            .FindingIf(best is not null && worst is not null,
                $"Лучший вклад дал сегмент «{best?.Segment}» ({Fmt.Pct(best?.Total ?? 0, 2)}), " +
                $"худший — «{worst?.Segment}» ({Fmt.Pct(worst?.Total ?? 0, 2)}).")
            .FindingIf(allocationDriven,
                "Результат создан главным образом распределением капитала между " +
                "сегментами, а не выбором конкретных инструментов.")
            .FindingIf(!allocationDriven,
                "Результат создан выбором инструментов внутри сегментов. Распределение " +
                "капитала близко к эталонному и вклада почти не дало.")
            .WarningIf(Math.Abs(TotalInteraction) > Math.Abs(ActiveReturn) * 0.4,
                "Совместный эффект велик относительно активной доходности. Он не " +
                "приписывается ни одному решению по отдельности и затрудняет " +
                "интерпретацию — часто его объединяют с эффектом выбора.")
            .Warning("Разложение относится к одному периоду. При суммировании по периодам " +
                     "составляющие не складываются линейно из-за капитализации, " +
                     "и для многопериодной атрибуции нужны сглаживающие поправки.")
            .Recommendation("Смотрите на устойчивость источников во времени: разовый " +
                            "удачный выбор в одном сегменте и систематическое " +
                            "преимущество в распределении — разные вещи.")
            .Build();
    }
}

/// <summary>
/// Факторные модели доходности и атрибуция активного результата.
/// </summary>
/// <remarks>
/// <para>
/// Факторная модель раскладывает доходность портфеля на премии за известные
/// риски и остаток:
/// </para>
/// <code>
/// R_p - Rf = alpha + b_mkt * MKT + b_smb * SMB + b_hml * HML + b_mom * MOM + e
/// </code>
/// <para>
/// Свободный член и есть альфа — часть доходности, не объяснённая факторами.
/// Практический смысл модели в том, что нагрузки на факторы воспроизводимы
/// пассивно и стоят дёшево; платить активную комиссию имеет смысл только
/// за альфу.
/// </para>
/// <para>
/// Когда факторы не заданы заранее, их можно извлечь из самих доходностей
/// методом главных компонент: собственные векторы ковариационной матрицы дают
/// статистические факторы в духе теории арбитражного ценообразования.
/// </para>
/// <para>
/// Разложение Бринсона решает другую задачу — приписывает активную доходность
/// конкретным решениям:
/// </para>
/// <code>
/// Allocation  = (w_p - w_b) * (r_b - R_b)
/// Selection   = w_b * (r_p - r_b)
/// Interaction = (w_p - w_b) * (r_p - r_b)
/// </code>
/// </remarks>
public static class FactorModels
{
    /// <summary>Оценивает факторную модель доходности портфеля.</summary>
    /// <param name="excessReturns">Избыточная доходность портфеля над безрисковой ставкой.</param>
    /// <param name="factors">Доходности факторов: строка — период, столбец — фактор.</param>
    /// <param name="factorNames">Названия факторов.</param>
    /// <param name="periodsPerYear">Число периодов в году для приведения альфы.</param>
    /// <param name="portfolio">Название портфеля.</param>
    /// <returns>Нагрузки, альфа и вклады факторов в доходность.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static FactorModelResult Fit(
        Vector excessReturns, Matrix factors, IReadOnlyList<string>? factorNames = null,
        int periodsPerYear = 12, string portfolio = "портфель")
    {
        ArgumentNullException.ThrowIfNull(excessReturns);
        ArgumentNullException.ThrowIfNull(factors);

        if (excessReturns.Count != factors.Height)
            throw new ArgumentException("Число наблюдений должно совпадать.", nameof(factors));

        RegressionResult fit = LinearRegression.Fit(
            factors, excessReturns, factorNames,
            new RegressionOptions { Variance = RobustVariance.NeweyWest });

        var loadings = new List<FactorLoading>(factors.Width);

        for (int j = 0; j < factors.Width; j++)
        {
            Coefficient coefficient = fit.Coefficients[j + 1];

            double factorMean = 0;
            for (int t = 0; t < factors.Height; t++) factorMean += factors[t, j];
            factorMean /= factors.Height;

            loadings.Add(new FactorLoading(
                coefficient.Name, coefficient.Estimate, coefficient.StandardError,
                coefficient.TStatistic, coefficient.Estimate * factorMean * periodsPerYear));
        }

        Coefficient intercept = fit.Coefficients[0];

        return new FactorModelResult
        {
            Portfolio = portfolio,
            Loadings = loadings,
            Alpha = intercept.Estimate * periodsPerYear,
            AlphaStandardError = intercept.StandardError * periodsPerYear,
            AlphaTStatistic = intercept.TStatistic,
            RSquared = fit.RSquared,
            TotalReturn = excessReturns.Average() * periodsPerYear,
            ExplainedReturn = loadings.Sum(l => l.Contribution),
            Observations = excessReturns.Count,
        };
    }

    /// <summary>Извлекает статистические факторы методом главных компонент.</summary>
    /// <param name="returns">Доходности активов: строка — период, столбец — актив.</param>
    /// <param name="factorCount">Число извлекаемых факторов.</param>
    /// <returns>Доходности факторов, доли объяснённой дисперсии и нагрузки активов.</returns>
    /// <exception cref="ArgumentNullException">Доходности не заданы.</exception>
    /// <exception cref="ArgumentException">Активов или наблюдений недостаточно.</exception>
    public static (Matrix Factors, Vector ExplainedVariance, Matrix Loadings) PrincipalComponents(
        Matrix returns, int factorCount = 3)
    {
        ArgumentNullException.ThrowIfNull(returns);

        int t = returns.Height, n = returns.Width;
        if (n < 2) throw new ArgumentException("Нужно минимум два актива.", nameof(returns));
        if (t < n) throw new ArgumentException("Наблюдений должно быть больше числа активов.", nameof(returns));

        int k = Math.Clamp(factorCount, 1, n);

        var data = new double[t, n];
        for (int i = 0; i < t; i++)
            for (int j = 0; j < n; j++) data[i, j] = returns[i, j];

        double[,] covariance = LinearAlgebra.Covariance(data);
        (double[] values, double[,] vectors) = LinearAlgebra.SymmetricEigen(covariance);

        double total = values.Sum(v => Math.Max(v, 0));

        var factors = new Matrix(t, k);
        var loadings = new Matrix(n, k);
        var explained = new Vector(k);

        var means = new double[n];
        for (int j = 0; j < n; j++)
        {
            double sum = 0;
            for (int i = 0; i < t; i++) sum += data[i, j];
            means[j] = sum / t;
        }

        for (int f = 0; f < k; f++)
        {
            explained[f] = total > 0 ? Math.Max(values[f], 0) / total : 0;

            for (int j = 0; j < n; j++) loadings[j, f] = vectors[j, f];

            for (int i = 0; i < t; i++)
            {
                double score = 0;
                for (int j = 0; j < n; j++) score += (data[i, j] - means[j]) * vectors[j, f];
                factors[i, f] = score;
            }
        }

        return (factors, explained, loadings);
    }

    /// <summary>Раскладывает активную доходность по методу Бринсона.</summary>
    /// <param name="segments">Названия сегментов.</param>
    /// <param name="portfolioWeights">Веса сегментов в портфеле.</param>
    /// <param name="benchmarkWeights">Веса сегментов в эталоне.</param>
    /// <param name="portfolioReturns">Доходности сегментов в портфеле.</param>
    /// <param name="benchmarkReturns">Доходности сегментов в эталоне.</param>
    /// <param name="portfolio">Название портфеля.</param>
    /// <returns>Вклады распределения, выбора и совместного эффекта.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static AttributionResult BrinsonAttribution(
        IReadOnlyList<string> segments, Vector portfolioWeights, Vector benchmarkWeights,
        Vector portfolioReturns, Vector benchmarkReturns, string portfolio = "портфель")
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(portfolioWeights);
        ArgumentNullException.ThrowIfNull(benchmarkWeights);
        ArgumentNullException.ThrowIfNull(portfolioReturns);
        ArgumentNullException.ThrowIfNull(benchmarkReturns);

        int n = segments.Count;
        if (portfolioWeights.Count != n || benchmarkWeights.Count != n
            || portfolioReturns.Count != n || benchmarkReturns.Count != n)
            throw new ArgumentException("Все ряды должны совпадать по числу сегментов.", nameof(segments));

        double benchmarkTotal = 0;
        for (int i = 0; i < n; i++) benchmarkTotal += benchmarkWeights[i] * benchmarkReturns[i];

        double portfolioTotal = 0;
        for (int i = 0; i < n; i++) portfolioTotal += portfolioWeights[i] * portfolioReturns[i];

        var results = new List<BrinsonSegment>(n);

        for (int i = 0; i < n; i++)
        {
            double weightGap = portfolioWeights[i] - benchmarkWeights[i];
            double returnGap = portfolioReturns[i] - benchmarkReturns[i];

            double allocation = weightGap * (benchmarkReturns[i] - benchmarkTotal);
            double selection = benchmarkWeights[i] * returnGap;
            double interaction = weightGap * returnGap;

            results.Add(new BrinsonSegment(
                segments[i], allocation, selection, interaction,
                allocation + selection + interaction));
        }

        return new AttributionResult
        {
            Portfolio = portfolio,
            Segments = results,
            PortfolioReturn = portfolioTotal,
            BenchmarkReturn = benchmarkTotal,
        };
    }
}
