using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.MST;

/// <summary>
/// Алгоритм Борувки для нахождения минимального остовного дерева (MST).
/// На каждом шаге для каждой компоненты находится минимальное внешнее ребро,
/// после чего компоненты объединяются.
/// </summary>
[Serializable]
public class Boruvka<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Рёбра минимального остовного дерева
    /// </summary>
    public List<T> MSTEdges { get; }

    /// <summary>
    /// Суммарный вес минимального остовного дерева
    /// </summary>
    public double TotalWeight { get; private set; }

    /// <summary>
    /// Строит минимальное остовное дерево алгоритмом Борувки
    /// </summary>
    /// <param name="graph">Взвешенный неориентированный граф</param>
    public Boruvka(GraphW<T> graph)
    {
        MSTEdges = new List<T>();
        TotalWeight = 0;

        int v = graph.V;
        int[] parent = new int[v];
        int[] rank = new int[v];
        for (int i = 0; i < v; i++)
            parent[i] = i;

        int numComponents = v;

        List<T> allEdges = CollectEdges(graph);

        while (numComponents > 1)
        {
            T[] cheapest = new T[v];

            foreach (T edge in allEdges)
            {
                int u = Find(parent, edge.Either());
                int w = Find(parent, edge.Other(edge.Either()));

                if (u == w) continue;

                if (cheapest[u] == null || edge.W < cheapest[u].W)
                    cheapest[u] = edge;
                if (cheapest[w] == null || edge.W < cheapest[w].W)
                    cheapest[w] = edge;
            }

            bool merged = false;
            for (int i = 0; i < v; i++)
            {
                if (cheapest[i] == null) continue;

                T e = cheapest[i];
                int cu = Find(parent, e.Either());
                int cw = Find(parent, e.Other(e.Either()));

                if (cu == cw) continue;

                Union(parent, rank, cu, cw);
                MSTEdges.Add(e);
                TotalWeight += e.W;
                numComponents--;
                merged = true;
            }

            if (!merged) break;
        }
    }

    private static List<T> CollectEdges(GraphW<T> graph)
    {
        HashSet<(int, int)> seen = new HashSet<(int, int)>();
        List<T> edges = new List<T>();

        for (int i = 0; i < graph.V; i++)
        {
            foreach (T e in graph.AdjEW(i))
            {
                int u = e.Either();
                int w = e.Other(u);
                int lo = Math.Min(u, w);
                int hi = Math.Max(u, w);

                if (seen.Add((lo, hi)))
                    edges.Add(e);
            }
        }

        return edges;
    }

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    private static void Union(int[] parent, int[] rank, int x, int y)
    {
        if (rank[x] < rank[y])
            parent[x] = y;
        else if (rank[x] > rank[y])
            parent[y] = x;
        else
        {
            parent[y] = x;
            rank[x]++;
        }
    }
}
