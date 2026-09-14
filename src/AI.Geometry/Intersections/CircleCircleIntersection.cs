#nullable enable
using System;
using AI.Geometry.Numerics;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение двух окружностей на плоскости.
/// </summary>
public static class CircleCircleIntersection
{
    /// <summary>
    /// Точки пересечения окружностей: две, одна при касании (с допуском <see cref="Eps.Default"/>, отнесенным к радиусу)
    /// или ни одной. Для концентрических окружностей, в том числе совпадающих, возвращается пустой массив.
    /// </summary>
    /// <param name="a">Первая окружность.</param>
    /// <param name="b">Вторая окружность.</param>
    /// <returns>Точки пересечения; при двух точках первая лежит слева от направления из центра a в центр b.</returns>
    public static Vector[] Intersect(Circle a, Circle b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        double ux = b.Center[0] - a.Center[0], uy = b.Center[1] - a.Center[1];
        double d = Math.Sqrt(ux * ux + uy * uy);
        if (d == 0)
            return Array.Empty<Vector>();

        double tolerance = Eps.Default * Math.Max(1.0, Math.Max(a.Radius, b.Radius));
        if (d > a.Radius + b.Radius + tolerance || d < Math.Abs(a.Radius - b.Radius) - tolerance)
            return Array.Empty<Vector>();

        ux /= d;
        uy /= d;

        // Расстояние от центра a до хорды вдоль линии центров и половина хорды
        double along = (d * d + a.Radius * a.Radius - b.Radius * b.Radius) / (2 * d);
        double half = Math.Sqrt(Math.Max(0.0, a.Radius * a.Radius - along * along));
        double baseX = a.Center[0] + along * ux, baseY = a.Center[1] + along * uy;

        if (half <= tolerance)
            return [new Vector(baseX, baseY)];

        return
        [
            new Vector(baseX - half * uy, baseY + half * ux),
            new Vector(baseX + half * uy, baseY - half * ux),
        ];
    }
}
