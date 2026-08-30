using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Econometrics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>
/// Метод Theta — победитель соревнования M3 и до сих пор один из самых
/// сильных базовых прогнозов.
/// </summary>
/// <remarks>
/// <para>
/// Ряд раскладывается на две «тета-линии». Линия с параметром 0 — обычная
/// линейная регрессия на время, она описывает долгосрочный тренд. Линия
/// с параметром 2 удваивает кривизну ряда и прогнозируется простым
/// экспоненциальным сглаживанием. Итоговый прогноз — среднее двух
/// экстраполяций.
/// </para>
/// <para>
/// Результат эквивалентен сглаживанию с добавленным полутрендом:
/// </para>
/// <code>
/// forecast(h) = SES(h) + (b / 2) * (h - 1 + 1/alpha - (1-alpha)^n / alpha)
/// </code>
/// <para>
/// Практическая ценность метода в том, что у него нет параметров, кроме
/// одного коэффициента сглаживания. На коротких и шумных рядах он регулярно
/// обыгрывает ARIMA, у которой на тех же данных не из чего выбирать порядок.
/// </para>
/// </remarks>
public static class ThetaMethod
{
    /// <summary>Строит прогноз методом Theta.</summary>
    /// <param name="series">Исторический ряд.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="season">Длина сезонного цикла; 1 отключает сезонную корректировку.</param>
    /// <param name="confidenceLevel">Уровень доверия интервалов.</param>
    /// <returns>Прогноз с интервалами и диагностикой.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Ряд слишком короткий.</exception>
    public static ForecastResult Fit(Vector series, int horizon, int season = 1, double confidenceLevel = 0.9)
    {
        ArgumentNullException.ThrowIfNull(series);

        double[] y = [.. series];
        int n = y.Length;
        if (n < 5) throw new ArgumentException("Нужно минимум пять наблюдений.", nameof(series));
        if (horizon < 1) throw new ArgumentException("Горизонт должен быть положительным.", nameof(horizon));

        // Сезонность снимается мультипликативно: метод работает с рядом
        // без периодической составляющей, она возвращается в конце
        bool seasonal = season > 1 && n >= 2 * season && y.All(v => v > 0) && IsSeasonal(y, season);
        double[] indices = seasonal ? SeasonalIndices(y, season) : [];
        double[] adjusted = seasonal ? Deseasonalize(y, indices, season) : y;

        (double intercept, double slope) = LinearTrend(adjusted);

        double alpha = OptimizeAlpha(adjusted);
        (double[] fittedSes, double level) = SimpleSmoothing(adjusted, alpha);

        double drift = slope / 2.0;
        double decay = Math.Pow(1 - alpha, n) / alpha;

        var point = new Vector(horizon);
        var lower = new Vector(horizon);
        var upper = new Vector(horizon);

        double rss = 0;
        var residuals = new Vector(n);
        var fitted = new Vector(n);

        for (int t = 0; t < n; t++)
        {
            double value = seasonal ? fittedSes[t] * indices[t % season] : fittedSes[t];
            fitted[t] = value;
            residuals[t] = y[t] - value;
            if (t > 0) rss += residuals[t] * residuals[t];
        }

        double sigma = n > 1 ? Math.Sqrt(rss / (n - 1)) : 0;
        double z = EconMath.NormalInv(1 - ((1 - confidenceLevel) / 2));

        for (int h = 1; h <= horizon; h++)
        {
            double value = level + (drift * (h - 1 + (1.0 / alpha) - decay));
            if (seasonal) value *= indices[(n + h - 1) % season];

            // Дисперсия накапливается как у сглаживания с постоянным весом
            double variance = 1 + ((h - 1) * alpha * alpha);
            double halfWidth = z * sigma * Math.Sqrt(variance);

            point[h - 1] = value;
            lower[h - 1] = value - halfWidth;
            upper[h - 1] = value + halfWidth;
        }

        return new ForecastResult
        {
            Model = seasonal ? $"Theta с сезонной корректировкой [{season}]" : "Theta",
            PointForecast = point,
            Lower = lower,
            Upper = upper,
            ConfidenceLevel = confidenceLevel,
            Fitted = fitted,
            Residuals = residuals,
            Parameters = new Dictionary<string, double>
            {
                ["alpha"] = alpha,
                ["drift"] = drift,
                ["trend_slope"] = slope,
                ["intercept"] = intercept,
            },
            Sigma = sigma,
            SeasonalPeriod = seasonal ? season : 1,
            InSampleMase = Mase(y, fitted, season),
            ResidualAutocorrelation = Autocorrelation(residuals),
        };
    }

