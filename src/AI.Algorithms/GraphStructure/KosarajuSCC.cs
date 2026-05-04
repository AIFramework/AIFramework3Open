using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.GraphStructure;

/// <summary>
/// Алгоритм Косарайю для нахождения сильно связанных компонент (SCC)
/// ориентированного графа. Выполняет два прохода DFS: первый по исходному графу
/// для определения порядка завершения, второй по обратному графу.
/// </summary>
[Serializable]
public class KosarajuSCC
{
    /// <summary>
    /// Индекс компоненты для каждой вершины
    /// </summary>
    public int[] ComponentId { get; }

    /// <summary>
    /// Количество сильно связанных компонент
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Находит все сильно связанные компоненты в ориентированном графе
    /// </summary>
    /// <param name="graph">Ориентированный граф (построенный с помощью AddArc)</param>
    public KosarajuSCC(Graph graph)
    {
        int v = graph.V;
        ComponentId = new int[v];
        Count = 0;

        for (int i = 0; i < v; i++)
            ComponentId[i] = -1;

        // Первый проход: DFS по исходному графу, сохраняем порядок завершения
        bool[] visited = new bool[v];
        Stack<int> finishOrder = new Stack<int>();

        for (int i = 0; i < v; i++)
        {
            if (!visited[i])
                DfsFirst(graph, i, visited, finishOrder);
        }

        // Построение обратного графа
        Graph reversed = BuildReverse(graph);

        // Второй проход: DFS по обратному графу в порядке убывания времени завершения
        while (finishOrder.Count > 0)
        {
            int u = finishOrder.Pop();
            if (ComponentId[u] == -1)
            {
                DfsSecond(reversed, u);
                Count++;
            }
        }
    }

    private void DfsFirst(Graph graph, int u, bool[] visited, Stack<int> finishOrder)
    {
        visited[u] = true;
        foreach (int w in graph.Adj(u))
        {
            if (!visited[w])
                DfsFirst(graph, w, visited, finishOrder);
        }
        finishOrder.Push(u);
    }

    private void DfsSecond(Graph reversed, int u)
    {
        ComponentId[u] = Count;
        foreach (int w in reversed.Adj(u))
        {
            if (ComponentId[w] == -1)
                DfsSecond(reversed, w);
        }
    }

    private static Graph BuildReverse(Graph original)
    {
        Graph rev = new Graph(original.V);
        for (int i = 0; i < original.V; i++)
        {
            foreach (int w in original.Adj(i))
            {
                rev.AddArc(w, i);
            }
        }
        return rev;
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
