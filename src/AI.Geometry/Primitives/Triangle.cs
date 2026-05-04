using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Треугольник, заданный тремя вершинами.
/// </summary>
/// <param name="A">Первая вершина.</param>
/// <param name="B">Вторая вершина.</param>
/// <param name="C">Третья вершина.</param>
public record Triangle(Vector A, Vector B, Vector C)
{
    /// <summary>
    /// Площадь треугольника (2D — формула шнурка, 3D — через векторное произведение).
    /// </summary>
    public double Area()
    {
        var ab = B - A;
        var ac = C - A;

        if (ab.Count == 2)
        {
            return 0.5 * Math.Abs(ab[0] * ac[1] - ab[1] * ac[0]);
        }

        var cross = Vector.Cross(ab, ac);
        return 0.5 * Math.Sqrt(Vector.Dot(cross, cross));
    }

    /// <summary>
    /// Центроид (центр масс) треугольника.
    /// </summary>
    public Vector Centroid => (1.0 / 3.0) * (A + B + C);

    /// <summary>
    /// Нормаль к плоскости треугольника (для 3D).
    /// </summary>
    public Vector Normal()
    {
        var ab = B - A;
        var ac = C - A;
        var n = Vector.Cross(ab, ac);
        double len = Math.Sqrt(Vector.Dot(n, n));
        return (1.0 / len) * n;
    }

    /// <summary>
    /// Барицентрические координаты точки относительно треугольника.
    /// </summary>
    public (double u, double v, double w) BarycentricCoords(Vector p)
    {
        var v0 = B - A;
        var v1 = C - A;
        var v2 = p - A;

        double d00 = Vector.Dot(v0, v0);
        double d01 = Vector.Dot(v0, v1);
        double d11 = Vector.Dot(v1, v1);
        double d20 = Vector.Dot(v2, v0);
        double d21 = Vector.Dot(v2, v1);

        double denom = d00 * d11 - d01 * d01;
        double v = (d11 * d20 - d01 * d21) / denom;
        double w = (d00 * d21 - d01 * d20) / denom;
        double u = 1.0 - v - w;
        return (u, v, w);
    }
}
