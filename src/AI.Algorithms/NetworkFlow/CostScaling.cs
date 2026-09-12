using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм масштабирования стоимости (Goldberg-Tarjan cost scaling)
/// для задачи потока минимальной стоимости
/// </summary>
/// <remarks>
/// <para>
/// Сначала находится максимальный поток без учёта стоимостей, затем он доводится до минимальной
/// стоимости уточнениями. На каждом уточнении ε уменьшается вдвое, дуги с отрицательной
/// приведённой стоимостью насыщаются, а возникшие избытки выталкиваются по допустимым дугам
/// с пересчётом потенциалов — во всех вершинах, включая исток и сток.
/// </para>
/// <para>
/// Поток ε-оптимален при ε &lt; 1/n и целых стоимостях — значит, оптимален. Поэтому стоимости
/// умножаются на n + 1 и уточнения идут до ε = 1. Дробные стоимости сначала переводятся в целые
/// с шагом 10⁻⁶ — в этом случае стоимость оптимальна с той же точностью.
/// </para>
/// </remarks>
[Serializable]
public class CostScaling
{
    private const double FractionalCostScale = 1e6;

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
    public CostScaling(int v)
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
    public (double flow, double cost) Solve(int s, int t)
    {
        for (int i = 0; i < _flow.Count; i++)
            _flow[i] = 0;

        if (s == t)
            return (0, 0);

        FindMaxFlow(s, t);

        long[] cost = ScaledCosts();
        long[] potential = new long[_v];

        long epsilon = 0;
        foreach (long c in cost)
            epsilon = Math.Max(epsilon, Math.Abs(c));

        // При нулевых потенциалах любой поток maxCost-оптимален; дальше ε делится пополам до единицы
        while (epsilon > 1)
        {
            epsilon = Math.Max(1, epsilon / 2);
            Refine(cost, potential, epsilon);
        }

        double totalFlow = 0;
        double totalCost = 0;

        for (int i = 0; i < _from.Count; i += 2)
        {
            if (_from[i] == s)
                totalFlow += _flow[i];

            if (_to[i] == s)
                totalFlow -= _flow[i];

            totalCost += _flow[i] * _cost[i];
        }

        return (totalFlow, totalCost);
    }

    private long[] ScaledCosts()
    {
        bool integral = true;

        foreach (double c in _cost)
        {
            if (Math.Abs(c - Math.Round(c)) > 1e-9)
            {
                integral = false;
                break;
            }
        }

        double unit = integral ? 1 : FractionalCostScale;
        long[] result = new long[_cost.Count];

        for (int i = 0; i < _cost.Count; i++)
        {
            double scaled = Math.Round(_cost[i] * unit) * (_v + 1);

            if (Math.Abs(scaled) > long.MaxValue / 8)
                throw new OverflowException("Стоимости слишком велики для масштабирования");

            result[i] = (long)scaled;
        }

        return result;
    }

    private void Refine(long[] cost, long[] potential, long epsilon)
    {
        long[] excess = new long[_v];

        // Насыщение дуг с отрицательной приведённой стоимостью делает поток 0-оптимальным,
        // но нарушает баланс в вершинах
        for (int id = 0; id < _from.Count; id++)
        {
            int residual = _cap[id] - _flow[id];

            if (residual > 0 && cost[id] + potential[_from[id]] - potential[_to[id]] < 0)
            {
                excess[_from[id]] -= residual;
                excess[_to[id]] += residual;
                _flow[id] += residual;
                _flow[id ^ 1] -= residual;
            }
        }

        var active = new Queue<int>();
        for (int v = 0; v < _v; v++)
        {
            if (excess[v] > 0)
                active.Enqueue(v);
        }

        while (active.Count > 0)
        {
            int u = active.Dequeue();

            while (excess[u] > 0)
            {
                foreach (int id in _graph[u])
                {
                    int residual = _cap[id] - _flow[id];

                    if (residual <= 0 || cost[id] + potential[u] - potential[_to[id]] >= 0)
                        continue;

                    int delta = (int)Math.Min(excess[u], residual);
                    int w = _to[id];
                    bool wasActive = excess[w] > 0;

                    excess[u] -= delta;
                    excess[w] += delta;
                    _flow[id] += delta;
                    _flow[id ^ 1] -= delta;

                    if (!wasActive && excess[w] > 0)
                        active.Enqueue(w);

                    if (excess[u] == 0)
                        break;
                }

                if (excess[u] == 0)
                    break;

                // Допустимых дуг нет: потенциал опускается так, чтобы появилась хотя бы одна,
                // а ε-оптимальность остальных дуг сохранилась
                long best = long.MinValue;

                foreach (int id in _graph[u])
                {
                    if (_cap[id] - _flow[id] > 0)
                        best = Math.Max(best, potential[_to[id]] - cost[id]);
                }

                if (best == long.MinValue)
                    throw new InvalidOperationException("Избыток в вершине без выходящих остаточных дуг: поток несогласован");

                potential[u] = best - epsilon;
            }
        }
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
}
