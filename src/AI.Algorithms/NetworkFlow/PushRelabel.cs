using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм проталкивания предпотока (push-relabel / relabel-to-front)
/// для нахождения максимального потока
/// </summary>
[Serializable]
public class PushRelabel
{
    private readonly int _n;
    private readonly int _s;
    private readonly int _t;
    private readonly double[] _excess;
    private readonly int[] _height;
    private readonly List<FlowEdge>[] _adj;

    /// <summary>
    /// Значение максимального потока
    /// </summary>
    public double MaxFlow { get; private set; }

    /// <summary>
    /// Вычисляет максимальный поток из s в t методом проталкивания предпотока
    /// </summary>
    /// <param name="network">Сеть потоков</param>
    /// <param name="s">Исток</param>
    /// <param name="t">Сток</param>
    public PushRelabel(FlowNetwork network, int s, int t)
    {
        _n = network.V;
        _s = s;
        _t = t;
        _excess = new double[_n];
        _height = new int[_n];
        _adj = new List<FlowEdge>[_n];

        for (int i = 0; i < _n; i++)
            _adj[i] = network.Adj(i);

        _height[s] = _n;

        foreach (FlowEdge e in _adj[s])
        {
            double cap = e.ResidualCapacityTo(e.Other(s));
            if (cap > 0)
            {
                e.AddFlowTo(e.Other(s), cap);
                _excess[e.Other(s)] += cap;
                _excess[s] -= cap;
            }
        }

        List<int> list = new List<int>();
        for (int i = 0; i < _n; i++)
        {
            if (i != s && i != t)
                list.Add(i);
        }

        int idx = 0;
        while (idx < list.Count)
        {
            int u = list[idx];
            int oldHeight = _height[u];
            Discharge(u);
            if (_height[u] > oldHeight)
            {
                list.RemoveAt(idx);
                list.Insert(0, u);
                idx = 0;
            }
            else
            {
                idx++;
            }
        }

        MaxFlow = _excess[t];
    }

    private void Discharge(int u)
    {
        while (_excess[u] > 0)
        {
            bool pushed = false;
            foreach (FlowEdge e in _adj[u])
            {
                int v = e.Other(u);
                if (e.ResidualCapacityTo(v) > 0 && _height[u] == _height[v] + 1)
                {
                    double delta = Math.Min(_excess[u], e.ResidualCapacityTo(v));
                    e.AddFlowTo(v, delta);
                    _excess[u] -= delta;
                    _excess[v] += delta;
                    pushed = true;
                    if (_excess[u] <= 0) break;
                }
            }

            if (!pushed)
            {
                Relabel(u);
                break;
            }
        }
    }

    private void Relabel(int u)
    {
        int minHeight = int.MaxValue;
        foreach (FlowEdge e in _adj[u])
        {
            int v = e.Other(u);
            if (e.ResidualCapacityTo(v) > 0)
                minHeight = Math.Min(minHeight, _height[v]);
        }

        if (minHeight < int.MaxValue)
            _height[u] = minHeight + 1;
    }
}
