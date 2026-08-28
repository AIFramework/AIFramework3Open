using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AI.Solvers.Chem.Signals;

/// <summary>
/// Коррекция базовой линии хроматограмм и спектров
/// </summary>
/// <remarks>
/// Оба метода не требуют указывать «пустые» участки: дрейф оценивается по самому
/// сигналу. Итерационный полином (ModPoly) подходит для плавного дрейфа детектора,
/// морфологический метод - для широких неразрешённых горбов.
/// </remarks>
public static class BaselineCorrection
{
    /// <summary>
    /// Итерационная полиномиальная базовая линия: полином подгоняется по сигналу,
    /// затем точки выше полинома заменяются его значением, и так до сходимости.
    /// Пики «срезаются», а дрейф остаётся.
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="order">Порядок полинома (2..5 для типичного дрейфа)</param>
    /// <param name="iterations">Предельное число итераций</param>
    /// <param name="tolerance">Порог сходимости по относительному изменению</param>
    public static double[] ModifiedPolynomial(double[] signal, int order = 3, int iterations = 50, double tolerance = 1e-4)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (signal.Length <= order + 1)
            return (double[])signal.Clone();

        var x = NormalizedAxis(signal.Length);
        var design = Vandermonde(x, order);
        var pseudoInverse = Pseudoinverse.Compute(design);

        var working = (double[])signal.Clone();
        double[] baseline = null;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var fitted = FitPolynomial(pseudoInverse, design, working);
            double change = 0, magnitude = 0;

            for (int i = 0; i < signal.Length; i++)
            {
                if (baseline != null)
                {
                    change += Math.Abs(fitted[i] - baseline[i]);
                    magnitude += Math.Abs(baseline[i]);
                }

                // Пик обрезается по базовой линии, впадина остаётся как есть.
                // Сравнение идёт с исходным сигналом, иначе оценка от итерации
                // к итерации монотонно уползала бы вниз
                working[i] = Math.Min(signal[i], fitted[i]);
            }

            baseline = fitted;

