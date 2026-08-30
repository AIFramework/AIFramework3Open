using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Econometrics;

/// <summary>Диагностика первой ступени для одного эндогенного регрессора.</summary>
/// <param name="Variable">Название эндогенного регрессора.</param>
/// <param name="FStatistic">F-статистика исключённых инструментов.</param>
/// <param name="PValue">Уровень значимости.</param>
/// <param name="PartialRSquared">Частный коэффициент детерминации инструментов.</param>
public sealed record FirstStage(string Variable, double FStatistic, double PValue, double PartialRSquared)
{
    /// <summary>Слабы ли инструменты по правилу Стайгера — Стока.</summary>
    public bool IsWeak => FStatistic < 10;
}

/// <summary>Результат оценивания с инструментальными переменными.</summary>
public sealed record IvResult : IInterpretable
{
    /// <summary>Название метода.</summary>
    public string Model { get; init; } = "Двухшаговый МНК";

    /// <summary>Оценки коэффициентов структурного уравнения.</summary>
    public IReadOnlyList<Coefficient> Coefficients { get; init; } = [];

    /// <summary>Оценки того же уравнения обычным МНК — для сравнения.</summary>
    public IReadOnlyList<Coefficient> OrdinaryLeastSquares { get; init; } = [];

    /// <summary>Диагностика первой ступени по каждому эндогенному регрессору.</summary>
    public IReadOnlyList<FirstStage> FirstStages { get; init; } = [];

    /// <summary>Статистика теста на сверхидентифицирующие ограничения.</summary>
    public double OveridentificationStatistic { get; init; }

    /// <summary>Уровень значимости теста на сверхидентифицирующие ограничения.</summary>
    public double OveridentificationPValue { get; init; } = 1;

    /// <summary>Число избыточных инструментов.</summary>
    public int OveridentifyingRestrictions { get; init; }

    /// <summary>Статистика теста Хаусмана на эндогенность.</summary>
    public double HausmanStatistic { get; init; }

    /// <summary>Уровень значимости теста Хаусмана.</summary>
    public double HausmanPValue { get; init; } = 1;

    /// <summary>Остатки структурного уравнения.</summary>
    public Vector Residuals { get; init; } = new(0);

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Есть ли хотя бы один слабый инструментальный набор.</summary>
    public bool HasWeakInstruments => FirstStages.Any(s => s.IsWeak);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        FirstStage? weakest = FirstStages.OrderBy(s => s.FStatistic).FirstOrDefault();

        Coefficient? iv = Coefficients.FirstOrDefault(c => c.Name != "const");
        Coefficient? ols = OrdinaryLeastSquares.FirstOrDefault(c => c.Name == iv?.Name);
        double gap = iv is not null && ols is not null ? iv.Estimate - ols.Estimate : 0;

        bool endogenous = HausmanPValue < 0.05;
        bool overidentified = OveridentifyingRestrictions > 0;

        var builder = new InterpretationBuilder($"Инструментальные переменные: {Model}")
            .Summary($"Оценено по {Observations} наблюдениям. Первая ступень: минимальная " +
                     $"F-статистика {Fmt.Num(weakest?.FStatistic ?? 0, 1)} " +
                     $"({(HasWeakInstruments ? "инструменты слабые" : "инструменты сильные")}). " +
                     $"Тест Хаусмана p = {Fmt.Num(HausmanPValue, 4)}: " +
                     $"{(endogenous ? "эндогенность подтверждается" : "эндогенность не подтверждается")}. " +
                     (overidentified
                         ? $"Сверхидентификация: p = {Fmt.Num(OveridentificationPValue, 4)}."
                         : "Модель точно идентифицирована, проверить инструменты нельзя."))
            .Metric("Минимальная F первой ступени", weakest?.FStatistic ?? 0, null,
                "порог Стайгера — Стока равен десяти",
                (weakest?.FStatistic ?? 0) >= 10 ? MetricQuality.Good : MetricQuality.Critical, 1)
            .Metric("Хаусман", HausmanStatistic, null,
                $"p = {Fmt.Num(HausmanPValue, 4)}; " +
                (endogenous ? "МНК смещён" : "разница с МНК статистически незначима"),
                endogenous ? MetricQuality.Warning : MetricQuality.Neutral, 3)
            .Metric("Сверхидентификация", OveridentificationStatistic, null,
                overidentified
                    ? $"{OveridentifyingRestrictions} избыточных инструментов, p = {Fmt.Num(OveridentificationPValue, 4)}"
                    : "тест недоступен: инструментов ровно столько же, сколько эндогенных регрессоров",
                overidentified && OveridentificationPValue < 0.05 ? MetricQuality.Critical : MetricQuality.Neutral, 3);

