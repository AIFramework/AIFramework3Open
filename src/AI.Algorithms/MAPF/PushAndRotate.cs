using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Push and Rotate — полиномиальный алгоритм для многоагентного поиска пути.
/// Расширяет Push and Swap, используя операцию вращения (rotate)
/// группы агентов по циклу вместо попарного обмена.
/// </summary>
[Serializable]
public class PushAndRotate
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;

    /// <summary>
    /// Создаёт решатель Push and Rotate.
    /// </summary>
    public PushAndRotate(GridMap map, List<MAPFAgent> agents)
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
        var pos = new (int X, int Y)[n];
        for (int i = 0; i < n; i++)
            pos[i] = (_agents[i].StartX, _agents[i].StartY);

        var paths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
            paths.Add(new List<(int X, int Y)> { pos[i] });

        int maxIter = _map.Width * _map.Height * n * 4;

        for (int iter = 0; iter < maxIter; iter++)
        {
            bool allDone = true;
            for (int i = 0; i < n; i++)
            {
                if (pos[i] != (_agents[i].GoalX, _agents[i].GoalY))
                { allDone = false; break; }
            }
            if (allDone) break;

            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                var goal = (_agents[i].GoalX, _agents[i].GoalY);
                if (pos[i] == goal) continue;

                var route = FindRoute(pos[i], goal);
                if (route == null || route.Count < 2) continue;

                var nextCell = route[1];
                int occupant = Occupant(pos, nextCell, i);

                if (occupant < 0)
                {
                    pos[i] = nextCell;
                    Step(paths, pos, n);
                    moved = true;
                    break;
                }

                if (TryRotate(i, pos, paths, n))
                { moved = true; break; }

                if (TryPush(occupant, pos, paths, n))
                {
                    pos[i] = nextCell;
                    Step(paths, pos, n);
                    moved = true;
                    break;
                }
            }

            if (!moved) break;
        }

        return new MAPFSolution { Paths = paths };
    }

    private bool TryRotate(int initiator, (int X, int Y)[] pos,
        List<List<(int X, int Y)>> paths, int n)
    {
        var cycle = FindCycle(initiator, pos, n);
        if (cycle == null || cycle.Count < 2) return false;

        var emptyNb = FindFreeNb(pos[cycle[0]], pos);
        if (emptyNb == null) return false;

        int last = cycle[^1];
        var savedPos = pos[last];
        pos[last] = emptyNb.Value;
        Step(paths, pos, n);

        for (int k = cycle.Count - 1; k > 0; k--)
        {
            pos[cycle[k]] = pos[cycle[k - 1]];
            Step(paths, pos, n);
        }

        var goal = (_agents[initiator].GoalX, _agents[initiator].GoalY);
        var route = FindRoute(pos[initiator], goal);
        if (route != null && route.Count >= 2)
        {
            pos[initiator] = route[1];
            Step(paths, pos, n);
        }

        return true;
    }

    private List<int> FindCycle(int start, (int X, int Y)[] pos, int n)
    {
        var goal = (_agents[start].GoalX, _agents[start].GoalY);
        var route = FindRoute(pos[start], goal);
        if (route == null || route.Count < 2) return null;

        var cycle = new List<int> { start };
        var visited = new HashSet<int> { start };

        var current = route[1];
        for (int step = 0; step < n + 2; step++)
        {
            int occ = Occupant(pos, current, -1);
            if (occ < 0) break;
            if (visited.Contains(occ))
            {
                if (occ == start && cycle.Count >= 2)
                    return cycle;
                break;
            }
            visited.Add(occ);
            cycle.Add(occ);

            var nextGoal = (_agents[occ].GoalX, _agents[occ].GoalY);
            var nextRoute = FindRoute(pos[occ], nextGoal);
            if (nextRoute == null || nextRoute.Count < 2) break;
            current = nextRoute[1];
        }
        return cycle.Count >= 2 ? cycle : null;
    }

    private bool TryPush(int agent, (int X, int Y)[] pos,
        List<List<(int X, int Y)>> paths, int n)
    {
        var free = FindFreeNb(pos[agent], pos);
        if (free == null) return false;
        pos[agent] = free.Value;
        Step(paths, pos, n);
        return true;
    }

    private (int X, int Y)? FindFreeNb((int X, int Y) cell, (int X, int Y)[] pos)
    {
        foreach (var nb in _map.Neighbors(cell.X, cell.Y))
            if (Occupant(pos, nb, -1) < 0) return nb;
        return null;
    }

    private int Occupant((int X, int Y)[] pos, (int X, int Y) cell, int exclude)
    {
        for (int i = 0; i < pos.Length; i++)
            if (i != exclude && pos[i] == cell) return i;
        return -1;
    }

    private void Step(List<List<(int X, int Y)>> paths, (int X, int Y)[] pos, int n)
    {
        for (int i = 0; i < n; i++)
            paths[i].Add(pos[i]);
    }

    private List<(int X, int Y)> FindRoute((int X, int Y) start, (int X, int Y) goal)
    {
        var parent = new Dictionary<(int, int), (int, int)?>();
        var queue = new Queue<(int, int)>();
        parent[start] = null;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == goal)
            {
                var path = new List<(int X, int Y)>();
                (int, int)? cur = c;
                while (cur != null) { path.Add(cur.Value); cur = parent[cur.Value]; }
                path.Reverse();
                return path;
            }
            foreach (var nb in _map.Neighbors(c.Item1, c.Item2))
                if (!parent.ContainsKey(nb))
                {
                    parent[nb] = c;
                    queue.Enqueue(nb);
                }
        }
        return null;
    }
}
