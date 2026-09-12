using AI.Algorithms.DynamicPathfinding;
using AI.Algorithms.EWG;
using AI.Algorithms.GraphStructure;
using AI.Algorithms.MST;
using AI.Algorithms.PriorityQueues;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Алгоритмы на графах сверяются с эталонами, написанными здесь же независимо от библиотеки:
/// расстояния — с Беллманом — Фордом на списке дуг, остовы — с Крускалом на списке рёбер,
/// компоненты, мосты и точки сочленения — с полным перебором, пути на решётке — с Дейкстрой
/// на стандартной очереди. Библиотечные алгоритмы друг другу эталоном не служат: общая
/// очередь с приоритетом ошибалась у всех сразу.
/// </summary>
public class GraphAlgorithmsTests
{
    #region Очередь с приоритетом

    [Fact]
    public void IndexQueue_UpdateAfterExtraction_KeepsTheRightElement()
    {
        var queue = new IndexPriorityQueueMin<double>(4);
        queue.Insert(10, 1);
        queue.Insert(20, 5);

        Assert.Equal(10, queue.DelMinGetIndex());

        // Прежде обновление писало в освободившуюся ячейку, и извлекался не тот элемент
        queue.Update(20, 0.5);
        queue.Insert(30, 2);

        Assert.Equal(20, queue.DelMinGetIndex());
        Assert.Equal(30, queue.DelMinGetIndex());
        Assert.True(queue.IsEmpty());
    }

    [Fact]
    public void IndexQueue_RandomOperations_AgreeWithNaiveModel()
    {
        var random = new Random(7);
        var queue = new IndexPriorityQueueMin<double>(4);
        var model = new Dictionary<int, double>();
        int next = 0;

        for (int step = 0; step < 5_000; step++)
        {
            int operation = random.Next(3);

            if (operation == 0 || model.Count == 0)
            {
                double priority = random.Next(100);
                queue.Insert(next, priority);
                model[next++] = priority;
            }
            else if (operation == 1)
            {
                int key = model.Keys.ElementAt(random.Next(model.Count));
                double priority = random.Next(100);
                queue.Update(key, priority);
                model[key] = priority;
            }
            else
            {
                (int index, double priority) = queue.DelMin();

                Assert.Equal(model.Values.Min(), priority);
                Assert.Equal(model[index], priority);
                model.Remove(index);
            }

            Assert.Equal(model.Count, queue.Size());
        }
    }

    #endregion

    #region Кратчайшие пути

    [Fact]
    public void Dijkstra_TextbookNetwork_MatchesKnownDistances()
    {
        // Кормен и др., рис. 24.6: s, t, x, y, z = 0…4
        GraphW<Edge> graph = Arcs(5, [(0, 1, 10), (0, 3, 5), (1, 2, 1), (1, 3, 2), (3, 1, 3), (3, 2, 9), (3, 4, 2), (2, 4, 4), (4, 2, 6), (4, 0, 7)]);

        Assert.Equal([0.0, 8, 9, 5, 7], new DijkstraSPath<Edge>(graph, 0).Distances);

        // Прежде A* на этой сети терял вершину в очереди и не находил пути вовсе
        var astar = new AStarSearch<Edge>(graph, 0, 4, _ => 0);
        Assert.True(astar.Found);
        Assert.Equal(7, astar.PathCost, 9);

        // Тот же случай на пяти вершинах: вершина 3 пропадала из очереди
        GraphW<Edge> lost = Arcs(5, [(0, 2, 10), (0, 1, 1), (1, 2, 1), (1, 3, 5), (3, 4, 1)]);
        Assert.Equal(7, new DijkstraSPath<Edge>(lost, 0).Distances[4], 9);
    }

