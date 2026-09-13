namespace AI.Psychology.Internal;

/// <summary>Мелкая численная арифметика, общая для моделей сборки</summary>
/// <remarks>
/// Прямая по методу наименьших квадратов здесь своя: в ядре её нет, а единственная готовая —
/// в химической метрологии, и тянуть ради неё зависимость на химию психологии незачем.
/// </remarks>
internal static class Numerics
{
    /// <summary>Логистическая функция без переполнения при больших по модулю аргументах</summary>
    public static double Logistic(double x)
        => x >= 0 ? 1 / (1 + Math.Exp(-x)) : Math.Exp(x) / (1 + Math.Exp(x));

    /// <summary>ln(1 + eˣ) без переполнения</summary>
    public static double Softplus(double x)
        => x > 0 ? x + Math.Log(1 + Math.Exp(-x)) : Math.Log(1 + Math.Exp(x));

    /// <summary>Прямая y = a + b·x методом наименьших квадратов и доля объяснённой дисперсии</summary>
    public static (double Intercept, double Slope, double RSquared) Line(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Count != y.Count || x.Count < 2)
            throw new ArgumentException("Нужны хотя бы две пары значений одинаковой длины", nameof(y));

        double meanX = x.Average(), meanY = y.Average();
        double sxx = 0, sxy = 0, syy = 0;

        for (int i = 0; i < x.Count; i++)
        {
            RequireFinite(x[i], nameof(x));
            RequireFinite(y[i], nameof(y));

            double dx = x[i] - meanX, dy = y[i] - meanY;
            sxx += dx * dx;
            sxy += dx * dy;
            syy += dy * dy;
        }

        if (sxx == 0)
            throw new ArgumentException("Все значения аргумента совпадают: наклон не определён", nameof(x));

        double slope = sxy / sxx;

        return (meanY - (slope * meanX), slope, syy == 0 ? 1 : sxy * sxy / (sxx * syy));
    }

    /// <summary>Требует конечное число</summary>
    public static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Значение должно быть конечным числом");
    }

    /// <summary>Требует положительное конечное число</summary>
    public static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "Значение должно быть положительным конечным числом");
    }

    /// <summary>Требует вероятность из отрезка [0; 1]</summary>
    public static void RequireProbability(double value, string name)
    {
        if (!(value >= 0 && value <= 1))
            throw new ArgumentOutOfRangeException(name, "Вероятность лежит на отрезке [0; 1]");
    }
}
