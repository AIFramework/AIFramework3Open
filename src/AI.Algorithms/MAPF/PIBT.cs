using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Priority Inheritance with Backtracking (PIBT).
/// На каждом временном шаге агенты по приоритету выбирают ход;
/// при блокировке нижестоящий агент наследует приоритет и отходит.
/// </summary>
[Serializable]
public class PIBT
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _maxTimesteps;

    /// <summary>
    /// Создаёт решатель PIBT.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="maxTimesteps">Максимальное число шагов симуляции.</param>
    public PIBT(GridMap map, List<MAPFAgent> agents, int maxTimesteps = 200)
    {
        _map = map;
        _agents = agents;
        _maxTimesteps = maxTimesteps;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        int n = _agents.Count;
        var pos = new (int X, int Y)[n];
        for (int i = 0; i < n; i++)
            pos[i] = (_agents[i].StartX, _agents[i].StartY);

        var paths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
            paths.Add(new List<(int X, int Y)> { pos[i] });

        var priority = new double[n];
        for (int i = 0; i < n; i++)
            priority[i] = n - i;

        for (int t = 0; t < _maxTimesteps; t++)
        {
            bool allDone = true;
            for (int i = 0; i < n; i++)
            {
                if (pos[i] != (_agents[i].GoalX, _agents[i].GoalY))
                { allDone = false; break; }
            }
            if (allDone) break;

            var next = new (int X, int Y)[n];
            Array.Copy(pos, next, n);
            var decided = new bool[n];

            var order = Enumerable.Range(0, n)
                .OrderByDescending(i => priority[i])
                .ToArray();

            foreach (int i in order)
            {
                if (!decided[i])
                    PibtStep(i, pos, next, decided);
            }

            for (int i = 0; i < n; i++)
            {
                pos[i] = next[i];
                paths[i].Add(pos[i]);

                if (pos[i] == (_agents[i].GoalX, _agents[i].GoalY))
                    priority[i] = 0;
                else
                    priority[i] += 1;
            }
        }

        return new MAPFSolution { Paths = paths };
    }

    private bool PibtStep(int agent, (int X, int Y)[] cur, (int X, int Y)[] next, bool[] decided)
    {
        decided[agent] = true;
        var (x, y) = cur[agent];
        int gx = _agents[agent].GoalX, gy = _agents[agent].GoalY;

        var candidates = new List<(int X, int Y)>(_map.Neighbors(x, y));
        candidates.Add((x, y));
        candidates.Sort((a, b) =>
            H(a.X, a.Y, gx, gy).CompareTo(H(b.X, b.Y, gx, gy)));

        foreach (var (nx, ny) in candidates)
        {
            bool takenByDecided = false;
            for (int j = 0; j < _agents.Count; j++)
            {
                if (j != agent && decided[j] && next[j] == (nx, ny))
                { takenByDecided = true; break; }
            }
            if (takenByDecided) continue;

            int occupant = -1;
            for (int j = 0; j < _agents.Count; j++)
            {
                if (j != agent && !decided[j] && cur[j] == (nx, ny))
                { occupant = j; break; }
            }

            if (occupant >= 0)
            {
                next[agent] = (nx, ny);
                if (PibtStep(occupant, cur, next, decided))
                {
                    bool swapConflict = false;
                    if (next[occupant] == cur[agent]) swapConflict = true;
                    if (!swapConflict) return true;
                }
                next[agent] = cur[agent];
                decided[occupant] = false;
            }
            else
            {
                next[agent] = (nx, ny);
                return true;
            }
        }

        next[agent] = cur[agent];
        return false;
    }

    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);
}
