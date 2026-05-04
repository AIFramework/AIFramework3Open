using System;
using System.Collections.Generic;

namespace AI.Algorithms.Matching;

/// <summary>
/// Алгоритм Хопкрофта-Карпа для нахождения максимального паросочетания в двудольном графе
/// </summary>
[Serializable]
public class HopcroftKarp
{
    private readonly int _leftSize;
    private readonly int _rightSize;
    private readonly List<int>[] _adj;
    private int[] _dist;

    /// <summary>
    /// Паросочетание левых вершин: MatchLeft[i] — индекс правой вершины (-1, если не сопоставлена)
    /// </summary>
    public int[] MatchLeft { get; private set; }

    /// <summary>
    /// Паросочетание правых вершин: MatchRight[j] — индекс левой вершины (-1, если не сопоставлена)
    /// </summary>
    public int[] MatchRight { get; private set; }

    /// <summary>
    /// Создаёт экземпляр для двудольного графа
    /// </summary>
    /// <param name="leftSize">Число вершин левой доли</param>
    /// <param name="rightSize">Число вершин правой доли</param>
    public HopcroftKarp(int leftSize, int rightSize)
    {
        _leftSize = leftSize;
        _rightSize = rightSize;
        _adj = new List<int>[leftSize];
        for (int i = 0; i < leftSize; i++)
            _adj[i] = new List<int>();

        MatchLeft = new int[leftSize];
        MatchRight = new int[rightSize];

        for (int i = 0; i < leftSize; i++) MatchLeft[i] = -1;
        for (int i = 0; i < rightSize; i++) MatchRight[i] = -1;
    }

    /// <summary>
    /// Добавляет ребро между левой и правой вершинами
    /// </summary>
    /// <param name="left">Индекс левой вершины</param>
    /// <param name="right">Индекс правой вершины</param>
    public void AddEdge(int left, int right)
    {
        _adj[left].Add(right);
    }

    /// <summary>
    /// Находит максимальное паросочетание
    /// </summary>
    /// <returns>Размер максимального паросочетания</returns>
    public int MaxMatching()
    {
        int matching = 0;
        _dist = new int[_leftSize + 1];

        while (Bfs())
        {
            for (int u = 0; u < _leftSize; u++)
            {
                if (MatchLeft[u] == -1)
                {
                    if (Dfs(u))
                        matching++;
                }
            }
        }

        return matching;
    }

    private bool Bfs()
    {
        Queue<int> queue = new Queue<int>();

        for (int u = 0; u < _leftSize; u++)
        {
            if (MatchLeft[u] == -1)
            {
                _dist[u] = 0;
                queue.Enqueue(u);
            }
            else
            {
                _dist[u] = int.MaxValue;
            }
        }

        _dist[_leftSize] = int.MaxValue;

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            if (_dist[u] < _dist[_leftSize])
            {
                foreach (int v in _adj[u])
                {
                    int pairU = MatchRight[v] == -1 ? _leftSize : MatchRight[v];
                    if (_dist[pairU] == int.MaxValue)
                    {
                        _dist[pairU] = _dist[u] + 1;
                        if (pairU != _leftSize)
                            queue.Enqueue(pairU);
                    }
                }
            }
        }

        return _dist[_leftSize] != int.MaxValue;
    }

    private bool Dfs(int u)
    {
        if (u == _leftSize) return true;

        foreach (int v in _adj[u])
        {
            int pairU = MatchRight[v] == -1 ? _leftSize : MatchRight[v];
            if (_dist[pairU] == _dist[u] + 1)
            {
                if (Dfs(pairU))
                {
                    MatchRight[v] = u;
                    MatchLeft[u] = v;
                    return true;
                }
            }
        }

        _dist[u] = int.MaxValue;
        return false;
    }
}
