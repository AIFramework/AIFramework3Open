using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм масштабирования стоимости (Goldberg-Tarjan cost scaling)
/// для задачи потока минимальной стоимости
/// </summary>
[Serializable]
public class CostScaling
{
    private readonly int _v;
    private readonly List<int>[] _graph;
    private readonly List<int> _from;
    private readonly List<int> _to;
    private readonly List<int> _cap;
    private readonly List<long> _cost;
    private readonly List<int> _flow;

    private const long ScaleFactor = 2;

    /// <summary>
    /// Создаёт экземпляр алгоритма для графа с заданным числом вершин
    /// </summary>
    /// <param name="v">Число вершин</param>
    public CostScaling(int v)
    {
        _v = v;
        _graph = new List<int>[v];
        for (int i = 0; i < v; i++)
            _graph[i] = new List<int>();

        _from = new List<int>();
        _to = new List<int>();
        _cap = new List<int>();
        _cost = new List<long>();
        _flow = new List<int>();
    }

    /// <summary>
    /// Добавляет ориентированное ребро с пропускной способностью и стоимостью
    /// </summary>
    /// <param name="from">Начальная вершина</param>
    /// <param name="to">Конечная вершина</param>
    /// <param name="capacity">Пропускная способность</param>
    /// <param name="cost">Стоимость единицы потока</param>
    public void AddEdge(int from, int to, int capacity, double cost)
    {
        _graph[from].Add(_from.Count);
        _from.Add(from);
        _to.Add(to);
        _cap.Add(capacity);
        _cost.Add((long)(cost * _v));
        _flow.Add(0);

        _graph[to].Add(_from.Count);
        _from.Add(to);
        _to.Add(from);
        _cap.Add(0);
        _cost.Add((long)(-cost * _v));
        _flow.Add(0);
    }

    /// <summary>
    /// Находит поток минимальной стоимости из s в t
    /// </summary>
    /// <param name="s">Исток</param>
    /// <param name="t">Сток</param>
    /// <returns>Кортеж (поток, стоимость)</returns>
    public (double flow, double cost) Solve(int s, int t)
    {
        long[] potential = new long[_v];
        double[] excess = new double[_v];

        long maxCost = 0;
        for (int i = 0; i < _cost.Count; i++)
            maxCost = Math.Max(maxCost, Math.Abs(_cost[i]));

        long epsilon = maxCost;

        // Первоначальное нахождение максимального потока через BFS
        while (true)
        {
            int[] parentEdge = new int[_v];
            for (int i = 0; i < _v; i++)
                parentEdge[i] = -1;

            bool[] visited = new bool[_v];
            Queue<int> queue = new Queue<int>();
            visited[s] = true;
            queue.Enqueue(s);

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int id in _graph[u])
                {
                    if (!visited[_to[id]] && _cap[id] - _flow[id] > 0)
                    {
                        visited[_to[id]] = true;
                        parentEdge[_to[id]] = id;
                        queue.Enqueue(_to[id]);
                    }
                }
            }

            if (!visited[t]) break;

            int pushFlow = int.MaxValue;
            for (int v = t; v != s; v = _from[parentEdge[v]])
                pushFlow = Math.Min(pushFlow, _cap[parentEdge[v]] - _flow[parentEdge[v]]);

            for (int v = t; v != s; v = _from[parentEdge[v]])
            {
                _flow[parentEdge[v]] += pushFlow;
                _flow[parentEdge[v] ^ 1] -= pushFlow;
            }
        }

        // Масштабирование стоимости: итеративное уменьшение epsilon и рефайнинг
        while (epsilon >= 1)
        {
            // Saturate negative reduced-cost arcs
            for (int id = 0; id < _from.Count; id++)
            {
                long reducedCost = _cost[id] + potential[_from[id]] - potential[_to[id]];
                if (reducedCost < 0 && _cap[id] - _flow[id] > 0)
                {
                    int delta = _cap[id] - _flow[id];
                    excess[_from[id]] -= delta;
                    excess[_to[id]] += delta;
                    _flow[id] += delta;
                    _flow[id ^ 1] -= delta;
                }
            }

            // Discharge excess nodes (push/relabel)
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int u = 0; u < _v; u++)
                {
                    if (u == s || u == t) continue;
                    while (excess[u] > 0)
                    {
                        bool pushed = false;
                        foreach (int id in _graph[u])
                        {
                            long reducedCost = _cost[id] + potential[u] - potential[_to[id]];
                            if (reducedCost < 0 && _cap[id] - _flow[id] > 0)
                            {
                                int delta = (int)Math.Min(excess[u], _cap[id] - _flow[id]);
                                if (delta <= 0) continue;
                                excess[u] -= delta;
                                excess[_to[id]] += delta;
                                _flow[id] += delta;
                                _flow[id ^ 1] -= delta;
                                pushed = true;
                                changed = true;
                                if (excess[u] <= 0) break;
                            }
                        }

                        if (!pushed)
                        {
                            long minPotential = long.MaxValue;
                            foreach (int id in _graph[u])
                            {
                                if (_cap[id] - _flow[id] > 0)
                                {
                                    long rp = _cost[id] + potential[u] - potential[_to[id]];
                                    minPotential = Math.Min(minPotential, rp);
                                }
                            }

                            if (minPotential < long.MaxValue)
                                potential[u] -= minPotential + epsilon;
                            else
                                break;

                            changed = true;
                        }
                    }
                }
            }

            epsilon = epsilon / ScaleFactor;
        }

        double totalFlow = 0;
        double totalCost = 0;

        for (int i = 0; i < _from.Count; i += 2)
        {
            if (_from[i] == s)
                totalFlow += _flow[i];
            totalCost += (double)_flow[i] * _cost[i] / _v;
        }

        return (totalFlow, totalCost);
    }
}
