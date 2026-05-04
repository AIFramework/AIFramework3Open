using System;
using System.Collections.Generic;

namespace AI.Algorithms.Matching;

/// <summary>
/// Задача о назначениях методом последовательных кратчайших путей (Successive Shortest Paths)
/// </summary>
[Serializable]
public class SSPAssignment
{
    /// <summary>
    /// Результат назначения: Assignment[i] — столбец, назначенный строке i (-1, если не назначена)
    /// </summary>
    public int[] Assignment { get; private set; }

    /// <summary>
    /// Суммарная стоимость оптимального назначения
    /// </summary>
    public double TotalCost { get; private set; }

    /// <summary>
    /// Решает задачу о назначениях методом последовательных кратчайших путей
    /// </summary>
    /// <param name="costMatrix">Матрица стоимости (строки — работники, столбцы — задачи)</param>
    public SSPAssignment(double[,] costMatrix)
    {
        int rows = costMatrix.GetLength(0);
        int cols = costMatrix.GetLength(1);

        int s = rows + cols;
        int t = s + 1;
        int totalV = t + 1;

        // Представление графа: пары (прямое + обратное ребро)
        List<int>[] graph = new List<int>[totalV];
        for (int i = 0; i < totalV; i++)
            graph[i] = new List<int>();

        List<int> edgeFrom = new List<int>();
        List<int> edgeTo = new List<int>();
        List<int> edgeCap = new List<int>();
        List<double> edgeCost = new List<double>();
        List<int> edgeFlow = new List<int>();

        void AddEdge(int from, int to, int cap, double cost)
        {
            graph[from].Add(edgeFrom.Count);
            edgeFrom.Add(from);
            edgeTo.Add(to);
            edgeCap.Add(cap);
            edgeCost.Add(cost);
            edgeFlow.Add(0);

            graph[to].Add(edgeFrom.Count);
            edgeFrom.Add(to);
            edgeTo.Add(from);
            edgeCap.Add(0);
            edgeCost.Add(-cost);
            edgeFlow.Add(0);
        }

        for (int i = 0; i < rows; i++)
            AddEdge(s, i, 1, 0);

        for (int j = 0; j < cols; j++)
            AddEdge(rows + j, t, 1, 0);

        int[,] edgeIdx = new int[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                edgeIdx[i, j] = edgeFrom.Count;
                AddEdge(i, rows + j, 1, costMatrix[i, j]);
            }
        }

        // SPFA (Bellman-Ford на очереди)
        while (true)
        {
            double[] dist = new double[totalV];
            for (int i = 0; i < totalV; i++)
                dist[i] = double.MaxValue;
            dist[s] = 0;

            bool[] inQueue = new bool[totalV];
            int[] parentEdge = new int[totalV];
            for (int i = 0; i < totalV; i++)
                parentEdge[i] = -1;

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(s);
            inQueue[s] = true;

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                inQueue[u] = false;

                foreach (int id in graph[u])
                {
                    if (edgeCap[id] - edgeFlow[id] > 0 &&
                        dist[u] + edgeCost[id] < dist[edgeTo[id]])
                    {
                        dist[edgeTo[id]] = dist[u] + edgeCost[id];
                        parentEdge[edgeTo[id]] = id;
                        if (!inQueue[edgeTo[id]])
                        {
                            queue.Enqueue(edgeTo[id]);
                            inQueue[edgeTo[id]] = true;
                        }
                    }
                }
            }

            if (dist[t] >= double.MaxValue)
                break;

            int pushFlow = int.MaxValue;
            for (int v = t; v != s; v = edgeFrom[parentEdge[v]])
                pushFlow = Math.Min(pushFlow, edgeCap[parentEdge[v]] - edgeFlow[parentEdge[v]]);

            for (int v = t; v != s; v = edgeFrom[parentEdge[v]])
            {
                edgeFlow[parentEdge[v]] += pushFlow;
                edgeFlow[parentEdge[v] ^ 1] -= pushFlow;
            }
        }

        Assignment = new int[rows];
        TotalCost = 0;

        for (int i = 0; i < rows; i++)
        {
            Assignment[i] = -1;
            for (int j = 0; j < cols; j++)
            {
                int id = edgeIdx[i, j];
                if (edgeFlow[id] > 0)
                {
                    Assignment[i] = j;
                    TotalCost += costMatrix[i, j];
                    break;
                }
            }
        }
    }
}
