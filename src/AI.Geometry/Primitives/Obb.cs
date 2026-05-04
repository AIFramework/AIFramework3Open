using System;
using Vector = AI.DataStructs.Algebraic.Vector;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Primitives;

/// <summary>
/// Ориентированный ограничивающий параллелепипед (Oriented Bounding Box).
/// </summary>
/// <param name="Center">Центр OBB.</param>
/// <param name="HalfExtents">Полуразмеры по локальным осям.</param>
/// <param name="Rotation">Матрица поворота 3×3 (столбцы — локальные оси).</param>
public record Obb(Vector Center, Vector HalfExtents, Matrix Rotation)
{
    /// <summary>
    /// Проверяет, содержит ли OBB данную точку.
    /// </summary>
    public bool Contains(Vector point)
    {
        var d = point - Center;
        for (int i = 0; i < 3; i++)
        {
            var axis = new Vector(new[] { Rotation[0, i], Rotation[1, i], Rotation[2, i] });
            double proj = Math.Abs(Vector.Dot(d, axis));
            if (proj > HalfExtents[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Возвращает 8 вершин OBB.
    /// </summary>
    public Vector[] Corners()
    {
        var corners = new Vector[8];
        for (int i = 0; i < 8; i++)
        {
            double sx = (i & 1) == 0 ? -1 : 1;
            double sy = (i & 2) == 0 ? -1 : 1;
            double sz = (i & 4) == 0 ? -1 : 1;

            var offset = new Vector(3);
            for (int r = 0; r < 3; r++)
            {
                offset[r] = sx * HalfExtents[0] * Rotation[r, 0]
                           + sy * HalfExtents[1] * Rotation[r, 1]
                           + sz * HalfExtents[2] * Rotation[r, 2];
            }
            corners[i] = Center + offset;
        }
        return corners;
    }
}
