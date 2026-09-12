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
    /// <remarks>
    /// Маршрут режется на сегменты A | B | C | D, и B с C пересобираются всеми семью способами:
    /// каждый по отдельности или оба развёрнуты, переставлены местами, переставлены и развёрнуты.
    /// Среди этих ходов есть все ходы 2-opt, включая разворот хвоста маршрута, поэтому результат
    /// не хуже 2-opt. Прежняя версия разворачивала только B и сводилась к урезанному 2-opt.
    /// </remarks>
    public VRPSolution ThreeOpt(VRPSolution sol)
    {
        var result = sol.Clone();

        foreach (var route in result.Routes)
        {
            bool improved = true;

            while (improved)
            {
                improved = false;
                double current = RouteDist(route);
                int len = route.Count;

                for (int i = 0; i < len && !improved; i++)
                {
                    for (int j = i; j < len && !improved; j++)
                    {
                        for (int k = j + 1; k < len && !improved; k++)
                        {
                            List<int> candidate = BestReconnection(route, i, j, k, current);

                            if (candidate != null)
                            {
                                route.Clear();
                                route.AddRange(candidate);
                                improved = true;
                            }
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
                        // Позиция вставки j = route.Count — в самый конец маршрута
                        for (int j = 0; j <= route.Count && !improved; j++)
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

    // A = [0, i), B = [i, j], C = [j + 1, k], D = (k, конец]: лучшая из семи пересборок B и C,
    // если она короче текущего маршрута; иначе null
    private List<int> BestReconnection(List<int> route, int i, int j, int k, double current)
    {
        var a = route.GetRange(0, i);
        var b = route.GetRange(i, j - i + 1);
        var c = route.GetRange(j + 1, k - j);
        var d = route.GetRange(k + 1, route.Count - k - 1);

        var bReversed = new List<int>(b);
        bReversed.Reverse();
        var cReversed = new List<int>(c);
        cReversed.Reverse();

        List<int>[][] variants =
        {
            new[] { bReversed, c },
            new[] { b, cReversed },
            new[] { bReversed, cReversed },
            new[] { c, b },
            new[] { cReversed, b },
            new[] { c, bReversed },
            new[] { cReversed, bReversed },
        };

        List<int> best = null;
        double bestCost = current - 1e-10;

        foreach (var variant in variants)
        {
            var candidate = new List<int>(route.Count);
            candidate.AddRange(a);
            candidate.AddRange(variant[0]);
            candidate.AddRange(variant[1]);
            candidate.AddRange(d);

            double cost = RouteDist(candidate);

            if (cost < bestCost)
            {
                bestCost = cost;
                best = candidate;
            }
        }

        return best;
    }
}
