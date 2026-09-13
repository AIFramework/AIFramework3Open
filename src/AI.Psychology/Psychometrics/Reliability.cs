using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Psychology.Internal;
using AI.Statistics;

namespace AI.Psychology.Psychometrics;

/// <summary>Надёжность шкалы и анализ её пунктов</summary>
public sealed class ReliabilityResult : IInterpretable
{
    internal ReliabilityResult(
        int respondents, int items, double alpha, double standardizedAlpha, double omega, double splitHalf,
        double meanInterItemCorrelation, double standardErrorOfMeasurement,
        double[] alphaIfDeleted, double[] itemTotal, double[] loadings)
    {
        Respondents = respondents;
        Items = items;
        CronbachAlpha = alpha;
        StandardizedAlpha = standardizedAlpha;
        McDonaldOmega = omega;
        SplitHalf = splitHalf;
        MeanInterItemCorrelation = meanInterItemCorrelation;
        StandardErrorOfMeasurement = standardErrorOfMeasurement;
        AlphaIfItemDeleted = alphaIfDeleted;
        CorrectedItemTotalCorrelations = itemTotal;
        Loadings = loadings;
    }

    /// <summary>Число респондентов</summary>
    public int Respondents { get; }

    /// <summary>Число пунктов</summary>
    public int Items { get; }

    /// <summary>α Кронбаха по сырым баллам</summary>
    public double CronbachAlpha { get; }

    /// <summary>Стандартизованная α: k·r̄/(1 + (k − 1)·r̄)</summary>
    public double StandardizedAlpha { get; }

    /// <summary>ω Макдональда по однофакторному решению для стандартизованных пунктов; NaN при двух пунктах</summary>
    public double McDonaldOmega { get; }

    /// <summary>Надёжность расщеплением на чётные и нечётные пункты с поправкой Спирмена — Брауна</summary>
    public double SplitHalf { get; }

    /// <summary>Средняя корреляция между пунктами</summary>
    public double MeanInterItemCorrelation { get; }

    /// <summary>Стандартная ошибка измерения суммарного балла: σ·√(1 − α)</summary>
    public double StandardErrorOfMeasurement { get; }

    /// <summary>α при исключении каждого пункта</summary>
    public IReadOnlyList<double> AlphaIfItemDeleted { get; }

    /// <summary>Корреляция пункта с суммой остальных пунктов</summary>
    public IReadOnlyList<double> CorrectedItemTotalCorrelations { get; }

