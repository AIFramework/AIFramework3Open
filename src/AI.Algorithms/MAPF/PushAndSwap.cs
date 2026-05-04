using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Push and Swap — полиномиальный алгоритм для просто-связных графов.
/// Перемещает агентов к целям методом «толкни», при невозможности —
/// выполняет операцию обмена (swap) двух агентов.
/// </summary>
[Serializable]
public class PushAndSwap
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;

    /// <summary>
    /// Создаёт решатель Push and Swap.
    /// </summary>
    public PushAndSwap(GridMap map, List<MAPFAgent> agents)
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

                var route = FindPathBFS(pos[i].X, pos[i].Y, goal.Item1, goal.Item2, pos, i);
                if (route == null || route.Count < 2) continue;

                var nextCell = route[1];
                int occupant = OccupantAt(pos, nextCell, i);

                if (occupant < 0)
                {
                    pos[i] = nextCell;
                    RecordStep(paths, pos, n);
                    moved = true;
                    break;
                }

                var occupantGoal = (_agents[occupant].GoalX, _agents[occupant].GoalY);
                if (pos[occupant] == occupantGoal)
                {
                    if (TrySwap(i, occupant, pos, paths, n))
                    { moved = true; break; }
                }

                if (TryPush(occupant, i, pos, paths, n))
                {
                    pos[i] = nextCell;
                    RecordStep(paths, pos, n);
                    moved = true;
                    break;
                }

                if (TrySwap(i, occupant, pos, paths, n))
                { moved = true; break; }
            }

            if (!moved) break;
        }

        return new MAPFSolution { Paths = paths };
    }

    private bool TryPush(int pushed, int pusher, (int X, int Y)[] pos,
        List<List<(int X, int Y)>> paths, int n)
    {
        var freeCell = FindFreeNeighbor(pos[pushed], pos, pushed);
        if (freeCell == null) return false;

        pos[pushed] = freeCell.Value;
        RecordStep(paths, pos, n);
        return true;
    }

    private bool TrySwap(int a, int b, (int X, int Y)[] pos,
        List<List<(int X, int Y)>> paths, int n)
    {
        var freeCell = FindFreeNeighbor(pos[a], pos, -1);
        if (freeCell == null)
            freeCell = FindFreeNeighbor(pos[b], pos, -1);
        if (freeCell == null) return false;

        var tempA = pos[a];
        var tempB = pos[b];

        int helper = OccupantAt(pos, freeCell.Value, -1);
        if (helper >= 0) return false;

        pos[a] = freeCell.Value;
        RecordStep(paths, pos, n);

        pos[b] = tempA;
        RecordStep(paths, pos, n);

        pos[a] = tempB;
        RecordStep(paths, pos, n);

        return true;
    }

    private (int X, int Y)? FindFreeNeighbor((int X, int Y) cell, (int X, int Y)[] pos, int exclude)
    {
        foreach (var nb in _map.Neighbors(cell.X, cell.Y))
        {
            if (OccupantAt(pos, nb, exclude) < 0)
                return nb;
        }
        return null;
    }

    private int OccupantAt((int X, int Y)[] pos, (int X, int Y) cell, int exclude)
    {
        for (int i = 0; i < pos.Length; i++)
            if (i != exclude && pos[i] == cell) return i;
        return -1;
    }

    private void RecordStep(List<List<(int X, int Y)>> paths, (int X, int Y)[] pos, int n)
    {
        for (int i = 0; i < n; i++)
            paths[i].Add(pos[i]);
    }

    private List<(int X, int Y)> FindPathBFS(int sx, int sy, int gx, int gy,
        (int X, int Y)[] positions, int self)
    {
        var occupied = new HashSet<(int, int)>();
        for (int i = 0; i < positions.Length; i++)
            if (i != self) occupied.Add(positions[i]);

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
        return null;
    }
}
