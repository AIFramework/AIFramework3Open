using AI.Algorithms.VRP;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Маршрутизация проверяется независимым контролёром допустимости и полным перебором на малых
/// задачах: каждая эвристика обязана давать допустимое решение не короче оптимума, а улучшающая —
/// не длиннее исходного. На точках окружности оптимум известен в замкнутом виде.
/// </summary>
public class VrpTests
{
    #region Регрессии

    [Fact]
    public void ClarkeWright_DoesNotShatterIntoSingleCustomerRoutes()
    {
        // Прежде лишние маршруты «разбивались пополам», и решение распадалось на четыре поездки к одному клиенту
        VRPInstance inst = Make(0, 0, [(1, 0), (2, 0), (0, 1), (0, -1)], [1, 1, 1, 1], capacity: 2, vehicles: 2);
        VRPSolution solution = new ClarkeWright(inst).Solve();

        Feasible(inst, solution);
        Assert.Equal(2, solution.Routes.Count);
        Assert.Equal(8, solution.TotalDistance(inst), 9);
    }

    [Fact]
    public void ThreeOpt_IsAtLeastAsGoodAsTwoOpt()
    {
        // Прежний «3-opt» разворачивал только первый сегмент и не находил даже хода 2-opt
        VRPInstance inst = Make(0, 0, [(0, 1), (1, 1), (2, 1), (2, 0)]);
        var start = new VRPSolution { Routes = { new List<int> { 0, 1, 3, 2 } } };
        var search = new LocalSearch(inst);

        Assert.Equal(6, search.TwoOpt(start).TotalDistance(inst), 9);
        Assert.Equal(6, search.ThreeOpt(start).TotalDistance(inst), 9);
    }

    [Fact]
    public void LinKernighan_ImprovesShortRoutes()
    {
        // Маршруты до трёх клиентов прежде пропускались
        VRPInstance inst = Make(0, 0, [(0, 1), (1, 1), (1, 0)]);
        var start = new VRPSolution { Routes = { new List<int> { 0, 2, 1 } } };

        Assert.Equal(4, new LinKernighan(inst).Solve(start).TotalDistance(inst), 9);
    }

    [Fact]
    public void Christofides_ConvertsToSolutionAndKeepsGuarantee()
    {
        // Четыре листа на единичной окружности: жадное паросочетание давало тур не короче 6.08 при оптимуме 5.15
        VRPInstance inst = Make(0, 0, [(-0.99619, -0.08716), (-0.51504, 0.85717), (0.51504, 0.85717), (0.99619, -0.08716)]);
        var christofides = new Christofides(inst);
        VRPSolution solution = christofides.SolveAsSolution();

        Feasible(inst, solution);
        Assert.True(christofides.GuaranteeHolds);
        Assert.True(solution.TotalDistance(inst) <= (1.5 * Brute(inst)) + 1e-9);
    }

    [Fact]
    public void Metaheuristics_StayFeasibleAtLargeCoordinates()
    {
        // При постоянном штрафе 10⁶ недопустимый общий маршрут выходил дешевле двух допустимых
        VRPInstance inst = Make(0, 0, [(1e7, 0), (1e7, 1)], [1, 1], capacity: 1, vehicles: 2);

        Feasible(inst, new SimulatedAnnealingVRP(inst, maxIterations: 2_000, seed: 1).Solve());
        Feasible(inst, new GeneticVRP(inst, populationSize: 20, generations: 30, seed: 1).Solve());
    }

    [Fact]
    public void AntColony_RejectsDemandAboveCapacityInsteadOfHanging()
    {
        VRPInstance inst = Make(0, 0, [(1, 0)], [2], capacity: 1);

        bool finished = Task.Run(() => Assert.Throws<ArgumentException>(() => new AntColony(inst, numAnts: 3, maxIterations: 3, seed: 1).Solve()))
            .Wait(TimeSpan.FromSeconds(20));

        Assert.True(finished, "Муравьиная колония зависла");
    }

    #endregion

    #region Сверка с перебором

