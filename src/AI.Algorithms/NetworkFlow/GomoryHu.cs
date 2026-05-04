using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Дерево Гомори-Ху для нахождения минимальных разрезов между всеми парами вершин
/// </summary>
[Serializable]
public class GomoryHu
{
    private readonly int _n;
    private readonly List<(int to, double w)>[] _adj;
    private int[] _treeParent;
    private double[] _treeCost;
    private bool _built;

    /// <summary>
    /// Создаёт экземпляр для графа с заданным числом вершин
    /// </summary>
    /// <param name="v">Число вершин</param>
    public GomoryHu(int v)
    {
        _n = v;
        _adj = new List<(int, double)>[v];
        for (int i = 0; i < v; i++)
            _adj[i] = new List<(int, double)>();

        _treeParent = new int[v];
        _treeCost = new double[v];
        _built = false;
    }

    /// <summary>
    /// Добавляет неориентированное ребро с весом
    /// </summary>
    /// <param name="u">Первая вершина</param>
    /// <param name="v">Вторая вершина</param>
    /// <param name="w">Вес ребра</param>
    public void AddEdge(int u, int v, double w)
    {
        _adj[u].Add((v, w));
        _adj[v].Add((u, w));
    }

    /// <summary>
    /// Строит дерево Гомори-Ху
    /// </summary>
    public void Build()
    {
        for (int i = 0; i < _n; i++)
        {
            _treeParent[i] = 0;
            _treeCost[i] = 0;
        }

        for (int i = 1; i < _n; i++)
        {
            FlowNetwork network = new FlowNetwork(_n);
            for (int u = 0; u < _n; u++)
            {
                foreach (var (to, w) in _adj[u])
                {
                    if (u < to)
                    {
                        network.AddEdge(new FlowEdge(u, to, w));
                        network.AddEdge(new FlowEdge(to, u, w));
                    }
                }
            }

            FordFulkerson ff = new FordFulkerson(network, i, _treeParent[i]);
            _treeCost[i] = ff.MaxFlow;

            for (int j = i + 1; j < _n; j++)
            {
                if (_treeParent[j] == _treeParent[i] && ff.InCut(j))
                    _treeParent[j] = i;
            }
        }

        _built = true;
    }

    /// <summary>
    /// Возвращает величину минимального разреза между вершинами u и v
    /// </summary>
    /// <param name="u">Первая вершина</param>
    /// <param name="v">Вторая вершина</param>
    public double MinCut(int u, int v)
    {
        if (!_built)
            throw new InvalidOperationException("Сначала вызовите Build()");

        double minCut = double.MaxValue;
        int cur = u;
        while (cur != v)
        {
            // Поиск пути от u до v в дереве
            bool[] visited = new bool[_n];
            int[] parent = new int[_n];
            for (int i = 0; i < _n; i++)
                parent[i] = -1;

            Queue<int> queue = new Queue<int>();
            visited[u] = true;
            queue.Enqueue(u);
            bool found = false;

            while (queue.Count > 0 && !found)
            {
                int node = queue.Dequeue();
                for (int i = 0; i < _n; i++)
                {
                    if (!visited[i] && (_treeParent[i] == node || _treeParent[node] == i))
                    {
                        visited[i] = true;
                        parent[i] = node;
                        if (i == v) { found = true; break; }
                        queue.Enqueue(i);
                    }
                }
            }

            minCut = double.MaxValue;
            cur = v;
            while (cur != u)
            {
                int p = parent[cur];
                double edgeCost = _treeParent[cur] == p ? _treeCost[cur] : _treeCost[p];
                minCut = Math.Min(minCut, edgeCost);
                cur = p;
            }
            break;
        }

        return minCut;
    }

    /// <summary>
    /// Возвращает множество вершин одной из долей минимального разреза между u и v
    /// </summary>
    /// <param name="u">Первая вершина</param>
    /// <param name="v">Вторая вершина</param>
    public List<int> MinCutPartition(int u, int v)
    {
        if (!_built)
            throw new InvalidOperationException("Сначала вызовите Build()");

        // Найти ребро-бутылочное горло на пути u-v в дереве
        bool[] visited = new bool[_n];
        int[] parent = new int[_n];
        for (int i = 0; i < _n; i++)
            parent[i] = -1;

        Queue<int> queue = new Queue<int>();
        visited[u] = true;
        queue.Enqueue(u);

        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            if (node == v) break;
            for (int i = 0; i < _n; i++)
            {
                if (!visited[i] && (_treeParent[i] == node || _treeParent[node] == i))
                {
                    visited[i] = true;
                    parent[i] = node;
                    queue.Enqueue(i);
                }
            }
        }

        double minCost = double.MaxValue;
        int cutChild = -1;

        int cur = v;
        while (cur != u)
        {
            int p = parent[cur];
            double edgeCost = _treeParent[cur] == p ? _treeCost[cur] : _treeCost[p];
            if (edgeCost < minCost)
            {
                minCost = edgeCost;
                cutChild = _treeParent[cur] == p ? cur : p;
            }
            cur = p;
        }

        // Собираем поддерево cutChild (исключая ребро к его родителю)
        List<int> partition = new List<int>();
        bool[] vis2 = new bool[_n];
        Queue<int> q2 = new Queue<int>();
        vis2[cutChild] = true;
        q2.Enqueue(cutChild);

        while (q2.Count > 0)
        {
            int node = q2.Dequeue();
            partition.Add(node);
            for (int i = 0; i < _n; i++)
            {
                if (!vis2[i] && _treeParent[i] == node)
                {
                    vis2[i] = true;
                    q2.Enqueue(i);
                }
            }
        }

        return partition;
    }
}
