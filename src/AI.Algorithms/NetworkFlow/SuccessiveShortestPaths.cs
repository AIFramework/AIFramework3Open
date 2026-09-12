using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм последовательных кратчайших путей для задачи
/// нахождения потока минимальной стоимости (min-cost max-flow)
/// </summary>
[Serializable]
public class SuccessiveShortestPaths
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
    public SuccessiveShortestPaths(int v)
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
    /// <returns>Кортеж (поток, стоимость); повторный вызов решает задачу заново</returns>
    /// <exception cref="InvalidOperationException">
    /// В сети есть цикл отрицательной стоимости: метод кратчайших путей требует его отсутствия,
    /// такие задачи решают <see cref="CycleCanceling"/> и <see cref="CostScaling"/>
    /// </exception>
    public (double flow, double cost) Solve(int s, int t)
    {
        double totalFlow = 0;
        double totalCost = 0;

        for (int i = 0; i < _flow.Count; i++)
            _flow[i] = 0;

        if (s == t)
            return (0, 0);

        while (true)
        {
            double[] dist = new double[_v];
            for (int i = 0; i < _v; i++)
                dist[i] = double.MaxValue;
            dist[s] = 0;

            bool[] inQueue = new bool[_v];
            int[] parentEdge = new int[_v];
            for (int i = 0; i < _v; i++)
                parentEdge[i] = -1;

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(s);
            inQueue[s] = true;
            int[] enqueued = new int[_v];

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                inQueue[u] = false;

                foreach (int id in _graph[u])
                {
                    if (_cap[id] - _flow[id] > 0 && dist[u] + _cost[id] < dist[_to[id]] - 1e-9)
                    {
                        dist[_to[id]] = dist[u] + _cost[id];
                        parentEdge[_to[id]] = id;
                        if (!inQueue[_to[id]])
                        {
                            // Вершина, попавшая в очередь больше n раз, лежит на отрицательном цикле
                            if (++enqueued[_to[id]] > _v)
                                throw new InvalidOperationException(
                                    "Сеть содержит цикл отрицательной стоимости: метод последовательных кратчайших путей "
                                    + "требует его отсутствия — используйте CycleCanceling или CostScaling");

                            queue.Enqueue(_to[id]);
                            inQueue[_to[id]] = true;
                        }
                    }
                }
            }

            if (dist[t] >= double.MaxValue)
                break;

            int pushFlow = int.MaxValue;
            for (int v = t; v != s; v = _from[parentEdge[v]])
                pushFlow = Math.Min(pushFlow, _cap[parentEdge[v]] - _flow[parentEdge[v]]);

            for (int v = t; v != s; v = _from[parentEdge[v]])
            {
                _flow[parentEdge[v]] += pushFlow;
                _flow[parentEdge[v] ^ 1] -= pushFlow;
            }

            totalFlow += pushFlow;
            totalCost += pushFlow * dist[t];
        }

        return (totalFlow, totalCost);
    }
}
