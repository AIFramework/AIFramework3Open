using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Решение задачи маршрутизации транспортных средств
/// </summary>
[Serializable]
public class VRPSolution
{
    /// <summary>
    /// Маршруты (каждый маршрут — список индексов клиентов, без депо)
    /// </summary>
    public List<List<int>> Routes { get; set; } = new List<List<int>>();

    /// <summary>
    /// Вычисляет суммарное расстояние всех маршрутов (депо -> клиенты -> депо)
    /// </summary>
    public double TotalDistance(VRPInstance inst)
    {
        double total = 0;
        foreach (var route in Routes)
        {
            if (route.Count == 0) continue;
            total += inst.Distance(0, route[0] + 1);
            for (int i = 0; i < route.Count - 1; i++)
                total += inst.Distance(route[i] + 1, route[i + 1] + 1);
            total += inst.Distance(route[route.Count - 1] + 1, 0);
        }
        return total;
    }

    /// <summary>
    /// Проверяет допустимость решения (ограничение по грузоподъёмности и посещение всех клиентов ровно один раз)
    /// </summary>
    public bool IsValid(VRPInstance inst)
    {
        var visited = new HashSet<int>();
        foreach (var route in Routes)
        {
            double load = 0;
            foreach (int c in route)
            {
                if (c < 0 || c >= inst.N) return false;
                if (!visited.Add(c)) return false;
                load += inst.Demand[c];
            }
            if (load > inst.VehicleCapacity) return false;
        }
        return visited.Count == inst.N;
    }

    /// <summary>
    /// Создаёт глубокую копию решения
    /// </summary>
    public VRPSolution Clone()
    {
        var clone = new VRPSolution();
        foreach (var route in Routes)
            clone.Routes.Add(new List<int>(route));
        return clone;
    }
}
