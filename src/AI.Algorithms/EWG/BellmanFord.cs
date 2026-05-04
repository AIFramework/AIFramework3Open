using System;
using System.Collections.Generic;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм Беллмана-Форда для поиска кратчайших путей из одной вершины.
/// Поддерживает отрицательные веса рёбер и обнаружение отрицательных циклов.
/// </summary>
[Serializable]
public class BellmanFordSP<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Расстояния от начальной вершины до всех остальных
    /// </summary>
    public double[] Distances { get; }

    /// <summary>
    /// Рёбра кратчайшего пути (предшествующее ребро для каждой вершины)
    /// </summary>
    public T[] Edges { get; }

    /// <summary>
    /// Признак наличия отрицательного цикла в графе
    /// </summary>
    public bool HasNegativeCycle { get; }

    /// <summary>
    /// Алгоритм Беллмана-Форда
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="startVertex">Начальная вершина</param>
    public BellmanFordSP(GraphW<T> graph, int startVertex)
    {
        int V = graph.V;
        Distances = new double[V];
        Edges = new T[V];

        for (int i = 0; i < V; i++)
            Distances[i] = double.MaxValue;

        Distances[startVertex] = 0;

        for (int pass = 0; pass < V - 1; pass++)
        {
            for (int v = 0; v < V; v++)
            {
                if (Distances[v] >= double.MaxValue) continue;

                foreach (T e in graph.AdjEW(v))
                {
                    int u = e.StartV, w = e.EndV;
                    double newDist = Distances[u] + e.W;

                    if (Distances[u] < double.MaxValue && newDist < Distances[w])
                    {
                        Distances[w] = newDist;
                        Edges[w] = e;
                    }
                }
            }
        }

        HasNegativeCycle = false;
        for (int v = 0; v < V && !HasNegativeCycle; v++)
        {
            foreach (T e in graph.AdjEW(v))
            {
                int u = e.StartV, w = e.EndV;
                if (Distances[u] < double.MaxValue && Distances[u] + e.W < Distances[w])
                {
                    HasNegativeCycle = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Последовательность рёбер кратчайшего пути до вершины
    /// </summary>
    /// <param name="v">Целевая вершина</param>
    /// <returns>Рёбра пути или null если путь не существует</returns>
    public IEnumerable<T> PathTo(int v)
    {
        if (Distances[v] >= double.MaxValue) return null;

        Stack<T> path = new Stack<T>();
        for (T e = Edges[v]; e != null; e = Edges[e.StartV])
            path.Push(e);

        return path;
    }
}