        foreach (Coefficient coefficient in Coefficients)
        {
            Coefficient? comparison = OrdinaryLeastSquares.FirstOrDefault(c => c.Name == coefficient.Name);

            builder.Metric(coefficient.Name, coefficient.Estimate, null,
                $"ст. ошибка {Fmt.Num(coefficient.StandardError, 4)}, p = {Fmt.Num(coefficient.PValue, 4)}" +
                (comparison is not null ? $"; МНК даёт {Fmt.Num(comparison.Estimate, 4)}" : ""),
                coefficient.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        foreach (FirstStage stage in FirstStages)
        {
            builder.Metric($"Первая ступень: {stage.Variable}", stage.FStatistic, null,
                $"частный R² {Fmt.Num(stage.PartialRSquared, 3)}, p = {Fmt.Num(stage.PValue, 4)}",
                stage.IsWeak ? MetricQuality.Critical : MetricQuality.Good, 1);
        }

        return builder
            .FindingIf(iv is not null && ols is not null,
                $"Оценка «{iv?.Name}» меняется с {Fmt.Num(ols?.Estimate ?? 0, 4)} по МНК " +
                $"до {Fmt.Num(iv?.Estimate ?? 0, 4)} с инструментами — сдвиг {Fmt.Num(gap, 4)}. " +
                "Именно ради этого сдвига инструменты и вводятся: он показывает величину " +
                "смещения от эндогенности.")
            .FindingIf(!endogenous,
                "Тест Хаусмана не отвергает экзогенность. Если так, МНК эффективнее: " +
                "инструментальная оценка состоятельна, но её стандартные ошибки заметно шире.")
            .FindingIf(overidentified && OveridentificationPValue >= 0.05,
                "Сверхидентифицирующие ограничения не отвергаются: инструменты согласованы " +
                "между собой. Это необходимое, но не достаточное условие их валидности.")
            .WarningIf(HasWeakInstruments,
                $"F-статистика первой ступени {Fmt.Num(weakest?.FStatistic ?? 0, 1)} ниже десяти. " +
                "При слабых инструментах двухшаговый МНК смещён в сторону МНК, а его " +
                "доверительные интервалы не имеют заявленного покрытия.")
            .WarningIf(overidentified && OveridentificationPValue < 0.05,
                "Сверхидентифицирующие ограничения отвергаются: как минимум один инструмент " +
                "коррелирован с ошибкой структурного уравнения либо модель неверно " +
                "специфицирована.")
            .Warning("Экзогенность инструментов статистически непроверяема при точной " +
                     "идентификации и проверяема лишь частично при сверхидентификации. " +
                     "Обоснование должно быть содержательным, а не эконометрическим.")
            .Recommendation("Всегда показывайте оценку МНК рядом с инструментальной: " +
                            "различие между ними — главный аргумент в пользу инструментов.")
            .Recommendation("Приводите F-статистику первой ступени в основной таблице. " +
                            "Без неё читатель не может судить о надёжности результата.")
            .Build();
    }
}

/// <summary>
/// Оценивание с инструментальными переменными: двухшаговый МНК и обобщённый
/// метод моментов.
/// </summary>
/// <remarks>
/// <para>
/// Задача возникает всюду, где регрессор определяется одновременно с откликом.
/// Классический пример — цена и спрос: наивная регрессия количества на цену
/// даёт положительный коэффициент, потому что и то и другое реагирует на
/// ненаблюдаемый спросовый шок.
/// </para>
/// <para>
/// Двухшаговый МНК проецирует эндогенные регрессоры на пространство
/// инструментов и экзогенных переменных:
/// </para>
/// <code>
/// P = Z (Z'Z)^-1 Z'
/// beta = (X' P X)^-1 X' P y
/// </code>
/// <para>
/// Остатки при этом считаются по фактическим, а не по спроецированным
/// регрессорам — иначе стандартные ошибки окажутся заниженными. Это самая
/// частая ошибка ручной реализации двухшагового МНК.
/// </para>
/// <para>
/// Обобщённый метод моментов обобщает эту схему на произвольную весовую
/// матрицу: двухшаговая версия использует оценку ковариации моментных условий,
/// что даёт эффективность при гетероскедастичности.
/// </para>
/// </remarks>
public static class InstrumentalVariables
{
    /// <summary>Оценивает структурное уравнение двухшаговым МНК.</summary>
    /// <param name="endogenous">Эндогенные регрессоры.</param>
    /// <param name="exogenous">Экзогенные регрессоры без свободного члена; может быть <c>null</c>.</param>
    /// <param name="instruments">Исключённые инструменты.</param>
    /// <param name="y">Отклик.</param>
    /// <param name="endogenousNames">Названия эндогенных регрессоров.</param>
    /// <param name="exogenousNames">Названия экзогенных регрессоров.</param>
    /// <param name="robust">Использовать ли устойчивые к гетероскедастичности ошибки.</param>
    /// <returns>Коэффициенты, диагностика первой ступени и тесты.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или инструментов меньше эндогенных регрессоров.</exception>
    public static IvResult TwoStage(
        Matrix endogenous, Matrix? exogenous, Matrix instruments, Vector y,
        IReadOnlyList<string>? endogenousNames = null,
        IReadOnlyList<string>? exogenousNames = null,
        bool robust = true)
    {
        ArgumentNullException.ThrowIfNull(endogenous);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(y);

        int n = y.Count;

        if (endogenous.Height != n || instruments.Height != n)
            throw new ArgumentException("Число наблюдений должно совпадать во всех блоках.", nameof(y));
        if (exogenous is not null && exogenous.Height != n)
            throw new ArgumentException("Число наблюдений должно совпадать во всех блоках.", nameof(exogenous));
        if (instruments.Width < endogenous.Width)
            throw new ArgumentException(
                "Инструментов должно быть не меньше, чем эндогенных регрессоров.", nameof(instruments));

        int exogenousCount = exogenous?.Width ?? 0;
        int k = 1 + exogenousCount + endogenous.Width;
        int instrumentCount = 1 + exogenousCount + instruments.Width;

        var x = new double[n, k];
        var z = new double[n, instrumentCount];
        var response = new double[n];

        for (int i = 0; i < n; i++)
        {
            x[i, 0] = 1;
            z[i, 0] = 1;

            for (int j = 0; j < exogenousCount; j++)
            {
                x[i, 1 + j] = exogenous![i, j];
                z[i, 1 + j] = exogenous[i, j];
            }

            for (int j = 0; j < endogenous.Width; j++) x[i, 1 + exogenousCount + j] = endogenous[i, j];
            for (int j = 0; j < instruments.Width; j++) z[i, 1 + exogenousCount + j] = instruments[i, j];

            response[i] = y[i];
        }

        var names = new List<string> { "const" };
        for (int j = 0; j < exogenousCount; j++)
            names.Add(exogenousNames is not null && j < exogenousNames.Count ? exogenousNames[j] : $"w{j + 1}");
        for (int j = 0; j < endogenous.Width; j++)
            names.Add(endogenousNames is not null && j < endogenousNames.Count ? endogenousNames[j] : $"d{j + 1}");

        (double[] beta, double[,] covariance, double[] residuals) = Solve(x, z, response, robust);

        var coefficients = BuildCoefficients(names, beta, covariance, n - k);

        // Обычный МНК на тех же данных — контраст, ради которого всё и делается
        var options = new RegressionOptions
        {
            Variance = robust ? RobustVariance.Hc1 : RobustVariance.Classical,
            AddIntercept = false,
        };

        RegressionResult ols = LinearRegression.FitDesign(x, response, names, options, "МНК");

        int restrictions = Math.Max(0, instruments.Width - endogenous.Width);
        double sargan = restrictions > 0 ? Sargan(z, residuals) : 0;
        double hausman = Hausman(coefficients, ols.Coefficients, out double hausmanP);

        return new IvResult
        {
            Coefficients = coefficients,
            OrdinaryLeastSquares = ols.Coefficients,
            FirstStages = FirstStageDiagnostics(z, endogenous, exogenousCount, endogenousNames),
            OveridentificationStatistic = sargan,
            OveridentificationPValue = restrictions > 0
                ? Distributions.ChiSquarePValue(sargan, restrictions)
                : 1,
            OveridentifyingRestrictions = restrictions,
            HausmanStatistic = hausman,
            HausmanPValue = hausmanP,
            Residuals = LinearRegression.ToVector(residuals),
            Observations = n,
        };
    }

