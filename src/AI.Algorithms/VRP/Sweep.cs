using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Алгоритм заметания (Sweep) для решения VRP
/// </summary>
[Serializable]
public class Sweep
{
    private readonly VRPInstance _inst;

    /// <summary>
    /// Создаёт экземпляр алгоритма Sweep
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    public Sweep(VRPInstance inst)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
    }

    /// <summary>
    /// Решает задачу VRP алгоритмом заметания (кластеризация по углу)
    /// </summary>
    public VRPSolution Solve()
    {
        int n = _inst.N;
        double dx = _inst.DepotX[0];
        double dy = _inst.DepotY[0];

        var angles = new (double angle, int idx)[n];
        for (int i = 0; i < n; i++)
            angles[i] = (Math.Atan2(_inst.CustomerY[i] - dy, _inst.CustomerX[i] - dx), i);

        Array.Sort(angles, (a, b) => a.angle.CompareTo(b.angle));

        var sol = new VRPSolution();
        var current = new List<int>();
        double load = 0;

        foreach (var (_, idx) in angles)
        {
            double d = _inst.Demand[idx];
            if (load + d > _inst.VehicleCapacity && current.Count > 0)
            {
                sol.Routes.Add(current);
                current = new List<int>();
                load = 0;
            }
            current.Add(idx);
            load += d;
        }

        if (current.Count > 0)
            sol.Routes.Add(current);

        return sol;
    }
}
