using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;
using AI.Algorithms.PriorityQueues;

namespace AI.Algorithms.MST;

/// <summary>
/// Алгоритм Прима для нахождения минимального остовного дерева (MST).
/// Использует индексированную очередь с приоритетом (IndexPriorityQueueMin).
/// </summary>
[Serializable]
public class Prim<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Ребро, ведущее к каждой вершине в MST
    /// </summary>
    public T[] EdgeTo { get; }

    /// <summary>
    /// Минимальный вес ребра для включения вершины в MST
    /// </summary>
    public double[] KeyTo { get; }

    /// <summary>
    /// Суммарный вес минимального остовного дерева
    /// </summary>
    public double TotalWeight { get; private set; }

    private readonly bool[] _inMST;

    /// <summary>
    /// Строит минимальное остовное дерево алгоритмом Прима
    /// </summary>
    /// <param name="graph">Взвешенный неориентированный граф</param>
    public Prim(GraphW<T> graph)
    {
        int v = graph.V;
        EdgeTo = new T[v];
        KeyTo = new double[v];
        _inMST = new bool[v];

        for (int i = 0; i < v; i++)
            KeyTo[i] = double.MaxValue;

        IndexPriorityQueueMin<double> pq = new IndexPriorityQueueMin<double>(v);

        KeyTo[0] = 0.0;
        pq.Insert(0, 0.0);

        while (!pq.IsEmpty())
        {
            int u = pq.DelMinGetIndex();
            _inMST[u] = true;

            foreach (T edge in graph.AdjEW(u))
            {
                int w = edge.Other(u);
                if (_inMST[w]) continue;

                if (edge.W < KeyTo[w])
                {
                    KeyTo[w] = edge.W;
                    EdgeTo[w] = edge;

                    if (pq.IsContain(w))
                        pq.Update(w, edge.W);
                    else
                        pq.Insert(w, edge.W);
                }
            }
        }

        TotalWeight = 0;
        for (int i = 1; i < v; i++)
        {
            if (EdgeTo[i] != null)
                TotalWeight += EdgeTo[i].W;
        }
    }

    /// <summary>
    /// Возвращает рёбра минимального остовного дерева
    /// </summary>
    /// <returns>Перечисление рёбер MST</returns>
    public IEnumerable<T> MSTEdges()
    {
        List<T> edges = new List<T>();
        for (int i = 1; i < EdgeTo.Length; i++)
        {
            if (EdgeTo[i] != null)
                edges.Add(EdgeTo[i]);
        }
        return edges;
    }
}
