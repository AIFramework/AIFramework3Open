using System;
using System.Collections.Generic;

namespace AI.Algorithms.VRP;

/// <summary>
/// Эвристика Лина-Кернигана для TSP/VRP
/// </summary>
[Serializable]
public class LinKernighan
{
    private readonly VRPInstance _inst;

    /// <summary>
    /// Создаёт экземпляр алгоритма Лина-Кернигана
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    public LinKernighan(VRPInstance inst)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
    }

    /// <summary>
    /// Решает задачу VRP эвристикой Лина-Кернигана
    /// </summary>
    /// <param name="initial">Начальное решение (если null — строится жадно)</param>
    public VRPSolution Solve(VRPSolution initial = null)
    {
        var sol = initial?.Clone() ?? BuildInitial();

        for (int r = 0; r < sol.Routes.Count; r++)
        {
            var route = sol.Routes[r];
            if (route.Count < 4) continue;
            LKImprove(route);
        }

        return sol;
    }

    private void LKImprove(List<int> route)
    {
        int n = route.Count;
        bool improved = true;
        int maxDepth = 5;

        while (improved)
        {
            improved = false;
            for (int i = 0; i < n && !improved; i++)
            {
                improved = LKStep(route, i, maxDepth);
            }
        }
    }

    private bool LKStep(List<int> route, int startIdx, int maxDepth)
    {
        int n = route.Count;
        var bestRoute = new List<int>(route);
        double bestCost = RouteDist(route);
        bool found = false;

        var tried = new HashSet<long>();

        int t1 = startIdx;
        for (int t2Off = 1; t2Off <= 2; t2Off++)
        {
            int t2 = (t1 + t2Off) % n;
            double g1 = EdgeDist(route, t1, t2);

            for (int depth = 0; depth < maxDepth; depth++)
            {
                double bestGain = -1;
                int bestT3 = -1;

                for (int t3 = 0; t3 < n; t3++)
                {
                    if (t3 == t1 || t3 == t2) continue;
                    long key = Math.Min(t2, t3) * (long)n + Math.Max(t2, t3);
                    if (tried.Contains(key)) continue;

                    double g2 = EdgeDist(route, t2, t3);
                    double gain = g1 - g2;
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestT3 = t3;
                    }
                }

                if (bestT3 < 0) break;
                tried.Add(Math.Min(t2, bestT3) * (long)n + Math.Max(t2, bestT3));

                int lo = Math.Min(t2, bestT3);
                int hi = Math.Max(t2, bestT3);
                route.Reverse(lo, hi - lo + 1);
                double newCost = RouteDist(route);

                if (newCost < bestCost - 1e-10)
                {
                    bestCost = newCost;
                    bestRoute = new List<int>(route);
                    found = true;
                    break;
                }

                route.Reverse(lo, hi - lo + 1);
                t2 = bestT3;
                g1 = bestGain;
            }
        }

        if (found)
        {
            route.Clear();
            route.AddRange(bestRoute);
        }
        return found;
    }

    private double EdgeDist(List<int> route, int i, int j)
    {
        int ni = route[i] + 1;
        int nj = route[j] + 1;
        return _inst.Distance(ni, nj);
    }

    private double RouteDist(List<int> route)
    {
        if (route.Count == 0) return 0;
        double d = _inst.Distance(0, route[0] + 1);
        for (int i = 0; i < route.Count - 1; i++)
            d += _inst.Distance(route[i] + 1, route[i + 1] + 1);
        d += _inst.Distance(route[route.Count - 1] + 1, 0);
        return d;
    }

    private VRPSolution BuildInitial()
    {
        var sw = new Sweep(_inst);
        return sw.Solve();
    }
}