    /// <summary>Линейный тренд методом наименьших квадратов.</summary>
    private static (double Intercept, double Slope) LinearTrend(double[] y)
    {
        int n = y.Length;
        double meanT = (n - 1) / 2.0;
        double meanY = y.Average();

        double covariance = 0, variance = 0;
        for (int t = 0; t < n; t++)
        {
            covariance += (t - meanT) * (y[t] - meanY);
            variance += (t - meanT) * (t - meanT);
        }

        double slope = variance > 0 ? covariance / variance : 0;
        return (meanY - (slope * meanT), slope);
    }

    private static double OptimizeAlpha(double[] y)
    {
        double[] result = NelderMead.Minimize(
            u =>
            {
                double alpha = 0.02 + (0.96 / (1 + Math.Exp(-u[0])));
                (double[] fitted, _) = SimpleSmoothing(y, alpha);

                double sum = 0;
                for (int t = 1; t < y.Length; t++)
                {
                    double e = y[t] - fitted[t];
                    sum += e * e;
                }
                return sum;
            },
            [0.0], 0.5, 500);

        return 0.02 + (0.96 / (1 + Math.Exp(-result[0])));
    }

    private static (double[] Fitted, double Level) SimpleSmoothing(double[] y, double alpha)
    {
        int n = y.Length;
        var fitted = new double[n];
        double level = y[0];

        for (int t = 0; t < n; t++)
        {
            fitted[t] = level;
            level = (alpha * y[t]) + ((1 - alpha) * level);
        }

        return (fitted, level);
    }

    /// <summary>Мультипликативные сезонные индексы по классическому разложению.</summary>
    private static double[] SeasonalIndices(double[] y, int season)
    {
        int n = y.Length;
        var ratios = new List<double>[season];
        for (int i = 0; i < season; i++) ratios[i] = [];

        int half = season / 2;
        for (int t = half; t < n - half; t++)
        {
            double sum = 0;
            int count = 0;

            for (int k = -half; k <= half; k++)
            {
                double weight = season % 2 == 0 && Math.Abs(k) == half ? 0.5 : 1.0;
                sum += weight * y[t + k];
                count++;
            }

            double centered = sum / (season % 2 == 0 ? season : count);
            if (centered > 1e-9) ratios[t % season].Add(y[t] / centered);
        }

        var indices = new double[season];
        for (int i = 0; i < season; i++)
            indices[i] = ratios[i].Count > 0 ? ratios[i].Average() : 1.0;

        double mean = indices.Average();
        if (mean > 1e-9)
            for (int i = 0; i < season; i++) indices[i] /= mean;

        return indices;
    }

    private static double[] Deseasonalize(double[] y, double[] indices, int season)
    {
        var adjusted = new double[y.Length];
        for (int t = 0; t < y.Length; t++)
        {
            double index = indices[t % season];
            adjusted[t] = index > 1e-9 ? y[t] / index : y[t];
        }
        return adjusted;
    }

    /// <summary>
    /// Признак сезонности: автокорреляция на сезонном лаге выше порога,
    /// зависящего от длины ряда.
    /// </summary>
    private static bool IsSeasonal(double[] y, int season)
    {
        int n = y.Length;
        if (n <= season + 2) return false;

        double mean = y.Average();
        double numerator = 0, denominator = 0;

        for (int t = 0; t < n; t++)
        {
            double d = y[t] - mean;
            denominator += d * d;
            if (t >= season) numerator += d * (y[t - season] - mean);
        }

        double acf = denominator > 1e-12 ? numerator / denominator : 0;
        return acf > 1.645 / Math.Sqrt(n);
    }

    private static double Mase(double[] y, Vector fitted, int season)
    {
        int lag = Math.Max(season, 1);
        if (y.Length <= lag) return double.NaN;

        double scale = 0;
        for (int i = lag; i < y.Length; i++) scale += Math.Abs(y[i] - y[i - lag]);
        scale /= y.Length - lag;

        double mae = 0;
        for (int t = 1; t < y.Length; t++) mae += Math.Abs(y[t] - fitted[t]);
        mae /= y.Length - 1;

        return scale > 1e-12 ? mae / scale : double.NaN;
    }

    private static double Autocorrelation(Vector values)
    {
        int n = values.Count;
        if (n < 4) return double.NaN;

        double mean = 0;
        for (int t = 1; t < n; t++) mean += values[t];
        mean /= n - 1;

        double numerator = 0, denominator = 0;
        for (int t = 1; t < n; t++)
        {
            double d = values[t] - mean;
            denominator += d * d;
            if (t > 1) numerator += d * (values[t - 1] - mean);
        }

        return denominator > 1e-12 ? numerator / denominator : double.NaN;
    }
}
