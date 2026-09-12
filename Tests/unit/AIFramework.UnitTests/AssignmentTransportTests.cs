using AI.Algorithms.Matching;
using AI.Algorithms.TransportTask.Methods;
using AI.Algorithms.TransportTask.PlanBuilders;
using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Назначения, паросочетания и транспортная задача сверяются с полным перебором на малых
/// задачах и с линейной программой: транспортная задача и задача о назначениях — частные
/// случаи линейного программирования, и их оптимум обязан совпасть с симплексом.
/// </summary>
public class AssignmentTransportTests
{
    #region Назначения

    [Fact]
    public void Hungarian_TextbookAndRectangularCases()
    {
        Assert.Equal(5, new Hungarian(new double[,] { { 4, 1, 3 }, { 2, 0, 5 }, { 3, 2, 2 } }).TotalCost, 9);
        Assert.Equal(13, new Hungarian(new double[,] { { 9, 2, 7, 8 }, { 6, 4, 3, 7 }, { 5, 8, 1, 8 }, { 7, 6, 9, 4 } }).TotalCost, 9);

        // Строк больше, чем столбцов: прежде лишняя строка получала столбец 0 второй раз
        var tall = new Hungarian(new double[,] { { 5 }, { 1 } });
        Assert.Equal([-1, 0], tall.Assignment);
        Assert.Equal(1, tall.TotalCost, 9);

        _ = Assert.Throws<ArgumentException>(() => new Hungarian(new double[,] { { 1, double.PositiveInfinity }, { 2, 3 } }));
    }

    [Fact]
    public void Assignment_HungarianAndShortestPathsAgreeWithBruteForce()
    {
        for (int seed = 1; seed <= 80; seed++)
        {
            var random = new Random(seed);
            int rows = random.Next(1, 6);
            int cols = random.Next(1, 6);
            var cost = new double[rows, cols];

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    cost[i, j] = random.Next(-5, 10);

            double best = BestAssignment(cost, minimise: true);

            var hungarian = new Hungarian(cost);
            var ssp = new SSPAssignment(cost);

            Assert.True(Math.Abs(best - hungarian.TotalCost) < 1e-9, $"Зерно {seed}: венгерский {hungarian.TotalCost}, перебор {best}");
            Assert.True(Math.Abs(best - ssp.TotalCost) < 1e-9, $"Зерно {seed}: SSP {ssp.TotalCost}, перебор {best}");

            CheckAssignment(hungarian.Assignment, rows, cols);
            CheckAssignment(ssp.Assignment, rows, cols);
        }
    }

    [Fact]
    public void Auction_ReachesTheOptimumForIntegerBenefits()
    {
        for (int seed = 1; seed <= 80; seed++)
        {
            var random = new Random(seed);
            int rows = random.Next(1, 7);
            int cols = random.Next(1, 7);
            var benefit = new double[rows, cols];

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    benefit[i, j] = random.Next(0, 10);

            var auction = new AuctionAlgorithm(benefit);

            Assert.True(Math.Abs(BestAssignment(benefit, minimise: false) - auction.TotalBenefit) < 1e-9, $"Зерно {seed}: аукцион {auction.TotalBenefit}");
            CheckAssignment(auction.Assignment, rows, cols);
        }
    }

    [Fact]
    public void Auction_SmallEpsilonAndPriceWar_DoNotBreakIt()
    {
        // При ε меньше 1/(n+1) фаз прежде не было вовсе, и разбор результата падал
        Assert.Equal(11, new AuctionAlgorithm(new double[,] { { 4, 1, 3 }, { 2, 0, 5 }, { 3, 2, 2 } }, epsilon: 0.1).TotalBenefit, 9);

        // Затяжная ценовая война: прежде предел проходов обрывал её с неназначенным агентом
        bool finished = Task.Run(() =>
            Assert.Equal(2000, new AuctionAlgorithm(new double[,] { { 1000, 1000, 0 }, { 1000, 1000, 0 }, { 1000, 1000, 0 } }).TotalBenefit, 9))
            .Wait(TimeSpan.FromSeconds(20));

        Assert.True(finished, "Аукцион не завершился");
    }

    #endregion

    #region Паросочетания

