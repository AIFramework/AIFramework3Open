using System;
using System.Collections.Generic;

namespace AI.Algorithms.Matching;

/// <summary>
/// Алгоритм Куна для нахождения максимального паросочетания
/// в двудольном графе методом увеличивающих путей
/// </summary>
[Serializable]
public class KuhnMatching
{
    private readonly int _leftSize;
    private readonly int _rightSize;
    private readonly List<int>[] _adj;
    private bool[] _used;

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
    public KuhnMatching(int leftSize, int rightSize)
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
    /// <param name="l">Индекс левой вершины</param>
    /// <param name="r">Индекс правой вершины</param>
    public void AddEdge(int l, int r)
    {
        _adj[l].Add(r);
    }

    /// <summary>
    /// Находит максимальное паросочетание
    /// </summary>
    /// <returns>Размер максимального паросочетания</returns>
    public int Solve()
    {
        int result = 0;

        for (int v = 0; v < _leftSize; v++)
        {
            _used = new bool[_rightSize];
            if (TryKuhn(v))
                result++;
        }

        return result;
    }

    private bool TryKuhn(int v)
    {
        foreach (int to in _adj[v])
        {
            if (!_used[to])
            {
                _used[to] = true;
                if (MatchRight[to] == -1 || TryKuhn(MatchRight[to]))
                {
                    MatchLeft[v] = to;
                    MatchRight[to] = v;
                    return true;
                }
            }
        }
        return false;
    }
}
