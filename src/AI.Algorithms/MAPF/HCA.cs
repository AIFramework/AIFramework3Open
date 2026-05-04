using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Hierarchical Cooperative A* (HCA*).
/// Планирует пути агентов последовательно на всём горизонте:
/// каждый следующий агент учитывает пути всех ранее спланированных
/// через таблицу резервирования.
/// </summary>
[Serializable]
public class HCA
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;

    /// <summary>
    /// Создаёт решатель HCA*.
    /// </summary>
    public HCA(GridMap map, List<MAPFAgent> agents)
    {
        _map = map;
        _agents = agents;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        int n = _agents.Count;
        int maxT = (_map.Width + _map.Height) * 2 + n;
        var reservation = new HashSet<(int X, int Y, int T)>();
        var paths = new List<List<(int X, int Y)>>(n);

        var order = Enumerable.Range(0, n).ToList();
        order.Sort((a, b) =>
        {
            int da = H(_agents[a].StartX, _agents[a].StartY, _agents[a].GoalX, _agents[a].GoalY);
            int db = H(_agents[b].StartX, _agents[b].StartY, _agents[b].GoalX, _agents[b].GoalY);
            return db.CompareTo(da);
        });

        var orderedPaths = new List<(int X, int Y)>[n];

        foreach (int i in order)
        {
            var path = PlanSingle(i, reservation, maxT);
            orderedPaths[i] = path;

            for (int t = 0; t < path.Count; t++)
                reservation.Add((path[t].X, path[t].Y, t));

            var last = path[^1];
            for (int t = path.Count; t <= maxT + 10; t++)
                reservation.Add((last.X, last.Y, t));
        }

        int maxLen = orderedPaths.Max(p => p.Count);
        for (int i = 0; i < n; i++)
        {
            var p = orderedPaths[i];
            while (p.Count < maxLen)
                p.Add(p[^1]);
            paths.Add(p);
        }

        return new MAPFSolution { Paths = paths };
    }

    private List<(int X, int Y)> PlanSingle(int agentId,
        HashSet<(int X, int Y, int T)> reservation, int maxT)
    {
        var agent = _agents[agentId];
        int gx = agent.GoalX, gy = agent.GoalY;
        int sx = agent.StartX, sy = agent.StartY;

        var open = new PriorityQueue<(int X, int Y, int T), int>();
        var closed = new HashSet<(int, int, int)>();
        var parent = new Dictionary<(int, int, int), (int, int, int)?>();

        if (reservation.Contains((sx, sy, 0)))
            return new List<(int X, int Y)> { (sx, sy) };

        var s0 = (sx, sy, 0);
        open.Enqueue(s0, H(sx, sy, gx, gy));
        parent[s0] = null;

        while (open.Count > 0)
        {
            var (x, y, t) = open.Dequeue();
            if (closed.Contains((x, y, t))) continue;
            closed.Add((x, y, t));

            if (x == gx && y == gy)
                return Reconstruct((x, y, t), parent);

            if (t >= maxT) continue;

            foreach (var (nx, ny) in NbWait(x, y))
            {
                int nt = t + 1;
                if (reservation.Contains((nx, ny, nt))) continue;
                if (closed.Contains((nx, ny, nt))) continue;
                if (parent.ContainsKey((nx, ny, nt))) continue;

                parent[(nx, ny, nt)] = (x, y, t);
                open.Enqueue((nx, ny, nt), nt + H(nx, ny, gx, gy));
            }
        }

        return new List<(int X, int Y)> { (sx, sy) };
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
