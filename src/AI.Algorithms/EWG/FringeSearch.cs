using System;
using System.Collections.Generic;

namespace AI.Algorithms.EWG;

/// <summary>
/// Поиск по границе (Fringe Search).
/// Алгоритм, аналогичный IDA*, но с использованием списка границы
/// для избежания повторных обходов.
/// </summary>
[Serializable]
public class FringeSearch<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Найденный путь (список вершин)
    /// </summary>
    public List<int> Path { get; private set; }

    /// <summary>
    /// Стоимость найденного пути
    /// </summary>
    public double PathCost { get; private set; }

    /// <summary>
    /// Признак того, что путь найден
    /// </summary>
    public bool Found { get; private set; }

    /// <summary>
    /// Поиск по границе
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="start">Начальная вершина</param>
    /// <param name="goal">Целевая вершина</param>
    /// <param name="heuristic">Эвристическая функция h(v)</param>
    public FringeSearch(GraphW<T> graph, int start, int goal, Func<int, double> heuristic)
    {
        int V = graph.V;
        LinkedList<int> fringe = new LinkedList<int>();
        Dictionary<int, LinkedListNode<int>> nodeMap = new Dictionary<int, LinkedListNode<int>>();
        double[] g = new double[V];
        int[] parent = new int[V];
        bool[] inFringe = new bool[V];

        for (int i = 0; i < V; i++)
        {
            g[i] = double.MaxValue;
            parent[i] = -1;
        }

        g[start] = 0;
        var startNode = fringe.AddLast(start);
        nodeMap[start] = startNode;
        inFringe[start] = true;

        double threshold = heuristic(start);
        Found = false;

        while (fringe.Count > 0 && !Found)
        {
            double fmin = double.MaxValue;
            var current = fringe.First;

            while (current != null && !Found)
            {
                int v = current.Value;
                double fVal = g[v] + heuristic(v);

                if (fVal > threshold)
                {
                    if (fVal < fmin) fmin = fVal;
                    current = current.Next;
                    continue;
                }

                if (v == goal)
                {
                    Found = true;
                    PathCost = g[goal];
                    break;
                }

                var insertAfter = current;
                foreach (T e in graph.AdjEW(v))
                {
                    int w = e.EndV;
                    double gNew = g[v] + e.W;

                    if (gNew >= g[w]) continue;

                    g[w] = gNew;
                    parent[w] = v;

                    if (inFringe[w])
                    {
                        var existingNode = nodeMap[w];
                        if (existingNode != current)
                        {
                            fringe.Remove(existingNode);
                            var newNode = fringe.AddAfter(insertAfter, w);
                            nodeMap[w] = newNode;
                            insertAfter = newNode;
                        }
                    }
                    else
                    {
                        var newNode = fringe.AddAfter(insertAfter, w);
                        nodeMap[w] = newNode;
                        inFringe[w] = true;
                        insertAfter = newNode;
                    }
                }

                var next = current.Next;
                fringe.Remove(current);
                inFringe[v] = false;
                nodeMap.Remove(v);
                current = next;
            }

            if (!Found)
                threshold = fmin;
        }

        if (Found)
        {
            Path = new List<int>();
            for (int v = goal; v != -1; v = parent[v])
                Path.Insert(0, v);
        }
        else
        {
            Path = new List<int>();
            PathCost = double.MaxValue;
        }
    }
}