    [Fact]
    public void ShortestPaths_AllSingleSourceMethodsAgreeWithOracle()
    {
        for (int seed = 1; seed <= 80; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(2, 10);
            (int U, int V, double W)[] arcs = RandomArcs(random, n, random.Next(0, 3 * n), 1, 9);
            GraphW<Edge> graph = Arcs(n, arcs);
            double[] oracle = Oracle(n, arcs, 0);
            int target = n - 1;
            double expected = oracle[target];

            Assert.Equal(oracle, Normalize(new DijkstraSPath<Edge>(graph, 0).Distances));
            Assert.Equal(oracle, Normalize(new BellmanFordSP<Edge>(graph, 0).Distances));

            var bidirectional = new BidirectionalDijkstra<Edge>(graph, 0, target);
            CheckPath(arcs, 0, target, expected, bidirectional.Found, bidirectional.PathCost, bidirectional.GetPath(), $"Зерно {seed}, двунаправленный");

            var astar = new AStarSearch<Edge>(graph, 0, target, _ => 0);
            CheckPath(arcs, 0, target, expected, astar.Found, astar.PathCost, astar.GetPath(), $"Зерно {seed}, A*");

            var yen = new YenKShortestPaths<Edge>(graph, 0, target, 1);
            Assert.Equal(double.IsFinite(expected), yen.Paths.Count == 1);

            if (yen.Paths.Count == 1)
                CheckPath(arcs, 0, target, expected, true, yen.Paths[0].Cost, yen.Paths[0].Path, $"Зерно {seed}, Йен");

            // Итеративное углубление на недостижимой цели в графе с циклами по определению не кончается
            if (!double.IsFinite(expected))
                continue;

            var ida = new IDAStarSearch<Edge>(graph, 0, target, _ => 0);
            CheckPath(arcs, 0, target, expected, ida.Found, ida.PathCost, ida.Path, $"Зерно {seed}, IDA*");

            var fringe = new FringeSearch<Edge>(graph, 0, target, _ => 0);
            CheckPath(arcs, 0, target, expected, fringe.Found, fringe.PathCost, fringe.Path, $"Зерно {seed}, Fringe");
        }
    }

    [Fact]
    public void AllPairs_AgreeWithOracle_IncludingNegativeArcs()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(2, 8);

            // Веса через потенциалы: base + p(u) − p(v) бывают отрицательными, но циклы остаются положительными
            int[] potential = Enumerable.Range(0, n).Select(_ => random.Next(0, 7)).ToArray();
            (int U, int V, double W)[] arcs = RandomArcs(random, n, random.Next(0, 3 * n), 1, 6)
                .Select(a => (a.U, a.V, a.W + potential[a.U] - potential[a.V])).ToArray();

            GraphW<Edge> graph = Arcs(n, arcs);
            var floyd = new FloydWarshall<Edge>(graph);
            var johnson = new JohnsonAllPairs<Edge>(graph);

            Assert.False(johnson.HasNegativeCycle);