    /// <summary>Нагрузки пунктов на общий фактор, по которым считалась ω; пусто при двух пунктах</summary>
    public IReadOnlyList<double> Loadings { get; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int[] harmful = Enumerable.Range(0, Items).Where(i => AlphaIfItemDeleted[i] > CronbachAlpha + 0.01).ToArray();
        int[] reversed = Enumerable.Range(0, Items).Where(i => CorrectedItemTotalCorrelations[i] < 0).ToArray();

        return new InterpretationBuilder("Надёжность шкалы")
            .Summary($"Пунктов {Items}, респондентов {Respondents}: α Кронбаха = {Fmt.Num(CronbachAlpha, 3)}"
                + (double.IsNaN(McDonaldOmega) ? "." : $", ω Макдональда = {Fmt.Num(McDonaldOmega, 3)}.")
                + $" Стандартная ошибка суммарного балла {Fmt.Num(StandardErrorOfMeasurement, 2)}.")
            .Metric("α Кронбаха", CronbachAlpha, null, "согласованность по сырым баллам",
                CronbachAlpha >= 0.7 ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Стандартизованная α", StandardizedAlpha, null, "по средней корреляции пунктов", MetricQuality.Unknown, 3)
            .Metric("ω Макдональда", McDonaldOmega, null, "доля дисперсии суммы, приходящаяся на общий фактор", MetricQuality.Unknown, 3)
            .Metric("Расщепление", SplitHalf, null, "чётные против нечётных с поправкой Спирмена — Брауна", MetricQuality.Unknown, 3)
            .Metric("Средняя корреляция пунктов", MeanInterItemCorrelation, null, null, MetricQuality.Unknown, 3)
            .Metric("Ошибка измерения", StandardErrorOfMeasurement, "балла", "σ·√(1 − α)", MetricQuality.Unknown, 2)
            .FindingIf(harmful.Length > 0,
                $"Исключение пунктов {string.Join(", ", harmful.Select(i => i + 1))} повышает α: они измеряют что-то другое или плохо сформулированы.")
            .FindingIf(reversed.Length > 0,
                $"Пункты {string.Join(", ", reversed.Select(i => i + 1))} отрицательно коррелируют с остальными: вероятно, это обратные пункты, "
                + "и их нужно перекодировать до расчёта. Пока они не перекодированы, α и ω занижены.")
            .WarningIf(CronbachAlpha >= 0.95,
                "α выше 0,95 — пункты почти дублируют друг друга: шкалу можно сократить без потери надёжности.")
            .Warning("Высокая α не доказывает, что шкала измеряет одно свойство: её дают и две сильно коррелирующие черты. "
                + "Одномерность проверяют факторным анализом. α — нижняя граница надёжности, точная лишь при равных нагрузках "
                + "пунктов; при неравных ω точнее.")
            .Build();
    }
}

/// <summary>
/// Надёжность психологических шкал: α Кронбаха, ω Макдональда, расщепление, формула Спирмена — Брауна.
/// </summary>
/// <remarks>
/// <para>
/// Надёжность — доля дисперсии наблюдаемого балла, приходящаяся на истинный балл. α Кронбаха оценивает её
/// по согласованности пунктов: α = k/(k − 1)·(1 − Σσᵢ²/σ²ₓ). Она точна, только если все пункты одинаково
/// связаны с измеряемым свойством (тау-эквивалентность); иначе α — нижняя граница. ω Макдональда берёт
/// нагрузки из однофакторного решения, (Σλ)²/((Σλ)² + Σψ), и при неравных нагрузках точнее; факторы
/// считает <see cref="FactorAnalysis"/> этой же сборки.
/// </para>
/// <para>
/// Формула Спирмена — Брауна переводит надёжность при изменении длины теста: удлинение в n раз даёт
/// n·ρ/(1 + (n − 1)·ρ). Отсюда поправка расщепления и оценка того, сколько пунктов нужно для заданной
/// надёжности. Пропуски в ответах не поддерживаются: их нужно исключить или восстановить заранее.
/// </para>
/// </remarks>
public static class Reliability
{
    /// <summary>Полный анализ надёжности</summary>
    /// <param name="scores">Баллы: респонденты по строкам, пункты по столбцам</param>
    public static ReliabilityResult Analyze(double[,] scores)
    {
        (int n, int k) = Require(scores);

        double[] itemVariance = Enumerable.Range(0, k).Select(j => Variance(Column(scores, j))).ToArray();
        double[] totals = Enumerable.Range(0, n).Select(i => Enumerable.Range(0, k).Sum(j => scores[i, j])).ToArray();
        double totalVariance = Variance(totals);
        double alpha = Alpha(k, itemVariance.Sum(), totalVariance);

        double[,] correlation = FactorAnalysis.CorrelationMatrix(scores);
        double sumR = 0;

        for (int a = 0; a < k; a++)
            for (int b = a + 1; b < k; b++)
                sumR += correlation[a, b];

        double meanR = sumR / (k * (k - 1) / 2.0);
        double standardized = k * meanR / (1 + ((k - 1) * meanR));

        var alphaIfDeleted = new double[k];
        var itemTotal = new double[k];

        for (int j = 0; j < k; j++)
        {
            double[] rest = Enumerable.Range(0, n).Select(i => totals[i] - scores[i, j]).ToArray();
            alphaIfDeleted[j] = k > 2 ? Alpha(k - 1, itemVariance.Sum() - itemVariance[j], Variance(rest)) : double.NaN;
            itemTotal[j] = Statistic.CorrelationCoefficient(new Vector(Column(scores, j)), new Vector(rest));
        }

        double[] odd = Enumerable.Range(0, n).Select(i => Enumerable.Range(0, k).Where(j => j % 2 == 0).Sum(j => scores[i, j])).ToArray();
        double[] even = Enumerable.Range(0, n).Select(i => Enumerable.Range(0, k).Where(j => j % 2 == 1).Sum(j => scores[i, j])).ToArray();
        double halves = Statistic.CorrelationCoefficient(new Vector(odd), new Vector(even));
        double splitHalf = SpearmanBrown(Math.Max(halves, -0.999), 2);

        double omega = double.NaN;
        double[] loadings = [];

        if (k >= 3)
        {
            FactorAnalysisResult single = FactorAnalysis.PrincipalAxis(correlation, 1, FactorRotation.None);
            loadings = Enumerable.Range(0, k).Select(j => single.Loading(j, 0)).ToArray();
            double sum = loadings.Sum();
            double unique = loadings.Sum(l => 1 - (l * l));
            omega = sum * sum / ((sum * sum) + unique);
        }

        return new ReliabilityResult(n, k, alpha, standardized, omega, splitHalf, meanR,
            Math.Sqrt(totalVariance) * Math.Sqrt(Math.Max(0, 1 - alpha)), alphaIfDeleted, itemTotal, loadings);
    }

