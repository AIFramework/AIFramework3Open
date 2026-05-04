using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Метод имитации отжига для решения задачи VRP
/// </summary>
[Serializable]
public class SimulatedAnnealingVRP
{
    private readonly VRPInstance _inst;
    private readonly double _initialTemp;
    private readonly double _coolingRate;
    private readonly int _maxIterations;
    private readonly Random _rng;

    /// <summary>
    /// Создаёт экземпляр метода имитации отжига для VRP
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    /// <param name="initialTemp">Начальная температура</param>
    /// <param name="coolingRate">Скорость охлаждения</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    /// <param name="seed">Начальное значение генератора случайных чисел</param>
    public SimulatedAnnealingVRP(VRPInstance inst, double initialTemp = 1000,
        double coolingRate = 0.9995, int maxIterations = 50000, int seed = 42)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
        _initialTemp = initialTemp;
        _coolingRate = coolingRate;
        _maxIterations = maxIterations;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Решает задачу VRP методом имитации отжига
    /// </summary>
    /// <param name="initial">Начальное решение (если null — строится автоматически)</param>
    public VRPSolution Solve(VRPSolution initial = null)
    {
        var current = initial?.Clone() ?? new ClarkeWright(_inst).Solve();
        double currentCost = current.TotalDistance(_inst);

        var best = current.Clone();
        double bestCost = currentCost;
        double temp = _initialTemp;

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            var neighbor = GenerateNeighbor(current);
            double neighborCost = neighbor.TotalDistance(_inst);

            if (!neighbor.IsValid(_inst))
                neighborCost += 1e6;

            double delta = neighborCost - currentCost;

            if (delta < 0 || _rng.NextDouble() < Math.Exp(-delta / Math.Max(temp, 1e-10)))
            {
                current = neighbor;
                currentCost = neighborCost;

                if (currentCost < bestCost)
                {
                    bestCost = currentCost;
                    best = current.Clone();
                }
            }

            temp *= _coolingRate;
        }

        return best;
    }

    private VRPSolution GenerateNeighbor(VRPSolution sol)
    {
        var neighbor = sol.Clone();
        int moveType = _rng.Next(4);

        switch (moveType)
        {
            case 0: IntraRouteSwap(neighbor); break;
            case 1: InterRouteMove(neighbor); break;
            case 2: IntraRoute2Opt(neighbor); break;
            case 3: InterRouteSwap(neighbor); break;
        }

        return neighbor;
    }

    private void IntraRouteSwap(VRPSolution sol)
    {
        var nonEmpty = sol.Routes.Where(r => r.Count >= 2).ToList();
        if (nonEmpty.Count == 0) return;

        var route = nonEmpty[_rng.Next(nonEmpty.Count)];
        int i = _rng.Next(route.Count);
        int j = _rng.Next(route.Count);
        int tmp = route[i]; route[i] = route[j]; route[j] = tmp;
    }

    private void InterRouteMove(VRPSolution sol)
    {
        var nonEmpty = sol.Routes.Where(r => r.Count > 0).ToList();
        if (nonEmpty.Count < 1) return;

        var srcRoute = nonEmpty[_rng.Next(nonEmpty.Count)];
        int idx = _rng.Next(srcRoute.Count);
        int customer = srcRoute[idx];
        srcRoute.RemoveAt(idx);

        if (sol.Routes.Count < 2 || _rng.NextDouble() < 0.3)
        {
            sol.Routes.Add(new List<int> { customer });
        }
        else
        {
            var dstRoute = sol.Routes[_rng.Next(sol.Routes.Count)];
            int pos = _rng.Next(dstRoute.Count + 1);
            dstRoute.Insert(pos, customer);
        }

        sol.Routes.RemoveAll(r => r.Count == 0);
    }

    private void IntraRoute2Opt(VRPSolution sol)
    {
        var nonEmpty = sol.Routes.Where(r => r.Count >= 3).ToList();
        if (nonEmpty.Count == 0) return;

        var route = nonEmpty[_rng.Next(nonEmpty.Count)];
        int i = _rng.Next(route.Count - 1);
        int j = _rng.Next(i + 1, route.Count);
        route.Reverse(i, j - i + 1);
    }

    private void InterRouteSwap(VRPSolution sol)
    {
        if (sol.Routes.Count < 2) return;

        int r1 = _rng.Next(sol.Routes.Count);
        int r2 = _rng.Next(sol.Routes.Count);
        while (r2 == r1 && sol.Routes.Count > 1) r2 = _rng.Next(sol.Routes.Count);

        if (sol.Routes[r1].Count == 0 || sol.Routes[r2].Count == 0) return;

        int i = _rng.Next(sol.Routes[r1].Count);
        int j = _rng.Next(sol.Routes[r2].Count);

        int tmp = sol.Routes[r1][i];
        sol.Routes[r1][i] = sol.Routes[r2][j];
        sol.Routes[r2][j] = tmp;
    }
}
