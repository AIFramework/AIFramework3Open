using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Token Passing — децентрализованный алгоритм, в котором токен
/// передаётся между агентами по кругу. Агент, владеющий токеном,
/// планирует свой путь, остальные ожидают на месте.
/// </summary>
[Serializable]
public class TokenPassing
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;

    /// <summary>
    /// Создаёт решатель Token Passing.
    /// </summary>
    public TokenPassing(GridMap map, List<MAPFAgent> agents)
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
        var pos = new (int X, int Y)[n];
        for (int i = 0; i < n; i++)
            pos[i] = (_agents[i].StartX, _agents[i].StartY);

        var paths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
            paths.Add(new List<(int X, int Y)> { pos[i] });

        var endpoints = new HashSet<(int, int)>();
        for (int i = 0; i < n; i++)
            endpoints.Add((_agents[i].GoalX, _agents[i].GoalY));

        int globalTime = 0;
        int maxRounds = _map.Width * _map.Height * n;

        for (int round = 0; round < maxRounds; round++)
        {
            bool allDone = true;
            for (int i = 0; i < n; i++)
            {
                if (pos[i] != (_agents[i].GoalX, _agents[i].GoalY))
                { allDone = false; break; }
            }
            if (allDone) break;

            int tokenHolder = round % n;
            var agent = _agents[tokenHolder];

            if (pos[tokenHolder] == (agent.GoalX, agent.GoalY))
            {
                Tick(paths, pos, n);
                globalTime++;
                continue;
            }

            var occupied = new HashSet<(int, int)>();
            for (int j = 0; j < n; j++)
                if (j != tokenHolder) occupied.Add(pos[j]);

            var route = PlanAvoidingOccupied(tokenHolder, pos[tokenHolder],
                occupied, endpoints, maxT, globalTime);

            if (route.Count <= 1)
            {
                Tick(paths, pos, n);
                globalTime++;
                continue;
            }

            for (int step = 1; step < route.Count; step++)
            {
                pos[tokenHolder] = route[step];
                Tick(paths, pos, n);
                globalTime++;
            }
        }

        return new MAPFSolution { Paths = paths };
    }

    private List<(int X, int Y)> PlanAvoidingOccupied(int agentId, (int X, int Y) start,
        HashSet<(int, int)> occupied, HashSet<(int, int)> endpoints, int maxT, int globalTime)
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

            if (x == gx && y == gy)
                return Reconstruct((x, y, t), parent);

            if (t >= maxT) continue;

            foreach (var (nx, ny) in _map.Neighbors(x, y))
            {
                if (occupied.Contains((nx, ny))) continue;
                var nk = (nx, ny, t + 1);
                if (closed.Contains(nk)) continue;
                if (parent.ContainsKey(nk)) continue;

                parent[nk] = (x, y, t);
                open.Enqueue(nk, t + 1 + H(nx, ny, gx, gy));
            }

            var wk = (x, y, t + 1);
            if (!closed.Contains(wk) && !parent.ContainsKey(wk))
            {
                parent[wk] = (x, y, t);
                open.Enqueue(wk, t + 1 + H(x, y, gx, gy));
            }
        }

        return new List<(int X, int Y)> { start };
    }

    private void Tick(List<List<(int X, int Y)>> paths, (int X, int Y)[] pos, int n)
    {
        for (int i = 0; i < n; i++)
            paths[i].Add(pos[i]);
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