    [Fact]
    public void BipartiteMatching_ThreeAlgorithmsAgreeWithBruteForce()
    {
        for (int seed = 1; seed <= 80; seed++)
        {
            var random = new Random(seed);
            int left = random.Next(1, 6);
            int right = random.Next(1, 6);
            var edges = new List<(int L, int R)>();

            for (int l = 0; l < left; l++)
                for (int r = 0; r < right; r++)
                    if (random.NextDouble() < 0.4)
                        edges.Add((l, r));

            int expected = MaxMatching(left + right, edges.Select(e => (e.L, left + e.R)).ToList());

            var kuhn = new KuhnMatching(left, right);
            var hopcroftKarp = new HopcroftKarp(left, right);
            var blossom = new EdmondsBlossom(left + right);

            foreach ((int l, int r) in edges)
            {
                kuhn.AddEdge(l, r);
                hopcroftKarp.AddEdge(l, r);
                blossom.AddEdge(l, left + r);
            }

            Assert.Equal(expected, kuhn.Solve());
            Assert.Equal(expected, kuhn.Solve());
            Assert.Equal(expected, hopcroftKarp.MaxMatching());
            Assert.Equal(expected, blossom.MaxMatching());

            for (int l = 0; l < left; l++)
            {
                if (kuhn.MatchLeft[l] >= 0)
                    Assert.Equal(l, kuhn.MatchRight[kuhn.MatchLeft[l]]);

                if (hopcroftKarp.MatchLeft[l] >= 0)
                    Assert.Equal(l, hopcroftKarp.MatchRight[hopcroftKarp.MatchLeft[l]]);
            }
        }
    }

