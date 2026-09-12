using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AI.Algorithms.VRP;

/// <summary>
/// Алгоритм Кристофидеса (3/2-приближение) для задачи коммивояжёра (TSP).
/// Использует МОД + минимальное паросочетание + Эйлеров обход + срезание повторов.
/// </summary>
/// <remarks>
/// <para>
/// Гарантия «не длиннее полутора оптимумов» держится на том, что паросочетание нечётных вершин
/// остовного дерева минимально. До 20 нечётных вершин оно ищется точно — динамикой по
/// подмножествам; при большем числе — жадно, и тогда гарантии нет: жадное паросочетание бывает
/// заметно тяжелее минимального. Какой случай был, показывает <see cref="GuaranteeHolds"/>.
/// </para>
/// <para>
/// Расстояния должны удовлетворять неравенству треугольника — у евклидовых точек это так.
/// </para>
/// </remarks>
[Serializable]
public class Christofides
{
    /// <summary>
    /// Наибольшее число нечётных вершин, для которого паросочетание ищется точно
    /// </summary>
    public const int ExactMatchingLimit = 20;

    private readonly VRPInstance _inst;

    /// <summary>
    /// Создаёт экземпляр алгоритма Кристофидеса
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    public Christofides(VRPInstance inst)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
    }

    /// <summary>
    /// Выполняется ли гарантия 3/2 для последнего построенного тура: паросочетание было точным
    /// </summary>
    public bool GuaranteeHolds { get; private set; }

    /// <summary>
    /// Решает задачу TSP, возвращая тур в виде списка узлов (включая депо)
    /// </summary>
    /// <returns>
    /// Узлы тура: 0 — депо, клиент c — узел c + 1; тур начинается и кончается депо. Для решения
    /// VRP с индексами клиентов используйте <see cref="SolveAsSolution"/>
    /// </returns>
    public List<int> SolveTSP()
    {
        int n = _inst.TotalNodes;
        var mstAdj = BuildMST(n);
        var oddVertices = FindOddDegreeVertices(mstAdj, n);

        GuaranteeHolds = oddVertices.Count <= ExactMatchingLimit;
        var matchEdges = GuaranteeHolds ? ExactMinMatching(oddVertices) : GreedyMinMatching(oddVertices);

        var multigraph = BuildMultigraph(mstAdj, matchEdges, n);
        var euler = FindEulerCircuit(multigraph, n);
        var tour = Shortcut(euler, n);
        return tour;
    }

    /// <summary>
    /// Тур как решение VRP: один маршрут по всем клиентам в индексах клиентов, без депо
    /// </summary>
    public VRPSolution SolveAsSolution()
    {
        List<int> tour = SolveTSP();
        var solution = new VRPSolution();

        if (tour.Count <= 2)
            return solution;

        // Тур замкнут: последний узел повторяет первый. Поворачиваем так, чтобы он начинался с депо
        var cycle = tour.Take(tour.Count - 1).ToList();
        int depot = cycle.IndexOf(0);
        var route = cycle.Skip(depot + 1).Concat(cycle.Take(depot)).Select(node => node - 1).ToList();

        solution.Routes.Add(route);

        return solution;
    }

    private List<(int, double)>[] BuildMST(int n)
    {
        var adj = new List<(int to, double w)>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<(int, double)>();

        bool[] inMST = new bool[n];
        double[] key = new double[n];
        int[] parent = new int[n];

        for (int i = 0; i < n; i++) { key[i] = double.MaxValue; parent[i] = -1; }
        key[0] = 0;

        for (int iter = 0; iter < n; iter++)
        {
            int u = -1;
            double minKey = double.MaxValue;
            for (int v = 0; v < n; v++)
            {
                if (!inMST[v] && key[v] < minKey)
                {
                    minKey = key[v];
                    u = v;
                }
            }
            if (u < 0) break;
            inMST[u] = true;

            if (parent[u] >= 0)
            {
                double w = _inst.Distance(u, parent[u]);
                adj[u].Add((parent[u], w));
                adj[parent[u]].Add((u, w));
            }

            for (int v = 0; v < n; v++)
            {
                if (!inMST[v])
                {
                    double d = _inst.Distance(u, v);
                    if (d < key[v])
                    {
                        key[v] = d;
                        parent[v] = u;
                    }
                }
            }
        }
        return adj;
    }

    private List<int> FindOddDegreeVertices(List<(int, double)>[] adj, int n)
    {
        var odd = new List<int>();
        for (int i = 0; i < n; i++)
            if (adj[i].Count % 2 == 1)
                odd.Add(i);
        return odd;
    }

    // Минимальное совершенное паросочетание динамикой по подмножествам:
    // best[mask] — наименьший вес паросочетания вершин из mask; младшая вершина маски
    // сопоставляется с каждой из остальных по очереди
    private List<(int, int)> ExactMinMatching(List<int> odd)
    {
        int k = odd.Count;
        var result = new List<(int, int)>();

        if (k == 0)
            return result;

        int full = (1 << k) - 1;
        double[] best = new double[1 << k];
        int[] partner = new int[1 << k];

        for (int mask = 1; mask <= full; mask++)
        {
            best[mask] = double.PositiveInfinity;

            if ((BitOperations.PopCount((uint)mask) & 1) == 1)
                continue;

            int i = BitOperations.TrailingZeroCount(mask);
            int rest = mask & ~(1 << i);

            for (int j = i + 1; j < k; j++)
            {
                if (((rest >> j) & 1) == 0)
                    continue;

                double cost = best[rest & ~(1 << j)] + _inst.Distance(odd[i], odd[j]);

                if (cost < best[mask])
                {
                    best[mask] = cost;
                    partner[mask] = j;
                }
            }
        }

        for (int mask = full; mask != 0;)
        {
            int i = BitOperations.TrailingZeroCount(mask);
            int j = partner[mask];
            result.Add((odd[i], odd[j]));
            mask &= ~(1 << i) & ~(1 << j);
        }

        return result;
    }

    private List<(int, int)> GreedyMinMatching(List<int> oddVerts)
    {
        var result = new List<(int, int)>();
        bool[] matched = new bool[oddVerts.Count];

        var edges = new List<(double w, int i, int j)>();
        for (int i = 0; i < oddVerts.Count; i++)
            for (int j = i + 1; j < oddVerts.Count; j++)
                edges.Add((_inst.Distance(oddVerts[i], oddVerts[j]), i, j));
        edges.Sort((a, b) => a.w.CompareTo(b.w));

        foreach (var (_, i, j) in edges)
        {
            if (!matched[i] && !matched[j])
            {
                matched[i] = true;
                matched[j] = true;
                result.Add((oddVerts[i], oddVerts[j]));
            }
        }
        return result;
    }

    private List<(int to, int edgeId)>[] BuildMultigraph(
        List<(int to, double w)>[] mstAdj, List<(int, int)> matchEdges, int n)
    {
        var adj = new List<(int to, int edgeId)>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<(int, int)>();

        int edgeCount = 0;
        for (int u = 0; u < n; u++)
        {
            foreach (var (v, _) in mstAdj[u])
            {
                if (u < v)
                {
                    adj[u].Add((v, edgeCount));
                    adj[v].Add((u, edgeCount));
                    edgeCount++;
                }
            }
        }

        foreach (var (u, v) in matchEdges)
        {
            adj[u].Add((v, edgeCount));
            adj[v].Add((u, edgeCount));
            edgeCount++;
        }

        return adj;
    }

    private List<int> FindEulerCircuit(List<(int to, int edgeId)>[] adj, int n)
    {
        var usedEdge = new HashSet<int>();
        var idx = new int[n];
        var stack = new Stack<int>();
        var circuit = new List<int>();

        stack.Push(0);
        while (stack.Count > 0)
        {
            int v = stack.Peek();
            bool found = false;
            while (idx[v] < adj[v].Count)
            {
                var (to, eid) = adj[v][idx[v]];
                idx[v]++;
                if (usedEdge.Add(eid))
                {
                    stack.Push(to);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                stack.Pop();
                circuit.Add(v);
            }
        }
        return circuit;
    }

    private List<int> Shortcut(List<int> euler, int n)
    {
        var visited = new HashSet<int>();
        var tour = new List<int>();

        foreach (int v in euler)
        {
            if (visited.Add(v))
                tour.Add(v);
        }

        if (tour.Count > 0 && tour[0] != tour[tour.Count - 1])
            tour.Add(tour[0]);

        return tour;
    }
}
