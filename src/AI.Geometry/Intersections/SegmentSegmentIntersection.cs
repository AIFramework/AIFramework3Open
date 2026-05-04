using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение двух отрезков на плоскости (2D).
/// </summary>
public static class SegmentSegmentIntersection
{
    /// <summary>
    /// Находит точку пересечения двух 2D-отрезков. Возвращает null, если не пересекаются.
    /// Обрабатывает коллинеарный случай (возвращает null — не выделяет отрезок перекрытия).
    /// </summary>
    public static Vector Intersect(Segment s1, Segment s2)
    {
        var d1 = s1.B - s1.A;
        var d2 = s2.B - s2.A;
        var d = s2.A - s1.A;

        double cross = d1[0] * d2[1] - d1[1] * d2[0];
        if (Eps.IsZero(cross))
            return null;

        double t = (d[0] * d2[1] - d[1] * d2[0]) / cross;
        double u = (d[0] * d1[1] - d[1] * d1[0]) / cross;

        if (t >= -Eps.Default && t <= 1 + Eps.Default &&
            u >= -Eps.Default && u <= 1 + Eps.Default)
        {
            return s1.PointAt(t);
        }

        return null;
    }
}
