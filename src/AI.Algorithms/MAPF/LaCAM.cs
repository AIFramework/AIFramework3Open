using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// LaCAM — Lazy Constraints Addition search для многоагентного поиска пути.
/// Выполняет поиск по совместным конфигурациям агентов, порождая
/// преемников с помощью PIBT и лениво добавляя ограничения при возврате.
/// </summary>
[Serializable]
public class LaCAM
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _maxIter;

    /// <summary>
    /// Создаёт решатель LaCAM.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="maxIter">Максимальное число итераций.</param>
    public LaCAM(GridMap map, List<MAPFAgent> agents, int maxIter = 10000)
    {
        _map = map;
        _agents = agents;
        _maxIter = maxIter;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        int n = _agents.Count;
        var initial = new int[n * 2];
        var goal = new int[n * 2];
        for (int i = 0; i < n; i++)
        {
            initial[i * 2] = _agents[i].StartX;
            initial[i * 2 + 1] = _agents[i].StartY;
            goal[i * 2] = _agents[i].GoalX;
            goal[i * 2 + 1] = _agents[i].GoalY;
        }

        var nodes = new List<SearchNode>();
        var visited = new Dictionary<string, int>();

        var root = new SearchNode
        {
            Config = initial,
            Parent = -1,
            Constraints = new List<(int Agent, int X, int Y)>(),
            GenIndex = 0
        };
        nodes.Add(root);
        visited[Key(initial)] = 0;

        var stack = new Stack<int>();
        stack.Push(0);

        string goalKey = Key(goal);
        int iter = 0;

        while (stack.Count > 0 && iter < _maxIter)
        {
            iter++;
            int curIdx = stack.Peek();
            var cur = nodes[curIdx];

            if (Key(cur.Config) == goalKey)
                return ReconstructSolution(curIdx, nodes);

            var successor = GenerateSuccessor(cur, n);
            if (successor == null)
            {
                stack.Pop();
                continue;
            }

            string sKey = Key(successor);
            if (!visited.ContainsKey(sKey))
            {
                int sIdx = nodes.Count;
                var sNode = new SearchNode
                {
                    Config = successor,
                    Parent = curIdx,
                    Constraints = new List<(int, int, int)>(),
                    GenIndex = 0
                };
                nodes.Add(sNode);
                visited[sKey] = sIdx;
                stack.Push(sIdx);
            }
            else
            {
                AddConstraintFromConfig(cur, successor, n);
            }
        }

        return FallbackPIBT();
    }

    private int[] GenerateSuccessor(SearchNode node, int n)
    {
        var cur = node.Config;
        var next = new int[n * 2];
        var decided = new bool[n];
        Array.Copy(cur, next, cur.Length);

        var constraintSet = new HashSet<(int, int, int)>();
        foreach (var c in node.Constraints)
            constraintSet.Add(c);

        var order = Enumerable.Range(0, n).ToArray();
        Array.Sort(order, (a, b) =>
        {
            int da = H(cur[a * 2], cur[a * 2 + 1], _agents[a].GoalX, _agents[a].GoalY);
            int db = H(cur[b * 2], cur[b * 2 + 1], _agents[b].GoalX, _agents[b].GoalY);
            return db.CompareTo(da);
        });

        foreach (int i in order)
        {
            int cx = cur[i * 2], cy = cur[i * 2 + 1];
            int gx = _agents[i].GoalX, gy = _agents[i].GoalY;

            var candidates = new List<(int X, int Y)>(_map.Neighbors(cx, cy));
            candidates.Add((cx, cy));

            if (constraintSet.Contains((i, -1, -1)))
            {
                var forced = node.Constraints.Where(c => c.Agent == i && c.X >= 0).ToList();
                if (forced.Count > 0)
                {
                    candidates.RemoveAll(c => forced.Any(f => f.X == c.X && f.Y == c.Y));
                }
            }

            candidates.Sort((a, b) =>
                H(a.X, a.Y, gx, gy).CompareTo(H(b.X, b.Y, gx, gy)));

            bool placed = false;
            foreach (var (nx, ny) in candidates)
            {
                bool conflict = false;
                for (int j = 0; j < n; j++)
                {
                    if (j != i && decided[j] && next[j * 2] == nx && next[j * 2 + 1] == ny)
                    { conflict = true; break; }
                }
                if (conflict) continue;

                next[i * 2] = nx;
                next[i * 2 + 1] = ny;
                decided[i] = true;
                placed = true;
                break;
            }

            if (!placed)
            {
                next[i * 2] = cx;
                next[i * 2 + 1] = cy;
                decided[i] = true;
            }
        }

        if (node.GenIndex > n * 3) return null;
        node.GenIndex++;
        return next;
    }

    private void AddConstraintFromConfig(SearchNode node, int[] successor, int n)
    {
        for (int i = 0; i < n; i++)
        {
            int sx = successor[i * 2], sy = successor[i * 2 + 1];
            if (!node.Constraints.Any(c => c.Agent == i && c.X == sx && c.Y == sy))
            {
                node.Constraints.Add((i, sx, sy));
                break;
            }
        }
    }

    private MAPFSolution ReconstructSolution(int goalIdx, List<SearchNode> nodes)
    {
        var configs = new List<int[]>();
        int idx = goalIdx;
        while (idx >= 0)
        {
            configs.Add(nodes[idx].Config);
            idx = nodes[idx].Parent;
        }
        configs.Reverse();

        int n = _agents.Count;
        var paths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
        {
            var p = new List<(int X, int Y)>();
            foreach (var cfg in configs)
                p.Add((cfg[i * 2], cfg[i * 2 + 1]));
            paths.Add(p);
        }
        return new MAPFSolution { Paths = paths };
    }

    private MAPFSolution FallbackPIBT()
    {
        var pibt = new PIBT(_map, _agents, _maxIter);
        return pibt.Solve();
    }

    private static string Key(int[] config) => string.Join(",", config);
    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);

    [Serializable]
    private class SearchNode
    {
        public int[] Config;
        public int Parent;
        public List<(int Agent, int X, int Y)> Constraints;
        public int GenIndex;
    }
}
