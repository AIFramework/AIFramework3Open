using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>Вид сезонной составляющей в модели экспоненциального сглаживания.</summary>
public enum SeasonalityType
{
    /// <summary>Сезонности нет.</summary>
    None,

    /// <summary>Аддитивная: сезонный размах не зависит от уровня ряда.</summary>
    Additive,

    /// <summary>Мультипликативная: сезонный размах растёт вместе с уровнем.</summary>
    Multiplicative,
}

/// <summary>
/// Экспоненциальное сглаживание: простое, Хольта и Хольта — Уинтерса
/// с затухающим трендом.
/// </summary>
/// <remarks>
/// <para>
/// Модель хранит три состояния — уровень, тренд и сезонность — и обновляет
/// их с весами, убывающими вглубь истории. В отличие от ARIMA она не требует
/// стационарности и хорошо работает на коротких рядах, где подбор порядка
/// был бы гаданием.
/// </para>
/// <para>
/// Затухание тренда (параметр <c>phi</c> меньше единицы) — не косметика.
/// Недемпфированный тренд линейно продолжает последний наклон, и на горизонте
/// в год прогноз уходит в значения, которых бизнес никогда не видел.
/// Демпфирование делает долгосрочный прогноз конечным.
/// </para>
/// <para>
/// Выбор между аддитивной и мультипликативной сезонностью определяется тем,
/// растёт ли сезонный размах вместе с уровнем ряда. Для выручки почти всегда
/// верен мультипликативный вариант.
/// </para>
/// </remarks>
public sealed class ExponentialSmoothing
{
    /// <summary>Параметр сглаживания уровня.</summary>
    public double Alpha { get; private set; }

    /// <summary>Параметр сглаживания тренда.</summary>
    public double Beta { get; private set; }

    /// <summary>Параметр сглаживания сезонности.</summary>
    public double Gamma { get; private set; }

    /// <summary>Коэффициент затухания тренда.</summary>
    public double Phi { get; private set; } = 1.0;

    /// <summary>Обучает модель и строит прогноз.</summary>
    /// <param name="series">Исторический ряд.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="season">Длина сезонного цикла; 1 отключает сезонность.</param>
    /// <param name="seasonality">Вид сезонной составляющей.</param>
    /// <param name="damped">Использовать ли затухающий тренд.</param>
    /// <param name="withTrend">Включать ли тренд.</param>
    /// <param name="confidenceLevel">Уровень доверия интервалов.</param>
    /// <returns>Прогноз с интервалами и диагностикой.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Ряд слишком короткий.</exception>
    public ForecastResult Fit(
        Vector series, int horizon, int season = 1,
        SeasonalityType seasonality = SeasonalityType.None,
        bool damped = true, bool withTrend = true, double confidenceLevel = 0.9)
    {
        ArgumentNullException.ThrowIfNull(series);

        double[] y = [.. series];
        int n = y.Length;

        if (season < 1) season = 1;
        if (seasonality == SeasonalityType.None) season = 1;

        int minimum = seasonality == SeasonalityType.None ? 5 : (2 * season) + 2;
        if (n < minimum)
            throw new ArgumentException($"Нужно минимум {minimum} наблюдений.", nameof(series));

        if (seasonality == SeasonalityType.Multiplicative && y.Any(v => v <= 0))
            throw new ArgumentException(
                "Мультипликативная сезонность требует строго положительного ряда.", nameof(series));

        int parameters = 1 + (withTrend ? 1 : 0) + (seasonality != SeasonalityType.None ? 1 : 0) + (damped && withTrend ? 1 : 0);

        double[] optimum = NelderMead.Minimize(
            u => Sse(y, Decode(u, withTrend, seasonality, damped), season, seasonality, withTrend),
            new double[parameters], 0.5, 3000);

        (Alpha, Beta, Gamma, Phi) = Decode(optimum, withTrend, seasonality, damped);

        (double[] fitted, double[] errors, double level, double trend, double[] seasonal) =
            Smooth(y, (Alpha, Beta, Gamma, Phi), season, seasonality, withTrend);

        int warmup = seasonality == SeasonalityType.None ? 1 : season;
        double rss = 0;
        int counted = 0;
        for (int t = warmup; t < n; t++)
        {
            rss += errors[t] * errors[t];
            counted++;
        }

        double sigma = counted > 0 ? Math.Sqrt(rss / counted) : 0;
        double z = EconMath.NormalInv(1 - ((1 - confidenceLevel) / 2));

        var point = new Vector(horizon);
        var lower = new Vector(horizon);
        var upper = new Vector(horizon);

        double damping = 0;
        for (int h = 1; h <= horizon; h++)
        {
            damping += Math.Pow(Phi, h);
            double value = level + (withTrend ? damping * trend : 0);

            if (seasonality != SeasonalityType.None)
            {
                double factor = seasonal[(n + h - 1) % season];
                value = seasonality == SeasonalityType.Additive ? value + factor : value * factor;
            }

            // Дисперсия прогноза растёт с горизонтом: для сглаживания она
            // накапливается по весам, эквивалентным представлению ARIMA
            double variance = 0;
            for (int j = 0; j < h; j++)
            {
                double weight = j == 0 ? 1 : Alpha * (1 + (withTrend ? Beta * j * Phi : 0));
                variance += weight * weight;
            }

            double halfWidth = z * sigma * Math.Sqrt(variance);
            point[h - 1] = value;
            lower[h - 1] = value - halfWidth;
            upper[h - 1] = value + halfWidth;
        }

        var parameterMap = new Dictionary<string, double> { ["alpha"] = Alpha };
        if (withTrend) parameterMap["beta"] = Beta;
        if (seasonality != SeasonalityType.None) parameterMap["gamma"] = Gamma;
        if (damped && withTrend) parameterMap["phi"] = Phi;

        string name = seasonality switch
        {
            SeasonalityType.None => withTrend ? (damped ? "Хольт с затуханием" : "Хольт") : "Простое сглаживание",
            SeasonalityType.Additive => "Хольт — Уинтерс (аддитивная сезонность)",
            _ => "Хольт — Уинтерс (мультипликативная сезонность)",
        };

        return new ForecastResult
        {
            Model = name,
            PointForecast = point,
            Lower = lower,
            Upper = upper,
            ConfidenceLevel = confidenceLevel,
            Fitted = new Vector(fitted),
            Residuals = new Vector(errors),
            Parameters = parameterMap,
            Sigma = sigma,
            Aic = counted > 0 ? (counted * Math.Log(rss / counted)) + (2 * (parameters + 1)) : double.NaN,
            SeasonalPeriod = season,
            InSampleMase = Mase(y, fitted, warmup, season),
            ResidualAutocorrelation = Autocorrelation(errors, warmup),
        };
    }

