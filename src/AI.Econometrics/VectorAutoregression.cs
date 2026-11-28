using System;
using System.Collections.Generic;
using System.Linq;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Econometrics;

/// <summary>Результат проверки причинности по Гренджеру.</summary>
/// <param name="From">Переменная-причина.</param>
/// <param name="To">Переменная-следствие.</param>
/// <param name="FStatistic">Статистика Фишера.</param>
/// <param name="PValue">Уровень значимости.</param>
public sealed record GrangerTest(string From, string To, double FStatistic, double PValue)
{
    /// <summary>Отвергается ли отсутствие причинности на уровне 5%.</summary>
    public bool Causes => PValue < 0.05;
}

/// <summary>Результат оценивания векторной авторегрессии.</summary>
public sealed record VarResult : IInterpretable
{
    /// <summary>Названия переменных системы.</summary>
    public IReadOnlyList<string> Variables { get; init; } = [];

    /// <summary>Порядок модели.</summary>
    public int Order { get; init; }

    /// <summary>Коэффициенты уравнений: строка — уравнение, столбец — регрессор.</summary>
    public Matrix Coefficients { get; init; } = new(1, 1);

    /// <summary>Ковариационная матрица остатков.</summary>
    public Matrix ResidualCovariance { get; init; } = new(1, 1);

    /// <summary>Остатки уравнений: строка — наблюдение, столбец — переменная.</summary>
    public Matrix Residuals { get; init; } = new(1, 1);

    /// <summary>Логарифм правдоподобия.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Информационный критерий Акаике.</summary>
    public double Aic { get; init; }

    /// <summary>Информационный критерий Шварца.</summary>
    public double Bic { get; init; }

    /// <summary>Максимальный модуль корня характеристического уравнения.</summary>
    public double SpectralRadius { get; init; }

    /// <summary>Тесты причинности по Гренджеру для всех пар переменных.</summary>
    public IReadOnlyList<GrangerTest> Granger { get; init; } = [];

    /// <summary>Число использованных наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Устойчива ли система.</summary>
    public bool IsStable => SpectralRadius < 1;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var causal = Granger.Where(g => g.Causes).ToList();
        GrangerTest? strongest = Granger.OrderBy(g => g.PValue).FirstOrDefault();

        var builder = new InterpretationBuilder($"Векторная авторегрессия VAR({Order})")
            .Summary($"Система из {Variables.Count} переменных, порядок {Order}, " +
                     $"{Observations} наблюдений. Максимальный корень " +
                     $"{Fmt.Num(SpectralRadius, 3)} — система " +
                     $"{(IsStable ? "устойчива" : "неустойчива")}. " +
                     $"Причинность по Гренджеру обнаружена в {causal.Count} из {Granger.Count} пар.")
            .Metric("Порядок", Order, null, $"переменных {Variables.Count}", MetricQuality.Neutral, 0)
            .Metric("Максимальный корень", SpectralRadius, null,
                IsStable ? "все корни внутри единичного круга" : "есть корень вне единичного круга",
                IsStable ? MetricQuality.Good : MetricQuality.Critical, 3)
            .Metric("AIC", Aic, null, $"BIC {Fmt.Num(Bic, 1)}", MetricQuality.Neutral, 1)
            .Metric("Причинных связей", causal.Count, null,
                $"из {Granger.Count} проверенных пар", MetricQuality.Neutral, 0);

        foreach (GrangerTest test in Granger)
        {
            builder.Metric($"{test.From} → {test.To}", test.FStatistic, null,
                $"p = {Fmt.Num(test.PValue, 4)}; " +
                (test.Causes ? "лаги улучшают прогноз" : "лаги прогноз не улучшают"),
                test.Causes ? MetricQuality.Good : MetricQuality.Neutral, 2);
        }

        return builder
            .FindingIf(strongest is not null,
                $"Сильнее всего выражена связь «{strongest?.From} → {strongest?.To}» " +
                $"(p = {Fmt.Num(strongest?.PValue ?? 1, 4)}).")
            .Finding("Причинность по Гренджеру означает только улучшение прогноза: лаги одной " +
                     "переменной помогают предсказывать другую. Это предсказательная, " +
                     "а не структурная причинность — обе переменные могут реагировать " +
                     "на общий пропущенный фактор.")
            .FindingIf(IsStable,
                $"Максимальный корень {Fmt.Num(SpectralRadius, 3)} меньше единицы: реакция " +
                "системы на шок затухает, и импульсные отклики сходятся.")
            .WarningIf(!IsStable,
                $"Максимальный корень {Fmt.Num(SpectralRadius, 3)} не меньше единицы. " +
                "Импульсные отклики не затухают, а система, скорее всего, содержит " +
                "нестационарные ряды — проверьте порядок интегрирования и коинтеграцию.")
            .WarningIf(Observations < 10 * Variables.Count * Order,
                $"Параметров в системе {Variables.Count * ((Variables.Count * Order) + 1)} " +
                $"при {Observations} наблюдениях. VAR быстро переопределяется: " +
                "каждая дополнительная переменная добавляет по порядку параметров в каждое уравнение.")
            .Warning("Импульсные отклики зависят от порядка переменных при разложении " +
                     "Холецкого: он задаёт, кто на кого влияет мгновенно. Экономический " +
                     "смысл этого упорядочения нужно обосновывать отдельно.")
            .Recommendation("Выбирайте порядок модели по информационным критериям, а не " +
                            "по максимуму: лишние лаги съедают степени свободы быстрее, " +
                            "чем улучшают прогноз.")
            .Recommendation("Проверяйте стационарность рядов до оценивания. Для " +
                            "нестационарных, но коинтегрированных данных нужна модель " +
                            "коррекции ошибками, а не VAR в разностях.")
            .Build();
    }
}

