using System;
using System.Collections.Generic;

namespace AI.Algorithms.EWG;

/// <summary>
/// Поиск в ширину (BFS)
/// </summary>
[Serializable]
public class BFS
{
    /// <summary>
    /// Массив посещённых вершин
    /// </summary>
    public bool[] Visited { get; }

    /// <summary>
    /// Расстояние (число рёбер) до каждой вершины от начальной
    /// </summary>
    public int[] DistanceTo { get; }

    /// <summary>
    /// Массив родительских вершин (предшественник на кратчайшем пути)
    /// </summary>
    public int[] EdgeTo { get; }

    /// <summary>
    /// Поиск в ширину
    /// </summary>
    /// <param name="graph">Невзвешенный граф</param>
    /// <param name="startVertex">Начальная вершина</param>
    public BFS(Graph graph, int startVertex)
    {
        Visited = new bool[graph.V];
        DistanceTo = new int[graph.V];
        EdgeTo = new int[graph.V];

        for (int i = 0; i < graph.V; i++)
        {
            DistanceTo[i] = -1;
            EdgeTo[i] = -1;
        }

        Queue<int> queue = new Queue<int>();
        Visited[startVertex] = true;
        DistanceTo[startVertex] = 0;
        queue.Enqueue(startVertex);

        while (queue.Count > 0)
        {
            int v = queue.Dequeue();

            foreach (int w in graph.Adj(v))
            {
                if (!Visited[w])
                {
                    Visited[w] = true;
                    EdgeTo[w] = v;
                    DistanceTo[w] = DistanceTo[v] + 1;
                    queue.Enqueue(w);
                }
            }
        }
    }

    /// <summary>
    /// Путь до вершины от начальной
    /// </summary>
    /// <param name="v">Целевая вершина</param>
    /// <returns>Последовательность вершин от начальной до v, или null если пути нет</returns>
    public IEnumerable<int> PathTo(int v)
    {
        if (!Visited[v]) return null;

        Stack<int> path = new Stack<int>();
        for (int x = v; x != -1; x = EdgeTo[x])
            path.Push(x);

        return path;
    }
}
