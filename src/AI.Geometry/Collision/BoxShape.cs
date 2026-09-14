#nullable enable
using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Ориентированная коробка для поиска столкновений
/// </summary>
/// <param name="Pose">Центр и оси коробки</param>
/// <param name="HalfExtents">Полуразмеры по локальным осям</param>
public sealed record BoxShape(Pose Pose, Vector3 HalfExtents) : IConvexShape
{
    /// <summary>Коробка из примитива <see cref="Obb"/></summary>
    /// <param name="box">Ориентированный параллелепипед</param>
    public static BoxShape FromObb(Obb box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return new BoxShape(Pose.FromMatrix(Vector3.FromVector(box.Center), box.Rotation), Vector3.FromVector(box.HalfExtents));
    }

    /// <inheritdoc/>
    public Vector3 Center => Pose.Position;

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction)
    {
        var local = Pose.InverseRotate(direction);
        return Pose.ToWorld(new Vector3(
            local.X < 0 ? -HalfExtents.X : HalfExtents.X,
            local.Y < 0 ? -HalfExtents.Y : HalfExtents.Y,
            local.Z < 0 ? -HalfExtents.Z : HalfExtents.Z));
    }

    /// <summary>Вершина по номеру от 0 до 7: биты номера задают знаки полуразмеров по X, Y, Z</summary>
    /// <param name="index">Номер вершины</param>
    public Vector3 Corner(int index) => Pose.ToWorld(new Vector3(
        (index & 1) == 0 ? -HalfExtents.X : HalfExtents.X,
        (index & 2) == 0 ? -HalfExtents.Y : HalfExtents.Y,
        (index & 4) == 0 ? -HalfExtents.Z : HalfExtents.Z));

    /// <summary>Ближайшая к точке точка коробки; для точки внутри это она сама</summary>
    /// <param name="point">Точка в мировых координатах</param>
    public Vector3 ClosestPoint(Vector3 point)
    {
        var local = Pose.ToLocal(point);
        return Pose.ToWorld(new Vector3(
            Math.Clamp(local.X, -HalfExtents.X, HalfExtents.X),
            Math.Clamp(local.Y, -HalfExtents.Y, HalfExtents.Y),
            Math.Clamp(local.Z, -HalfExtents.Z, HalfExtents.Z)));
    }
}
