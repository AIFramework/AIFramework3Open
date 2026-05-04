using System;
using System.Collections.Generic;
using AI.Algorithms.PriorityQueues;

namespace AI.Algorithms.EWG;

/// <summary>
/// Двунаправленный алгоритм Дейкстры.
/// Выполняет поиск одновременно от начальной и конечной вершин.
/// </summary>
[Serializable]
public class BidirectionalDijkstra<T> where T : BaseEdge, new()
{
    private readonly int _start;
    private readonly int _goal;
    private readonly int[] _parentF;
    private readonly int[] _parentB;
    private int _meetVertex;

    /// <summary>
    /// Стоимость найденного пути
    /// </summary>
    public double PathCost { get; }

    /// <summary>
    /// Признак того, что путь найден
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Двунаправленный алгоритм Дейкстры
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="start">Начальная вершина</param>
    /// <param name="goal">Целевая вершина</param>
    public BidirectionalDijkstra(GraphW<T> graph, int start, int goal)
    {
        _start = start;
        _goal = goal;
        int V = graph.V;

        double[] distF = new double[V];
        double[] distB = new double[V];
        bool[] settledF = new bool[V];
        bool[] settledB = new bool[V];
        _parentF = new int[V];
        _parentB = new int[V];

        for (int i = 0; i < V; i++)
        {
            distF[i] = double.MaxValue;
            distB[i] = double.MaxValue;
            _parentF[i] = -1;
            _parentB[i] = -1;
        }

        distF[start] = 0;
        distB[goal] = 0;

        List<T>[] revAdj = new List<T>[V];
        for (int i = 0; i < V; i++)
            revAdj[i] = new List<T>();
        for (int v = 0; v < V; v++)
            foreach (T e in graph.AdjEW(v))
                revAdj[e.EndV].Add(e);

        var pqF = new IndexPriorityQueueMin<double>(V);
        var pqB = new IndexPriorityQueueMin<double>(V);

        pqF.Insert(start, 0);
        pqB.Insert(goal, 0);

        double mu = double.MaxValue;
        _meetVertex = -1;
        bool fDone = false, bDone = false;

        while (true)
        {
            if ((pqF.IsEmpty() || fDone) && (pqB.IsEmpty() || bDone))
                break;

            if (!pqF.IsEmpty() && !fDone)
            {
                int vf = pqF.DelMinGetIndex();
                settledF[vf] = true;

                if (distF[vf] >= mu)
                {
                    fDone = true;
                }
                else
                {
                    if (settledB[vf] && distF[vf] + distB[vf] < mu)
                    {
                        mu = distF[vf] + distB[vf];
                        _meetVertex = vf;
                    }

                    foreach (T e in graph.AdjEW(vf))
                    {
                        int w = e.EndV;
                        if (settledF[w]) continue;
                        double newDist = distF[vf] + e.W;
                        if (newDist < distF[w])
                        {
                            distF[w] = newDist;
                            _parentF[w] = vf;
                            if (pqF.IsContain(w))
                                pqF.Update(w, newDist);
                            else
                                pqF.Insert(w, newDist);
                        }
                    }
                }
            }
            else
            {
                fDone = true;
            }

            if (!pqB.IsEmpty() && !bDone)
            {
                int vb = pqB.DelMinGetIndex();
                settledB[vb] = true;

                if (distB[vb] >= mu)
                {
                    bDone = true;
                }
                else
                {
                    if (settledF[vb] && distF[vb] + distB[vb] < mu)
                    {
                        mu = distF[vb] + distB[vb];
                        _meetVertex = vb;
                    }

                    foreach (T e in revAdj[vb])
                    {
                        int w = e.StartV;
                        if (settledB[w]) continue;
                        double newDist = distB[vb] + e.W;
                        if (newDist < distB[w])
                        {
                            distB[w] = newDist;
                            _parentB[w] = vb;
                            if (pqB.IsContain(w))
                                pqB.Update(w, newDist);
                            else
                                pqB.Insert(w, newDist);
                        }
                    }
                }
            }
            else
            {
                bDone = true;
            }
        }

        if (_meetVertex != -1)
        {
            Found = true;
            PathCost = mu;
        }
        else
        {
            Found = false;
            PathCost = double.MaxValue;
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

        Stack<int> forward = new Stack<int>();
        for (int v = _meetVertex; v != -1; v = _parentF[v])
            forward.Push(v);
        while (forward.Count > 0)
            path.Add(forward.Pop());

        int cur = _parentB[_meetVertex];
        while (cur != -1)
        {
            path.Add(cur);
            cur = _parentB[cur];
        }

        return path;
    }
}
