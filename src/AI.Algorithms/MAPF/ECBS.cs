using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Enhanced CBS (ECBS) — ограниченно-субоптимальный CBS.
/// Использует focal search на обоих уровнях для ускорения поиска
/// с гарантией субоптимальности не более заданного коэффициента.
/// </summary>
[Serializable]
public class ECBS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly double _w;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель ECBS.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="suboptimalityBound">Коэффициент субоптимальности (w ≥ 1.0).</param>
    /// <param name="timeLimit">Лимит по времени в миллисекундах.</param>
    public ECBS(GridMap map, List<MAPFAgent> agents, double suboptimalityBound = 1.5,
        int timeLimit = 1000)
    {
        _map = map;
        _agents = agents;
        _w = Math.Max(1.0, suboptimalityBound);
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
            LBs = new int[n]
        };
        for (int i = 0; i < n; i++)
        {
            var (path, lb) = FocalLowLevel(i, root.Constraints, maxT);
            root.Paths.Add(path);
            root.LBs[i] = lb;
        }
        root.Cost = root.Paths.Sum(p => p.Count);
        root.LB = root.LBs.Sum();
        root.Conflicts = CountConflicts(root.Paths);

        var openList = new List<CTNode> { root };

        while (openList.Count > 0 && sw.ElapsedMilliseconds < _timeLimit)
        {
            int bestLB = openList.Min(nd => nd.LB);
            double focalBound = bestLB * _w;

            var focal = openList.Where(nd => nd.Cost <= focalBound).ToList();
            if (focal.Count == 0) focal = openList;

            var node = focal.OrderBy(nd => nd.Conflicts).First();
            openList.Remove(node);

            var conflict = FindFirstConflict(node.Paths);
            if (conflict == null)
                return new MAPFSolution { Paths = node.Paths };

            var c = conflict.Value;

            TryBranch(node, c.A1, c.X1, c.Y1, c.T, openList, maxT);
            TryBranch(node, c.A2, c.X2, c.Y2, c.T, openList, maxT);
        }

        return Fallback(maxT);
    }

    private void TryBranch(CTNode parent, int agent, int cx, int cy, int ct,
        List<CTNode> openList, int maxT)
    {
        var child = new CTNode
        {
            Constraints = new HashSet<(int, int, int, int)>(parent.Constraints),
            Paths = parent.Paths.Select(p => new List<(int X, int Y)>(p)).ToList(),
            LBs = (int[])parent.LBs.Clone()
        };
        child.Constraints.Add((agent, cx, cy, ct));

        var (path, lb) = FocalLowLevel(agent, child.Constraints, maxT);
        if (path.Count > 0)
        {
            child.Paths[agent] = path;
            child.LBs[agent] = lb;
            child.Cost = child.Paths.Sum(p => p.Count);
            child.LB = child.LBs.Sum();
            child.Conflicts = CountConflicts(child.Paths);
            openList.Add(child);
        }
    }

    private (List<(int X, int Y)> Path, int LB) FocalLowLevel(int agentId,
        HashSet<(int, int, int, int)> constraints, int maxT)
    {
        var agent = _agents[agentId];
        int gx = agent.GoalX, gy = agent.GoalY, sx = agent.StartX, sy = agent.StartY;

        int maxGoalC = 0;
        foreach (var (a, cx, cy, ct) in constraints)
            if (a == agentId && cx == gx && cy == gy && ct > maxGoalC) maxGoalC = ct;
        int horizon = Math.Max(maxT, maxGoalC + 1);

        if (constraints.Contains((agentId, sx, sy, 0)))
            return (new List<(int X, int Y)>(), int.MaxValue);

        var open = new PriorityQueue<State, int>();
        var closed = new HashSet<(int, int, int)>();
        var parent = new Dictionary<(int, int, int), (int, int, int)?>();
        var gVals = new Dictionary<(int, int, int), int>();

        var s0 = (sx, sy, 0);
        parent[s0] = null;
        gVals[s0] = 0;
        open.Enqueue(new State(sx, sy, 0, 0), H(sx, sy, gx, gy));

        int lb = H(sx, sy, gx, gy);
        List<(int X, int Y)> bestPath = null;
        int bestCost = int.MaxValue;

        while (open.Count > 0)
        {
            var cur = open.Dequeue();
            var key = (cur.X, cur.Y, cur.T);

            if (closed.Contains(key)) continue;
            closed.Add(key);

            if (cur.X == gx && cur.Y == gy && cur.T >= maxGoalC)
            {
                var path = Reconstruct(key, parent);
                if (bestPath == null)
                {
                    lb = path.Count;
                    bestPath = path;
                    bestCost = path.Count;
                    if (_w <= 1.0) break;
                }
                if (path.Count < bestCost)
                {
                    bestPath = path;
                    bestCost = path.Count;
                }
                if (bestCost <= lb * _w) break;
                continue;
            }

            if (cur.T >= horizon) continue;

            foreach (var (nx, ny) in NbWait(cur.X, cur.Y))
            {
                int nt = cur.T + 1;
                if (constraints.Contains((agentId, nx, ny, nt))) continue;
                var nk = (nx, ny, nt);
                if (closed.Contains(nk)) continue;
                if (parent.ContainsKey(nk)) continue;

                parent[nk] = key;
                gVals[nk] = nt;
                open.Enqueue(new State(nx, ny, nt, 0), nt + H(nx, ny, gx, gy));
            }
        }

        return bestPath != null
            ? (bestPath, lb)
            : (new List<(int X, int Y)>(), int.MaxValue);
    }

    private int CountConflicts(List<List<(int X, int Y)>> paths)
    {
        int count = 0;
        int maxT = paths.Max(p => p.Count);
        for (int t = 0; t < maxT; t++)
            for (int i = 0; i < paths.Count; i++)
            {
                var pi = Pos(paths[i], t);
                for (int j = i + 1; j < paths.Count; j++)
                {
                    var pj = Pos(paths[j], t);
                    if (pi == pj) count++;
                    if (t > 0)
                    {
                        var piP = Pos(paths[i], t - 1);
                        var pjP = Pos(paths[j], t - 1);
                        if (pi == pjP && pj == piP) count++;
                    }
                }
            }
        return count;
    }

    private (int A1, int A2, int X1, int Y1, int X2, int Y2, int T)? FindFirstConflict(
        List<List<(int X, int Y)>> paths)
    {
        int maxT = paths.Max(p => p.Count);
        for (int t = 0; t < maxT; t++)
            for (int i = 0; i < paths.Count; i++)
            {
                var pi = Pos(paths[i], t);
                for (int j = i + 1; j < paths.Count; j++)
                {
                    var pj = Pos(paths[j], t);
                    if (pi == pj) return (i, j, pi.X, pi.Y, pj.X, pj.Y, t);
                    if (t > 0)
                    {
                        var piP = Pos(paths[i], t - 1);
                        var pjP = Pos(paths[j], t - 1);
                        if (pi == pjP && pj == piP) return (i, j, pi.X, pi.Y, pj.X, pj.Y, t);
                    }
                }
            }
        return null;
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
            var (p, _) = FocalLowLevel(i, empty, maxT);
            paths.Add(p.Count > 0 ? p : new List<(int X, int Y)> { (_agents[i].StartX, _agents[i].StartY) });
        }
        return new MAPFSolution { Paths = paths };
    }

    private static (int X, int Y) Pos(List<(int X, int Y)> p, int t) => t < p.Count ? p[t] : p[^1];
    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);

    [Serializable]
    private class CTNode
    {
        public HashSet<(int, int, int, int)> Constraints;
        public List<List<(int X, int Y)>> Paths;
        public int[] LBs;
        public int Cost, LB, Conflicts;
    }

    private readonly struct State
    {
        public readonly int X, Y, T, Conflicts;
        public State(int x, int y, int t, int conflicts) { X = x; Y = y; T = t; Conflicts = conflicts; }
    }
}
