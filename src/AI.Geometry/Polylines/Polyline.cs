#nullable enable
using System;
using AI.Geometry.Distances;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polylines;

/// <summary>
/// Ломаная — вершины, соединённые отрезками: дорога, маршрут, русло.
/// Как и многоугольник в <see cref="Polygons.PointInPolygon"/>, задаётся массивом вершин.
/// </summary>
public static class Polyline
{
    /// <summary>
    /// Длина ломаной — сумма длин звеньев.
    /// </summary>
    public static double Length(Vector[] vertices)
    {
        Require(vertices);

        double length = 0;
        for (int i = 1; i < vertices.Length; i++)
            length += new Segment(vertices[i - 1], vertices[i]).Length;

        return length;
    }

    /// <summary>
    /// Расстояние от точки до ломаной.
    /// </summary>
    public static double Distance(Vector point, Vector[] vertices)
    {
        return Math.Sqrt(SquaredDistance(point, ClosestPoint(point, vertices)));
    }

    /// <summary>
    /// Ближайшая к точке точка ломаной. Совпадающие соседние вершины допустимы: вырожденное звено
    /// считается точкой.
    /// </summary>
    public static Vector ClosestPoint(Vector point, Vector[] vertices)
    {
        Require(vertices);

        Vector best = vertices[0];
        double bestSquared = SquaredDistance(point, best);

        for (int i = 1; i < vertices.Length; i++)
        {
            var link = new Segment(vertices[i - 1], vertices[i]);
            Vector candidate = Vector.Dot(link.Direction, link.Direction) == 0
                ? vertices[i]
                : PointSegment.ClosestPoint(point, link);

            double squared = SquaredDistance(point, candidate);
            if (squared < bestSquared)
            {
                best = candidate;
                bestSquared = squared;
            }
        }

        // Копия: вершина ломаной принадлежит вызывающему, и правка результата не должна её менять
        return new Vector(best);
    }

    private static double SquaredDistance(Vector a, Vector b)
    {
        var diff = a - b;
        return Vector.Dot(diff, diff);
    }

    private static void Require(Vector[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Length == 0)
            throw new ArgumentException("У ломаной нет ни одной вершины", nameof(vertices));
    }
}
