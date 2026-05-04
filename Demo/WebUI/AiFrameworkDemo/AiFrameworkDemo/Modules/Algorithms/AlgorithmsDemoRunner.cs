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
        private static readonly SKColor[] Pal =
        [
            new(0x60, 0xA5, 0xFA), new(0xF8, 0x71, 0x71), new(0x4A, 0xDE, 0x80),
            new(0xFB, 0xBF, 0x24), new(0xA7, 0x8B, 0xFA), new(0x38, 0xBD, 0xF8),
            new(0xFB, 0x92, 0x3C), new(0xF4, 0x72, 0xB6), new(0x34, 0xD3, 0x99),
            new(0xE8, 0x79, 0xF9), new(0x22, 0xD3, 0xEE), new(0xFF, 0xE0, 0x60),
        ];

        private static SKColor C(int i) => Pal[i % Pal.Length];

        public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
        {
            var cv = MakeView(s);
            string? txt = null;

            switch (key)
            {
                case "bfs_dfs":          txt = DoBfsDfs(p, cv); break;
                case "dijkstra_demo":    txt = DoDijkstra(p, cv); break;
                case "bellman_ford":     txt = DoBellmanFord(p, cv); break;
                case "floyd_warshall":   txt = DoFloydWarshall(p, cv); break;
                case "astar":            txt = DoAStar(p, cv); break;
                case "yen_k_shortest":   txt = DoYenK(p, cv); break;
                case "topo_sort_scc":    txt = DoTopoScc(p, cv); break;
                case "mst":              txt = DoMst(p, cv); break;
                case "max_flow":         txt = DoMaxFlow(p, cv); break;
                case "min_cost_flow":    txt = DoMinCostFlow(p, cv); break;
                case "min_cut":          txt = DoMinCut(p, cv); break;
                case "gomory_hu":        txt = DoGomoryHu(p, cv); break;
                case "hungarian":        txt = DoHungarian(p, cv); break;
                case "bipartite_matching": txt = DoBipartiteMatching(p, cv); break;
                case "general_matching": txt = DoGeneralMatching(p, cv); break;
                case "stable_marriage":  txt = DoStableMarriage(p, cv); break;
                case "mapf_basic":       txt = DoMapfBasic(p, cv); break;
                case "mapf_priority":    txt = DoMapfPriority(p, cv); break;
                case "mapf_local":       txt = DoMapfLocal(p, cv); break;
                case "mapf_cooperative": txt = DoMapfCooperative(p, cv); break;
                case "mapf_lacam":       txt = DoMapfLacam(p, cv); break;
                case "transport_task":   txt = DoTransportTask(p, cv); break;
                case "vrp_constructive": txt = DoVrpConstructive(p, cv); break;
                case "tsp_heuristic":    txt = DoTspHeuristic(p, cv); break;
                case "vrp_metaheuristic": txt = DoVrpMeta(p, cv); break;
                case "task_auction":     txt = DoTaskAuction(p, cv); break;
                case "task_dcop":        txt = DoTaskDcop(p, cv); break;
                case "task_cbba":        txt = DoTaskCbba(p, cv); break;
                default:
                    return new DemoResult { Error = $"Неизвестный ключ «{key}»" };
            }

            return Png(cv, s, textOutput: txt);
        }

        #region Helpers

        private static double[] CircleX(int n, double r = 3.0)
        {
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = r * Math.Cos(2 * Math.PI * i / n);
            return x;
        }

        private static double[] CircleY(int n, double r = 3.0)
        {
            var y = new double[n];
            for (int i = 0; i < n; i++) y[i] = r * Math.Sin(2 * Math.PI * i / n);
            return y;
        }

        private static void DrawGraphEdges(ChartView cv, double[] vx, double[] vy, Graph g, SKColor color, int width = 1)
        {
            for (int u = 0; u < g.V; u++)
                foreach (int v in g.Adj(u))
                    if (u < v)
                        DrawLine(cv, vx[u], vy[u], vx[v], vy[v], color, width);
        }

        private static void DrawGraphEdgesW<T>(ChartView cv, double[] vx, double[] vy, GraphW<T> g, SKColor color, int width = 1) where T : BaseEdge, new()
        {
            var drawn = new HashSet<(int, int)>();
            for (int u = 0; u < g.V; u++)
                foreach (var e in g.AdjEW(u))
                {
                    int a = e.StartV, b = e.EndV;
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (drawn.Add(key))
                        DrawLine(cv, vx[a], vy[a], vx[b], vy[b], color, width);
                }
        }

        private static void DrawVertices(ChartView cv, double[] vx, double[] vy, SKColor color)
        {
            var xv = new AI.DataStructs.Algebraic.Vector(vx.Length);
            var yv = new AI.DataStructs.Algebraic.Vector(vy.Length);
            for (int i = 0; i < vx.Length; i++) { xv[i] = vx[i]; yv[i] = vy[i]; }
            cv.AddScatter(xv, yv, "Вершины", color);
        }

        private static void DrawLine(ChartView cv, double x1, double y1, double x2, double y2, SKColor color, int width = 1)
        {
            var xv = new AI.DataStructs.Algebraic.Vector(2) { [0] = x1, [1] = x2 };
            var yv = new AI.DataStructs.Algebraic.Vector(2) { [0] = y1, [1] = y2 };
            cv.AddPlot(xv, yv, "", color, width);
        }

        private static void DrawPath(ChartView cv, double[] vx, double[] vy, List<int> path, SKColor color, int width = 3, string label = "Путь")
        {
            if (path == null || path.Count < 2) return;
            var xv = new AI.DataStructs.Algebraic.Vector(path.Count);
            var yv = new AI.DataStructs.Algebraic.Vector(path.Count);
            for (int i = 0; i < path.Count; i++) { xv[i] = vx[path[i]]; yv[i] = vy[path[i]]; }
            cv.AddPlot(xv, yv, label, color, width);
        }

        private static Graph BuildRandomGraph(int n, double density, Random rng)
        {
            var g = new Graph(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < density)
                        g.AddEdge(i, j);
            return g;
        }

        private static GraphW<Edge> BuildRandomGraphW(int n, double density, Random rng, double maxW = 10, bool allowNeg = false)
        {
            var g = new GraphW<Edge>(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < density)
                    {
                        double w = rng.NextDouble() * maxW + (allowNeg ? -maxW / 2 : 0.5);
                        g.AddEdge(i, j, Math.Round(w, 1));
                    }
            return g;
        }

        private static GraphW<Edge> BuildRandomDirGraphW(int n, double density, Random rng, double maxW = 10, double negFrac = 0)
        {
            var g = new GraphW<Edge>(n);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j && rng.NextDouble() < density)
                    {
                        double w = rng.NextDouble() * maxW + 0.5;
                        if (rng.NextDouble() < negFrac) w = -w * 0.3;
                        g.AddArce(i, j, Math.Round(w, 1));
                    }
            return g;
        }

        private static VRPInstance BuildVRPInstance(int customers, double capacity, int vehicles, Random rng)
        {
            var cx = new double[customers];
            var cy = new double[customers];
            var dem = new double[customers];
            for (int i = 0; i < customers; i++)
            {
                cx[i] = rng.NextDouble() * 20 - 10;
                cy[i] = rng.NextDouble() * 20 - 10;
                dem[i] = rng.Next(1, (int)(capacity / 3) + 1);
            }
            return new VRPInstance(0, 0, cx, cy, dem, capacity, vehicles);
        }

        private static void DrawVRPSolution(ChartView cv, VRPInstance inst, VRPSolution sol)
        {
            for (int r = 0; r < sol.Routes.Count; r++)
            {
                var route = sol.Routes[r];
                if (route.Count == 0) continue;
                int total = route.Count + 2;
                var xv = new AI.DataStructs.Algebraic.Vector(total);
                var yv = new AI.DataStructs.Algebraic.Vector(total);
                xv[0] = 0; yv[0] = 0;
                for (int i = 0; i < route.Count; i++)
                {
                    int ci = route[i];
                    xv[i + 1] = inst.CustomerX[ci];
                    yv[i + 1] = inst.CustomerY[ci];
                }
                xv[total - 1] = 0; yv[total - 1] = 0;
                cv.AddPlot(xv, yv, $"Маршрут {r + 1}", C(r), 2);
            }

            var sx = new AI.DataStructs.Algebraic.Vector(inst.N);
            var sy = new AI.DataStructs.Algebraic.Vector(inst.N);
            for (int i = 0; i < inst.N; i++) { sx[i] = inst.CustomerX[i]; sy[i] = inst.CustomerY[i]; }
            cv.AddScatter(sx, sy, "Клиенты", new SKColor(0xC9, 0xD0, 0xE0));

            var dx = new AI.DataStructs.Algebraic.Vector(1) { [0] = 0 };
            var dy = new AI.DataStructs.Algebraic.Vector(1) { [0] = 0 };
            cv.AddScatter(dx, dy, "Депо", new SKColor(0xFF, 0x44, 0x44));
        }

        private static void DrawMAPFSolution(ChartView cv, GridMap map, List<MAPFAgent> agents, MAPFSolution sol)
        {
            int w = map.Width, h = map.Height;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (map.IsBlocked(x, y))
                    {
                        var bx = new AI.DataStructs.Algebraic.Vector(1) { [0] = x };
                        var by = new AI.DataStructs.Algebraic.Vector(1) { [0] = y };
                        cv.AddScatter(bx, by, "", new SKColor(0x55, 0x55, 0x55));
                    }

            for (int a = 0; a < agents.Count && a < sol.Paths.Count; a++)
            {
                var path = sol.Paths[a];
                if (path.Count < 1) continue;
                var xv = new AI.DataStructs.Algebraic.Vector(path.Count);
                var yv = new AI.DataStructs.Algebraic.Vector(path.Count);
                for (int i = 0; i < path.Count; i++) { xv[i] = path[i].X; yv[i] = path[i].Y; }
                cv.AddPlot(xv, yv, $"Агент {a}", C(a), 2);

                var sx = new AI.DataStructs.Algebraic.Vector(1) { [0] = agents[a].StartX };
                var sy = new AI.DataStructs.Algebraic.Vector(1) { [0] = agents[a].StartY };
                cv.AddScatter(sx, sy, "", C(a));

                var gx = new AI.DataStructs.Algebraic.Vector(1) { [0] = agents[a].GoalX };
                var gy = new AI.DataStructs.Algebraic.Vector(1) { [0] = agents[a].GoalY };
                cv.AddScatter(gx, gy, "", C(a));
            }
        }

        private static (GridMap map, List<MAPFAgent> agents) BuildMAPFInstance(int gridSize, int numAgents, double obsPct, Random rng)
        {
            var map = new GridMap(gridSize, gridSize);
            int obs = (int)(gridSize * gridSize * obsPct / 100.0);
            int placed = 0;
            while (placed < obs)
            {
                int x = rng.Next(gridSize), y = rng.Next(gridSize);
                if (!map.IsBlocked(x, y)) { map.SetBlocked(x, y, true); placed++; }
            }

            var agents = new List<MAPFAgent>();
            for (int a = 0; a < numAgents; a++)
            {
                int sx, sy, gx, gy;
                do { sx = rng.Next(gridSize); sy = rng.Next(gridSize); } while (map.IsBlocked(sx, sy));
                do { gx = rng.Next(gridSize); gy = rng.Next(gridSize); } while (map.IsBlocked(gx, gy) || (gx == sx && gy == sy));
                agents.Add(new MAPFAgent { Id = a, StartX = sx, StartY = sy, GoalX = gx, GoalY = gy });
            }

            return (map, agents);
        }

        private static void DrawTaskAllocation(ChartView cv, List<AgentDef> agents, List<TaskDef> tasks, AllocationResult result)
        {
            var tx = new AI.DataStructs.Algebraic.Vector(tasks.Count);
            var ty = new AI.DataStructs.Algebraic.Vector(tasks.Count);
            for (int i = 0; i < tasks.Count; i++) { tx[i] = tasks[i].X; ty[i] = tasks[i].Y; }
            cv.AddScatter(tx, ty, "Задачи", new SKColor(0xFB, 0xBF, 0x24));

            var ax = new AI.DataStructs.Algebraic.Vector(agents.Count);
            var ay = new AI.DataStructs.Algebraic.Vector(agents.Count);
            for (int i = 0; i < agents.Count; i++) { ax[i] = agents[i].X; ay[i] = agents[i].Y; }
            cv.AddScatter(ax, ay, "Агенты", new SKColor(0x60, 0xA5, 0xFA));

            foreach (var (agentId, taskId) in result.Assignments)
            {
                var agent = agents.Find(a => a.Id == agentId);
                var task = tasks.Find(t => t.Id == taskId);
                if (agent != null && task != null)
                    DrawLine(cv, agent.X, agent.Y, task.X, task.Y, C(agentId), 2);
            }
        }

        #endregion
    }
}
