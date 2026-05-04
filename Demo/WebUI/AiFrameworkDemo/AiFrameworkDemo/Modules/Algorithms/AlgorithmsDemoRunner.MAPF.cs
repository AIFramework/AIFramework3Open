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
        private static string DoMapfBasic(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int gridSize = I(p, "gridSize", 8);
            int numAgents = I(p, "agents", 3);
            double obsPct = N(p, "obstacles", 15);
            var rng = new Random(I(p, "seed", 42));

            var (map, agents) = BuildMAPFInstance(gridSize, numAgents, obsPct, rng);
            MAPFSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new ECBS(map, agents, 1.5, 5000).Solve(); algoName = "ECBS"; break;
                case 2: sol = new PBS(map, agents, 5000).Solve(); algoName = "PBS"; break;
                default: sol = new CBS(map, agents, 5000).Solve(); algoName = "CBS"; break;
            }

            DrawMAPFSolution(cv, map, agents, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Сетка: {gridSize}×{gridSize}, агентов: {numAgents}");
            sb.AppendLine($"Makespan: {sol.Makespan}");
            sb.AppendLine($"Sum of costs: {sol.SumOfCosts}");
            return sb.ToString();
        }

        private static string DoMapfPriority(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int gridSize = I(p, "gridSize", 8);
            int numAgents = I(p, "agents", 4);
            double obsPct = N(p, "obstacles", 10);
            var rng = new Random(I(p, "seed", 42));

            var (map, agents) = BuildMAPFInstance(gridSize, numAgents, obsPct, rng);
            MAPFSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new TokenPassing(map, agents).Solve(); algoName = "Token Passing"; break;
                default: sol = new PIBT(map, agents, 200).Solve(); algoName = "PIBT"; break;
            }

            DrawMAPFSolution(cv, map, agents, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Сетка: {gridSize}×{gridSize}, агентов: {numAgents}");
            sb.AppendLine($"Makespan: {sol.Makespan}");
            sb.AppendLine($"Sum of costs: {sol.SumOfCosts}");
            return sb.ToString();
        }

        private static string DoMapfLocal(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int gridSize = I(p, "gridSize", 6);
            int numAgents = I(p, "agents", 3);
            var rng = new Random(I(p, "seed", 42));

            var (map, agents) = BuildMAPFInstance(gridSize, numAgents, 0, rng);
            MAPFSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new PushAndRotate(map, agents).Solve(); algoName = "Push & Rotate"; break;
                default: sol = new PushAndSwap(map, agents).Solve(); algoName = "Push & Swap"; break;
            }

            DrawMAPFSolution(cv, map, agents, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Сетка: {gridSize}×{gridSize}, агентов: {numAgents}");
            sb.AppendLine($"Makespan: {sol.Makespan}");
            sb.AppendLine($"Sum of costs: {sol.SumOfCosts}");
            return sb.ToString();
        }

        private static string DoMapfCooperative(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int gridSize = I(p, "gridSize", 8);
            int numAgents = I(p, "agents", 3);
            double obsPct = N(p, "obstacles", 15);
            var rng = new Random(I(p, "seed", 42));

            var (map, agents) = BuildMAPFInstance(gridSize, numAgents, obsPct, rng);
            MAPFSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new HCA(map, agents).Solve(); algoName = "HCA*"; break;
                default: sol = new WHCA(map, agents, 16).Solve(); algoName = "WHCA*"; break;
            }

            DrawMAPFSolution(cv, map, agents, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Сетка: {gridSize}×{gridSize}, агентов: {numAgents}");
            sb.AppendLine($"Makespan: {sol.Makespan}");
            sb.AppendLine($"Sum of costs: {sol.SumOfCosts}");
            return sb.ToString();
        }

        private static string DoMapfLacam(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int gridSize = I(p, "gridSize", 10);
            int numAgents = I(p, "agents", 5);
            double obsPct = N(p, "obstacles", 15);
            var rng = new Random(I(p, "seed", 42));

            var (map, agents) = BuildMAPFInstance(gridSize, numAgents, obsPct, rng);
            MAPFSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new LaCAMStar(map, agents, 10000).Solve(); algoName = "LaCAM*"; break;
                default: sol = new LaCAM(map, agents, 10000).Solve(); algoName = "LaCAM"; break;
            }

            DrawMAPFSolution(cv, map, agents, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Сетка: {gridSize}×{gridSize}, агентов: {numAgents}");
            sb.AppendLine($"Makespan: {sol.Makespan}");
            sb.AppendLine($"Sum of costs: {sol.SumOfCosts}");
            return sb.ToString();
        }
    }
}