/// <summary>
/// Векторная авторегрессия: совместная динамика нескольких рядов, причинность
/// по Гренджеру, импульсные отклики и разложение дисперсии прогноза.
/// </summary>
/// <remarks>
/// <para>
/// Модель описывает каждую переменную системы через лаги всех переменных:
/// </para>
/// <code>
/// y_t = c + A_1 y_{t-1} + ... + A_p y_{t-p} + e_t
/// </code>
/// <para>
/// Каждое уравнение оценивается обычным МНК — регрессоры одинаковы для всех
/// уравнений, поэтому системная оценка совпадает с поуравненной.
/// </para>
/// <para>
/// Импульсные отклики показывают реакцию системы на единичный шок. Поскольку
/// шоки коррелированы между собой, они ортогонализуются разложением Холецкого
/// ковариационной матрицы остатков. Порядок переменных при этом становится
/// содержательным предположением: первая переменная влияет на остальные
/// мгновенно, последняя — только с лагом.
/// </para>
/// <para>
/// Устойчивость проверяется по максимальному модулю корня характеристического
/// уравнения: он должен быть меньше единицы, иначе отклики не затухают.
/// </para>
/// </remarks>
public static class VectorAutoregression
{
    /// <summary>Оценивает векторную авторегрессию.</summary>
    /// <param name="data">Ряды: строка — наблюдение, столбец — переменная.</param>
    /// <param name="order">Порядок модели.</param>
    /// <param name="names">Названия переменных.</param>
    /// <returns>Коэффициенты, остатки, критерии и тесты причинности.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно для выбранного порядка.</exception>
    public static VarResult Fit(Matrix data, int order = 1, IReadOnlyList<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);

        int t = data.Height, k = data.Width;
        int rows = t - order;
        int regressors = 1 + (k * order);

        if (rows <= regressors)
            throw new ArgumentException("Наблюдений недостаточно для выбранного порядка.", nameof(order));

        var labels = new List<string>(k);
        for (int j = 0; j < k; j++)
            labels.Add(names is not null && j < names.Count ? names[j] : $"y{j + 1}");

        var design = new double[rows, regressors];
        for (int i = 0; i < rows; i++)
        {
            design[i, 0] = 1;
            for (int lag = 1; lag <= order; lag++)
                for (int j = 0; j < k; j++)
                    design[i, 1 + ((lag - 1) * k) + j] = data[i + order - lag, j];
        }

        var coefficients = new Matrix(k, regressors);
        var residuals = new Matrix(rows, k);

        var regressorNames = new List<string> { "const" };
        for (int lag = 1; lag <= order; lag++)
            for (int j = 0; j < k; j++) regressorNames.Add($"{labels[j]}(-{lag})");

        for (int equation = 0; equation < k; equation++)
        {
            var response = new double[rows];
            for (int i = 0; i < rows; i++) response[i] = data[i + order, equation];

            RegressionResult fit = LinearRegression.FitDesign(
                design, response, regressorNames,
                new RegressionOptions { AddIntercept = false }, $"уравнение {labels[equation]}");

            for (int j = 0; j < regressors; j++) coefficients[equation, j] = fit.Coefficients[j].Estimate;
            for (int i = 0; i < rows; i++) residuals[i, equation] = fit.Residuals[i];
        }

        var covariance = new Matrix(k, k);
        for (int a = 0; a < k; a++)
            for (int b = 0; b < k; b++)
            {
                double sum = 0;
                for (int i = 0; i < rows; i++) sum += residuals[i, a] * residuals[i, b];
                covariance[a, b] = sum / rows;
            }

        double determinant = Determinant(covariance);
        double logLikelihood = -0.5 * rows * ((k * Math.Log(2 * Math.PI)) + Math.Log(Math.Max(determinant, 1e-300)) + k);
        int parameters = k * regressors;

