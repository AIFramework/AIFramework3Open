using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Priority-Based Search (PBS) — поиск на основе приоритетов.
/// Исследует порядки приоритетов агентов: при конфликте ветвится,
/// назначая одному из агентов более высокий приоритет.
/// </summary>
[Serializable]
public class PBS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель PBS.
    /// </summary>
    public PBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)
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

        var root = new PBSNode
        {
            PriorityEdges = new HashSet<(int Higher, int Lower)>(),
            Paths = new List<List<(int X, int Y)>>()
        };
        for (int i = 0; i < n; i++)
            root.Paths.Add(PlanSingle(i, maxT, new HashSet<(int, int, int)>()));
        root.Cost = root.Paths.Sum(p => p.Count);

        var stack = new Stack<PBSNode>();
        stack.Push(root);
        MAPFSolution best = null;
        int bestCost = int.MaxValue;

        while (stack.Count > 0 && sw.ElapsedMilliseconds < _timeLimit)
        {
            var node = stack.Pop();

            var conflict = FindFirstConflict(node.Paths);
            if (conflict == null)
            {
                if (node.Cost < bestCost)
                {
                    bestCost = node.Cost;
                    best = new MAPFSolution { Paths = node.Paths };
                }
                continue;
            }

            var (a1, a2, t) = conflict.Value;

            TryBranch(node, a1, a2, stack, maxT);
            TryBranch(node, a2, a1, stack, maxT);
        }

        return best ?? Fallback(maxT);
    }

    private void TryBranch(PBSNode parent, int higher, int lower,
        Stack<PBSNode> stack, int maxT)
    {
        var child = new PBSNode
        {
            PriorityEdges = new HashSet<(int, int)>(parent.PriorityEdges),
            Paths = parent.Paths.Select(p => new List<(int X, int Y)>(p)).ToList()
        };
        child.PriorityEdges.Add((higher, lower));

        if (HasCycle(child.PriorityEdges, _agents.Count)) return;

        var reservation = BuildReservation(child, lower, maxT);
        var newPath = PlanSingle(lower, maxT, reservation);
        if (newPath.Count > 0)
        {
            child.Paths[lower] = newPath;
            child.Cost = child.Paths.Sum(p => p.Count);
            stack.Push(child);
        }
    }

    private HashSet<(int X, int Y, int T)> BuildReservation(PBSNode node, int forAgent, int maxT)
    {
        var reserved = new HashSet<(int, int, int)>();
        var higherAgents = GetHigherPriority(node.PriorityEdges, forAgent, _agents.Count);

        foreach (int h in higherAgents)
        {
            var path = node.Paths[h];
            int len = path.Count;
            for (int t = 0; t < Math.Max(len, maxT); t++)
            {
                var pos = t < len ? path[t] : path[^1];
                reserved.Add((pos.X, pos.Y, t));
                if (t >= len && t > maxT) break;
            }
        }
        return reserved;
    }

    private HashSet<int> GetHigherPriority(HashSet<(int Higher, int Lower)> edges,
        int agent, int n)
    {
        var higher = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(agent);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var (h, l) in edges)
            {
                if (l == cur && higher.Add(h))
                    queue.Enqueue(h);
            }
        }
        return higher;
    }

    private bool HasCycle(HashSet<(int Higher, int Lower)> edges, int n)
    {
        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();
        foreach (var (h, l) in edges) adj[h].Add(l);

        var color = new int[n];
        for (int i = 0; i < n; i++)
            if (color[i] == 0 && DfsCycle(i, adj, color)) return true;
        return false;
    }

    private bool DfsCycle(int u, List<int>[] adj, int[] color)
    {
        color[u] = 1;
        foreach (int v in adj[u])
        {
            if (color[v] == 1) return true;
            if (color[v] == 0 && DfsCycle(v, adj, color)) return true;
        }
        color[u] = 2;
        return false;
    }

    private List<(int X, int Y)> PlanSingle(int agentId, int maxT,
        HashSet<(int X, int Y, int T)> reserved)
    {
        var agent = _agents[agentId];
        int gx = agent.GoalX, gy = agent.GoalY, sx = agent.StartX, sy = agent.StartY;

        var open = new PriorityQueue<(int X, int Y, int T), int>();
        var closed = new HashSet<(int, int, int)>();
        var parent = new Dictionary<(int, int, int), (int, int, int)?>();

        if (reserved.Contains((sx, sy, 0)))
            return new List<(int X, int Y)> { (sx, sy) };

        open.Enqueue((sx, sy, 0), H(sx, sy, gx, gy));
        parent[(sx, sy, 0)] = null;

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
                if (reserved.Contains((nx, ny, nt))) continue;
                if (closed.Contains((nx, ny, nt))) continue;
                if (parent.ContainsKey((nx, ny, nt))) continue;
                parent[(nx, ny, nt)] = (x, y, t);
                open.Enqueue((nx, ny, nt), nt + H(nx, ny, gx, gy));
            }
        }
        return new List<(int X, int Y)> { (sx, sy) };
    }

    private (int A1, int A2, int T)? FindFirstConflict(List<List<(int X, int Y)>> paths)
    {
        int maxT = paths.Max(p => p.Count);
        for (int t = 0; t < maxT; t++)
            for (int i = 0; i < paths.Count; i++)
            {
                var pi = Pos(paths[i], t);
                for (int j = i + 1; j < paths.Count; j++)
                {
                    var pj = Pos(paths[j], t);
                    if (pi == pj) return (i, j, t);
                    if (t > 0)
                    {
                        var piP = Pos(paths[i], t - 1);
                        var pjP = Pos(paths[j], t - 1);
                        if (pi == pjP && pj == piP) return (i, j, t);
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
        var empty = new HashSet<(int, int, int)>();
        for (int i = 0; i < _agents.Count; i++)
        {
            var p = PlanSingle(i, maxT, empty);
            paths.Add(p);
        }
        return new MAPFSolution { Paths = paths };
    }

    private static (int X, int Y) Pos(List<(int X, int Y)> p, int t) => t < p.Count ? p[t] : p[^1];
    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);

    [Serializable]
    private class PBSNode
    {
        public HashSet<(int Higher, int Lower)> PriorityEdges;
        public List<List<(int X, int Y)>> Paths;
        public int Cost;
    }
}
