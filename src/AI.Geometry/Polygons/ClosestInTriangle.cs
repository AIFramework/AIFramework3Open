using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Ближайшая точка в 2D-треугольнике к заданной точке.
/// </summary>
public static class ClosestInTriangle
{
    /// <summary>
    /// Ближайшая точка на треугольнике (или внутри него) к точке p (2D, барицентрический/региональный подход).
    /// </summary>
    public static Vector ClosestPoint(Vector p, Vector a, Vector b, Vector c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = p - a;

        double d1 = Dot2(ab, ap);
        double d2 = Dot2(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;

        var bp = p - b;
        double d3 = Dot2(ab, bp);
        double d4 = Dot2(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;

        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            double v = d1 / (d1 - d3);
            return a + v * ab;
        }

        var cp2 = p - c;
        double d5 = Dot2(ab, cp2);
        double d6 = Dot2(ac, cp2);
        if (d6 >= 0 && d5 <= d6) return c;

        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            double w = d2 / (d2 - d6);
            return a + w * ac;
        }

        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + w * (c - b);
        }

        double denom = 1.0 / (va + vb + vc);
        double vF = vb * denom;
        double wF = vc * denom;
        return a + vF * ab + wF * ac;
    }

    private static double Dot2(Vector u, Vector v) => u[0] * v[0] + u[1] * v[1];
}
