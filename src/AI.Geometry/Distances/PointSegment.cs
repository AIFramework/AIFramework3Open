using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Расстояние от точки до отрезка.
/// </summary>
public static class PointSegment
{
    /// <summary>
    /// Расстояние от точки до отрезка.
    /// </summary>
    public static double Distance(Vector point, Segment seg)
    {
        var closest = ClosestPoint(point, seg);
        var diff = point - closest;
        return Math.Sqrt(Vector.Dot(diff, diff));
    }

    /// <summary>
    /// Ближайшая точка на отрезке к заданной точке.
    /// </summary>
    public static Vector ClosestPoint(Vector point, Segment seg)
    {
        var ab = seg.B - seg.A;
        var ap = point - seg.A;
        double t = Vector.Dot(ap, ab) / Vector.Dot(ab, ab);
        t = Math.Max(0, Math.Min(1, t));
        return seg.PointAt(t);
    }
}
