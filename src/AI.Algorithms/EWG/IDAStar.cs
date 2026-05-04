using System;
using System.Collections.Generic;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм IDA* (итеративное углубление A*)
/// </summary>
[Serializable]
public class IDAStarSearch<T> where T : BaseEdge, new()
{
    private readonly GraphW<T> _graph;
    private readonly int _goal;
    private readonly Func<int, double> _heuristic;

    /// <summary>
    /// Найденный путь (список вершин)
    /// </summary>
    public List<int> Path { get; private set; }

    /// <summary>
    /// Стоимость найденного пути
    /// </summary>
    public double PathCost { get; private set; }

    /// <summary>
    /// Признак того, что путь найден
    /// </summary>
    public bool Found { get; private set; }

    /// <summary>
    /// Алгоритм IDA*
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="start">Начальная вершина</param>
    /// <param name="goal">Целевая вершина</param>
    /// <param name="heuristic">Эвристическая функция h(v)</param>
    public IDAStarSearch(GraphW<T> graph, int start, int goal, Func<int, double> heuristic)
    {
        _graph = graph;
        _goal = goal;
        _heuristic = heuristic;

        Path = new List<int>();
        Found = false;
        PathCost = double.MaxValue;

        double threshold = heuristic(start);
        List<int> currentPath = new List<int> { start };
        HashSet<int> visited = new HashSet<int> { start };

        while (true)
        {
            double result = Search(currentPath, 0, threshold, visited);

            if (Found)
            {
                Path = new List<int>(currentPath);
                PathCost = result;
                break;
            }

            if (result >= double.MaxValue)
                break;

            threshold = result;
        }
    }

    private double Search(List<int> path, double g, double threshold, HashSet<int> visited)
    {
        int node = path[path.Count - 1];
        double f = g + _heuristic(node);

        if (f > threshold)
            return f;

        if (node == _goal)
        {
            Found = true;
            return g;
        }

        double min = double.MaxValue;

        foreach (T e in _graph.AdjEW(node))
        {
            int neighbor = e.EndV;
            if (visited.Contains(neighbor)) continue;

            path.Add(neighbor);
            visited.Add(neighbor);

            double result = Search(path, g + e.W, threshold, visited);

            if (Found)
                return result;

            if (result < min)
                min = result;

            path.RemoveAt(path.Count - 1);
            visited.Remove(neighbor);
        }

        return min;
    }
}