    /// <summary>
    /// Подбирает лучшую конфигурацию сглаживания по AIC: с трендом и без,
    /// с затуханием и без, аддитивная и мультипликативная сезонность.
    /// </summary>
    /// <param name="series">Исторический ряд.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="season">Длина сезонного цикла.</param>
    /// <param name="confidenceLevel">Уровень доверия интервалов.</param>
    /// <returns>Прогноз лучшей конфигурации.</returns>
    public static ForecastResult AutoFit(Vector series, int horizon, int season = 1, double confidenceLevel = 0.9)
    {
        ArgumentNullException.ThrowIfNull(series);

        var seasonalities = new List<SeasonalityType> { SeasonalityType.None };
        if (season > 1 && series.Count >= (2 * season) + 2)
        {
            seasonalities.Add(SeasonalityType.Additive);
            if (series.All(v => v > 0)) seasonalities.Add(SeasonalityType.Multiplicative);
        }

        ForecastResult? best = null;

        foreach (SeasonalityType seasonality in seasonalities)
        {
            foreach (bool withTrend in new[] { false, true })
            {
                foreach (bool damped in withTrend ? new[] { false, true } : [false])
                {
                    try
                    {
                        ForecastResult candidate = new ExponentialSmoothing()
                            .Fit(series, horizon, season, seasonality, damped, withTrend, confidenceLevel);

                        if (best is null || (!double.IsNaN(candidate.Aic) && candidate.Aic < best.Aic))
                            best = candidate;
                    }
                    catch (ArgumentException)
                    {
                        // Конфигурация не подходит длине ряда — пропускаем
                    }
                }
            }
        }

        return best ?? throw new ArgumentException("Не удалось подобрать модель.", nameof(series));
    }

    /// <summary>Переводит неограниченные параметры в допустимый диапазон (0; 1).</summary>
    private static (double Alpha, double Beta, double Gamma, double Phi) Decode(
        double[] u, bool withTrend, SeasonalityType seasonality, bool damped)
    {
        double Sigmoid(double x) => 0.02 + (0.96 / (1 + Math.Exp(-x)));

        int index = 0;
        double alpha = Sigmoid(u[index++]);
        double beta = withTrend ? Sigmoid(u[index++]) * alpha : 0;
        double gamma = seasonality != SeasonalityType.None ? Sigmoid(u[index++]) * (1 - alpha) : 0;
        double phi = damped && withTrend ? 0.8 + (0.19 / (1 + Math.Exp(-u[index]))) : 1.0;

        return (alpha, beta, gamma, phi);
    }

