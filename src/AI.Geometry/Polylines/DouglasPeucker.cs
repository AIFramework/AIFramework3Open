#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Distances;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polylines;

/// <summary>
/// Упрощение ломаной алгоритмом Дугласа–Пекера: остаются вершины, без которых ломаная отклонилась бы больше допуска.
/// </summary>
public static class DouglasPeucker
{
    /// <summary>
    /// Упрощает ломаную. Концы сохраняются всегда; каждая выброшенная вершина лежит не дальше допуска от звена,
    /// которое ее заменило.
    /// </summary>
    /// <param name="vertices">Вершины ломаной (2D или 3D).</param>
    /// <param name="tolerance">Наибольшее допустимое отклонение, неотрицательное.</param>
    /// <returns>Копии оставленных вершин в исходном порядке.</returns>
    public static Vector[] Simplify(Vector[] vertices, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (!(tolerance >= 0))
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Допуск должен быть неотрицательным");

        int n = vertices.Length;
        var keep = new bool[n];
        if (n > 0)
        {
            keep[0] = true;
            keep[n - 1] = true;
        }

        // Стек вместо рекурсии: длинная ломаная не переполнит стек вызовов
        var ranges = new Stack<(int First, int Last)>();
        if (n > 2)
            ranges.Push((0, n - 1));

        while (ranges.Count > 0)
        {
            var (first, last) = ranges.Pop();
            var chord = new Segment(vertices[first], vertices[last]);
            bool degenerate = Vector.Dot(chord.Direction, chord.Direction) == 0;

            double worst = -1;
            int worstIndex = -1;
            for (int k = first + 1; k < last; k++)
            {
                double deviation = degenerate
                    ? Math.Sqrt(Vector.Dot(vertices[k] - chord.A, vertices[k] - chord.A))
                    : PointSegment.Distance(vertices[k], chord);
                if (deviation > worst)
                {
                    worst = deviation;
                    worstIndex = k;
                }
            }

            if (worst > tolerance)
            {
                keep[worstIndex] = true;
                ranges.Push((first, worstIndex));
                ranges.Push((worstIndex, last));
            }
        }

        var result = new List<Vector>();
        for (int k = 0; k < n; k++)
        {
            if (keep[k])
                result.Add(new Vector(vertices[k]));
        }

        return result.ToArray();
    }
}
