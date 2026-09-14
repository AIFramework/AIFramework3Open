using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение двух отрезков на плоскости (2D).
/// </summary>
public static class SegmentSegmentIntersection
{
    /// <summary>
    /// Находит точку пересечения двух 2D-отрезков. Возвращает null, если не пересекаются.
    /// Обрабатывает коллинеарный случай (возвращает null — не выделяет отрезок перекрытия).
    /// </summary>
    public static Vector Intersect(Segment s1, Segment s2)
    {
        var d1 = s1.B - s1.A;
        var d2 = s2.B - s2.A;
        var d = s2.A - s1.A;

        double cross = d1[0] * d2[1] - d1[1] * d2[0];
        if (Eps.IsZero(cross))
            return null;

        double t = (d[0] * d2[1] - d[1] * d2[0]) / cross;
        double u = (d[0] * d1[1] - d[1] * d1[0]) / cross;

        if (t >= -Eps.Default && t <= 1 + Eps.Default &&
            u >= -Eps.Default && u <= 1 + Eps.Default)
        {
            return s1.PointAt(t);
        }

        return null;
    }

    /// <summary>
    /// Общая часть двух 2D-отрезков: отрезок перекрытия для лежащих на одной прямой, вырожденный отрезок (A = B)
    /// для пересечения или касания в точке, null, если общих точек нет. В отличие от <see cref="Intersect"/>,
    /// коллинеарное перекрытие не теряется. Отклонение от прямой и параметры сравниваются с допуском
    /// <see cref="Eps.Default"/>, отнесенным к длине отрезков.
    /// </summary>
    /// <param name="s1">Первый отрезок.</param>
    /// <param name="s2">Второй отрезок.</param>
    /// <returns>Общая часть или null.</returns>
    public static Segment CommonPart(Segment s1, Segment s2)
    {
        ArgumentNullException.ThrowIfNull(s1);
        ArgumentNullException.ThrowIfNull(s2);

        var d1 = s1.Direction;
        var d2 = s2.Direction;
        double len1 = Math.Sqrt(Vector.Dot(d1, d1));
        double len2 = Math.Sqrt(Vector.Dot(d2, d2));
        double tolerance = Eps.Default * Math.Max(1.0, Math.Max(len1, len2));

        if (len1 == 0 || len2 == 0)
        {
            var (point, other) = len1 == 0 ? (s1.A, s2) : (s2.A, s1);
            var offset = point - (len1 == 0 && len2 == 0 ? other.A : Distances.PointSegment.ClosestPoint(point, other));
            return Math.Sqrt(Vector.Dot(offset, offset)) <= tolerance ? new Segment(new Vector(point), new Vector(point)) : null;
        }

        var toA = s2.A - s1.A;
        var toB = s2.B - s1.A;
        double offA = (d1[0] * toA[1] - d1[1] * toA[0]) / len1;
        double offB = (d1[0] * toB[1] - d1[1] * toB[0]) / len1;

        if (Math.Abs(offA) <= tolerance && Math.Abs(offB) <= tolerance)
        {
            // На одной прямой: пересечение интервалов параметров вдоль s1
            double tA = Vector.Dot(toA, d1) / (len1 * len1);
            double tB = Vector.Dot(toB, d1) / (len1 * len1);
            var (low, lowPoint) = tA <= tB ? (tA, s2.A) : (tB, s2.B);
            var (high, highPoint) = tA <= tB ? (tB, s2.B) : (tA, s2.A);
            double slack = tolerance / len1;

            if (high < -slack || low > 1 + slack)
                return null;

            var start = low > 0 ? lowPoint : s1.A;
            var end = high < 1 ? highPoint : s1.B;
            if (Math.Max(low, 0) > Math.Min(high, 1))
                end = start;
            return new Segment(new Vector(start), new Vector(end));
        }

        double cross = d1[0] * d2[1] - d1[1] * d2[0];
        if (Math.Abs(cross) <= Eps.Default * len1 * len2)
            return null;

        double t = (toA[0] * d2[1] - toA[1] * d2[0]) / cross;
        double u = (toA[0] * d1[1] - toA[1] * d1[0]) / cross;
        double slack1 = tolerance / len1, slack2 = tolerance / len2;
        if (t < -slack1 || t > 1 + slack1 || u < -slack2 || u > 1 + slack2)
            return null;

        var hit = s1.PointAt(Math.Clamp(t, 0.0, 1.0));
        return new Segment(hit, new Vector(hit));
    }
}
