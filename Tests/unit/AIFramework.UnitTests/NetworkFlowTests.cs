using AI.Algorithms.NetworkFlow;
using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Потоки в сетях проверяются перекрёстно: все алгоритмы одной задачи обязаны давать один ответ,
/// ответ — совпадать с линейной программой, а величина потока — с пропускной способностью
/// найденного разреза. Минимальные разрезы сверяются с полным перебором разбиений.
/// </summary>
public class NetworkFlowTests
{
    // Сеть из учебника Кормена — Лейзерсона — Ривеста — Штайна, с встречными рёбрами 1↔2; поток 23
    private static readonly (int U, int V, double C)[] Clrs =
    [
        (0, 1, 16), (0, 2, 13), (1, 2, 10), (2, 1, 4), (1, 3, 12),
        (3, 2, 9), (2, 4, 14), (4, 3, 7), (3, 5, 20), (4, 5, 4),
    ];

    #region Максимальный поток

    [Fact]
    public void MaxFlow_TextbookNetwork_Is23ForEveryAlgorithm()
    {
        Assert.Equal(23, new EdmondsKarp(Network(6, Clrs), 0, 5).MaxFlow, 9);
        Assert.Equal(23, new FordFulkerson(Network(6, Clrs), 0, 5).MaxFlow, 9);
        Assert.Equal(23, new Dinic(Network(6, Clrs), 0, 5).MaxFlow, 9);
        Assert.Equal(23, new PushRelabel(Network(6, Clrs), 0, 5).MaxFlow, 9);

        // Единственный минимальный разрез: исток на стороне {0, 1, 2, 4}
        var karp = new EdmondsKarp(Network(6, Clrs), 0, 5);
        Assert.Equal([0, 1, 2, 4], Enumerable.Range(0, 6).Where(karp.InCut));
    }

