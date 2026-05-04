using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Improved CBS (ICBS) — улучшенный CBS с классификацией конфликтов
/// на кардинальные, полукардинальные и некардинальные.
/// Кардинальные конфликты приоритизируются при ветвлении.
/// </summary>
[Serializable]
public class ICBS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель ICBS.
    /// </summary>
    public ICBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)
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
            Paths = new List<List<(int X, int Y)>>(),
            IndividualCosts = new int[n]
        };
        for (int i = 0; i < n; i++)
        {
            root.Paths.Add(LowLevelSearch(i, root.Constraints, maxT));
            root.IndividualCosts[i] = root.Paths[i].Count;
        }
        root.Cost = root.Paths.Sum(p => p.Count);

        var open = new PriorityQueue<CTNode, int>();
        open.Enqueue(root, root.Cost);

        while (open.Count > 0 && sw.ElapsedMilliseconds < _timeLimit)
        {
            var node = open.Dequeue();

            var conflicts = FindAllConflicts(node.Paths);
            if (conflicts.Count == 0)
                return new MAPFSolution { Paths = node.Paths };

            var best = SelectBestConflict(conflicts, node);

            Branch(node, best.A1, best.X1, best.Y1, best.T, open, maxT);
            Branch(node, best.A2, best.X2, best.Y2, best.T, open, maxT);
        }

        return Fallback(maxT);
    }

    private Conflict SelectBestConflict(List<Conflict> conflicts, CTNode node)
    {
        Conflict? cardinal = null;
        Conflict? semiCardinal = null;

        foreach (var c in conflicts)
        {
            bool a1Increases = WouldIncreaseCost(node, c.A1, c.X1, c.Y1, c.T);
            bool a2Increases = WouldIncreaseCost(node, c.A2, c.X2, c.Y2, c.T);

            if (a1Increases && a2Increases)
                return c;
            if ((a1Increases || a2Increases) && semiCardinal == null)
                semiCardinal = c;
        }

        return semiCardinal ?? cardinal ?? conflicts[0];
    }

    private bool WouldIncreaseCost(CTNode node, int agent, int cx, int cy, int ct)
    {
        int maxT = (_map.Width + _map.Height) * 2 + _agents.Count;
        var newConstraints = new HashSet<(int, int, int, int)>(node.Constraints);
        newConstraints.Add((agent, cx, cy, ct));
        var newPath = LowLevelSearch(agent, newConstraints, maxT);
        return newPath.Count == 0 || newPath.Count > node.IndividualCosts[agent];
    }

    private void Branch(CTNode parent, int agent, int cx, int cy, int ct,
        PriorityQueue<CTNode, int> open, int maxT)
    {
        var child = new CTNode
        {
            Constraints = new HashSet<(int, int, int, int)>(parent.Constraints),
            Paths = parent.Paths.Select(p => new List<(int X, int Y)>(p)).ToList(),
            IndividualCosts = (int[])parent.IndividualCosts.Clone()
        };
        child.Constraints.Add((agent, cx, cy, ct));

        var path = LowLevelSearch(agent, child.Constraints, maxT);
        if (path.Count > 0)
        {
            child.Paths[agent] = path;
            child.IndividualCosts[agent] = path.Count;
            child.Cost = child.Paths.Sum(p => p.Count);
            open.Enqueue(child, child.Cost);
        }
    }

    private List<Conflict> FindAllConflicts(List<List<(int X, int Y)>> paths)
    {
        var result = new List<Conflict>();
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
                        result.Add(new Conflict { A1 = i, A2 = j, X1 = pi.X, Y1 = pi.Y, X2 = pj.X, Y2 = pj.Y, T = t });
                    else if (t > 0)
                    {
                        var piP = Pos(paths[i], t - 1);
                        var pjP = Pos(paths[j], t - 1);
                        if (pi == pjP && pj == piP)
                            result.Add(new Conflict { A1 = i, A2 = j, X1 = pi.X, Y1 = pi.Y, X2 = pj.X, Y2 = pj.Y, T = t });
                    }
                }
            }
        }
        return result;
    }

    private List<(int X, int Y)> LowLevelSearch(int agentId,
        HashSet<(int, int, int, int)> constraints, int maxT)
    {
        var agent = _agents[agentId];
        int gx = agent.GoalX, gy = agent.GoalY;

        int maxGoalC = 0;
        foreach (var (a, cx, cy, ct) in constraints)
            if (a == agentId && cx == gx && cy == gy && ct > maxGoalC) maxGoalC = ct;
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

            foreach (var (nx, ny) in NbWait(x, y))
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

    private static (int X, int Y) Pos(List<(int X, int Y)> p, int t) => t < p.Count ? p[t] : p[^1];
    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);

    [Serializable]
    private struct Conflict
    {
        public int A1, A2, X1, Y1, X2, Y2, T;
    }

    [Serializable]
    private class CTNode
    {
        public HashSet<(int, int, int, int)> Constraints;
        public List<List<(int X, int Y)>> Paths;
        public int[] IndividualCosts;
        public int Cost;
    }
}
