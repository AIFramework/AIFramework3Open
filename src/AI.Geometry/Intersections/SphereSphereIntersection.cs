using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Тест пересечения двух сфер.
/// </summary>
public static class SphereSphereIntersection
{
    /// <summary>
    /// Проверяет, пересекаются ли (или касаются) две сферы.
    /// </summary>
    public static bool Test(Sphere a, Sphere b)
    {
        var d = a.Center - b.Center;
        double dist2 = Vector.Dot(d, d);
        double rSum = a.Radius + b.Radius;
        return dist2 <= rSum * rSum;
    }
}
