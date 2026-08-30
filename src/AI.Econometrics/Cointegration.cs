using System;
using System.Collections.Generic;
using System.Linq;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Econometrics;

/// <summary>Строка таблицы теста Йохансена для одного предполагаемого ранга.</summary>
/// <param name="Rank">Проверяемое число коинтеграционных соотношений.</param>
/// <param name="Eigenvalue">Соответствующее собственное число.</param>
/// <param name="TraceStatistic">Статистика следа.</param>
/// <param name="TraceCritical">Критическое значение статистики следа на уровне 5%.</param>
/// <param name="MaxEigenStatistic">Статистика максимального собственного числа.</param>
/// <param name="MaxEigenCritical">Критическое значение на уровне 5%.</param>
public sealed record JohansenRow(
    int Rank, double Eigenvalue, double TraceStatistic, double TraceCritical,
    double MaxEigenStatistic, double MaxEigenCritical)
{
    /// <summary>Отвергается ли гипотеза о данном ранге по статистике следа.</summary>
    public bool TraceRejected => TraceStatistic > TraceCritical;

    /// <summary>Отвергается ли гипотеза по статистике максимального собственного числа.</summary>
    public bool MaxEigenRejected => MaxEigenStatistic > MaxEigenCritical;
}

/// <summary>Результат теста Йохансена на коинтеграцию.</summary>
public sealed record JohansenResult : IInterpretable
{
    /// <summary>Названия переменных.</summary>
    public IReadOnlyList<string> Variables { get; init; } = [];

    /// <summary>Таблица теста по возрастанию ранга.</summary>
    public IReadOnlyList<JohansenRow> Rows { get; init; } = [];

    /// <summary>Коинтеграционные векторы по столбцам, нормированные на первую переменную.</summary>
    public Matrix CointegratingVectors { get; init; } = new(1, 1);

    /// <summary>Определённый по статистике следа ранг коинтеграции.</summary>
    public int Rank { get; init; }

    /// <summary>Число лагов в разностях.</summary>
    public int Lags { get; init; }

    /// <summary>Число использованных наблюдений.</summary>
    public int Observations { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int k = Variables.Count;
        bool cointegrated = Rank is > 0 && Rank < k;

        var builder = new InterpretationBuilder("Тест Йохансена на коинтеграцию")
            .Summary($"Система из {k} переменных, {Lags} лагов в разностях, " +
                     $"{Observations} наблюдений. Статистика следа указывает на ранг {Rank}: " +
                     (Rank == 0 ? "коинтеграции нет, ряды связаны только через разности."
                      : Rank == k ? "все ряды стационарны, коинтеграция как понятие неприменима."
                      : $"обнаружено {Rank} долгосрочных соотношений."))
            .Metric("Ранг коинтеграции", Rank, null,
                $"из {k} возможных",
                cointegrated ? MetricQuality.Good : MetricQuality.Neutral, 0)
            .Metric("Лагов", Lags, null, $"наблюдений {Observations}", MetricQuality.Neutral, 0);

        foreach (JohansenRow row in Rows)
        {
            builder.Metric($"r ≤ {row.Rank}", row.TraceStatistic, null,
                $"собственное число {Fmt.Num(row.Eigenvalue, 4)}, критическое " +
                $"{Fmt.Num(row.TraceCritical, 2)}; " +
                (row.TraceRejected ? "гипотеза отвергается" : "гипотеза не отвергается"),
                row.TraceRejected ? MetricQuality.Good : MetricQuality.Neutral, 2);
        }

        if (Rank > 0 && CointegratingVectors.Width > 0)
        {
            for (int j = 0; j < k && j < CointegratingVectors.Height; j++)
            {
                builder.Metric($"β: {Variables[j]}", CointegratingVectors[j, 0], null,
                    "первый коинтеграционный вектор, нормированный на первую переменную",
                    MetricQuality.Unknown, 4);
            }
        }

        return builder
            .FindingIf(cointegrated,
                $"Найдено {Rank} коинтеграционных соотношений. Ряды нестационарны " +
                "по отдельности, но их линейная комбинация стационарна: между " +
                "переменными есть долгосрочное равновесие, к которому система возвращается.")
            .FindingIf(Rank == 0,
                "Коинтеграции не обнаружено. Регрессию в уровнях строить нельзя — " +
                "она даст ложную значимость; работать нужно с разностями.")
            .FindingIf(Rank == k,
                "Ранг равен числу переменных: все ряды стационарны, и достаточно " +
                "обычной векторной авторегрессии в уровнях.")
            .Finding("Коинтеграция — это утверждение о долгосрочной связи, а не о " +
                     "причинности. Направление связи и скорость возврата к равновесию " +
                     "определяются моделью коррекции ошибками.")
            .WarningIf(Observations < 50,
                $"Всего {Observations} наблюдений. Асимптотические критические значения " +
                "на коротких рядах занижают ранг: тест склонен не находить коинтеграцию.")
            .WarningIf(Lags > 4,
                $"Использовано {Lags} лагов. Каждый лаг добавляет по числу переменных " +
                "параметров в каждое уравнение и быстро исчерпывает степени свободы.")
            .Warning("Критические значения приведены для спецификации с ограниченной " +
                     "константой в коинтеграционном соотношении. При другом составе " +
                     "детерминированной части пороги отличаются, и вывод о ранге может измениться.")
            .Recommendation("Проверьте порядок интегрирования каждого ряда до теста: " +
                            "коинтеграция определена для рядов одного порядка, обычно первого.")
            .Recommendation("Найденный ранг подставляйте в модель коррекции ошибками — " +
                            "именно она превращает факт коинтеграции в оценку скорости " +
                            "возврата к равновесию.")
            .Build();
    }
}