    private static double Sse(
        double[] y, (double Alpha, double Beta, double Gamma, double Phi) p,
        int season, SeasonalityType seasonality, bool withTrend)
    {
        (_, double[] errors, _, _, _) = Smooth(y, p, season, seasonality, withTrend);

        int warmup = seasonality == SeasonalityType.None ? 1 : season;
        double sum = 0;
        for (int t = warmup; t < errors.Length; t++) sum += errors[t] * errors[t];

        return double.IsNaN(sum) || double.IsInfinity(sum) ? double.PositiveInfinity : sum;
    }

    /// <summary>Прогон рекуррентных уравнений сглаживания по всему ряду.</summary>
    private static (double[] Fitted, double[] Errors, double Level, double Trend, double[] Seasonal) Smooth(
        double[] y, (double Alpha, double Beta, double Gamma, double Phi) p,
        int season, SeasonalityType seasonality, bool withTrend)
    {
        int n = y.Length;
        var fitted = new double[n];
        var errors = new double[n];
        var seasonal = new double[Math.Max(season, 1)];

        // Начальные состояния: уровень и наклон по первому циклу,
        // сезонные индексы как отклонения от среднего этого цикла
        double level, trend = 0;

        if (seasonality == SeasonalityType.None)
        {
            level = y[0];
            if (withTrend && n > 1) trend = y[1] - y[0];
            for (int i = 0; i < seasonal.Length; i++)
                seasonal[i] = seasonality == SeasonalityType.Multiplicative ? 1 : 0;
        }
        else
        {
            double firstCycle = 0;
            for (int i = 0; i < season; i++) firstCycle += y[i];
            firstCycle /= season;
            level = firstCycle;

            if (withTrend && n >= 2 * season)
            {
                double secondCycle = 0;
                for (int i = season; i < 2 * season; i++) secondCycle += y[i];
                secondCycle /= season;
                trend = (secondCycle - firstCycle) / season;
            }

            for (int i = 0; i < season; i++)
            {
                seasonal[i] = seasonality == SeasonalityType.Additive
                    ? y[i] - firstCycle
                    : firstCycle > 0 ? y[i] / firstCycle : 1;
            }
        }

        for (int t = 0; t < n; t++)
        {
            double seasonalFactor = seasonality == SeasonalityType.None
                ? (seasonality == SeasonalityType.Multiplicative ? 1 : 0)
                : seasonal[t % season];

            double prediction = level + (withTrend ? p.Phi * trend : 0);
            prediction = seasonality switch
            {
                SeasonalityType.Additive => prediction + seasonalFactor,
                SeasonalityType.Multiplicative => prediction * seasonalFactor,
                _ => prediction,
            };

            fitted[t] = prediction;
            errors[t] = y[t] - prediction;

            double deseasonalized = seasonality switch
            {
                SeasonalityType.Additive => y[t] - seasonalFactor,
                SeasonalityType.Multiplicative => seasonalFactor > 1e-9 ? y[t] / seasonalFactor : y[t],
                _ => y[t],
            };

            double previousLevel = level;
            level = (p.Alpha * deseasonalized) + ((1 - p.Alpha) * (level + (withTrend ? p.Phi * trend : 0)));

            if (withTrend)
                trend = (p.Beta * (level - previousLevel)) + ((1 - p.Beta) * p.Phi * trend);

            if (seasonality == SeasonalityType.Additive)
                seasonal[t % season] = (p.Gamma * (y[t] - level)) + ((1 - p.Gamma) * seasonalFactor);
            else if (seasonality == SeasonalityType.Multiplicative && Math.Abs(level) > 1e-9)
                seasonal[t % season] = (p.Gamma * (y[t] / level)) + ((1 - p.Gamma) * seasonalFactor);
        }

        return (fitted, errors, level, trend, seasonal);
    }

    private static double Mase(double[] y, double[] fitted, int warmup, int season)
    {
        int lag = Math.Max(season, 1);
        if (y.Length <= lag) return double.NaN;

        double scale = 0;
        for (int i = lag; i < y.Length; i++) scale += Math.Abs(y[i] - y[i - lag]);
        scale /= y.Length - lag;

        double mae = 0;
        int counted = 0;
        for (int t = warmup; t < y.Length; t++)
        {
            mae += Math.Abs(y[t] - fitted[t]);
            counted++;
        }

        return counted > 0 && scale > 1e-12 ? mae / counted / scale : double.NaN;
    }

    private static double Autocorrelation(double[] values, int from)
    {
        int n = values.Length;
        if (n - from < 3) return double.NaN;

        double mean = 0;
        for (int t = from; t < n; t++) mean += values[t];
        mean /= n - from;

        double numerator = 0, denominator = 0;
        for (int t = from; t < n; t++)
        {
            double d = values[t] - mean;
            denominator += d * d;
            if (t > from) numerator += d * (values[t - 1] - mean);
        }

        return denominator > 1e-12 ? numerator / denominator : double.NaN;
    }
}
