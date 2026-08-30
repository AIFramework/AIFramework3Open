using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Econometrics;

/// <summary>Способ оценки ковариационной матрицы коэффициентов.</summary>
public enum RobustVariance
{
    /// <summary>Классическая: предполагается гомоскедастичность и независимость ошибок.</summary>
    Classical,

    /// <summary>Уайт без поправки на степени свободы.</summary>
    Hc0,

    /// <summary>Уайт с поправкой <c>n/(n-k)</c>.</summary>
    Hc1,

    /// <summary>Уайт с поправкой на рычаг наблюдения.</summary>
    Hc2,

    /// <summary>Уайт с квадратом поправки на рычаг: самая консервативная версия.</summary>
    Hc3,

    /// <summary>Ньюи — Уэст: устойчивость к гетероскедастичности и автокорреляции.</summary>
    NeweyWest,

    /// <summary>Кластерные ошибки: корреляция внутри групп произвольна.</summary>
    Clustered,
}

/// <summary>Настройки оценивания линейной регрессии.</summary>
public sealed record RegressionOptions
{
    /// <summary>Способ оценки ковариационной матрицы.</summary>
    public RobustVariance Variance { get; init; } = RobustVariance.Classical;

    /// <summary>Число лагов для оценки Ньюи — Уэста; при нуле берётся правило Бартлетта.</summary>
    public int Lags { get; init; }

    /// <summary>Идентификаторы кластеров по наблюдениям для кластерных ошибок.</summary>
    public IReadOnlyList<int>? Clusters { get; init; }

    /// <summary>Веса наблюдений для взвешенного МНК; при <c>null</c> все веса равны единице.</summary>
    public IReadOnlyList<double>? Weights { get; init; }

    /// <summary>Коэффициент гребневой регуляризации при обращении матрицы.</summary>
    public double Ridge { get; init; } = 1e-10;

    /// <summary>Добавлять ли свободный член автоматически.</summary>
    public bool AddIntercept { get; init; } = true;
}

/// <summary>Оценка одного коэффициента регрессии.</summary>
/// <param name="Name">Название переменной.</param>
/// <param name="Estimate">Оценка коэффициента.</param>
/// <param name="StandardError">Стандартная ошибка.</param>
/// <param name="TStatistic">Статистика Стьюдента.</param>
/// <param name="PValue">Двустороннее p-значение.</param>
/// <param name="ConfidenceLow">Нижняя граница 95-процентного интервала.</param>
/// <param name="ConfidenceHigh">Верхняя граница 95-процентного интервала.</param>
public sealed record Coefficient(
    string Name, double Estimate, double StandardError, double TStatistic,
    double PValue, double ConfidenceLow, double ConfidenceHigh)
{
    /// <summary>Значим ли коэффициент на уровне 5%.</summary>
    public bool IsSignificant => PValue < 0.05;

    /// <summary>Звёздочки значимости в принятой в статьях нотации.</summary>
    public string Stars =>
        PValue < 0.01 ? "***" : PValue < 0.05 ? "**" : PValue < 0.1 ? "*" : "";
}

/// <summary>Результат оценивания линейной регрессии.</summary>
public sealed record RegressionResult : IInterpretable
{
    /// <summary>Название модели для отчёта.</summary>
    public string Model { get; init; } = "МНК";

    /// <summary>Оценки коэффициентов.</summary>
    public IReadOnlyList<Coefficient> Coefficients { get; init; } = [];

    /// <summary>Остатки.</summary>
    public Vector Residuals { get; init; } = new(0);

    /// <summary>Расчётные значения отклика.</summary>
    public Vector Fitted { get; init; } = new(0);

    /// <summary>Коэффициент детерминации.</summary>
    public double RSquared { get; init; }

    /// <summary>Скорректированный коэффициент детерминации.</summary>
    public double AdjustedRSquared { get; init; }

    /// <summary>Статистика Фишера общей значимости.</summary>
    public double FStatistic { get; init; }

    /// <summary>Уровень значимости статистики Фишера.</summary>
    public double FPValue { get; init; } = 1;

    /// <summary>Стандартная ошибка регрессии.</summary>
    public double Sigma { get; init; }

    /// <summary>Логарифм правдоподобия в предположении нормальных ошибок.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Информационный критерий Акаике.</summary>
    public double Aic { get; init; }

