#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Numerics;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Triangulation;

/// <summary>
/// Триангуляция простого многоугольника с дырами отсечением ушей.
/// </summary>
/// <remarks>
/// Каждая дыра соединяется с внешним контуром мостом (разрез туда и обратно от самой правой вершины дыры до видимой
/// вершины контура, по Эберли), после чего остается один слабо простой контур. От него по одному отрезаются уши:
/// выпуклые вершины, в треугольнике которых нет вогнутых вершин контура. Повороты проверяются точным предикатом
/// <see cref="RobustPredicates"/>. Вершины на одной прямой и повторы допустимы. Сложность O(n²) в худшем случае.
/// Для самопересекающегося входа результат не гарантирован.
/// </remarks>
public static class EarClipping
{
    /// <summary>
    /// Разбивает многоугольник с дырами на треугольники. Обход контуров может быть любым: внешний приводится
    /// к обходу против часовой стрелки, дыры к обходу по часовой.
    /// </summary>
    /// <param name="outer">Внешний контур (2D).</param>
    /// <param name="holes">Дыры (2D): простые, лежат внутри внешнего контура и не пересекаются между собой.</param>
    /// <returns>Треугольники с обходом против часовой стрелки; их площади в сумме равны площади многоугольника.</returns>
    public static IReadOnlyList<Triangle> Triangulate(Vector[] outer, params Vector[][] holes)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(holes);

        var points = new List<(double X, double Y)>();
        var ring = Ring(outer, counterClockwise: true, points);
        if (ring.Count < 3)
            return Array.Empty<Triangle>();

        var holeRings = new List<List<int>>();
        foreach (var hole in holes)
        {
            if (hole is null)
                throw new ArgumentException("Дыра не задана", nameof(holes));
            var holeRing = Ring(hole, counterClockwise: false, points);
            if (holeRing.Count >= 3)
                holeRings.Add(holeRing);
        }

        // Правые дыры подключаются первыми: луч вправо от крайней вершины не встретит еще не подключенных дыр
        holeRings.Sort((p, q) => MaxX(q, points).CompareTo(MaxX(p, points)));
        foreach (var hole in holeRings)
            ring = Bridge(ring, hole, points);

