using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Econometrics;

/// <summary>Спецификация модели условной дисперсии.</summary>
public enum GarchModel
{
    /// <summary>Симметричная модель GARCH(1,1).</summary>
    Garch,

    /// <summary>GJR-GARCH: отрицательные шоки влияют на волатильность сильнее.</summary>
    GjrGarch,

    /// <summary>Экспоненциальная модель: положительность дисперсии обеспечена по построению.</summary>
    Egarch,
}

/// <summary>Результат оценивания модели условной дисперсии.</summary>
public sealed record GarchResult : IInterpretable
{
    /// <summary>Спецификация модели.</summary>
    public GarchModel Model { get; init; }

    /// <summary>Постоянная составляющая уравнения дисперсии.</summary>
    public double Omega { get; init; }

    /// <summary>Коэффициент при квадрате прошлого шока.</summary>
    public double Alpha { get; init; }

    /// <summary>Коэффициент при прошлой дисперсии.</summary>
    public double Beta { get; init; }

    /// <summary>Коэффициент асимметрии реакции на отрицательные шоки.</summary>
    public double Gamma { get; init; }

    /// <summary>Среднее доходности.</summary>
    public double Mean { get; init; }

    /// <summary>Ряд условной волатильности.</summary>
    public Vector ConditionalVolatility { get; init; } = new(0);

    /// <summary>Стандартизованные остатки.</summary>
    public Vector StandardizedResiduals { get; init; } = new(0);

    /// <summary>Прогноз волатильности на горизонт.</summary>
    public Vector Forecast { get; init; } = new(0);

    /// <summary>Логарифм правдоподобия.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Информационный критерий Акаике.</summary>
    public double Aic { get; init; }

    /// <summary>Информационный критерий Шварца.</summary>
    public double Bic { get; init; }

    /// <summary>Инерция волатильности.</summary>
    public double Persistence { get; init; }

    /// <summary>Долгосрочная волатильность.</summary>
    public double LongRunVolatility { get; init; }

    /// <summary>Статистика теста ARCH-LM на остаточную гетероскедастичность.</summary>
    public double ArchStatistic { get; init; }

    /// <summary>Уровень значимости теста ARCH-LM.</summary>
    public double ArchPValue { get; init; } = 1;

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Период полураспада шока волатильности.</summary>
    public double HalfLife =>
        Persistence is > 0 and < 1 ? Math.Log(0.5) / Math.Log(Persistence) : double.PositiveInfinity;

    /// <summary>Стационарна ли условная дисперсия.</summary>
    public bool IsStationary => Persistence < 1;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool leverage = Model != GarchModel.Garch && Math.Abs(Gamma) > 1e-6;
        bool cleanResiduals = ArchPValue >= 0.05;
        double currentVolatility = ConditionalVolatility.Count > 0 ? ConditionalVolatility[^1] : 0;