    /// <summary>Информационный критерий Шварца.</summary>
    public double Bic { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Число оценённых коэффициентов.</summary>
    public int Parameters { get; init; }

    /// <summary>Использованный способ оценки ковариационной матрицы.</summary>
    public RobustVariance Variance { get; init; }

    /// <summary>Ковариационная матрица коэффициентов.</summary>
    public Matrix CovarianceMatrix { get; init; } = new(1, 1);

    /// <summary>Число степеней свободы остатков.</summary>
    public int ResidualDegreesOfFreedom => Math.Max(1, Observations - Parameters);

    /// <summary>Значимые на уровне 5% коэффициенты, кроме свободного члена.</summary>
    public IReadOnlyList<Coefficient> Significant =>
        [.. Coefficients.Where(c => c.IsSignificant && c.Name != "const")];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        Coefficient? strongest = Coefficients
            .Where(c => c.Name != "const")
            .OrderByDescending(c => Math.Abs(c.TStatistic))
            .FirstOrDefault();

        int insignificant = Coefficients.Count(c => c.Name != "const" && !c.IsSignificant);
        bool robust = Variance != RobustVariance.Classical;

        var builder = new InterpretationBuilder($"Регрессия: {Model}")
            .Summary($"Оценено {Parameters} коэффициентов по {Observations} наблюдениям. " +
                     $"Модель объясняет {Fmt.Pct(RSquared, 1)} дисперсии отклика " +
                     $"(скорректированный {Fmt.Num(AdjustedRSquared, 3)}), совместная значимость " +
                     $"F = {Fmt.Num(FStatistic, 2)} при p = {Fmt.Num(FPValue, 4)}. " +
                     $"Стандартные ошибки: {VarianceName()}.")
            .Metric("R²", RSquared, null, "доля объяснённой дисперсии",
                RSquared > 0.5 ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Скорректированный R²", AdjustedRSquared, null,
                "с поправкой на число регрессоров", MetricQuality.Neutral, 4)
            .Metric("F-статистика", FStatistic, null,
                $"совместная значимость, p = {Fmt.Num(FPValue, 4)}",
                FPValue < 0.05 ? MetricQuality.Good : MetricQuality.Warning, 2)
            .Metric("Стандартная ошибка", Sigma, null, "разброс остатков", MetricQuality.Neutral, 4)
            .Metric("Наблюдений на параметр", (double)Observations / Math.Max(1, Parameters), null,
                "меньше десяти — модель переопределена",
                Observations >= 10 * Parameters ? MetricQuality.Good : MetricQuality.Warning, 1)
            .Metric("AIC", Aic, null, $"BIC {Fmt.Num(Bic, 1)}", MetricQuality.Neutral, 1);

        foreach (Coefficient coefficient in Coefficients)
        {
            builder.Metric(coefficient.Name, coefficient.Estimate, null,
                $"ст. ошибка {Fmt.Num(coefficient.StandardError, 4)}, t = {Fmt.Num(coefficient.TStatistic, 2)}, " +
                $"p = {Fmt.Num(coefficient.PValue, 4)} {coefficient.Stars}".TrimEnd(),
                coefficient.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        return builder
            .FindingIf(strongest is not null,
                $"Сильнее всего связан с откликом регрессор «{strongest?.Name}»: коэффициент " +
                $"{Fmt.Num(strongest?.Estimate ?? 0, 4)} при t = {Fmt.Num(strongest?.TStatistic ?? 0, 2)}, " +
                $"интервал [{Fmt.Num(strongest?.ConfidenceLow ?? 0, 4)}; {Fmt.Num(strongest?.ConfidenceHigh ?? 0, 4)}].")
            .FindingIf(robust,
                $"Использованы устойчивые стандартные ошибки ({VarianceName()}). Оценки " +
                "коэффициентов от этого не меняются — меняется только их точность, " +
                "а значит и выводы о значимости.")
            .FindingIf(!robust,
                "Стандартные ошибки классические. Они верны только при постоянной дисперсии " +
                "ошибок и их независимости; обе предпосылки стоит проверить диагностикой.")
            .FindingIf(insignificant > 0,
                $"Незначимых регрессоров: {insignificant}. Незначимость означает нехватку " +
                "данных для различения эффекта с нулём, а не доказанное отсутствие эффекта.")
            .WarningIf(Observations < 10 * Parameters,
                $"На один параметр приходится меньше десяти наблюдений " +
                $"({Fmt.Num((double)Observations / Math.Max(1, Parameters), 1)}). " +
                "Оценки неустойчивы, доверительные интервалы широки.")
            .WarningIf(RSquared > 0.95,
                $"R² = {Fmt.Num(RSquared, 3)} подозрительно высок. Частые причины — " +
                "регрессор, механически связанный с откликом, или общий тренд у обеих переменных.")
            .Warning("Коэффициенты регрессии измеряют условную корреляцию, а не причинный " +
                     "эффект. Причинная интерпретация требует либо эксперимента, либо явной " +
                     "стратегии идентификации — инструментов, панельных эффектов, разрывного дизайна.")
            .Recommendation("Проверьте остатки диагностикой: гетероскедастичность и " +
                            "автокорреляция не смещают оценки, но делают классические " +
                            "стандартные ошибки неверными.")
            .Recommendation("Сравнивайте вложенные модели по информационным критериям, " +
                            "а не по R²: он растёт от любого добавленного регрессора.")
            .Build();
    }

    /// <summary>Читаемое название способа оценки ошибок.</summary>
    private string VarianceName() => Variance switch
    {
        RobustVariance.Classical => "классические",
        RobustVariance.Hc0 => "Уайта HC0",
        RobustVariance.Hc1 => "Уайта HC1",
        RobustVariance.Hc2 => "Уайта HC2",
        RobustVariance.Hc3 => "Уайта HC3",
        RobustVariance.NeweyWest => "Ньюи — Уэста",
        _ => "кластерные",
    };
}

/// <summary>
/// Линейная регрессия с устойчивыми стандартными ошибками.
/// </summary>
/// <remarks>
/// <para>
/// Оценка коэффициентов — обычный или взвешенный метод наименьших квадратов:
/// </para>
/// <code>
/// beta = (X' W X)^-1 X' W y
/// </code>
/// <para>
/// Способ оценки ковариационной матрицы задаётся отдельно и не влияет на сами
/// коэффициенты. Это ключевая мысль: гетероскедастичность и автокорреляция не
/// смещают оценки, но делают классические стандартные ошибки неверными, а
/// значит неверными становятся все выводы о значимости.
/// </para>
/// <para>
/// Сэндвич-оценка имеет общий вид
/// </para>
/// <code>
/// V = (X'X)^-1 * Omega * (X'X)^-1
/// </code>
/// <para>
/// где «начинка» <c>Omega</c> для HC0 равна <c>sum e_i^2 x_i x_i'</c>, для HC2 и
/// HC3 остатки делятся на <c>1-h_i</c> и <c>(1-h_i)^2</c>, для Ньюи — Уэста
/// добавляются взвешенные по Бартлетту автоковариации, а для кластерных ошибок
/// суммируются произведения групповых сумм.
/// </para>
/// </remarks>
public static class LinearRegression
{
    /// <summary>Оценивает линейную регрессию.</summary>
    /// <param name="x">Матрица регрессоров без свободного члена.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <param name="names">Названия регрессоров; при <c>null</c> подставляются <c>x1..xk</c>.</param>
    /// <param name="options">Настройки оценивания; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Коэффициенты, качество подгонки и ковариационная матрица.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или наблюдений меньше числа параметров.</exception>
    public static RegressionResult Fit(
        Matrix x, Vector y, IReadOnlyList<string>? names = null, RegressionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        options ??= new RegressionOptions();

        if (x.Height != y.Count)
            throw new ArgumentException("Число строк матрицы должно совпадать с длиной отклика.", nameof(y));

        int n = x.Height;
        int regressors = x.Width;
        int k = regressors + (options.AddIntercept ? 1 : 0);

        if (n <= k)
            throw new ArgumentException("Наблюдений должно быть больше числа параметров.", nameof(x));

        var design = new double[n, k];
        var response = new double[n];

        for (int i = 0; i < n; i++)
        {
            int offset = 0;
            if (options.AddIntercept) { design[i, 0] = 1; offset = 1; }
            for (int j = 0; j < regressors; j++) design[i, j + offset] = x[i, j];
            response[i] = y[i];
        }

        var labels = new List<string>(k);
        if (options.AddIntercept) labels.Add("const");
        for (int j = 0; j < regressors; j++)
            labels.Add(names is not null && j < names.Count ? names[j] : $"x{j + 1}");

        return FitDesign(design, response, labels, options, "МНК");
    }

    /// <summary>Оценивает регрессию по уже собранной матрице плана.</summary>
    /// <param name="design">Матрица плана, включая свободный член, если он нужен.</param>
    /// <param name="response">Вектор отклика.</param>
    /// <param name="names">Названия столбцов матрицы плана.</param>
    /// <param name="options">Настройки оценивания.</param>
    /// <param name="model">Название модели для отчёта.</param>
    /// <returns>Результат оценивания.</returns>
    internal static RegressionResult FitDesign(
        double[,] design, double[] response, IReadOnlyList<string> names,
        RegressionOptions options, string model)
    {
        int n = design.GetLength(0), k = design.GetLength(1);

        var weights = new double[n];
        for (int i = 0; i < n; i++)
            weights[i] = options.Weights is not null && i < options.Weights.Count
                ? Math.Max(options.Weights[i], 0)
                : 1;

        double[,] gram = LinearAlgebra.WeightedGram(design, weights);
        for (int j = 0; j < k; j++) gram[j, j] += options.Ridge;

        double[,] gramInverse = EconMath.Inverse(gram)
            ?? throw new ArgumentException(
                "Матрица регрессоров вырождена: проверьте коллинеарность.", nameof(design));

        double[] cross = LinearAlgebra.WeightedCross(design, weights, response);
        double[] beta = LinearAlgebra.Multiply(gramInverse, cross);

        var fitted = new double[n];
        var residuals = new double[n];
        double rss = 0, tss = 0;
        double mean = response.Average();

        for (int i = 0; i < n; i++)
        {
            double prediction = 0;
            for (int j = 0; j < k; j++) prediction += design[i, j] * beta[j];

            fitted[i] = prediction;
            residuals[i] = response[i] - prediction;
            rss += weights[i] * residuals[i] * residuals[i];
            tss += weights[i] * (response[i] - mean) * (response[i] - mean);
        }

        double sigmaSquared = rss / Math.Max(1, n - k);
        double[,] covariance = Sandwich(design, residuals, weights, gramInverse, sigmaSquared, options);

        var coefficients = new List<Coefficient>(k);
        int df = Math.Max(1, n - k);

        for (int j = 0; j < k; j++)
        {
            double error = Math.Sqrt(Math.Max(covariance[j, j], 0));
            double t = error > 0 ? beta[j] / error : double.NaN;
            double p = Distributions.TPValue(t, df);
            double critical = StatisticsCritical(df);

            coefficients.Add(new Coefficient(
                names[j], beta[j], error, t, double.IsNaN(p) ? 1 : p,
                beta[j] - (critical * error), beta[j] + (critical * error)));
        }

        double rSquared = tss > 0 ? 1 - (rss / tss) : 0;
        double adjusted = n > k ? 1 - ((1 - rSquared) * (n - 1) / (n - k)) : 0;

        bool hasIntercept = names.Count > 0 && names[0] == "const";
        int restrictions = Math.Max(1, k - (hasIntercept ? 1 : 0));
        double fStatistic = rSquared < 1 && restrictions > 0
            ? rSquared / restrictions / ((1 - rSquared) / df)
            : double.NaN;

        double logLikelihood = -0.5 * n * (Math.Log(2 * Math.PI) + Math.Log(Math.Max(rss / n, 1e-300)) + 1);

        var covarianceMatrix = new Matrix(k, k);
        for (int a = 0; a < k; a++)
            for (int b = 0; b < k; b++) covarianceMatrix[a, b] = covariance[a, b];

        return new RegressionResult
        {
            Model = model,
            Coefficients = coefficients,
            Residuals = ToVector(residuals),
            Fitted = ToVector(fitted),
            RSquared = rSquared,
            AdjustedRSquared = adjusted,
            FStatistic = double.IsNaN(fStatistic) ? 0 : fStatistic,
            FPValue = double.IsNaN(fStatistic) ? 1 : Distributions.FPValue(fStatistic, restrictions, df),
            Sigma = Math.Sqrt(sigmaSquared),
            LogLikelihood = logLikelihood,
            Aic = (-2 * logLikelihood) + (2 * k),
            Bic = (-2 * logLikelihood) + (k * Math.Log(n)),
            Observations = n,
            Parameters = k,
            Variance = options.Variance,
            CovarianceMatrix = covarianceMatrix,
        };
    }

    /// <summary>Сэндвич-оценка ковариационной матрицы коэффициентов.</summary>
    private static double[,] Sandwich(
        double[,] design, double[] residuals, double[] weights,
        double[,] gramInverse, double sigmaSquared, RegressionOptions options)
    {
        int n = design.GetLength(0), k = design.GetLength(1);

        if (options.Variance == RobustVariance.Classical)
        {
            var classical = new double[k, k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++) classical[a, b] = sigmaSquared * gramInverse[a, b];

            return classical;
        }

        var meat = new double[k, k];

        if (options.Variance == RobustVariance.Clustered)
        {
            IReadOnlyList<int> clusters = options.Clusters
                ?? throw new ArgumentException(
                    "Для кластерных ошибок нужны идентификаторы кластеров.", nameof(options));

            var groups = new Dictionary<int, double[]>();

            for (int i = 0; i < n; i++)
            {
                int group = i < clusters.Count ? clusters[i] : 0;
                if (!groups.TryGetValue(group, out double[]? sum))
                {
                    sum = new double[k];
                    groups[group] = sum;
                }

                for (int j = 0; j < k; j++) sum[j] += weights[i] * design[i, j] * residuals[i];
            }

            foreach (double[] sum in groups.Values)
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) meat[a, b] += sum[a] * sum[b];

            int g = groups.Count;
            double correction = g > 1
                ? (double)g / (g - 1) * (n - 1) / Math.Max(1, n - k)
                : 1;

            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++) meat[a, b] *= correction;
        }
        else
        {
            var leverage = new double[n];
            if (options.Variance is RobustVariance.Hc2 or RobustVariance.Hc3)
            {
                for (int i = 0; i < n; i++)
                {
                    double h = 0;
                    for (int a = 0; a < k; a++)
                        for (int b = 0; b < k; b++) h += design[i, a] * gramInverse[a, b] * design[i, b];

                    leverage[i] = Math.Min(h * weights[i], 0.9999);
                }
            }

            var scaled = new double[n];
            for (int i = 0; i < n; i++)
            {
                double e2 = weights[i] * residuals[i] * residuals[i];

                scaled[i] = options.Variance switch
                {
                    RobustVariance.Hc2 => e2 / (1 - leverage[i]),
                    RobustVariance.Hc3 => e2 / ((1 - leverage[i]) * (1 - leverage[i])),
                    _ => e2,
                };
            }

            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++)
                        meat[a, b] += scaled[i] * weights[i] * design[i, a] * design[i, b];

            if (options.Variance == RobustVariance.Hc1)
            {
                double correction = (double)n / Math.Max(1, n - k);
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) meat[a, b] *= correction;
            }

