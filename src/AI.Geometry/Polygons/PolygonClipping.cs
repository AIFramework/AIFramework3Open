#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Numerics;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Отсечение многоугольника выпуклым многоугольником (Сазерленд–Ходжмен) и полуплоскостью.
/// </summary>
/// <remarks>
/// Точки на границе отсекателя считаются внутренними. Для выпуклого отсекаемого многоугольника результат выпуклый;
/// у невыпуклого результат может содержать вырожденные стороны вдоль границы отсекателя (свойство алгоритма).
/// </remarks>
public static class PolygonClipping
{
    /// <summary>
    /// Часть многоугольника внутри выпуклого многоугольника. Обход отсекателя может быть любым.
    /// </summary>
    /// <param name="subject">Отсекаемый многоугольник (2D).</param>
    /// <param name="clip">Выпуклый отсекатель (2D), не меньше трех вершин.</param>
    /// <returns>Вершины результата; пустой массив, если в результате меньше трех вершин.</returns>
    public static Vector[] SutherlandHodgman(Vector[] subject, Vector[] clip)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(clip);

        var window = Distinct(ToPoints(clip));
        if (window.Count < 3)
            throw new ArgumentException("У отсекателя меньше трех различных вершин", nameof(clip));
        if (ShoelaceArea.SignedArea(clip) < 0)
            window.Reverse();

        var result = ToPoints(subject);
        for (int i = 0; i < window.Count && result.Count > 0; i++)
        {
            var (ax, ay) = window[i];
            var (bx, by) = window[(i + 1) % window.Count];
            result = ClipBy(
                result,
                p => (bx - ax) * (p.Y - ay) - (by - ay) * (p.X - ax),
                p => RobustPredicates.Orient2D(ax, ay, bx, by, p.X, p.Y));
        }

        return ToVectors(result);
    }

    /// <summary>
    /// Часть многоугольника в полуплоскости слева от направленной прямой (по ходу направления).
    /// </summary>
    /// <param name="polygon">Отсекаемый многоугольник (2D).</param>
    /// <param name="boundary">Граница полуплоскости; сохраняется сторона слева от направления.</param>
    /// <returns>Вершины результата; пустой массив, если в результате меньше трех вершин.</returns>
    public static Vector[] HalfPlane(Vector[] polygon, Line2D boundary)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        ArgumentNullException.ThrowIfNull(boundary);

        double px = boundary.Point[0], py = boundary.Point[1];
        double dx = boundary.Direction[0], dy = boundary.Direction[1];
        if (dx == 0 && dy == 0)
            throw new ArgumentException("Направление прямой нулевое", nameof(boundary));

        var result = ClipBy(ToPoints(polygon), p => dx * (p.Y - py) - dy * (p.X - px), null);
        return ToVectors(result);
    }

    /// <summary>
    /// Один шаг Сазерленда–Ходжмена: side дает расстояние со знаком (для точки пересечения), sign дает точный знак стороны.
    /// </summary>
    private static List<(double X, double Y)> ClipBy(
        List<(double X, double Y)> input,
        Func<(double X, double Y), double> side,
        Func<(double X, double Y), int>? sign)
    {
        var output = new List<(double X, double Y)>(input.Count + 2);
        if (input.Count == 0)
            return output;

        var previous = input[^1];
        double previousSide = side(previous);
        int previousSign = sign?.Invoke(previous) ?? Math.Sign(previousSide);

        foreach (var current in input)
        {
            double currentSide = side(current);
            int currentSign = sign?.Invoke(current) ?? Math.Sign(currentSide);

            if (currentSign >= 0)
            {
                if (previousSign < 0 && currentSign > 0)
                    output.Add(Crossing(previous, current, previousSide, currentSide));
                output.Add(current);
            }
            else if (previousSign > 0)
            {
                output.Add(Crossing(previous, current, previousSide, currentSide));
            }

            (previous, previousSide, previousSign) = (current, currentSide, currentSign);
        }

        return Distinct(output);
    }

    private static (double X, double Y) Crossing((double X, double Y) p, (double X, double Y) q, double sp, double sq)
    {
        // Знаки заданы точно, а значения приближенно, поэтому параметр ограничивается отрезком
        double t = sp == sq ? 0.5 : Math.Clamp(sp / (sp - sq), 0.0, 1.0);
        return (p.X + t * (q.X - p.X), p.Y + t * (q.Y - p.Y));
    }

    private static List<(double X, double Y)> ToPoints(Vector[] polygon)
    {
        var points = new List<(double X, double Y)>(polygon.Length);
        foreach (var v in polygon)
            points.Add((v[0], v[1]));
        return points;
    }

    /// <summary>Убирает подряд идущие совпадающие вершины, включая пару последняя–первая.</summary>
    private static List<(double X, double Y)> Distinct(List<(double X, double Y)> points)
    {
        var result = new List<(double X, double Y)>(points.Count);
        foreach (var p in points)
        {
            if (result.Count == 0 || result[^1] != p)
                result.Add(p);
        }

        while (result.Count > 1 && result[0] == result[^1])
            result.RemoveAt(result.Count - 1);
        return result;
    }

    private static Vector[] ToVectors(List<(double X, double Y)> points)
    {
        if (points.Count < 3)
            return Array.Empty<Vector>();

        var result = new Vector[points.Count];
        for (int i = 0; i < points.Count; i++)
            result[i] = new Vector(points[i].X, points[i].Y);
        return result;
    }
}
