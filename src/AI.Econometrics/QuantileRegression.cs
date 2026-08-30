using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;
using AI.Statistics;

namespace AI.Econometrics;

/// <summary>Результат квантильной регрессии для одного квантиля.</summary>
public sealed record QuantileRegressionResult : IInterpretable
{
    /// <summary>Оцениваемый квантиль.</summary>
    public double Quantile { get; init; } = 0.5;

    /// <summary>Оценки коэффициентов.</summary>
    public IReadOnlyList<Coefficient> Coefficients { get; init; } = [];

    /// <summary>Значение минимизируемой асимметричной функции потерь.</summary>
    public double Objective { get; init; }

    /// <summary>Псевдо-R² Кёнкера — Мачадо.</summary>
    public double PseudoRSquared { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Число бутстрап-повторов для стандартных ошибок.</summary>
    public int BootstrapSamples { get; init; }

    /// <summary>Остатки.</summary>
    public Vector Residuals { get; init; } = new(0);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        Coefficient? strongest = Coefficients
            .Where(c => c.Name != "const")
            .OrderByDescending(c => Math.Abs(c.TStatistic))
            .FirstOrDefault();

        string position = Quantile < 0.25 ? "нижнем хвосте"
            : Quantile > 0.75 ? "верхнем хвосте" : "центре распределения";

        var builder = new InterpretationBuilder($"Квантильная регрессия: тау = {Fmt.Num(Quantile, 2)}")
            .Summary($"Оценено по {Observations} наблюдениям для квантиля " +
                     $"{Fmt.Num(Quantile, 2)} — модель описывает связь в {position}. " +
                     $"Псевдо-R² {Fmt.Num(PseudoRSquared, 3)}, стандартные ошибки получены " +
                     $"бутстрапом по {BootstrapSamples} повторам.")
            .Metric("Квантиль", Quantile, null, $"условный {Fmt.Pct(Quantile, 0)} отклика",
                MetricQuality.Neutral, 2)
            .Metric("Псевдо-R²", PseudoRSquared, null,
                "доля снижения асимметричной функции потерь",
                PseudoRSquared > 0.2 ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Функция потерь", Objective, null,
                "минимизированная сумма асимметричных отклонений", MetricQuality.Neutral, 2);

        foreach (Coefficient coefficient in Coefficients)
        {
            builder.Metric(coefficient.Name, coefficient.Estimate, null,
                $"ст. ошибка {Fmt.Num(coefficient.StandardError, 4)}, p = {Fmt.Num(coefficient.PValue, 4)} " +
                coefficient.Stars,
                coefficient.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        return builder
            .FindingIf(strongest is not null,
                $"Сильнее всего влияет «{strongest?.Name}»: изменение регрессора на единицу " +
                $"сдвигает условный квантиль {Fmt.Num(Quantile, 2)} на " +
                $"{Fmt.Num(strongest?.Estimate ?? 0, 4)}.")
            .Finding("Квантильная регрессия отвечает на вопрос, который не решает МНК: " +
                     "как фактор влияет не на среднее, а на конкретную часть распределения. " +
                     "Одинаковый средний эффект может складываться из сильного влияния " +
                     "на слабых и нулевого на сильных.")
            .Finding("Оценка устойчива к выбросам в отклике: медианная регрессия минимизирует " +
                     "сумму модулей, а не квадратов, и одно аномальное наблюдение не тянет " +
                     "линию на себя.")
            .WarningIf(BootstrapSamples < 200,
                $"Бутстрап выполнен по {BootstrapSamples} повторам. Для устойчивых " +
                "стандартных ошибок обычно требуется не менее двухсот.")
            .WarningIf(Quantile is < 0.1 or > 0.9,
                $"Квантиль {Fmt.Num(Quantile, 2)} лежит в хвосте: в этой области " +
                "наблюдений мало, и оценки заметно менее устойчивы, чем в центре.")
            .Warning("Квантильные регрессии для разных тау оцениваются независимо, поэтому " +
                     "их линии в принципе могут пересекаться — это артефакт конечной " +
                     "выборки, а не свойство данных.")
            .Recommendation("Оценивайте набор квантилей сразу и смотрите на форму " +
                            "коэффициента как функции тау: она и есть содержательный результат.")
            .Recommendation("Сравнивайте с МНК: совпадение коэффициентов по всем квантилям " +
                            "означает, что фактор сдвигает распределение целиком, " +
                            "а расхождение — что он меняет его форму.")
            .Build();
    }
}

/// <summary>Набор квантильных регрессий по нескольким уровням.</summary>
public sealed record QuantileProcessResult : IInterpretable
{
    /// <summary>Регрессии по возрастанию квантиля.</summary>
    public IReadOnlyList<QuantileRegressionResult> Quantiles { get; init; } = [];

    /// <summary>Оценка тех же коэффициентов методом наименьших квадратов.</summary>
    public IReadOnlyList<Coefficient> LeastSquares { get; init; } = [];

    /// <summary>Названия регрессоров без свободного члена.</summary>
    public IReadOnlyList<string> Variables { get; init; } = [];

    /// <summary>Траектория коэффициента по квантилям.</summary>
    /// <param name="variable">Название регрессора.</param>
    /// <returns>Оценки коэффициента для каждого квантиля.</returns>
    public Vector Path(string variable)
    {
        var path = new Vector(Quantiles.Count);

        for (int i = 0; i < Quantiles.Count; i++)
        {
            path[i] = Quantiles[i].Coefficients
                .FirstOrDefault(c => string.Equals(c.Name, variable, StringComparison.Ordinal))?.Estimate ?? 0;
        }

        return path;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var spreads = new List<(string Variable, double Spread, double Low, double High)>();

        foreach (string variable in Variables)
        {
            Vector path = Path(variable);
            if (path.Count == 0) continue;

            spreads.Add((variable, path.Max() - path.Min(), path[0], path[^1]));
        }

        (string Variable, double Spread, double Low, double High) widest =
            spreads.OrderByDescending(s => Math.Abs(s.Spread)).FirstOrDefault();

        bool heterogeneous = Math.Abs(widest.Spread) > 1e-9;

        var builder = new InterpretationBuilder("Квантильный процесс")
            .Summary($"Оценено {Quantiles.Count} квантильных регрессий по " +
                     $"{Quantiles.FirstOrDefault()?.Observations ?? 0} наблюдениям. " +
                     (heterogeneous
                         ? $"Наибольшая неоднородность у «{widest.Variable}»: коэффициент меняется " +
                           $"от {Fmt.Num(widest.Low, 4)} в нижнем квантиле до {Fmt.Num(widest.High, 4)} в верхнем."
                         : "Коэффициенты почти не меняются по квантилям."))
            .Metric("Квантилей", Quantiles.Count, null,
                $"от {Fmt.Num(Quantiles.FirstOrDefault()?.Quantile ?? 0, 2)} до " +
                $"{Fmt.Num(Quantiles.LastOrDefault()?.Quantile ?? 1, 2)}",
                MetricQuality.Neutral, 0);

        foreach ((string variable, double spread, double low, double high) in spreads)
        {
            Coefficient? ols = LeastSquares.FirstOrDefault(c => c.Name == variable);

            builder.Metric($"Размах: {variable}", spread, null,
                $"от {Fmt.Num(low, 4)} до {Fmt.Num(high, 4)}" +
                (ols is not null ? $", МНК даёт {Fmt.Num(ols.Estimate, 4)}" : ""),
                MetricQuality.Unknown, 4);
        }

        return builder
            .FindingIf(heterogeneous,
                $"Эффект «{widest.Variable}» неоднороден по распределению отклика. " +
                "Средний эффект из МНК в этом случае скрывает содержательную картину: " +
                "он усредняет разное влияние на слабых и сильных.")
            .FindingIf(!heterogeneous,
                "Коэффициенты стабильны по квантилям: фактор сдвигает распределение " +
                "целиком, не меняя его формы. В такой ситуации МНК даёт полное описание.")
            .Finding("Сравнение с МНК — главный смысл квантильного процесса. Расхождение " +
                     "показывает, где именно средний эффект вводит в заблуждение.")
            .WarningIf(Quantiles.Any(q => q.Quantile is < 0.1 or > 0.9),
                "В наборе есть крайние квантили. Их оценки опираются на малое число " +
                "наблюдений и наименее устойчивы.")
            .Warning("Каждая регрессия оценивается независимо, поэтому монотонность " +
                     "квантилей не гарантирована. Пересечение линий на некоторых значениях " +
                     "регрессоров — известный артефакт метода.")
            .Recommendation("Стройте график коэффициента по тау вместе с доверительной " +
                            "полосой и горизонтальной линией оценки МНК — это стандартная " +
                            "и самая читаемая подача результата.")
            .Build();
    }
}

/// <summary>
/// Квантильная регрессия: связь регрессоров с заданным квантилем условного
/// распределения отклика.
/// </summary>
/// <remarks>
/// <para>
/// Метод наименьших квадратов описывает условное среднее. Когда важно, как
/// фактор влияет на бедных и богатых, на медленных и быстрых, на малые и
/// крупные заказы, среднее скрывает картину. Квантильная регрессия минимизирует
/// асимметричную функцию потерь:
/// </para>
/// <code>
/// rho_tau(e) = e * (tau - 1{e &lt; 0})
/// beta(tau) = argmin sum rho_tau(y_i - x_i' beta)
/// </code>
/// <para>
/// Задача решается итеративно взвешенным МНК с весами, обратными модулю
/// остатка и асимметричными по знаку. Стандартные ошибки получаются
/// бутстрапом по парам: аналитическая формула требует оценки плотности
/// остатков в нуле и на практике менее надёжна.
/// </para>
/// <para>
/// При tau равном 0,5 получается медианная регрессия — устойчивая к выбросам
/// в отклике альтернатива МНК.
/// </para>
/// </remarks>
public static class QuantileRegression
{
    /// <summary>Оценивает регрессию для одного квантиля.</summary>
    /// <param name="x">Матрица регрессоров без свободного члена.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <param name="quantile">Квантиль от нуля до единицы.</param>
    /// <param name="names">Названия регрессоров.</param>
    /// <param name="bootstrapSamples">Число бутстрап-повторов для стандартных ошибок.</param>
    /// <param name="seed">Зерно генератора для бутстрапа.</param>
    /// <returns>Коэффициенты и качество подгонки.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Квантиль вне интервала.</exception>
    public static QuantileRegressionResult Fit(
        Matrix x, Vector y, double quantile = 0.5,
        IReadOnlyList<string>? names = null, int bootstrapSamples = 300, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(quantile, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(quantile, 1);

        if (x.Height != y.Count)
            throw new ArgumentException("Число строк матрицы должно совпадать с длиной отклика.", nameof(y));

        int n = x.Height, k = x.Width + 1;
        var design = new double[n, k];
        var response = new double[n];

        for (int i = 0; i < n; i++)
        {
            design[i, 0] = 1;
            for (int j = 0; j < x.Width; j++) design[i, j + 1] = x[i, j];
            response[i] = y[i];
        }

        var labels = new List<string> { "const" };
        for (int j = 0; j < x.Width; j++)
            labels.Add(names is not null && j < names.Count ? names[j] : $"x{j + 1}");

        double[] beta = Solve(design, response, quantile);

        var residuals = new Vector(n);
        double objective = 0;

        for (int i = 0; i < n; i++)
        {
            double prediction = 0;
            for (int j = 0; j < k; j++) prediction += design[i, j] * beta[j];

            residuals[i] = response[i] - prediction;
            objective += Loss(residuals[i], quantile);
        }

        double baseline = 0;
        double[] sorted = [.. response.OrderBy(v => v)];
        double reference = EconMath.Quantile(sorted, quantile);
        foreach (double value in response) baseline += Loss(value - reference, quantile);

        double[] errors = Bootstrap(design, response, quantile, bootstrapSamples, seed, k);

        var coefficients = new List<Coefficient>(k);
        for (int j = 0; j < k; j++)
        {
            double error = errors[j];
            double t = error > 0 ? beta[j] / error : 0;
            double p = error > 0 ? Distributions.NormalPValue(t) : 1;

            coefficients.Add(new Coefficient(
                labels[j], beta[j], error, t, p,
                beta[j] - (1.96 * error), beta[j] + (1.96 * error)));
        }

        return new QuantileRegressionResult
        {
            Quantile = quantile,
            Coefficients = coefficients,
            Objective = objective,
            PseudoRSquared = baseline > 0 ? 1 - (objective / baseline) : 0,
            Observations = n,
            BootstrapSamples = bootstrapSamples,
            Residuals = residuals,
        };
    }

    /// <summary>Оценивает набор квантилей и сравнивает их с МНК.</summary>
    /// <param name="x">Матрица регрессоров.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <param name="quantiles">Уровни квантилей; при <c>null</c> берутся девять децилей.</param>
    /// <param name="names">Названия регрессоров.</param>
    /// <param name="bootstrapSamples">Число бутстрап-повторов.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Квантильный процесс с траекториями коэффициентов.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static QuantileProcessResult FitProcess(
        Matrix x, Vector y, IReadOnlyList<double>? quantiles = null,
        IReadOnlyList<string>? names = null, int bootstrapSamples = 200, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        IReadOnlyList<double> levels = quantiles is { Count: > 0 }
            ? [.. quantiles.OrderBy(q => q)]
            : [0.1, 0.25, 0.5, 0.75, 0.9];

        var results = new List<QuantileRegressionResult>(levels.Count);
        for (int i = 0; i < levels.Count; i++)
            results.Add(Fit(x, y, levels[i], names, bootstrapSamples, seed + i));

        var variables = new List<string>();
        for (int j = 0; j < x.Width; j++)
            variables.Add(names is not null && j < names.Count ? names[j] : $"x{j + 1}");

        return new QuantileProcessResult
        {
            Quantiles = results,
            LeastSquares = LinearRegression.Fit(x, y, names).Coefficients,
            Variables = variables,
        };
    }

    /// <summary>Итеративно взвешенный МНК для асимметричной функции потерь.</summary>
    private static double[] Solve(double[,] design, double[] y, double quantile)
    {
        int n = design.GetLength(0), k = design.GetLength(1);
        var names = new List<string>();
        for (int j = 0; j < k; j++) names.Add($"b{j}");

        var options = new RegressionOptions { AddIntercept = false };
        RegressionResult start = LinearRegression.FitDesign(design, y, names, options, "старт");

        var beta = new double[k];
        for (int j = 0; j < k; j++) beta[j] = start.Coefficients[j].Estimate;

        const double floor = 1e-6;

        for (int iteration = 0; iteration < 200; iteration++)
        {
            var weights = new double[n];

            for (int i = 0; i < n; i++)
            {
                double prediction = 0;
                for (int j = 0; j < k; j++) prediction += design[i, j] * beta[j];

                double residual = y[i] - prediction;
                double asymmetric = residual >= 0 ? quantile : 1 - quantile;
                weights[i] = asymmetric / Math.Max(Math.Abs(residual), floor);
            }

            double[,] gram = LinearAlgebra.WeightedGram(design, weights);
            for (int j = 0; j < k; j++) gram[j, j] += 1e-10;

            double[,]? inverse = EconMath.Inverse(gram);
            if (inverse is null) break;

            double[] updated = LinearAlgebra.Multiply(
                inverse, LinearAlgebra.WeightedCross(design, weights, y));

            double shift = 0;
            for (int j = 0; j < k; j++) shift += Math.Abs(updated[j] - beta[j]);

            beta = updated;
            if (shift < 1e-10) break;
        }

        return beta;
    }

    /// <summary>Стандартные ошибки бутстрапом по парам наблюдений.</summary>
    private static double[] Bootstrap(
        double[,] design, double[] y, double quantile, int samples, int seed, int k)
    {
        if (samples <= 1) return new double[k];

        Random rng = RandomEngine.Create(seed);
        int n = design.GetLength(0);
        var draws = new List<double[]>(samples);

        for (int b = 0; b < samples; b++)
        {
            var resampledX = new double[n, k];
            var resampledY = new double[n];

            for (int i = 0; i < n; i++)
            {
                int pick = rng.Next(n);
                for (int j = 0; j < k; j++) resampledX[i, j] = design[pick, j];
                resampledY[i] = y[pick];
            }

            draws.Add(Solve(resampledX, resampledY, quantile));
        }

        var errors = new double[k];
        for (int j = 0; j < k; j++)
        {
            double mean = draws.Average(d => d[j]);
            double variance = draws.Sum(d => (d[j] - mean) * (d[j] - mean)) / (draws.Count - 1);
            errors[j] = Math.Sqrt(Math.Max(variance, 0));
        }

        return errors;
    }

    /// <summary>Асимметричная функция потерь.</summary>
    private static double Loss(double residual, double quantile) =>
        residual >= 0 ? quantile * residual : (quantile - 1) * residual;
}
