using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Kinetics;

/// <summary>
/// Экспериментальные данные для подгонки: начальные концентрации и измерения во времени
/// </summary>
public sealed class KineticDataset
{
    /// <summary>Моменты времени измерений</summary>
    public IReadOnlyList<double> Times { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Измеренные концентрации: вещество -> значения в те же моменты времени.
    /// Достаточно одного вещества, остальные восстанавливаются моделью.
    /// </summary>
    public IReadOnlyDictionary<string, double[]> Measurements { get; init; } = new Dictionary<string, double[]>();

    /// <summary>Начальные концентрации в порядке веществ схемы</summary>
    public IReadOnlyList<double> Initial { get; init; } = Array.Empty<double>();

    /// <summary>Число измеренных точек по всем веществам</summary>
    public int PointCount => Measurements.Sum(m => m.Value.Length);

    /// <summary>Проверяет согласованность данных со схемой</summary>
    /// <param name="scheme">Кинетическая схема</param>
    public void Validate(KineticScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        if (Times.Count < 3)
            throw new ArgumentException("At least three time points are required");

        if (Initial.Count != scheme.Species.Count)
            throw new ArgumentException($"Expected {scheme.Species.Count} initial concentrations, got {Initial.Count}");

        if (Measurements.Count == 0)
            throw new ArgumentException("At least one measured species is required");

        foreach (var measurement in Measurements)
        {
            if (scheme.IndexOf(measurement.Key) < 0)
                throw new ArgumentException($"Species '{measurement.Key}' is not part of the scheme");

            if (measurement.Value.Length != Times.Count)
                throw new ArgumentException($"Series '{measurement.Key}' has {measurement.Value.Length} values for {Times.Count} time points");
        }
    }
}

/// <summary>
/// Результат подгонки констант скорости
/// </summary>
public sealed class KineticFitResult
{
    /// <summary>Схема, по которой шла подгонка</summary>
    public KineticScheme Scheme { get; init; }

    /// <summary>Найденные константы скорости</summary>
    public double[] RateConstants { get; init; }

    /// <summary>Стандартные ошибки констант</summary>
    public double[] StandardErrors { get; init; }

    /// <summary>Доверительные интервалы констант</summary>
    public (double Lower, double Upper)[] Intervals { get; init; }

    /// <summary>Сумма квадратов остатков</summary>
    public double ResidualSumOfSquares { get; init; }

    /// <summary>СКО остатка, в единицах концентрации</summary>
    public double ResidualStd { get; init; }

    /// <summary>Доля объяснённой дисперсии</summary>
    public double R2 { get; init; }

    /// <summary>Сошёлся ли алгоритм</summary>
    public bool Converged { get; init; }

    /// <summary>Число итераций</summary>
    public int Iterations { get; init; }

    /// <summary>Доверительная вероятность интервалов</summary>
    public double Confidence { get; init; }

    /// <summary>Относительная погрешность константы, %</summary>
    public double RelativeErrorPercent(int index)
        => RateConstants[index] == 0 ? double.NaN : 100.0 * StandardErrors[index] / RateConstants[index];

    /// <summary>Отчёт по подгонке</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Подгонка констант скорости");
        text.Append(Scheme);
        text.AppendLine();

        for (int i = 0; i < RateConstants.Length; i++)
        {
            text.AppendLine(string.Format(culture,
                "  k{0} = {1:G4} +- {2:G3} ({3:F1}%), интервал [{4:G4}; {5:G4}]",
                i + 1, RateConstants[i], StandardErrors[i], RelativeErrorPercent(i),
                Intervals[i].Lower, Intervals[i].Upper));
        }

        text.AppendLine(string.Format(culture, "  R2 = {0:F4}, СКО остатка = {1:G3}", R2, ResidualStd));
        text.AppendLine($"  Итераций: {Iterations}, сходимость: {(Converged ? "достигнута" : "по лимиту итераций")}");

        return text.ToString();
    }
}

/// <summary>
/// Определение порядка реакции перебором
/// </summary>
/// <param name="Order">Порядок с наименьшей суммой квадратов</param>
/// <param name="RateConstant">Константа скорости при этом порядке</param>
/// <param name="ResidualSumOfSquares">Сумма квадратов остатков</param>
/// <param name="R2">Коэффициент детерминации</param>
/// <param name="Candidates">Все проверенные варианты</param>
public readonly record struct ReactionOrderResult(
    double Order,
    double RateConstant,
    double ResidualSumOfSquares,
    double R2,
    IReadOnlyList<(double Order, double RateConstant, double ResidualSumOfSquares)> Candidates);

