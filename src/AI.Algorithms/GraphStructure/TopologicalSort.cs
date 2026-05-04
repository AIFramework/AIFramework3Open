using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.GraphStructure;

/// <summary>
/// Топологическая сортировка ориентированного ациклического графа (DAG).
/// Используется алгоритм Кана на основе подсчёта входящих степеней и очереди.
/// </summary>
[Serializable]
public class TopologicalSort
{
    /// <summary>
    /// Результат топологической сортировки (порядок вершин)
    /// </summary>
    public int[] Order { get; }

    /// <summary>
    /// Признак наличия цикла в графе (если true — граф не является DAG)
    /// </summary>
    public bool HasCycle { get; }

    /// <summary>
    /// Выполняет топологическую сортировку ориентированного графа алгоритмом Кана
    /// </summary>
    /// <param name="graph">Ориентированный граф (построенный с помощью AddArc)</param>
    public TopologicalSort(Graph graph)
    {
        int v = graph.V;
        int[] inDegree = new int[v];

        for (int i = 0; i < v; i++)
        {
            foreach (int neighbor in graph.Adj(i))
            {
                inDegree[neighbor]++;
            }
        }

        Queue<int> queue = new Queue<int>();
        for (int i = 0; i < v; i++)
        {
            if (inDegree[i] == 0)
                queue.Enqueue(i);
        }

        List<int> order = new List<int>(v);

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            order.Add(u);

            foreach (int neighbor in graph.Adj(u))
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (order.Count == v)
        {
            Order = order.ToArray();
            HasCycle = false;
        }
        else
        {
            Order = null;
            HasCycle = true;
        }
    }
}