/// <summary>Результат оценивания модели коррекции ошибками.</summary>
public sealed record VecmResult : IInterpretable
{
    /// <summary>Названия переменных.</summary>
    public IReadOnlyList<string> Variables { get; init; } = [];

    /// <summary>Ранг коинтеграции.</summary>
    public int Rank { get; init; }

    /// <summary>Коэффициенты приспособления: строка — уравнение, столбец — соотношение.</summary>
    public Matrix Adjustment { get; init; } = new(1, 1);

    /// <summary>Коинтеграционные векторы: строка — переменная, столбец — соотношение.</summary>
    public Matrix Cointegrating { get; init; } = new(1, 1);

    /// <summary>Значимость коэффициентов приспособления.</summary>
    public IReadOnlyList<Coefficient> AdjustmentCoefficients { get; init; } = [];

    /// <summary>Ряд отклонений от долгосрочного равновесия.</summary>
    public Vector EquilibriumError { get; init; } = new(0);

    /// <summary>Число лагов в разностях.</summary>
    public int Lags { get; init; }

    /// <summary>Число использованных наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Период полувозврата к равновесию по самому быстрому уравнению.</summary>
    public double HalfLife { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        Coefficient? fastest = AdjustmentCoefficients
            .OrderBy(c => c.Estimate)
            .FirstOrDefault();

        var significant = AdjustmentCoefficients.Where(c => c.IsSignificant).ToList();

        var result = new InterpretationBuilder("Модель коррекции ошибками")
            .Summary($"Система из {Variables.Count} переменных с рангом коинтеграции {Rank} " +
                     $"и {Lags} лагами в разностях, {Observations} наблюдений. " +
                     (double.IsFinite(HalfLife)
                         ? $"Половина отклонения от равновесия устраняется за {Fmt.Num(HalfLife, 1)} периодов. "
                         : "Скорость возврата к равновесию не определена. ") +
                     $"Значимо приспосабливаются {significant.Count} из {AdjustmentCoefficients.Count} уравнений.")
            .Metric("Ранг коинтеграции", Rank, null,
                $"из {Variables.Count} переменных", MetricQuality.Neutral, 0)
            .Metric("Период полувозврата", double.IsFinite(HalfLife) ? HalfLife : 0, "периодов",
                "скорость устранения отклонения от равновесия",
                double.IsFinite(HalfLife) && HalfLife < 10 ? MetricQuality.Good : MetricQuality.Neutral, 1)
            .Metric("Приспосабливающихся уравнений", significant.Count, null,
                $"из {AdjustmentCoefficients.Count}",
                significant.Count > 0 ? MetricQuality.Good : MetricQuality.Warning, 0);

        foreach (Coefficient coefficient in AdjustmentCoefficients)
        {
            result.Metric($"α: {coefficient.Name}", coefficient.Estimate, null,
                $"ст. ошибка {Fmt.Num(coefficient.StandardError, 4)}, p = {Fmt.Num(coefficient.PValue, 4)}; " +
                (coefficient.Estimate < 0 ? "уравнение возвращает систему к равновесию"
                    : "уравнение отталкивает систему от равновесия"),
                coefficient.IsSignificant && coefficient.Estimate < 0
                    ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        for (int j = 0; j < Variables.Count && j < Cointegrating.Height; j++)
        {
            result.Metric($"β: {Variables[j]}", Cointegrating[j, 0], null,
                "долгосрочное соотношение, нормированное на первую переменную",
                MetricQuality.Unknown, 4);
        }

        return result
            .FindingIf(fastest is not null && fastest.Estimate < 0,
                $"Быстрее всего к равновесию возвращается «{fastest?.Name}»: коэффициент " +
                $"приспособления {Fmt.Num(fastest?.Estimate ?? 0, 4)}. Отрицательный знак " +
                "и означает возврат: при отклонении вверх переменная снижается.")
            .Finding("Модель разделяет краткосрочную динамику и долгосрочное равновесие. " +
                     "Коэффициент приспособления показывает, какая доля отклонения " +
                     "устраняется за период, а коинтеграционный вектор — само равновесие.")
            .FindingIf(significant.Count == 1,
                "Приспосабливается только одно уравнение. Это означает слабую экзогенность " +
                "остальных переменных: они формируют равновесие, но сами к нему не подстраиваются.")
            .WarningIf(significant.Count == 0,
                "Ни один коэффициент приспособления не значим. Формально коинтеграция " +
                "найдена, но система к равновесию не возвращается — вывод о долгосрочной " +
                "связи ненадёжен.")
            .WarningIf(AdjustmentCoefficients.Any(c => c.Estimate > 0 && c.IsSignificant),
                "Есть значимый положительный коэффициент приспособления: соответствующее " +
                "уравнение усиливает отклонение. Обычно это признак неверной нормировки " +
                "коинтеграционного вектора или переоценённого ранга.")
            .Warning("Стандартные ошибки коэффициентов приспособления посчитаны при " +
                     "фиксированном коинтеграционном векторе. Неопределённость его оценки " +
                     "в них не учтена, поэтому значимость слегка завышена.")
            .Recommendation("Смотрите на ряд отклонений от равновесия: он должен выглядеть " +
                            "стационарным и колебаться вокруг нуля. Если у него виден тренд, " +
                            "ранг коинтеграции завышен.")
            .Build();
    }
}

/// <summary>
/// Коинтеграция: тест Йохансена и модель коррекции ошибками.
/// </summary>
/// <remarks>
/// <para>
/// Нестационарные ряды могут быть связаны долгосрочным равновесием: каждый из
/// них блуждает, но их линейная комбинация стационарна. Тест Йохансена
/// определяет число таких соотношений через ранг матрицы <c>Pi</c> в модели
/// </para>
/// <code>
/// d y_t = Pi * y_{t-1} + sum_i Gamma_i * d y_{t-i} + mu + e_t,
/// Pi = alpha * beta'
/// </code>
/// <para>
/// Ранг находится решением обобщённой задачи на собственные значения для
/// матриц вторых моментов остатков двух вспомогательных регрессий. Собственные
/// числа дают статистику следа и статистику максимального собственного числа,
/// которые сравниваются с табличными критическими значениями.
/// </para>
/// <para>
/// Найденный ранг превращает VAR в модель коррекции ошибками. Матрица
/// <c>beta</c> описывает само равновесие, матрица <c>alpha</c> — скорость
/// возврата к нему: отрицательный коэффициент означает, что при отклонении
/// вверх переменная начинает снижаться.
/// </para>
/// </remarks>
public static class Cointegration
{
    private static readonly double[] TraceCritical = [9.24, 19.96, 34.91, 53.12, 76.07, 102.14];
    private static readonly double[] MaxEigenCritical = [9.24, 15.67, 22.00, 28.14, 34.40, 40.30];

    /// <summary>Проводит тест Йохансена на число коинтеграционных соотношений.</summary>
    /// <param name="data">Ряды: строка — наблюдение, столбец — переменная.</param>
    /// <param name="lags">Число лагов в разностях.</param>
    /// <param name="names">Названия переменных.</param>
    /// <returns>Таблица теста, ранг и коинтеграционные векторы.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно.</exception>
    public static JohansenResult Johansen(Matrix data, int lags = 1, IReadOnlyList<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfLessThan(lags, 1);

        int t = data.Height, k = data.Width;
        int rows = t - lags - 1;

        if (rows <= (k * lags) + 2)
            throw new ArgumentException("Наблюдений недостаточно для теста.", nameof(data));
        if (k > TraceCritical.Length)
            throw new ArgumentException("Поддерживается не более шести переменных.", nameof(data));

        (double[,] r0, double[,] r1) = AuxiliaryResiduals(data, lags, rows);

        double[,] s00 = Moment(r0, r0, rows);
        double[,] s01 = Moment(r0, r1, rows);
        double[,] s11 = Moment(r1, r1, rows);

        double[,] s00Inverse = EconMath.Inverse(s00)
            ?? throw new ArgumentException("Матрица моментов вырождена.", nameof(data));

        double[,] s11Root = InverseSquareRoot(s11);
        double[,] s10 = LinearAlgebra.Transpose(s01);

        double[,] core = LinearAlgebra.Multiply(
            LinearAlgebra.Multiply(s11Root, LinearAlgebra.Multiply(s10, s00Inverse)),
            LinearAlgebra.Multiply(s01, s11Root));

        (double[] eigenvalues, double[,] eigenvectors) = LinearAlgebra.SymmetricEigen(core);

        var rowsTable = new List<JohansenRow>(k);
        int rank = 0;

        for (int r = 0; r < k; r++)
        {
            double trace = 0;
            for (int i = r; i < k; i++)
                trace += -rows * Math.Log(Math.Max(1 - Math.Clamp(eigenvalues[i], 0, 0.999999), 1e-12));

            double maxEigen = -rows * Math.Log(Math.Max(1 - Math.Clamp(eigenvalues[r], 0, 0.999999), 1e-12));

            int index = Math.Min(k - r - 1, TraceCritical.Length - 1);
            var row = new JohansenRow(
                r, Math.Clamp(eigenvalues[r], 0, 1), trace, TraceCritical[index],
                maxEigen, MaxEigenCritical[index]);

            rowsTable.Add(row);
            if (row.TraceRejected && rank == r) rank = r + 1;
        }

        double[,] beta = LinearAlgebra.Multiply(s11Root, eigenvectors);
        var vectors = new Matrix(k, k);

        for (int j = 0; j < k; j++)
        {
            double normalizer = Math.Abs(beta[0, j]) > 1e-12 ? beta[0, j] : 1;
            for (int i = 0; i < k; i++) vectors[i, j] = beta[i, j] / normalizer;
        }

        var labels = new List<string>(k);
        for (int j = 0; j < k; j++)
            labels.Add(names is not null && j < names.Count ? names[j] : $"y{j + 1}");

        return new JohansenResult
        {
            Variables = labels,
            Rows = rowsTable,
            CointegratingVectors = vectors,
            Rank = rank,
            Lags = lags,
            Observations = rows,
        };
    }

    /// <summary>Оценивает модель коррекции ошибками при заданном ранге.</summary>
    /// <param name="data">Ряды системы.</param>
    /// <param name="rank">Ранг коинтеграции; при нуле берётся из теста Йохансена.</param>
    /// <param name="lags">Число лагов в разностях.</param>
    /// <param name="names">Названия переменных.</param>
    /// <returns>Коэффициенты приспособления, долгосрочные соотношения и скорость возврата.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Ранг вне допустимого диапазона.</exception>
    public static VecmResult ErrorCorrection(
        Matrix data, int rank = 0, int lags = 1, IReadOnlyList<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        JohansenResult test = Johansen(data, lags, names);
        int r = rank > 0 ? rank : Math.Max(1, test.Rank);

        if (r >= data.Width)
            throw new ArgumentException("Ранг должен быть меньше числа переменных.", nameof(rank));

        int t = data.Height, k = data.Width;
        int rows = t - lags - 1;

        var errors = new Vector(rows);
        var adjustment = new Matrix(k, r);
        var coefficients = new List<Coefficient>(k);

        // Отклонение от равновесия по первому коинтеграционному вектору
        for (int i = 0; i < rows; i++)
        {
            double value = 0;
            for (int j = 0; j < k; j++) value += test.CointegratingVectors[j, 0] * data[i + lags, j];
            errors[i] = value;
        }

        int columns = r + (k * lags) + 1;
        var design = new double[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int c = 0; c < r; c++)
            {
                double value = 0;
                for (int j = 0; j < k; j++) value += test.CointegratingVectors[j, c] * data[i + lags, j];
                design[i, c] = value;
            }

            int column = r;
            for (int lag = 1; lag <= lags; lag++)
                for (int j = 0; j < k; j++)
                    design[i, column++] = data[i + lags + 1 - lag, j] - data[i + lags - lag, j];

            design[i, columns - 1] = 1;
        }

        var designNames = new List<string>();
        for (int c = 0; c < r; c++) designNames.Add($"ecm{c + 1}");
        for (int lag = 1; lag <= lags; lag++)
            for (int j = 0; j < k; j++) designNames.Add($"d{j}(-{lag})");
        designNames.Add("const");

        double fastest = 0;

        for (int equation = 0; equation < k; equation++)
        {
            var response = new double[rows];
            for (int i = 0; i < rows; i++)
                response[i] = data[i + lags + 1, equation] - data[i + lags, equation];

            RegressionResult fit = LinearRegression.FitDesign(
                design, response, designNames,
                new RegressionOptions { AddIntercept = false, Variance = RobustVariance.Hc1 },
                "VECM");

            for (int c = 0; c < r; c++) adjustment[equation, c] = fit.Coefficients[c].Estimate;

            coefficients.Add(fit.Coefficients[0] with
            {
                Name = names is not null && equation < names.Count ? names[equation] : $"y{equation + 1}",
            });

            fastest = Math.Min(fastest, fit.Coefficients[0].Estimate);
        }

        double halfLife = fastest < 0 && fastest > -1
            ? Math.Log(0.5) / Math.Log(1 + fastest)
            : double.PositiveInfinity;

        var cointegrating = new Matrix(k, r);
        for (int j = 0; j < k; j++)
            for (int c = 0; c < r; c++) cointegrating[j, c] = test.CointegratingVectors[j, c];

        return new VecmResult
        {
            Variables = test.Variables,
            Rank = r,
            Adjustment = adjustment,
            Cointegrating = cointegrating,
            AdjustmentCoefficients = coefficients,
            EquilibriumError = errors,
            Lags = lags,
            Observations = rows,
            HalfLife = halfLife,
        };
    }

    /// <summary>Остатки вспомогательных регрессий на лаги разностей и константу.</summary>
    private static (double[,] R0, double[,] R1) AuxiliaryResiduals(Matrix data, int lags, int rows)
    {
        int k = data.Width;
        int columns = (k * lags) + 1;

        var controls = new double[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            int column = 0;
            for (int lag = 1; lag <= lags; lag++)
                for (int j = 0; j < k; j++)
                    controls[i, column++] = data[i + lags + 1 - lag, j] - data[i + lags - lag, j];

            controls[i, columns - 1] = 1;
        }

        var names = new List<string>();
        for (int j = 0; j < columns; j++) names.Add($"c{j}");

        var r0 = new double[rows, k];
        var r1 = new double[rows, k];
        var options = new RegressionOptions { AddIntercept = false, Ridge = 1e-9 };

        for (int j = 0; j < k; j++)
        {
            var differences = new double[rows];
            var levels = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                differences[i] = data[i + lags + 1, j] - data[i + lags, j];
                levels[i] = data[i + lags, j];
            }

            RegressionResult fitDifference = LinearRegression.FitDesign(
                controls, differences, names, options, "вспомогательная");
            RegressionResult fitLevel = LinearRegression.FitDesign(
                controls, levels, names, options, "вспомогательная");

            for (int i = 0; i < rows; i++)
            {
                r0[i, j] = fitDifference.Residuals[i];
                r1[i, j] = fitLevel.Residuals[i];
            }
        }

        return (r0, r1);
    }