    /// <summary>Оценивает структурное уравнение двухшаговым обобщённым методом моментов.</summary>
    /// <param name="endogenous">Эндогенные регрессоры.</param>
    /// <param name="exogenous">Экзогенные регрессоры без свободного члена.</param>
    /// <param name="instruments">Исключённые инструменты.</param>
    /// <param name="y">Отклик.</param>
    /// <param name="endogenousNames">Названия эндогенных регрессоров.</param>
    /// <param name="exogenousNames">Названия экзогенных регрессоров.</param>
    /// <returns>Коэффициенты и статистика Хансена.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static IvResult GeneralizedMethodOfMoments(
        Matrix endogenous, Matrix? exogenous, Matrix instruments, Vector y,
        IReadOnlyList<string>? endogenousNames = null,
        IReadOnlyList<string>? exogenousNames = null)
    {
        IvResult first = TwoStage(endogenous, exogenous, instruments, y, endogenousNames, exogenousNames);

        int n = y.Count;
        int exogenousCount = exogenous?.Width ?? 0;
        int k = 1 + exogenousCount + endogenous.Width;
        int instrumentCount = 1 + exogenousCount + instruments.Width;

        var x = new double[n, k];
        var z = new double[n, instrumentCount];
        var response = new double[n];

        for (int i = 0; i < n; i++)
        {
            x[i, 0] = 1;
            z[i, 0] = 1;

            for (int j = 0; j < exogenousCount; j++)
            {
                x[i, 1 + j] = exogenous![i, j];
                z[i, 1 + j] = exogenous[i, j];
            }

            for (int j = 0; j < endogenous.Width; j++) x[i, 1 + exogenousCount + j] = endogenous[i, j];
            for (int j = 0; j < instruments.Width; j++) z[i, 1 + exogenousCount + j] = instruments[i, j];

            response[i] = y[i];
        }

        // Вторая ступень: весовая матрица из моментных условий первой
        var weight = new double[instrumentCount, instrumentCount];
        for (int i = 0; i < n; i++)
        {
            double u = first.Residuals[i];
            for (int a = 0; a < instrumentCount; a++)
                for (int b = 0; b < instrumentCount; b++) weight[a, b] += u * u * z[i, a] * z[i, b];
        }

        double[,] weightInverse = EconMath.Inverse(weight)
            ?? throw new ArgumentException("Моментные условия вырождены.", nameof(instruments));

        double[,] zt = LinearAlgebra.Transpose(z);
        double[,] ztx = LinearAlgebra.Multiply(zt, x);
        double[] zty = LinearAlgebra.Multiply(zt, response);

        double[,] xtz = LinearAlgebra.Transpose(ztx);
        double[,] left = LinearAlgebra.Multiply(xtz, weightInverse);
        double[,] bread = LinearAlgebra.Multiply(left, ztx);

        double[,] breadInverse = EconMath.Inverse(bread)
            ?? throw new ArgumentException("Матрица моментов вырождена.", nameof(instruments));

        double[] rhs = LinearAlgebra.Multiply(left, zty);
        double[] beta = LinearAlgebra.Multiply(breadInverse, rhs);

        var residuals = new double[n];
        for (int i = 0; i < n; i++)
        {
            double prediction = 0;
            for (int j = 0; j < k; j++) prediction += x[i, j] * beta[j];
            residuals[i] = response[i] - prediction;
        }

        // Статистика Хансена: n умножить на минимизированную квадратичную форму
        var moments = new double[instrumentCount];
        for (int a = 0; a < instrumentCount; a++)
            for (int i = 0; i < n; i++) moments[a] += z[i, a] * residuals[i];

        double hansen = LinearAlgebra.QuadraticForm(moments, weightInverse);
        int restrictions = instrumentCount - k;

        var names = new List<string> { "const" };
        for (int j = 0; j < exogenousCount; j++)
            names.Add(exogenousNames is not null && j < exogenousNames.Count ? exogenousNames[j] : $"w{j + 1}");
        for (int j = 0; j < endogenous.Width; j++)
            names.Add(endogenousNames is not null && j < endogenousNames.Count ? endogenousNames[j] : $"d{j + 1}");

        return first with
        {
            Model = "Обобщённый метод моментов",
            Coefficients = BuildCoefficients(names, beta, breadInverse, n - k),
            OveridentificationStatistic = hansen,
            OveridentificationPValue = restrictions > 0
                ? Distributions.ChiSquarePValue(hansen, restrictions)
                : 1,
            OveridentifyingRestrictions = Math.Max(0, restrictions),
            Residuals = LinearRegression.ToVector(residuals),
        };
    }

