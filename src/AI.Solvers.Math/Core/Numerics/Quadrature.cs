namespace AI.Solvers.Math.Core.Numerics;

/// <summary>
/// Численное интегрирование по одной переменной.
/// <para>
/// Единственная реализация на всю зону. До этого их было три с разной точностью:
/// трапеции на 1000 узлов в определённом интеграле, трапеции на 2000–4096 узлов
/// в спецфункциях и левые прямоугольники (первый порядок) в SymbolicIntegrator.
/// Готовой квадратуры во фреймворке нет — искал по Simpson/Trapezoid/Трапеци,
/// нашлись только частные вычисления в AI.DSP и AI.Fuzzy.
/// </para>
/// </summary>
internal static class Quadrature
{
    /// <summary>Доля отрезка, на которую отступают от неинтегрируемого конца.</summary>
    private const double EndpointNudge = 1e-9;

    private const int MaxDepth = 40;

    /// <summary>
    /// Адаптивный метод Симпсона: делит отрезок там, где подынтегральная функция
    /// того требует, поэтому не тратит узлы на гладких участках и не промахивается
    /// на пиках. Порядок точности O(h⁴) против O(h²) у трапеций.
    /// </summary>
    /// <param name="f">Подынтегральная функция.</param>
    /// <param name="a">Нижний предел.</param>
    /// <param name="b">Верхний предел.</param>
    /// <param name="tolerance">Требуемая абсолютная погрешность.</param>
    public static double Integrate(Func<double, double> f, double a, double b, double tolerance = 1e-10)
    {
        if (a == b) return 0.0;

        // Значение на конце может быть не определено (1/√x в нуле, ln в нуле):
        // отступаем внутрь отрезка вместо того, чтобы вернуть Infinity.
        double fa = Safe(f, a, a + ((b - a) * EndpointNudge));
        double fb = Safe(f, b, b - ((b - a) * EndpointNudge));

        double middle = (a + b) / 2;
        double fm = Safe(f, middle, middle);
        double whole = SimpsonRule(a, b, fa, fm, fb);

        return Refine(f, a, b, fa, fm, fb, whole, tolerance, MaxDepth);
    }

    private static double Refine(Func<double, double> f, double a, double b,
                                 double fa, double fm, double fb,
                                 double whole, double tolerance, int depth)
    {
        double middle      = (a + b) / 2;
        double leftMiddle  = (a + middle) / 2;
        double rightMiddle = (middle + b) / 2;

        double fLeft  = Safe(f, leftMiddle,  leftMiddle);
        double fRight = Safe(f, rightMiddle, rightMiddle);

        double left  = SimpsonRule(a, middle, fa, fLeft, fm);
        double right = SimpsonRule(middle, b, fm, fRight, fb);
        double delta = left + right - whole;

        // Оценка Ричардсона: погрешность составной формулы ≈ delta/15
        if (depth <= 0 || System.Math.Abs(delta) <= 15 * tolerance)
            return left + right + (delta / 15);

        return Refine(f, a, middle, fa, fLeft, fm, left, tolerance / 2, depth - 1) +
               Refine(f, middle, b, fm, fRight, fb, right, tolerance / 2, depth - 1);
    }

    private static double SimpsonRule(double a, double b, double fa, double fm, double fb) =>
        (b - a) / 6 * (fa + (4 * fm) + fb);

    /// <summary>
    /// Значение функции в точке; если оно не определено или бесконечно,
    /// берётся значение в запасной точке, а при неудаче — ноль.
    /// </summary>
    private static double Safe(Func<double, double> f, double x, double fallbackPoint)
    {
        double value;
        try { value = f(x); }
        catch { value = double.NaN; }

        if (!double.IsNaN(value) && !double.IsInfinity(value)) return value;
        if (fallbackPoint == x) return 0.0;

        try { value = f(fallbackPoint); }
        catch { return 0.0; }

        return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
    }
}
