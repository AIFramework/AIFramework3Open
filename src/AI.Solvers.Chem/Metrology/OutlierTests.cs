using AI.Statistics;

namespace AI.Solvers.Chem.Metrology;

/// <summary>
/// Результат проверки на грубый промах
/// </summary>
/// <param name="IsOutlier">Признан ли результат промахом</param>
/// <param name="Index">Индекс подозрительного значения</param>
/// <param name="Value">Само значение</param>
/// <param name="Statistic">Значение критерия</param>
/// <param name="CriticalValue">Критическое значение при заданном уровне значимости</param>
/// <param name="Alpha">Уровень значимости</param>
/// <param name="Method">Название критерия</param>
public readonly record struct OutlierResult(
    bool IsOutlier,
    int Index,
    double Value,
    double Statistic,
    double CriticalValue,
    double Alpha,
    string Method)
{
    /// <summary>Текстовое описание вывода</summary>
    public override string ToString()
        => $"{Method}: G = {Statistic:F3} vs G_крит = {CriticalValue:F3} → "
         + (IsOutlier ? $"значение {Value:G6} (№{Index + 1}) - промах" : "промахов не обнаружено");
}

/// <summary>
/// Критерии отбраковки грубых промахов в серии параллельных измерений
/// </summary>
/// <remarks>
/// Критическое значение критерия Граббса вычисляется через квантиль Стьюдента,
/// а не берётся из таблицы: это даёт любые n и любые уровни значимости.
/// </remarks>
public static class OutlierTests
{
    // Критические значения Q-критерия Диксона (r10) для доверительной вероятности 95%, n = 3..10
    private static readonly double[] DixonQ95 = { 0.970, 0.829, 0.710, 0.625, 0.568, 0.526, 0.493, 0.466 };

    // То же для 99%
    private static readonly double[] DixonQ99 = { 0.994, 0.926, 0.821, 0.740, 0.680, 0.634, 0.598, 0.568 };

    /// <summary>
    /// Критерий Граббса: проверяет наиболее удалённое от среднего значение
    /// </summary>
    /// <param name="data">Серия результатов (n ≥ 3)</param>
    /// <param name="alpha">Уровень значимости, по умолчанию 0.05</param>
    public static OutlierResult Grubbs(double[] data, double alpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < 3)
            throw new ArgumentException("Grubbs' test requires at least three values");

        int n = data.Length;
        double mean = data.Average();
        double std = SampleStd(data, mean);

        if (std <= 0)
            return new OutlierResult(false, 0, data[0], 0, GrubbsCritical(n, alpha), alpha, "Критерий Граббса");

        int index = 0;
        double maxDeviation = 0;

        for (int i = 0; i < n; i++)
        {
            double deviation = Math.Abs(data[i] - mean);

            if (deviation > maxDeviation)
            {
                maxDeviation = deviation;
                index = i;
            }
        }

        double statistic = maxDeviation / std;
        double critical = GrubbsCritical(n, alpha);

        return new OutlierResult(statistic > critical, index, data[index], statistic, critical, alpha, "Критерий Граббса");
    }

    /// <summary>
    /// Последовательная отбраковка по Граббсу: промахи убираются по одному,
    /// пока критерий их находит
    /// </summary>
    /// <param name="data">Серия результатов</param>
    /// <param name="alpha">Уровень значимости</param>
    /// <returns>Очищенная серия и список отбракованных значений</returns>
    public static (double[] Clean, List<double> Removed) GrubbsIterative(double[] data, double alpha = 0.05)
    {
        var remaining = data.ToList();
        var removed = new List<double>();

        while (remaining.Count >= 3)
        {
            var result = Grubbs(remaining.ToArray(), alpha);

            if (!result.IsOutlier)
                break;

            removed.Add(result.Value);
            remaining.RemoveAt(result.Index);
        }

        return (remaining.ToArray(), removed);
    }

    /// <summary>
    /// Q-критерий Диксона (r10) для малых выборок: n = 3..10
    /// </summary>
    /// <param name="data">Серия результатов</param>
    /// <param name="confidence">Доверительная вероятность: 0.95 или 0.99</param>
    public static OutlierResult Dixon(double[] data, double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < 3 || data.Length > 10)
            throw new ArgumentException("Dixon's Q-test is defined here for 3..10 values; use Grubbs' test for larger series");

        var sorted = data.OrderBy(v => v).ToArray();
        int n = sorted.Length;
        double range = sorted[n - 1] - sorted[0];

        if (range <= 0)
            return new OutlierResult(false, 0, sorted[0], 0, DixonCritical(n, confidence), 1 - confidence, "Q-критерий Диксона");

        double qLow = (sorted[1] - sorted[0]) / range;
        double qHigh = (sorted[n - 1] - sorted[n - 2]) / range;

        bool lowIsSuspect = qLow >= qHigh;
        double statistic = lowIsSuspect ? qLow : qHigh;
        double suspect = lowIsSuspect ? sorted[0] : sorted[n - 1];
        double critical = DixonCritical(n, confidence);

        return new OutlierResult(
            statistic > critical,
            Array.IndexOf(data, suspect),
            suspect,
            statistic,
            critical,
            1 - confidence,
            "Q-критерий Диксона");
    }

    /// <summary>
    /// Критическое значение критерия Граббса (двусторонний вариант)
    /// </summary>
    /// <param name="n">Объём выборки</param>
    /// <param name="alpha">Уровень значимости</param>
    public static double GrubbsCritical(int n, double alpha = 0.05)
    {
        if (n < 3)
            throw new ArgumentOutOfRangeException(nameof(n), "At least three values are required");

        double t = StatInference.TQuantile(1 - (alpha / (2.0 * n)), n - 2);
        double t2 = t * t;

        return (n - 1) / Math.Sqrt(n) * Math.Sqrt(t2 / (n - 2 + t2));
    }

    private static double DixonCritical(int n, double confidence)
    {
        var table = confidence >= 0.99 ? DixonQ99 : DixonQ95;
        return table[n - 3];
    }

    private static double SampleStd(double[] data, double mean)
    {
        double sum = data.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sum / (data.Length - 1));
    }
}
