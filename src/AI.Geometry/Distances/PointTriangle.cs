using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Расстояние от точки до треугольника.
/// </summary>
public static class PointTriangle
{
    /// <summary>
    /// Ближайшая точка на треугольнике к заданной точке (в 3D).
    /// </summary>
    public static Vector ClosestPoint(Vector p, Triangle tri)
    {
        var ab = tri.B - tri.A;
        var ac = tri.C - tri.A;
        var ap = p - tri.A;

        double d1 = Vector.Dot(ab, ap);
        double d2 = Vector.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return tri.A;

        var bp = p - tri.B;
        double d3 = Vector.Dot(ab, bp);
        double d4 = Vector.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return tri.B;

        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            double v = d1 / (d1 - d3);
            return tri.A + v * ab;
        }

        var cp = p - tri.C;
        double d5 = Vector.Dot(ab, cp);
        double d6 = Vector.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return tri.C;

        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            double w = d2 / (d2 - d6);
            return tri.A + w * ac;
        }

        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return tri.B + w * (tri.C - tri.B);
        }

        double denom = 1.0 / (va + vb + vc);
        double vFinal = vb * denom;
        double wFinal = vc * denom;
        return tri.A + vFinal * ab + wFinal * ac;
    }

    /// <summary>
    /// Расстояние от точки до треугольника.
    /// </summary>
    public static double Distance(Vector p, Triangle tri)
    {
        var closest = ClosestPoint(p, tri);
        var diff = p - closest;
        return Math.Sqrt(Vector.Dot(diff, diff));
    }

    /// <summary>
    /// Ближайшая точка треугольника к заданной точке по зонам Вороного (без выделения памяти).
    /// </summary>
    /// <param name="p">Точка.</param>
    /// <param name="a">Первая вершина треугольника.</param>
    /// <param name="b">Вторая вершина треугольника.</param>
    /// <param name="c">Третья вершина треугольника.</param>
    public static Vector3 ClosestPoint(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;

        double d1 = ab.Dot(ap);
        double d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0) return a;

        Vector3 bp = p - b;
        double d3 = ab.Dot(bp);
        double d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3) return b;

        double vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
            return a + (ab * (d1 / (d1 - d3)));

        Vector3 cp = p - c;
        double d5 = ab.Dot(cp);
        double d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6) return c;

        double vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
            return a + (ac * (d2 / (d2 - d6)));

        double va = (d3 * d6) - (d5 * d4);
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            return b + ((c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))));

        double denom = 1.0 / (va + vb + vc);
        return a + (ab * (vb * denom)) + (ac * (vc * denom));
    }
}