        return new VarResult
        {
            Variables = labels,
            Order = order,
            Coefficients = coefficients,
            ResidualCovariance = covariance,
            Residuals = residuals,
            LogLikelihood = logLikelihood,
            Aic = (-2 * logLikelihood) + (2 * parameters),
            Bic = (-2 * logLikelihood) + (parameters * Math.Log(rows)),
            SpectralRadius = Eigen.SpectralRadius(Companion(coefficients, order)),
            Granger = GrangerTests(data, order, labels),
            Observations = rows,
        };
    }

    /// <summary>Подбирает порядок модели по информационному критерию.</summary>
    /// <param name="data">Ряды системы.</param>
    /// <param name="maxOrder">Максимальный проверяемый порядок.</param>
    /// <param name="names">Названия переменных.</param>
    /// <param name="useBic">Использовать критерий Шварца вместо Акаике.</param>
    /// <returns>Модель с наилучшим значением критерия.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static VarResult SelectOrder(
        Matrix data, int maxOrder = 6, IReadOnlyList<string>? names = null, bool useBic = false)
    {
        ArgumentNullException.ThrowIfNull(data);

        VarResult? best = null;

        for (int order = 1; order <= maxOrder; order++)
        {
            VarResult candidate;
            try { candidate = Fit(data, order, names); }
            catch (ArgumentException) { break; }

            double criterion = useBic ? candidate.Bic : candidate.Aic;
            double reference = best is null ? double.MaxValue : useBic ? best.Bic : best.Aic;

            if (criterion < reference) best = candidate;
        }

        return best ?? Fit(data, 1, names);
    }

    /// <summary>Ортогонализованные импульсные отклики.</summary>
    /// <param name="result">Оценённая модель.</param>
    /// <param name="horizon">Горизонт отклика.</param>
    /// <returns>Отклики: первый индекс — шок, второй — переменная, третий — период.</returns>
    /// <exception cref="ArgumentNullException">Модель не задана.</exception>
    public static double[][][] ImpulseResponse(VarResult result, int horizon = 20)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfLessThan(horizon, 1);

        int k = result.Variables.Count, p = result.Order;
        double[,] chol = LinearAlgebra.Cholesky(LinearRegression.ToArray(result.ResidualCovariance));

        // Матрицы psi рекуррентно: psi_0 = I, psi_h = sum_i A_i psi_{h-i}
        var psi = new List<double[,]> { LinearAlgebra.Identity(k) };

        for (int h = 1; h < horizon; h++)
        {
            var current = new double[k, k];

            for (int lag = 1; lag <= Math.Min(p, h); lag++)
            {
                double[,] a = LagMatrix(result, lag, k);
                double[,] previous = psi[h - lag];

                for (int i = 0; i < k; i++)
                    for (int j = 0; j < k; j++)
                    {
                        double sum = 0;
                        for (int m = 0; m < k; m++) sum += a[i, m] * previous[m, j];
                        current[i, j] += sum;
                    }
            }

            psi.Add(current);
        }

        var responses = new double[k][][];
        for (int shock = 0; shock < k; shock++)
        {
            responses[shock] = new double[k][];
            for (int variable = 0; variable < k; variable++)
            {
                responses[shock][variable] = new double[horizon];

                for (int h = 0; h < horizon; h++)
                {
                    double sum = 0;
                    for (int m = 0; m < k; m++) sum += psi[h][variable, m] * chol[m, shock];
                    responses[shock][variable][h] = sum;
                }
            }
        }

        return responses;
    }

    /// <summary>Разложение дисперсии ошибки прогноза по источникам шоков.</summary>
    /// <param name="result">Оценённая модель.</param>
    /// <param name="horizon">Горизонт разложения.</param>
    /// <returns>Матрица долей: строка — переменная, столбец — источник шока.</returns>
    /// <exception cref="ArgumentNullException">Модель не задана.</exception>
    public static Matrix VarianceDecomposition(VarResult result, int horizon = 20)
    {
        ArgumentNullException.ThrowIfNull(result);

        int k = result.Variables.Count;
        double[][][] responses = ImpulseResponse(result, horizon);
        var decomposition = new Matrix(k, k);

        for (int variable = 0; variable < k; variable++)
        {
            var contributions = new double[k];
            double total = 0;

            for (int shock = 0; shock < k; shock++)
            {
                double sum = 0;
                for (int h = 0; h < horizon; h++)
                    sum += responses[shock][variable][h] * responses[shock][variable][h];

                contributions[shock] = sum;
                total += sum;
            }

            for (int shock = 0; shock < k; shock++)
                decomposition[variable, shock] = total > 0 ? contributions[shock] / total : 0;
        }

        return decomposition;
    }

    /// <summary>Матрица коэффициентов при заданном лаге.</summary>
    private static double[,] LagMatrix(VarResult result, int lag, int k)
    {
        var matrix = new double[k, k];

        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++)
                matrix[i, j] = result.Coefficients[i, 1 + ((lag - 1) * k) + j];

        return matrix;
    }

    /// <summary>Тесты причинности по Гренджеру для всех упорядоченных пар.</summary>
    private static IReadOnlyList<GrangerTest> GrangerTests(
        Matrix data, int order, IReadOnlyList<string> names)
    {
        int t = data.Height, k = data.Width;
        int rows = t - order;
        var tests = new List<GrangerTest>();

        if (k < 2) return tests;

        for (int target = 0; target < k; target++)
        {
            var response = new double[rows];
            for (int i = 0; i < rows; i++) response[i] = data[i + order, target];

            int fullColumns = 1 + (k * order);
            var full = new double[rows, fullColumns];

            for (int i = 0; i < rows; i++)
            {
                full[i, 0] = 1;
                for (int lag = 1; lag <= order; lag++)
                    for (int j = 0; j < k; j++)
                        full[i, 1 + ((lag - 1) * k) + j] = data[i + order - lag, j];
            }

            var fullNames = new List<string>();
            for (int j = 0; j < fullColumns; j++) fullNames.Add($"c{j}");

            RegressionResult unrestricted = LinearRegression.FitDesign(
                full, response, fullNames, new RegressionOptions { AddIntercept = false }, "полная");

            double rssFull = Diagnostics.Rss(unrestricted.Residuals);

            for (int source = 0; source < k; source++)
            {
                if (source == target) continue;

                int restrictedColumns = fullColumns - order;
                var restricted = new double[rows, restrictedColumns];

                for (int i = 0; i < rows; i++)
                {
                    restricted[i, 0] = 1;
                    int column = 1;

                    for (int lag = 1; lag <= order; lag++)
                        for (int j = 0; j < k; j++)
                        {
                            if (j == source) continue;
                            restricted[i, column++] = data[i + order - lag, j];
                        }
                }

                var restrictedNames = new List<string>();
                for (int j = 0; j < restrictedColumns; j++) restrictedNames.Add($"c{j}");

                RegressionResult limited = LinearRegression.FitDesign(
                    restricted, response, restrictedNames,
                    new RegressionOptions { AddIntercept = false }, "ограниченная");

                double rssRestricted = Diagnostics.Rss(limited.Residuals);
                int df = rows - fullColumns;

                double f = df > 0 && rssFull > 0
                    ? (rssRestricted - rssFull) / order / (rssFull / df)
                    : 0;

                tests.Add(new GrangerTest(
                    names[source], names[target], f,
                    Distributions.FPValue(f, order, Math.Max(1, df))));
            }
        }

        return tests;
    }

    /// <summary>
    /// Сопровождающая матрица модели: её собственные числа — корни характеристического уравнения.
    /// </summary>
    /// <remarks>
    /// Модель порядка <c>p</c> по <c>k</c> переменным записывается как модель первого порядка
    /// по вектору из <c>k·p</c> значений: верхние <c>k</c> строк — коэффициенты при лагах,
    /// ниже — единичный сдвиг. Система устойчива, когда все собственные числа этой матрицы
    /// по модулю меньше единицы; считает их <see cref="Eigen.General"/>.
    /// </remarks>
    /// <param name="coefficients">
    /// Коэффициенты в раскладке <see cref="VarResult.Coefficients"/>: строка — уравнение,
    /// первый столбец — константа, дальше лаги подряд.
    /// </param>
    /// <param name="order">Порядок модели.</param>
    /// <returns>Квадратная матрица размера <c>k·p</c>.</returns>
    /// <exception cref="ArgumentNullException">Коэффициенты не заданы.</exception>
    /// <exception cref="ArgumentException">Число столбцов не равно <c>1 + k·p</c>.</exception>
    public static Matrix Companion(Matrix coefficients, int order)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);

        int k = coefficients.Height;
        int size = k * order;

        if (coefficients.Width != 1 + size)
            throw new ArgumentException("Ожидается константа и k столбцов на каждый лаг.", nameof(coefficients));

        var companion = new Matrix(size, size);

        for (int i = 0; i < k; i++)
            for (int j = 0; j < size; j++)
                companion[i, j] = coefficients[i, 1 + j];

        for (int i = k; i < size; i++) companion[i, i - k] = 1;

        return companion;
    }

    /// <summary>Определитель матрицы через разложение Холецкого.</summary>
    private static double Determinant(Matrix matrix)
    {
        double[,] chol = LinearAlgebra.Cholesky(LinearRegression.ToArray(matrix));
        double logDeterminant = 0;

        for (int i = 0; i < chol.GetLength(0); i++)
            logDeterminant += 2 * Math.Log(Math.Max(chol[i, i], 1e-150));

        return Math.Exp(logDeterminant);
    }
}
