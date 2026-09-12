using System.Numerics;
using AI.Algorithms.MAPF;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Многоагентный поиск пути проверяется независимым контролёром решения и точным оракулом:
/// поиском Дейкстры по совместным состояниям всех агентов. Оптимальные решатели обязаны давать
/// ту же сумму времён прибытия, что и оракул; остальные — хотя бы решение без столкновений.
/// </summary>
public class MapfTests
{
    private static readonly (string Name, Func<GridMap, List<MAPFAgent>, MAPFSolution> Solve)[] Optimal =
    [
        ("CBS", (map, agents) => new CBS(map, agents, 5000).Solve()),
        ("ICBS", (map, agents) => new ICBS(map, agents, 5000).Solve()),
    ];

    private static readonly (string Name, Func<GridMap, List<MAPFAgent>, MAPFSolution> Solve)[] Prioritized =
    [
        ("HCA", (map, agents) => new HCA(map, agents).Solve()),
        ("WHCA", (map, agents) => new WHCA(map, agents, 8).Solve()),
        ("PIBT", (map, agents) => new PIBT(map, agents).Solve()),
        ("TokenPassing", (map, agents) => new TokenPassing(map, agents).Solve()),
    ];

    #region Оптимальные решатели

    [Fact]
    public void Crossing2x2_IsSolvedOptimally()
    {
        // Соседи меняются местами на поле 2×2: один идёт напрямую, другой — в обход, итого 1 + 3
        GridMap map = Grid(2, 2);
        List<MAPFAgent> agents = Agents(((0, 0), (1, 0)), ((1, 0), (0, 0)));

        Assert.Equal(4, OptimalSoc(map, agents));

        foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Optimal)
        {
            MAPFSolution solution = Within(() => solve(map, agents));
            AssertValid(map, agents, solution, name);
            Assert.Equal(4, Arrivals(solution, agents));
        }
    }

    [Fact]
    public void SwapInCorridorWithPocket_IsSolvedOptimally()
    {
        // Коридор из трёх клеток с карманом над средней: один агент пропускает другого
        GridMap map = Grid(3, 2, (0, 1), (2, 1));
        List<MAPFAgent> agents = Agents(((0, 0), (2, 0)), ((2, 0), (0, 0)));

        Assert.Equal(7, OptimalSoc(map, agents));

        foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Optimal)
        {
            MAPFSolution solution = Within(() => solve(map, agents));
            AssertValid(map, agents, solution, name);
            Assert.Equal(7, Arrivals(solution, agents));
        }
    }

    [Fact]
    public void AgentStandingOnItsGoal_StepsAsideAndReturns()
    {
        // Агент уже на цели посреди коридора; второму нужно пройти сквозь неё
        GridMap map = Grid(3, 2, (0, 1), (2, 1));
        List<MAPFAgent> agents = Agents(((1, 0), (1, 0)), ((0, 0), (2, 0)));

        Assert.Equal(4, OptimalSoc(map, agents));

        foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Optimal)
        {
            MAPFSolution solution = Within(() => solve(map, agents));
            AssertValid(map, agents, solution, name);
            Assert.Equal(4, Arrivals(solution, agents));
        }
    }

    [Fact]
    public void OptimalSolvers_MatchJointSearch_OnRandomInstances()
    {
        int compared = 0;

        for (int seed = 0; seed < 40 && compared < 15; seed++)
        {
            (GridMap map, List<MAPFAgent> agents) = RandomInstance(seed, 4, 3, walls: seed % 3, count: 3, goalsOnStarts: true);
            int optimum = OptimalSoc(map, agents);

            if (optimum == int.MaxValue)
                continue;

            compared++;

            foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Optimal)
            {
                MAPFSolution solution = Within(() => solve(map, agents));
                AssertValid(map, agents, solution, $"{name}, seed {seed}");
                Assert.Equal(optimum, Arrivals(solution, agents));
            }

            // ECBS: сумма длин путей не больше w·оптимум
            MAPFSolution bounded = Within(() => new ECBS(map, agents, 1.5, 5000).Solve());
            AssertValid(map, agents, bounded, $"ECBS, seed {seed}");
            Assert.True(bounded.SumOfCosts <= 1.5 * (optimum + agents.Count) + 1e-9, $"ECBS, seed {seed}: {bounded.SumOfCosts}");
        }

        Assert.True(compared >= 10, $"сравнено лишь {compared} задач");
    }

    [Fact]
    public void UnreachableGoal_GivesInvalidSolutionInsteadOfException()
    {
        // Угол (2,2) отрезан стенами — прежде CBS падал на пустом пути, ECBS переполнял сумму
        GridMap map = Grid(3, 3, (1, 2), (2, 1));
        List<MAPFAgent> agents = Agents(((0, 0), (2, 2)), ((1, 1), (0, 2)));

        Assert.Equal(int.MaxValue, OptimalSoc(map, agents));

        var solvers = Optimal.Append(("ECBS", (m, a) => new ECBS(m, a, 1.5, 2000).Solve()));

        foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in solvers)
        {
            MAPFSolution solution = Within(() => solve(map, agents));
            Assert.Equal(2, solution.Paths.Count);
            Assert.NotNull(Violation(map, agents, solution));
            Assert.False(solution.IsValid(map, agents), name);
        }
    }

    #endregion

    #region Приоритетные и реактивные решатели

    [Fact]
    public void Crossing2x2_PrioritizedPlannersDoNotSwap()
    {
        // Прежние HCA и WHCA резервировали только клетки и меняли агентов местами
        GridMap map = Grid(2, 2);
        List<MAPFAgent> agents = Agents(((0, 0), (1, 0)), ((1, 0), (0, 0)));

        foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Prioritized.Where(s => s.Name != "TokenPassing"))
            AssertValid(map, agents, Within(() => solve(map, agents)), name);
    }

    [Fact]
    public void Following_IsAllowed()
    {
        // Агенты идут друг за другом по коридору: следовать в освобождаемую клетку можно
        GridMap map = Grid(4, 1);
        List<MAPFAgent> agents = Agents(((0, 0), (2, 0)), ((1, 0), (3, 0)));

        foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Optimal.Concat(Prioritized))
        {
            MAPFSolution solution = Within(() => solve(map, agents));
            AssertValid(map, agents, solution, name);
            Assert.Equal(4, Arrivals(solution, agents));
        }
    }

    [Fact]
    public void TokenPassing_LetsTheAgentStandingOnAnotherGoalGoFirst()
    {
        // Цель первого агента занята вторым: прежде первый ждал его вечно
        GridMap map = Grid(3, 1);
        List<MAPFAgent> agents = Agents(((0, 0), (1, 0)), ((1, 0), (2, 0)));

        MAPFSolution solution = Within(() => new TokenPassing(map, agents).Solve());
        AssertValid(map, agents, solution, "TokenPassing");
        Assert.Equal(2, Arrivals(solution, agents));
    }

    [Fact]
    public void AgentStandingOnItsGoal_IsPushedAsideByPibt()
    {
        GridMap map = Grid(3, 2, (0, 1), (2, 1));
        List<MAPFAgent> agents = Agents(((1, 0), (1, 0)), ((0, 0), (2, 0)));

        AssertValid(map, agents, Within(() => new PIBT(map, agents).Solve()), "PIBT");
    }

    [Fact]
    public void PrioritizedPlanners_SolveOpenGrids()
    {
        for (int seed = 0; seed < 10; seed++)
        {
            (GridMap map, List<MAPFAgent> agents) = RandomInstance(100 + seed, 6, 6, walls: 3, count: 5, goalsOnStarts: false);

            if (OptimalLowerBound(map, agents) == int.MaxValue)
                continue;

            foreach ((string name, Func<GridMap, List<MAPFAgent>, MAPFSolution> solve) in Prioritized)
            {
                MAPFSolution solution = Within(() => solve(map, agents));
                AssertValid(map, agents, solution, $"{name}, seed {seed}");
                Assert.True(Arrivals(solution, agents) >= OptimalLowerBound(map, agents));
            }
        }
    }

    #endregion

    #region SIPP

    [Fact]
    public void Sipp_ArrivesOnlyInTheLastSafeIntervalOfTheGoal()
    {
        // Препятствие проходит через цель в момент 3: прийти в момент 1 и остаться нельзя
        var sipp = new SIPP(Grid(3, 1), [(1, 0, 3, 3)]);
        List<(int X, int Y)> path = sipp.FindPath(0, 0, 1, 0);

        Assert.Equal((1, 0), path[^1]);
        Assert.Equal(4, path.Count - 1);
        Assert.DoesNotContain(3, Enumerable.Range(0, path.Count).Where(t => path[t] == (1, 0)));
    }

    [Fact]
    public void Sipp_WaitsForObstacleToPass()
    {
        var obstacles = new List<(int X, int Y, int TimeStart, int TimeEnd)> { (1, 0, 1, 2) };
        List<(int X, int Y)> path = new SIPP(Grid(3, 1), obstacles).FindPath(0, 0, 2, 0);

        Assert.Equal(4, path.Count - 1);

        for (int t = 0; t < path.Count; t++)
            Assert.False(path[t] == (1, 0) && t is >= 1 and <= 2, $"столкновение с препятствием при t={t}");
    }

    #endregion

    #region Инструменты

    private static GridMap Grid(int width, int height, params (int X, int Y)[] walls)
    {
        var map = new GridMap(width, height);

        foreach ((int x, int y) in walls)
            map.SetBlocked(x, y, true);

        return map;
    }

    private static List<MAPFAgent> Agents(params ((int X, int Y) Start, (int X, int Y) Goal)[] specs)
        => specs.Select((s, i) => new MAPFAgent { Id = i, StartX = s.Start.X, StartY = s.Start.Y, GoalX = s.Goal.X, GoalY = s.Goal.Y }).ToList();

    private static (GridMap Map, List<MAPFAgent> Agents) RandomInstance(int seed, int width, int height, int walls, int count, bool goalsOnStarts)
    {
        var rng = new Random(seed);
        List<(int X, int Y)> cells = Enumerable.Range(0, width * height).Select(c => (c % width, c / width)).OrderBy(_ => rng.Next()).ToList();
        GridMap map = Grid(width, height, cells.Take(walls).ToArray());
        List<(int X, int Y)> free = cells.Skip(walls).ToList();

        List<(int X, int Y)> starts = free.Take(count).ToList();
        List<(int X, int Y)> goals = (goalsOnStarts ? free : free.Skip(count)).OrderBy(_ => rng.Next()).Take(count).ToList();

        return (map, Agents(starts.Zip(goals).ToArray()));
    }

    private static void AssertValid(GridMap map, List<MAPFAgent> agents, MAPFSolution solution, string name)
    {
        Assert.True(Violation(map, agents, solution) is null, $"{name}: {Violation(map, agents, solution)}");
        Assert.True(solution.IsValid(map, agents), name);
    }

    /// <summary>Независимый контролёр: описание первого нарушения или null</summary>
    private static string? Violation(GridMap map, List<MAPFAgent> agents, MAPFSolution solution)
    {
        if (solution.Paths.Count != agents.Count)
            return "число путей не равно числу агентов";

        for (int i = 0; i < agents.Count; i++)
        {
            List<(int X, int Y)> path = solution.Paths[i];

            if (path.Count == 0)
                return $"агент {i}: пустой путь";

            if (path[0] != (agents[i].StartX, agents[i].StartY))
                return $"агент {i}: путь начинается не со старта";

            if (path[^1] != (agents[i].GoalX, agents[i].GoalY))
                return $"агент {i}: цель не достигнута";

            for (int t = 0; t < path.Count; t++)
            {
                if (!map.InBounds(path[t].X, path[t].Y) || map.IsBlocked(path[t].X, path[t].Y))
                    return $"агент {i}: непроходимая клетка {path[t]} при t={t}";

                if (t > 0 && Math.Abs(path[t].X - path[t - 1].X) + Math.Abs(path[t].Y - path[t - 1].Y) > 1)
                    return $"агент {i}: прыжок при t={t}";
            }
        }

        int horizon = solution.Paths.Max(p => p.Count);

        for (int t = 0; t < horizon; t++)
        {
            var occupied = new Dictionary<(int X, int Y), int>();
            var moves = new HashSet<((int X, int Y) From, (int X, int Y) To)>();

            for (int i = 0; i < agents.Count; i++)
            {
                (int X, int Y) here = At(solution.Paths[i], t);

                if (!occupied.TryAdd(here, i))
                    return $"агенты {occupied[here]} и {i} в клетке {here} при t={t}";

                if (t == 0)
                    continue;

                (int X, int Y) before = At(solution.Paths[i], t - 1);

                if (before == here)
                    continue;

                if (moves.Contains((here, before)))
                    return $"обмен местами между {before} и {here} при t={t}";

                moves.Add((before, here));
            }
        }

        return null;
    }

    private static (int X, int Y) At(List<(int X, int Y)> path, int t) => t < path.Count ? path[t] : path[^1];

    /// <summary>Сумма моментов, с которых агенты стоят на целях не сходя</summary>
    private static int Arrivals(MAPFSolution solution, List<MAPFAgent> agents)
    {
        int total = 0;

        for (int i = 0; i < agents.Count; i++)
        {
            List<(int X, int Y)> path = solution.Paths[i];
            int t = path.Count - 1;

            while (t > 0 && path[t - 1] == (agents[i].GoalX, agents[i].GoalY))
                t--;

            total += t;
        }

        return total;
    }

    /// <summary>Сумма кратчайших расстояний без учёта других агентов — нижняя оценка</summary>
    private static int OptimalLowerBound(GridMap map, List<MAPFAgent> agents)
    {
        int total = 0;

        foreach (MAPFAgent agent in agents)
        {
            int d = OptimalSoc(map, [agent]);

            if (d == int.MaxValue)
                return int.MaxValue;

            total += d;
        }

        return total;
    }

    /// <summary>
    /// Точный оракул: Дейкстра по совместным состояниям. Агент может в любой момент, стоя на цели,
    /// «закончить» — после этого он неподвижен; каждый шаг стоит числа незакончивших агентов,
    /// поэтому стоимость пути в этом графе равна сумме времён прибытия
    /// </summary>
    private static int OptimalSoc(GridMap map, List<MAPFAgent> agents)
    {
        int n = agents.Count, cells = map.Width * map.Height, all = (1 << n) - 1;
        int Cell(int x, int y) => (x * map.Height) + y;
        int[] goals = agents.Select(a => Cell(a.GoalX, a.GoalY)).ToArray();
        int[] starts = agents.Select(a => Cell(a.StartX, a.StartY)).ToArray();

        long Key(int[] positions, int mask)
        {
            long key = mask;

            foreach (int c in positions)
                key = (key * cells) + c;

            return key;
        }

        var best = new Dictionary<long, int> { [Key(starts, 0)] = 0 };
        var queue = new PriorityQueue<(int[] Positions, int Mask), int>();
        queue.Enqueue((starts, 0), 0);

        while (queue.TryDequeue(out (int[] Positions, int Mask) state, out int cost))
        {
            if (best[Key(state.Positions, state.Mask)] < cost)
                continue;

            if (state.Mask == all)
                return cost;

            for (int i = 0; i < n; i++)
            {
                if ((state.Mask >> i & 1) == 0 && state.Positions[i] == goals[i])
                    Push(state.Positions, state.Mask | (1 << i), cost);
            }

            int step = cost + BitOperations.PopCount((uint)(all & ~state.Mask));
            var next = new int[n];
            Enumerate(0);

            void Enumerate(int i)
            {
                if (i == n)
                {
                    for (int a = 0; a < n; a++)
                    {
                        for (int b = a + 1; b < n; b++)
                        {
                            if (next[a] == next[b] || (next[a] == state.Positions[b] && next[b] == state.Positions[a]))
                                return;
                        }
                    }

                    Push((int[])next.Clone(), state.Mask, step);
                    return;
                }

                int x = state.Positions[i] / map.Height, y = state.Positions[i] % map.Height;
                IEnumerable<(int X, int Y)> options = (state.Mask >> i & 1) == 1 ? [(x, y)] : map.Neighbors(x, y).Append((x, y));

                foreach ((int ox, int oy) in options)
                {
                    next[i] = Cell(ox, oy);
                    Enumerate(i + 1);
                }
            }
        }

        return int.MaxValue;

        void Push(int[] positions, int mask, int cost)
        {
            long key = Key(positions, mask);

            if (!best.TryGetValue(key, out int old) || cost < old)
            {
                best[key] = cost;
                queue.Enqueue((positions, mask), cost);
            }
        }
    }

    private static T Within<T>(Func<T> work, int seconds = 60)
    {
        Task<T> task = Task.Run(work);
        Assert.True(task.Wait(TimeSpan.FromSeconds(seconds)), "решатель завис");

        return task.Result;
    }

    #endregion
}
