using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Psychology.Internal;
using AI.Statistics;

namespace AI.Psychology.Psychometrics;

/// <summary>Вращение факторного решения</summary>
public enum FactorRotation
{
    /// <summary>Без вращения: первый фактор забирает наибольшую дисперсию</summary>
    None,

    /// <summary>Варимакс Кайзера: ортогональное вращение к простой структуре</summary>
    Varimax,

    /// <summary>Промакс: косоугольное вращение, факторы могут коррелировать</summary>
    Promax
}

/// <summary>Решение эксплораторного факторного анализа</summary>
public sealed class FactorAnalysisResult : IInterpretable
{
    private readonly double[,] _loadings;
    private readonly double[,]? _factorCorrelations;
    private readonly double[] _communalities;
    private readonly double[] _eigenvalues;

    internal FactorAnalysisResult(
        double[,] loadings, double[,]? factorCorrelations, double[] communalities, double[] eigenvalues,
        FactorRotation rotation, int iterations, bool converged, bool heywood)
    {
        _loadings = loadings;
        _factorCorrelations = factorCorrelations;
        _communalities = communalities;
        _eigenvalues = eigenvalues;
        Rotation = rotation;
        Iterations = iterations;
        Converged = converged;
        HeywoodCase = heywood;
    }

    /// <summary>Число переменных</summary>
    public int Items => _loadings.GetLength(0);

    /// <summary>Число факторов</summary>
    public int Factors => _loadings.GetLength(1);

    /// <summary>Вращение</summary>
    public FactorRotation Rotation { get; }

    /// <summary>Число итераций уточнения общностей</summary>
    public int Iterations { get; }

    /// <summary>Сошлись ли общности</summary>
    public bool Converged { get; }

    /// <summary>Общность какой-то переменной дошла до единицы (случай Хейвуда) и была ограничена</summary>
    public bool HeywoodCase { get; }

    /// <summary>Нагрузка переменной на фактор; для промакса — коэффициент матрицы образца</summary>
    /// <param name="item">Переменная</param>
    /// <param name="factor">Фактор</param>
    public double Loading(int item, int factor) => _loadings[item, factor];

    /// <summary>Копия матрицы нагрузок: переменные по строкам, факторы по столбцам</summary>
    public double[,] Loadings => (double[,])_loadings.Clone();

    /// <summary>Корреляции факторов после промакса; null для ортогональных решений</summary>
    public double[,]? FactorCorrelations => (double[,]?)_factorCorrelations?.Clone();

    /// <summary>Общности: доля дисперсии переменной, объяснённая факторами</summary>
    public IReadOnlyList<double> Communalities => _communalities;

    /// <summary>Уникальности: 1 − общность</summary>
    public IReadOnlyList<double> Uniquenesses => _communalities.Select(h => 1 - h).ToArray();

    /// <summary>Собственные значения исходной корреляционной матрицы по убыванию</summary>
    public IReadOnlyList<double> Eigenvalues => _eigenvalues;

    /// <summary>Сумма квадратов нагрузок каждого фактора, делённая на число переменных</summary>
    public IReadOnlyList<double> VarianceExplained
        => Enumerable.Range(0, Factors).Select(f => Enumerable.Range(0, Items).Sum(i => _loadings[i, f] * _loadings[i, f]) / Items).ToArray();

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int weak = _communalities.Count(h => h < 0.2);
        int cross = Enumerable.Range(0, Items).Count(i => Enumerable.Range(0, Factors).Count(f => Math.Abs(_loadings[i, f]) >= 0.3) >= 2);
        double total = _communalities.Average();

        var builder = new InterpretationBuilder("Эксплораторный факторный анализ")
            .Summary($"Переменных {Items}, факторов {Factors}, вращение — {RotationName(Rotation)}. Факторы объясняют "
                + $"{Fmt.Pct(total)} общей дисперсии переменных.")
            .Metric("Факторов", Factors, null, null, MetricQuality.Unknown, 0)
            .Metric("Средняя общность", Fmt.Pct(total), null, "доля дисперсии, объяснённая факторами");

        for (int f = 0; f < Factors; f++)
            builder = builder.Metric($"Фактор {f + 1}", Fmt.Pct(VarianceExplained[f]), null, "сумма квадратов нагрузок на переменную");

