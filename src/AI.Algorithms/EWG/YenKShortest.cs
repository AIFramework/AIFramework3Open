using System;
using System.Collections.Generic;
using AI.Algorithms.PriorityQueues;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм Йена для поиска K кратчайших путей между двумя вершинами
/// </summary>
[Serializable]
public class YenKShortestPaths<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Найденные кратчайшие пути и их стоимости
    /// </summary>
    public List<(List<int> Path, double Cost)> Paths { get; }

    /// <summary>
    /// Алгоритм Йена
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="source">Начальная вершина</param>
    /// <param name="target">Конечная вершина</param>
    /// <param name="K">Количество кратчайших путей</param>
    public YenKShortestPaths(GraphW<T> graph, int source, int target, int K)
    {
        Paths = new List<(List<int> Path, double Cost)>();
        int V = graph.V;

        double cost0;
        List<int> path0 = FindShortestPath(graph, source, target,
            new HashSet<int>(), new HashSet<long>(), out cost0);

        if (path0 == null) return;
        Paths.Add((path0, cost0));

        var candidates = new List<(List<int> Path, double Cost)>();

        for (int k = 1; k < K; k++)
        {
            List<int> prevPath = Paths[k - 1].Path;

            for (int i = 0; i < prevPath.Count - 1; i++)
            {
                int spurNode = prevPath[i];

                var excludedEdges = new HashSet<long>();

                foreach (var (p, c) in Paths)
                {
                    if (p.Count <= i) continue;

                    bool match = true;
                    for (int j = 0; j <= i; j++)
                    {
                        if (p[j] != prevPath[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        excludedEdges.Add((long)p[i] * V + p[i + 1]);
                }

                var excludedVertices = new HashSet<int>();
                for (int j = 0; j < i; j++)
                    excludedVertices.Add(prevPath[j]);

                double spurCost;
                List<int> spurPath = FindShortestPath(graph, spurNode, target,
                    excludedVertices, excludedEdges, out spurCost);

                if (spurPath != null)
                {
                    var totalPath = new List<int>();
                    for (int j = 0; j < i; j++)
                        totalPath.Add(prevPath[j]);
                    totalPath.AddRange(spurPath);

                    double rootCost = 0;
                    for (int j = 0; j < i; j++)
                    {
                        foreach (T e in graph.AdjEW(prevPath[j]))
                        {
                            if (e.Other(prevPath[j]) == prevPath[j + 1])
                            {
                                rootCost += e.W;
                                break;
                            }
                        }
                    }

                    double totalCost = rootCost + spurCost;

                    bool isDuplicate = false;
                    foreach (var (cp, cc) in candidates)
                    {
                        if (PathsEqual(cp, totalPath))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (!isDuplicate)
                    {
                        foreach (var (ap, ac) in Paths)
                        {
                            if (PathsEqual(ap, totalPath))
                            {
                                isDuplicate = true;
                                break;
                            }
                        }
                    }

                    if (!isDuplicate)
                        candidates.Add((totalPath, totalCost));
                }
            }

            if (candidates.Count == 0)
                break;

            int bestIdx = 0;
            for (int j = 1; j < candidates.Count; j++)
                if (candidates[j].Cost < candidates[bestIdx].Cost)
                    bestIdx = j;

            Paths.Add(candidates[bestIdx]);
            candidates.RemoveAt(bestIdx);
        }
    }

    private static List<int> FindShortestPath(GraphW<T> graph, int source, int target,
        HashSet<int> excludedVertices, HashSet<long> excludedEdges, out double cost)
    {
        int V = graph.V;
        double[] dist = new double[V];
        int[] prev = new int[V];

        for (int i = 0; i < V; i++)
        {
            dist[i] = double.MaxValue;
            prev[i] = -1;
        }

        dist[source] = 0;

        var pq = new IndexPriorityQueueMin<double>(V);
        pq.Insert(source, 0);
        bool[] settled = new bool[V];

        while (!pq.IsEmpty())
        {
            int v = pq.DelMinGetIndex();

            if (v == target)
                break;

            settled[v] = true;

            foreach (T e in graph.AdjEW(v))
            {
                int w = e.EndV;
                if (excludedVertices.Contains(w)) continue;

                long edgeKey = (long)e.StartV * V + e.EndV;
                if (excludedEdges.Contains(edgeKey)) continue;

                double newDist = dist[v] + e.W;
                if (newDist < dist[w])
                {
                    dist[w] = newDist;
                    prev[w] = v;
                    if (pq.IsContain(w))
                        pq.Update(w, newDist);
                    else if (!settled[w])
                        pq.Insert(w, newDist);
                }
            }
        }

        if (dist[target] >= double.MaxValue)
        {
            cost = double.MaxValue;
            return null;
        }

        cost = dist[target];

        List<int> path = new List<int>();
        for (int v = target; v != -1; v = prev[v])
            path.Insert(0, v);

        return path;
    }

    private static bool PathsEqual(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
