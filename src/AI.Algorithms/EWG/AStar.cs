using System;
using System.Collections.Generic;
using AI.Algorithms.PriorityQueues;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм A* для поиска кратчайшего пути с эвристикой
/// </summary>
[Serializable]
public class AStarSearch<T> where T : BaseEdge, new()
{
    private readonly int _start;
    private readonly int _goal;

    /// <summary>
    /// Стоимость пути от начальной вершины до каждой вершины
    /// </summary>
    public double[] GScore { get; }

    /// <summary>
    /// Ребро, по которому была достигнута каждая вершина
    /// </summary>
    public T[] CameFrom { get; }

    /// <summary>
    /// Стоимость найденного пути
    /// </summary>
    public double PathCost { get; }

    /// <summary>
    /// Признак того, что путь найден
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Алгоритм A*
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="start">Начальная вершина</param>
    /// <param name="goal">Целевая вершина</param>
    /// <param name="heuristic">Эвристическая функция h(v), оценка расстояния от v до цели</param>
    public AStarSearch(GraphW<T> graph, int start, int goal, Func<int, double> heuristic)
    {
        _start = start;
        _goal = goal;
        int V = graph.V;

        GScore = new double[V];
        CameFrom = new T[V];
        double[] fScore = new double[V];

        for (int i = 0; i < V; i++)
        {
            GScore[i] = double.MaxValue;
            fScore[i] = double.MaxValue;
        }

        GScore[start] = 0;
        fScore[start] = heuristic(start);

        var openSet = new IndexPriorityQueueMin<double>(V);
        openSet.Insert(start, fScore[start]);
        bool[] closedSet = new bool[V];

        Found = false;
        PathCost = double.MaxValue;

        while (!openSet.IsEmpty())
        {
            int current = openSet.DelMinGetIndex();

            if (current == goal)
            {
                Found = true;
                PathCost = GScore[goal];
                break;
            }

            closedSet[current] = true;

            foreach (T e in graph.AdjEW(current))
            {
                int neighbor = e.EndV;
                if (closedSet[neighbor]) continue;

                double tentativeG = GScore[current] + e.W;

                if (tentativeG < GScore[neighbor])
                {
                    CameFrom[neighbor] = e;
                    GScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + heuristic(neighbor);

                    if (openSet.IsContain(neighbor))
                        openSet.Update(neighbor, fScore[neighbor]);
                    else
                        openSet.Insert(neighbor, fScore[neighbor]);
                }
            }
        }
    }

    /// <summary>
    /// Восстановление найденного пути
    /// </summary>
    /// <returns>Список вершин пути от начальной до целевой</returns>
    public List<int> GetPath()
    {
        if (!Found) return new List<int>();

        List<int> path = new List<int>();
        int current = _goal;

        while (CameFrom[current] != null)
        {
            path.Insert(0, current);
            current = CameFrom[current].StartV;
        }

        path.Insert(0, current);
        return path;
    }
}
