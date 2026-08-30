using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Econometrics;

/// <summary>Структурная модель ненаблюдаемых компонент.</summary>
public enum StateSpaceModel
{
    /// <summary>Локальный уровень: случайное блуждание плюс шум наблюдения.</summary>
    LocalLevel,

    /// <summary>Локальный линейный тренд: уровень и наклон изменяются во времени.</summary>
    LocalLinearTrend,
}

/// <summary>Результат оценивания модели в пространстве состояний.</summary>
public sealed record StateSpaceResult : IInterpretable
{
    /// <summary>Спецификация модели.</summary>
    public StateSpaceModel Model { get; init; }

    /// <summary>Сглаженная оценка уровня.</summary>
    public Vector Level { get; init; } = new(0);

    /// <summary>Сглаженная оценка наклона.</summary>
    public Vector Slope { get; init; } = new(0);

    /// <summary>Отфильтрованная оценка уровня: только по прошлым наблюдениям.</summary>
    public Vector FilteredLevel { get; init; } = new(0);

    /// <summary>Ошибки прогноза на один шаг.</summary>
    public Vector Innovations { get; init; } = new(0);

    /// <summary>Прогноз на будущие периоды.</summary>
    public Vector Forecast { get; init; } = new(0);

    /// <summary>Нижняя граница интервала прогноза.</summary>
    public Vector ForecastLower { get; init; } = new(0);

    /// <summary>Верхняя граница интервала прогноза.</summary>
    public Vector ForecastUpper { get; init; } = new(0);

    /// <summary>Дисперсия шума наблюдения.</summary>
    public double ObservationVariance { get; init; }

    /// <summary>Дисперсия шума уровня.</summary>
    public double LevelVariance { get; init; }

    /// <summary>Дисперсия шума наклона.</summary>
    public double SlopeVariance { get; init; }

    /// <summary>Отношение сигнал-шум.</summary>
    public double SignalToNoise =>
        ObservationVariance > 0 ? LevelVariance / ObservationVariance : 0;

