using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Табу-поиск для задачи маршрутизации транспортных средств
/// </summary>
[Serializable]
public class TabuSearchVRP
{
    private readonly VRPInstance _inst;
    private readonly int _maxIterations;
    private readonly int _tabuTenure;
    private readonly Random _rng;

    /// <summary>
    /// Создаёт экземпляр табу-поиска для VRP
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    /// <param name="tabuTenure">Длительность запрета (число итераций)</param>
    /// <param name="seed">Начальное значение генератора случайных чисел</param>
    public TabuSearchVRP(VRPInstance inst, int maxIterations = 3000, int tabuTenure = 15, int seed = 42)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
        _maxIterations = maxIterations;
        _tabuTenure = tabuTenure;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Решает задачу VRP методом табу-поиска
    /// </summary>
    /// <param name="initial">Начальное решение (если null — строится автоматически)</param>
    public VRPSolution Solve(VRPSolution initial = null)
    {
        var current = initial?.Clone() ?? new ClarkeWright(_inst).Solve();
        double currentCost = current.TotalDistance(_inst);

        var best = current.Clone();
        double bestCost = currentCost;

        var tabuList = new Dictionary<long, int>();

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            VRPSolution bestNeighbor = null;
            double bestNeighborCost = double.MaxValue;
            long bestMoveKey = 0;

            var moves = GenerateMoves(current);

            foreach (var (candidate, moveKey) in moves)
            {
                double cost = candidate.TotalDistance(_inst);

                bool isTabu = tabuList.ContainsKey(moveKey) && tabuList[moveKey] > iter;
                bool aspiration = cost < bestCost;

                if ((!isTabu || aspiration) && cost < bestNeighborCost)
                {
                    bestNeighborCost = cost;
                    bestNeighbor = candidate;
                    bestMoveKey = moveKey;
                }
            }

            if (bestNeighbor == null) break;

            current = bestNeighbor;
            currentCost = bestNeighborCost;
            tabuList[bestMoveKey] = iter + _tabuTenure;

            if (currentCost < bestCost)
            {
                bestCost = currentCost;
                best = current.Clone();
            }
        }

        return best;
    }

    // Окрестность: перемещения клиента в другой маршрут (не больше 50, выбранных случайно
    // с заданным зерном) и все обмены соседей внутри маршрутов. Прежде перемещения брались
    // всегда в одном порядке и обрывались на 51-м: клиенты поздних маршрутов не двигались вовсе,
    // а до обменов дело не доходило. Запрет — на повторное перемещение того же клиента
    private const int RelocateLimit = 50;

    private List<(VRPSolution, long)> GenerateMoves(VRPSolution sol)
    {
        var moves = new List<(VRPSolution, long)>();
        var relocations = new List<(int Route, int Index, int Target)>();

        for (int r = 0; r < sol.Routes.Count; r++)
            for (int i = 0; i < sol.Routes[r].Count; i++)
                for (int r2 = 0; r2 < sol.Routes.Count; r2++)
                    if (r2 != r)
                        relocations.Add((r, i, r2));

        Shuffle(relocations);

        foreach (var (r, i, r2) in relocations)
        {
            if (moves.Count >= RelocateLimit) break;

            int customer = sol.Routes[r][i];
            double load = sol.Routes[r2].Sum(c => _inst.Demand[c]);
            if (load + _inst.Demand[customer] > _inst.VehicleCapacity) continue;

            var candidate = sol.Clone();
            candidate.Routes[r].RemoveAt(i);

            int bestPos = 0;
            double bestInsertCost = double.MaxValue;
            for (int p = 0; p <= candidate.Routes[r2].Count; p++)
            {
                candidate.Routes[r2].Insert(p, customer);
                double c = candidate.TotalDistance(_inst);
                if (c < bestInsertCost) { bestInsertCost = c; bestPos = p; }
                candidate.Routes[r2].RemoveAt(p);
            }

            candidate.Routes[r2].Insert(bestPos, customer);
            candidate.Routes.RemoveAll(rt => rt.Count == 0);

            moves.Add((candidate, customer));
        }

        for (int r = 0; r < sol.Routes.Count; r++)
        {
            var route = sol.Routes[r];
            if (route.Count < 2) continue;

            for (int i = 0; i < route.Count - 1; i++)
            {
                var candidate = sol.Clone();
                var cr = candidate.Routes[r];
                int tmp = cr[i]; cr[i] = cr[i + 1]; cr[i + 1] = tmp;
                long key = -(Math.Min(route[i], route[i + 1]) * 10000L + Math.Max(route[i], route[i + 1]) + 1);
                moves.Add((candidate, key));
            }
        }

        return moves;
    }

    private void Shuffle<TItem>(List<TItem> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
