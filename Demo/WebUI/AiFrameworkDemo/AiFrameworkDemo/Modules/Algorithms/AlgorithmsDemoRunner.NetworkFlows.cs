using AI.Algorithms.EWG;
using AI.Algorithms.GraphStructure;
using AI.Algorithms.Matching;
using AI.Algorithms.MST;
using AI.Algorithms.NetworkFlow;
using AI.Algorithms.MAPF;
using AI.Algorithms.VRP;
using AI.Algorithms.TaskAllocation;
using AI.Algorithms.TransportTask;
using AI.Algorithms.TransportTask.Methods;
using AI.Algorithms.TransportTask.PlanBuilders;
using AI.Charts;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Algorithms
{
    public static partial class AlgorithmsDemoRunner
    {
        private static string DoMaxFlow(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int n = I(p, "n", 8);
            double density = N(p, "density", 0.4);
            var rng = new Random(I(p, "seed", 42));

            var net = new FlowNetwork(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < density)
                    {
                        double cap = rng.Next(1, 15);
                        net.AddEdge(new FlowEdge(i, j, cap));
                    }

            double maxFlow = 0;
            string algoName;
            switch (algoIdx)
            {
                case 1: var ek = new EdmondsKarp(net, 0, n - 1); maxFlow = ek.MaxFlow; algoName = "Edmonds-Karp"; break;
                case 2: var din = new Dinic(net, 0, n - 1); maxFlow = din.MaxFlow; algoName = "Dinic"; break;
                case 3: var pr = new PushRelabel(net, 0, n - 1); maxFlow = pr.MaxFlow; algoName = "Push-Relabel"; break;
                default: var ff = new FordFulkerson(net, 0, n - 1); maxFlow = ff.MaxFlow; algoName = "Ford-Fulkerson"; break;
            }

            var vx = CircleX(n); var vy = CircleY(n);
            foreach (var e in net.AllEdges())
            {
                var col = e.Flow > 0 ? Pal[0] : new SKColor(0x44, 0x44, 0x66);
                int w = e.Flow > 0 ? 2 : 1;
                DrawLine(cv, vx[e.From], vy[e.From], vx[e.To], vy[e.To], col, w);
            }
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Граф: {n} вершин, исток=0, сток={n - 1}");
            sb.AppendLine($"Максимальный поток: {maxFlow:F0}");
            return sb.ToString();
        }

        private static string DoMinCostFlow(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int n = I(p, "n", 6);
            var rng = new Random(I(p, "seed", 42));

            (double flow, double cost) result;
            string algoName;

            switch (algoIdx)
            {
                case 1:
                {
                    var cc = new CycleCanceling(n);
                    AddMinCostEdges(cc, n, rng);
                    result = cc.Solve(0, n - 1);
                    algoName = "Cycle-Canceling";
                    break;
                }
                case 2:
                {
                    var cs = new CostScaling(n);
                    AddMinCostEdges(cs, n, rng);
                    result = cs.Solve(0, n - 1);
                    algoName = "Cost-Scaling";
                    break;
                }
                default:
                {
                    var ssp = new SuccessiveShortestPaths(n);
                    AddMinCostEdges(ssp, n, rng);
                    result = ssp.Solve(0, n - 1);
                    algoName = "SSP";
                    break;
                }
            }

            var vx = CircleX(n); var vy = CircleY(n);
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Поток: {result.flow:F0}");
            sb.AppendLine($"Минимальная стоимость: {result.cost:F1}");
            return sb.ToString();
        }

        private static void AddMinCostEdges(dynamic solver, int n, Random rng)
        {
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < 0.5)
                        solver.AddEdge(i, j, rng.Next(2, 10), rng.NextDouble() * 5 + 0.5);
        }

        private static string DoMinCut(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 8);
            var rng = new Random(I(p, "seed", 42));

            var sw = new StoerWagner(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < 0.45)
                        sw.AddEdge(i, j, rng.Next(1, 10));

            var (minCut, partition) = sw.Solve();

            var vx = CircleX(n); var vy = CircleY(n);
            var partSet = new HashSet<int>(partition);
            for (int i = 0; i < n; i++)
            {
                var px = new AI.DataStructs.Algebraic.Vector(1) { [0] = vx[i] };
                var py = new AI.DataStructs.Algebraic.Vector(1) { [0] = vy[i] };
                cv.AddScatter(px, py, "", partSet.Contains(i) ? Pal[0] : Pal[1]);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Stoer-Wagner: минимальный разрез = {minCut:F0}");
            sb.AppendLine($"Раздел A: [{string.Join(", ", partition)}]");
            return sb.ToString();
        }

        private static string DoGomoryHu(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 6);
            var rng = new Random(I(p, "seed", 42));

            var gh = new GomoryHu(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < 0.55)
                        gh.AddEdge(i, j, rng.Next(1, 10));
            gh.Build();

            var vx = CircleX(n); var vy = CircleY(n);
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var sb = new StringBuilder();
            sb.AppendLine($"Дерево Гомори—Ху ({n} вершин):");
            sb.AppendLine("Попарные мин. разрезы:");
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    sb.AppendLine($"  mincut({i},{j}) = {gh.MinCut(i, j):F0}");
            return sb.ToString();
        }
    }
}