    /// <summary>Логарифм правдоподобия.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Информационный критерий Акаике.</summary>
    public double Aic { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Статистика Люнга — Бокса по ошибкам прогноза.</summary>
    public double LjungBox { get; init; }

    /// <summary>Уровень значимости теста Люнга — Бокса.</summary>
    public double LjungBoxPValue { get; init; } = 1;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool whiteNoise = LjungBoxPValue >= 0.05;
        bool smooth = SignalToNoise < 0.1;
        double currentLevel = Level.Count > 0 ? Level[^1] : 0;
        double currentSlope = Slope.Count > 0 ? Slope[^1] : 0;

        var builder = new InterpretationBuilder($"Модель в пространстве состояний: {ModelName()}")
            .Summary($"Оценено по {Observations} наблюдениям фильтром Калмана. " +
                     $"Отношение сигнал-шум {Fmt.Num(SignalToNoise, 4)}: " +
                     (smooth ? "уровень меняется медленно, ряд в основном шум."
                             : "уровень подвижен, наблюдения информативны.") +
                     $" Текущий уровень {Fmt.Num(currentLevel, 3)}" +
                     (Model == StateSpaceModel.LocalLinearTrend
                         ? $", наклон {Fmt.Num(currentSlope, 4)} за период."
                         : ".") +
                     $" Ошибки прогноза: p = {Fmt.Num(LjungBoxPValue, 4)}.")
            .Metric("Сигнал-шум", SignalToNoise, null,
                "отношение дисперсии уровня к дисперсии наблюдения",
                MetricQuality.Neutral, 4)
            .Metric("Текущий уровень", currentLevel, null,
                "сглаженная оценка на последнюю дату", MetricQuality.Neutral, 3)
            .Metric("Дисперсия наблюдения", ObservationVariance, null,
                "часть колебаний, объявленная шумом", MetricQuality.Neutral, 5)
            .Metric("Дисперсия уровня", LevelVariance, null,
                "насколько свободно уровень меняется", MetricQuality.Neutral, 5)
            .Metric("Люнг — Бокс", LjungBox, null,
                $"p = {Fmt.Num(LjungBoxPValue, 4)}; " +
                (whiteNoise ? "ошибки прогноза не автокоррелированы" : "в ошибках осталась структура"),
                whiteNoise ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Логарифм правдоподобия", LogLikelihood, null,
                $"AIC {Fmt.Num(Aic, 1)}", MetricQuality.Neutral, 2);

        if (Model == StateSpaceModel.LocalLinearTrend)
        {
            builder
                .Metric("Текущий наклон", currentSlope, null, "прирост уровня за период",
                    MetricQuality.Neutral, 4)
                .Metric("Дисперсия наклона", SlopeVariance, null,
                    SlopeVariance < 1e-10 ? "наклон практически постоянен" : "наклон меняется во времени",
                    MetricQuality.Neutral, 6);
        }

        return builder
            .Finding("Фильтр Калмана разделяет наблюдаемый ряд на медленно меняющийся " +
                     "уровень и шум наблюдения. В отличие от скользящего среднего, " +
                     "вес прошлого подбирается из данных, а не назначается вручную.")
            .FindingIf(smooth,
                $"Отношение сигнал-шум {Fmt.Num(SignalToNoise, 4)} мало: почти вся " +
                "изменчивость ряда — шум измерения, и сглаженный уровень близок к прямой. " +
                "Реагировать на отдельные всплески в таком ряду не стоит.")
            .FindingIf(!smooth,
                $"Отношение сигнал-шум {Fmt.Num(SignalToNoise, 4)} велико: уровень " +
                "действительно меняется, и последние наблюдения несут информацию о нём.")
            .FindingIf(Model == StateSpaceModel.LocalLinearTrend && SlopeVariance < 1e-10,
                "Дисперсия наклона практически нулевая: тренд постоянен, и модель " +
                "вырождается в линейный тренд со случайным уровнем.")
            .FindingIf(whiteNoise,
                "Ошибки прогноза ведут себя как белый шум — модель извлекла из ряда " +
                "всю предсказуемую динамику.")
            .WarningIf(!whiteNoise,
                "В ошибках прогноза осталась автокорреляция. Ряд содержит структуру, " +
                "которую модель не описывает: сезонность, цикл или нелинейность.")
            .WarningIf(Observations < 30,
                $"Всего {Observations} наблюдений. Дисперсии компонент разделяются " +
                "по данным, и на коротком ряду это разделение крайне неустойчиво.")
            .Warning("Оценка дисперсий максимизирует правдоподобие, а оно на коротких " +
                     "рядах часто имеет плоский максимум: близкие по правдоподобию " +
                     "параметры дают заметно разное сглаживание.")
            .Recommendation("Смотрите на сглаженный уровень, а не на отфильтрованный, " +
                            "когда нужен ретроспективный анализ: сглаживание использует " +
                            "всю выборку, фильтрация — только прошлое.")
            .Recommendation("Модель локального уровня — естественная база для сравнения " +
                            "любого прогноза: если сложная модель её не обыгрывает, " +
                            "сложность не нужна.")
            .Build();
    }

    /// <summary>Читаемое название модели.</summary>
    private string ModelName() => Model == StateSpaceModel.LocalLevel
        ? "локальный уровень"
        : "локальный линейный тренд";
}

