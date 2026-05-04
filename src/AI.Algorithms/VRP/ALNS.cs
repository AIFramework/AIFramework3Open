using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Адаптивный поиск с разрушением и восстановлением (ALNS) для VRP
/// </summary>
[Serializable]
public class ALNS
{
    private readonly VRPInstance _inst;
    private readonly int _maxIterations;
    private readonly Random _rng;

    /// <summary>
    /// Создаёт экземпляр ALNS
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    /// <param name="seed">Начальное значение генератора случайных чисел</param>
    public ALNS(VRPInstance inst, int maxIterations = 5000, int seed = 42)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
        _maxIterations = maxIterations;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Решает задачу VRP методом ALNS
    /// </summary>
    /// <param name="initial">Начальное решение (если null — строится автоматически)</param>
    public VRPSolution Solve(VRPSolution initial = null)
    {
        var current = initial?.Clone() ?? new ClarkeWright(_inst).Solve();
        double currentCost = current.TotalDistance(_inst);

        var best = current.Clone();
        double bestCost = currentCost;

        double[] destroyWeights = { 1, 1, 1 };
        double[] repairWeights = { 1, 1 };
        double[] destroyScores = { 0, 0, 0 };
        double[] repairScores = { 0, 0 };
        int[] destroyCount = { 0, 0, 0 };
        int[] repairCount = { 0, 0 };

        double temp = currentCost * 0.05;
        double cooling = 0.9999;

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            int dIdx = RouletteSelect(destroyWeights);
            int rIdx = RouletteSelect(repairWeights);
            destroyCount[dIdx]++;
            repairCount[rIdx]++;

            var candidate = current.Clone();
            var removed = Destroy(candidate, dIdx);
            Repair(candidate, removed, rIdx);

            double candCost = candidate.TotalDistance(_inst);
            double delta = candCost - currentCost;

            bool accepted = delta < 0 || _rng.NextDouble() < Math.Exp(-delta / Math.Max(temp, 1e-10));

            if (accepted)
            {
                current = candidate;
                currentCost = candCost;
                destroyScores[dIdx] += (delta < 0) ? 3 : 1;
                repairScores[rIdx] += (delta < 0) ? 3 : 1;
            }

            if (currentCost < bestCost)
            {
                best = current.Clone();
                bestCost = currentCost;
                destroyScores[dIdx] += 5;
                repairScores[rIdx] += 5;
            }

            temp *= cooling;

            if ((iter + 1) % 100 == 0)
            {
                UpdateWeights(destroyWeights, destroyScores, destroyCount);
                UpdateWeights(repairWeights, repairScores, repairCount);
            }
        }

