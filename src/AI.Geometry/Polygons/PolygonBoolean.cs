#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Булевы операции над простыми многоугольниками: пересечение, объединение, разность.
/// </summary>
/// <remarks>
/// Метод наложения. Стороны обоих многоугольников разрезаются во всех общих точках, включая концы коллинеарных
/// перекрытий и касания вершиной, так что две стороны встречаются только в узлах. Каждый кусок стороны одного
/// многоугольника по середине относится к внутренности или внешности другого; куски, общие для обоих, различаются
/// по направлению (одинаковое или встречное, как в методе Мартинеса–Руэды). Отобранные направленные куски
/// сшиваются в контуры обходом граней: в узле берется самый левый поворот, поэтому касающиеся в вершине части
/// выходят отдельными контурами.
/// <para>
/// Положение точек касания, концов перекрытий и общих вершин определяется точными предикатами
/// <see cref="RobustPredicates"/> и берется из входа без округления; округляются только точки собственного
/// пересечения сторон, и узлы ближе 1e-12 от масштаба координат сливаются. Сложность O(n·m + k²), где k есть число узлов.
/// </para>
/// <para>
/// Результат есть список контуров: внешние границы обходятся против часовой стрелки, дыры по часовой. Площадь
/// результата равна сумме знаковых площадей контуров (<see cref="ShoelaceArea.SignedArea"/>). Вершины на одной
/// прямой между соседями удаляются.
/// </para>
/// </remarks>
public static class PolygonBoolean
{
    /// <summary>
    /// Пересечение двух простых многоугольников (обход любой).
    /// </summary>
    /// <param name="a">Первый многоугольник (2D).</param>
    /// <param name="b">Второй многоугольник (2D).</param>
    /// <returns>Контуры результата: внешние против часовой стрелки, дыры по часовой.</returns>
    public static IReadOnlyList<Vector[]> Intersection(Vector[] a, Vector[] b)
    {
        return Overlay(a, b, aInsideB: true, bInsideA: true, reverseB: false, keepSame: true, keepOpposite: false);
    }

    /// <summary>
    /// Объединение двух простых многоугольников (обход любой).
    /// </summary>
    /// <param name="a">Первый многоугольник (2D).</param>
    /// <param name="b">Второй многоугольник (2D).</param>
    /// <returns>Контуры результата: внешние против часовой стрелки, дыры по часовой.</returns>
    public static IReadOnlyList<Vector[]> Union(Vector[] a, Vector[] b)
    {
        return Overlay(a, b, aInsideB: false, bInsideA: false, reverseB: false, keepSame: true, keepOpposite: false);
    }

    /// <summary>
    /// Разность a − b двух простых многоугольников (обход любой).
    /// </summary>
    /// <param name="a">Уменьшаемое (2D).</param>
    /// <param name="b">Вычитаемое (2D).</param>
    /// <returns>Контуры результата: внешние против часовой стрелки, дыры по часовой.</returns>
    public static IReadOnlyList<Vector[]> Difference(Vector[] a, Vector[] b)
    {
        return Overlay(a, b, aInsideB: false, bInsideA: true, reverseB: true, keepSame: false, keepOpposite: true);
    }

    private static IReadOnlyList<Vector[]> Overlay(
        Vector[] a, Vector[] b, bool aInsideB, bool bInsideA, bool reverseB, bool keepSame, bool keepOpposite)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var ringA = Ring(a);
        var ringB = Ring(b);
        double tolerance = 1e-12 * Math.Max(Scale(ringA), Scale(ringB));

        var splitsA = Splits(ringA);
        var splitsB = Splits(ringB);
        for (int i = 0; i < ringA.Count; i++)
        {
            for (int j = 0; j < ringB.Count; j++)
            {
                Cut(ringA[i], ringA[(i + 1) % ringA.Count], ringB[j], ringB[(j + 1) % ringB.Count], splitsA[i], splitsB[j]);
            }
        }

        var nodes = new List<(double X, double Y)>();
        var edgesA = Pieces(ringA, splitsA, nodes, tolerance);
        var edgesB = Pieces(ringB, splitsB, nodes, tolerance);
        var setA = new HashSet<(int, int)>(edgesA);
        var setB = new HashSet<(int, int)>(edgesB);
        var polygonA = ToVectors(ringA);
        var polygonB = ToVectors(ringB);

        var selected = new List<(int From, int To)>();
        foreach (var (u, v) in edgesA)
        {
            if (setB.Contains((u, v)))
            {
                if (keepSame)
                    selected.Add((u, v));
            }
            else if (setB.Contains((v, u)))
            {
                if (keepOpposite)
                    selected.Add((u, v));
            }
            else if (Inside(nodes, u, v, polygonB) == aInsideB)
            {
                selected.Add((u, v));
            }
        }

