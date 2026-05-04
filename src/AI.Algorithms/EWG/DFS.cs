using System;
using System.Collections.Generic;

namespace AI.Algorithms.EWG;

/// <summary>
/// Поиск в глубину (DFS)
/// </summary>
[Serializable]
public class DFS
{
    /// <summary>
    /// Массив посещённых вершин
    /// </summary>
    public bool[] Visited { get; }

    /// <summary>
    /// Массив родительских вершин (предшественник на пути обхода)
    /// </summary>
    public int[] EdgeTo { get; }

    /// <summary>
    /// Поиск в глубину
    /// </summary>
    /// <param name="graph">Невзвешенный граф</param>
    /// <param name="startVertex">Начальная вершина</param>
    public DFS(Graph graph, int startVertex)
    {
        Visited = new bool[graph.V];
        EdgeTo = new int[graph.V];

        for (int i = 0; i < graph.V; i++)
            EdgeTo[i] = -1;

        Stack<int> stack = new Stack<int>();
        stack.Push(startVertex);

        while (stack.Count > 0)
        {
            int v = stack.Pop();

            if (Visited[v]) continue;
            Visited[v] = true;

            foreach (int w in graph.Adj(v))
            {
                if (!Visited[w])
                {
                    EdgeTo[w] = v;
                    stack.Push(w);
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
