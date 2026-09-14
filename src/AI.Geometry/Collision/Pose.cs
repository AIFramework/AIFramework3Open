#nullable enable
using System;
using AI.Geometry.Primitives;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Collision;

/// <summary>
/// Положение тела: перенос и поворот. Поворот хранится столбцами матрицы, то есть локальными осями тела в мировых
/// координатах; оси должны быть ортонормированы
/// </summary>
/// <param name="Position">Начало локальных осей в мировых координатах</param>
/// <param name="AxisX">Локальная ось X в мировых координатах</param>
/// <param name="AxisY">Локальная ось Y в мировых координатах</param>
/// <param name="AxisZ">Локальная ось Z в мировых координатах</param>
public readonly record struct Pose(Vector3 Position, Vector3 AxisX, Vector3 AxisY, Vector3 AxisZ)
{
    /// <summary>Положение без переноса и поворота</summary>
    public static Pose Identity => At(Vector3.Zero);

    /// <summary>Положение с переносом без поворота</summary>
    /// <param name="position">Начало локальных осей</param>
    public static Pose At(Vector3 position) => new(position, new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1));

    /// <summary>Положение по переносу и матрице поворота 3×3, столбцы которой локальные оси</summary>
    /// <param name="position">Начало локальных осей</param>
    /// <param name="rotation">Матрица поворота 3×3</param>
    public static Pose FromMatrix(Vector3 position, Matrix rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);

        return new Pose(
            position,
            new Vector3(rotation[0, 0], rotation[1, 0], rotation[2, 0]),
            new Vector3(rotation[0, 1], rotation[1, 1], rotation[2, 1]),
            new Vector3(rotation[0, 2], rotation[1, 2], rotation[2, 2]));
    }

    /// <summary>Локальная ось по номеру 0, 1 или 2</summary>
    /// <param name="index">Номер оси</param>
    public Vector3 Axis(int index) => index switch
    {
        0 => AxisX,
        1 => AxisY,
        2 => AxisZ,
        _ => throw new ArgumentOutOfRangeException(nameof(index), "У тела три оси")
    };

    /// <summary>Поворот направления из локальных осей в мировые</summary>
    /// <param name="local">Направление в локальных осях</param>
    public Vector3 Rotate(Vector3 local) => (AxisX * local.X) + (AxisY * local.Y) + (AxisZ * local.Z);

    /// <summary>Поворот направления из мировых осей в локальные</summary>
    /// <param name="world">Направление в мировых осях</param>
    public Vector3 InverseRotate(Vector3 world) => new(AxisX.Dot(world), AxisY.Dot(world), AxisZ.Dot(world));

    /// <summary>Точка из локальных координат в мировые</summary>
    /// <param name="local">Точка в локальных координатах</param>
    public Vector3 ToWorld(Vector3 local) => Position + Rotate(local);

    /// <summary>Точка из мировых координат в локальные</summary>
    /// <param name="world">Точка в мировых координатах</param>
    public Vector3 ToLocal(Vector3 world) => InverseRotate(world - Position);
}
