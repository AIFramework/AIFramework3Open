using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.GraphStructure;

/// <summary>
/// Нахождение точек сочленения и мостов в неориентированном графе
/// на основе алгоритма Тарьяна (DFS с массивами disc и low).
/// </summary>
[Serializable]
public class ArticulationBridges
{
    /// <summary>
    /// Список точек сочленения графа
    /// </summary>
    public List<int> ArticulationPoints { get; }

    /// <summary>
    /// Список мостов графа (пар вершин)
    /// </summary>
    public List<(int U, int V)> Bridges { get; }

    private int _timer;
    private readonly int[] _disc;
    private readonly int[] _low;
    private readonly bool[] _visited;
    private readonly bool[] _isArticulation;
    private readonly Graph _graph;

    /// <summary>
    /// Находит все точки сочленения и мосты в неориентированном графе
    /// </summary>
    /// <param name="graph">Неориентированный граф (построенный с помощью AddEdge)</param>
    public ArticulationBridges(Graph graph)
    {
        _graph = graph;
        int v = graph.V;
        _disc = new int[v];
        _low = new int[v];
        _visited = new bool[v];
        _isArticulation = new bool[v];
        _timer = 0;
        ArticulationPoints = new List<int>();
        Bridges = new List<(int U, int V)>();

        for (int i = 0; i < v; i++)
        {
            if (!_visited[i])
                Dfs(i, -1);
        }

        for (int i = 0; i < v; i++)
        {
            if (_isArticulation[i])
                ArticulationPoints.Add(i);
        }
    }

    private void Dfs(int u, int parent)
    {
        _visited[u] = true;
        _disc[u] = _low[u] = _timer++;
        int children = 0;

        foreach (int w in _graph.Adj(u))
        {
            if (!_visited[w])
            {
                children++;
                Dfs(w, u);
                _low[u] = Math.Min(_low[u], _low[w]);

                if (parent == -1 && children > 1)
                    _isArticulation[u] = true;

                if (parent != -1 && _low[w] >= _disc[u])
                    _isArticulation[u] = true;

                if (_low[w] > _disc[u])
                    Bridges.Add((u, w));
            }
            else if (w != parent)
            {
                _low[u] = Math.Min(_low[u], _disc[w]);
            }
        }
    }
}
