using System;
using AI.DataStructs.Algebraic;
using AI.Statistics;

namespace AI.DSP.Multirate;

/// <summary>
/// Показатели качества передискретизации главы 3: максимум нормированной ВКФ, эквивалентное ОСШ,
/// дисперсия шума квантования. Номера формул — по редакции глав 2–3 (в экспериментах — (3.10), (3.11)).
/// </summary>
public static class ResamplingMetrics
{
    /// <summary>
    /// Максимум модуля нормированной взаимокорреляционной функции по сдвигам |k| ≤ K_max, (2.22) [(3.10)].
    /// Сравниваются min(N₁, N₂) отсчётов; края шириной max(K_max, N/20) исключаются.
    /// </summary>
    /// <param name="reference">s⁽¹⁾ — эталонный выход (например, стандартной схемы).</param>
    /// <param name="test">s⁽²⁾ — выход исследуемой схемы.</param>
    /// <param name="maxShift">K_max.</param>
    public static double MaxNormalizedCrossCorrelation(Vector reference, Vector test, int maxShift = 32)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(test);
        int n = Math.Min(reference.Count, test.Count);
        int edge = Math.Max(maxShift, n / 20);
        int len = n - (2 * edge);
        if (len < 2)
        {
            throw new ArgumentException($"Сигналы слишком короткие: {n} отсчётов при сдвиге {maxShift}.");
        }

        double[] a = reference.ToArray();
        double[] b = test.ToArray();
        double best = 0;

        // Нормированная взаимная корреляция на сдвиге k — модуль коэффициента Пирсона двух окон
        for (int k = -maxShift; k <= maxShift; k++)
        {
            double r = Math.Abs(Statistic.Pearson(a.AsSpan(edge, len), b.AsSpan(edge - k, len)));
            best = Math.Max(best, r);
        }

        return best;
    }

    /// <summary>
    /// Эквивалентное ОСШ по коэффициенту корреляции, (2.23): 10 lg (Q² / (1 − Q²)), дБ.
    /// </summary>
    public static double SnrFromCorrelation(double q) => q >= 1 ? double.PositiveInfinity : 10 * Math.Log10(q * q / (1 - (q * q)));

    /// <summary>
    /// Дисперсия шума квантования, (2.24) [(3.11)]: Σ (s⁽¹⁾ − s⁽²⁾)² / (N − 1).
    /// </summary>
    /// <param name="fixedPoint">Выход целочисленной реализации.</param>
    /// <param name="floatingPoint">Выход той же схемы в double.</param>
    public static double QuantizationNoiseVariance(Vector fixedPoint, Vector floatingPoint)
    {
        ArgumentNullException.ThrowIfNull(fixedPoint);
        ArgumentNullException.ThrowIfNull(floatingPoint);
        int n = Math.Min(fixedPoint.Count, floatingPoint.Count);
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double d = fixedPoint[i] - floatingPoint[i];
            sum += d * d;
        }

        return sum / (n - 1);
    }

    /// <summary>
    /// ОСШ квантования: 10 lg (σ²_s / D_N), дБ (таблица 3.4).
    /// </summary>
    public static double QuantizationSnr(Vector fixedPoint, Vector floatingPoint)
    {
        double power = StatUtils.MeanAndVariance(floatingPoint.ToArray(), skipNaN: false).variance;
        return 10 * Math.Log10(power / QuantizationNoiseVariance(fixedPoint, floatingPoint));
    }
}