        foreach (var (u, v) in edgesB)
        {
            if (setA.Contains((u, v)) || setA.Contains((v, u)))
                continue;
            if (Inside(nodes, u, v, polygonA) == bInsideA)
                selected.Add(reverseB ? (v, u) : (u, v));
        }

        return Assemble(selected, nodes);
    }

    /// <summary>Вершины без подряд идущих повторов, против часовой стрелки; вырожденный контур пуст.</summary>
    private static List<(double X, double Y)> Ring(Vector[] polygon)
    {
        var ring = new List<(double X, double Y)>(polygon.Length);
        foreach (var vertex in polygon)
        {
            var point = (vertex[0], vertex[1]);
            if (ring.Count == 0 || ring[^1] != point)
                ring.Add(point);
        }

        while (ring.Count > 1 && ring[0] == ring[^1])
            ring.RemoveAt(ring.Count - 1);

        if (ring.Count < 3)
            return new List<(double X, double Y)>();
        if (ShoelaceArea.SignedArea(polygon) < 0)
            ring.Reverse();
        return ring;
    }

    private static double Scale(List<(double X, double Y)> ring)
    {
        double scale = 0;
        foreach (var (x, y) in ring)
            scale = Math.Max(scale, Math.Max(Math.Abs(x), Math.Abs(y)));
        return scale;
    }

    private static List<List<(double X, double Y)>> Splits(List<(double X, double Y)> ring)
    {
        var splits = new List<List<(double X, double Y)>>(ring.Count);
        for (int i = 0; i < ring.Count; i++)
            splits.Add(new List<(double X, double Y)> { ring[i], ring[(i + 1) % ring.Count] });
        return splits;
    }

    /// <summary>Добавляет общие точки двух сторон в списки разрезов каждой из них.</summary>
    private static void Cut(
        (double X, double Y) a0, (double X, double Y) a1, (double X, double Y) b0, (double X, double Y) b1,
        List<(double X, double Y)> splitsA, List<(double X, double Y)> splitsB)
    {
        int o1 = Orient(a0, a1, b0), o2 = Orient(a0, a1, b1);
        if (o1 == 0 && o2 == 0)
        {
            // На одной прямой: концы перекрытия суть концы одной стороны, лежащие внутри другой
            AddIfBetween(a0, a1, b0, splitsA);
            AddIfBetween(a0, a1, b1, splitsA);
            AddIfBetween(b0, b1, a0, splitsB);
            AddIfBetween(b0, b1, a1, splitsB);
            return;
        }

        if (o1 * o2 > 0)
            return;
        int o3 = Orient(b0, b1, a0), o4 = Orient(b0, b1, a1);
        if (o3 * o4 > 0)
            return;

        if (o1 == 0 || o2 == 0 || o3 == 0 || o4 == 0)
        {
            // Касание: общая точка есть вершина одной из сторон и берется точно
            var touch = o1 == 0 ? b0 : o2 == 0 ? b1 : o3 == 0 ? a0 : a1;
            AddIfBetween(a0, a1, touch, splitsA);
            AddIfBetween(b0, b1, touch, splitsB);
            return;
        }

        double dax = a1.X - a0.X, day = a1.Y - a0.Y;
        double dbx = b1.X - b0.X, dby = b1.Y - b0.Y;
        double t = ((b0.X - a0.X) * dby - (b0.Y - a0.Y) * dbx) / (dax * dby - day * dbx);
        t = Math.Clamp(t, 0.0, 1.0);
        var crossing = (a0.X + t * dax, a0.Y + t * day);
        splitsA.Add(crossing);
        splitsB.Add(crossing);
    }

    /// <summary>Добавляет точку, лежащую на прямой отрезка, если она строго между его концами.</summary>
    private static void AddIfBetween((double X, double Y) s0, (double X, double Y) s1, (double X, double Y) p, List<(double X, double Y)> splits)
    {
        bool between = s0.X != s1.X
            ? Math.Min(s0.X, s1.X) < p.X && p.X < Math.Max(s0.X, s1.X)
            : Math.Min(s0.Y, s1.Y) < p.Y && p.Y < Math.Max(s0.Y, s1.Y);
        if (between)
            splits.Add(p);
    }

    /// <summary>Куски сторон между соседними разрезами как пары индексов узлов.</summary>
    private static List<(int, int)> Pieces(
        List<(double X, double Y)> ring, List<List<(double X, double Y)>> splits, List<(double X, double Y)> nodes, double tolerance)
    {
        var pieces = new List<(int, int)>();
        for (int i = 0; i < ring.Count; i++)
        {
            var start = ring[i];
            var end = ring[(i + 1) % ring.Count];
            double dx = end.X - start.X, dy = end.Y - start.Y;
            var cuts = splits[i];
            cuts.Sort((p, q) => ((p.X - start.X) * dx + (p.Y - start.Y) * dy).CompareTo((q.X - start.X) * dx + (q.Y - start.Y) * dy));

            int previous = Node(cuts[0]);
            for (int k = 1; k < cuts.Count; k++)
            {
                int node = Node(cuts[k]);
                if (node != previous)
                    pieces.Add((previous, node));
                previous = node;
            }
        }

        return pieces;

        int Node((double X, double Y) point)
        {
            for (int k = 0; k < nodes.Count; k++)
            {
                if (Math.Abs(nodes[k].X - point.X) <= tolerance && Math.Abs(nodes[k].Y - point.Y) <= tolerance)
                    return k;
            }

            nodes.Add(point);
            return nodes.Count - 1;
        }
    }

    private static bool Inside(List<(double X, double Y)> nodes, int u, int v, Vector[] polygon)
    {
        if (polygon.Length < 3)
            return false;
        var middle = new Vector((nodes[u].X + nodes[v].X) / 2, (nodes[u].Y + nodes[v].Y) / 2);
        return PointInPolygon.Contains(middle, polygon);
    }

    /// <summary>Сшивает направленные куски в замкнутые контуры обходом граней.</summary>
    private static IReadOnlyList<Vector[]> Assemble(List<(int From, int To)> edges, List<(double X, double Y)> nodes)
    {
        var outgoing = new Dictionary<int, List<int>>();
        for (int e = 0; e < edges.Count; e++)
        {
            if (!outgoing.TryGetValue(edges[e].From, out var list))
                outgoing[edges[e].From] = list = new List<int>();
            list.Add(e);
        }

        var used = new bool[edges.Count];
        var result = new List<Vector[]>();
        for (int first = 0; first < edges.Count; first++)
        {
            if (used[first])
                continue;

            var ring = new List<(double X, double Y)>();
            int e = first;
            bool closed = false;
            while (true)
            {
                used[e] = true;
                var (from, to) = edges[e];
                ring.Add(nodes[from]);
                if (to == edges[first].From)
                {
                    closed = true;
                    break;
                }

                e = NextEdge(from, to);
                if (e < 0)
                    break;
            }

            if (closed)
            {
                var polygon = DropCollinear(ring);
                if (polygon.Length >= 3)
                    result.Add(polygon);
            }
        }

        return result;

        // Следующий кусок той же грани: первый по часовой стрелке от направления назад, то есть самый левый поворот
        int NextEdge(int from, int at)
        {
            if (!outgoing.TryGetValue(at, out var candidates))
                return -1;

            double wx = nodes[from].X - nodes[at].X, wy = nodes[from].Y - nodes[at].Y;
            int best = -1;
            double bestAngle = double.PositiveInfinity;
            foreach (int candidate in candidates)
            {
                if (used[candidate])
                    continue;

                double ox = nodes[edges[candidate].To].X - nodes[at].X, oy = nodes[edges[candidate].To].Y - nodes[at].Y;
                double clockwise = -Math.Atan2(wx * oy - wy * ox, wx * ox + wy * oy);
                if (clockwise <= 0)
                    clockwise += 2 * Math.PI;
                if (clockwise < bestAngle)
                {
                    bestAngle = clockwise;
                    best = candidate;
                }
            }

            return best;
        }
    }

    private static Vector[] DropCollinear(List<(double X, double Y)> ring)
    {
        bool changed = true;
        while (changed && ring.Count >= 3)
        {
            changed = false;
            for (int k = 0; k < ring.Count && ring.Count >= 3; k++)
            {
                var prev = ring[(k - 1 + ring.Count) % ring.Count];
                var next = ring[(k + 1) % ring.Count];
                if (Orient(prev, ring[k], next) == 0)
                {
                    ring.RemoveAt(k);
                    k--;
                    changed = true;
                }
            }
        }

        return ring.Count < 3 ? Array.Empty<Vector>() : ToVectors(ring);
    }

    private static Vector[] ToVectors(List<(double X, double Y)> ring)
    {
        var result = new Vector[ring.Count];
        for (int k = 0; k < ring.Count; k++)
            result[k] = new Vector(ring[k].X, ring[k].Y);
        return result;
    }

    private static int Orient((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        return RobustPredicates.Orient2D(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }
}
