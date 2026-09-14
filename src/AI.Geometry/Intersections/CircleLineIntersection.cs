#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Numerics;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение окружности с прямой и с отрезком на плоскости.
/// </summary>
public static class CircleLineIntersection
{
    /// <summary>
    /// Точки пересечения окружности с прямой: две, одна при касании (с допуском <see cref="Eps.Default"/>, отнесенным
    /// к радиусу) или ни одной.
    /// </summary>
    /// <param name="circle">Окружность.</param>
    /// <param name="line">Прямая с ненулевым направлением.</param>
    /// <returns>Точки в порядке возрастания параметра прямой.</returns>
    public static Vector[] Intersect(Circle circle, Line2D line)
    {
        ArgumentNullException.ThrowIfNull(circle);
        ArgumentNullException.ThrowIfNull(line);

        var parameters = Parameters(circle, line.Point, line.Direction);
        var points = new Vector[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            points[i] = line.PointAt(parameters[i]);
        return points;
    }

    /// <summary>
    /// Точки пересечения окружности с отрезком (концы включаются с допуском <see cref="Eps.Default"/> по параметру).
    /// Вырожденный отрезок дает свою точку, если она лежит на окружности.
    /// </summary>
    /// <param name="circle">Окружность.</param>
    /// <param name="segment">Отрезок.</param>
    /// <returns>Точки в порядке от A к B.</returns>
    public static Vector[] Intersect(Circle circle, Segment segment)
    {
        ArgumentNullException.ThrowIfNull(circle);
        ArgumentNullException.ThrowIfNull(segment);

        var direction = segment.Direction;
        if (direction[0] == 0 && direction[1] == 0)
        {
            var offset = segment.A - circle.Center;
            double distance = Math.Sqrt(Vector.Dot(offset, offset));
            return Math.Abs(distance - circle.Radius) <= Tolerance(circle)
                ? [new Vector(segment.A)]
                : Array.Empty<Vector>();
        }

        var points = new List<Vector>(2);
        foreach (double t in Parameters(circle, segment.A, direction))
        {
            if (t >= -Eps.Default && t <= 1 + Eps.Default)
                points.Add(segment.PointAt(Math.Clamp(t, 0.0, 1.0)));
        }

        return points.ToArray();
    }

    /// <summary>Параметры t точек P + t·D на окружности по возрастанию.</summary>
    private static double[] Parameters(Circle circle, Vector point, Vector direction)
    {
        double dx = direction[0], dy = direction[1];
        double dd = dx * dx + dy * dy;
        if (dd == 0)
            throw new ArgumentException("Направление прямой нулевое", nameof(direction));

        double fx = point[0] - circle.Center[0], fy = point[1] - circle.Center[1];

        // Основание перпендикуляра из центра и расстояние до прямой считаются без вычитания близких квадратов
        double foot = -(fx * dx + fy * dy) / dd;
        double gx = fx + foot * dx, gy = fy + foot * dy;
        double distance = Math.Sqrt(gx * gx + gy * gy);
        double tolerance = Tolerance(circle);

        if (distance > circle.Radius + tolerance)
            return Array.Empty<double>();

        double half = Math.Sqrt(Math.Max(0.0, (circle.Radius - distance) * (circle.Radius + distance)));
        if (half <= tolerance)
            return [foot];

        double step = half / Math.Sqrt(dd);
        return [foot - step, foot + step];
    }

    private static double Tolerance(Circle circle) => Eps.Default * Math.Max(1.0, circle.Radius);
}