        return builder
            .FindingIf(weak > 0, $"У {weak} переменных общность ниже 0,2: факторы их почти не объясняют — это шум или отдельная черта.")
            .FindingIf(cross > 0, $"{cross} переменных нагружают два фактора и больше (|λ| ≥ 0,3): они не относятся однозначно ни к одному.")
            .WarningIf(HeywoodCase, "Общность дошла до единицы (случай Хейвуда): факторов, вероятно, слишком много или выборка мала.")
            .WarningIf(!Converged, "Общности не сошлись за отведённое число итераций: решение приблизительное.")
            .Warning("Факторное решение определено с точностью до вращения: нагрузки описывают, как удобно смотреть "
                + "на корреляции, а не открывают единственно верную структуру. Число факторов — решение исследователя; "
                + "параллельный анализ даёт ему опору, но не заменяет его.")
            .Build();
    }

    private static string RotationName(FactorRotation rotation) => rotation switch
    {
        FactorRotation.Varimax => "варимакс",
        FactorRotation.Promax => "промакс",
        _ => "без вращения"
    };
}

/// <summary>
/// Эксплораторный факторный анализ методом главных осей.
/// </summary>
/// <remarks>
/// <para>
/// Корреляции между пунктами опросника объясняются небольшим числом скрытых факторов: R ≈ ΛΛᵀ + Ψ.
/// Метод главных осей ставит на диагональ корреляционной матрицы общности вместо единиц — начальные
/// берутся квадратами множественных корреляций, 1 − 1/(R⁻¹)ᵢᵢ, — раскладывает её по собственным
/// векторам и повторяет, пока общности не перестанут меняться. Собственные значения и обратную
/// матрицу считает общий решатель <c>Eigen</c> ядра.
/// </para>
/// <para>
/// Число факторов подсказывают два правила. Критерий Кайзера — собственные значения больше единицы —
/// систематически завышает число факторов. Параллельный анализ Хорна сравнивает собственные значения
/// с теми, что даёт случайная выборка того же размера, и оставляет только превышающие её 95-й
/// процентиль; это правило надёжнее.
/// </para>
/// <para>
/// Вращения: варимакс Кайзера с нормировкой строк максимизирует дисперсию квадратов нагрузок и даёт
/// ортогональные факторы; промакс возводит варимаксное решение в четвёртую степень как цель и
/// разрешает факторам коррелировать — психологические черты обычно коррелируют. Оценки максимального
/// правдоподобия и подтверждающего анализа с проверкой согласия здесь нет.
/// </para>
/// </remarks>
public static class FactorAnalysis
{
    /// <summary>Корреляционная матрица столбцов данных</summary>
    /// <param name="data">Наблюдения по строкам, переменные по столбцам</param>
    public static double[,] CorrelationMatrix(double[,] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        int n = data.GetLength(0), p = data.GetLength(1);

        if (n < 3 || p < 2)
            throw new ArgumentException("Нужно хотя бы три наблюдения и две переменные", nameof(data));

        var columns = new Vector[p];

        for (int j = 0; j < p; j++)
        {
            columns[j] = new Vector(n);

            for (int i = 0; i < n; i++)
            {
                Numerics.RequireFinite(data[i, j], nameof(data));
                columns[j][i] = data[i, j];
            }

            if (columns[j].Max() == columns[j].Min())
                throw new ArgumentException($"Переменная {j + 1} постоянна: корреляции с ней не определены", nameof(data));
        }

        var correlation = new double[p, p];

        for (int a = 0; a < p; a++)
        {
            correlation[a, a] = 1;

            for (int b = a + 1; b < p; b++)
            {
                double r = Statistic.CorrelationCoefficient(columns[a], columns[b]);
                correlation[a, b] = r;
                correlation[b, a] = r;
            }
        }

        return correlation;
    }

    /// <summary>Число собственных значений корреляционной матрицы больше единицы — критерий Кайзера</summary>
    /// <param name="correlation">Корреляционная матрица</param>
    public static int KaiserCount(double[,] correlation)
    {
        RequireCorrelation(correlation);

        return Eigen.Symmetric(ToMatrix(correlation), EigenOrder.Descending).Values.Count(v => v > 1);
    }