            if (iteration > 0 && change <= tolerance * Math.Max(1e-12, magnitude))
                break;
        }

        return baseline;
    }

    /// <summary>
    /// Базовая линия методом асимметричных наименьших квадратов (AsLS).
    /// Линия притягивается к сигналу снизу: точки выше неё получают малый вес,
    /// поэтому пики любой высоты почти не тянут её вверх.
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="smoothness">
    /// Штраф за кривизну λ: чем больше, тем жёстче линия. Значение подбирается под ширину
    /// пика в точках - см. <see cref="SmoothnessForPeakWidth"/>. Слишком мягкая линия
    /// «съедает» площадь пика и оставляет по бокам от него ложные горбы.
    /// </param>
    /// <param name="asymmetry">Вес точек выше линии p (0.001..0.05)</param>
    /// <param name="iterations">Число итераций перевзвешивания</param>
    public static double[] AsymmetricLeastSquares(double[] signal, double smoothness = 1e6,
        double asymmetry = 0.01, int iterations = 10)
    {
        ArgumentNullException.ThrowIfNull(signal);

        int n = signal.Length;

        if (n < 5)
            return (double[])signal.Clone();

        if (asymmetry is <= 0 or >= 1)
            throw new ArgumentException("Asymmetry must be within (0; 1)", nameof(asymmetry));

        var weights = new double[n];
        Array.Fill(weights, 1.0);

        var baseline = (double[])signal.Clone();

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            baseline = SolvePenalized(signal, weights, smoothness);

            for (int i = 0; i < n; i++)
                weights[i] = signal[i] > baseline[i] ? asymmetry : 1 - asymmetry;
        }

        return baseline;
    }

    /// <summary>
    /// Подбор штрафа λ по ширине пика: базовая линия должна быть заметно глаже
    /// самого узкого пика, иначе она пойдёт по нему
    /// </summary>
    /// <param name="peakWidthPoints">Ширина самого узкого пика на половине высоты, точек</param>
    public static double SmoothnessForPeakWidth(double peakWidthPoints)
    {
        if (peakWidthPoints <= 0)
            throw new ArgumentException("Peak width must be positive", nameof(peakWidthPoints));

        // Характерный масштаб сглаживания растёт как λ^(1/4), берём тройной запас по ширине
        return Math.Pow(3 * peakWidthPoints, 4);
    }

    /// <summary>
    /// Морфологическая базовая линия: минимум в скользящем окне, затем максимум
    /// (операция размыкания) и сглаживание
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="window">Окно, заведомо шире самого широкого пика</param>
    public static double[] RollingMinimum(double[] signal, int window)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (window < 3 || signal.Length < window)
            return new double[signal.Length];

        var eroded = Morphology(signal, window, minimum: true);
        var opened = Morphology(eroded, window, minimum: false);
        int smoothWindow = window % 2 == 0 ? window + 1 : window;

        return SavitzkyGolay.Apply(opened, Math.Min(smoothWindow, OddLength(signal.Length)), 1);
    }

    /// <summary>
    /// Вычитает базовую линию из сигнала, не опуская результат ниже нуля
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="baseline">Базовая линия</param>
    /// <param name="clampToZero">Обрезать отрицательные значения</param>
    public static double[] Subtract(double[] signal, double[] baseline, bool clampToZero = false)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(baseline);

        if (signal.Length != baseline.Length)
            throw new ArgumentException("Signal and baseline must have the same length");

        var result = new double[signal.Length];

        for (int i = 0; i < signal.Length; i++)
        {
            double value = signal[i] - baseline[i];
            result[i] = clampToZero && value < 0 ? 0 : value;
        }

        return result;
    }

    /// <summary>
    /// Оценка шума базовой линии: медиана модулей разностей соседних точек,
    /// пересчитанная в СКО. Устойчива к присутствию пиков.
    /// </summary>
    /// <param name="signal">Сигнал</param>
    public static double EstimateNoise(double[] signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (signal.Length < 3)
            return 0;

        var differences = new double[signal.Length - 1];

        for (int i = 1; i < signal.Length; i++)
            differences[i - 1] = Math.Abs(signal[i] - signal[i - 1]);

        Array.Sort(differences);
        double median = differences[differences.Length / 2];

        // Разности соседних отсчётов имеют СКО σ·√2, медиана их модуля равна
        // 0.6745·σ·√2 = 0.9539·σ - отсюда и множитель
        return median / 0.9539;
    }

    /// <summary>
    /// Решает (W + λ·D₂ᵀD₂)·z = W·y, где D₂ - вторая разность.
    /// Матрица симметричная ленточная с полушириной 2, поэтому используется
    /// ленточное разложение Холецкого: O(n) вместо O(n³) у плотного решателя.
    /// </summary>
    private static double[] SolvePenalized(double[] y, double[] weights, double smoothness)
    {
        int n = y.Length;
        const int band = 2;

        // Диагонали симметричной матрицы: [i, 0] - главная, [i, 1] и [i, 2] - верхние
        var matrix = new double[n, band + 1];

        // λ·D₂ᵀD₂ для внутренних строк даёт шаблон (1, -4, 6, -4, 1)
        for (int i = 0; i < n - 2; i++)
        {
            matrix[i, 0] += smoothness;
            matrix[i + 1, 0] += 4 * smoothness;
            matrix[i + 2, 0] += smoothness;

            matrix[i, 1] += -2 * smoothness;
            matrix[i + 1, 1] += -2 * smoothness;
            matrix[i, 2] += smoothness;
        }

        var rhs = new double[n];

        for (int i = 0; i < n; i++)
        {
            matrix[i, 0] += weights[i];
            rhs[i] = weights[i] * y[i];
        }

        // Ленточное разложение Холецкого: matrix = UᵀU
        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k <= band; k++)
            {
                if (i + k >= n)
                    break;

                double sum = matrix[i, k];

                for (int m = 1; m <= Math.Min(band, i); m++)
                {
                    if (m + k <= band)
                        sum -= matrix[i - m, m] * matrix[i - m, m + k];
                }

                if (k == 0)
                {
                    if (sum <= 0)
                        throw new InvalidOperationException("Penalised least squares matrix is not positive definite");

                    matrix[i, 0] = Math.Sqrt(sum);
                }
                else
                {
                    matrix[i, k] = sum / matrix[i, 0];
                }
            }
        }

        // Прямой ход: Uᵀ·v = rhs
        for (int i = 0; i < n; i++)
        {
            double sum = rhs[i];

            for (int m = 1; m <= Math.Min(band, i); m++)
                sum -= matrix[i - m, m] * rhs[i - m];

            rhs[i] = sum / matrix[i, 0];
        }

        // Обратный ход: U·z = v
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = rhs[i];

            for (int m = 1; m <= band && i + m < n; m++)
                sum -= matrix[i, m] * rhs[i + m];

            rhs[i] = sum / matrix[i, 0];
        }

        return rhs;
    }

    private static double[] Morphology(double[] signal, int window, bool minimum)
    {
        int half = window / 2;
        var result = new double[signal.Length];

        for (int i = 0; i < signal.Length; i++)
        {
            int from = Math.Max(0, i - half);
            int to = Math.Min(signal.Length - 1, i + half);
            double value = signal[from];

            for (int j = from + 1; j <= to; j++)
                value = minimum ? Math.Min(value, signal[j]) : Math.Max(value, signal[j]);

            result[i] = value;
        }

        return result;
    }

    // Ось, нормированная в [-1, 1]: степени большого индекса иначе теряют обусловленность
    private static double[] NormalizedAxis(int length)
    {
        var x = new double[length];

        for (int i = 0; i < length; i++)
            x[i] = length == 1 ? 0 : (2.0 * i / (length - 1)) - 1.0;

        return x;
    }

    private static Matrix Vandermonde(double[] x, int order)
    {
        var design = new Matrix(x.Length, order + 1);

        for (int i = 0; i < x.Length; i++)
        {
            double power = 1;

            for (int j = 0; j <= order; j++)
            {
                design[i, j] = power;
                power *= x[i];
            }
        }

        return design;
    }

    private static double[] FitPolynomial(Matrix pseudoInverse, Matrix design, double[] y)
    {
        int terms = pseudoInverse.Height;
        var coefficients = new double[terms];

        for (int j = 0; j < terms; j++)
        {
            double sum = 0;

            for (int i = 0; i < y.Length; i++)
                sum += pseudoInverse[j, i] * y[i];

            coefficients[j] = sum;
        }

        var fitted = new double[y.Length];

        for (int i = 0; i < y.Length; i++)
        {
            double sum = 0;

            for (int j = 0; j < terms; j++)
                sum += design[i, j] * coefficients[j];

            fitted[i] = sum;
        }

        return fitted;
    }

    private static int OddLength(int length) => length % 2 == 0 ? length - 1 : length;
}