/// <summary>
/// Модели в пространстве состояний и фильтр Калмана.
/// </summary>
/// <remarks>
/// <para>
/// Ряд представляется как ненаблюдаемое состояние плюс шум измерения. Модель
/// локального уровня:
/// </para>
/// <code>
/// y_t   = mu_t + eps_t,      eps ~ N(0, sigma_eps^2)
/// mu_t  = mu_{t-1} + eta_t,  eta ~ N(0, sigma_eta^2)
/// </code>
/// <para>
/// Модель локального линейного тренда добавляет меняющийся наклон. Фильтр
/// Калмана рекуррентно обновляет оценку состояния и её дисперсию, а побочным
/// продуктом даёт логарифм правдоподобия через ошибки прогноза на один шаг —
/// по нему и оцениваются дисперсии.
/// </para>
/// <para>
/// Отношение дисперсии состояния к дисперсии наблюдения определяет всё
/// поведение модели. Близкое к нулю означает, что уровень почти постоянен и
/// колебания ряда — шум; большое означает, что уровень подвижен и последнее
/// наблюдение важнее истории. Экспоненциальное сглаживание получается как
/// частный случай этой модели, но с весом, назначенным вручную, а не
/// оценённым из данных.
/// </para>
/// </remarks>
public static class StateSpace
{
    /// <summary>Оценивает модель и строит прогноз.</summary>
    /// <param name="series">Временной ряд.</param>
    /// <param name="model">Спецификация модели.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <returns>Сглаженные компоненты, прогноз и диагностика.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно.</exception>
    public static StateSpaceResult Fit(
        Vector series, StateSpaceModel model = StateSpaceModel.LocalLevel, int horizon = 12)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count < 10) throw new ArgumentException("Нужно минимум десять наблюдений.", nameof(series));

        int n = series.Count;
        double scale = Variance(series);

        bool trend = model == StateSpaceModel.LocalLinearTrend;
        int parameters = trend ? 3 : 2;

        double Negative(double[] p)
        {
            double observation = Math.Exp(Math.Clamp(p[0], -30, 20)) * scale;
            double level = Math.Exp(Math.Clamp(p[1], -30, 20)) * scale;
            double slope = trend ? Math.Exp(Math.Clamp(p[2], -40, 20)) * scale : 0;

            return -Filter(series, observation, level, slope, trend, out _, out _, out _, out _);
        }

        double[] start = trend ? [Math.Log(0.5), Math.Log(0.05), Math.Log(0.005)] : [Math.Log(0.5), Math.Log(0.05)];
        double[] estimate = NelderMead.Minimize(Negative, start, 3000);

        double observationVariance = Math.Exp(Math.Clamp(estimate[0], -30, 20)) * scale;
        double levelVariance = Math.Exp(Math.Clamp(estimate[1], -30, 20)) * scale;
        double slopeVariance = trend ? Math.Exp(Math.Clamp(estimate[2], -40, 20)) * scale : 0;

        double logLikelihood = Filter(
            series, observationVariance, levelVariance, slopeVariance, trend,
            out double[] filteredLevel, out double[] smoothedLevel,
            out double[] smoothedSlope, out double[] innovations);

        var forecast = new Vector(Math.Max(1, horizon));
        var lower = new Vector(forecast.Count);
        var upper = new Vector(forecast.Count);

        double lastLevel = smoothedLevel[n - 1];
        double lastSlope = trend ? smoothedSlope[n - 1] : 0;

        for (int h = 0; h < forecast.Count; h++)
        {
            forecast[h] = lastLevel + (lastSlope * (h + 1));

            // Дисперсия прогноза растёт с горизонтом: накапливается шум состояния
            double variance = observationVariance + ((h + 1) * levelVariance)
                + (trend ? slopeVariance * (h + 1) * (h + 2) * ((2 * h) + 3) / 6.0 : 0);

            double band = 1.96 * Math.Sqrt(Math.Max(variance, 0));
            lower[h] = forecast[h] - band;
            upper[h] = forecast[h] + band;
        }

        double ljung = LjungBox(innovations, Math.Min(10, n / 5), out double ljungP);

        return new StateSpaceResult
        {
            Model = model,
            Level = LinearRegression.ToVector(smoothedLevel),
            Slope = LinearRegression.ToVector(smoothedSlope),
            FilteredLevel = LinearRegression.ToVector(filteredLevel),
            Innovations = LinearRegression.ToVector(innovations),
            Forecast = forecast,
            ForecastLower = lower,
            ForecastUpper = upper,
            ObservationVariance = observationVariance,
            LevelVariance = levelVariance,
            SlopeVariance = slopeVariance,
            LogLikelihood = logLikelihood,
            Aic = (-2 * logLikelihood) + (2 * parameters),
            Observations = n,
            LjungBox = ljung,
            LjungBoxPValue = ljungP,
        };
    }

    /// <summary>
    /// Фильтр и сглаживатель Калмана для модели уровня и наклона.
    /// </summary>
    /// <remarks>
    /// Реализация ведёт двумерное состояние даже для модели локального уровня:
    /// дисперсия наклона в ней равна нулю, и второй компонент остаётся
    /// тождественным нулю. Это заметно упрощает код без потери точности.
    /// </remarks>
    private static double Filter(
        Vector series, double observationVariance, double levelVariance, double slopeVariance,
        bool trend, out double[] filteredLevel, out double[] smoothedLevel,
        out double[] smoothedSlope, out double[] innovations)
    {
        int n = series.Count;

        filteredLevel = new double[n];
        smoothedLevel = new double[n];
        smoothedSlope = new double[n];
        innovations = new double[n];

        // Априорное состояние и его ковариация на каждом шаге: нужны при сглаживании
        var priorLevel = new double[n];
        var priorSlope = new double[n];
        var priorP = new double[n][];
        var gain = new double[n][];
        var forecastVariance = new double[n];
        var rawInnovation = new double[n];

        double a0 = series[0], a1 = 0;

        // Диффузная инициализация: начальная неопределённость велика
        double diffuse = 1e6 * Math.Max(observationVariance, 1e-6);
        double p00 = diffuse, p01 = 0, p11 = trend ? diffuse : 0;

        double logLikelihood = 0;
        int skip = trend ? 2 : 1;

        for (int t = 0; t < n; t++)
        {
            priorLevel[t] = a0;
            priorSlope[t] = a1;
            priorP[t] = [p00, p01, p11];

            double f = p00 + observationVariance;
            if (f <= 0 || !double.IsFinite(f)) return double.NegativeInfinity;

            double v = series[t] - a0;
            rawInnovation[t] = v;
            forecastVariance[t] = f;
            innovations[t] = v / Math.Sqrt(f);

            // Усиление по Дурбину — Купману включает матрицу перехода
            double k0 = (p00 + p01) / f;
            double k1 = trend ? p01 / f : 0;
            gain[t] = [k0, k1];

            filteredLevel[t] = a0 + (p00 / f * v);

            // Прогноз состояния на следующий шаг
            a0 = a0 + a1 + (k0 * v);
            a1 = trend ? a1 + (k1 * v) : 0;

            double t00 = p00 + (2 * p01) + p11;
            double t01 = p01 + p11;
            double t11 = p11;

            p00 = t00 + levelVariance - (f * k0 * k0);
            p01 = trend ? t01 - (f * k0 * k1) : 0;
            p11 = trend ? t11 + slopeVariance - (f * k1 * k1) : 0;

            if (t >= skip)
                logLikelihood += -0.5 * (Math.Log(2 * Math.PI) + Math.Log(f) + (v * v / f));
        }

        // Обратный ход: r_{t-1} = Z' v_t / F_t + L_t' r_t, L_t = T - K_t Z
        double r0 = 0, r1 = 0;

        for (int t = n - 1; t >= 0; t--)
        {
            double f = forecastVariance[t];
            double k0 = gain[t][0], k1 = gain[t][1];

            double newR0 = (rawInnovation[t] / f) + ((1 - k0) * r0) - (k1 * r1);
            double newR1 = trend ? r0 + r1 : 0;

            r0 = newR0;
            r1 = newR1;

            smoothedLevel[t] = priorLevel[t] + (priorP[t][0] * r0) + (priorP[t][1] * r1);
            smoothedSlope[t] = trend
                ? priorSlope[t] + (priorP[t][1] * r0) + (priorP[t][2] * r1)
                : 0;
        }

        return double.IsFinite(logLikelihood) ? logLikelihood : double.NegativeInfinity;
    }

    /// <summary>Статистика Люнга — Бокса на автокорреляцию ошибок прогноза.</summary>
    private static double LjungBox(double[] innovations, int lags, out double pValue)
    {
        int n = innovations.Length;
        if (lags < 1 || n <= lags + 2) { pValue = 1; return 0; }

        double mean = innovations.Average();
        double variance = innovations.Sum(v => (v - mean) * (v - mean));

        if (variance <= 0) { pValue = 1; return 0; }

        double statistic = 0;

        for (int l = 1; l <= lags; l++)
        {
            double covariance = 0;
            for (int t = l; t < n; t++) covariance += (innovations[t] - mean) * (innovations[t - l] - mean);

            double rho = covariance / variance;
            statistic += rho * rho / (n - l);
        }

        statistic *= n * (n + 2.0);
        pValue = Distributions.ChiSquarePValue(statistic, lags);

        return statistic;
    }

    /// <summary>Выборочная дисперсия ряда.</summary>
    private static double Variance(Vector series)
    {
        double mean = series.Average();
        double sum = 0;

        for (int i = 0; i < series.Count; i++) sum += (series[i] - mean) * (series[i] - mean);

        return Math.Max(sum / Math.Max(1, series.Count - 1), 1e-12);
    }
}
