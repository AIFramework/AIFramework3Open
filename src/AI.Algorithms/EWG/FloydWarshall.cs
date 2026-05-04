using System;
using System.Collections.Generic;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм Флойда-Уоршелла для поиска кратчайших путей между всеми парами вершин
/// </summary>
[Serializable]
public class FloydWarshall<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Матрица кратчайших расстояний между всеми парами вершин
    /// </summary>
    public double[,] Dist { get; }

    /// <summary>
    /// Матрица следующих вершин для восстановления пути
    /// </summary>
    public int[,] Next { get; }

    /// <summary>
    /// Алгоритм Флойда-Уоршелла
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    public FloydWarshall(GraphW<T> graph)
    {
        int V = graph.V;
        Dist = new double[V, V];
        Next = new int[V, V];

        for (int i = 0; i < V; i++)
        {
            for (int j = 0; j < V; j++)
            {
                Dist[i, j] = (i == j) ? 0 : double.MaxValue;
                Next[i, j] = -1;
            }
        }

        for (int v = 0; v < V; v++)
        {
            foreach (T e in graph.AdjEW(v))
            {
                int u = e.StartV, w = e.EndV;
                if (e.W < Dist[u, w])
                {
                    Dist[u, w] = e.W;
                    Next[u, w] = w;
                }
            }
        }

        for (int k = 0; k < V; k++)
        {
            for (int i = 0; i < V; i++)
            {
                for (int j = 0; j < V; j++)
                {
                    if (Dist[i, k] < double.MaxValue && Dist[k, j] < double.MaxValue)
                    {
                        double newDist = Dist[i, k] + Dist[k, j];
                        if (newDist < Dist[i, j])
                        {
                            Dist[i, j] = newDist;
                            Next[i, j] = Next[i, k];
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Кратчайшее расстояние между двумя вершинами
    /// </summary>
    /// <param name="u">Начальная вершина</param>
    /// <param name="v">Конечная вершина</param>
    /// <returns></returns>
    public double DistanceBetween(int u, int v)
    {
        return Dist[u, v];
    }

    /// <summary>
    /// Восстановление кратчайшего пути между двумя вершинами
    /// </summary>
    /// <param name="u">Начальная вершина</param>
    /// <param name="v">Конечная вершина</param>
    /// <returns>Список вершин пути или null если путь не существует</returns>
    public List<int> PathBetween(int u, int v)
    {
        if (Next[u, v] == -1) return null;

        List<int> path = new List<int>();
        int current = u;
        path.Add(current);

        while (current != v)
        {
            current = Next[current, v];
            if (current == -1) return null;
            path.Add(current);
        }

        return path;
    }
}
