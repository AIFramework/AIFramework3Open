using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>Результат STL-разложения ряда.</summary>
public sealed record StlResult : IInterpretable
{
    /// <summary>Исходный ряд.</summary>
    public Vector Series { get; init; } = new Vector(0);

    /// <summary>Трендовая составляющая.</summary>
    public Vector Trend { get; init; } = new Vector(0);

    /// <summary>Сезонная составляющая.</summary>
    public Vector Seasonal { get; init; } = new Vector(0);

    /// <summary>Остаток после вычитания тренда и сезонности.</summary>
    public Vector Remainder { get; init; } = new Vector(0);

    /// <summary>Ряд, очищенный от сезонности.</summary>
    public Vector SeasonallyAdjusted { get; init; } = new Vector(0);

    /// <summary>Длина сезонного цикла.</summary>
    public int Period { get; init; }

    /// <summary>
    /// Сила тренда по Хиндману: доля дисперсии, которую тренд убирает
    /// из остатка. Значения выше 0,6 означают выраженный тренд.
    /// </summary>
    public double TrendStrength { get; init; }

    /// <summary>Сила сезонности по той же шкале.</summary>
    public double SeasonalStrength { get; init; }

    /// <summary>Индексы наблюдений, признанных выбросами по остатку.</summary>
    public IReadOnlyList<int> Outliers { get; init; } = [];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double seasonalRange = Seasonal.Count > 0 ? Seasonal.Max() - Seasonal.Min() : 0;
        double level = Series.Count > 0 ? Series.Average() : 0;
        double relativeSeasonal = Math.Abs(level) > 1e-9 ? seasonalRange / Math.Abs(level) : 0;
        double trendChange = Trend.Count > 1 ? Trend[Trend.Count - 1] - Trend[0] : 0;

        return new InterpretationBuilder("STL-разложение ряда")
            .Summary($"Сила тренда {Fmt.Num(TrendStrength)}, сила сезонности {Fmt.Num(SeasonalStrength)}. " +
                     $"Сезонный размах составляет {Fmt.Pct(relativeSeasonal)} среднего уровня ряда, " +
                     $"тренд за период наблюдений изменился на {Fmt.Num(trendChange)}.")
            .Metric("Сила тренда", TrendStrength, null,
                "выше 0,6 — тренд выражен, ниже 0,4 — его почти нет",
                TrendStrength > 0.6 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Сила сезонности", SeasonalStrength, null,
                "выше 0,6 — сезонность надо моделировать явно",
                SeasonalStrength > 0.6 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Сезонный размах", Fmt.Pct(relativeSeasonal), null,
                "относительно среднего уровня ряда")
            .Metric("Изменение тренда", trendChange, null, "от начала к концу наблюдений")
            .Metric("Выбросов", Outliers.Count, null,
                "наблюдений с остатком больше трёх межквартильных размахов",
                Outliers.Count > Series.Count * 0.05 ? MetricQuality.Warning : MetricQuality.Good, 0)
            .FindingIf(SeasonalStrength > 0.6,
                "Сезонность выражена: сравнивать месяц с предыдущим месяцем бессмысленно, " +
                "сравнивайте с тем же месяцем прошлого года или используйте очищенный ряд.")
            .FindingIf(TrendStrength > 0.6 && trendChange > 0,
                "Тренд устойчиво растущий: рост не сводится к сезонному пику.")
            .FindingIf(TrendStrength > 0.6 && trendChange < 0,
                "Тренд устойчиво снижается. Сезонные всплески маскируют это в исходном ряде.")
            .FindingIf(SeasonalStrength < 0.3,
                "Сезонность слабая: сезонная модель добавит параметров, не добавив точности.")
            .FindingIf(Outliers.Count > 0,
                $"Найдены выбросы в позициях: {string.Join(", ", Outliers.Take(10))}" +
                (Outliers.Count > 10 ? " и других" : "") +
                ". Проверьте, не промоакции ли это — их стоит вынести в отдельный регрессор.")
            .WarningIf(Series.Count < Period * 3,
                $"Ряд короче трёх сезонных циклов ({Series.Count} наблюдений при периоде {Period}). " +
                "Сезонная составляющая оценена ненадёжно.")
            .Warning("Разложение аддитивное. Если сезонный размах растёт вместе с уровнем ряда, " +
                     "прологарифмируйте ряд перед разложением.")
            .Recommendation("Используйте очищенный от сезонности ряд для отслеживания динамики " +
                            "и исходный — для планирования запасов и штата.")
            .Build();
    }
}