    /// <summary>Решает систему двухшагового МНК и возвращает ковариацию коэффициентов.</summary>
    private static (double[] Beta, double[,] Covariance, double[] Residuals) Solve(
        double[,] x, double[,] z, double[] y, bool robust)
    {
        int n = x.GetLength(0), k = x.GetLength(1);

        double[,] zt = LinearAlgebra.Transpose(z);
        double[,] ztz = LinearAlgebra.Multiply(zt, z);
        double[,] ztzInverse = EconMath.Inverse(ztz)
            ?? throw new ArgumentException("Матрица инструментов вырождена.", nameof(z));

        double[,] ztx = LinearAlgebra.Multiply(zt, x);
        double[] zty = LinearAlgebra.Multiply(zt, y);

        double[,] xtz = LinearAlgebra.Transpose(ztx);
        double[,] left = LinearAlgebra.Multiply(xtz, ztzInverse);
        double[,] bread = LinearAlgebra.Multiply(left, ztx);

        double[,] breadInverse = EconMath.Inverse(bread)
            ?? throw new ArgumentException(
                "Структурное уравнение не идентифицировано: проверьте инструменты.", nameof(z));

        double[] rhs = LinearAlgebra.Multiply(left, zty);
        double[] beta = LinearAlgebra.Multiply(breadInverse, rhs);

        // Остатки считаются по фактическим регрессорам, а не по спроецированным
        var residuals = new double[n];
        for (int i = 0; i < n; i++)
        {
            double prediction = 0;
            for (int j = 0; j < k; j++) prediction += x[i, j] * beta[j];
            residuals[i] = y[i] - prediction;
        }

        double[,] covariance;

        if (robust)
        {
            double[,] projection = LinearAlgebra.Multiply(LinearAlgebra.Multiply(z, ztzInverse), zt);
            var xHat = new double[n, k];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < k; j++)
                {
                    double sum = 0;
                    for (int t = 0; t < n; t++) sum += projection[i, t] * x[t, j];
                    xHat[i, j] = sum;
                }

            var meat = new double[k, k];
            for (int i = 0; i < n; i++)
            {
                double e2 = residuals[i] * residuals[i];
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) meat[a, b] += e2 * xHat[i, a] * xHat[i, b];
            }