        var builder = new InterpretationBuilder($"Условная волатильность: {ModelName()}")
            .Summary($"Оценено по {Observations} наблюдениям. Инерция волатильности " +
                     $"{Fmt.Num(Persistence, 4)}, период полураспада шока " +
                     $"{(double.IsFinite(HalfLife) ? Fmt.Num(HalfLife, 1) : "бесконечен")} периодов. " +
                     $"Долгосрочная волатильность {Fmt.Pct(LongRunVolatility, 2)}, текущая " +
                     $"{Fmt.Pct(currentVolatility, 2)}. Остаточная гетероскедастичность: " +
                     $"p = {Fmt.Num(ArchPValue, 4)}.")
            .Metric("Инерция", Persistence, null,
                IsStationary ? "дисперсия стационарна" : "дисперсия не возвращается к среднему",
                IsStationary ? MetricQuality.Good : MetricQuality.Critical, 4)
            .Metric("Период полураспада", double.IsFinite(HalfLife) ? HalfLife : 0, "периодов",
                "за сколько шаг волатильности возвращается к норме наполовину",
                MetricQuality.Neutral, 1)
            .Metric("Долгосрочная волатильность", LongRunVolatility, null,
                "уровень, к которому возвращается дисперсия", MetricQuality.Neutral, 5)
            .Metric("Текущая волатильность", currentVolatility, null,
                currentVolatility > LongRunVolatility ? "выше долгосрочного уровня" : "ниже долгосрочного уровня",
                MetricQuality.Neutral, 5)
            .Metric("Реакция на шок", Alpha, null, "вес квадрата вчерашнего шока",
                MetricQuality.Neutral, 4)
            .Metric("Память", Beta, null, "вес вчерашней дисперсии", MetricQuality.Neutral, 4)
            .Metric("ARCH-LM на остатках", ArchStatistic, null,
                $"p = {Fmt.Num(ArchPValue, 4)}; " +
                (cleanResiduals ? "остаточной гетероскедастичности нет" : "модель не описала всю динамику"),
                cleanResiduals ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("AIC", Aic, null, $"BIC {Fmt.Num(Bic, 1)}", MetricQuality.Neutral, 1);

        if (Model != GarchModel.Garch)
        {
            builder.Metric("Асимметрия", Gamma, null,
                Gamma > 0
                    ? "падения повышают волатильность сильнее, чем рост той же величины"
                    : "асимметрия не обнаружена",
                MetricQuality.Neutral, 4);
        }

        return builder
            .Finding($"Инерция {Fmt.Num(Persistence, 4)} означает, что всплеск волатильности " +
                     "затухает медленно: спокойные и бурные периоды группируются. " +
                     "Именно этот эффект и делает модель нужной — постоянная дисперсия " +
                     "занижает риск в кризис и завышает в спокойное время.")
            .FindingIf(leverage && Gamma > 0,
                $"Коэффициент асимметрии {Fmt.Num(Gamma, 4)} положителен: отрицательные шоки " +
                "поднимают волатильность сильнее положительных той же величины. " +
                "Это устойчивый эффект на рынках акций.")
            .FindingIf(currentVolatility > LongRunVolatility * 1.2,
                $"Текущая волатильность {Fmt.Pct(currentVolatility, 2)} заметно выше " +
                $"долгосрочной {Fmt.Pct(LongRunVolatility, 2)}: модель прогнозирует " +
                "постепенное снижение к норме.")
            .FindingIf(cleanResiduals,
                "Тест на остаточную гетероскедастичность не отвергает нулевую гипотезу: " +
                "модель описала кластеризацию волатильности.")
            .WarningIf(!IsStationary,
                $"Сумма коэффициентов {Fmt.Num(Persistence, 4)} не меньше единицы. " +
                "Безусловная дисперсия не существует, долгосрочный уровень не определён, " +
                "и прогноз волатильности расходится с горизонтом.")
            .WarningIf(!cleanResiduals,
                "В стандартизованных остатках осталась гетероскедастичность. " +
                "Попробуйте асимметричную спецификацию или добавьте лаг.")
            .WarningIf(Observations < 250,
                $"Всего {Observations} наблюдений. Параметры условной дисперсии оцениваются " +
                "неустойчиво на выборках короче года дневных данных.")
            .Warning("Модель предполагает нормальность стандартизованных остатков. " +
                     "На финансовых данных их хвосты тяжелее нормальных, поэтому оценка " +
                     "квантилей риска по этой модели занижает вероятность экстремальных " +
                     "потерь — используйте эмпирические квантили остатков.")
            .Recommendation("Прогноз волатильности подставляйте в расчёт стоимости под риском: " +
                            "именно там условная дисперсия даёт основной практический выигрыш " +
                            "перед постоянной.")
            .Recommendation("Сравнивайте симметричную и асимметричную спецификации по " +
                            "информационному критерию: асимметрия нужна не всегда, " +
                            "а лишние параметры ухудшают прогноз.")
            .Build();
    }

    /// <summary>Читаемое название спецификации.</summary>
    private string ModelName() => Model switch
    {
        GarchModel.Garch => "GARCH(1,1)",
        GarchModel.GjrGarch => "GJR-GARCH(1,1)",
        _ => "EGARCH(1,1)",
    };
}

