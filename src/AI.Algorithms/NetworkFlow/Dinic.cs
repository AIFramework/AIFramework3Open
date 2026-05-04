using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм Диница для нахождения максимального потока
/// (построение слоистой сети + блокирующий поток)
/// </summary>
[Serializable]
public class Dinic
{
    private readonly FlowNetwork _network;
    private readonly int _s;
    private readonly int _t;
    private int[] _level;
    private int[] _iter;

    /// <summary>
    /// Значение максимального потока
    /// </summary>
    public double MaxFlow { get; private set; }

    /// <summary>
    /// Вычисляет максимальный поток из s в t алгоритмом Диница
    /// </summary>
    /// <param name="network">Сеть потоков</param>
    /// <param name="s">Исток</param>
    /// <param name="t">Сток</param>
    public Dinic(FlowNetwork network, int s, int t)
    {
        _network = network;
        _s = s;
        _t = t;
        _level = new int[network.V];
        _iter = new int[network.V];
        MaxFlow = 0.0;

        while (BuildLevelGraph())
        {
            Array.Clear(_iter, 0, _iter.Length);
            double pushed;
            while ((pushed = SendFlow(_s, double.MaxValue)) > 0)
                MaxFlow += pushed;
        }
    }

    private bool BuildLevelGraph()
    {
        for (int i = 0; i < _level.Length; i++)
            _level[i] = -1;

        _level[_s] = 0;
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(_s);

        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            foreach (FlowEdge e in _network.Adj(v))
            {
                int w = e.Other(v);
                if (_level[w] < 0 && e.ResidualCapacityTo(w) > 0)
                {
                    _level[w] = _level[v] + 1;
                    queue.Enqueue(w);
                }
            }
        }

        return _level[_t] >= 0;
    }

    private double SendFlow(int v, double pushed)
    {
        if (v == _t) return pushed;

        List<FlowEdge> adj = _network.Adj(v);
        for (; _iter[v] < adj.Count; _iter[v]++)
        {
            FlowEdge e = adj[_iter[v]];
            int w = e.Other(v);
            if (_level[w] == _level[v] + 1 && e.ResidualCapacityTo(w) > 0)
            {
                double d = SendFlow(w, Math.Min(pushed, e.ResidualCapacityTo(w)));
                if (d > 0)
                {
                    e.AddFlowTo(w, d);
                    return d;
                }
            }
        }

        return 0;
    }
}