    /// <summary>Матрица вторых моментов двух наборов остатков.</summary>
    private static double[,] Moment(double[,] left, double[,] right, int rows)
    {
        int a = left.GetLength(1), b = right.GetLength(1);
        var moment = new double[a, b];

        for (int i = 0; i < a; i++)
            for (int j = 0; j < b; j++)
            {
                double sum = 0;
                for (int t = 0; t < rows; t++) sum += left[t, i] * right[t, j];
                moment[i, j] = sum / rows;
            }

        return moment;
    }

    /// <summary>Обратный квадратный корень симметричной положительно определённой матрицы.</summary>
    private static double[,] InverseSquareRoot(double[,] matrix)
    {
        // Спектральная функция матрицы берётся из ядра: та же ортогонализация по Лёвдину,
        // что и в квантовой химии. Малые собственные числа здесь не отвергаются, а
        // подрезаются: матрица моментов бывает почти вырожденной, и тест должен доводиться
        // до конца, а не падать.
        int n = matrix.GetLength(0);
        var source = new Matrix(n, n);

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                source[i, j] = matrix[i, j];

        Matrix root = Eigen.SymmetricFunction(source, value => 1.0 / Math.Sqrt(Math.Max(value, 1e-12)));

        var result = new double[n, n];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                result[i, j] = root[i, j];

        return result;
    }
}
