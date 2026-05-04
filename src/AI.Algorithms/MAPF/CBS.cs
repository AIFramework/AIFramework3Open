using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Conflict-Based Search (CBS) — оптимальный алгоритм многоагентного поиска пути.
/// Верхний уровень строит дерево ограничений (constraint tree),
/// нижний уровень использует A* в пространстве-времени.
/// </summary>
[Serializable]
public class CBS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель CBS.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="timeLimit">Лимит по времени в миллисекундах.</param>
    public CBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)
    {
        _map = map;
        _agents = agents;
        _timeLimit = timeLimit;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        var sw = Stopwatch.StartNew();
        int n = _agents.Count;
        int maxT = (_map.Width + _map.Height) * 2 + n;

        var root = new CTNode
        {
            Constraints = new HashSet<(int, int, int, int)>(),
            Paths = new List<List<(int X, int Y)>>()
        };

        for (int i = 0; i < n; i++)
            root.Paths.Add(LowLevelSearch(i, root.Constraints, maxT));
        root.Cost = root.Paths.Sum(p => p.Count);

        var open = new PriorityQueue<CTNode, int>();
        open.Enqueue(root, root.Cost);

        while (open.Count > 0 && sw.ElapsedMilliseconds < _timeLimit)
        {
            var node = open.Dequeue();

            var conflict = FindFirstConflict(node.Paths);
            if (conflict == null)
                return new MAPFSolution { Paths = node.Paths };

            var c = conflict.Value;
            Branch(node, c.A1, c.X1, c.Y1, c.T, open, maxT);
            Branch(node, c.A2, c.X2, c.Y2, c.T, open, maxT);
        }

        return Fallback(maxT);
    }

    private void Branch(CTNode parent, int agent, int cx, int cy, int ct,
        PriorityQueue<CTNode, int> open, int maxT)
    {
        var child = new CTNode
        {
            Constraints = new HashSet<(int, int, int, int)>(parent.Constraints),
            Paths = parent.Paths.Select(p => new List<(int X, int Y)>(p)).ToList()
        };
        child.Constraints.Add((agent, cx, cy, ct));

        var path = LowLevelSearch(agent, child.Constraints, maxT);
        if (path.Count > 0)
        {
            child.Paths[agent] = path;
            child.Cost = child.Paths.Sum(p => p.Count);
            open.Enqueue(child, child.Cost);
        }
    }

    internal (int A1, int A2, int X1, int Y1, int X2, int Y2, int T)? FindFirstConflict(
        List<List<(int X, int Y)>> paths)
    {
        int maxT = paths.Max(p => p.Count);
        for (int t = 0; t < maxT; t++)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                var pi = Pos(paths[i], t);
                for (int j = i + 1; j < paths.Count; j++)
                {
                    var pj = Pos(paths[j], t);
                    if (pi == pj)
                        return (i, j, pi.X, pi.Y, pj.X, pj.Y, t);

                    if (t > 0)
                    {
                        var piP = Pos(paths[i], t - 1);
                        var pjP = Pos(paths[j], t - 1);
                        if (pi == pjP && pj == piP)
                            return (i, j, pi.X, pi.Y, pj.X, pj.Y, t);
                    }
                }
            }
        }
        return null;
    }

    internal List<(int X, int Y)> LowLevelSearch(int agentId,
        HashSet<(int Agent, int X, int Y, int T)> constraints, int maxT)
    {
        var agent = _agents[agentId];
        int gx = agent.GoalX, gy = agent.GoalY;

        int maxGoalC = 0;
        foreach (var (a, cx, cy, ct) in constraints)
            if (a == agentId && cx == gx && cy == gy && ct > maxGoalC)
                maxGoalC = ct;
        int horizon = Math.Max(maxT, maxGoalC + 1);

        var open = new PriorityQueue<(int X, int Y, int T), int>();
        var closed = new HashSet<(int, int, int)>();
        var parent = new Dictionary<(int, int, int), (int, int, int)?>();

        int sx = agent.StartX, sy = agent.StartY;
        if (constraints.Contains((agentId, sx, sy, 0)))
            return new List<(int X, int Y)>();

        open.Enqueue((sx, sy, 0), H(sx, sy, gx, gy));
        parent[(sx, sy, 0)] = null;

        while (open.Count > 0)
        {
            var (x, y, t) = open.Dequeue();
            if (closed.Contains((x, y, t))) continue;
            closed.Add((x, y, t));

            if (x == gx && y == gy && t >= maxGoalC)
                return Reconstruct((x, y, t), parent);

            if (t >= horizon) continue;

            foreach (var (nx, ny) in NeighborsWithWait(x, y))
            {
                int nt = t + 1;
                if (constraints.Contains((agentId, nx, ny, nt))) continue;
                if (closed.Contains((nx, ny, nt))) continue;
                if (parent.ContainsKey((nx, ny, nt))) continue;

                parent[(nx, ny, nt)] = (x, y, t);
                open.Enqueue((nx, ny, nt), nt + H(nx, ny, gx, gy));
            }
        }
        return new List<(int X, int Y)>();
    }

    private List<(int X, int Y)> NeighborsWithWait(int x, int y)
    {
        var result = new List<(int X, int Y)> { (x, y) };
        result.AddRange(_map.Neighbors(x, y));
        return result;
    }

    private List<(int X, int Y)> Reconstruct((int X, int Y, int T) goal,
        Dictionary<(int, int, int), (int, int, int)?> parent)
    {
        var path = new List<(int X, int Y)>();
        (int X, int Y, int T)? cur = goal;
        while (cur != null)
        {
            path.Add((cur.Value.X, cur.Value.Y));
            cur = parent[cur.Value];
        }
        path.Reverse();
        return path;
    }

    private MAPFSolution Fallback(int maxT)
    {
        var paths = new List<List<(int X, int Y)>>();
        var empty = new HashSet<(int, int, int, int)>();
        for (int i = 0; i < _agents.Count; i++)
        {
            var p = LowLevelSearch(i, empty, maxT);
            paths.Add(p.Count > 0 ? p : new List<(int X, int Y)> { (_agents[i].StartX, _agents[i].StartY) });
        }
        return new MAPFSolution { Paths = paths };
    }

    private static (int X, int Y) Pos(List<(int X, int Y)> path, int t)
        => t < path.Count ? path[t] : path[^1];

    private static int H(int x, int y, int gx, int gy)
        => Math.Abs(x - gx) + Math.Abs(y - gy);

    [Serializable]
    private class CTNode
    {
        public HashSet<(int Agent, int X, int Y, int T)> Constraints;
        public List<List<(int X, int Y)>> Paths;
        public int Cost;
    }
}
