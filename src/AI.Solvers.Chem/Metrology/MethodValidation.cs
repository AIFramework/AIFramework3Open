using AI.Statistics;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Metrology;

/// <summary>
/// Показатели прецизионности методики по ГОСТ Р ИСО 5725
/// </summary>
/// <param name="GrandMean">Общее среднее</param>
/// <param name="RepeatabilityStd">СКО повторяемости s_r</param>
/// <param name="BetweenGroupStd">Межсерийное СКО s_L</param>
/// <param name="IntermediateStd">СКО промежуточной прецизионности s_R = √(s_r² + s_L²)</param>
/// <param name="GroupCount">Число серий</param>
/// <param name="TotalCount">Общее число измерений</param>
public readonly record struct PrecisionResult(
    double GrandMean,
    double RepeatabilityStd,
    double BetweenGroupStd,
    double IntermediateStd,
    int GroupCount,
    int TotalCount)
{
    /// <summary>Относительное СКО повторяемости, %</summary>
    public double RepeatabilityRsdPercent => GrandMean == 0 ? double.NaN : 100.0 * RepeatabilityStd / Math.Abs(GrandMean);

    /// <summary>Относительное СКО промежуточной прецизионности, %</summary>
    public double IntermediateRsdPercent => GrandMean == 0 ? double.NaN : 100.0 * IntermediateStd / Math.Abs(GrandMean);

    /// <summary>Предел повторяемости r = 2.8·s_r</summary>
    public double RepeatabilityLimit => 2.8 * RepeatabilityStd;

    /// <summary>Предел промежуточной прецизионности R = 2.8·s_R</summary>
    public double IntermediateLimit => 2.8 * IntermediateStd;
}

/// <summary>
/// Оценка правильности методики по опытам с добавками
/// </summary>
/// <param name="MeanRecoveryPercent">Средняя степень извлечения, %</param>
/// <param name="StdPercent">СКО степени извлечения, %</param>
/// <param name="Lower">Нижняя граница доверительного интервала, %</param>
/// <param name="Upper">Верхняя граница доверительного интервала, %</param>
/// <param name="BiasSignificant">Значима ли систематическая погрешность</param>
/// <param name="Count">Число опытов</param>
public readonly record struct RecoveryResult(
    double MeanRecoveryPercent,
    double StdPercent,
    double Lower,
    double Upper,
    bool BiasSignificant,
    int Count);

/// <summary>
/// Валидация аналитической методики: прецизионность, правильность,
/// пределы обнаружения по холостой пробе.
/// </summary>
public static class MethodValidation
{
    /// <summary>
    /// Прецизионность по данным нескольких серий (дней, операторов, приборов):
    /// однофакторный дисперсионный анализ разделяет внутрисерийный и межсерийный разброс
    /// </summary>
    /// <param name="groups">Серии измерений: каждая - параллельные определения одной пробы</param>
    public static PrecisionResult Precision(double[][] groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var valid = groups.Where(g => g is { Length: > 0 }).ToArray();

        if (valid.Length < 2)
            throw new ArgumentException("At least two series are required to separate within- and between-series variance");

        int total = valid.Sum(g => g.Length);
        double grandMean = valid.SelectMany(g => g).Average();

        double withinSum = 0;
        double betweenSum = 0;

        foreach (var group in valid)
        {
            double mean = group.Average();
            withinSum += group.Sum(v => (v - mean) * (v - mean));
            betweenSum += group.Length * (mean - grandMean) * (mean - grandMean);
        }

        int withinDf = total - valid.Length;
        int betweenDf = valid.Length - 1;

        double msWithin = withinDf > 0 ? withinSum / withinDf : 0;
        double msBetween = betweenSum / betweenDf;

        // Эффективный размер серии: для несбалансированных данных - формула n0
        double sumSquares = valid.Sum(g => (double)g.Length * g.Length);
        double n0 = (total - (sumSquares / total)) / betweenDf;

        double betweenVariance = n0 > 0 ? (msBetween - msWithin) / n0 : 0;

        if (betweenVariance < 0)
            betweenVariance = 0; // отрицательная оценка дисперсии смысла не имеет

        double repeatability = Math.Sqrt(msWithin);
        double between = Math.Sqrt(betweenVariance);

        return new PrecisionResult(
            grandMean,
            repeatability,
            between,
            Math.Sqrt(msWithin + betweenVariance),
            valid.Length,
            total);
    }

