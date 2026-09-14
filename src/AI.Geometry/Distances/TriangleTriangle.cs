#nullable enable

using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Distances;

/// <summary>
/// Расстояние и ближайшие точки двух треугольников в 3D.
/// </summary>
/// <remarks>
/// У непересекающихся треугольников минимум достигается на паре «вершина – треугольник» или
/// «ребро – ребро», поэтому перебираются 6 первых и 9 вторых пар. Если треугольники пересекаются,
/// какое-то ребро одного протыкает другой: точка, где ребро пересекает плоскость другого треугольника,
/// лежит в нем, и эта пара дает нулевое расстояние. Касание и наложение в одной плоскости
/// покрываются парами «ребро – ребро» и «вершина – треугольник».
/// </remarks>
public static class TriangleTriangle
{
    /// <summary>
    /// Расстояние и ближайшие точки двух треугольников.
    /// </summary>
    /// <param name="a0">Первая вершина первого треугольника.</param>
    /// <param name="a1">Вторая вершина первого треугольника.</param>
    /// <param name="a2">Третья вершина первого треугольника.</param>
    /// <param name="b0">Первая вершина второго треугольника.</param>
    /// <param name="b1">Вторая вершина второго треугольника.</param>
    /// <param name="b2">Третья вершина второго треугольника.</param>
    /// <returns>Расстояние и точки на первом и втором треугольнике; при пересечении это их общая точка.</returns>
    public static (double Distance, Vector3 OnA, Vector3 OnB) Closest(
        Vector3 a0, Vector3 a1, Vector3 a2, Vector3 b0, Vector3 b1, Vector3 b2)
    {
        Span<Vector3> a = [a0, a1, a2];
        Span<Vector3> b = [b0, b1, b2];
        double best = double.PositiveInfinity;
        Vector3 onA = a0, onB = b0;

        void Consider(Vector3 p, Vector3 q)
        {
            double d = (p - q).LengthSquared;

            if (d < best)
            {
                best = d;
                onA = p;
                onB = q;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            Consider(a[i], PointTriangle.ClosestPoint(a[i], b0, b1, b2));
            Consider(PointTriangle.ClosestPoint(b[i], a0, a1, a2), b[i]);

            for (int j = 0; j < 3; j++)
            {
                var (_, _, p, q) = SegmentSegment.ClosestPoints(a[i], a[(i + 1) % 3], b[j], b[(j + 1) % 3]);
                Consider(p, q);
            }

            if (Crossing(a[i], a[(i + 1) % 3], b0, b1, b2) is Vector3 onEdgeA)
                Consider(onEdgeA, PointTriangle.ClosestPoint(onEdgeA, b0, b1, b2));

            if (Crossing(b[i], b[(i + 1) % 3], a0, a1, a2) is Vector3 onEdgeB)
                Consider(PointTriangle.ClosestPoint(onEdgeB, a0, a1, a2), onEdgeB);
        }

        return (Math.Sqrt(best), onA, onB);
    }

    /// <summary>
    /// Расстояние между двумя трехмерными треугольниками.
    /// </summary>
    /// <param name="a">Первый треугольник.</param>
    /// <param name="b">Второй треугольник.</param>
    public static double Distance(Triangle a, Triangle b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return Closest(
            Vector3.FromVector(a.A), Vector3.FromVector(a.B), Vector3.FromVector(a.C),
            Vector3.FromVector(b.A), Vector3.FromVector(b.B), Vector3.FromVector(b.C)).Distance;
    }

    /// <summary>
    /// Точка, где отрезок строго пересекает плоскость треугольника, либо null.
    /// </summary>
    private static Vector3? Crossing(Vector3 p, Vector3 q, Vector3 t0, Vector3 t1, Vector3 t2)
    {
        Vector3 normal = (t1 - t0).Cross(t2 - t0);
        double dp = normal.Dot(p - t0);
        double dq = normal.Dot(q - t0);

        if ((dp > 0 && dq < 0) || (dp < 0 && dq > 0))
            return Vector3.Lerp(p, q, dp / (dp - dq));

        return null;
    }
}