    [Fact]
    public void GeneralMatching_BlossomAgreesWithBruteForce()
    {
        // Пятиугольник — 2, Петерсен — 5, два треугольника с мостом — 3
        Assert.Equal(2, Blossom(5, [(0, 1), (1, 2), (2, 3), (3, 4), (4, 0)]).MaxMatching());
        Assert.Equal(5, Blossom(10, [(0, 1), (1, 2), (2, 3), (3, 4), (4, 0), (0, 5), (1, 6), (2, 7), (3, 8), (4, 9), (5, 7), (7, 9), (9, 6), (6, 8), (8, 5)]).MaxMatching());
        Assert.Equal(3, Blossom(6, [(0, 1), (1, 2), (2, 0), (3, 4), (4, 5), (5, 3), (2, 3)]).MaxMatching());

        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(1, 9);
            var edges = new List<(int U, int V)>();

            for (int u = 0; u < n; u++)
                for (int v = u + 1; v < n; v++)
                    if (random.NextDouble() < 0.35)
                        edges.Add((u, v));

            EdmondsBlossom blossom = Blossom(n, edges);

            Assert.Equal(MaxMatching(n, edges), blossom.MaxMatching());

            for (int v = 0; v < n; v++)
            {
                if (blossom.Match[v] >= 0)
                    Assert.Equal(v, blossom.Match[blossom.Match[v]]);
            }
        }
    }

    [Fact]
    public void StableMarriage_IsStableAndMenOptimal()
    {
        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(1, 6);
            int[][] men = Enumerable.Range(0, n).Select(_ => Enumerable.Range(0, n).OrderBy(_ => random.Next()).ToArray()).ToArray();
            int[][] women = Enumerable.Range(0, n).Select(_ => Enumerable.Range(0, n).OrderBy(_ => random.Next()).ToArray()).ToArray();

            var result = new GaleShapley(men, women);
            int[] partner = result.ManPartner;

            Assert.True(IsStable(partner, men, women), $"Зерно {seed}: есть блокирующая пара");

            // Для каждого мужчины — лучшая партнёрша среди всех устойчивых паросочетаний
            List<int[]> stable = Permutations(n).Where(p => IsStable(p, men, women)).ToList();

            for (int m = 0; m < n; m++)
            {
                int best = stable.Select(p => Array.IndexOf(men[m], p[m])).Min();
                Assert.Equal(best, Array.IndexOf(men[m], partner[m]));
            }
        }
    }

    #endregion

    #region Транспортная задача

    [Fact]
    public void Transport_TextbookInstance_ImprovesVogelToOptimum()
    {
        // Прежде метод потенциалов не делал ни одного шага и возвращал план Фогеля за 779
        var solver = new PotentialMethod(
            new double[,] { { 19, 30, 50, 10 }, { 70, 30, 40, 60 }, { 40, 8, 70, 20 } },
            [7, 9, 18], [5, 8, 7, 14], new VogelApproximationMethod());

        solver.Solve();

        Assert.Equal(743, solver.GetTotalCost(), 9);
        Assert.True(solver.ReachedOptimum);
        Assert.True(solver.IsOptimal(out _, out _, out _));
    }

    [Fact]
    public void Transport_BadStartAndDegeneratePlan_AreHandled()
    {
        // Заданный извне начальный план стоит 103; оптимум — 7
        var fromBadStart = new PotentialMethod(new double[,] { { 1, 2 }, { 3, 100 } }, [2, 1], [1, 2], new FixedPlan(new double[,] { { 1, 1 }, { 0, 1 } }));
        fromBadStart.Solve();
        Assert.Equal(7, fromBadStart.GetTotalCost(), 9);

        // Пример из консольной программы: нулевой спрос делает план вырожденным
        var degenerate = new PotentialMethod(
            new double[,] { { 8.5, 6.0, 10.1, 9.3 }, { 9.2, 12.4, 13.8, 7.6 }, { 14.5, 9.1, 16.3, 5.2 }, { 14.5, 9.1, 16.3, 11.2 } },
            [12, 1000, 100, 2], [101, 1, 1012, 0], new VogelApproximationMethod());
        degenerate.Solve();
        Assert.Equal(LpTransport(degenerate.Costs, degenerate.Supply, degenerate.Demand), degenerate.GetTotalCost(), 6);

        // План с циклом занятых клеток — не опорный; об этом говорится прямо
        var cyclic = new PotentialMethod(new double[,] { { 1, 2 }, { 3, 4 } }, [2, 2], [2, 2], new FixedPlan(new double[,] { { 1, 1 }, { 1, 1 } }));
        _ = Assert.Throws<InvalidOperationException>(cyclic.Solve);
    }

    [Fact]
    public void Transport_RandomInstances_AgreeWithLinearProgram()
    {
        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            int m = random.Next(1, 6);
            int n = random.Next(1, 6);
            var costs = new double[m, n];

            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                    costs[i, j] = random.Next(1, 20);

            double[] supply = Enumerable.Range(0, m).Select(_ => (double)random.Next(1, 15)).ToArray();
            double[] demand = Enumerable.Range(0, n).Select(_ => (double)random.Next(1, 15)).ToArray();

            // Половина задач сбалансирована, половина — нет в ту или другую сторону
            if (seed % 2 == 0)
                demand[^1] += supply.Sum() - demand.Sum();

            if (demand[^1] < 0)
                continue;

            var solver = new PotentialMethod(costs, supply, demand, new VogelApproximationMethod());
            solver.Solve();

            double expected = LpTransport(costs, supply, demand);
            Assert.True(Math.Abs(expected - solver.GetTotalCost()) < 1e-6, $"Зерно {seed}: метод потенциалов {solver.GetTotalCost()}, LP {expected}");
            Assert.True(solver.ReachedOptimum);

            // План допустим: поставщики не отдают больше запаса, а при сбалансированной задаче — ровно запас
            bool surplus = supply.Sum() >= demand.Sum();

            for (int i = 0; i < m; i++)
            {
                double row = Enumerable.Range(0, n).Sum(j => solver.Allocation[i, j]);
                Assert.True(surplus ? row <= supply[i] + 1e-6 : Math.Abs(row - supply[i]) < 1e-6);
            }

            for (int j = 0; j < n; j++)
            {
                double column = Enumerable.Range(0, m).Sum(i => solver.Allocation[i, j]);
                Assert.True(surplus ? Math.Abs(column - demand[j]) < 1e-6 : column <= demand[j] + 1e-6);
            }
        }
    }

    #endregion

    #region Эталоны

    private sealed class FixedPlan(double[,] plan) : IInitialPlanBuilder
    {
        public double[,] BuildInitialPlan(double[,] costs, double[] supply, double[] demand) => (double[,])plan.Clone();
    }

    private static EdmondsBlossom Blossom(int n, IEnumerable<(int U, int V)> edges)
    {
        var blossom = new EdmondsBlossom(n);

        foreach ((int u, int v) in edges)
            blossom.AddEdge(u, v);

        return blossom;
    }

    // Лучшее назначение min(строк, столбцов) пар полным перебором
    private static double BestAssignment(double[,] matrix, bool minimise)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        bool transpose = rows > cols;
        int small = Math.Min(rows, cols);
        int large = Math.Max(rows, cols);
        double best = minimise ? double.PositiveInfinity : double.NegativeInfinity;
        var used = new bool[large];

        void Walk(int k, double sum)
        {
            if (k == small)
            {
                best = minimise ? Math.Min(best, sum) : Math.Max(best, sum);
                return;
            }

            for (int t = 0; t < large; t++)
            {
                if (used[t])
                    continue;

                used[t] = true;
                Walk(k + 1, sum + (transpose ? matrix[t, k] : matrix[k, t]));
                used[t] = false;
            }
        }

        Walk(0, 0);

        return best;
    }

    private static void CheckAssignment(int[] assignment, int rows, int cols)
    {
        int[] taken = assignment.Where(j => j >= 0).ToArray();

        Assert.Equal(Math.Min(rows, cols), taken.Length);
        Assert.Equal(taken.Length, taken.Distinct().Count());
        Assert.All(taken, j => Assert.InRange(j, 0, cols - 1));
    }

    // Наибольшее паросочетание перебором: вершина либо свободна, либо сопоставлена одному из соседей
    private static int MaxMatching(int n, IReadOnlyList<(int U, int V)> edges)
    {
        var memo = new Dictionary<int, int>();

        int Best(int freeMask)
        {
            if (freeMask == 0)
                return 0;

            if (memo.TryGetValue(freeMask, out int known))
                return known;

            int v = System.Numerics.BitOperations.TrailingZeroCount(freeMask);
            int rest = freeMask & ~(1 << v);
            int result = Best(rest);

            foreach ((int a, int b) in edges)
            {
                int other = a == v ? b : b == v ? a : -1;

                if (other >= 0 && (rest >> other & 1) == 1)
                    result = Math.Max(result, 1 + Best(rest & ~(1 << other)));
            }

            memo[freeMask] = result;

            return result;
        }

        return Best((1 << n) - 1);
    }

    private static bool IsStable(int[] partner, int[][] men, int[][] women)
    {
        int n = partner.Length;
        int[] husband = new int[n];

        for (int m = 0; m < n; m++)
            husband[partner[m]] = m;

        for (int m = 0; m < n; m++)
        {
            for (int w = 0; w < n; w++)
            {
                bool manPrefers = Array.IndexOf(men[m], w) < Array.IndexOf(men[m], partner[m]);
                bool womanPrefers = Array.IndexOf(women[w], m) < Array.IndexOf(women[w], husband[w]);

                if (manPrefers && womanPrefers)
                    return false;
            }
        }

        return true;
    }

    private static IEnumerable<int[]> Permutations(int n)
    {
        int[] items = Enumerable.Range(0, n).ToArray();

        IEnumerable<int[]> Walk(int k)
        {
            if (k == n)
            {
                yield return (int[])items.Clone();
                yield break;
            }

            for (int i = k; i < n; i++)
            {
                (items[k], items[i]) = (items[i], items[k]);

                foreach (int[] permutation in Walk(k + 1))
                    yield return permutation;

                (items[k], items[i]) = (items[i], items[k]);
            }
        }

        return Walk(0);
    }

    // Транспортная задача как линейная программа; при избытке запасов вывоз ограничен сверху
    private static double LpTransport(double[,] costs, double[] supply, double[] demand)
    {
        int m = supply.Length;
        int n = demand.Length;
        var program = new LinearProgram();
        var objective = new double[m * n];

        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                program.AddVariable($"x{i}_{j}");
                objective[(i * n) + j] = costs[i, j];
            }
        }

        program.SetObjective(new Vector(objective));
        bool surplus = supply.Sum() >= demand.Sum();

        for (int i = 0; i < m; i++)
        {
            var row = new double[m * n];
            for (int j = 0; j < n; j++)
                row[(i * n) + j] = 1;

            program.AddConstraint(new Vector(row), surplus ? ConstraintSign.LessOrEqual : ConstraintSign.Equal, supply[i]);
        }

        for (int j = 0; j < n; j++)
        {
            var column = new double[m * n];
            for (int i = 0; i < m; i++)
                column[(i * n) + j] = 1;

            program.AddConstraint(new Vector(column), surplus ? ConstraintSign.Equal : ConstraintSign.LessOrEqual, demand[j]);
        }

        LpSolution solution = LpSolver.Solve(program);
        Assert.True(solution.IsOptimal);

        return solution.Objective;
    }

    #endregion
}
