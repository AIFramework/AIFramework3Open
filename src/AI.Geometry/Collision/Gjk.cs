#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Алгоритм Гилберта, Джонсона и Кирти: ближайшая к началу координат точка разности Минковского A − B. Если начало
/// внутри разности, формы пересекаются; иначе расстояние до нее равно расстоянию между формами, а веса вершин
/// симплекса дают ближайшие точки на каждой форме
/// </summary>
public static class Gjk
{
    private const int MaxIterations = 128;

    /// <summary>Относительный допуск сходимости: зазор v·v − v·w против v·v</summary>
    private const double RelativeTolerance = 1e-12;

    /// <summary>Допуск округления в долях размера разности Минковского</summary>
    private const double RoundoffTolerance = 1e-14;

    /// <summary>Пересекаются ли формы (касание считается пересечением)</summary>
    /// <param name="a">Первая форма</param>
    /// <param name="b">Вторая форма</param>
    public static bool Intersects(IConvexShape a, IConvexShape b) => Run(a, b, Vector3.Zero).Intersecting;

    /// <summary>Расстояние между формами и ближайшие точки на них; у пересекающихся форм расстояние 0 и общая точка</summary>
    /// <param name="a">Первая форма</param>
    /// <param name="b">Вторая форма</param>
    /// <returns>Расстояние, точка на A и точка на B</returns>
    public static (double Distance, Vector3 OnA, Vector3 OnB) Distance(IConvexShape a, IConvexShape b)
    {
        var result = Run(a, b, Vector3.Zero);
        return (result.Distance, result.OnA, result.OnB);
    }

    /// <summary>Итерации GJK; форма B сдвинута на offsetB. Возвращает и последний симплекс для EPA</summary>
    internal static (bool Intersecting, double Distance, Vector3 OnA, Vector3 OnB, SupportPoint[] Simplex) Run(
        IConvexShape a, IConvexShape b, Vector3 offsetB)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var direction = b.Center + offsetB - a.Center;
        if (direction.LengthSquared == 0)
            direction = new Vector3(1, 0, 0);

        var simplex = new[] { SupportPoint.Of(a, b, offsetB, direction) };
        double[] weights = [1];
        var v = simplex[0].W;
        double scale = Math.Max(v.Length, 1e-300);

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            double vv = v.LengthSquared;
            if (vv <= Square(RoundoffTolerance * scale * 100))
                return Finish(true, simplex, weights);

            var next = SupportPoint.Of(a, b, offsetB, -v);
            scale = Math.Max(scale, next.W.Length);
            double gap = vv - v.Dot(next.W);
            if (gap <= (RelativeTolerance * vv) + (RoundoffTolerance * scale * Math.Sqrt(vv)))
                break;
            if (simplex.Any(point => point.W == next.W))
                break;

            var (reduced, reducedWeights, inside) = Closest([.. simplex, next]);
            (simplex, weights) = (reduced, reducedWeights);
            if (inside)
                return Finish(true, simplex, weights);

