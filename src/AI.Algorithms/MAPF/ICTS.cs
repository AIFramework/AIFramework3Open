using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Increasing Cost Tree Search (ICTS) — поиск по дереву возрастающей стоимости.
/// На верхнем уровне перебирает вектора стоимостей,
/// на нижнем проверяет совместимость MDD (Multi-Value Decision Diagram).
/// </summary>
[Serializable]
public class ICTS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель ICTS.
    /// </summary>
    public ICTS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)
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

        var optCosts = new int[n];
        for (int i = 0; i < n; i++)
            optCosts[i] = ShortestPathLength(i);

        var costVectors = new PriorityQueue<int[], int>();
        var visited = new HashSet<string>();

        costVectors.Enqueue((int[])optCosts.Clone(), optCosts.Sum());
        visited.Add(string.Join(",", optCosts));

        while (costVectors.Count > 0 && sw.ElapsedMilliseconds < _timeLimit)
        {
            var costs = costVectors.Dequeue();

            var mdds = new List<HashSet<(int X, int Y)>[]>(n);
            bool valid = true;
            for (int i = 0; i < n; i++)
            {
                var mdd = BuildMDD(i, costs[i]);
                if (mdd == null) { valid = false; break; }
                mdds.Add(mdd);
            }

            if (valid)
            {
                var paths = FindCompatiblePaths(mdds, costs);
                if (paths != null)
                    return new MAPFSolution { Paths = paths };
            }

            for (int i = 0; i < n; i++)
            {
                var next = (int[])costs.Clone();
                next[i]++;
                string key = string.Join(",", next);
                if (visited.Add(key))
                    costVectors.Enqueue(next, next.Sum());
            }
        }

        return Fallback();
    }

    private int ShortestPathLength(int agentId)
    {
        var agent = _agents[agentId];
        var dist = BFS(agent.StartX, agent.StartY);
        int d = dist[agent.GoalX, agent.GoalY];
        return d >= 0 ? d + 1 : _map.Width * _map.Height;
    }

    private int[,] BFS(int sx, int sy)
    {
        var dist = new int[_map.Width, _map.Height];
        for (int x = 0; x < _map.Width; x++)
            for (int y = 0; y < _map.Height; y++)
                dist[x, y] = -1;

        dist[sx, sy] = 0;
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((sx, sy));

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (nx, ny) in _map.Neighbors(cx, cy))
            {
                if (dist[nx, ny] < 0)
                {
                    dist[nx, ny] = dist[cx, cy] + 1;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        return dist;
    }

    private HashSet<(int X, int Y)>[] BuildMDD(int agentId, int cost)
    {
        var agent = _agents[agentId];
        var fwd = BFS(agent.StartX, agent.StartY);
        var bwd = BFS(agent.GoalX, agent.GoalY);
        int pathLen = cost - 1;

        var layers = new HashSet<(int X, int Y)>[cost];
        for (int t = 0; t < cost; t++)
        {
            layers[t] = new HashSet<(int X, int Y)>();
            for (int x = 0; x < _map.Width; x++)
                for (int y = 0; y < _map.Height; y++)
                    if (fwd[x, y] >= 0 && bwd[x, y] >= 0
                        && fwd[x, y] <= t && bwd[x, y] <= pathLen - t)
                        layers[t].Add((x, y));
        }

        if (layers[cost - 1].Count == 0) return null;
        return layers;
    }

    private List<List<(int X, int Y)>> FindCompatiblePaths(
        List<HashSet<(int X, int Y)>[]> mdds, int[] costs)
    {
        int n = _agents.Count;
        int maxLen = costs.Max();

        var paths = new List<List<(int X, int Y)>>();
        for (int i = 0; i < n; i++)
            paths.Add(new List<(int X, int Y)>());

        var current = new (int X, int Y)[n];
        for (int i = 0; i < n; i++)
        {
            current[i] = (_agents[i].StartX, _agents[i].StartY);
            paths[i].Add(current[i]);
        }

        for (int t = 1; t < maxLen; t++)
        {
            var next = new (int X, int Y)[n];
            bool ok = true;

            for (int i = 0; i < n; i++)
            {
                if (t >= costs[i])
                {
                    next[i] = (_agents[i].GoalX, _agents[i].GoalY);
                    continue;
                }

                var layer = mdds[i][t];
                (int X, int Y) best = current[i];
                int bestDist = int.MaxValue;

                foreach (var pos in layer)
                {
                    int dx = Math.Abs(pos.X - current[i].X);
                    int dy = Math.Abs(pos.Y - current[i].Y);
                    if (dx + dy <= 1)
                    {
                        int dist = Math.Abs(pos.X - _agents[i].GoalX)
                                 + Math.Abs(pos.Y - _agents[i].GoalY);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = pos;
                        }
                    }
                }

                bool collision = false;
                for (int j = 0; j < i; j++)
                    if (next[j] == best) { collision = true; break; }

                if (collision)
                {
                    if (layer.Contains(current[i]))
                        best = current[i];
                    else { ok = false; break; }
                }

                next[i] = best;
            }

            if (!ok) return null;

            for (int i = 0; i < n; i++)
            {
                current[i] = next[i];
                paths[i].Add(current[i]);
            }
        }

        for (int i = 0; i < n; i++)
        {
            var last = paths[i][^1];
            if (last.X != _agents[i].GoalX || last.Y != _agents[i].GoalY)
                return null;
        }

        return paths;
    }

    private MAPFSolution Fallback()
    {
        var paths = new List<List<(int X, int Y)>>();
        for (int i = 0; i < _agents.Count; i++)
        {
            var a = _agents[i];
            var path = FindPathBFS(a.StartX, a.StartY, a.GoalX, a.GoalY);
            paths.Add(path);
        }
        return new MAPFSolution { Paths = paths };
    }

    private List<(int X, int Y)> FindPathBFS(int sx, int sy, int gx, int gy)
    {
        var parent = new Dictionary<(int, int), (int, int)?>();
        var queue = new Queue<(int, int)>();
        parent[(sx, sy)] = null;
        queue.Enqueue((sx, sy));

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (cx == gx && cy == gy)
            {
                var path = new List<(int X, int Y)>();
                (int, int)? c = (cx, cy);
                while (c != null) { path.Add(c.Value); c = parent[c.Value]; }
                path.Reverse();
                return path;
            }
            foreach (var (nx, ny) in _map.Neighbors(cx, cy))
            {
                if (!parent.ContainsKey((nx, ny)))
                {
                    parent[(nx, ny)] = (cx, cy);
                    queue.Enqueue((nx, ny));
                }
            }
        }
        return new List<(int X, int Y)> { (sx, sy) };
    }
}