    /// <summary>
    /// Правильность по методу добавок: степень извлечения и проверка значимости
    /// систематической погрешности (t-критерий против 100%)
    /// </summary>
    /// <param name="found">Найденные количества добавки</param>
    /// <param name="added">Введённые количества добавки</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static RecoveryResult Recovery(double[] found, double[] added, double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(added);

        if (found.Length != added.Length)
            throw new ArgumentException("Found and added arrays must have the same length");

        if (found.Length < 2)
            throw new ArgumentException("At least two spike experiments are required");

        var recoveries = new double[found.Length];

        for (int i = 0; i < found.Length; i++)
        {
            if (added[i] == 0)
                throw new ArgumentException($"Added amount is zero in experiment {i + 1}");

            recoveries[i] = 100.0 * found[i] / added[i];
        }

        int n = recoveries.Length;
        double mean = recoveries.Average();
        double std = Math.Sqrt(recoveries.Sum(r => (r - mean) * (r - mean)) / (n - 1));
        double t = StatInference.TQuantile(1 - ((1 - confidence) / 2), n - 1);
        double delta = t * std / Math.Sqrt(n);

        return new RecoveryResult(
            mean,
            std,
            mean - delta,
            mean + delta,
            !(mean - delta <= 100.0 && 100.0 <= mean + delta),
            n);
    }

    /// <summary>
    /// Предел обнаружения по холостой пробе: LOD = k·s(blank)/S
    /// </summary>
    /// <param name="blankSignals">Отклики холостых проб</param>
    /// <param name="slope">Чувствительность (наклон градуировки)</param>
    /// <param name="k">Коэффициент: 3 для LOD, 10 для LOQ</param>
    public static double DetectionLimitFromBlank(double[] blankSignals, double slope, double k = 3.0)
    {
        ArgumentNullException.ThrowIfNull(blankSignals);

        if (blankSignals.Length < 2)
            throw new ArgumentException("At least two blank measurements are required");

        if (Math.Abs(slope) < 1e-15)
            throw new ArgumentException("Slope must be non-zero");

        double mean = blankSignals.Average();
        double std = Math.Sqrt(blankSignals.Sum(v => (v - mean) * (v - mean)) / (blankSignals.Length - 1));

        return k * std / Math.Abs(slope);
    }

    /// <summary>Отчёт по прецизионности</summary>
    /// <param name="result">Результат расчёта</param>
    public static string Report(PrecisionResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Прецизионность методики (ГОСТ Р ИСО 5725)");
        text.AppendLine($"  Серий: {result.GroupCount}, измерений: {result.TotalCount}");
        text.AppendLine($"  Общее среднее: {result.GrandMean.ToString("G6", culture)}");
        text.AppendLine($"  Повторяемость s_r = {result.RepeatabilityStd.ToString("G4", culture)}"
            + $" (RSD = {result.RepeatabilityRsdPercent.ToString("F2", culture)}%), предел r = {result.RepeatabilityLimit.ToString("G4", culture)}");
        text.AppendLine($"  Межсерийное s_L = {result.BetweenGroupStd.ToString("G4", culture)}");
        text.AppendLine($"  Промежуточная прецизионность s_R = {result.IntermediateStd.ToString("G4", culture)}"
            + $" (RSD = {result.IntermediateRsdPercent.ToString("F2", culture)}%), предел R = {result.IntermediateLimit.ToString("G4", culture)}");

        return text.ToString();
    }
}