            var closer = SupportPoint.Combine(simplex, weights).W;
            bool stalled = closer.LengthSquared >= vv;
            v = closer;
            if (stalled)
                break;
        }

        return Finish(false, simplex, weights);
    }

    /// <summary>Барицентрические веса ближайшей к p точки треугольника abc (Эриксон, раздел 5.1.5)</summary>
    internal static (double U, double V, double W) TriangleWeights(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var (ab, ac, ap) = (b - a, c - a, p - a);
        double d1 = ab.Dot(ap), d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0)
            return (1, 0, 0);

        var bp = p - b;
        double d3 = ab.Dot(bp), d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3)
            return (0, 1, 0);

        double vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            double t = Ratio(d1, d1 - d3);
            return (1 - t, t, 0);
        }

        var cp = p - c;
        double d5 = ab.Dot(cp), d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6)
            return (0, 0, 1);

        double vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            double t = Ratio(d2, d2 - d6);
            return (1 - t, 0, t);
        }

        double va = (d3 * d6) - (d5 * d4);
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            double t = Ratio(d4 - d3, (d4 - d3) + (d5 - d6));
            return (0, 1 - t, t);
        }

        double denominator = va + vb + vc;
        if (denominator <= 0)
            return DegenerateTriangle(p, a, b, c);

        double v = vb / denominator, w = vc / denominator;
        return (1 - v - w, v, w);
    }

    private static (bool, double, Vector3, Vector3, SupportPoint[]) Finish(bool intersecting, SupportPoint[] simplex, double[] weights)
    {
        var (w, onA, onB) = SupportPoint.Combine(simplex, weights);
        return (intersecting, intersecting ? 0 : w.Length, onA, onB, simplex);
    }

    /// <summary>
    /// Ближайшая к началу координат точка симплекса: остаются только вершины с положительным весом; inside означает,
    /// что начало внутри тетраэдра
    /// </summary>
    private static (SupportPoint[] Simplex, double[] Weights, bool Inside) Closest(SupportPoint[] simplex)
    {
        double[] weights = simplex.Length switch
        {
            2 => SegmentWeights(simplex[0].W, simplex[1].W),
            3 => Unpack(TriangleWeights(Vector3.Zero, simplex[0].W, simplex[1].W, simplex[2].W)),
            _ => TetrahedronWeights(simplex),
        };

        bool inside = simplex.Length == 4 && weights.All(weight => weight > 0);
        var kept = Enumerable.Range(0, simplex.Length).Where(i => weights[i] > 0).ToArray();
        return (kept.Select(i => simplex[i]).ToArray(), kept.Select(i => weights[i]).ToArray(), inside);
    }

    private static double[] SegmentWeights(Vector3 a, Vector3 b)
    {
        var ab = b - a;
        double length = ab.LengthSquared;
        double t = length > 0 ? Math.Clamp(-a.Dot(ab) / length, 0, 1) : 0;
        return [1 - t, t];
    }

    /// <summary>
    /// Веса ближайшей к началу точки тетраэдра. Начало снаружи грани, если оно и противолежащая вершина по разные
    /// стороны от ее плоскости; у вырожденного тетраэдра снаружи считаются все грани. Если начало не снаружи ни одной
    /// грани, оно внутри, и веса это его барицентрические координаты
    /// </summary>
    private static double[] TetrahedronWeights(SupportPoint[] simplex)
    {
        int[][] faces = [[1, 2, 3, 0], [0, 3, 2, 1], [0, 1, 3, 2], [0, 2, 1, 3]];
        var interior = new double[4];
        double[]? best = null;
        double bestDistance = double.PositiveInfinity;
        bool inside = true;
        foreach (var face in faces)
        {
            var (a, b, c, opposite) = (simplex[face[0]].W, simplex[face[1]].W, simplex[face[2]].W, simplex[face[3]].W);
            var normal = (b - a).Cross(c - a);
            double origin = -normal.Dot(a), vertex = normal.Dot(opposite - a);
            bool degenerate = Math.Abs(vertex) <= 1e-12 * Math.Max(normal.Length * (opposite - a).Length, 1e-300);
            if (!degenerate && origin * vertex >= 0)
            {
                interior[face[3]] = origin / vertex;
                continue;
            }

            inside = false;
            var (u, v, w) = TriangleWeights(Vector3.Zero, a, b, c);
            double distance = ((a * u) + (b * v) + (c * w)).LengthSquared;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new double[4];
                (best[face[0]], best[face[1]], best[face[2]]) = (u, v, w);
            }
        }

        return inside ? interior : best!;
    }

    private static (double, double, double) DegenerateTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        (double, double, double) best = (1, 0, 0);
        double bestDistance = double.PositiveInfinity;
        foreach (var (from, to, i, j) in new[] { (a, b, 0, 1), (b, c, 1, 2), (a, c, 0, 2) })
        {
            var weights = SegmentWeights(from - p, to - p);
            double distance = ((from * weights[0]) + (to * weights[1]) - p).LengthSquared;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            var packed = new double[3];
            (packed[i], packed[j]) = (weights[0], weights[1]);
            best = (packed[0], packed[1], packed[2]);
        }

        return best;
    }

    private static double[] Unpack((double U, double V, double W) weights) => [weights.U, weights.V, weights.W];

    private static double Ratio(double numerator, double denominator) => denominator != 0 ? numerator / denominator : 0;

    private static double Square(double value) => value * value;
}