            covariance = LinearAlgebra.Multiply(LinearAlgebra.Multiply(breadInverse, meat), breadInverse);
        }
        else
        {
            double rss = residuals.Sum(e => e * e);
            double sigmaSquared = rss / Math.Max(1, n - k);

            covariance = new double[k, k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++) covariance[a, b] = sigmaSquared * breadInverse[a, b];
        }

        return (beta, covariance, residuals);
    }

    /// <summary>Собирает записи о коэффициентах из оценок и ковариационной матрицы.</summary>
    private static IReadOnlyList<Coefficient> BuildCoefficients(
        IReadOnlyList<string> names, double[] beta, double[,] covariance, int df)
    {
        var coefficients = new List<Coefficient>(beta.Length);
        int degrees = Math.Max(1, df);

        for (int j = 0; j < beta.Length; j++)
        {
            double error = Math.Sqrt(Math.Max(covariance[j, j], 0));
            double t = error > 0 ? beta[j] / error : double.NaN;
            double p = Distributions.TPValue(t, degrees);

            coefficients.Add(new Coefficient(
                names[j], beta[j], error, double.IsNaN(t) ? 0 : t, double.IsNaN(p) ? 1 : p,
                beta[j] - (1.96 * error), beta[j] + (1.96 * error)));
        }

        return coefficients;
    }

    /// <summary>F-статистики исключённых инструментов по каждому эндогенному регрессору.</summary>
    private static IReadOnlyList<FirstStage> FirstStageDiagnostics(
        double[,] z, Matrix endogenous, int exogenousCount, IReadOnlyList<string>? names)
    {
        int n = z.GetLength(0);
        int total = z.GetLength(1);
        int excluded = total - 1 - exogenousCount;
        var stages = new List<FirstStage>(endogenous.Width);

        for (int e = 0; e < endogenous.Width; e++)
        {
            var response = new double[n];
            for (int i = 0; i < n; i++) response[i] = endogenous[i, e];

            var full = new List<string>();
            for (int j = 0; j < total; j++) full.Add($"z{j}");

            RegressionResult unrestricted = LinearRegression.FitDesign(
                z, response, full, new RegressionOptions { AddIntercept = false }, "первая ступень");

            var restricted = new double[n, 1 + exogenousCount];
            var restrictedNames = new List<string> { "const" };
            for (int j = 0; j < exogenousCount; j++) restrictedNames.Add($"w{j + 1}");

            for (int i = 0; i < n; i++)
                for (int j = 0; j < 1 + exogenousCount; j++) restricted[i, j] = z[i, j];

            RegressionResult baseline = LinearRegression.FitDesign(
                restricted, response, restrictedNames,
                new RegressionOptions { AddIntercept = false }, "без инструментов");

            double rssFull = 0, rssBase = 0;
            for (int i = 0; i < n; i++)
            {
                rssFull += unrestricted.Residuals[i] * unrestricted.Residuals[i];
                rssBase += baseline.Residuals[i] * baseline.Residuals[i];
            }

            int df = n - total;
            double f = excluded > 0 && df > 0 && rssFull > 0
                ? (rssBase - rssFull) / excluded / (rssFull / df)
                : 0;

            double partial = rssBase > 0 ? (rssBase - rssFull) / rssBase : 0;

            stages.Add(new FirstStage(
                names is not null && e < names.Count ? names[e] : $"d{e + 1}",
                f, Distributions.FPValue(f, Math.Max(1, excluded), Math.Max(1, df)), partial));
        }

        return stages;
    }

    /// <summary>Статистика Саргана на сверхидентифицирующие ограничения.</summary>
    private static double Sargan(double[,] z, double[] residuals)
    {
        int n = z.GetLength(0), m = z.GetLength(1);
        var names = new List<string>();
        for (int j = 0; j < m; j++) names.Add($"z{j}");

        RegressionResult auxiliary = LinearRegression.FitDesign(
            z, residuals, names, new RegressionOptions { AddIntercept = false }, "Сарган");

        return n * auxiliary.RSquared;
    }

    /// <summary>Тест Хаусмана на эндогенность регрессоров.</summary>
    private static double Hausman(
        IReadOnlyList<Coefficient> iv, IReadOnlyList<Coefficient> ols, out double pValue)
    {
        double statistic = 0;
        int df = 0;

        for (int j = 0; j < iv.Count && j < ols.Count; j++)
        {
            if (iv[j].Name == "const") continue;

            double difference = iv[j].Estimate - ols[j].Estimate;
            double variance = (iv[j].StandardError * iv[j].StandardError)
                - (ols[j].StandardError * ols[j].StandardError);

            if (variance <= 1e-18) continue;

            statistic += difference * difference / variance;
            df++;
        }

        pValue = df > 0 ? Distributions.ChiSquarePValue(statistic, df) : 1;
        return statistic;
    }
}
