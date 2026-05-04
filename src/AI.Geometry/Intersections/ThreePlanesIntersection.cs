using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение трёх плоскостей — точка в 3D.
/// </summary>
public static class ThreePlanesIntersection
{
    /// <summary>
    /// Находит точку пересечения трёх плоскостей. Возвращает null, если решение не единственно.
    /// </summary>
    public static Vector Intersect(Plane a, Plane b, Plane c)
    {
        var n1xn2 = Vector.Cross(a.Normal, b.Normal);
        double denom = Vector.Dot(n1xn2, c.Normal);
        if (Eps.IsZero(denom))
            return null;

        var n2xn3 = Vector.Cross(b.Normal, c.Normal);
        var n3xn1 = Vector.Cross(c.Normal, a.Normal);

        var point = (-a.D * n2xn3 + -b.D * n3xn1 + -c.D * n1xn2) * (1.0 / denom);
        return point;
    }
}