    /// <summary>α Кронбаха</summary>
    /// <param name="scores">Баллы: респонденты по строкам, пункты по столбцам</param>
    public static double CronbachAlpha(double[,] scores)
    {
        (int n, int k) = Require(scores);

        double items = Enumerable.Range(0, k).Sum(j => Variance(Column(scores, j)));
        double total = Variance(Enumerable.Range(0, n).Select(i => Enumerable.Range(0, k).Sum(j => scores[i, j])).ToArray());

        return Alpha(k, items, total);
    }

    /// <summary>Надёжность теста, удлинённого в n раз: n·ρ/(1 + (n − 1)·ρ)</summary>
    /// <param name="reliability">Исходная надёжность ρ</param>
    /// <param name="lengthFactor">Во сколько раз меняется длина</param>
    public static double SpearmanBrown(double reliability, double lengthFactor)
    {
        if (!(reliability > -1 && reliability < 1))
            throw new ArgumentOutOfRangeException(nameof(reliability), "Надёжность лежит в (−1; 1)");

        Numerics.RequirePositive(lengthFactor, nameof(lengthFactor));

        return lengthFactor * reliability / (1 + ((lengthFactor - 1) * reliability));
    }

    /// <summary>Во сколько раз удлинить тест, чтобы надёжность достигла цели: ρ*(1 − ρ)/(ρ(1 − ρ*))</summary>
    /// <param name="reliability">Текущая надёжность</param>
    /// <param name="target">Желаемая надёжность</param>
    public static double RequiredLengthFactor(double reliability, double target)
    {
        if (!(reliability > 0 && reliability < 1) || !(target > 0 && target < 1))
            throw new ArgumentOutOfRangeException(nameof(target), "Надёжности лежат в (0; 1)");

        return target * (1 - reliability) / (reliability * (1 - target));
    }

    private static double Alpha(int items, double sumOfItemVariances, double totalVariance)
        => totalVariance <= 0 ? double.NaN : items / (items - 1.0) * (1 - (sumOfItemVariances / totalVariance));

    private static double[] Column(double[,] scores, int j)
        => Enumerable.Range(0, scores.GetLength(0)).Select(i => scores[i, j]).ToArray();

    private static double Variance(IReadOnlyList<double> values)
    {
        double mean = values.Average();

        return values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
    }

    private static (int Respondents, int Items) Require(double[,] scores)
    {
        ArgumentNullException.ThrowIfNull(scores);

        int n = scores.GetLength(0), k = scores.GetLength(1);

        if (n < 3 || k < 2)
            throw new ArgumentException("Нужно хотя бы три респондента и два пункта", nameof(scores));

        foreach (double value in scores)
            Numerics.RequireFinite(value, nameof(scores));

        return (n, k);
    }
}
