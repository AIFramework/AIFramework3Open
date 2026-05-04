using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Алгоритм Штёра-Вагнера для нахождения минимального разреза неориентированного взвешенного графа
/// </summary>
[Serializable]
public class StoerWagner
{
    private readonly int _n;
    private double[,] _w;

    /// <summary>
    /// Создаёт экземпляр алгоритма для графа с заданным числом вершин
    /// </summary>
    /// <param name="v">Число вершин</param>
    public StoerWagner(int v)
    {
        _n = v;
        _w = new double[v, v];
    }

    /// <summary>
    /// Добавляет неориентированное ребро с весом
    /// </summary>
    /// <param name="u">Первая вершина</param>
    /// <param name="v">Вторая вершина</param>
    /// <param name="w">Вес ребра</param>
    public void AddEdge(int u, int v, double w)
    {
        _w[u, v] += w;
        _w[v, u] += w;
    }

    /// <summary>
    /// Находит минимальный разрез графа
    /// </summary>
    /// <returns>Кортеж (величина минимального разреза, список вершин одной из долей разбиения)</returns>
    public (double MinCut, List<int> Partition) Solve()
    {
        int n = _n;
        double[,] w = new double[n, n];
        Array.Copy(_w, w, _w.Length);

        int[] vertexMap = new int[n];
        for (int i = 0; i < n; i++)
            vertexMap[i] = i;

        List<List<int>> merged = new List<List<int>>();
        for (int i = 0; i < n; i++)
            merged.Add(new List<int> { i });

        double bestCut = double.MaxValue;
        List<int> bestPartition = null;

        bool[] active = new bool[n];
        for (int i = 0; i < n; i++)
            active[i] = true;

        int remaining = n;

        while (remaining > 1)
        {
            double[] key = new double[n];
            bool[] inA = new bool[n];
            int prev = -1, last = -1;

            int start = -1;
            for (int i = 0; i < n; i++)
            {
                if (active[i]) { start = i; break; }
            }

            inA[start] = true;
            prev = start;

            for (int j = 1; j < remaining; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (active[i] && !inA[i])
                        key[i] += w[prev, i];
                }

                double maxKey = -1;
                int maxV = -1;
                for (int i = 0; i < n; i++)
                {
                    if (active[i] && !inA[i] && key[i] > maxKey)
                    {
                        maxKey = key[i];
                        maxV = i;
                    }
                }

                inA[maxV] = true;
                if (j == remaining - 2) prev = maxV;
                if (j == remaining - 1) last = maxV;
            }

            double cutOfPhase = key[last];
            if (cutOfPhase < bestCut)
            {
                bestCut = cutOfPhase;
                bestPartition = new List<int>(merged[last]);
            }

            merged[prev].AddRange(merged[last]);

            for (int i = 0; i < n; i++)
            {
                if (active[i] && i != last)
                {
                    w[prev, i] += w[last, i];
                    w[i, prev] += w[i, last];
                }
            }

            active[last] = false;
            remaining--;
        }

        return (bestCut, bestPartition ?? new List<int>());
    }
}
