using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Расстояние от точки до плоскости.
/// </summary>
public static class PointPlane
{
    /// <summary>
    /// Знаковое расстояние от точки до плоскости.
    /// </summary>
    public static double SignedDistance(Vector point, Plane plane)
    {
        return plane.SignedDistance(point);
    }

    /// <summary>
    /// Расстояние от точки до плоскости (модуль).
    /// </summary>
    public static double Distance(Vector point, Plane plane)
    {
        return Math.Abs(plane.SignedDistance(point));
    }

    /// <summary>
    /// Ближайшая точка на плоскости к заданной точке.
    /// </summary>
    public static Vector ClosestPoint(Vector point, Plane plane)
    {
        double d = plane.SignedDistance(point);
        double nn = Math.Sqrt(Vector.Dot(plane.Normal, plane.Normal));
        return point - (d / nn) * plane.Normal;
    }
}
