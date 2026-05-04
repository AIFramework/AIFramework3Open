using System;
using System.Collections.Generic;

namespace AI.Algorithms.VRP;

/// <summary>
/// Методы локального поиска для улучшения маршрутов VRP: 2-opt, 3-opt, Or-opt
/// </summary>
[Serializable]
public class LocalSearch
{
    private readonly VRPInstance _inst;

    /// <summary>
    /// Создаёт экземпляр локального поиска
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    public LocalSearch(VRPInstance inst)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
    }

    /// <summary>
    /// Улучшение маршрутов методом 2-opt (инвертирование подпоследовательности)
    /// </summary>
    public VRPSolution TwoOpt(VRPSolution sol)
    {
        var result = sol.Clone();

        for (int r = 0; r < result.Routes.Count; r++)
        {
            var route = result.Routes[r];
            bool improved = true;

            while (improved)
            {
                improved = false;
                for (int i = 0; i < route.Count - 1; i++)
                {
                    for (int j = i + 1; j < route.Count; j++)
                    {
                        double oldDist = SegmentDist(route, i, j);
                        route.Reverse(i, j - i + 1);
                        double newDist = SegmentDist(route, i, j);

                        if (newDist < oldDist - 1e-10)
                        {
                            improved = true;
                        }
                        else
                        {
                            route.Reverse(i, j - i + 1);
                        }
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Улучшение маршрутов методом 3-opt
    /// </summary>
    public VRPSolution ThreeOpt(VRPSolution sol)
    {
        var result = sol.Clone();

        for (int r = 0; r < result.Routes.Count; r++)
        {
            var route = result.Routes[r];
            bool improved = true;

            while (improved)
            {
                improved = false;
                int len = route.Count;
                if (len < 4) continue;

                for (int i = 0; i < len - 2 && !improved; i++)
                {
                    for (int j = i + 1; j < len - 1 && !improved; j++)
                    {
                        for (int k = j + 1; k < len && !improved; k++)
                        {
                            double bestGain = TryThreeOptMoves(route, i, j, k);
                            if (bestGain > 1e-10) improved = true;
                        }
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Улучшение маршрутов методом Or-opt (перемещение подпоследовательности из 1-3 клиентов)
    /// </summary>
    public VRPSolution OrOpt(VRPSolution sol)
    {
        var result = sol.Clone();

        for (int r = 0; r < result.Routes.Count; r++)
        {
            var route = result.Routes[r];
            bool improved = true;

            while (improved)
            {
                improved = false;
                for (int segLen = 1; segLen <= Math.Min(3, route.Count); segLen++)
                {
                    for (int i = 0; i < route.Count - segLen + 1 && !improved; i++)
                    {
                        for (int j = 0; j < route.Count - segLen + 1 && !improved; j++)
                        {
                            if (j >= i && j <= i + segLen) continue;

                            double oldCost = RouteDist(route);
                            var seg = route.GetRange(i, segLen);
                            route.RemoveRange(i, segLen);
                            int insertPos = j > i ? j - segLen : j;
                            route.InsertRange(insertPos, seg);
                            double newCost = RouteDist(route);

                            if (newCost < oldCost - 1e-10)
                            {
                                improved = true;
                            }
                            else
                            {
                                route.RemoveRange(insertPos, segLen);
                                route.InsertRange(i, seg);
                            }
                        }
                    }
                }
            }
        }
        return result;
    }

    private double SegmentDist(List<int> route, int from, int to)
    {
        double dist = 0;
        int prev = (from == 0) ? 0 : route[from - 1] + 1;

        for (int i = from; i <= to; i++)
        {
            dist += _inst.Distance(prev, route[i] + 1);
            prev = route[i] + 1;
        }

        int next = (to == route.Count - 1) ? 0 : route[to + 1] + 1;
        dist += _inst.Distance(prev, next);
        return dist;
    }

    private double RouteDist(List<int> route)
    {
        if (route.Count == 0) return 0;
        double dist = _inst.Distance(0, route[0] + 1);
        for (int i = 0; i < route.Count - 1; i++)
            dist += _inst.Distance(route[i] + 1, route[i + 1] + 1);
        dist += _inst.Distance(route[route.Count - 1] + 1, 0);
        return dist;
    }

    private int Node(List<int> route, int idx)
    {
        if (idx < 0 || idx >= route.Count) return 0;
        return route[idx] + 1;
    }

    private double D(List<int> route, int i, int j)
    {
        return _inst.Distance(Node(route, i), Node(route, j));
    }

    private double TryThreeOptMoves(List<int> route, int i, int j, int k)
    {
        int pi = (i == 0) ? -1 : i - 1;
        double d0 = _inst.Distance(Node(route, pi < 0 ? -1 : pi), Node(route, i))
                   + _inst.Distance(Node(route, j), Node(route, j + 1 < route.Count ? j + 1 : -1))
                   + _inst.Distance(Node(route, k), Node(route, k + 1 < route.Count ? k + 1 : -1));

        double bestGain = 0;

        var seg1 = route.GetRange(i, j - i + 1);
        var seg2 = route.GetRange(j + 1, k - j);
        double oldCost = RouteDist(route);

        seg1.Reverse();
        var test = new List<int>(route.GetRange(0, i));
        test.AddRange(seg1);
        test.AddRange(seg2);
        if (k + 1 < route.Count) test.AddRange(route.GetRange(k + 1, route.Count - k - 1));

        double newCost = RouteDist(test);
        if (oldCost - newCost > bestGain)
        {
            bestGain = oldCost - newCost;
            route.Clear();
            route.AddRange(test);
        }

        return bestGain;
    }
}