/// <summary>
/// STL: разложение ряда на тренд, сезонность и остаток локально взвешенной
/// регрессией.
/// </summary>
/// <remarks>
/// <para>
/// В отличие от классического разложения скользящим средним STL допускает
/// сезонность, меняющуюся во времени, и устойчив к выбросам за счёт
/// внешнего цикла с робастными весами. Это важно для реальных рядов
/// продаж, где сезонный профиль дрейфует год от года, а промоакции дают
/// разовые всплески.
/// </para>
/// <para>
/// Алгоритм: во внутреннем цикле ряд детрендируется, сезонные подряды
/// сглаживаются локальной регрессией, результат пропускается через
/// низкочастотный фильтр и вычитается, затем по очищенному ряду строится
/// тренд. Внешний цикл пересчитывает веса наблюдений по величине остатка.
/// </para>
/// </remarks>
public static class StlDecomposition
{
    /// <summary>Раскладывает ряд на составляющие.</summary>
    /// <param name="series">Исходный ряд.</param>
    /// <param name="period">Длина сезонного цикла.</param>
    /// <param name="seasonalSpan">Окно сглаживания сезонных подрядов, нечётное.</param>
    /// <param name="trendSpan">Окно сглаживания тренда; 0 — выбрать автоматически.</param>
    /// <param name="robustIterations">Число внешних итераций устойчивости к выбросам.</param>
    /// <returns>Тренд, сезонность, остаток и их характеристики.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Ряд короче двух сезонных циклов.</exception>
    public static StlResult Decompose(
        Vector series, int period, int seasonalSpan = 11, int trendSpan = 0, int robustIterations = 1)
    {
        ArgumentNullException.ThrowIfNull(series);

        int n = series.Count;
        if (period < 2) throw new ArgumentException("Период должен быть не меньше двух.", nameof(period));
        if (n < 2 * period)
            throw new ArgumentException("Ряд должен покрывать минимум два сезонных цикла.", nameof(series));

        double[] y = [.. series];

        seasonalSpan = MakeOdd(Math.Max(seasonalSpan, 7));
        if (trendSpan <= 0)
            trendSpan = MakeOdd((int)Math.Ceiling(1.5 * period / (1 - (1.5 / seasonalSpan))));
        int lowPassSpan = MakeOdd(period);

        var trend = new double[n];
        var seasonal = new double[n];
        var weights = new double[n];
        for (int i = 0; i < n; i++) weights[i] = 1.0;

        for (int outer = 0; outer <= robustIterations; outer++)
        {
            for (int inner = 0; inner < 2; inner++)
            {
                var detrended = new double[n];
                for (int t = 0; t < n; t++) detrended[t] = y[t] - trend[t];

                double[] extended = SmoothCycleSubseries(detrended, weights, period, seasonalSpan, n);
                double[] lowPass = LowPassFilter(extended, period, lowPassSpan, n);

                for (int t = 0; t < n; t++) seasonal[t] = extended[t + period] - lowPass[t];

                var adjusted = new double[n];
                for (int t = 0; t < n; t++) adjusted[t] = y[t] - seasonal[t];

                trend = Loess(adjusted, weights, trendSpan);
            }

            if (outer == robustIterations) break;

            var remainder = new double[n];
            for (int t = 0; t < n; t++) remainder[t] = y[t] - trend[t] - seasonal[t];
            weights = RobustnessWeights(remainder);
        }

        var remainderFinal = new double[n];
        var adjustedFinal = new double[n];
        for (int t = 0; t < n; t++)
        {
            remainderFinal[t] = y[t] - trend[t] - seasonal[t];
            adjustedFinal[t] = y[t] - seasonal[t];
        }

        return new StlResult
        {
            Series = series,
            Trend = new Vector(trend),
            Seasonal = new Vector(seasonal),
            Remainder = new Vector(remainderFinal),
            SeasonallyAdjusted = new Vector(adjustedFinal),
            Period = period,
            TrendStrength = Strength(remainderFinal, Add(remainderFinal, trend)),
            SeasonalStrength = Strength(remainderFinal, Add(remainderFinal, seasonal)),
            Outliers = FindOutliers(remainderFinal),
        };
    }

    private static int MakeOdd(int value) => value % 2 == 0 ? value + 1 : value;

    private static double[] Add(double[] a, double[] b)
    {
        var result = new double[a.Length];
        for (int i = 0; i < a.Length; i++) result[i] = a[i] + b[i];
        return result;
    }

    /// <summary>
    /// Локально взвешенная регрессия: в каждой точке строится линейная
    /// аппроксимация по ближайшим соседям с трикубическими весами.
    /// </summary>
    private static double[] Loess(double[] y, double[] weights, int span)
    {
        int n = y.Length;
        var result = new double[n];
        int half = Math.Max(span / 2, 1);

        for (int t = 0; t < n; t++)
        {
            int from = Math.Max(0, t - half);
            int to = Math.Min(n - 1, t + half);
            double maxDistance = Math.Max(t - from, to - t);
            if (maxDistance <= 0) maxDistance = 1;

            double sw = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;

            for (int i = from; i <= to; i++)
            {
                double u = Math.Abs(i - t) / maxDistance;
                double tricube = u >= 1 ? 0 : Math.Pow(1 - (u * u * u), 3);
                double w = tricube * weights[i];
                if (w <= 0) continue;

                sw += w;
                sx += w * i;
                sy += w * y[i];
                sxx += w * i * i;
                sxy += w * i * y[i];
            }

            if (sw <= 0) { result[t] = y[t]; continue; }

            double denominator = (sw * sxx) - (sx * sx);
            if (Math.Abs(denominator) < 1e-12)
            {
                result[t] = sy / sw;
                continue;
            }

            double slope = ((sw * sxy) - (sx * sy)) / denominator;
            double intercept = (sy - (slope * sx)) / sw;
            result[t] = intercept + (slope * t);
        }

        return result;
    }

