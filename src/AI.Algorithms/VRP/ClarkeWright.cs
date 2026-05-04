using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Алгоритм Кларка-Райта (метод сбережений) для решения VRP
/// </summary>
[Serializable]
public class ClarkeWright
{
    private readonly VRPInstance _inst;

    /// <summary>
    /// Создаёт экземпляр алгоритма Кларка-Райта
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    public ClarkeWright(VRPInstance inst)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
    }

    /// <summary>
    /// Решает задачу VRP методом сбережений
    /// </summary>
    public VRPSolution Solve()
    {
        int n = _inst.N;

        var savings = new List<(double saving, int i, int j)>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double s = _inst.Distance(0, i + 1) + _inst.Distance(0, j + 1) - _inst.Distance(i + 1, j + 1);
                savings.Add((s, i, j));
            }
        savings.Sort((a, b) => b.saving.CompareTo(a.saving));

        int[] routeOf = new int[n];
        var routes = new List<List<int>>();
        double[] routeLoad = new double[n];

        for (int i = 0; i < n; i++)
        {
            routes.Add(new List<int> { i });
            routeOf[i] = i;
            routeLoad[i] = _inst.Demand[i];
        }

        foreach (var (saving, ci, cj) in savings)
        {
            if (saving <= 0) break;

            int ri = routeOf[ci];
            int rj = routeOf[cj];
            if (ri == rj) continue;

            var routeI = routes[ri];
            var routeJ = routes[rj];

            if (routeI == null || routeJ == null) continue;

            bool iAtEnd = routeI[routeI.Count - 1] == ci;
            bool jAtStart = routeJ[0] == cj;
            bool iAtStart = routeI[0] == ci;
            bool jAtEnd = routeJ[routeJ.Count - 1] == cj;

            List<int> merged = null;

            if (iAtEnd && jAtStart)
            {
                merged = new List<int>(routeI);
                merged.AddRange(routeJ);
            }
            else if (jAtEnd && iAtStart)
            {
                merged = new List<int>(routeJ);
                merged.AddRange(routeI);
            }
            else if (iAtEnd && jAtEnd)
            {
                routeJ.Reverse();
                merged = new List<int>(routeI);
                merged.AddRange(routeJ);
            }
            else if (iAtStart && jAtStart)
            {
                routeI.Reverse();
                merged = new List<int>(routeI);
                merged.AddRange(routeJ);
            }

            if (merged == null) continue;

            double newLoad = routeLoad[ri] + routeLoad[rj];
            if (newLoad > _inst.VehicleCapacity) continue;

            routes[ri] = merged;
            routes[rj] = null;
            routeLoad[ri] = newLoad;

            foreach (int c in merged)
                routeOf[c] = ri;
        }

        var sol = new VRPSolution();
        foreach (var r in routes)
        {
            if (r != null && r.Count > 0)
                sol.Routes.Add(r);
        }

        SplitIfNeeded(sol);
        return sol;
    }

    private void SplitIfNeeded(VRPSolution sol)
    {
        if (_inst.NumVehicles <= 0) return;

        while (sol.Routes.Count > _inst.NumVehicles)
        {
            int maxIdx = 0;
            for (int i = 1; i < sol.Routes.Count; i++)
                if (sol.Routes[i].Count > sol.Routes[maxIdx].Count)
                    maxIdx = i;

            var big = sol.Routes[maxIdx];
            if (big.Count <= 1) break;

            int mid = big.Count / 2;
            var r1 = big.GetRange(0, mid);
            var r2 = big.GetRange(mid, big.Count - mid);
            sol.Routes[maxIdx] = r1;
            sol.Routes.Add(r2);
        }
    }
}
