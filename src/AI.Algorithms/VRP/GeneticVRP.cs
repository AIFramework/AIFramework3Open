using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Генетический алгоритм для решения задачи VRP
/// </summary>
[Serializable]
public class GeneticVRP
{
    private readonly VRPInstance _inst;
    private readonly int _populationSize;
    private readonly int _generations;
    private readonly Random _rng;

    /// <summary>
    /// Создаёт экземпляр генетического алгоритма для VRP
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    /// <param name="populationSize">Размер популяции</param>
    /// <param name="generations">Число поколений</param>
    /// <param name="seed">Начальное значение генератора случайных чисел</param>
    public GeneticVRP(VRPInstance inst, int populationSize = 100, int generations = 500, int seed = 42)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
        _populationSize = populationSize;
        _generations = generations;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Решает задачу VRP генетическим алгоритмом
    /// </summary>
    /// <remarks>
    /// Возвращается лучшее допустимое решение из встреченных; элита каждого поколения — лучшая
    /// особь текущей популяции. Прежде индекс элиты не обновлялся после смены поколения, и в
    /// следующее переходила случайная особь, а постоянный штраф 10⁶ при больших координатах
    /// позволял недопустимому решению выиграть.
    /// </remarks>
    public VRPSolution Solve()
    {
        _penalty = VrpPenalty.Scale(_inst);

        var population = InitPopulation();
        var fitness = population.Select(p => Fitness(p)).ToArray();

        VRPSolution bestSol = null;
        double bestDistance = double.PositiveInfinity;
        Remember(population, ref bestSol, ref bestDistance);

        for (int gen = 0; gen < _generations; gen++)
        {
            var newPop = new List<VRPSolution>();
            newPop.Add(population[ArgMin(fitness)].Clone());

            while (newPop.Count < _populationSize)
            {
                var p1 = TournamentSelect(population, fitness);
                var p2 = TournamentSelect(population, fitness);
                var child = Crossover(p1, p2);
                Mutate(child);
                RepairSolution(child);
                newPop.Add(child);
            }

            population = newPop;
            fitness = population.Select(p => Fitness(p)).ToArray();
            Remember(population, ref bestSol, ref bestDistance);
        }

        return bestSol ?? population[ArgMin(fitness)].Clone();
    }

    private double _penalty = 1;

    private void Remember(List<VRPSolution> population, ref VRPSolution bestSol, ref double bestDistance)
    {
        foreach (var candidate in population)
        {
            if (!candidate.IsValid(_inst))
                continue;

            double distance = candidate.TotalDistance(_inst);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSol = candidate.Clone();
            }
        }
    }

    private static int ArgMin(double[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] < values[best]) best = i;
        return best;
    }

    private List<VRPSolution> InitPopulation()
    {
        var pop = new List<VRPSolution>();
        pop.Add(new ClarkeWright(_inst).Solve());
        pop.Add(new Sweep(_inst).Solve());

        while (pop.Count < _populationSize)
        {
            var perm = Enumerable.Range(0, _inst.N).ToList();
            Shuffle(perm);
            pop.Add(PermToSolution(perm));
        }
        return pop;
    }

    private VRPSolution PermToSolution(List<int> perm)
    {
        var sol = new VRPSolution();
        var route = new List<int>();
        double load = 0;

        foreach (int c in perm)
        {
            if (load + _inst.Demand[c] > _inst.VehicleCapacity && route.Count > 0)
            {
                sol.Routes.Add(route);
                route = new List<int>();
                load = 0;
            }
            route.Add(c);
            load += _inst.Demand[c];
        }
        if (route.Count > 0) sol.Routes.Add(route);
        return sol;
    }

    private double Fitness(VRPSolution sol) => VrpPenalty.Cost(_inst, sol, _penalty);

    private VRPSolution TournamentSelect(List<VRPSolution> pop, double[] fit)
    {
        int size = Math.Min(5, pop.Count);
        int best = _rng.Next(pop.Count);
        for (int i = 1; i < size; i++)
        {
            int idx = _rng.Next(pop.Count);
            if (fit[idx] < fit[best]) best = idx;
        }
        return pop[best];
    }

    private VRPSolution Crossover(VRPSolution p1, VRPSolution p2)
    {
        var perm1 = p1.Routes.SelectMany(r => r).ToList();
        var perm2 = p2.Routes.SelectMany(r => r).ToList();

        int n = _inst.N;
        int start = _rng.Next(n);
        int end = _rng.Next(n);
        if (start > end) { int t = start; start = end; end = t; }

        int[] child = new int[n];
        bool[] used = new bool[n];
        Array.Fill(child, -1);

        for (int i = start; i <= end; i++)
        {
            child[i] = perm1[i];
            used[perm1[i]] = true;
        }

        int pos = (end + 1) % n;
        for (int i = 0; i < n; i++)
        {
            int idx = (end + 1 + i) % n;
            int c = perm2[idx];
            if (!used[c])
            {
                child[pos] = c;
                used[c] = true;
                pos = (pos + 1) % n;
            }
        }

        return PermToSolution(child.ToList());
    }

    private void Mutate(VRPSolution sol)
    {
        if (_rng.NextDouble() > 0.3) return;

        var allCustomers = sol.Routes.SelectMany(r => r).ToList();
        if (allCustomers.Count < 2) return;

        int i = _rng.Next(allCustomers.Count);
        int j = _rng.Next(allCustomers.Count);

        int ci = allCustomers[i];
        int cj = allCustomers[j];

        foreach (var route in sol.Routes)
        {
            for (int k = 0; k < route.Count; k++)
            {
                if (route[k] == ci) route[k] = cj;
                else if (route[k] == cj) route[k] = ci;
            }
        }
    }

    private void RepairSolution(VRPSolution sol)
    {
        var visited = new HashSet<int>();
        var missing = new List<int>();

        foreach (var route in sol.Routes)
        {
            for (int i = route.Count - 1; i >= 0; i--)
            {
                if (!visited.Add(route[i]))
                    route.RemoveAt(i);
            }
        }

        for (int c = 0; c < _inst.N; c++)
            if (!visited.Contains(c)) missing.Add(c);

        foreach (int c in missing)
        {
            bool added = false;
            foreach (var route in sol.Routes)
            {
                double load = route.Sum(x => _inst.Demand[x]);
                if (load + _inst.Demand[c] <= _inst.VehicleCapacity)
                {
                    route.Add(c);
                    added = true;
                    break;
                }
            }
            if (!added) sol.Routes.Add(new List<int> { c });
        }

        sol.Routes.RemoveAll(r => r.Count == 0);
    }

    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
