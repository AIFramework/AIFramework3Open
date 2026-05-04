using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение прямой и плоскости в 3D.
/// </summary>
public static class LinePlaneIntersection
{
    /// <summary>
    /// Находит точку пересечения прямой и плоскости. Возвращает null, если прямая параллельна плоскости.
    /// </summary>
    public static Vector Intersect(Line3D line, Plane plane)
    {
        double denom = Vector.Dot(plane.Normal, line.Direction);
        if (Eps.IsZero(denom))
            return null;

        double t = -(Vector.Dot(plane.Normal, line.Point) + plane.D) / denom;
        return line.PointAt(t);
    }
}
