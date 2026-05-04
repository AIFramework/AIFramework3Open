using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.GraphStructure;

/// <summary>
/// Алгоритм Тарьяна для нахождения сильно связанных компонент (SCC)
/// ориентированного графа за один проход DFS.
/// </summary>
[Serializable]
public class TarjanSCC
{
    /// <summary>
    /// Индекс компоненты для каждой вершины
    /// </summary>
    public int[] ComponentId { get; }

    /// <summary>
    /// Количество сильно связанных компонент
    /// </summary>
    public int Count { get; private set; }

    private int _index;
    private readonly int[] _disc;
    private readonly int[] _low;
    private readonly bool[] _onStack;
    private readonly Stack<int> _stack;
    private readonly Graph _graph;

    /// <summary>
    /// Находит все сильно связанные компоненты в ориентированном графе
    /// </summary>
    /// <param name="graph">Ориентированный граф (построенный с помощью AddArc)</param>
    public TarjanSCC(Graph graph)
    {
        _graph = graph;
        int v = graph.V;
        ComponentId = new int[v];
        _disc = new int[v];
        _low = new int[v];
        _onStack = new bool[v];
        _stack = new Stack<int>();
        _index = 0;
        Count = 0;

        for (int i = 0; i < v; i++)
        {
            _disc[i] = -1;
            ComponentId[i] = -1;
        }

        for (int i = 0; i < v; i++)
        {
            if (_disc[i] == -1)
                Dfs(i);
        }
    }

    private void Dfs(int u)
    {
        _disc[u] = _low[u] = _index++;
        _stack.Push(u);
        _onStack[u] = true;

        foreach (int w in _graph.Adj(u))
        {
            if (_disc[w] == -1)
            {
                Dfs(w);
                _low[u] = Math.Min(_low[u], _low[w]);
            }
            else if (_onStack[w])
            {
                _low[u] = Math.Min(_low[u], _disc[w]);
            }
        }

        if (_low[u] == _disc[u])
        {
            int w;
            do
            {
                w = _stack.Pop();
                _onStack[w] = false;
                ComponentId[w] = Count;
            } while (w != u);
            Count++;
        }
    }

    /// <summary>
    /// Проверяет, находятся ли две вершины в одной сильно связанной компоненте
    /// </summary>
    /// <param name="u">Первая вершина</param>
    /// <param name="v">Вторая вершина</param>
    /// <returns>true, если вершины сильно связаны</returns>
    public bool StronglyConnected(int u, int v)
    {
        return ComponentId[u] == ComponentId[v];
    }
}
