using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение двух прямых на плоскости.
/// </summary>
public static class LineLineIntersection
{
    /// <summary>
    /// Находит точку пересечения двух 2D-прямых. Возвращает null, если прямые параллельны.
    /// </summary>
    public static Vector Intersect(Line2D a, Line2D b)
    {
        var (a1, b1, c1) = a.ToGeneral();
        var (a2, b2, c2) = b.ToGeneral();

        double det = a1 * b2 - a2 * b1;
        if (Eps.IsZero(det))
            return null;

        double x = (b1 * c2 - b2 * c1) / det;
        double y = (a2 * c1 - a1 * c2) / det;
        return new Vector(new[] { x, y });
    }
}