    [Fact]
    public void PolygonTour_IsOptimalAfterTwoOptThreeOptAndLinKernighan()
    {
        // Депо и клиенты — вершины правильного (N+1)-угольника: оптимальный тур — его периметр
        const int N = 7;
        const double R = 10;
        (double X, double Y)[] points = Enumerable.Range(1, N)
            .Select(k => (R * Math.Cos(2 * Math.PI * k / (N + 1)), R * Math.Sin(2 * Math.PI * k / (N + 1)))).ToArray();
        VRPInstance inst = Make(R, 0, points);
        double optimum = (N + 1) * 2 * R * Math.Sin(Math.PI / (N + 1));

        var random = new Random(3);
        var start = new VRPSolution { Routes = { Enumerable.Range(0, N).OrderBy(_ => random.Next()).ToList() } };
        var search = new LocalSearch(inst);

        Assert.Equal(optimum, search.TwoOpt(start).TotalDistance(inst), 9);
        Assert.Equal(optimum, search.ThreeOpt(start).TotalDistance(inst), 9);
        Assert.Equal(optimum, new LinKernighan(inst).Solve(start).TotalDistance(inst), 9);
    }

    [Fact]
    public void Travelling_AllMethodsFeasibleAndNotBelowOptimum()
    {
        for (int seed = 1; seed <= 15; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(3, 8);
            VRPInstance inst = Make(random.Next(11), random.Next(11),
                Enumerable.Range(0, n).Select(_ => ((double)random.Next(11), (double)random.Next(11))).ToArray());

            double optimum = Brute(inst);

            var christofides = new Christofides(inst);
            VRPSolution tour = christofides.SolveAsSolution();
            Feasible(inst, tour);
            Assert.True(tour.TotalDistance(inst) >= optimum - 1e-9);
            Assert.True(tour.TotalDistance(inst) <= (1.5 * optimum) + 1e-9, $"Зерно {seed}: Кристофидес длиннее полутора оптимумов");

            foreach (VRPSolution solution in Heuristics(inst, seed))
            {
                Feasible(inst, solution);
                Assert.True(solution.TotalDistance(inst) >= optimum - 1e-9, $"Зерно {seed}: решение короче оптимума");
            }
        }
    }

    [Fact]
    public void CapacitatedRouting_AllMethodsFeasibleAndImproversNeverWorsen()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(3, 7);
            VRPInstance inst = Make(5, 5,
                Enumerable.Range(0, n).Select(_ => ((double)random.Next(11), (double)random.Next(11))).ToArray(),
                Enumerable.Range(0, n).Select(_ => (double)random.Next(1, 5)).ToArray(),
                capacity: 7, vehicles: n);

            double optimum = Brute(inst);

            foreach (VRPSolution solution in Heuristics(inst, seed))
            {
                Feasible(inst, solution);
                Assert.True(solution.TotalDistance(inst) >= optimum - 1e-9, $"Зерно {seed}: решение короче оптимума");
            }

            VRPSolution start = new ClarkeWright(inst).Solve();
            double startCost = start.TotalDistance(inst);
            var search = new LocalSearch(inst);