            for (int s = 0; s < n; s++)
            {
                double[] oracle = Oracle(n, arcs, s);

                Assert.Equal(oracle, Normalize(new DijkstraSPath<Edge>(graph, s).Distances));

                for (int v = 0; v < n; v++)
                {
                    Assert.True(Same(oracle[v], floyd.Dist[s, v]), $"Зерно {seed}: Флойд {s}→{v} = {floyd.Dist[s, v]}, ожидалось {oracle[v]}");
                    Assert.True(Same(oracle[v], johnson.DistanceBetween(s, v)), $"Зерно {seed}: Джонсон {s}→{v}");

                    // Деревья Джонсона отдают исходные рёбра, а не перевзвешенные
                    if (v != s && double.IsFinite(oracle[v]))
                        Assert.Equal(oracle[v], johnson.Trees[s].GetPath(v).Sum(e => e.W), 9);
                }
            }
        }
    }

    [Fact]
    public void NegativeCycle_IsReportedWithoutHanging()
    {
        GraphW<Edge> graph = Arcs(4, [(0, 1, 1), (1, 2, -1), (2, 1, -1)]);

        bool finished = Task.Run(() =>
        {
            var bellmanFord = new BellmanFordSP<Edge>(graph, 0);
            Assert.True(bellmanFord.HasNegativeCycle);
            Assert.Null(bellmanFord.PathTo(1));
            Assert.False(new BellmanFordSP<Edge>(graph, 3).HasNegativeCycle);

            var johnson = new JohnsonAllPairs<Edge>(graph);
            Assert.True(johnson.HasNegativeCycle);
            Assert.True(double.IsNaN(johnson.DistanceBetween(0, 2)));

            var floyd = new FloydWarshall<Edge>(graph);
            Assert.True(floyd.Dist[1, 1] < 0);
            _ = floyd.PathBetween(0, 2);

            _ = Assert.Throws<InvalidOperationException>(() => new DijkstraSPath<Edge>(graph, 0));
        }).Wait(TimeSpan.FromSeconds(20));

        Assert.True(finished, "Алгоритм завис на отрицательном цикле");
    }

    [Fact]
    public void ParallelArcs_AndInconsistentHeuristic_AreHandled()
    {
        // Fringe падал на параллельных дугах, где тяжёлая шла раньше лёгкой
        var fringe = new FringeSearch<Edge>(Arcs(2, [(0, 1, 5), (0, 1, 3), (0, 1, 5)]), 0, 1, _ => 0);
        Assert.True(fringe.Found);
        Assert.Equal(3, fringe.PathCost, 9);

        // Йен считал стоимость корня по первой попавшейся из параллельных дуг
        var yen = new YenKShortestPaths<Edge>(Arcs(4, [(0, 1, 5), (0, 1, 1), (0, 1, 5), (1, 2, 1), (1, 3, 1), (3, 2, 1)]), 0, 2, 2);
        Assert.Equal([2.0, 3.0], yen.Paths.Select(p => p.Cost));
        Assert.Empty(new YenKShortestPaths<Edge>(Arcs(2, [(0, 1, 1)]), 0, 1, 0).Paths);

        // Допустимая, но несогласованная эвристика: без переоткрытия вершин A* нашёл бы путь 8 вместо 6
        var astar = new AStarSearch<Edge>(Arcs(4, [(0, 1, 1), (0, 2, 4), (1, 2, 1), (2, 3, 4)]), 0, 3, v => v == 1 ? 5 : 0);
        Assert.Equal(6, astar.PathCost, 9);

        Assert.Equal([0], new FloydWarshall<Edge>(Arcs(2, [(0, 1, 1)])).PathBetween(0, 0));
    }

    [Fact]
    public void KShortestPaths_AgreeWithEnumerationOfSimplePaths()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(3, 8);
            (int U, int V, double W)[] arcs = RandomArcs(random, n, random.Next(n, 3 * n), 1, 9);
            List<double> all = SimplePathCosts(n, arcs, 0, n - 1);

            var yen = new YenKShortestPaths<Edge>(Arcs(n, arcs), 0, n - 1, 4);
            double[] expected = all.OrderBy(c => c).Take(4).ToArray();

            Assert.Equal(expected.Length, yen.Paths.Count);

            for (int k = 0; k < expected.Length; k++)
            {
                Assert.True(Math.Abs(expected[k] - yen.Paths[k].Cost) < 1e-9, $"Зерно {seed}: путь {k} стоит {yen.Paths[k].Cost}, ожидалось {expected[k]}");
                Assert.Equal(yen.Paths[k].Path.Count, yen.Paths[k].Path.Distinct().Count());
            }

            Assert.Equal(yen.Paths.Count, yen.Paths.Select(p => string.Join(",", p.Path)).Distinct().Count());
        }
    }

    [Fact]
    public void BreadthFirst_MatchesUnitWeightDistances()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(2, 10);
            var plain = new Graph(n);
            var arcs = new List<(int U, int V, double W)>();

            for (int k = 0; k < 2 * n; k++)
            {
                int u = random.Next(n);
                int v = random.Next(n);
                plain.AddEdge(u, v);
                arcs.Add((u, v, 1));
                arcs.Add((v, u, 1));
            }

            double[] oracle = Oracle(n, arcs.ToArray(), 0);
            int[] bfs = new BFS(plain, 0).DistanceTo;

            for (int v = 0; v < n; v++)
                Assert.Equal(double.IsFinite(oracle[v]) ? oracle[v] : -1, bfs[v]);
        }
    }

    #endregion

    #region Устройство графов

    [Fact]
    public void GraphTypes_AdjacencyAndReverse_AreCorrect()
    {
        GraphW<Edge> weighted = Arcs(3, [(0, 1, 1), (0, 2, 1), (2, 0, 1)]);

        // Adj писал все соседи в одну ячейку, а на вершине с одной дугой падал
        Assert.Equal([1, 2], weighted.Adj(0).OrderBy(v => v));
        Assert.Equal([0], weighted.Adj(2));

        // Обращение прежде обходило новый, пустой граф и возвращало граф без дуг
        Graph reversed = Arcs(3, [(0, 1, 1), (1, 2, 1)]).Reverse();
        Assert.Equal(2, reversed.Arcs);
        Assert.Equal([0], reversed.Adj(1));
        Assert.Equal([1], reversed.Adj(2));

        var plain = new Graph(3);
        plain.AddArc(0, 1);
        plain.AddArc(1, 2);
        Assert.Equal([0], plain.Reverse().Adj(1));

        // Ребро поверх существующей дуги не дублирует её в списке смежности
        var mixed = new Graph(2);
        mixed.AddArc(0, 1);
        mixed.AddEdge(0, 1);
        Assert.Equal([1], mixed.Adj(0));
        Assert.Equal([0], mixed.Adj(1));
    }

    #endregion

    #region Остовные деревья

    [Fact]
    public void MinimumSpanningTree_TextbookAndRegressions()
    {
        // Кормен и др., рис. 23.1: вес 37
        GraphW<Edge> clrs = Undirected(9, [(0, 1, 4), (0, 7, 8), (1, 2, 8), (1, 7, 11), (2, 3, 7), (2, 5, 4), (2, 8, 2),
            (3, 4, 9), (3, 5, 14), (4, 5, 10), (5, 6, 2), (6, 7, 1), (6, 8, 6), (7, 8, 7)]);

        Assert.Equal(37, new Kruskal<Edge>(clrs).TotalWeight, 9);
        Assert.Equal(37, new Prim<Edge>(clrs).TotalWeight, 9);
        Assert.Equal(37, new Boruvka<Edge>(clrs).TotalWeight, 9);

        // Параллельные рёбра: прежде Крускал и Борувка брали первое, а не лёгкое
        GraphW<Edge> parallel = Undirected(2, [(0, 1, 5), (0, 1, 1)]);
        Assert.Equal(1, new Kruskal<Edge>(parallel).TotalWeight, 9);
        Assert.Equal(1, new Boruvka<Edge>(parallel).TotalWeight, 9);

        // Несвязный граф: Прим строил дерево только одной компоненты
        Assert.Equal(3, new Prim<Edge>(Undirected(4, [(0, 1, 1), (2, 3, 2)])).TotalWeight, 9);
    }

    [Fact]
    public void MinimumSpanningForest_AllAlgorithmsAgreeWithOracle()
    {
        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(1, 10);
            var edges = new (int U, int V, double W)[random.Next(0, 3 * n)];

            for (int k = 0; k < edges.Length; k++)
                edges[k] = (random.Next(n), random.Next(n), random.Next(1, 21));

            (double weight, int count) = KruskalOracle(n, edges);
            GraphW<Edge> graph = Undirected(n, edges);

            var kruskal = new Kruskal<Edge>(graph);
            var prim = new Prim<Edge>(graph);
            var boruvka = new Boruvka<Edge>(graph);

            Assert.True(Math.Abs(weight - kruskal.TotalWeight) < 1e-9, $"Зерно {seed}: Крускал {kruskal.TotalWeight}, эталон {weight}");
            Assert.True(Math.Abs(weight - prim.TotalWeight) < 1e-9, $"Зерно {seed}: Прим {prim.TotalWeight}, эталон {weight}");
            Assert.True(Math.Abs(weight - boruvka.TotalWeight) < 1e-9, $"Зерно {seed}: Борувка {boruvka.TotalWeight}, эталон {weight}");

            Assert.Equal(count, kruskal.MSTEdges.Count);
            Assert.Equal(count, prim.MSTEdges().Count());
            Assert.Equal(count, boruvka.MSTEdges.Count);
        }
    }

    #endregion

    #region Структура графа

    [Fact]
    public void StronglyConnectedComponents_AgreeWithMutualReachability()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(1, 10);
            (int U, int V)[] arcs = Enumerable.Range(0, random.Next(0, 3 * n)).Select(_ => (random.Next(n), random.Next(n))).ToArray();
            Graph graph = Directed(n, arcs);
            bool[,] reach = Reachability(n, arcs);

            var kosaraju = new KosarajuSCC(graph);
            var tarjan = new TarjanSCC(graph);

            for (int u = 0; u < n; u++)
            {
                for (int v = 0; v < n; v++)
                {
                    bool together = reach[u, v] && reach[v, u];

                    Assert.Equal(together, kosaraju.ComponentId[u] == kosaraju.ComponentId[v]);
                    Assert.Equal(together, tarjan.ComponentId[u] == tarjan.ComponentId[v]);
                }
            }

            // Топологическая сортировка: цикл есть ровно тогда, когда есть нетривиальная компонента или петля
            bool cyclic = arcs.Any(a => a.U == a.V) || Enumerable.Range(0, n).Any(u => Enumerable.Range(0, n).Any(v => u != v && reach[u, v] && reach[v, u]));
            var sort = new TopologicalSort(graph);

            Assert.Equal(cyclic, sort.HasCycle);

            if (!cyclic)
            {
                int[] position = new int[n];
                for (int k = 0; k < n; k++)
                    position[sort.Order[k]] = k;

                Assert.All(arcs, a => Assert.True(position[a.U] < position[a.V]));
            }
        }
    }

    [Fact]
    public void BridgesAndArticulationPoints_AgreeWithBruteForce()
    {
        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(1, 9);
            var set = new HashSet<(int, int)>();

            for (int k = 0; k < random.Next(0, 2 * n); k++)
            {
                int u = random.Next(n);
                int v = random.Next(n);

                if (u != v)
                    set.Add((Math.Min(u, v), Math.Max(u, v)));
            }

            (int U, int V)[] edges = set.ToArray();
            var graph = new Graph(n);
            foreach ((int u, int v) in edges)
                graph.AddEdge(u, v);

            var result = new ArticulationBridges(graph);
            int baseline = Components(n, edges, -1);

            var bridges = edges.Where(e => !Connected(n, edges.Where(x => x != e), e.U, e.V)).ToHashSet();
            Assert.True(bridges.SetEquals(result.Bridges.Select(b => (Math.Min(b.U, b.V), Math.Max(b.U, b.V)))), $"Зерно {seed}: мосты");

            var points = Enumerable.Range(0, n)
                .Where(v => Components(n, edges, v) > baseline - (edges.Any(e => e.U == v || e.V == v) ? 0 : 1))
                .ToHashSet();
            Assert.True(points.SetEquals(result.ArticulationPoints), $"Зерно {seed}: точки сочленения");
        }
    }

    [Fact]
    public void TwoSat_AssignmentSatisfiesEveryClause()
    {
        // Единственная импликация ¬x → x: прежде подстановка выходила обратной
        var single = new TwoSAT(1);
        single.AddClause(1, 1);
        Assert.True(single.Solve());
        Assert.True(single.Assignment[0]);

        for (int seed = 1; seed <= 80; seed++)
        {
            var random = new Random(seed);
            int n = random.Next(1, 9);
            var clauses = Enumerable.Range(0, random.Next(1, 3 * n))
                .Select(_ => (Literal(random, n), Literal(random, n))).ToArray();

            var solver = new TwoSAT(n);
            foreach ((int a, int b) in clauses)
                solver.AddClause(a, b);

            bool Holds(int mask, int literal) => ((mask >> (Math.Abs(literal) - 1)) & 1) == 1 == literal > 0;
            bool brute = Enumerable.Range(0, 1 << n).Any(mask => clauses.All(c => Holds(mask, c.Item1) || Holds(mask, c.Item2)));

            Assert.Equal(brute, solver.Solve());

            if (brute)
            {
                bool Value(int literal) => solver.Assignment[Math.Abs(literal) - 1] == literal > 0;
                Assert.All(clauses, c => Assert.True(Value(c.Item1) || Value(c.Item2), $"Зерно {seed}: дизъюнкт ({c.Item1} ∨ {c.Item2}) не выполнен"));
            }
        }
    }

    #endregion

    #region Инкрементальный поиск

    [Fact]
    public void DynamicGridPlanners_AgreeWithDijkstraAfterEveryChange()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            var random = new Random(seed);
            const int Size = 8;
            var blocked = new bool[Size, Size];
            (int X, int Y) start = (0, 0);
            (int X, int Y) goal = (Size - 1, Size - 1);

            var dstar = new DStarLite(Size, Size, start, goal);
            var lpa = new LPAStar(Size, Size, start, goal);
            lpa.ComputeShortestPath();

            for (int change = 0; change < 25; change++)
            {
                int x = random.Next(Size);
                int y = random.Next(Size);

                if ((x, y) == start || (x, y) == goal)
                    continue;

                blocked[x, y] = !blocked[x, y];
                dstar.SetBlocked(x, y, blocked[x, y]);
                dstar.Replan();
                lpa.UpdateEdgeCost(x, y, blocked[x, y]);
                lpa.ComputeShortestPath();

                double expected = GridDistance(blocked, start, goal);

                Assert.True(Same(expected, GridPathCost(dstar.GetPath().Select(c => (c.Item1, c.Item2)), blocked, start, goal)), $"Зерно {seed}, шаг {change}: D* Lite");
                Assert.True(Same(expected, GridPathCost(lpa.GetPath().Select(c => (c.Item1, c.Item2)), blocked, start, goal)), $"Зерно {seed}, шаг {change}: LPA*");
            }
        }
    }

    [Fact]
    public void AnytimeAStar_RespectsBoundAndEndsOptimal()
    {
        for (int seed = 1; seed <= 15; seed++)
        {
            var random = new Random(seed);
            const int Size = 7;
            var free = new bool[Size, Size];

            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    free[x, y] = random.NextDouble() > 0.25 || (x, y) == (0, 0) || (x, y) == (Size - 1, Size - 1);

            var graph = new GraphW<Edge>(Size * Size);
            var arcs = new List<(int U, int V, double W)>();

            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    if (!free[x, y])
                        continue;

                    if (x + 1 < Size && free[x + 1, y])
                    {
                        graph.AddEdge((x * Size) + y, ((x + 1) * Size) + y, 1);
                        arcs.Add(((x * Size) + y, ((x + 1) * Size) + y, 1));
                        arcs.Add((((x + 1) * Size) + y, (x * Size) + y, 1));
                    }

                    if (y + 1 < Size && free[x, y + 1])
                    {
                        graph.AddEdge((x * Size) + y, (x * Size) + y + 1, 1);
                        arcs.Add(((x * Size) + y, (x * Size) + y + 1, 1));
                        arcs.Add(((x * Size) + y + 1, (x * Size) + y, 1));
                    }
                }
            }

            int goal = (Size * Size) - 1;
            double optimum = Oracle(Size * Size, arcs.ToArray(), 0)[goal];

            if (!double.IsFinite(optimum))
                continue;

            double Manhattan(int v) => Math.Abs((v / Size) - (Size - 1)) + Math.Abs((v % Size) - (Size - 1));

            var ara = new ARAStar<Edge>(graph, 0, goal, Manhattan, 3.0);
            ara.ImprovePath();
            Assert.True(ara.PathCost <= (ara.Epsilon * optimum) + 1e-9, $"Зерно {seed}: {ara.PathCost} > {ara.Epsilon}·{optimum}");

            while (ara.Epsilon > 1)
            {
                ara.DecreaseEpsilon(1.0);
                ara.ImprovePath();
            }

            Assert.Equal(optimum, ara.PathCost, 9);
        }
    }

    #endregion

    #region Эталоны и построители

    private static GraphW<Edge> Arcs(int n, IEnumerable<(int U, int V, double W)> arcs)
    {
        var graph = new GraphW<Edge>(n);

        foreach ((int u, int v, double w) in arcs)
            graph.AddArce(u, v, w);

        return graph;
    }

    private static GraphW<Edge> Undirected(int n, IEnumerable<(int U, int V, double W)> edges)
    {
        var graph = new GraphW<Edge>(n);

        foreach ((int u, int v, double w) in edges)
            graph.AddEdge(u, v, w);

        return graph;
    }

    private static Graph Directed(int n, IEnumerable<(int U, int V)> arcs)
    {
        var graph = new Graph(n);

        foreach ((int u, int v) in arcs)
            graph.AddArc(u, v);

        return graph;
    }

    private static (int U, int V, double W)[] RandomArcs(Random random, int n, int count, int low, int high)
        => Enumerable.Range(0, count).Select(_ => (random.Next(n), random.Next(n), (double)random.Next(low, high + 1))).ToArray();

    // Беллман — Форд на списке дуг: эталон, не зависящий от библиотеки
    private static double[] Oracle(int n, (int U, int V, double W)[] arcs, int source)
    {
        double[] distance = Enumerable.Repeat(double.PositiveInfinity, n).ToArray();
        distance[source] = 0;

        for (int pass = 0; pass < n; pass++)
        {
            foreach ((int u, int v, double w) in arcs)
            {
                if (distance[u] + w < distance[v])
                    distance[v] = distance[u] + w;
            }
        }

        return distance;
    }

    private static double[] Normalize(double[] distances)
        => distances.Select(d => d >= double.MaxValue / 2 ? double.PositiveInfinity : d).ToArray();

    private static bool Same(double expected, double actual)
    {
        bool infinite = actual >= double.MaxValue / 2;

        return double.IsFinite(expected) ? !infinite && Math.Abs(expected - actual) < 1e-9 : infinite;
    }

    private static void CheckPath(
        (int U, int V, double W)[] arcs, int source, int target, double expected, bool found, double cost, IReadOnlyList<int> path, string label)
    {
        Assert.True(found == double.IsFinite(expected), $"{label}: найден {found}, ожидалось {expected}");

        if (!found)
            return;

        Assert.True(Math.Abs(expected - cost) < 1e-9, $"{label}: стоимость {cost} вместо {expected}");
        Assert.Equal(source, path[0]);
        Assert.Equal(target, path[^1]);

        double sum = 0;

        for (int k = 1; k < path.Count; k++)
        {
            double[] options = arcs.Where(a => a.U == path[k - 1] && a.V == path[k]).Select(a => a.W).ToArray();
            Assert.True(options.Length > 0, $"{label}: дуги {path[k - 1]}→{path[k]} нет");
            sum += options.Min();
        }

        Assert.True(Math.Abs(expected - sum) < 1e-9, $"{label}: путь по дугам стоит {sum}");
    }

    private static List<double> SimplePathCosts(int n, (int U, int V, double W)[] arcs, int source, int target)
    {
        var costs = new List<double>();
        var onPath = new bool[n];

        void Walk(int v, double cost)
        {
            if (v == target)
            {
                costs.Add(cost);
                return;
            }

            onPath[v] = true;

            foreach (int w in arcs.Where(a => a.U == v && a.V != v).Select(a => a.V).Distinct())
            {
                if (!onPath[w])
                    Walk(w, cost + arcs.Where(a => a.U == v && a.V == w).Min(a => a.W));
            }

            onPath[v] = false;
        }

        Walk(source, 0);

        return costs;
    }

    private static (double Weight, int Count) KruskalOracle(int n, (int U, int V, double W)[] edges)
    {
        int[] parent = Enumerable.Range(0, n).ToArray();

        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);

        double weight = 0;
        int count = 0;

        foreach ((int u, int v, double w) in edges.OrderBy(e => e.W))
        {
            int a = Find(u);
            int b = Find(v);

            if (a != b)
            {
                parent[a] = b;
                weight += w;
                count++;
            }
        }

        return (weight, count);
    }

    private static bool[,] Reachability(int n, (int U, int V)[] arcs)
    {
        var reach = new bool[n, n];

        for (int v = 0; v < n; v++)
            reach[v, v] = true;

        foreach ((int u, int v) in arcs)
            reach[u, v] = true;

        for (int k = 0; k < n; k++)
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    reach[i, j] |= reach[i, k] && reach[k, j];

        return reach;
    }

    private static int Components(int n, IEnumerable<(int U, int V)> edges, int removed)
    {
        int[] parent = Enumerable.Range(0, n).ToArray();

        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);

        foreach ((int u, int v) in edges)
        {
            if (u != removed && v != removed)
                parent[Find(u)] = Find(v);
        }

        return Enumerable.Range(0, n).Where(v => v != removed).Select(Find).Distinct().Count();
    }

    private static bool Connected(int n, IEnumerable<(int U, int V)> edges, int a, int b)
    {
        int[] parent = Enumerable.Range(0, n).ToArray();

        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);

        foreach ((int u, int v) in edges)
            parent[Find(u)] = Find(v);

        return Find(a) == Find(b);
    }

    private static int Literal(Random random, int n) => (random.Next(n) + 1) * (random.Next(2) == 0 ? 1 : -1);

    // Дейкстра на решётке с восемью направлениями: шаг стоит 1 или √2, заблокированная клетка недоступна
    private static double GridDistance(bool[,] blocked, (int X, int Y) start, (int X, int Y) goal)
    {
        int width = blocked.GetLength(0);
        int height = blocked.GetLength(1);
        var distance = new double[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                distance[x, y] = double.PositiveInfinity;

        var queue = new PriorityQueue<(int X, int Y), double>();
        distance[start.X, start.Y] = 0;
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out (int X, int Y) cell, out double d))
        {
            if (d > distance[cell.X, cell.Y])
                continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = cell.X + dx;
                    int y = cell.Y + dy;

                    if ((dx == 0 && dy == 0) || x < 0 || y < 0 || x >= width || y >= height)
                        continue;

                    if (blocked[x, y] || blocked[cell.X, cell.Y])
                        continue;

                    double next = d + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);

                    if (next < distance[x, y] - 1e-12)
                    {
                        distance[x, y] = next;
                        queue.Enqueue((x, y), next);
                    }
                }
            }
        }

        return distance[goal.X, goal.Y];
    }

    private static double GridPathCost(IEnumerable<(int X, int Y)> path, bool[,] blocked, (int X, int Y) start, (int X, int Y) goal)
    {
        (int X, int Y)[] cells = path.ToArray();

        if (cells.Length == 0)
            return double.MaxValue;

        Assert.Equal(start, cells[0]);
        Assert.Equal(goal, cells[^1]);

        double cost = 0;

        for (int k = 1; k < cells.Length; k++)
        {
            int dx = Math.Abs(cells[k].X - cells[k - 1].X);
            int dy = Math.Abs(cells[k].Y - cells[k - 1].Y);

            Assert.True(dx <= 1 && dy <= 1 && dx + dy > 0, "Соседние клетки пути не смежны");
            Assert.False(blocked[cells[k].X, cells[k].Y], "Путь проходит через препятствие");

            cost += dx + dy == 2 ? Math.Sqrt(2) : 1;
        }

        return cost;
    }

    #endregion
}