    [Fact]
    public void MaxFlow_AllAlgorithmsAgreeWithLinearProgramAndMinCut()
    {
        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(2, 8);
            (int U, int V, double C)[] edges = RandomEdges(random, n, random.Next(0, 3 * n), 5)
                .Select(e => (e.U, e.V, (double)e.Capacity)).ToArray();

            double lp = LpMaxFlow(n, edges, 0, n - 1);
            var karp = new EdmondsKarp(Network(n, edges), 0, n - 1);
            FlowNetwork dinicNetwork = Network(n, edges);
            var dinic = new Dinic(dinicNetwork, 0, n - 1);

            Assert.Equal(lp, karp.MaxFlow, 9);
            Assert.Equal(lp, new FordFulkerson(Network(n, edges), 0, n - 1).MaxFlow, 9);
            Assert.Equal(lp, dinic.MaxFlow, 9);
            Assert.Equal(lp, new PushRelabel(Network(n, edges), 0, n - 1).MaxFlow, 9);

            // Теорема Форда — Фалкерсона: пропускная способность разреза по достижимости равна потоку
            double cut = edges.Where(e => karp.InCut(e.U) && !karp.InCut(e.V)).Sum(e => e.C);
            Assert.Equal(lp, cut, 9);

            // Сам поток допустим: в пределах пропускных способностей и сохраняется во внутренних вершинах
            var balance = new double[n];

            foreach (FlowEdge edge in dinicNetwork.AllEdges().Distinct())
            {
                Assert.InRange(edge.Flow, -1e-9, edge.Capacity + 1e-9);
                balance[edge.From] -= edge.Flow;
                balance[edge.To] += edge.Flow;
            }

            for (int v = 1; v < n - 1; v++)
                Assert.Equal(0, balance[v], 9);
        }
    }

    #endregion

    #region Поток минимальной стоимости

    [Theory]
    [MemberData(nameof(CostInstances))]
    public void MinCostFlow_TextbookInstances(int n, (int U, int V, int Capacity, double Cost)[] edges, double flow, double cost)
    {
        foreach (string algorithm in new[] { "ssp", "cc", "cs" })
        {
            (double actualFlow, double actualCost) = MinCostFlow(algorithm, n, edges, 0, n - 1);

            Assert.True(Math.Abs(flow - actualFlow) < 1e-9, $"{algorithm}: поток {actualFlow} вместо {flow}");
            Assert.True(Math.Abs(cost - actualCost) < 1e-9, $"{algorithm}: стоимость {actualCost} вместо {cost}");
        }
    }

    public static IEnumerable<object[]> CostInstances()
    {
        yield return [5, new[] { (0, 1, 4, 1.0), (0, 2, 3, 2.0), (1, 3, 2, 3.0), (2, 3, 5, 1.0), (3, 4, 6, 2.0) }, 5.0, 27.0];

        // Кратчайший путь второй единицы идёт по обратной дуге 2 → 1
        yield return [4, new[] { (0, 1, 1, 1.0), (0, 2, 1, 3.0), (1, 2, 1, 1.0), (1, 3, 1, 3.0), (2, 3, 1, 1.0) }, 2.0, 8.0];

        // Здесь отмена циклов проводила поток через дугу в исток и насчитывала поток 2
        yield return [6, new[] { (0, 1, 1, 0.0), (1, 2, 1, 10.0), (2, 3, 1, 0.0), (1, 4, 1, 0.0), (4, 0, 1, 0.0), (0, 5, 1, 0.0), (5, 2, 1, 0.0) }, 1.0, 0.0];

        // Отрицательная стоимость без отрицательного цикла
        yield return [3, new[] { (0, 1, 2, -1.0), (1, 2, 2, 2.0), (0, 2, 1, 3.0) }, 3.0, 5.0];
    }

    [Fact]
    public void MinCostFlow_AllAlgorithmsAgreeWithLinearProgram()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(2, 8);
            (int U, int V, int Capacity, double Cost)[] edges = RandomEdges(random, n, random.Next(0, 3 * n), 5)
                .Select(e => (e.U, e.V, e.Capacity, (double)random.Next(0, 10))).ToArray();

            double maxFlow = LpMaxFlow(n, edges.Select(e => (e.U, e.V, (double)e.Capacity)).ToArray(), 0, n - 1);
            double cost = LpMinCost(n, edges, 0, n - 1, maxFlow);

            foreach (string algorithm in new[] { "ssp", "cc", "cs" })
            {
                (double actualFlow, double actualCost) = MinCostFlow(algorithm, n, edges, 0, n - 1);

                Assert.True(Math.Abs(maxFlow - actualFlow) < 1e-9, $"Зерно {seed}, {algorithm}: поток {actualFlow} вместо {maxFlow}");
                Assert.True(Math.Abs(cost - actualCost) < 1e-6, $"Зерно {seed}, {algorithm}: стоимость {actualCost} вместо {cost}");
            }
        }
    }

    [Fact]
    public void CostScaling_HandlesFractionalCosts()
    {
        (int U, int V, int Capacity, double Cost)[] edges = [(0, 1, 3, 0.3), (0, 2, 2, 0.1), (2, 1, 2, 0.15), (1, 3, 4, 0.7), (2, 3, 1, 1.3)];

        (double flow, double cost) reference = MinCostFlow("ssp", 4, edges, 0, 3);
        (double flow, double cost) scaled = MinCostFlow("cs", 4, edges, 0, 3);

        Assert.Equal(reference.flow, scaled.flow, 9);
        Assert.Equal(reference.cost, scaled.cost, 5);
    }

    [Fact]
    public void NegativeCycle_IsCancelledOrReported()
    {
        // Цикл 0 ↔ 1 стоимостью −2 выгодно насытить, хотя к стоку он ничего не добавляет
        (int U, int V, int Capacity, double Cost)[] edges = [(0, 2, 1, 0.0), (0, 1, 1, -1.0), (1, 0, 1, -1.0)];

        Assert.Equal((1.0, -2.0), MinCostFlow("cc", 3, edges, 0, 2));
        Assert.Equal((1.0, -2.0), MinCostFlow("cs", 3, edges, 0, 2));

        // Метод кратчайших путей такие сети не решает — и говорит об этом, а не зависает
        _ = Assert.Throws<InvalidOperationException>(() => MinCostFlow("ssp", 3, edges, 0, 2));
    }

    [Fact]
    public void RepeatedSolve_GivesTheSameAnswer()
    {
        var ssp = new SuccessiveShortestPaths(5);
        foreach ((int u, int v, int c, double w) in new[] { (0, 1, 4, 1.0), (0, 2, 3, 2.0), (1, 3, 2, 3.0), (2, 3, 5, 1.0), (3, 4, 6, 2.0) })
            ssp.AddEdge(u, v, c, w);

        Assert.Equal(ssp.Solve(0, 4), ssp.Solve(0, 4));
    }

    [Fact]
    public void SourceEqualsSink_ReturnsZeroInsteadOfHanging()
    {
        (int U, int V, int Capacity, double Cost)[] edges = [(0, 1, 2, 1.0), (1, 2, 2, 1.0)];

        bool finished = Task.Run(() =>
        {
            Assert.Equal(0, new Dinic(Network(3, [(0, 1, 2.0), (1, 2, 2.0)]), 1, 1).MaxFlow);
            Assert.Equal(0, new EdmondsKarp(Network(3, [(0, 1, 2.0), (1, 2, 2.0)]), 1, 1).MaxFlow);
            Assert.Equal(0, new PushRelabel(Network(3, [(0, 1, 2.0), (1, 2, 2.0)]), 1, 1).MaxFlow);

            foreach (string algorithm in new[] { "ssp", "cc", "cs" })
                Assert.Equal((0.0, 0.0), MinCostFlow(algorithm, 3, edges, 1, 1));
        }).Wait(TimeSpan.FromSeconds(20));

        Assert.True(finished, "Алгоритм завис на задаче с совпадающими истоком и стоком");
    }

    #endregion

    #region Минимальные разрезы

    [Fact]
    public void StoerWagner_TextbookExamples()
    {
        // На четырёх вершинах ключи прежде росли по стартовой вершине, и разрез выходил нулевым
        var small = new StoerWagner(4);
        small.AddEdge(0, 1, 5);
        small.AddEdge(0, 2, 1);
        small.AddEdge(1, 3, 1);
        Assert.Equal(1, small.Solve().MinCut, 9);

        // Пример из статьи Штёра и Вагнера: разрез 4, доля {2, 3, 6, 7}
        var paper = new StoerWagner(8);
        foreach ((int u, int v, double w) in new[] { (0, 1, 2.0), (0, 4, 3.0), (1, 2, 3.0), (1, 4, 2.0), (1, 5, 2.0), (2, 3, 4.0),
                                                     (2, 6, 2.0), (3, 6, 2.0), (3, 7, 2.0), (4, 5, 3.0), (5, 6, 1.0), (6, 7, 3.0) })
            paper.AddEdge(u, v, w);

        (double cut, List<int> side) = paper.Solve();
        Assert.Equal(4, cut, 9);

        var expected = new HashSet<int> { 2, 3, 6, 7 };
        Assert.True(expected.SetEquals(side) || expected.SetEquals(Enumerable.Range(0, 8).Except(side)));
    }

    [Fact]
    public void Cuts_AgreeWithBruteForceAndPairwiseFlows()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(2, 8);
            (int U, int V, double W)[] edges = RandomEdges(random, n, random.Next(1, 3 * n), 5)
                .Select(e => (e.U, e.V, (double)Math.Max(1, e.Capacity))).ToArray();

            var stoerWagner = new StoerWagner(n);
            var gomoryHu = new GomoryHu(n);

            foreach ((int u, int v, double w) in edges)
            {
                stoerWagner.AddEdge(u, v, w);
                gomoryHu.AddEdge(u, v, w);
            }

            gomoryHu.Build();

            // Полный перебор разбиений: вершина 0 всегда на одной стороне
            double brute = double.PositiveInfinity;

            for (int mask = 1; mask < (1 << n) - 1; mask += 2)
                brute = Math.Min(brute, CutWeight(edges, v => (mask >> v & 1) == 1));

            (double cut, List<int> side) = stoerWagner.Solve();

            Assert.True(Math.Abs(brute - cut) < 1e-9, $"Зерно {seed}: Штёр — Вагнер {cut}, перебор {brute}");
            Assert.Equal(cut, CutWeight(edges, side.Contains), 9);

            double pairwiseMinimum = double.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double pair = gomoryHu.MinCut(i, j);
                    pairwiseMinimum = Math.Min(pairwiseMinimum, pair);

                    // Каждый разрез дерева — это максимальный поток между парой в неориентированной сети
                    var undirected = edges.SelectMany(e => new[] { (e.U, e.V, e.W), (e.V, e.U, e.W) }).ToArray();
                    Assert.Equal(new EdmondsKarp(Network(n, undirected), i, j).MaxFlow, pair, 9);

                    // И разбиение, которое дерево называет, действительно даёт этот разрез
                    List<int> partition = gomoryHu.MinCutPartition(i, j);
                    Assert.True(partition.Contains(i) != partition.Contains(j), $"Зерно {seed}: доля не разделяет {i} и {j}");
                    Assert.True(Math.Abs(pair - CutWeight(edges, partition.Contains)) < 1e-9,
                        $"Зерно {seed}: доля даёт разрез {CutWeight(edges, partition.Contains)} вместо {pair}");
                }
            }

            Assert.Equal(cut, pairwiseMinimum, 9);
        }
    }

    #endregion

    #region Вспомогательное

    private static FlowNetwork Network(int n, IEnumerable<(int U, int V, double C)> edges)
    {
        var network = new FlowNetwork(n);

        foreach ((int u, int v, double c) in edges)
            network.AddEdge(new FlowEdge(u, v, c));

        return network;
    }

    private static (int U, int V, int Capacity)[] RandomEdges(Random random, int n, int count, int maxCapacity)
    {
        var edges = new (int U, int V, int Capacity)[count];

        for (int k = 0; k < count; k++)
        {
            int u = random.Next(n);
            int v = random.Next(n - 1);

            if (v >= u)
                v++;

            edges[k] = (u, v, random.Next(0, maxCapacity + 1));
        }

        return edges;
    }

    private static (double flow, double cost) MinCostFlow(
        string algorithm, int n, (int U, int V, int Capacity, double Cost)[] edges, int s, int t)
    {
        switch (algorithm)
        {
            case "ssp":
            {
                var solver = new SuccessiveShortestPaths(n);
                foreach ((int u, int v, int c, double w) in edges)
                    solver.AddEdge(u, v, c, w);
                return solver.Solve(s, t);
            }

            case "cc":
            {
                var solver = new CycleCanceling(n);
                foreach ((int u, int v, int c, double w) in edges)
                    solver.AddEdge(u, v, c, w);
                return solver.Solve(s, t);
            }

            default:
            {
                var solver = new CostScaling(n);
                foreach ((int u, int v, int c, double w) in edges)
                    solver.AddEdge(u, v, c, w);
                return solver.Solve(s, t);
            }
        }
    }

    // Максимальный поток как линейная программа: независимая проверка всех четырёх алгоритмов
    private static double LpMaxFlow(int n, (int U, int V, double C)[] edges, int s, int t)
    {
        if (edges.Length == 0)
            return 0;

        var program = new LinearProgram(ObjectiveSense.Maximize);

        for (int k = 0; k < edges.Length; k++)
            program.AddVariable($"f{k}", 0, edges[k].C);

        var objective = new double[edges.Length];

        for (int k = 0; k < edges.Length; k++)
        {
            if (edges[k].U == s)
                objective[k] += 1;
            if (edges[k].V == s)
                objective[k] -= 1;
        }

        program.SetObjective(new Vector(objective));
        AddConservation(program, n, edges.Select(e => (e.U, e.V)).ToArray(), s, t);

        LpSolution solution = LpSolver.Solve(program);
        Assert.True(solution.IsOptimal);

        return solution.Objective;
    }

    private static double LpMinCost(int n, (int U, int V, int Capacity, double Cost)[] edges, int s, int t, double flow)
    {
        if (edges.Length == 0)
            return 0;

        var program = new LinearProgram();

        for (int k = 0; k < edges.Length; k++)
            program.AddVariable($"f{k}", 0, edges[k].Capacity);

        program.SetObjective(new Vector(edges.Select(e => e.Cost).ToArray()));
        AddConservation(program, n, edges.Select(e => (e.U, e.V)).ToArray(), s, t);

        var value = new double[edges.Length];

        for (int k = 0; k < edges.Length; k++)
        {
            if (edges[k].U == s)
                value[k] += 1;
            if (edges[k].V == s)
                value[k] -= 1;
        }

        program.AddConstraint(new Vector(value), ConstraintSign.Equal, flow);

        LpSolution solution = LpSolver.Solve(program);
        Assert.True(solution.IsOptimal);

        return solution.Objective;
    }

    private static void AddConservation(LinearProgram program, int n, (int U, int V)[] edges, int s, int t)
    {
        for (int v = 0; v < n; v++)
        {
            if (v == s || v == t)
                continue;

            var row = new double[edges.Length];

            for (int k = 0; k < edges.Length; k++)
            {
                if (edges[k].V == v)
                    row[k] += 1;
                if (edges[k].U == v)
                    row[k] -= 1;
            }

            if (row.Any(a => a != 0))
                program.AddConstraint(new Vector(row), ConstraintSign.Equal, 0);
        }
    }

    private static double CutWeight((int U, int V, double W)[] edges, Func<int, bool> side)
        => edges.Where(e => side(e.U) != side(e.V)).Sum(e => e.W);

    #endregion
}
