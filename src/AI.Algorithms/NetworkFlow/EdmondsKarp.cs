using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм Эдмондса-Карпа — нахождение максимального потока
/// с использованием кратчайших увеличивающих путей (BFS)
/// </summary>
[Serializable]
public class EdmondsKarp
{
    private readonly bool[] _marked;
    private readonly FlowEdge[] _edgeTo;

    /// <summary>
    /// Значение максимального потока
    /// </summary>
    public double MaxFlow { get; private set; }

    /// <summary>
    /// Вычисляет максимальный поток из s в t методом Эдмондса-Карпа
    /// </summary>
    /// <param name="network">Сеть потоков</param>
    /// <param name="s">Исток</param>
    /// <param name="t">Сток</param>
    public EdmondsKarp(FlowNetwork network, int s, int t)
    {
        _marked = new bool[network.V];
        _edgeTo = new FlowEdge[network.V];
        MaxFlow = 0.0;

        while (Bfs(network, s, t))
        {
            double bottleneck = double.MaxValue;
            for (int v = t; v != s; v = _edgeTo[v].Other(v))
                bottleneck = Math.Min(bottleneck, _edgeTo[v].ResidualCapacityTo(v));

            for (int v = t; v != s; v = _edgeTo[v].Other(v))
                _edgeTo[v].AddFlowTo(v, bottleneck);

            MaxFlow += bottleneck;
        }
    }

    private bool Bfs(FlowNetwork network, int s, int t)
    {
        Array.Clear(_marked, 0, _marked.Length);
        Array.Clear(_edgeTo, 0, _edgeTo.Length);

        Queue<int> queue = new Queue<int>();
        _marked[s] = true;
        queue.Enqueue(s);

        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            foreach (FlowEdge e in network.Adj(v))
            {
                int w = e.Other(v);
                if (!_marked[w] && e.ResidualCapacityTo(w) > 0)
                {
                    _edgeTo[w] = e;
                    _marked[w] = true;
                    if (w == t) return true;
                    queue.Enqueue(w);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, принадлежит ли вершина v стороне истока в минимальном разрезе
    /// </summary>
    /// <param name="v">Вершина</param>
    public bool InCut(int v)
    {
        return _marked[v];
    }
}
