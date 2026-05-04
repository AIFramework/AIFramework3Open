using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Контейнер решения задачи многоагентного поиска пути.
/// Хранит пути всех агентов и предоставляет метрики.
/// </summary>
[Serializable]
public class MAPFSolution
{
    /// <summary>Пути агентов: для каждого агента — список позиций по временным шагам.</summary>
    public List<List<(int X, int Y)>> Paths { get; set; } = new List<List<(int X, int Y)>>();

    /// <summary>Максимальная длина пути среди всех агентов (мейкспен).</summary>
    public int Makespan => Paths.Count == 0 ? 0 : Paths.Max(p => p.Count);

    /// <summary>Сумма длин всех путей (SoC).</summary>
    public int SumOfCosts => Paths.Sum(p => p.Count);

    /// <summary>
    /// Проверяет корректность решения: все пути допустимы, агенты достигают
    /// целей, нет столкновений (вершинных и рёберных).
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    public bool IsValid(GridMap map, List<MAPFAgent> agents)
    {
        if (Paths.Count != agents.Count) return false;

        for (int i = 0; i < agents.Count; i++)
        {
            var path = Paths[i];
            if (path.Count == 0) return false;

            if (path[0].X != agents[i].StartX || path[0].Y != agents[i].StartY)
                return false;

            var last = path[^1];
            if (last.X != agents[i].GoalX || last.Y != agents[i].GoalY)
                return false;

            for (int t = 0; t < path.Count; t++)
            {
                var (x, y) = path[t];
                if (!map.InBounds(x, y) || map.IsBlocked(x, y))
                    return false;

                if (t > 0)
                {
                    int dx = Math.Abs(x - path[t - 1].X);
                    int dy = Math.Abs(y - path[t - 1].Y);
                    if (dx + dy > 1) return false;
                }
            }
        }

        int maxT = Makespan;
        for (int t = 0; t < maxT; t++)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                var pi = Pos(i, t);
                for (int j = i + 1; j < agents.Count; j++)
                {
                    var pj = Pos(j, t);
                    if (pi == pj) return false;

                    if (t > 0)
                    {
                        var piPrev = Pos(i, t - 1);
                        var pjPrev = Pos(j, t - 1);
                        if (pi == pjPrev && pj == piPrev) return false;
                    }
                }
            }
        }

        return true;
    }

    private (int X, int Y) Pos(int agent, int t)
    {
        var p = Paths[agent];
        return t < p.Count ? p[t] : p[^1];
    }
}
