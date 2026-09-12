using AI.Algorithms.PriorityQueues;
using System;

namespace AI.Algorithms.EWG;

/// <summary>
/// Алгоритм Дейкстры
/// </summary>
[Serializable]
public class DijkstraSPath<T> where T : BaseEdge, new()
{
    private readonly T[] _edges;
    private readonly double[] _distace;
    private readonly IndexPriorityQueueMin<double> minPQ;
    private readonly long _relaxationLimit;
    private long _relaxations;

    /// <summary>
    /// Расстояния
    /// </summary>
    public double[] Distances => _distace;

    /// <summary>
    /// Ребра кратчайшего пути
    /// </summary>
    public T[] Edges => _edges;


    /// <summary>
    /// Алгоритм Дейкстры
    /// </summary>
    /// <remarks>
    /// Вершина может вернуться в очередь, поэтому отрицательные дуги без отрицательных циклов
    /// дают верный ответ, хотя и медленнее. Отрицательный цикл обнаруживается по числу улучшений
    /// расстояний и вызывает исключение вместо бесконечной работы.
    /// </remarks>
    /// <exception cref="InvalidOperationException">В графе, достижимом из начальной вершины, есть отрицательный цикл</exception>
    public DijkstraSPath(GraphW<T> graph, int vertex_start)
    {
        _edges = new T[graph.V];
        _distace = new double[graph.V];
        minPQ = new IndexPriorityQueueMin<double>(graph.V);
        _relaxationLimit = ((long)graph.V * (graph.Arcs + 1)) + graph.V;

        for (int i = 0; i < graph.V; i++)
            _distace[i] = double.MaxValue;

        _distace[vertex_start] = 0;

        minPQ.Insert(vertex_start, 0);

        while (!minPQ.IsEmpty())
        {
            int v = minPQ.DelMinGetIndex();

            foreach (T e in graph.AdjEW(v))
                Upd(e);
        }
    }

    // Обновление (ослабление связи)
    private void Upd(T e)
    {
        int v_in = e.StartV, v_out = e.EndV;
        double w = _distace[v_in] + e.W;

        if (_distace[v_out] > w)
        {
            if (++_relaxations > _relaxationLimit)
                throw new InvalidOperationException(
                    "Расстояния улучшаются без конца: в графе есть цикл отрицательной длины. Используйте BellmanFordSP");

            _distace[v_out] = w;
            _edges[v_out] = e;
            if (minPQ.IsContain(v_out))
                minPQ.Update(v_out, w);
            else minPQ.Insert(v_out, w);
        }
    }
}
