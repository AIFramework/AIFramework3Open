using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Расстояние от точки до прямой.
/// </summary>
public static class PointLine
{
    /// <summary>
    /// Расстояние от точки до прямой на плоскости (2D).
    /// </summary>
    public static double Distance2D(Vector point, Line2D line)
    {
        var (a, b, c) = line.ToGeneral();
        return Math.Abs(a * point[0] + b * point[1] + c) / Math.Sqrt(a * a + b * b);
    }

    /// <summary>
    /// Расстояние от точки до прямой в 3D.
    /// </summary>
    public static double Distance3D(Vector point, Line3D line)
    {
        var ap = point - line.Point;
        var cross = Vector.Cross(ap, line.Direction);
        double crossLen = Math.Sqrt(Vector.Dot(cross, cross));
        double dirLen = Math.Sqrt(Vector.Dot(line.Direction, line.Direction));
        return crossLen / dirLen;
    }

    /// <summary>
    /// Ближайшая точка на прямой (3D) к заданной точке.
    /// </summary>
    public static Vector ClosestPoint(Vector point, Line3D line)
    {
        var ap = point - line.Point;
        double t = Vector.Dot(ap, line.Direction) / Vector.Dot(line.Direction, line.Direction);
        return line.PointAt(t);
    }
}