/// <summary>
/// Подгонка кинетических параметров по временным рядам концентраций
/// </summary>
/// <remarks>
/// Константы ищутся в логарифмическом масштабе: они положительны и часто различаются
/// на порядки, а линейный шаг оптимизатора в таком случае либо уводит в отрицательные
/// значения, либо не двигает малые константы вовсе. Доверительные интервалы
/// пересчитываются обратно в исходный масштаб и потому несимметричны.
/// </remarks>
public static class KineticFit
{
    /// <summary>
    /// Подгоняет константы скорости схемы под измерения
    /// </summary>
    /// <param name="scheme">Кинетическая схема</param>
    /// <param name="data">Экспериментальные данные</param>
    /// <param name="initialGuess">Начальные значения констант; null - оценка по масштабу данных</param>
    /// <param name="options">Настройки оптимизации</param>
    public static KineticFitResult Fit(
        KineticScheme scheme,
        KineticDataset data,
        double[] initialGuess = null,
        NonlinearFitOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(data);

        data.Validate(scheme);
        options ??= new NonlinearFitOptions();

        int constants = scheme.RateConstantCount;
        double[] start = initialGuess ?? DefaultGuess(scheme, data);

        if (start.Length != constants)
            throw new ArgumentException($"Scheme needs {constants} rate constant(s)", nameof(initialGuess));

        var logStart = start.Select(k => Math.Log10(Math.Max(k, 1e-12))).ToArray();
        double observedVariance = ObservedVariance(data);

        NonlinearFitResult fit = NonlinearFit.Fit(
            logParameters => Residuals(scheme, data, logParameters),
            logStart,
            observedVariance,
            options);

        var rateConstants = new double[constants];
        var errors = new double[constants];
        var intervals = new (double, double)[constants];

        for (int i = 0; i < constants; i++)
        {
            rateConstants[i] = Math.Pow(10, fit.Parameters[i]);

            // Дельта-метод: k = 10^p, значит sigma_k = k · ln(10) · sigma_p
            errors[i] = rateConstants[i] * Math.Log(10) * fit.StandardErrors[i];
            intervals[i] = (Math.Pow(10, fit.Intervals[i].Lower), Math.Pow(10, fit.Intervals[i].Upper));
        }

        return new KineticFitResult
        {
            Scheme = scheme,
            RateConstants = rateConstants,
            StandardErrors = errors,
            Intervals = intervals,
            ResidualSumOfSquares = fit.ResidualSumOfSquares,
            ResidualStd = fit.ResidualStd,
            R2 = fit.R2,
            Converged = fit.Converged,
            Iterations = fit.Iterations,
            Confidence = fit.Confidence
        };
    }

    /// <summary>
    /// Определяет порядок простой реакции A -> B перебором и подгоняет константу
    /// </summary>
    /// <param name="times">Моменты времени</param>
    /// <param name="concentrations">Концентрации реагента</param>
    /// <param name="orders">Проверяемые порядки; null - от 0 до 3 с шагом 0.5</param>
    /// <param name="options">Настройки оптимизации</param>
    public static ReactionOrderResult DetermineOrder(
        IReadOnlyList<double> times,
        IReadOnlyList<double> concentrations,
        IReadOnlyList<double> orders = null,
        NonlinearFitOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(concentrations);

        if (times.Count != concentrations.Count)
            throw new ArgumentException("Times and concentrations must have the same length");

        orders ??= new[] { 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };

        var candidates = new List<(double, double, double)>();
        double bestRss = double.PositiveInfinity;
        double bestOrder = 0, bestK = 0, bestR2 = double.NaN;

        foreach (double order in orders)
        {
            var scheme = KineticScheme.Simple(order);
            var data = new KineticDataset
            {
                Times = times,
                Initial = new[] { concentrations[0], 0.0 },
                Measurements = new Dictionary<string, double[]> { ["A"] = concentrations.ToArray() }
            };

            KineticFitResult fit = Fit(scheme, data, null, options);
            candidates.Add((order, fit.RateConstants[0], fit.ResidualSumOfSquares));

            if (fit.ResidualSumOfSquares < bestRss)
            {
                bestRss = fit.ResidualSumOfSquares;
                bestOrder = order;
                bestK = fit.RateConstants[0];
                bestR2 = fit.R2;
            }
        }

        return new ReactionOrderResult(bestOrder, bestK, bestRss, bestR2, candidates);
    }

    private static double[] Residuals(KineticScheme scheme, KineticDataset data, double[] logParameters)
    {
        var rateConstants = logParameters.Select(p => Math.Pow(10, p)).ToArray();
        var simulated = scheme.Simulate(data.Initial, rateConstants, data.Times);

        var residuals = new double[data.PointCount];
        int position = 0;

        foreach (var measurement in data.Measurements)
        {
            int index = scheme.IndexOf(measurement.Key);

            for (int i = 0; i < measurement.Value.Length; i++)
            {
                double model = simulated[i][index];
                residuals[position++] = double.IsFinite(model)
                    ? model - measurement.Value[i]
                    : 1e6; // расходящаяся траектория штрафуется, а не роняет подгонку
            }
        }

        return residuals;
    }

    // Оценка масштаба константы: характерное время процесса - это время наблюдения
    private static double[] DefaultGuess(KineticScheme scheme, KineticDataset data)
    {
        double span = data.Times[^1] - data.Times[0];
        double guess = span > 0 ? 1.0 / span : 1.0;

        return Enumerable.Repeat(guess, scheme.RateConstantCount).ToArray();
    }

    private static double ObservedVariance(KineticDataset data)
    {
        var values = data.Measurements.SelectMany(m => m.Value).ToArray();

        if (values.Length < 2)
            return 0;

        double mean = values.Average();

        return values.Sum(v => (v - mean) * (v - mean));
    }
}
