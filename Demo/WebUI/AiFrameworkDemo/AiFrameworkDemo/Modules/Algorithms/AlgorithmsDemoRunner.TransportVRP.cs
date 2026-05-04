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
        private static string DoTransportTask(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int rows = I(p, "suppliers", 3);
            int cols = I(p, "consumers", 4);
            int maxCost = I(p, "maxCost", 15);
            int maxSupply = I(p, "maxSupply", 50);
            var rng = new Random(I(p, "seed", 42));

            var costs = new double[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    costs[i, j] = rng.Next(1, maxCost + 1);

            var supply = new double[rows];
            var demand = new double[cols];
            double totalSupply = 0, totalDemand = 0;
            for (int i = 0; i < rows; i++) { supply[i] = rng.Next(10, maxSupply + 1); totalSupply += supply[i]; }
            for (int j = 0; j < cols; j++) { demand[j] = rng.Next(10, maxSupply + 1); totalDemand += demand[j]; }

            if (totalSupply > totalDemand)
                demand[cols - 1] += totalSupply - totalDemand;
            else if (totalDemand > totalSupply)
                supply[rows - 1] += totalDemand - totalSupply;

            var pm = new PotentialMethod(costs, supply, demand, new VogelApproximationMethod());
            pm.Solve();

            var barX = new AI.DataStructs.Algebraic.Vector(rows * cols);
            var barY = new AI.DataStructs.Algebraic.Vector(rows * cols);
            int idx = 0;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    barX[idx] = idx;
                    barY[idx] = pm.Allocation[i, j];
                    idx++;
                }
            cv.AddPlot(barX, barY, "Распределение", Pal[0], 2);

            var sb = new StringBuilder();
            sb.AppendLine("Матрица стоимостей:");
            for (int i = 0; i < rows; i++)
            {
                var row = new List<string>();
                for (int j = 0; j < cols; j++) row.Add($"{costs[i, j],4:F0}");
                sb.AppendLine("  " + string.Join(" ", row));
            }

            sb.AppendLine($"\nПредложение: [{string.Join(", ", supply.Select(s => s.ToString("F0")))}]");
            sb.AppendLine($"Спрос:       [{string.Join(", ", demand.Select(d => d.ToString("F0")))}]");

            sb.AppendLine("\nОптимальное распределение:");
            for (int i = 0; i < rows; i++)
            {
                var row = new List<string>();
                for (int j = 0; j < cols; j++) row.Add($"{pm.Allocation[i, j],6:F0}");
                sb.AppendLine("  " + string.Join(" ", row));
            }
            sb.AppendLine($"\nОбщая стоимость: {pm.GetTotalCost():F0}");
            sb.AppendLine($"Средняя стоимость: {pm.GetMeanCost():F1}");

            return sb.ToString();
        }

        private static string DoVrpConstructive(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int customers = I(p, "customers", 12);
            int vehicles = I(p, "vehicles", 3);
            double capacity = N(p, "capacity", 50);
            var rng = new Random(I(p, "seed", 42));

            var inst = BuildVRPInstance(customers, capacity, vehicles, rng);
            VRPSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new Sweep(inst).Solve(); algoName = "Sweep"; break;
                case 2: sol = new SolomonInsertion(inst).Solve(); algoName = "Solomon I1"; break;
                default: sol = new ClarkeWright(inst).Solve(); algoName = "Clarke-Wright"; break;
            }

            DrawVRPSolution(cv, inst, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Клиентов: {customers}, ТС: {vehicles}, ёмкость: {capacity}");
            sb.AppendLine($"Маршрутов: {sol.Routes.Count}");
            sb.AppendLine($"Общее расстояние: {sol.TotalDistance(inst):F1}");
            sb.AppendLine($"Валидно: {sol.IsValid(inst)}");
            return sb.ToString();
        }

        private static string DoTspHeuristic(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int cities = I(p, "cities", 15);
            var rng = new Random(I(p, "seed", 42));

            var inst = BuildVRPInstance(cities, 1e9, 1, rng);
            string algoName;
            VRPSolution sol;

            switch (algoIdx)
            {
                case 1:
                {
                    var lk = new LinKernighan(inst);
                    sol = lk.Solve();
                    algoName = "Lin-Kernighan";
                    break;
                }
                case 2:
                {
                    var chris = new Christofides(inst);
                    var tour = chris.SolveTSP();
                    sol = new VRPSolution { Routes = new List<List<int>> { tour } };
                    algoName = "Christofides";
                    break;
                }
                default:
                {
                    var cw = new ClarkeWright(inst).Solve();
                    var ls = new LocalSearch(inst);
                    sol = ls.TwoOpt(cw);
                    algoName = "2-opt";
                    break;
                }
            }

            DrawVRPSolution(cv, inst, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Городов: {cities}");
            sb.AppendLine($"Расстояние: {sol.TotalDistance(inst):F1}");
            return sb.ToString();
        }

        private static string DoVrpMeta(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int customers = I(p, "customers", 15);
            int vehicles = I(p, "vehicles", 3);
            double capacity = N(p, "capacity", 50);
            var rng = new Random(I(p, "seed", 42));

            var inst = BuildVRPInstance(customers, capacity, vehicles, rng);
            VRPSolution sol;
            string algoName;

            switch (algoIdx)
            {
                case 1: sol = new TabuSearchVRP(inst, 500, 15, I(p, "seed", 42)).Solve(); algoName = "Tabu Search"; break;
                case 2: sol = new AntColony(inst, 20, 100, seed: I(p, "seed", 42)).Solve(); algoName = "ACO"; break;
                case 3: sol = new SimulatedAnnealingVRP(inst, seed: I(p, "seed", 42)).Solve(); algoName = "SA"; break;
                case 4: sol = new ALNS(inst, 1000, I(p, "seed", 42)).Solve(); algoName = "ALNS"; break;
                default: sol = new GeneticVRP(inst, 50, 200, I(p, "seed", 42)).Solve(); algoName = "GA"; break;
            }

            DrawVRPSolution(cv, inst, sol);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Клиентов: {customers}, ТС: {vehicles}");
            sb.AppendLine($"Маршрутов: {sol.Routes.Count}");
            sb.AppendLine($"Общее расстояние: {sol.TotalDistance(inst):F1}");
            sb.AppendLine($"Валидно: {sol.IsValid(inst)}");
            return sb.ToString();
        }

        private static (List<AgentDef> agents, List<TaskDef> tasks) BuildTaskAllocInstance(int numAgents, int numTasks, Random rng)
        {
            var agents = new List<AgentDef>();
            for (int i = 0; i < numAgents; i++)
                agents.Add(new AgentDef { Id = i, X = rng.NextDouble() * 10, Y = rng.NextDouble() * 10, Capacity = 3 });

            var tasks = new List<TaskDef>();
            for (int i = 0; i < numTasks; i++)
                tasks.Add(new TaskDef { Id = i, X = rng.NextDouble() * 10, Y = rng.NextDouble() * 10, Value = rng.Next(1, 10) });

            return (agents, tasks);
        }

        private static string DoTaskAuction(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int numAgents = I(p, "agents", 4);
            int numTasks = I(p, "tasks", 8);
            var rng = new Random(I(p, "seed", 42));

            var (agents, tasks) = BuildTaskAllocInstance(numAgents, numTasks, rng);
            AllocationResult result;
            string algoName;

            switch (algoIdx)
            {
                case 1: result = new SSIAuction(agents, tasks).Solve(); algoName = "SSI Auction"; break;
                case 2: result = new SequentialAuction(agents, tasks).Solve(); algoName = "Sequential Auction"; break;
                default: result = new ContractNet(agents, tasks).Solve(); algoName = "Contract Net"; break;
            }

            DrawTaskAllocation(cv, agents, tasks, result);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Агентов: {numAgents}, задач: {numTasks}");
            sb.AppendLine($"Назначено: {result.Assignments.Count}");
            sb.AppendLine($"Общая стоимость: {result.TotalCost:F1}");
            sb.AppendLine($"Не назначено задач: {result.UnassignedTasks}");
            return sb.ToString();
        }

        private static string DoTaskDcop(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int numAgents = I(p, "agents", 4);
            int numTasks = I(p, "tasks", 6);
            var rng = new Random(I(p, "seed", 42));

            var (agents, tasks) = BuildTaskAllocInstance(numAgents, numTasks, rng);
            AllocationResult result;
            string algoName;

            switch (algoIdx)
            {
                case 1: result = new DPOP(agents, tasks).Solve(); algoName = "DPOP"; break;
                case 2: result = new MaxSum(agents, tasks, 50).Solve(); algoName = "Max-Sum"; break;
                case 3: result = new DSA(agents, tasks, seed: I(p, "seed", 42)).Solve(); algoName = "DSA"; break;
                case 4: result = new MGM(agents, tasks, 50).Solve(); algoName = "MGM"; break;
                default: result = new ADOPT(agents, tasks, 200).Solve(); algoName = "ADOPT"; break;
            }

            DrawTaskAllocation(cv, agents, tasks, result);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Агентов: {numAgents}, задач: {numTasks}");
            sb.AppendLine($"Назначено: {result.Assignments.Count}");
            sb.AppendLine($"Общая стоимость: {result.TotalCost:F1}");
            return sb.ToString();
        }

        private static string DoTaskCbba(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int numAgents = I(p, "agents", 4);
            int numTasks = I(p, "tasks", 8);
            int capacity = I(p, "capacity", 2);
            var rng = new Random(I(p, "seed", 42));

            var (agents, tasks) = BuildTaskAllocInstance(numAgents, numTasks, rng);
            foreach (var a in agents) a.Capacity = capacity;

            AllocationResult result;
            string algoName;

            switch (algoIdx)
            {
                case 1: result = new GreedyAllocation(agents, tasks).Solve(); algoName = "Greedy"; break;
                default: result = new CBBA(agents, tasks, 100).Solve(); algoName = "CBBA"; break;
            }

            DrawTaskAllocation(cv, agents, tasks, result);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Агентов: {numAgents}, задач: {numTasks}, ёмкость: {capacity}");
            sb.AppendLine($"Назначено: {result.Assignments.Count}");
            sb.AppendLine($"Общая стоимость: {result.TotalCost:F1}");
            sb.AppendLine($"Не назначено задач: {result.UnassignedTasks}");
            return sb.ToString();
        }
    }
}
