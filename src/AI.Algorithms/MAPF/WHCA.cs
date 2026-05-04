using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Windowed Hierarchical Cooperative A* (WHCA*).
/// Планирует пути агентов последовательно в скользящем окне длиной w шагов,
/// используя таблицу резервирования для избежания конфликтов.
/// </summary>
[Serializable]
public class WHCA
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _windowSize;

    /// <summary>
    /// Создаёт решатель WHCA*.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="windowSize">Размер временного окна планирования.</param>
    public WHCA(GridMap map, List<MAPFAgent> agents, int windowSize = 16)
    {
        _map = map;
        _agents = agents;
        _windowSize = windowSize;
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

        var fullPaths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
            fullPaths.Add(new List<(int X, int Y)> { pos[i] });

        int maxIterations = (_map.Width + _map.Height) * 4;

        for (int round = 0; round < maxIterations; round++)
        {
            bool allDone = true;
            for (int i = 0; i < n; i++)
            {
                if (pos[i] != (_agents[i].GoalX, _agents[i].GoalY))
                { allDone = false; break; }
            }
            if (allDone) break;

            int globalT = round * _windowSize;
            var reservation = new HashSet<(int X, int Y, int T)>();

            var windowPaths = new List<(int X, int Y)>[n];

            for (int i = 0; i < n; i++)
            {
                windowPaths[i] = PlanWindow(i, pos[i], reservation, globalT);

                foreach (var (wp, idx) in windowPaths[i].Select((p, idx) => (p, idx)))
                    reservation.Add((wp.X, wp.Y, globalT + idx));
            }

            int windowLen = windowPaths.Max(w => w.Count);
            for (int t = 1; t < windowLen; t++)
            {
                for (int i = 0; i < n; i++)
                {
                    var wp = windowPaths[i];
                    pos[i] = t < wp.Count ? wp[t] : wp[^1];
                    fullPaths[i].Add(pos[i]);
                }
            }
        }

        return new MAPFSolution { Paths = fullPaths };
    }

    private List<(int X, int Y)> PlanWindow(int agentId, (int X, int Y) start,
        HashSet<(int X, int Y, int T)> reservation, int globalT)
    {
        var agent = _agents[agentId];
        int gx = agent.GoalX, gy = agent.GoalY;

        var open = new PriorityQueue<(int X, int Y, int T), int>();
        var closed = new HashSet<(int, int, int)>();
        var parent = new Dictionary<(int, int, int), (int, int, int)?>();

        var s0 = (start.X, start.Y, 0);
        open.Enqueue(s0, H(start.X, start.Y, gx, gy));
        parent[s0] = null;

        while (open.Count > 0)
        {
            var (x, y, t) = open.Dequeue();
            if (closed.Contains((x, y, t))) continue;
            closed.Add((x, y, t));

            if ((x == gx && y == gy) || t >= _windowSize)
                return Reconstruct((x, y, t), parent);

            foreach (var (nx, ny) in NbWait(x, y))
            {
                int nt = t + 1;
                if (reservation.Contains((nx, ny, globalT + nt))) continue;
                if (closed.Contains((nx, ny, nt))) continue;
                if (parent.ContainsKey((nx, ny, nt))) continue;

                parent[(nx, ny, nt)] = (x, y, t);
                open.Enqueue((nx, ny, nt), nt + H(nx, ny, gx, gy));
            }
        }

        return new List<(int X, int Y)> { start };
    }

    private List<(int X, int Y)> NbWait(int x, int y)
    {
        var r = new List<(int, int)> { (x, y) };
        r.AddRange(_map.Neighbors(x, y));
        return r;
    }

    private List<(int X, int Y)> Reconstruct((int, int, int) goal,
        Dictionary<(int, int, int), (int, int, int)?> parent)
    {
        var path = new List<(int X, int Y)>();
        (int, int, int)? c = goal;
        while (c != null) { path.Add((c.Value.Item1, c.Value.Item2)); c = parent[c.Value]; }
        path.Reverse();
        return path;
    }

    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);
}
