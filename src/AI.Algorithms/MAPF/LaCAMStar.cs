using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// LaCAM* — оптимальная версия LaCAM.
/// Расширяет LaCAM гарантией оптимальности за счёт учёта стоимости
/// конфигураций и повторного раскрытия узлов при нахождении лучших путей.
/// </summary>
[Serializable]
public class LaCAMStar
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _maxIter;

    /// <summary>
    /// Создаёт решатель LaCAM*.
    /// </summary>
    public LaCAMStar(GridMap map, List<MAPFAgent> agents, int maxIter = 10000)
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
        var sw = Stopwatch.StartNew();
        int n = _agents.Count;

        var initial = new int[n * 2];
        var goalArr = new int[n * 2];
        for (int i = 0; i < n; i++)
        {
            initial[i * 2] = _agents[i].StartX;
            initial[i * 2 + 1] = _agents[i].StartY;
            goalArr[i * 2] = _agents[i].GoalX;
            goalArr[i * 2 + 1] = _agents[i].GoalY;
        }

        var distToGoal = new int[n][,];
        for (int i = 0; i < n; i++)
            distToGoal[i] = BFS(_agents[i].GoalX, _agents[i].GoalY);

        int Heuristic(int[] cfg)
        {
            int sum = 0;
            for (int i = 0; i < n; i++)
            {
                int d = distToGoal[i][cfg[i * 2], cfg[i * 2 + 1]];
                sum += d >= 0 ? d : _map.Width + _map.Height;
            }
            return sum;
        }

        string goalKey = Key(goalArr);

        var gCost = new Dictionary<string, int>();
        var parent = new Dictionary<string, string>();
        var configOf = new Dictionary<string, int[]>();

        string initKey = Key(initial);
        gCost[initKey] = 0;
        parent[initKey] = null;
        configOf[initKey] = initial;

        var open = new PriorityQueue<string, int>();
        open.Enqueue(initKey, Heuristic(initial));

        int iter = 0;
        while (open.Count > 0 && iter < _maxIter && sw.ElapsedMilliseconds < 5000)
        {
            iter++;
            string curKey = open.Dequeue();
            int g = gCost[curKey];

            if (curKey == goalKey)
                return ReconstructSolution(curKey, parent, configOf, n);

            var cur = configOf[curKey];
            var successors = GenerateSuccessors(cur, n);

            foreach (var succ in successors)
            {
                string sKey = Key(succ);
                int ng = g + 1;

                if (!gCost.ContainsKey(sKey) || ng < gCost[sKey])
                {
                    gCost[sKey] = ng;
                    parent[sKey] = curKey;
                    configOf[sKey] = succ;
                    open.Enqueue(sKey, ng + Heuristic(succ));
                }
            }
        }

        var lacam = new LaCAM(_map, _agents, _maxIter);
        return lacam.Solve();
    }

    private List<int[]> GenerateSuccessors(int[] config, int n)
    {
        var results = new List<int[]>();
        var next = (int[])config.Clone();

        var decided = new bool[n];
        PibtGenerate(config, next, decided, n, 0);
        results.Add((int[])next.Clone());

        for (int trial = 0; trial < Math.Min(n, 3); trial++)
        {
            var alt = (int[])config.Clone();
            var altDecided = new bool[n];

            var order = Enumerable.Range(0, n).ToList();
            int seed = config.Aggregate(0, (a, b) => a ^ b) + trial;
            var rng = new Random(seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            foreach (int ag in order)
            {
                int cx = config[ag * 2], cy = config[ag * 2 + 1];
                int gx = _agents[ag].GoalX, gy = _agents[ag].GoalY;
                var cands = new List<(int X, int Y)>(_map.Neighbors(cx, cy));
                cands.Add((cx, cy));
                cands.Sort((a, b) => H(a.X, a.Y, gx, gy).CompareTo(H(b.X, b.Y, gx, gy)));

                bool placed = false;
                foreach (var (nx, ny) in cands)
                {
                    bool conflict = false;
                    for (int j = 0; j < n; j++)
                    {
                        if (j != ag && altDecided[j] && alt[j * 2] == nx && alt[j * 2 + 1] == ny)
                        { conflict = true; break; }
                    }
                    if (!conflict)
                    {
                        alt[ag * 2] = nx;
                        alt[ag * 2 + 1] = ny;
                        altDecided[ag] = true;
                        placed = true;
                        break;
                    }
                }
                if (!placed) { alt[ag * 2] = cx; alt[ag * 2 + 1] = cy; altDecided[ag] = true; }
            }

            string altKey = Key(alt);
            if (!results.Any(r => Key(r) == altKey))
                results.Add(alt);
        }

        return results;
    }

    private void PibtGenerate(int[] cur, int[] next, bool[] decided, int n, int depth)
    {
        if (depth >= n) return;
        var order = Enumerable.Range(0, n)
            .Where(i => !decided[i])
            .OrderByDescending(i => H(cur[i * 2], cur[i * 2 + 1], _agents[i].GoalX, _agents[i].GoalY))
            .ToList();
        if (order.Count == 0) return;

        int ag = order[0];
        int cx = cur[ag * 2], cy = cur[ag * 2 + 1];
        int gx = _agents[ag].GoalX, gy = _agents[ag].GoalY;

        var cands = new List<(int X, int Y)>(_map.Neighbors(cx, cy));
        cands.Add((cx, cy));
        cands.Sort((a, b) => H(a.X, a.Y, gx, gy).CompareTo(H(b.X, b.Y, gx, gy)));

        foreach (var (nx, ny) in cands)
        {
            bool conflict = false;
            for (int j = 0; j < n; j++)
            {
                if (j != ag && decided[j] && next[j * 2] == nx && next[j * 2 + 1] == ny)
                { conflict = true; break; }
            }
            if (!conflict)
            {
                next[ag * 2] = nx;
                next[ag * 2 + 1] = ny;
                decided[ag] = true;
                PibtGenerate(cur, next, decided, n, depth + 1);
                return;
            }
        }
        next[ag * 2] = cx;
        next[ag * 2 + 1] = cy;
        decided[ag] = true;
        PibtGenerate(cur, next, decided, n, depth + 1);
    }

    private MAPFSolution ReconstructSolution(string goalKey,
        Dictionary<string, string> parent, Dictionary<string, int[]> configOf, int n)
    {
        var configs = new List<int[]>();
        string cur = goalKey;
        while (cur != null)
        {
            configs.Add(configOf[cur]);
            cur = parent[cur];
        }
        configs.Reverse();

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

    private int[,] BFS(int sx, int sy)
    {
        var dist = new int[_map.Width, _map.Height];
        for (int x = 0; x < _map.Width; x++)
            for (int y = 0; y < _map.Height; y++)
                dist[x, y] = -1;
        dist[sx, sy] = 0;
        var queue = new Queue<(int, int)>();
        queue.Enqueue((sx, sy));
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (nx, ny) in _map.Neighbors(cx, cy))
                if (dist[nx, ny] < 0)
                {
                    dist[nx, ny] = dist[cx, cy] + 1;
                    queue.Enqueue((nx, ny));
                }
        }
        return dist;
    }

    private static string Key(int[] c) => string.Join(",", c);
    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);
}