    /// <summary>
    /// Число факторов по параллельному анализу Хорна: сколько первых собственных значений превышают
    /// заданный квантиль собственных значений случайных данных того же размера
    /// </summary>
    /// <param name="data">Наблюдения по строкам, переменные по столбцам</param>
    /// <param name="random">Генератор</param>
    /// <param name="replicates">Число случайных выборок</param>
    /// <param name="quantile">Квантиль сравнения</param>
    public static int ParallelAnalysis(double[,] data, Random random, int replicates = 100, double quantile = 0.95)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(replicates, 10);

        if (!(quantile > 0 && quantile < 1))
            throw new ArgumentOutOfRangeException(nameof(quantile), "Квантиль лежит в (0; 1)");

        int n = data.GetLength(0), p = data.GetLength(1);
        Vector observed = Eigen.Symmetric(ToMatrix(CorrelationMatrix(data)), EigenOrder.Descending).Values;
        var simulated = new double[p][];

        for (int j = 0; j < p; j++)
            simulated[j] = new double[replicates];

        var noise = new double[n, p];

        for (int r = 0; r < replicates; r++)
        {
            for (int i = 0; i < n; i++)
                for (int j = 0; j < p; j++)
                    noise[i, j] = RandomEngine.NextGaussian(random);

            Vector values = Eigen.Symmetric(ToMatrix(CorrelationMatrix(noise)), EigenOrder.Descending).Values;

            for (int j = 0; j < p; j++)
                simulated[j][r] = values[j];
        }

        int factors = 0;

        for (int j = 0; j < p; j++)
        {
            double[] sorted = simulated[j].OrderBy(v => v).ToArray();
            double threshold = sorted[Math.Min(replicates - 1, (int)Math.Ceiling(quantile * replicates) - 1)];

            if (observed[j] <= threshold)
                break;

            factors++;
        }

