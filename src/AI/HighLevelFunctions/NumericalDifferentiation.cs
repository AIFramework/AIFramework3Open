using System;

namespace AI.HighLevelFunctions;

/// <summary>
/// Численное дифференцирование: производная, градиент, якобиан и гессиан конечными разностями.
/// </summary>
/// <remarks>
/// <para>
/// Единственное место, где записаны разностные формулы. Шаг по умолчанию относительный и выбран под
/// порядок формулы: ошибка складывается из округления (растёт при уменьшении шага) и отбрасывания
/// (растёт при увеличении), минимум для центральной разности даёт ∛ε, для односторонней — √ε,
/// для вторых производных — ⁴√ε, где ε — машинная точность.
/// </para>
/// <para>
/// Шаг масштабируется точкой: <c>h = шаг · max(|x|, 1)</c>. Делится разность на фактическое расстояние
/// между пробными точками, а не на задуманное: так учитывается округление самого шага.
/// </para>
/// </remarks>
public static class NumericalDifferentiation
{
    /// <summary>Относительный шаг центральной разности: кубический корень машинной точности.</summary>
    public const double CentralStep = 6.055454452393343e-6;

    /// <summary>Относительный шаг односторонней разности: квадратный корень машинной точности.</summary>
    public const double ForwardStep = 1.4901161193847656e-8;

    /// <summary>Относительный шаг для вторых производных: корень четвёртой степени машинной точности.</summary>
    public const double SecondOrderStep = 1.220703125e-4;

    /// <summary>
    /// Производная функции одной переменной центральной разностью.
    /// </summary>
    /// <param name="function">Функция.</param>
    /// <param name="x">Точка.</param>
    /// <param name="step">Абсолютный шаг; 0 — подобрать по масштабу точки.</param>
    /// <returns>Оценка f′(x) с ошибкой порядка квадрата шага.</returns>
    public static double Derivative(Func<double, double> function, double x, double step = 0)
    {
        ArgumentNullException.ThrowIfNull(function);

        double h = step > 0 ? step : CentralStep * Math.Max(Math.Abs(x), 1.0);
        double forward = x + h, backward = x - h;

        return (function(forward) - function(backward)) / (forward - backward);
    }

    /// <summary>
    /// Производная справа — односторонней разностью.
    /// </summary>
    /// <remarks>
    /// Нужна на краю области определения и в точке излома, где слева функции нет или она другая.
    /// Точность ниже, чем у <see cref="Derivative"/>: ошибка порядка шага, а не его квадрата.
    /// </remarks>
    /// <param name="function">Функция.</param>
    /// <param name="x">Точка.</param>
    /// <param name="step">Абсолютный шаг; 0 — подобрать по масштабу точки.</param>
    public static double ForwardDerivative(Func<double, double> function, double x, double step = 0)
    {
        ArgumentNullException.ThrowIfNull(function);

        double h = step > 0 ? step : ForwardStep * Math.Max(Math.Abs(x), 1.0);
        double forward = x + h;

        return (function(forward) - function(x)) / (forward - x);
    }

    /// <summary>
    /// Градиент скалярной функции многих переменных центральными разностями.
    /// </summary>
    /// <param name="function">Функция.</param>
    /// <param name="point">Точка; не изменяется.</param>
    /// <param name="relativeStep">Относительный шаг; 0 — <see cref="CentralStep"/>.</param>
    public static double[] Gradient(Func<double[], double> function, double[] point, double relativeStep = 0)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(point);

        double[,] jacobian = Jacobian(probe => [function(probe)], point, 1, relativeStep);
        var gradient = new double[point.Length];

        for (int j = 0; j < gradient.Length; j++)
            gradient[j] = jacobian[0, j];

        return gradient;
    }

    /// <summary>
    /// Якобиан центральными разностями: <c>J[i, j] = ∂fᵢ/∂xⱼ</c>.
    /// </summary>
    /// <param name="function">Вектор-функция. Массив аргумента переиспользуется между вызовами — сохранять его нельзя.</param>
    /// <param name="point">Точка; не изменяется.</param>
    /// <param name="outputs">Число компонент функции.</param>
    /// <param name="relativeStep">Относительный шаг; 0 — <see cref="CentralStep"/>.</param>
    /// <exception cref="InvalidOperationException">Функция вернула вектор другой длины.</exception>
    public static double[,] Jacobian(Func<double[], double[]> function, double[] point, int outputs, double relativeStep = 0)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(point);
        ArgumentOutOfRangeException.ThrowIfNegative(outputs);

        double step = relativeStep > 0 ? relativeStep : CentralStep;
        int n = point.Length;
        var jacobian = new double[outputs, n];
        var probe = (double[])point.Clone();

        for (int j = 0; j < n; j++)
        {
            double delta = step * Math.Max(Math.Abs(point[j]), 1.0);
            double forward = point[j] + delta;
            double backward = point[j] - delta;

            probe[j] = forward;
            double[] plus = Checked(function(probe), outputs);
            probe[j] = backward;
            double[] minus = Checked(function(probe), outputs);
            probe[j] = point[j];

            double width = forward - backward;

            for (int i = 0; i < outputs; i++)
                jacobian[i, j] = (plus[i] - minus[i]) / width;
        }

        return jacobian;
    }

    /// <summary>
    /// Гессиан скалярной функции: матрица вторых производных, симметричная.
    /// </summary>
    /// <remarks>
    /// Диагональ считается по трём точкам, <c>(f(x+h) − 2f(x) + f(x−h)) / h²</c>, внедиагональные элементы —
    /// по четырём, <c>(f₊₊ − f₊₋ − f₋₊ + f₋₋) / (4·hₐ·h_b)</c>. Четырёхточечная формула на диагонали
    /// означала бы вдвое больший шаг и вчетверо большую ошибку отбрасывания.
    /// </remarks>
    /// <param name="function">Функция. Массив аргумента переиспользуется между вызовами.</param>
    /// <param name="point">Точка; не изменяется.</param>
    /// <param name="relativeStep">Относительный шаг; 0 — <see cref="SecondOrderStep"/>.</param>
    public static double[,] Hessian(Func<double[], double> function, double[] point, double relativeStep = 0)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(point);

        double step = relativeStep > 0 ? relativeStep : SecondOrderStep;
        int n = point.Length;
        var hessian = new double[n, n];
        var probe = (double[])point.Clone();
        var h = new double[n];

        for (int a = 0; a < n; a++)
            h[a] = step * Math.Max(Math.Abs(point[a]), 1.0);

        double center = function(probe);

        double At(int a, double da, int b, double db)
        {
            probe[a] = point[a] + da;
            if (b != a) probe[b] = point[b] + db;

            double value = function(probe);

            probe[a] = point[a];
            probe[b] = point[b];

            return value;
        }

        for (int a = 0; a < n; a++)
        {
            hessian[a, a] = (At(a, h[a], a, 0) - (2 * center) + At(a, -h[a], a, 0)) / (h[a] * h[a]);

            for (int b = a + 1; b < n; b++)
            {
                double value = (At(a, h[a], b, h[b]) - At(a, h[a], b, -h[b]) - At(a, -h[a], b, h[b]) + At(a, -h[a], b, -h[b]))
                    / (4 * h[a] * h[b]);

                hessian[a, b] = value;
                hessian[b, a] = value;
            }
        }

        return hessian;
    }

    private static double[] Checked(double[] value, int outputs)
    {
        if (value == null)
            throw new InvalidOperationException("Функция вернула null.");

        if (value.Length != outputs)
            throw new InvalidOperationException($"Функция вернула {value.Length} значений вместо {outputs}.");

        return value;
    }
}
