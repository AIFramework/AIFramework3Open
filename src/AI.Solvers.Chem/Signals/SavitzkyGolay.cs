using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AI.Solvers.Chem.Signals;

/// <summary>
/// Фильтр Савицкого-Голея: сглаживание и производные хроматограмм и спектров
/// локальной полиномиальной аппроксимацией.
/// </summary>
/// <remarks>
/// Коэффициенты считаются через псевдообратную матрицу фреймворка, а не берутся
/// из таблиц: это даёт любое окно, любой порядок полинома и любую производную.
/// В отличие от скользящего среднего фильтр сохраняет высоту и ширину пика -
/// именно поэтому в хроматографии применяют его.
/// </remarks>
public static class SavitzkyGolay
{
    /// <summary>
    /// Коэффициенты свёртки
    /// </summary>
    /// <param name="window">Ширина окна: нечётное число точек, больше порядка полинома</param>
    /// <param name="order">Порядок полинома</param>
    /// <param name="derivative">Порядок производной: 0 - сглаживание</param>
    /// <param name="spacing">Шаг по оси x</param>
    public static double[] Coefficients(int window, int order, int derivative = 0, double spacing = 1.0)
    {
        Validate(window, order, derivative);

        return Weights(PseudoInverse(window, order), order, derivative, position: 0, spacing);
    }

    /// <summary>
    /// Сглаживание или дифференцирование сигнала
    /// </summary>
    /// <param name="signal">Отсчёты сигнала</param>
    /// <param name="window">Ширина окна (нечётная)</param>
    /// <param name="order">Порядок полинома</param>
    /// <param name="derivative">Порядок производной</param>
    /// <param name="spacing">Шаг по оси x</param>
    public static double[] Apply(double[] signal, int window = 9, int order = 2, int derivative = 0, double spacing = 1.0)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (signal.Length == 0)
            return Array.Empty<double>();

        if (signal.Length < window)
            window = signal.Length % 2 == 0 ? Math.Max(3, signal.Length - 1) : Math.Max(3, signal.Length);

        if (window > signal.Length || window < 3)
            return (double[])signal.Clone();

        if (order >= window)
            order = window - 1;

        Validate(window, order, derivative);

        var pseudoInverse = PseudoInverse(window, order);
        var central = Weights(pseudoInverse, order, derivative, position: 0, spacing);

        int half = window / 2;
        int last = signal.Length - window;
        var result = new double[signal.Length];

        for (int i = 0; i < signal.Length; i++)
        {
            // У краёв окно не двигается, а полином первого (последнего) окна
            // вычисляется в нужной точке: отражение сигнала исказило бы наклон
            int start = Math.Clamp(i - half, 0, last);
            var coefficients = start == i - half
                ? central
                : Weights(pseudoInverse, order, derivative, i - start - half, spacing);

            double sum = 0;

            for (int k = 0; k < window; k++)
                sum += coefficients[k] * signal[start + k];

            result[i] = sum;
        }

        return result;
    }

    /// <summary>Сглаживание вектора фреймворка</summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="window">Ширина окна</param>
    /// <param name="order">Порядок полинома</param>
    public static Vector Smooth(Vector signal, int window = 9, int order = 2)
        => new(Apply(signal.ToArray(), window, order));

    private static void Validate(int window, int order, int derivative)
    {
        if (window < 3 || window % 2 == 0)
            throw new ArgumentException("Window must be an odd number of at least 3 points", nameof(window));

        if (order < 1 || order >= window)
            throw new ArgumentException("Polynomial order must be at least 1 and less than the window", nameof(order));

        if (derivative < 0 || derivative > order)
            throw new ArgumentException("Derivative order must not exceed the polynomial order", nameof(derivative));
    }

    // Псевдообратная матрица плана: строка k даёт коэффициент при z^k
    private static Matrix PseudoInverse(int window, int order)
    {
        int half = window / 2;
        var design = new Matrix(window, order + 1);

        for (int i = 0; i < window; i++)
        {
            double z = i - half;
            double power = 1;

            for (int j = 0; j <= order; j++)
            {
                design[i, j] = power;
                power *= z;
            }
        }

        return Pseudoinverse.Compute(design);
    }

    /// <summary>
    /// Веса свёртки для d-й производной полинома, вычисленной в точке position
    /// внутри окна (0 - центр окна)
    /// </summary>
    private static double[] Weights(Matrix pseudoInverse, int order, int derivative, double position, double spacing)
    {
        int window = pseudoInverse.Width;
        var coefficients = new double[window];

        // f(z) = Σ a_k·z^k, значит f⁽ᵈ⁾(p) = Σ_{k≥d} a_k·k!/(k-d)!·p^(k-d)
        for (int k = derivative; k <= order; k++)
        {
            double factor = 1;

            for (int t = 0; t < derivative; t++)
                factor *= k - t;

            factor *= Math.Pow(position, k - derivative);

            for (int j = 0; j < window; j++)
                coefficients[j] += pseudoInverse[k, j] * factor;
        }

        double scale = 1.0 / Math.Pow(spacing, derivative);

        for (int j = 0; j < window; j++)
            coefficients[j] *= scale;

        return coefficients;
    }
}