            if (options.Variance == RobustVariance.NeweyWest)
            {
                int lags = options.Lags > 0
                    ? options.Lags
                    : Math.Max(1, (int)Math.Floor(4 * Math.Pow(n / 100.0, 2.0 / 9.0)));

                for (int l = 1; l <= lags && l < n; l++)
                {
                    double bartlett = 1.0 - ((double)l / (lags + 1));

                    for (int t = l; t < n; t++)
                    {
                        double product = residuals[t] * residuals[t - l] * weights[t] * weights[t - l];

                        for (int a = 0; a < k; a++)
                            for (int b = 0; b < k; b++)
                            {
                                meat[a, b] += bartlett * product *
                                    ((design[t, a] * design[t - l, b]) + (design[t - l, a] * design[t, b]));
                            }
                    }
                }
            }
        }

        double[,] left = LinearAlgebra.Multiply(gramInverse, meat);
        return LinearAlgebra.Multiply(left, gramInverse);
    }

    /// <summary>Критическое значение Стьюдента для 95-процентного интервала.</summary>
    private static double StatisticsCritical(int df) =>
        df > 200 ? 1.959963985 : AI.Statistics.StatInference.TQuantile(0.975, df);

    /// <summary>Преобразует массив в вектор фреймворка.</summary>
    internal static Vector ToVector(double[] values)
    {
        var vector = new Vector(values.Length);
        for (int i = 0; i < values.Length; i++) vector[i] = values[i];
        return vector;
    }

    /// <summary>Преобразует матрицу фреймворка в массив.</summary>
    internal static double[,] ToArray(Matrix matrix)
    {
        var array = new double[matrix.Height, matrix.Width];
        for (int i = 0; i < matrix.Height; i++)
            for (int j = 0; j < matrix.Width; j++) array[i, j] = matrix[i, j];

        return array;
    }

    /// <summary>Преобразует массив в матрицу фреймворка.</summary>
    internal static Matrix ToMatrix(double[,] array)
    {
        var matrix = new Matrix(array.GetLength(0), array.GetLength(1));
        for (int i = 0; i < array.GetLength(0); i++)
            for (int j = 0; j < array.GetLength(1); j++) matrix[i, j] = array[i, j];

        return matrix;
    }
}