        return Clip(ring, points);
    }

    /// <summary>Индексы вершин контура без подряд идущих повторов, в нужном направлении обхода.</summary>
    private static List<int> Ring(Vector[] polygon, bool counterClockwise, List<(double X, double Y)> points)
    {
        var ring = new List<int>(polygon.Length);
        foreach (var vertex in polygon)
        {
            var point = (vertex[0], vertex[1]);
            if (ring.Count > 0 && points[ring[^1]] == point)
                continue;
            points.Add(point);
            ring.Add(points.Count - 1);
        }

        while (ring.Count > 1 && points[ring[0]] == points[ring[^1]])
            ring.RemoveAt(ring.Count - 1);

        if (ShoelaceArea.SignedArea(polygon) > 0 != counterClockwise)
            ring.Reverse();
        return ring;
    }

    private static double MaxX(List<int> ring, List<(double X, double Y)> points)
    {
        double max = double.NegativeInfinity;
        foreach (int index in ring)
            max = Math.Max(max, points[index].X);
        return max;
    }

    /// <summary>
    /// Вставляет дыру в контур: ..., P, M, (обход дыры), M, P, ..., где M есть самая правая вершина дыры,
    /// а P есть вершина контура, видимая из M.
    /// </summary>
    private static List<int> Bridge(List<int> ring, List<int> hole, List<(double X, double Y)> points)
    {
        int mPos = 0;
        for (int k = 1; k < hole.Count; k++)
        {
            if (points[hole[k]].X > points[hole[mPos]].X)
                mPos = k;
        }

        var m = points[hole[mPos]];
        int n = ring.Count;

        // Ближайшая сторона контура на луче из M вправо. Из внутренности луч выходит через стороны, идущие вверх
        double hitX = double.PositiveInfinity;
        int pPos = -1;
        for (int i = 0; i < n; i++)
        {
            var a = points[ring[i]];
            var b = points[ring[(i + 1) % n]];
            if (!(a.Y <= m.Y && m.Y <= b.Y && a.Y < b.Y))
                continue;

            double x = a.Y == m.Y ? a.X : b.Y == m.Y ? b.X : a.X + (m.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
            if (x < m.X || x >= hitX)
                continue;

            hitX = x;
            pPos = a.Y == m.Y ? i : b.Y == m.Y ? (i + 1) % n : (a.X > b.X ? i : (i + 1) % n);
        }

        if (pPos < 0)
            throw new ArgumentException("Дыра не лежит внутри внешнего контура", "holes");

        // Если в треугольнике M, I, P есть вершины контура, P может быть не виден: берется вершина с наименьшим углом к лучу
        var p = points[ring[pPos]];
        if (hitX > m.X)
        {
            var hit = (X: hitX, m.Y);
            double bestTan = double.PositiveInfinity;
            int best = pPos;
            for (int k = 0; k < n; k++)
            {
                var q = points[ring[k]];
                if (k == pPos || q == m || !InTriangle(m, hit, p, q) || !LocallyInside(ring, points, k, m))
                    continue;

                double tan = Math.Abs(m.Y - q.Y) / (q.X - m.X);
                if (tan < bestTan || (tan == bestTan && q.X > points[ring[best]].X))
                {
                    bestTan = tan;
                    best = k;
                }
            }

            pPos = best;
        }

        var result = new List<int>(n + hole.Count + 2);
        for (int k = 0; k <= pPos; k++)
            result.Add(ring[k]);
        for (int k = 0; k <= hole.Count; k++)
            result.Add(hole[(mPos + k) % hole.Count]);
        result.Add(ring[pPos]);
        for (int k = pPos + 1; k < n; k++)
            result.Add(ring[k]);
        return result;
    }

    /// <summary>Лежит ли направление из вершины k на точку b внутри угла многоугольника при этой вершине.</summary>
    private static bool LocallyInside(List<int> ring, List<(double X, double Y)> points, int k, (double X, double Y) b)
    {
        int n = ring.Count;
        var a = points[ring[k]];
        var prev = points[ring[(k - 1 + n) % n]];
        var next = points[ring[(k + 1) % n]];

        if (Orient(prev, a, next) >= 0)
            return Orient(a, next, b) > 0 && Orient(a, prev, b) < 0;
        return Orient(a, next, b) > 0 || Orient(a, prev, b) < 0;
    }

    private static IReadOnlyList<Triangle> Clip(List<int> ring, List<(double X, double Y)> points)
    {
        int n = ring.Count;
        var prev = new int[n];
        var next = new int[n];
        for (int i = 0; i < n; i++)
        {
            prev[i] = (i - 1 + n) % n;
            next[i] = (i + 1) % n;
        }

        var triangles = new List<Triangle>(n);
        int count = n, cur = 0, stall = 0;

        // Если строгих ушей не нашлось (почти вырожденный или не простой вход), берется любая выпуклая вершина
        bool relaxed = false;

        while (count > 3)
        {
            int p = prev[cur], q = next[cur];
            int turn = Orient(At(p), At(cur), At(q));

            if (turn == 0 || (turn > 0 && (relaxed || IsEar(p, cur, q))))
            {
                // Вершина на прямой между соседями убирается без треугольника: площадь не меняется
                if (turn > 0)
                    triangles.Add(Make(At(p), At(cur), At(q)));

                next[p] = q;
                prev[q] = p;
                count--;
                cur = turn == 0 ? p : q;
                stall = 0;
                relaxed = false;
                continue;
            }

            cur = q;
            if (++stall > count)
            {
                if (relaxed)
                    break;
                relaxed = true;
                stall = 0;
            }
        }

        if (count == 3 && Orient(At(prev[cur]), At(cur), At(next[cur])) > 0)
            triangles.Add(Make(At(prev[cur]), At(cur), At(next[cur])));

        return triangles;

        (double X, double Y) At(int node) => points[ring[node]];

        bool IsEar(int p, int cur, int q)
        {
            var a = At(p);
            var b = At(cur);
            var c = At(q);
            for (int k = next[q]; k != p; k = next[k])
            {
                var v = At(k);
                if (v == a || v == b || v == c)
                    continue;

                // Внутрь уха могут попасть только вогнутые (или лежащие на прямой) вершины, выпуклые не мешают
                if (Orient(a, b, v) >= 0 && Orient(b, c, v) >= 0 && Orient(c, a, v) >= 0 &&
                    Orient(At(prev[k]), v, At(next[k])) <= 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static bool InTriangle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) p)
    {
        int o1 = Orient(a, b, p), o2 = Orient(b, c, p), o3 = Orient(c, a, p);
        return (o1 >= 0 && o2 >= 0 && o3 >= 0) || (o1 <= 0 && o2 <= 0 && o3 <= 0);
    }

    private static Triangle Make((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        return new Triangle(new Vector(a.X, a.Y), new Vector(b.X, b.Y), new Vector(c.X, c.Y));
    }

    private static int Orient((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        return RobustPredicates.Orient2D(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }
}
