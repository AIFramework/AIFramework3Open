#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Triangulation;

/// <summary>
/// Триангуляция Делоне множества точек на плоскости (Боуэр–Уотсон с точными предикатами).
/// </summary>
/// <remarks>
/// Точки вставляются по одной в порядке возрастания (x, y). Треугольники, в описанный круг которых попала новая
/// точка, образуют полость, и она заново соединяется с этой точкой. Вместо большого внешнего треугольника к каждой
/// стороне выпуклой оболочки прикладывается «призрачный» треугольник с бесконечно удаленной вершиной, поэтому
/// оболочка получается точной, а не зависит от размера вспомогательного треугольника. Проверки «в круге» и
/// ориентации выполняются <see cref="RobustPredicates"/>, так что четыре точки на одной окружности и точки на
/// одной прямой обрабатываются без ошибок; при нескольких точках на одной окружности выбирается одна из допустимых
/// триангуляций. Точка ищется обходом по видимости от последнего треугольника, ожидаемая сложность близка к O(n log n)
/// для равномерно разбросанных точек, O(n²) в худшем случае.
/// </remarks>
public static class Delaunay
{
    /// <summary>
    /// Треугольники Делоне: тройки индексов входных точек с обходом против часовой стрелки. Ни одна входная точка
    /// не лежит строго внутри описанного круга треугольника. Повторы точек пропускаются (в треугольники входит
    /// первый из совпадающих индексов); если все точки лежат на одной прямой, треугольников нет.
    /// </summary>
    /// <param name="points">Точки (2D).</param>
    /// <returns>Тройки индексов в массиве <paramref name="points"/>.</returns>
    public static IReadOnlyList<(int A, int B, int C)> Triangulate(Vector[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        int n = points.Length;
        var x = new double[n];
        var y = new double[n];
        for (int i = 0; i < n; i++)
        {
            x[i] = points[i][0];
            y[i] = points[i][1];
        }

        var order = new int[n];
        for (int i = 0; i < n; i++)
            order[i] = i;
        Array.Sort(order, (i, j) => x[i] != x[j] ? x[i].CompareTo(x[j]) : y[i] != y[j] ? y[i].CompareTo(y[j]) : i.CompareTo(j));

        var unique = new List<int>(n);
        foreach (int i in order)
        {
            if (unique.Count == 0 || x[unique[^1]] != x[i] || y[unique[^1]] != y[i])
                unique.Add(i);
        }

        var result = new List<(int A, int B, int C)>();
        if (unique.Count < 3)
            return result;

        int third = 2;
        while (third < unique.Count && Orient(unique[0], unique[1], unique[third]) == 0)
            third++;
        if (third == unique.Count)
            return result;

        // Бесконечная вершина имеет индекс n и в треугольнике всегда стоит третьей: у призрака (a, b, ∞) снаружи
        // оболочки лежит левая сторона от a к b
        int infinite = n;
        var ta = new List<int>();
        var tb = new List<int>();
        var tc = new List<int>();
        var alive = new List<bool>();
        var edges = new Dictionary<long, int>();
        int last;

        int p0 = unique[0], p1 = unique[1], p2 = unique[third];
        if (Orient(p0, p1, p2) < 0)
            (p1, p2) = (p2, p1);
        last = Add(p0, p1, p2);
        Add(p1, p0, infinite);
        Add(p2, p1, infinite);
        Add(p0, p2, infinite);

        for (int k = 2; k < unique.Count; k++)
        {
            if (k != third)
                Insert(unique[k]);
        }

        for (int t = 0; t < ta.Count; t++)
        {
            if (alive[t] && tc[t] != infinite)
                result.Add((ta[t], tb[t], tc[t]));
        }

        return result;

        int Orient(int a, int b, int c) => RobustPredicates.Orient2D(x[a], y[a], x[b], y[b], x[c], y[c]);

        long Key(int u, int v) => (long)u * (n + 1) + v;

        int Add(int a, int b, int c)
        {
            if (a == infinite)
                (a, b, c) = (b, c, a);
            else if (b == infinite)
                (a, b, c) = (c, a, b);

            int t = ta.Count;
            ta.Add(a);
            tb.Add(b);
            tc.Add(c);
            alive.Add(true);
            edges[Key(a, b)] = t;
            edges[Key(b, c)] = t;
            edges[Key(c, a)] = t;
            return t;
        }

        // Точка в конфликте с треугольником, если лежит строго внутри его описанного круга; для призрака это
        // строго внешняя сторона ребра оболочки или внутренность самого ребра
        bool Conflict(int t, int p)
        {
            int a = ta[t], b = tb[t], c = tc[t];
            if (c != infinite)
                return RobustPredicates.InCircle(x[a], y[a], x[b], y[b], x[c], y[c], x[p], y[p]) > 0;

            int side = Orient(a, b, p);
            if (side != 0)
                return side > 0;
            return x[a] != x[b]
                ? Math.Min(x[a], x[b]) < x[p] && x[p] < Math.Max(x[a], x[b])
                : Math.Min(y[a], y[b]) < y[p] && y[p] < Math.Max(y[a], y[b]);
        }

        int Locate(int p)
        {
            int t = last;
            for (int step = 0; step <= ta.Count; step++)
            {
                if (tc[t] == infinite)
                {
                    if (Conflict(t, p))
                        return t;
                    break;
                }

                int a = ta[t], b = tb[t], c = tc[t];
                if (Orient(a, b, p) < 0)
                    t = edges[Key(b, a)];
                else if (Orient(b, c, p) < 0)
                    t = edges[Key(c, b)];
                else if (Orient(c, a, p) < 0)
                    t = edges[Key(a, c)];
                else
                    return t;
            }

            // Запасной путь: обход не должен зацикливаться на триангуляции Делоне, но перебор надежен всегда
            for (int s = 0; s < ta.Count; s++)
            {
                if (alive[s] && Conflict(s, p))
                    return s;
            }

            throw new InvalidOperationException("Не найден треугольник, содержащий точку");
        }

        void Insert(int p)
        {
            int start = Locate(p);
            var cavity = new List<int> { start };
            var inCavity = new HashSet<int> { start };
            var outside = new HashSet<int>();
            var boundary = new List<(int U, int V)>();

            for (int q = 0; q < cavity.Count; q++)
            {
                int t = cavity[q];
                foreach (var (u, v) in new[] { (ta[t], tb[t]), (tb[t], tc[t]), (tc[t], ta[t]) })
                {
                    int neighbor = edges[Key(v, u)];
                    if (inCavity.Contains(neighbor))
                        continue;

                    if (!outside.Contains(neighbor) && Conflict(neighbor, p))
                    {
                        inCavity.Add(neighbor);
                        cavity.Add(neighbor);
                    }
                    else
                    {
                        outside.Add(neighbor);
                        boundary.Add((u, v));
                    }
                }
            }

            foreach (int t in cavity)
            {
                alive[t] = false;
                edges.Remove(Key(ta[t], tb[t]));
                edges.Remove(Key(tb[t], tc[t]));
                edges.Remove(Key(tc[t], ta[t]));
            }

            foreach (var (u, v) in boundary)
            {
                int t = Add(u, v, p);
                if (tc[t] != infinite)
                    last = t;
            }
        }
    }
}