/// <summary>
/// Модели условной гетероскедастичности семейства GARCH.
/// </summary>
/// <remarks>
/// <para>
/// Доходности финансовых активов почти некоррелированы, но их квадраты — нет:
/// спокойные и бурные периоды группируются. Модель описывает дисперсию как
/// функцию прошлых шоков и прошлой дисперсии:
/// </para>
/// <code>
/// GARCH:  h_t = omega + alpha * e_{t-1}^2 + beta * h_{t-1}
/// GJR:    h_t = omega + (alpha + gamma * 1{e_{t-1} &lt; 0}) * e_{t-1}^2 + beta * h_{t-1}
/// EGARCH: ln h_t = omega + alpha * (|z_{t-1}| - E|z|) + gamma * z_{t-1} + beta * ln h_{t-1}
/// </code>
/// <para>
/// Сумма <c>alpha + beta</c> — инерция волатильности. На дневных данных она
/// обычно около 0,95-0,99: шок затухает неделями. Долгосрочная дисперсия равна
/// <c>omega / (1 - alpha - beta)</c> и существует только при инерции меньше
/// единицы.
/// </para>
/// <para>
/// Асимметричные спецификации добавляют эффект рычага: падение повышает
/// волатильность сильнее, чем рост той же величины. Оценивание ведётся
/// максимизацией правдоподобия при ограничениях, заданных через
/// параметризацию — так положительность дисперсии и стационарность
/// выполняются по построению.
/// </para>
/// </remarks>
public static class Garch
{
    /// <summary>Оценивает модель условной дисперсии.</summary>
    /// <param name="returns">Ряд доходностей.</param>
    /// <param name="model">Спецификация модели.</param>
    /// <param name="horizon">Горизонт прогноза волатильности.</param>
    /// <returns>Параметры, ряд условной волатильности и прогноз.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно.</exception>
    public static GarchResult Fit(Vector returns, GarchModel model = GarchModel.Garch, int horizon = 20)
    {
        ArgumentNullException.ThrowIfNull(returns);
        if (returns.Count < 50) throw new ArgumentException("Нужно минимум пятьдесят наблюдений.", nameof(returns));

        int n = returns.Count;
        double mean = returns.Average();
        var shocks = new double[n];
        for (int i = 0; i < n; i++) shocks[i] = returns[i] - mean;

        double variance = shocks.Sum(e => e * e) / n;

        double Negative(double[] p) => -Likelihood(shocks, variance, p, model, out _);

        double[] start = model switch
        {
            GarchModel.Garch => [-2.0, 1.5],
            GarchModel.GjrGarch => [-2.5, -2.5, 1.5],
            _ => [0.15, -0.05, 2.0],
        };

        double[] estimate = NelderMead.Minimize(Negative, start, 6000);
        double logLikelihood = Likelihood(shocks, variance, estimate, model, out double[] h);

        (double omega, double alpha, double beta, double gamma) = Unpack(estimate, model, variance);

        var volatility = new Vector(n);
        var standardized = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            volatility[i] = Math.Sqrt(Math.Max(h[i], 1e-300));
            standardized[i] = shocks[i] / Math.Max(volatility[i], 1e-150);
        }

        double persistence = model switch
        {
            GarchModel.Garch => alpha + beta,
            GarchModel.GjrGarch => alpha + (gamma / 2) + beta,
            _ => Math.Abs(beta),
        };

        double longRun = model == GarchModel.Egarch
            ? Math.Sqrt(Math.Exp(Math.Abs(1 - beta) > 1e-9 ? omega / (1 - beta) : Math.Log(variance)))
            : persistence < 1 ? Math.Sqrt(omega / (1 - persistence)) : Math.Sqrt(variance);

        int parameters = model == GarchModel.Garch ? 3 : 4;

