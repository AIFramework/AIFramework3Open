using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Муравьиная оптимизация (ACO) для решения задачи VRP
/// </summary>
[Serializable]
public class AntColony
{
    private readonly VRPInstance _inst;
    private readonly int _numAnts;
    private readonly int _maxIterations;
    private readonly double _alpha;
    private readonly double _beta;
    private readonly double _evaporation;
    private readonly Random _rng;

    /// <summary>
    /// Создаёт экземпляр муравьиной оптимизации для VRP
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    /// <param name="numAnts">Число муравьёв</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    /// <param name="alpha">Вес феромона</param>
    /// <param name="beta">Вес эвристической информации</param>
    /// <param name="evaporation">Коэффициент испарения</param>
    /// <param name="seed">Начальное значение генератора случайных чисел</param>
    public AntColony(VRPInstance inst, int numAnts = 30, int maxIterations = 200,
        double alpha = 1, double beta = 3, double evaporation = 0.1, int seed = 42)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
        _numAnts = numAnts;
        _maxIterations = maxIterations;
        _alpha = alpha;
        _beta = beta;
        _evaporation = evaporation;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Решает задачу VRP методом муравьиной оптимизации
    /// </summary>
    public VRPSolution Solve()
    {
        int total = _inst.TotalNodes;
        double[,] pheromone = new double[total, total];
        double tau0 = 1.0 / (_inst.N * NearestNeighborDist());

        for (int i = 0; i < total; i++)
            for (int j = 0; j < total; j++)
                pheromone[i, j] = tau0;

        VRPSolution bestSol = null;
        double bestCost = double.MaxValue;

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            var solutions = new List<VRPSolution>();
            var costs = new List<double>();

            for (int ant = 0; ant < _numAnts; ant++)
            {
                var sol = ConstructSolution(pheromone);
                double cost = sol.TotalDistance(_inst);
                solutions.Add(sol);
                costs.Add(cost);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSol = sol.Clone();
                }
            }

            for (int i = 0; i < total; i++)
                for (int j = 0; j < total; j++)
                    pheromone[i, j] *= (1 - _evaporation);

            for (int ant = 0; ant < _numAnts; ant++)
            {
                double deposit = 1.0 / costs[ant];
                foreach (var route in solutions[ant].Routes)
                {
                    int prev = 0;
                    foreach (int c in route)
                    {
                        pheromone[prev, c + 1] += deposit;
                        pheromone[c + 1, prev] += deposit;
                        prev = c + 1;
                    }
                    pheromone[prev, 0] += deposit;
                    pheromone[0, prev] += deposit;
                }
            }
        }

        return bestSol ?? new VRPSolution();
    }

    private VRPSolution ConstructSolution(double[,] pheromone)
    {
        bool[] visited = new bool[_inst.N];
        var sol = new VRPSolution();
        int remaining = _inst.N;

        while (remaining > 0)
        {
            var route = new List<int>();
            double load = 0;
            int current = 0;

            while (remaining > 0)
            {
                int next = SelectNext(current, visited, load, pheromone);
                if (next < 0) break;

                route.Add(next);
                visited[next] = true;
                load += _inst.Demand[next];
                current = next + 1;
                remaining--;
            }

            if (route.Count > 0)
                sol.Routes.Add(route);
        }

        return sol;
    }

    private int SelectNext(int currentNode, bool[] visited, double load, double[,] pheromone)
    {
        double totalProb = 0;
        var probs = new double[_inst.N];

        for (int c = 0; c < _inst.N; c++)
        {
            if (visited[c]) continue;
            if (load + _inst.Demand[c] > _inst.VehicleCapacity) continue;

            double dist = _inst.Distance(currentNode, c + 1);
            double eta = (dist > 1e-10) ? 1.0 / dist : 1e10;
            double tau = pheromone[currentNode, c + 1];

            probs[c] = Math.Pow(tau, _alpha) * Math.Pow(eta, _beta);
            totalProb += probs[c];
        }

        if (totalProb <= 0) return -1;

        double r = _rng.NextDouble() * totalProb;
        double cum = 0;
        for (int c = 0; c < _inst.N; c++)
        {
            if (probs[c] <= 0) continue;
            cum += probs[c];
            if (r <= cum) return c;
        }
        return -1;
    }

    private double NearestNeighborDist()
    {
        bool[] visited = new bool[_inst.N];
        double total = 0;
        int current = 0;

        for (int step = 0; step < _inst.N; step++)
        {
            double minDist = double.MaxValue;
            int nearest = -1;
            for (int c = 0; c < _inst.N; c++)
            {
                if (visited[c]) continue;
                double d = _inst.Distance(current, c + 1);
                if (d < minDist) { minDist = d; nearest = c; }
            }
            if (nearest < 0) break;
            visited[nearest] = true;
            total += minDist;
            current = nearest + 1;
        }
        total += _inst.Distance(current, 0);
        return Math.Max(total, 1);
    }
}