    /// <summary>
    /// Сглаживание сезонных подрядов с продлением на один цикл в каждую
    /// сторону — продление нужно низкочастотному фильтру следующего шага.
    /// </summary>
    private static double[] SmoothCycleSubseries(
        double[] detrended, double[] weights, int period, int span, int n)
    {
        var extended = new double[n + (2 * period)];

        for (int k = 0; k < period; k++)
        {
            int count = 0;
            for (int t = k; t < n; t += period) count++;
            if (count == 0) continue;

            var sub = new double[count];
            var subWeights = new double[count];
            int index = 0;
            for (int t = k; t < n; t += period)
            {
                sub[index] = detrended[t];
                subWeights[index] = weights[t];
                index++;
            }

            double[] smoothed = Loess(sub, subWeights, Math.Min(span, MakeOdd(Math.Max(count, 3))));

            // Значения до начала и после конца берутся продолжением
            // локальной регрессии крайних точек
            extended[k] = smoothed[0];
            for (int i = 0; i < count; i++) extended[k + period + (i * period)] = smoothed[i];

            int tail = k + period + (count * period);
            if (tail < extended.Length) extended[tail] = smoothed[count - 1];
        }

        // Позиции, не попавшие ни в один подряд, заполняются соседями
        for (int i = 1; i < extended.Length; i++)
            if (extended[i] == 0 && extended[i - 1] != 0) extended[i] = extended[i - 1];

        return extended;
    }

    /// <summary>Низкочастотный фильтр: три скользящих средних и локальная регрессия.</summary>
    private static double[] LowPassFilter(double[] extended, int period, int span, int n)
    {
        double[] first = MovingAverage(extended, period);
        double[] second = MovingAverage(first, period);
        double[] third = MovingAverage(second, 3);

        var trimmed = new double[n];
        int offset = Math.Max((third.Length - n) / 2, 0);
        for (int t = 0; t < n; t++)
        {
            int index = Math.Min(t + offset, third.Length - 1);
            trimmed[t] = third[index];
        }

        var unit = new double[n];
        for (int i = 0; i < n; i++) unit[i] = 1.0;

        return Loess(trimmed, unit, span);
    }

    private static double[] MovingAverage(double[] values, int window)
    {
        if (window <= 1 || values.Length < window) return values;

        var result = new double[values.Length - window + 1];
        double sum = 0;

        for (int i = 0; i < window; i++) sum += values[i];
        result[0] = sum / window;

        for (int i = window; i < values.Length; i++)
        {
            sum += values[i] - values[i - window];
            result[i - window + 1] = sum / window;
        }

        return result;
    }

    /// <summary>Бивесовые веса Тьюки: выбросы получают нулевой вес.</summary>
    private static double[] RobustnessWeights(double[] remainder)
    {
        int n = remainder.Length;
        double[] absolute = [.. remainder.Select(Math.Abs).OrderBy(v => v)];
        double median = absolute[n / 2];
        double scale = 6 * median;

        var weights = new double[n];
        for (int t = 0; t < n; t++)
        {
            if (scale < 1e-12) { weights[t] = 1; continue; }

            double u = Math.Abs(remainder[t]) / scale;
            weights[t] = u >= 1 ? 0 : Math.Pow(1 - (u * u), 2);
        }

        return weights;
    }

    /// <summary>
    /// Сила составляющей по Хиндману: во сколько раз она уменьшает дисперсию
    /// по сравнению с одним остатком.
    /// </summary>
    private static double Strength(double[] remainder, double[] withComponent)
    {
        double varianceRemainder = Variance(remainder);
        double varianceTotal = Variance(withComponent);
        if (varianceTotal < 1e-12) return 0;

        return Math.Max(0, 1 - (varianceRemainder / varianceTotal));
    }

    private static double Variance(double[] values)
    {
        if (values.Length < 2) return 0;
        double mean = values.Average();
        double sum = 0;
        foreach (double v in values) sum += (v - mean) * (v - mean);
        return sum / (values.Length - 1);
    }

    private static List<int> FindOutliers(double[] remainder)
    {
        double[] sorted = [.. remainder.OrderBy(v => v)];
        double q1 = EconMath.Quantile(sorted, 0.25);
        double q3 = EconMath.Quantile(sorted, 0.75);
        double iqr = q3 - q1;

        var outliers = new List<int>();
        if (iqr < 1e-12) return outliers;

        for (int t = 0; t < remainder.Length; t++)
            if (remainder[t] < q1 - (3 * iqr) || remainder[t] > q3 + (3 * iqr)) outliers.Add(t);

        return outliers;
    }
}