            Assert.True(search.TwoOpt(start).TotalDistance(inst) <= startCost + 1e-9);
            Assert.True(search.OrOpt(start).TotalDistance(inst) <= startCost + 1e-9);
            Assert.True(search.ThreeOpt(start).TotalDistance(inst) <= startCost + 1e-9);
            Assert.True(new LinKernighan(inst).Solve(start).TotalDistance(inst) <= startCost + 1e-9);
            Assert.True(new TabuSearchVRP(inst, 200, seed: seed).Solve(start).TotalDistance(inst) <= startCost + 1e-9);
            Assert.True(new SimulatedAnnealingVRP(inst, maxIterations: 3_000, seed: seed).Solve(start).TotalDistance(inst) <= startCost + 1e-9);
            Assert.True(new ALNS(inst, 500, seed).Solve(start).TotalDistance(inst) <= startCost + 1e-9);
        }
    }

    [Fact]
    public void SameSeed_ReproducesTheSameRoutes()
    {
        VRPInstance inst = Make(5, 5, [(1, 1), (9, 2), (3, 8), (7, 7), (2, 5), (8, 9)], [2, 3, 1, 2, 3, 1], capacity: 6, vehicles: 6);

        static string Key(VRPSolution s) => string.Join("|", s.Routes.Select(r => string.Join(",", r)));

        Assert.Equal(Key(new GeneticVRP(inst, 20, 30, 5).Solve()), Key(new GeneticVRP(inst, 20, 30, 5).Solve()));
        Assert.Equal(Key(new SimulatedAnnealingVRP(inst, seed: 5, maxIterations: 2_000).Solve()), Key(new SimulatedAnnealingVRP(inst, seed: 5, maxIterations: 2_000).Solve()));
        Assert.Equal(Key(new AntColony(inst, 5, 10, seed: 5).Solve()), Key(new AntColony(inst, 5, 10, seed: 5).Solve()));
        Assert.Equal(Key(new ALNS(inst, 300, 5).Solve()), Key(new ALNS(inst, 300, 5).Solve()));
        Assert.Equal(Key(new TabuSearchVRP(inst, 100, 15, 5).Solve()), Key(new TabuSearchVRP(inst, 100, 15, 5).Solve()));
    }

    #endregion

    #region Эталоны

    private static VRPInstance Make(double depotX, double depotY, (double X, double Y)[] points, double[]? demand = null, double capacity = 1e9, int vehicles = 1)
        => new(depotX, depotY, points.Select(p => p.X).ToArray(), points.Select(p => p.Y).ToArray(),
            demand ?? Enumerable.Repeat(1.0, points.Length).ToArray(), capacity, vehicles);

    private static IEnumerable<VRPSolution> Heuristics(VRPInstance inst, int seed)
    {
        yield return new ClarkeWright(inst).Solve();
        yield return new Sweep(inst).Solve();
        yield return new SolomonInsertion(inst).Solve();
        yield return new LinKernighan(inst).Solve();
        yield return new GeneticVRP(inst, 20, 40, seed).Solve();
        yield return new TabuSearchVRP(inst, 200, 15, seed).Solve();
        yield return new AntColony(inst, 8, 20, seed: seed).Solve();
        yield return new SimulatedAnnealingVRP(inst, maxIterations: 3_000, seed: seed).Solve();
        yield return new ALNS(inst, 500, seed).Solve();
    }

    private static double Point(VRPInstance inst, int customer, bool x)
        => customer < 0 ? (x ? inst.DepotX[0] : inst.DepotY[0]) : (x ? inst.CustomerX[customer] : inst.CustomerY[customer]);

    // Расстояние по координатам, а не по матрице экземпляра: контролёр не опирается на проверяемый код
    private static double Distance(VRPInstance inst, int a, int b)
    {
        double dx = Point(inst, a, true) - Point(inst, b, true);
        double dy = Point(inst, a, false) - Point(inst, b, false);

        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double Length(VRPInstance inst, IEnumerable<List<int>> routes)
        => routes.Where(r => r.Count > 0).Sum(r =>
        {
            double sum = Distance(inst, -1, r[0]) + Distance(inst, r[^1], -1);

            for (int k = 1; k < r.Count; k++)
                sum += Distance(inst, r[k - 1], r[k]);

            return sum;
        });

    private static void Feasible(VRPInstance inst, VRPSolution solution)
    {
        var seen = new bool[inst.N];

        foreach (List<int> route in solution.Routes)
        {
            Assert.NotEmpty(route);
            double load = 0;

            foreach (int c in route)
            {
                Assert.InRange(c, 0, inst.N - 1);
                Assert.False(seen[c], $"Клиент {c} посещён дважды");
                seen[c] = true;
                load += inst.Demand[c];
            }

            Assert.True(load <= inst.VehicleCapacity + 1e-9, $"Перегруз маршрута: {load} > {inst.VehicleCapacity}");
        }

        Assert.All(seen, visited => Assert.True(visited, "Клиент не посещён"));
        Assert.Equal(Length(inst, solution.Routes), solution.TotalDistance(inst), 9);
    }

    // Оптимум перебором: все порядки клиентов, все разрезы на маршруты с учётом грузоподъёмности
    private static double Brute(VRPInstance inst)
    {
        double best = double.PositiveInfinity;
        int n = inst.N;
        bool travelling = inst.Demand.Sum() <= inst.VehicleCapacity;

        foreach (int[] order in Permutations(Enumerable.Range(0, n).ToArray(), 0))
        {
            for (int cuts = 0; cuts < (travelling ? 1 : 1 << (n - 1)); cuts++)
            {
                var routes = new List<List<int>> { new() };

                for (int i = 0; i < n; i++)
                {
                    if (i > 0 && ((cuts >> (i - 1)) & 1) == 1)
                        routes.Add(new List<int>());

                    routes[^1].Add(order[i]);
                }

                if (routes.All(r => r.Sum(c => inst.Demand[c]) <= inst.VehicleCapacity + 1e-9))
                    best = Math.Min(best, Length(inst, routes));
            }
        }

        return best;
    }

    private static IEnumerable<int[]> Permutations(int[] items, int k)
    {
        if (k == items.Length)
        {
            yield return (int[])items.Clone();
            yield break;
        }

        for (int i = k; i < items.Length; i++)
        {
            (items[k], items[i]) = (items[i], items[k]);

            foreach (int[] order in Permutations(items, k + 1))
                yield return order;

            (items[k], items[i]) = (items[i], items[k]);
        }
    }

    #endregion
}