        return factors;
    }

    /// <summary>Факторный анализ данных методом главных осей</summary>
    /// <param name="data">Наблюдения по строкам, переменные по столбцам</param>
    /// <param name="factors">Число факторов</param>
    /// <param name="rotation">Вращение</param>
    public static FactorAnalysisResult Fit(double[,] data, int factors, FactorRotation rotation = FactorRotation.Varimax)
        => PrincipalAxis(CorrelationMatrix(data), factors, rotation);

    /// <summary>Факторный анализ корреляционной матрицы методом главных осей</summary>
    /// <param name="correlation">Корреляционная матрица</param>
    /// <param name="factors">Число факторов — меньше числа переменных</param>
    /// <param name="rotation">Вращение</param>
    /// <param name="maxIterations">Предел итераций уточнения общностей</param>
    /// <param name="tolerance">Порог сходимости общностей</param>
    public static FactorAnalysisResult PrincipalAxis(
        double[,] correlation, int factors, FactorRotation rotation = FactorRotation.Varimax,
        int maxIterations = 500, double tolerance = 1e-7)
    {
        RequireCorrelation(correlation);

        int p = correlation.GetLength(0);

        if (factors < 1 || factors >= p)
            throw new ArgumentOutOfRangeException(nameof(factors), "Факторов должно быть от одного до числа переменных минус один");

        Matrix matrix = ToMatrix(correlation);
        Vector spectrum = Eigen.Symmetric(matrix, EigenOrder.Descending).Values;
        double[] communality = InitialCommunalities(matrix, correlation, spectrum);
        var loadings = new double[p, factors];
        bool converged = false, heywood = false;
        int iterations = 0;

        while (iterations < maxIterations)
        {
            iterations++;
            Matrix reduced = ToMatrix(correlation);

            for (int i = 0; i < p; i++)
                reduced[i, i] = communality[i];

            (Vector values, Matrix vectors) = Eigen.Symmetric(reduced, EigenOrder.Descending);
            double change = 0;

            for (int i = 0; i < p; i++)
            {
                double h = 0;

                for (int f = 0; f < factors; f++)
                {
                    loadings[i, f] = vectors[i, f] * Math.Sqrt(Math.Max(values[f], 0));
                    h += loadings[i, f] * loadings[i, f];
                }

                if (h >= 0.995)
                {
                    h = 0.995;
                    heywood = true;
                }

                change = Math.Max(change, Math.Abs(h - communality[i]));
                communality[i] = h;
            }

            if (change < tolerance)
            {
                converged = true;
                break;
            }
        }

        double[] finalCommunality = Enumerable.Range(0, p)
            .Select(i => Enumerable.Range(0, factors).Sum(f => loadings[i, f] * loadings[i, f]))
            .ToArray();

        double[,]? factorCorrelations = null;

        if (rotation != FactorRotation.None)
            loadings = Varimax(loadings);

        if (rotation == FactorRotation.Promax)
            (loadings, factorCorrelations) = Promax(loadings);

        OrientColumns(loadings, factorCorrelations);

        return new FactorAnalysisResult(loadings, factorCorrelations, finalCommunality, spectrum.ToArray(),
            rotation, iterations, converged, heywood);
    }

    /// <summary>
    /// Коэффициент конгруэнтности Такера: косинус угла между двумя столбцами нагрузок. Выше 0,95 —
    /// факторы практически одинаковы
    /// </summary>
    /// <param name="first">Первый столбец</param>
    /// <param name="second">Второй столбец</param>
    public static double Congruence(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count != second.Count || first.Count == 0)
            throw new ArgumentException("Столбцы должны быть одной ненулевой длины", nameof(second));

        double xy = 0, xx = 0, yy = 0;

        for (int i = 0; i < first.Count; i++)
        {
            xy += first[i] * second[i];
            xx += first[i] * first[i];
            yy += second[i] * second[i];
        }

        return xx == 0 || yy == 0 ? 0 : xy / Math.Sqrt(xx * yy);
    }

    // Квадраты множественных корреляций через обратную матрицу из собственного разложения;
    // при почти вырожденной матрице — наибольшая по модулю корреляция в строке
    private static double[] InitialCommunalities(Matrix matrix, double[,] correlation, Vector spectrum)
    {
        int p = correlation.GetLength(0);

        if (spectrum.Min() > 1e-8)
        {
            Matrix inverse = Eigen.SymmetricFunction(matrix, v => 1 / v);

            return Enumerable.Range(0, p).Select(i => Math.Clamp(1 - (1 / inverse[i, i]), 0.005, 0.995)).ToArray();
        }

        return Enumerable.Range(0, p)
            .Select(i => Enumerable.Range(0, p).Where(j => j != i).Max(j => Math.Abs(correlation[i, j])))
            .Select(h => Math.Clamp(h, 0.005, 0.995))
            .ToArray();
    }

    // Варимакс Кайзера с нормировкой строк: вращение пар факторов на угол, найденный в замкнутом виде
    private static double[,] Varimax(double[,] loadings)
    {
        int p = loadings.GetLength(0), m = loadings.GetLength(1);
        var x = (double[,])loadings.Clone();

        if (m < 2)
            return x;

        var norm = new double[p];

        for (int i = 0; i < p; i++)
        {
            norm[i] = Math.Sqrt(Enumerable.Range(0, m).Sum(f => x[i, f] * x[i, f]));

            for (int f = 0; f < m && norm[i] > 0; f++)
                x[i, f] /= norm[i];
        }

        for (int sweep = 0; sweep < 1000; sweep++)
        {
            double largest = 0;

            for (int a = 0; a < m - 1; a++)
            {
                for (int b = a + 1; b < m; b++)
                {
                    double sumU = 0, sumV = 0, sumC = 0, sumD = 0;

                    for (int i = 0; i < p; i++)
                    {
                        double u = (x[i, a] * x[i, a]) - (x[i, b] * x[i, b]);
                        double v = 2 * x[i, a] * x[i, b];
                        sumU += u;
                        sumV += v;
                        sumC += (u * u) - (v * v);
                        sumD += 2 * u * v;
                    }

                    double numerator = sumD - (2 * sumU * sumV / p);
                    double denominator = sumC - (((sumU * sumU) - (sumV * sumV)) / p);
                    double angle = 0.25 * Math.Atan2(numerator, denominator);

                    if (Math.Abs(angle) < 1e-14)
                        continue;

                    double cos = Math.Cos(angle), sin = Math.Sin(angle);

                    for (int i = 0; i < p; i++)
                    {
                        double first = x[i, a], second = x[i, b];
                        x[i, a] = (first * cos) + (second * sin);
                        x[i, b] = (-first * sin) + (second * cos);
                    }

                    largest = Math.Max(largest, Math.Abs(angle));
                }
            }

            if (largest < 1e-11)
                break;
        }

        for (int i = 0; i < p; i++)
            for (int f = 0; f < m; f++)
                x[i, f] *= norm[i];

        return x;
    }

    // Промакс (Хендриксон и Уайт): цель — варимакс в четвёртой степени со знаком, преобразование —
    // наименьшие квадраты с нормировкой, при которой дисперсии факторов единичны
    private static (double[,] Pattern, double[,] Correlations) Promax(double[,] varimax, double power = 4)
    {
        int p = varimax.GetLength(0), m = varimax.GetLength(1);

        if (m < 2)
            return (varimax, new double[,] { { 1 } });

        var target = new double[p, m];

        for (int i = 0; i < p; i++)
            for (int f = 0; f < m; f++)
                target[i, f] = varimax[i, f] * Math.Pow(Math.Abs(varimax[i, f]), power - 1);

        Matrix gramInverse = Eigen.SymmetricFunction(ToMatrix(Gram(varimax, varimax)), v => 1 / v);
        double[,] cross = Gram(varimax, target);
        var transform = new double[m, m];

        for (int a = 0; a < m; a++)
            for (int b = 0; b < m; b++)
                for (int k = 0; k < m; k++)
                    transform[a, b] += gramInverse[a, k] * cross[k, b];

        Matrix inverse = Eigen.SymmetricFunction(ToMatrix(Gram(transform, transform)), v => 1 / v);

        for (int b = 0; b < m; b++)
        {
            double scale = Math.Sqrt(inverse[b, b]);

            for (int a = 0; a < m; a++)
                transform[a, b] *= scale;
        }

        var pattern = new double[p, m];

        for (int i = 0; i < p; i++)
            for (int b = 0; b < m; b++)
                for (int k = 0; k < m; k++)
                    pattern[i, b] += varimax[i, k] * transform[k, b];

        Matrix phi = Eigen.SymmetricFunction(ToMatrix(Gram(transform, transform)), v => 1 / v);
        var correlations = new double[m, m];

        for (int a = 0; a < m; a++)
            for (int b = 0; b < m; b++)
                correlations[a, b] = phi[a, b];

        return (pattern, correlations);
    }

    // Столбец нагрузок разворачивается так, чтобы сумма нагрузок была положительной
    private static void OrientColumns(double[,] loadings, double[,]? correlations)
    {
        int p = loadings.GetLength(0), m = loadings.GetLength(1);

        for (int f = 0; f < m; f++)
        {
            double sum = 0;

            for (int i = 0; i < p; i++)
                sum += loadings[i, f];

            if (sum >= 0)
                continue;

            for (int i = 0; i < p; i++)
                loadings[i, f] = -loadings[i, f];

            if (correlations is null)
                continue;

            for (int g = 0; g < m; g++)
            {
                if (g == f)
                    continue;

                correlations[f, g] = -correlations[f, g];
                correlations[g, f] = -correlations[g, f];
            }
        }
    }

    // Aᵀ·B для матриц с одинаковым числом строк
    private static double[,] Gram(double[,] first, double[,] second)
    {
        int rows = first.GetLength(0), a = first.GetLength(1), b = second.GetLength(1);
        var result = new double[a, b];

        for (int i = 0; i < rows; i++)
            for (int x = 0; x < a; x++)
                for (int y = 0; y < b; y++)
                    result[x, y] += first[i, x] * second[i, y];

        return result;
    }

    private static Matrix ToMatrix(double[,] values)
    {
        int rows = values.GetLength(0), columns = values.GetLength(1);
        var matrix = new Matrix(rows, columns);

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < columns; j++)
                matrix[i, j] = values[i, j];

        return matrix;
    }

    private static void RequireCorrelation(double[,] correlation)
    {
        ArgumentNullException.ThrowIfNull(correlation);

        int p = correlation.GetLength(0);

        if (p < 2 || correlation.GetLength(1) != p)
            throw new ArgumentException("Корреляционная матрица должна быть квадратной, не меньше 2×2", nameof(correlation));

        for (int i = 0; i < p; i++)
        {
            if (Math.Abs(correlation[i, i] - 1) > 1e-9)
                throw new ArgumentException("На диагонали корреляционной матрицы должны стоять единицы", nameof(correlation));

            for (int j = i + 1; j < p; j++)
            {
                if (!(Math.Abs(correlation[i, j] - correlation[j, i]) <= 1e-9) || Math.Abs(correlation[i, j]) > 1 + 1e-9)
                    throw new ArgumentException("Корреляционная матрица должна быть симметричной, с элементами из [−1; 1]", nameof(correlation));
            }
        }
    }
}
