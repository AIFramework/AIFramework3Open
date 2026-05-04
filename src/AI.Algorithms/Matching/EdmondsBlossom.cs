using System;
using System.Collections.Generic;

namespace AI.Algorithms.Matching;

/// <summary>
/// Алгоритм Эдмондса (расцветки / «цветения») для нахождения
/// максимального паросочетания в произвольном графе
/// </summary>
[Serializable]
public class EdmondsBlossom
{
    private readonly int _n;
    private readonly List<int>[] _adj;

    /// <summary>
    /// Паросочетание: Match[v] — вершина-пара для v (-1, если не сопоставлена)
    /// </summary>
    public int[] Match { get; private set; }

    /// <summary>
    /// Создаёт экземпляр для графа с заданным числом вершин
    /// </summary>
    /// <param name="v">Число вершин</param>
    public EdmondsBlossom(int v)
    {
        _n = v;
        _adj = new List<int>[v];
        for (int i = 0; i < v; i++)
            _adj[i] = new List<int>();

        Match = new int[v];
        for (int i = 0; i < v; i++)
            Match[i] = -1;
    }

    /// <summary>
    /// Добавляет неориентированное ребро
    /// </summary>
    /// <param name="u">Первая вершина</param>
    /// <param name="v">Вторая вершина</param>
    public void AddEdge(int u, int v)
    {
        _adj[u].Add(v);
        _adj[v].Add(u);
    }

    /// <summary>
    /// Находит максимальное паросочетание
    /// </summary>
    /// <returns>Размер максимального паросочетания</returns>
    public int MaxMatching()
    {
        int result = 0;

        for (int u = 0; u < _n; u++)
        {
            if (Match[u] == -1)
            {
                if (Augment(u))
                    result++;
            }
        }

        return result;
    }

    private bool Augment(int root)
    {
        int[] parent = new int[_n];
        int[] base_ = new int[_n];
        bool[] blossom = new bool[_n];
        bool[] inQueue = new bool[_n];

        for (int i = 0; i < _n; i++)
        {
            parent[i] = -1;
            base_[i] = i;
        }

        inQueue[root] = true;
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            foreach (int u in _adj[v])
            {
                if (base_[v] == base_[u] || Match[v] == u)
                    continue;

                if (u == root || (Match[u] != -1 && parent[Match[u]] != -1))
                {
                    // Blossom found
                    int curBase = Lca(base_, parent, root, base_[v], base_[u]);

                    Array.Clear(blossom, 0, _n);
                    MarkPath(base_, parent, blossom, v, curBase, u);
                    MarkPath(base_, parent, blossom, u, curBase, v);

                    for (int i = 0; i < _n; i++)
                    {
                        if (blossom[base_[i]])
                        {
                            base_[i] = curBase;
                            if (!inQueue[i])
                            {
                                inQueue[i] = true;
                                queue.Enqueue(i);
                            }
                        }
                    }
                }
                else if (parent[u] == -1)
                {
                    parent[u] = v;
                    if (Match[u] == -1)
                    {
                        UpdateMatching(parent, u);
                        return true;
                    }
                    else
                    {
                        int w = Match[u];
                        inQueue[w] = true;
                        queue.Enqueue(w);
                    }
                }
            }
        }

        return false;
    }

    private int Lca(int[] base_, int[] parent, int root, int a, int b)
    {
        bool[] visited = new bool[_n];
        int cur = a;
        while (true)
        {
            visited[cur] = true;
            if (cur == root) break;
            cur = base_[parent[Match[cur]]];
        }

        cur = b;
        while (!visited[cur])
            cur = base_[parent[Match[cur]]];

        return cur;
    }

    private void MarkPath(int[] base_, int[] parent, bool[] blossom, int v, int b, int child)
    {
        while (base_[v] != b)
        {
            blossom[base_[v]] = true;
            blossom[base_[Match[v]]] = true;
            parent[v] = child;
            child = Match[v];
            v = parent[Match[v]];
        }
    }

    private void UpdateMatching(int[] parent, int u)
    {
        while (u != -1)
        {
            int pv = parent[u];
            int ppv = Match[pv];
            Match[u] = pv;
            Match[pv] = u;
            u = ppv;
        }
    }
}