        return new GarchResult
        {
            Model = model,
            Omega = omega,
            Alpha = alpha,
            Beta = beta,
            Gamma = gamma,
            Mean = mean,
            ConditionalVolatility = volatility,
            StandardizedResiduals = standardized,
            Forecast = ForecastVolatility(h[^1], shocks[^1], omega, alpha, beta, gamma, model, horizon, variance),
            LogLikelihood = logLikelihood,
            Aic = (-2 * logLikelihood) + (2 * parameters),
            Bic = (-2 * logLikelihood) + (parameters * Math.Log(n)),
            Persistence = persistence,
            LongRunVolatility = longRun,
            ArchStatistic = ArchTest(standardized, 5, out double archP),
            ArchPValue = archP,
            Observations = n,
        };
    }

    /// <summary>Тест ARCH-LM на условную гетероскедастичность ряда.</summary>
    /// <param name="series">Проверяемый ряд, обычно стандартизованные остатки.</param>
    /// <param name="lags">Число лагов.</param>
    /// <param name="pValue">Уровень значимости.</param>
    /// <returns>Статистика теста.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    public static double ArchTest(Vector series, int lags, out double pValue)
    {
        ArgumentNullException.ThrowIfNull(series);

        int n = series.Count;
        int rows = n - lags;

        if (rows <= lags + 2) { pValue = 1; return 0; }

        var design = new double[rows, lags + 1];
        var response = new double[rows];

        for (int i = 0; i < rows; i++)
        {
            design[i, 0] = 1;
            for (int l = 1; l <= lags; l++)
                design[i, l] = series[i + lags - l] * series[i + lags - l];

            response[i] = series[i + lags] * series[i + lags];
        }

        var names = new List<string> { "const" };
        for (int l = 1; l <= lags; l++) names.Add($"лаг {l}");

        RegressionResult fit = LinearRegression.FitDesign(
            design, response, names, new RegressionOptions { AddIntercept = false }, "ARCH-LM");

        double statistic = rows * fit.RSquared;
        pValue = Distributions.ChiSquarePValue(statistic, lags);

        return statistic;
    }

    /// <summary>Логарифм правдоподобия и ряд условной дисперсии.</summary>
    private static double Likelihood(
        double[] shocks, double variance, double[] parameters, GarchModel model, out double[] h)
    {
        int n = shocks.Length;
        h = new double[n];

        (double omega, double alpha, double beta, double gamma) = Unpack(parameters, model, variance);

        double expectedAbsolute = Math.Sqrt(2 / Math.PI);
        double total = 0;
        double previous = variance;

        for (int t = 0; t < n; t++)
        {
            if (t == 0)
            {
                h[t] = variance;
            }
            else if (model == GarchModel.Egarch)
            {
                double z = shocks[t - 1] / Math.Sqrt(Math.Max(previous, 1e-300));
                double logH = omega + (alpha * (Math.Abs(z) - expectedAbsolute)) + (gamma * z)
                    + (beta * Math.Log(Math.Max(previous, 1e-300)));

                h[t] = Math.Exp(Math.Clamp(logH, -50, 50));
            }
            else
            {
                double shock = shocks[t - 1] * shocks[t - 1];
                double asymmetric = model == GarchModel.GjrGarch && shocks[t - 1] < 0 ? gamma : 0;
                h[t] = omega + ((alpha + asymmetric) * shock) + (beta * previous);
            }

            h[t] = Math.Max(h[t], 1e-300);
            previous = h[t];

            total += -0.5 * (Math.Log(2 * Math.PI) + Math.Log(h[t]) + (shocks[t] * shocks[t] / h[t]));
        }

        return double.IsFinite(total) ? total : double.NegativeInfinity;
    }

    /// <summary>
    /// Распаковка параметров из пространства оптимизации.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ограничения на положительность и стационарность заданы самой
    /// параметризацией: <c>alpha</c> и <c>beta</c> строятся так, что их сумма
    /// заведомо меньше единицы. Это избавляет от штрафов и делает поиск устойчивым.
    /// </para>
    /// <para>
    /// Постоянная составляющая не оценивается свободно, а привязывается к
    /// выборочной дисперсии: <c>omega = var * (1 - persistence)</c>. Такое
    /// таргетирование дисперсии убирает из задачи параметр, лежащий на много
    /// порядков ниже остальных, — без него симплексный поиск на доходностях
    /// систематически сползает к единичной инерции.
    /// </para>
    /// </remarks>
    private static (double Omega, double Alpha, double Beta, double Gamma) Unpack(
        double[] parameters, GarchModel model, double variance)
    {
        if (model == GarchModel.Egarch)
        {
            double persistence = Math.Tanh(parameters[2]);

            return (
                Math.Log(Math.Max(variance, 1e-300)) * (1 - persistence),
                parameters[0],
                persistence,
                parameters[1]);
        }

        if (model == GarchModel.GjrGarch)
        {
            double a = Sigmoid(parameters[0]) * 0.5;
            double asymmetry = Sigmoid(parameters[1]) * 0.5;
            double b = (1 - a - (asymmetry / 2)) * Sigmoid(parameters[2]) * 0.999;

            return (variance * Math.Max(1 - a - (asymmetry / 2) - b, 1e-12), a, b, asymmetry);
        }

        double alpha = Sigmoid(parameters[0]) * 0.999;
        double beta = (1 - alpha) * Sigmoid(parameters[1]) * 0.999;

        return (variance * Math.Max(1 - alpha - beta, 1e-12), alpha, beta, 0);
    }

    /// <summary>Логистическая функция.</summary>
    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-Math.Clamp(x, -35, 35)));

    /// <summary>Прогноз волатильности на несколько шагов вперёд.</summary>
    private static Vector ForecastVolatility(
        double lastVariance, double lastShock, double omega, double alpha, double beta,
        double gamma, GarchModel model, int horizon, double unconditional)
    {
        var forecast = new Vector(Math.Max(1, horizon));
        double current = lastVariance;

        for (int h = 0; h < forecast.Count; h++)
        {
            if (model == GarchModel.Egarch)
            {
                double logH = omega + (beta * Math.Log(Math.Max(current, 1e-300)));
                current = Math.Exp(Math.Clamp(logH, -50, 50));
            }
            else if (h == 0)
            {
                double asymmetric = model == GarchModel.GjrGarch && lastShock < 0 ? gamma : 0;
                current = omega + ((alpha + asymmetric) * lastShock * lastShock) + (beta * current);
            }
            else
            {
                double persistence = model == GarchModel.GjrGarch ? alpha + (gamma / 2) + beta : alpha + beta;
                current = omega + (persistence * current);
            }

            forecast[h] = Math.Sqrt(Math.Max(double.IsFinite(current) ? current : unconditional, 1e-300));
        }

        return forecast;
    }
}
