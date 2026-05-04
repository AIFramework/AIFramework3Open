using System;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм Джонсона для поиска кратчайших путей между всеми парами вершин.
/// Использует Беллмана-Форда для перевзвешивания и Дейкстру для каждой вершины.
/// </summary>
[Serializable]
public class JohnsonAllPairs<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Деревья кратчайших путей для каждой исходной вершины
    /// </summary>
    public ShortestPathTree<T>[] Trees { get; }

    /// <summary>
    /// Признак наличия отрицательного цикла
    /// </summary>
    public bool HasNegativeCycle { get; }

    /// <summary>
    /// Алгоритм Джонсона
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    public JohnsonAllPairs(GraphW<T> graph)
    {
        int V = graph.V;

        GraphW<T> augmented = new GraphW<T>(V + 1);

        for (int v = 0; v < V; v++)
            foreach (T e in graph.AdjEW(v))
                if (e.StartV == v)
                    augmented.AddArceW(e);

        for (int v = 0; v < V; v++)
            augmented.AddArce(V, v, 0);

        BellmanFordSP<T> bf = new BellmanFordSP<T>(augmented, V);

        if (bf.HasNegativeCycle)
        {
            HasNegativeCycle = true;
            return;
        }

        double[] h = bf.Distances;

        GraphW<T> reweighted = new GraphW<T>(V);
        for (int v = 0; v < V; v++)
            foreach (T e in graph.AdjEW(v))
                if (e.StartV == v)
                    reweighted.AddArce(e.StartV, e.EndV, e.W + h[e.StartV] - h[e.EndV]);

        Trees = new ShortestPathTree<T>[V];
        for (int s = 0; s < V; s++)
        {
            DijkstraSPath<T> dijkstra = new DijkstraSPath<T>(reweighted, s);

            double[] adjusted = new double[V];
            for (int v = 0; v < V; v++)
            {
                if (dijkstra.Distances[v] < double.MaxValue)
                    adjusted[v] = dijkstra.Distances[v] - h[s] + h[v];
                else
                    adjusted[v] = double.MaxValue;
            }

            Trees[s] = new ShortestPathTree<T>(dijkstra.Edges, adjusted);
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
        if (HasNegativeCycle) return double.NaN;
        return Trees[u].DistanceTo(v);
    }
}
