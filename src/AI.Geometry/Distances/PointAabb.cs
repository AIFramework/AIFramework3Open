#nullable enable

using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Ближайшая точка осеориентированного параллелепипеда и расстояние до него.
/// </summary>
/// <remarks>
/// Ближайшая точка получается ограничением каждой координаты отрезком [Min, Max]; для точки внутри
/// параллелепипеда это она сама, и расстояние равно нулю.
/// </remarks>
public static class PointAabb
{
    /// <summary>
    /// Ближайшая к точке точка параллелепипеда.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="min">Минимальный угол.</param>
    /// <param name="max">Максимальный угол.</param>
    public static Vector3 ClosestPoint(Vector3 point, Vector3 min, Vector3 max)
        => Vector3.Max(min, Vector3.Min(max, point));

    /// <summary>
    /// Расстояние от точки до параллелепипеда (ноль для точки внутри).
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="min">Минимальный угол.</param>
    /// <param name="max">Максимальный угол.</param>
    public static double Distance(Vector3 point, Vector3 min, Vector3 max)
        => point.DistanceTo(ClosestPoint(point, min, max));

    /// <summary>
    /// Ближайшая к точке точка параллелепипеда любой размерности.
    /// </summary>
    /// <param name="point">Точка той же размерности, что и параллелепипед.</param>
    /// <param name="box">Параллелепипед.</param>
    public static Vector ClosestPoint(Vector point, Aabb box)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(box);

        var closest = new Vector(point.Count);

        for (int i = 0; i < point.Count; i++)
            closest[i] = Math.Clamp(point[i], box.Min[i], box.Max[i]);

        return closest;
    }

    /// <summary>
    /// Расстояние от точки до параллелепипеда любой размерности.
    /// </summary>
    /// <param name="point">Точка той же размерности, что и параллелепипед.</param>
    /// <param name="box">Параллелепипед.</param>
    public static double Distance(Vector point, Aabb box)
    {
        var diff = point - ClosestPoint(point, box);
        return Math.Sqrt(Vector.Dot(diff, diff));
    }
}
