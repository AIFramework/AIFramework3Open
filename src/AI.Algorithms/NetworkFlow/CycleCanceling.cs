using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм устранения отрицательных циклов для задачи потока минимальной стоимости.
/// Сначала находит максимальный поток, затем устраняет отрицательные циклы в остаточном графе.
/// </summary>
[Serializable]
public class CycleCanceling
{
    private readonly int _v;
    private readonly List<int>[] _graph;
    private readonly List<int> _from;
    private readonly List<int> _to;
    private readonly List<int> _cap;
    private readonly List<double> _cost;
    private readonly List<int> _flow;

    /// <summary>
    /// Создаёт экземпляр алгоритма для графа с заданным числом вершин
    /// </summary>
    /// <param name="v">Число вершин</param>
    public CycleCanceling(int v)
    {
        _v = v;
        _graph = new List<int>[v];
        for (int i = 0; i < v; i++)
            _graph[i] = new List<int>();

        _from = new List<int>();
        _to = new List<int>();
        _cap = new List<int>();
        _cost = new List<double>();
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
        _cost.Add(cost);
        _flow.Add(0);

        _graph[to].Add(_from.Count);
        _from.Add(to);
        _to.Add(from);
        _cap.Add(0);
        _cost.Add(-cost);
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
        FindMaxFlow(s, t);

        while (true)
        {
            int[] parentEdge = new int[_v];
            for (int i = 0; i < _v; i++)
                parentEdge[i] = -1;

            int cycleNode = FindNegativeCycle(parentEdge);
            if (cycleNode < 0)
                break;

            int pushFlow = int.MaxValue;
            int v = cycleNode;
            do
            {
                pushFlow = Math.Min(pushFlow, _cap[parentEdge[v]] - _flow[parentEdge[v]]);
                v = _from[parentEdge[v]];
            } while (v != cycleNode);

            v = cycleNode;
            do
            {
                _flow[parentEdge[v]] += pushFlow;
                _flow[parentEdge[v] ^ 1] -= pushFlow;
                v = _from[parentEdge[v]];
            } while (v != cycleNode);
        }

        double totalFlow = 0;
        double totalCost = 0;

        for (int i = 0; i < _from.Count; i += 2)
        {
            if (_from[i] == s)
                totalFlow += _flow[i];
            totalCost += _flow[i] * _cost[i];
        }

        return (totalFlow, totalCost);
    }

    private void FindMaxFlow(int s, int t)
    {
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
    }

    private int FindNegativeCycle(int[] parentEdge)
    {
        double[] dist = new double[_v];
        int lastUpdated = -1;

        for (int iter = 0; iter < _v; iter++)
        {
            lastUpdated = -1;
            for (int id = 0; id < _from.Count; id++)
            {
                if (_cap[id] - _flow[id] > 0 && dist[_from[id]] + _cost[id] < dist[_to[id]])
                {
                    dist[_to[id]] = dist[_from[id]] + _cost[id];
                    parentEdge[_to[id]] = id;
                    lastUpdated = _to[id];
                }
            }
        }

        if (lastUpdated < 0)
            return -1;

        int node = lastUpdated;
        for (int i = 0; i < _v; i++)
            node = _from[parentEdge[node]];

        return node;
    }
}
