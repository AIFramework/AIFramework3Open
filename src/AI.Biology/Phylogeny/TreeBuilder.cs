namespace AI.Biology.Phylogeny;

/// <summary>
/// Построение деревьев по матрице расстояний: UPGMA и присоединение соседей.
/// </summary>
/// <remarks>
/// <para>
/// UPGMA объединяет два ближайших кластера и считает расстояние до нового кластера средним.
/// Это верно, только если скорость эволюции постоянна по всем ветвям (молекулярные часы) и
/// матрица ультраметрична; иначе UPGMA соединяет не родственные, а просто медленно
/// эволюционирующие линии. Дерево укоренённое, листья на одной высоте.
/// </para>
/// <para>
/// Присоединение соседей (Сайтоу и Ней, 1987) выбирает пару, минимизирующую
/// <c>(r − 2)·d(i,j) − Σd(i) − Σd(j)</c>: поправка на суммарные расстояния отличает настоящих
/// соседей от линий, которые просто эволюционируют медленно. На аддитивной матрице метод
/// восстанавливает дерево и длины ветвей точно. Дерево некорневое, корень записи — узел степени три.
/// Длина ветви может выйти отрицательной, если матрица далека от аддитивной; она сохраняется
/// как есть, а не обнуляется молча.
/// </para>
/// <para>Оба метода — O(n³) по числу таксонов.</para>
/// </remarks>
public static class TreeBuilder
{
    /// <summary>Дерево по методу UPGMA</summary>
    /// <param name="distances">Матрица расстояний</param>
    public static PhylogeneticTree Upgma(DistanceMatrix distances)
    {
        RequireFinite(distances);

        int n = distances.Count;
        double[,] d = distances.ToArray();
        var nodes = new TreeNode[n];
        var sizes = new int[n];
        var heights = new double[n];
        var active = new bool[n];

        for (int i = 0; i < n; i++)
        {
            nodes[i] = new TreeNode(distances.Labels[i]);
            sizes[i] = 1;
            active[i] = true;
        }

        for (int step = 0; step + 1 < n; step++)
        {
            (int a, int b) = Closest(d, active);
            double height = d[a, b] / 2;

            var parent = new TreeNode();
            parent.AddChild(nodes[a], height - heights[a]);
            parent.AddChild(nodes[b], height - heights[b]);

            for (int k = 0; k < n; k++)
            {
                if (!active[k] || k == a || k == b)
                    continue;

                double merged = ((sizes[a] * d[a, k]) + (sizes[b] * d[b, k])) / (sizes[a] + sizes[b]);
                d[a, k] = merged;
                d[k, a] = merged;
            }

            nodes[a] = parent;
            sizes[a] += sizes[b];
            heights[a] = height;
            active[b] = false;
        }

        return new PhylogeneticTree(nodes[Array.IndexOf(active, true)]);
    }

    /// <summary>Некорневое дерево по методу присоединения соседей</summary>
    /// <param name="distances">Матрица расстояний</param>
    public static PhylogeneticTree NeighborJoining(DistanceMatrix distances)
    {
        RequireFinite(distances);

        int n = distances.Count;
        double[,] d = distances.ToArray();
        var nodes = distances.Labels.Select(label => new TreeNode(label)).ToArray();

        if (n == 1)
            return new PhylogeneticTree(nodes[0]);

        if (n == 2)
        {
            var pair = new TreeNode();
            pair.AddChild(nodes[0], d[0, 1] / 2);
            pair.AddChild(nodes[1], d[0, 1] / 2);

            return new PhylogeneticTree(pair);
        }

        var ids = Enumerable.Range(0, n).ToList();
        var sums = new double[n];

        while (ids.Count > 3)
        {
            int r = ids.Count;

            foreach (int i in ids)
                sums[i] = ids.Sum(k => d[i, k]);

            int a = -1, b = -1;
            double best = double.PositiveInfinity;

            for (int x = 0; x < r; x++)
            {
                for (int y = x + 1; y < r; y++)
                {
                    int i = ids[x], j = ids[y];
                    double q = ((r - 2) * d[i, j]) - sums[i] - sums[j];

                    // При равных Q берётся первая пара: сравнение с допуском не даёт порядку
                    // суммирования решать за данные
                    if (a < 0 || q < best - (1e-12 * Math.Max(1, Math.Abs(best))))
                    {
                        best = q;
                        a = i;
                        b = j;
                    }
                }
            }

            double toA = (d[a, b] / 2) + ((sums[a] - sums[b]) / (2.0 * (r - 2)));
            double toB = d[a, b] - toA;

            var parent = new TreeNode();
            parent.AddChild(nodes[a], toA);
            parent.AddChild(nodes[b], toB);

            foreach (int k in ids)
            {
                if (k == a || k == b)
                    continue;

                double updated = (d[a, k] + d[b, k] - d[a, b]) / 2;
                d[a, k] = updated;
                d[k, a] = updated;
            }

            nodes[a] = parent;
            _ = ids.Remove(b);
        }

        int p = ids[0], s = ids[1], t = ids[2];
        var centre = new TreeNode();

        centre.AddChild(nodes[p], (d[p, s] + d[p, t] - d[s, t]) / 2);
        centre.AddChild(nodes[s], (d[p, s] + d[s, t] - d[p, t]) / 2);
        centre.AddChild(nodes[t], (d[p, t] + d[s, t] - d[p, s]) / 2);

        return new PhylogeneticTree(centre);
    }

    private static (int A, int B) Closest(double[,] d, bool[] active)
    {
        int n = active.Length;
        int a = -1, b = -1;
        double best = double.PositiveInfinity;

        for (int i = 0; i < n; i++)
        {
            if (!active[i])
                continue;

            for (int j = i + 1; j < n; j++)
            {
                if (active[j] && d[i, j] < best)
                {
                    best = d[i, j];
                    a = i;
                    b = j;
                }
            }
        }

        return (a, b);
    }

    private static void RequireFinite(DistanceMatrix distances)
    {
        ArgumentNullException.ThrowIfNull(distances);

        if (!distances.IsFinite)
            throw new ArgumentException(
                "В матрице есть бесконечные расстояния: последовательности насыщены заменами, и дерево по ним не строится",
                nameof(distances));
    }
}