        return best;
    }

    private List<int> Destroy(VRPSolution sol, int method)
    {
        int toRemove = Math.Max(1, (int)(_inst.N * (0.1 + _rng.NextDouble() * 0.3)));
        var removed = new List<int>();

        switch (method)
        {
            case 0: removed = RandomRemoval(sol, toRemove); break;
            case 1: removed = WorstRemoval(sol, toRemove); break;
            case 2: removed = ShawRemoval(sol, toRemove); break;
        }
        return removed;
    }

    private void Repair(VRPSolution sol, List<int> removed, int method)
    {
        switch (method)
        {
            case 0: GreedyInsert(sol, removed); break;
            case 1: RegretInsert(sol, removed); break;
        }
    }

    private List<int> RandomRemoval(VRPSolution sol, int count)
    {
        var all = sol.Routes.SelectMany(r => r).ToList();
        var removed = new List<int>();

        while (removed.Count < count && all.Count > 0)
        {
            int idx = _rng.Next(all.Count);
            int c = all[idx];
            all.RemoveAt(idx);
            removed.Add(c);
            foreach (var route in sol.Routes) route.Remove(c);
        }
        sol.Routes.RemoveAll(r => r.Count == 0);
        return removed;
    }

    private List<int> WorstRemoval(VRPSolution sol, int count)
    {
        var removed = new List<int>();

        for (int iter = 0; iter < count; iter++)
        {
            int worstC = -1;
            double worstCost = double.MinValue;

            foreach (var route in sol.Routes)
            {
                for (int i = 0; i < route.Count; i++)
                {
                    int c = route[i];
                    int prev = (i == 0) ? 0 : route[i - 1] + 1;
                    int next = (i == route.Count - 1) ? 0 : route[i + 1] + 1;
                    double cost = _inst.Distance(prev, c + 1) + _inst.Distance(c + 1, next) - _inst.Distance(prev, next);

                    if (cost > worstCost) { worstCost = cost; worstC = c; }
                }
            }

            if (worstC < 0) break;
            removed.Add(worstC);
            foreach (var route in sol.Routes) route.Remove(worstC);
        }
        sol.Routes.RemoveAll(r => r.Count == 0);
        return removed;
    }

    private List<int> ShawRemoval(VRPSolution sol, int count)
    {
        var all = sol.Routes.SelectMany(r => r).ToList();
        if (all.Count == 0) return new List<int>();

        int seed = all[_rng.Next(all.Count)];
        var removed = new List<int> { seed };
        foreach (var route in sol.Routes) route.Remove(seed);

        while (removed.Count < count)
        {
            var remaining = sol.Routes.SelectMany(r => r).ToList();
            if (remaining.Count == 0) break;

            int last = removed[removed.Count - 1];
            remaining.Sort((a, b) => _inst.Distance(a + 1, last + 1).CompareTo(_inst.Distance(b + 1, last + 1)));

            int pickIdx = (int)(Math.Pow(_rng.NextDouble(), 2) * remaining.Count);
            pickIdx = Math.Min(pickIdx, remaining.Count - 1);
            int c = remaining[pickIdx];
            removed.Add(c);
            foreach (var route in sol.Routes) route.Remove(c);
        }
        sol.Routes.RemoveAll(r => r.Count == 0);
        return removed;
    }

    private void GreedyInsert(VRPSolution sol, List<int> customers)
    {
        foreach (int c in customers)
        {
            double bestCost = double.MaxValue;
            int bestRoute = -1, bestPos = -1;

            for (int r = 0; r < sol.Routes.Count; r++)
            {
                double load = sol.Routes[r].Sum(x => _inst.Demand[x]);
                if (load + _inst.Demand[c] > _inst.VehicleCapacity) continue;

                for (int p = 0; p <= sol.Routes[r].Count; p++)
                {
                    double cost = InsertCost(sol.Routes[r], c, p);
                    if (cost < bestCost) { bestCost = cost; bestRoute = r; bestPos = p; }
                }
            }

            if (bestRoute >= 0)
            {
                sol.Routes[bestRoute].Insert(bestPos, c);
            }
            else
            {
                sol.Routes.Add(new List<int> { c });
            }
        }
    }

    private void RegretInsert(VRPSolution sol, List<int> customers)
    {
        var toInsert = new List<int>(customers);

        while (toInsert.Count > 0)
        {
            int bestC = -1;
            double bestRegret = double.MinValue;
            int bestR = -1, bestP = -1;

            foreach (int c in toInsert)
            {
                var costs = new List<(double cost, int r, int p)>();

                for (int r = 0; r < sol.Routes.Count; r++)
                {
                    double load = sol.Routes[r].Sum(x => _inst.Demand[x]);
                    if (load + _inst.Demand[c] > _inst.VehicleCapacity) continue;

                    for (int p = 0; p <= sol.Routes[r].Count; p++)
                        costs.Add((InsertCost(sol.Routes[r], c, p), r, p));
                }

                if (costs.Count == 0)
                {
                    costs.Add((0, -1, 0));
                }

                costs.Sort((a, b) => a.cost.CompareTo(b.cost));
                double regret = costs.Count >= 2 ? costs[1].cost - costs[0].cost : costs[0].cost;

                if (regret > bestRegret || bestC < 0)
                {
                    bestRegret = regret;
                    bestC = c;
                    bestR = costs[0].r;
                    bestP = costs[0].p;
                }
            }

            if (bestC < 0) break;
            toInsert.Remove(bestC);

            if (bestR >= 0)
                sol.Routes[bestR].Insert(bestP, bestC);
            else
                sol.Routes.Add(new List<int> { bestC });
        }
    }

    private double InsertCost(List<int> route, int customer, int pos)
    {
        int prev = (pos == 0) ? 0 : route[pos - 1] + 1;
        int next = (pos == route.Count) ? 0 : route[pos] + 1;
        return _inst.Distance(prev, customer + 1) + _inst.Distance(customer + 1, next) - _inst.Distance(prev, next);
    }

    private int RouletteSelect(double[] weights)
    {
        double total = weights.Sum();
        double r = _rng.NextDouble() * total;
        double cum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cum += weights[i];
            if (r <= cum) return i;
        }
        return weights.Length - 1;
    }

    private void UpdateWeights(double[] weights, double[] scores, int[] counts)
    {
        double decay = 0.8;
        for (int i = 0; i < weights.Length; i++)
        {
            double s = counts[i] > 0 ? scores[i] / counts[i] : 0;
            weights[i] = weights[i] * decay + (1 - decay) * s;
            weights[i] = Math.Max(weights[i], 0.1);
            scores[i] = 0;
            counts[i] = 0;
        }
    }
}
