#nullable enable

using System;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Ближайшая точка ориентированного параллелепипеда и расстояние до него.
/// </summary>
/// <remarks>
/// Точка переводится в оси параллелепипеда, там ограничивается полуразмерами и возвращается обратно.
/// </remarks>
public static class PointObb
{
    /// <summary>
    /// Ближайшая к точке точка параллелепипеда, заданного центром, полуразмерами и ориентацией.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="center">Центр параллелепипеда.</param>
    /// <param name="halfExtents">Полуразмеры по локальным осям.</param>
    /// <param name="orientation">Единичный кватернион: поворот из локальных осей в мировые.</param>
    public static Vector3 ClosestPoint(Vector3 point, Vector3 center, Vector3 halfExtents, Quaternion orientation)
    {
        Vector3 local = orientation.Conjugate.Rotate(point - center);
        Vector3 clamped = Vector3.Max(-halfExtents, Vector3.Min(halfExtents, local));
        return center + orientation.Rotate(clamped);
    }

    /// <summary>
    /// Расстояние от точки до параллелепипеда (ноль для точки внутри).
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="center">Центр параллелепипеда.</param>
    /// <param name="halfExtents">Полуразмеры по локальным осям.</param>
    /// <param name="orientation">Единичный кватернион: поворот из локальных осей в мировые.</param>
    public static double Distance(Vector3 point, Vector3 center, Vector3 halfExtents, Quaternion orientation)
        => point.DistanceTo(ClosestPoint(point, center, halfExtents, orientation));

    /// <summary>
    /// Ближайшая к точке точка параллелепипеда <see cref="Obb"/>.
    /// </summary>
    /// <param name="point">Трехмерная точка.</param>
    /// <param name="box">Параллелепипед; столбцы его матрицы поворота считаются единичными осями.</param>
    public static Vector ClosestPoint(Vector point, Obb box)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(box);

        Vector3 d = Vector3.FromVector(point) - Vector3.FromVector(box.Center);
        Vector3 result = Vector3.FromVector(box.Center);

        for (int i = 0; i < 3; i++)
        {
            var axis = new Vector3(box.Rotation[0, i], box.Rotation[1, i], box.Rotation[2, i]);
            double projection = Math.Clamp(d.Dot(axis), -box.HalfExtents[i], box.HalfExtents[i]);
            result += axis * projection;
        }

        return result.ToVector();
    }

    /// <summary>
    /// Расстояние от точки до параллелепипеда <see cref="Obb"/>.
    /// </summary>
    /// <param name="point">Трехмерная точка.</param>
    /// <param name="box">Параллелепипед.</param>
    public static double Distance(Vector point, Obb box)
        => Vector3.FromVector(point).DistanceTo(Vector3.FromVector(ClosestPoint(point, box)));
}
