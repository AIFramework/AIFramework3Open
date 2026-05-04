using System;

namespace AI.Fuzzy.Inference;

/// <summary>
/// Типовые функции принадлежности для фаззификации на универсуме.
/// </summary>
public static class FuzzyMembershipShapes
{
    /// <summary>
    /// Треугольная функция принадлежности: носитель [a, c], вершина в b.
    /// </summary>
    public static double Triangular(double x, double a, double b, double c)
    {
        if (x <= a || x >= c) return 0;
        if (Math.Abs(x - b) < double.Epsilon) return 1;
        if (x < b) return (x - a) / (b - a);
        return (c - x) / (c - b);
    }

    /// <summary>
    /// Трапециевидная функция принадлежности: плато [b, c], скаты [a,b] и [c,d].
    /// </summary>
    public static double Trapezoidal(double x, double a, double b, double c, double d)
    {
        if (x <= a || x >= d) return 0;
        if (x >= b && x <= c) return 1;
        if (x < b) return (x - a) / (b - a);
        return (d - x) / (d - c);
    }
}
