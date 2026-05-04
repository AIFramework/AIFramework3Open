using System;
using System.Collections.Generic;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Осеориентированный ограничивающий параллелепипед (Axis-Aligned Bounding Box).
/// </summary>
/// <param name="Min">Минимальный угол.</param>
/// <param name="Max">Максимальный угол.</param>
public record Aabb(Vector Min, Vector Max)
{
    /// <summary>
    /// Центр параллелепипеда.
    /// </summary>
    public Vector Center => 0.5 * (Min + Max);

    /// <summary>
    /// Полуразмеры по каждой оси.
    /// </summary>
    public Vector HalfExtents => 0.5 * (Max - Min);

    /// <summary>
    /// Проверяет, содержит ли AABB данную точку.
    /// </summary>
    public bool Contains(Vector point)
    {
        for (int i = 0; i < point.Count; i++)
        {
            if (point[i] < Min[i] || point[i] > Max[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Проверяет пересечение с другим AABB.
    /// </summary>
    public bool Intersects(Aabb other)
    {
        for (int i = 0; i < Min.Count; i++)
        {
            if (Min[i] > other.Max[i] || Max[i] < other.Min[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Строит AABB по набору точек.
    /// </summary>
    public static Aabb FromPoints(IReadOnlyList<Vector> points)
    {
        int dim = points[0].Count;
        var min = new Vector(dim);
        var max = new Vector(dim);

        for (int d = 0; d < dim; d++)
        {
            min[d] = double.MaxValue;
            max[d] = double.MinValue;
        }

        for (int i = 0; i < points.Count; i++)
        {
            for (int d = 0; d < dim; d++)
            {
                if (points[i][d] < min[d]) min[d] = points[i][d];
                if (points[i][d] > max[d]) max[d] = points[i][d];
            }
        }

        return new Aabb(min, max);
    }
}
