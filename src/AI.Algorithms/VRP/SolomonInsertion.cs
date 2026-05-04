using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.VRP;

/// <summary>
/// Эвристика вставки Соломона (I1) для VRPTW.
/// При отсутствии временных окон работает как эвристика ближайшей вставки.
/// </summary>
[Serializable]
public class SolomonInsertion
{
    private readonly VRPInstance _inst;
    private readonly double[] _readyTime;
    private readonly double[] _dueDate;
    private readonly double[] _serviceTime;
    private readonly bool _hasTW;

    /// <summary>
    /// Создаёт экземпляр эвристики вставки Соломона
    /// </summary>
    /// <param name="inst">Экземпляр задачи VRP</param>
    /// <param name="readyTime">Начало временного окна (null — без ограничений)</param>
    /// <param name="dueDate">Конец временного окна (null — без ограничений)</param>
    /// <param name="serviceTime">Время обслуживания (null — нулевое)</param>
    public SolomonInsertion(VRPInstance inst, double[] readyTime = null,
        double[] dueDate = null, double[] serviceTime = null)
    {
        _inst = inst ?? throw new ArgumentNullException(nameof(inst));
        _readyTime = readyTime;
        _dueDate = dueDate;
        _serviceTime = serviceTime;
        _hasTW = readyTime != null && dueDate != null;
    }

    /// <summary>
    /// Решает задачу методом вставки Соломона
    /// </summary>
    public VRPSolution Solve()
    {
        int n = _inst.N;
        bool[] inserted = new bool[n];
        var sol = new VRPSolution();
        int remaining = n;

        while (remaining > 0)
        {
            int seed = -1;
            double maxDist = -1;
            for (int i = 0; i < n; i++)
            {
                if (inserted[i]) continue;
                double d = _inst.Distance(0, i + 1);
                if (d > maxDist) { maxDist = d; seed = i; }
            }

            if (seed < 0) break;

            var route = new List<int> { seed };
            inserted[seed] = true;
            remaining--;
            double load = _inst.Demand[seed];

            bool improved = true;
            while (improved)
            {
                improved = false;
                int bestCust = -1;
                int bestPos = -1;
                double bestCost = double.MaxValue;

                for (int c = 0; c < n; c++)
                {
                    if (inserted[c]) continue;
                    if (load + _inst.Demand[c] > _inst.VehicleCapacity) continue;

                    for (int p = 0; p <= route.Count; p++)
                    {
                        double cost = InsertionCost(route, c, p);

                        if (_hasTW && !IsFeasibleInsertion(route, c, p))
                            continue;

                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestCust = c;
                            bestPos = p;
                        }
                    }
                }

                if (bestCust >= 0)
                {
                    route.Insert(bestPos, bestCust);
                    inserted[bestCust] = true;
                    remaining--;
                    load += _inst.Demand[bestCust];
                    improved = true;
                }
            }

            sol.Routes.Add(route);
        }

        return sol;
    }

    private double InsertionCost(List<int> route, int customer, int position)
    {
        int prev = (position == 0) ? 0 : route[position - 1] + 1;
        int next = (position == route.Count) ? 0 : route[position] + 1;
        int cNode = customer + 1;

        return _inst.Distance(prev, cNode) + _inst.Distance(cNode, next) - _inst.Distance(prev, next);
    }

    private bool IsFeasibleInsertion(List<int> route, int customer, int position)
    {
        var testRoute = new List<int>(route);
        testRoute.Insert(position, customer);

        double time = 0;
        int prev = 0;
        for (int i = 0; i < testRoute.Count; i++)
        {
            int c = testRoute[i];
            time += _inst.Distance(prev, c + 1);

            double ready = _readyTime != null && c < _readyTime.Length ? _readyTime[c] : 0;
            double due = _dueDate != null && c < _dueDate.Length ? _dueDate[c] : double.MaxValue;
            double svc = _serviceTime != null && c < _serviceTime.Length ? _serviceTime[c] : 0;

            if (time < ready) time = ready;
            if (time > due) return false;
            time += svc;
            prev = c + 1;
        }
        return true;
    }
}
