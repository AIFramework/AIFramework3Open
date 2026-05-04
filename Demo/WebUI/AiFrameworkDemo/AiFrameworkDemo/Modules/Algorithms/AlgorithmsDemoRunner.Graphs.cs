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
        private static string DoBfsDfs(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 12);
            double density = N(p, "density", 0.25);
            var rng = new Random(I(p, "seed", 42));
            var g = BuildRandomGraph(n, density, rng);
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdges(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var bfs = new BFS(g, 0);
            var dfs = new DFS(g, 0);

            int target = n - 1;
            var bfsPath = new List<int>(bfs.PathTo(target));
            var dfsPath = new List<int>(dfs.PathTo(target));

            DrawPath(cv, vx, vy, bfsPath, Pal[0], 3, "BFS");
            DrawPath(cv, vx, vy, dfsPath, Pal[1], 2, "DFS");

            int bfsReachable = 0, dfsReachable = 0;
            for (int i = 0; i < n; i++) { if (bfs.Visited[i]) bfsReachable++; if (dfs.Visited[i]) dfsReachable++; }

            var sb = new StringBuilder();
            sb.AppendLine($"Граф: {n} вершин, {g.E} рёбер");
            sb.AppendLine($"BFS из 0: достижимо {bfsReachable} вершин");
            sb.AppendLine($"BFS путь 0->{target}: {(bfsPath.Count > 0 ? string.Join("->", bfsPath) : "не найден")}");
            sb.AppendLine($"DFS путь 0->{target}: {(dfsPath.Count > 0 ? string.Join("->", dfsPath) : "не найден")}");
            if (bfsPath.Count > 0)
                sb.AppendLine($"BFS расст. = {bfs.DistanceTo[target]}, DFS длина пути = {dfsPath.Count - 1}");
            return sb.ToString();
        }

        private static string DoDijkstra(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 10);
            double density = N(p, "density", 0.3);
            var rng = new Random(I(p, "seed", 42));
            var g = BuildRandomGraphW(n, density, rng);
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdgesW(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var dijk = new DijkstraSPath<Edge>(g, 0);
            int target = n - 1;

            var path = new List<int>();
            if (dijk.Distances[target] < double.MaxValue)
            {
                int v = target;
                path.Add(v);
                while (v != 0 && dijk.Edges[v] != null)
                {
                    v = dijk.Edges[v].StartV;
                    path.Insert(0, v);
                }
            }

            DrawPath(cv, vx, vy, path, Pal[0], 3, "Dijkstra");

            var sb = new StringBuilder();
            sb.AppendLine($"Граф: {n} вершин");
            sb.AppendLine($"Dijkstra из 0:");
            for (int i = 0; i < n; i++)
                sb.AppendLine($"  d[{i}] = {(dijk.Distances[i] < double.MaxValue ? dijk.Distances[i].ToString("F1") : "∞")}");
            return sb.ToString();
        }

        private static string DoBellmanFord(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 8);
            double negPct = N(p, "negativeEdges", 20) / 100.0;
            var rng = new Random(I(p, "seed", 42));
            var g = BuildRandomDirGraphW(n, 0.35, rng, 10, negPct);
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdgesW(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var bf = new BellmanFordSP<Edge>(g, 0);

            if (!bf.HasNegativeCycle)
            {
                int target = n - 1;
                var edges = bf.PathTo(target);
                if (edges != null)
                {
                    var path = new List<int>();
                    foreach (var e in edges)
                    {
                        if (path.Count == 0) path.Add(e.StartV);
                        path.Add(e.EndV);
                    }
                    DrawPath(cv, vx, vy, path, Pal[1], 3, "Bellman-Ford");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Граф: {n} вершин, ориентированный");
            sb.AppendLine($"Отрицательный цикл: {(bf.HasNegativeCycle ? "ДА" : "нет")}");
            if (!bf.HasNegativeCycle)
            {
                sb.AppendLine("Расстояния из 0:");
                for (int i = 0; i < n; i++)
                    sb.AppendLine($"  d[{i}] = {(bf.Distances[i] < double.MaxValue / 2 ? bf.Distances[i].ToString("F1") : "∞")}");
            }
            return sb.ToString();
        }

        private static string DoFloydWarshall(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 6);
            double density = N(p, "density", 0.5);
            var rng = new Random(I(p, "seed", 42));
            var g = BuildRandomDirGraphW(n, density, rng);
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdgesW(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var fw = new FloydWarshall<Edge>(g);
            var path = fw.PathBetween(0, n - 1);
            DrawPath(cv, vx, vy, path, Pal[3], 3, "Floyd-Warshall");

            var sb = new StringBuilder();
            sb.AppendLine($"Матрица кратчайших расстояний ({n}×{n}):");
            for (int i = 0; i < n; i++)
            {
                var row = new List<string>();
                for (int j = 0; j < n; j++)
                {
                    double d = fw.Dist[i, j];
                    row.Add(d >= 1e15 ? "  ∞" : $"{d,5:F1}");
                }
                sb.AppendLine(string.Join(" ", row));
            }
            return sb.ToString();
        }

        private static string DoAStar(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int gw = I(p, "gridW", 15), gh = I(p, "gridH", 15);
            double obsPct = N(p, "obstacles", 20);
            var rng = new Random(I(p, "seed", 42));

            int total = gw * gh;
            var g = new GraphW<Edge>(total);
            var blocked = new bool[gw, gh];
            int obs = (int)(total * obsPct / 100.0);
            int placed = 0;
            while (placed < obs)
            {
                int x = rng.Next(gw), y = rng.Next(gh);
                if ((x == 0 && y == 0) || (x == gw - 1 && y == gh - 1)) continue;
                if (!blocked[x, y]) { blocked[x, y] = true; placed++; }
            }

            for (int x = 0; x < gw; x++)
                for (int y = 0; y < gh; y++)
                {
                    if (blocked[x, y]) continue;
                    int idx = y * gw + x;
                    int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d], ny = y + dy[d];
                        if (nx >= 0 && nx < gw && ny >= 0 && ny < gh && !blocked[nx, ny])
                            g.AddArce(idx, ny * gw + nx, 1);
                    }
                }

            int start = 0, goal = (gh - 1) * gw + (gw - 1);
            double heuristic(int v) { int vx2 = v % gw, vy2 = v / gw; return Math.Abs(vx2 - (gw - 1)) + Math.Abs(vy2 - (gh - 1)); }

            var astar = new AStarSearch<Edge>(g, start, goal, heuristic);

            var vx = new double[total]; var vy = new double[total];
            for (int x = 0; x < gw; x++)
                for (int y = 0; y < gh; y++) { int idx = y * gw + x; vx[idx] = x; vy[idx] = y; }

            for (int x = 0; x < gw; x++)
                for (int y = 0; y < gh; y++)
                    if (blocked[x, y])
                    {
                        var bx = new AI.DataStructs.Algebraic.Vector(1) { [0] = x };
                        var by = new AI.DataStructs.Algebraic.Vector(1) { [0] = y };
                        cv.AddScatter(bx, by, "", new SKColor(0x55, 0x55, 0x55));
                    }

            if (astar.Found)
            {
                var path = astar.GetPath();
                DrawPath(cv, vx, vy, path, Pal[0], 3, "A*");
            }

            var sx = new AI.DataStructs.Algebraic.Vector(1) { [0] = 0 };
            var sy = new AI.DataStructs.Algebraic.Vector(1) { [0] = 0 };
            cv.AddScatter(sx, sy, "Старт", Pal[2]);
            var gxv = new AI.DataStructs.Algebraic.Vector(1) { [0] = gw - 1 };
            var gyv = new AI.DataStructs.Algebraic.Vector(1) { [0] = gh - 1 };
            cv.AddScatter(gxv, gyv, "Цель", Pal[1]);

            var sb = new StringBuilder();
            sb.AppendLine($"Сетка: {gw}×{gh}, препятствий: {obs}");
            sb.AppendLine($"A* найден: {astar.Found}");
            if (astar.Found)
                sb.AppendLine($"Длина пути: {astar.PathCost:F0} шагов");
            return sb.ToString();
        }

        private static string DoYenK(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 8);
            int k = I(p, "k", 3);
            var rng = new Random(I(p, "seed", 42));
            var g = BuildRandomGraphW(n, 0.45, rng);
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdgesW(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var yen = new YenKShortestPaths<Edge>(g, 0, n - 1, k);

            var sb = new StringBuilder();
            sb.AppendLine($"K={k} кратчайших путей 0->{n - 1}:");
            for (int i = 0; i < yen.Paths.Count; i++)
            {
                var (path, cost) = yen.Paths[i];
                DrawPath(cv, vx, vy, path, C(i), 3 - Math.Min(i, 2), $"Путь {i + 1}");
                sb.AppendLine($"  #{i + 1}: стоимость={cost:F1}, путь: {string.Join("->", path)}");
            }
            if (yen.Paths.Count == 0) sb.AppendLine("  Путей не найдено");
            return sb.ToString();
        }

        private static string DoTopoScc(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 10);
            double density = N(p, "density", 0.25);
            var rng = new Random(I(p, "seed", 42));

            var g = new Graph(n);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j && rng.NextDouble() < density)
                        g.AddArc(i, j);

            var vx = CircleX(n); var vy = CircleY(n);
            DrawGraphEdges(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));

            var scc = new TarjanSCC(g);
            for (int i = 0; i < n; i++)
            {
                var px = new AI.DataStructs.Algebraic.Vector(1) { [0] = vx[i] };
                var py = new AI.DataStructs.Algebraic.Vector(1) { [0] = vy[i] };
                cv.AddScatter(px, py, $"SCC {scc.ComponentId[i]}", C(scc.ComponentId[i]));
            }

            var ab = new ArticulationBridges(g);

            var sb = new StringBuilder();
            sb.AppendLine($"Граф: {n} вершин, {g.E} рёбер");
            sb.AppendLine($"SCC (Tarjan): {scc.Count} компонент");
            for (int c = 0; c < scc.Count; c++)
            {
                var members = new List<int>();
                for (int i = 0; i < n; i++) if (scc.ComponentId[i] == c) members.Add(i);
                sb.AppendLine($"  SCC {c}: [{string.Join(", ", members)}]");
            }
            sb.AppendLine($"Точки сочленения: [{string.Join(", ", ab.ArticulationPoints)}]");
            sb.AppendLine($"Мосты: {ab.Bridges.Count}");
            return sb.ToString();
        }

        private static string DoMst(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 10);
            double density = N(p, "density", 0.5);
            var rng = new Random(I(p, "seed", 42));
            var g = BuildRandomGraphW(n, density, rng);
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdgesW(cv, vx, vy, g, new SKColor(0x33, 0x33, 0x55));
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var kruskal = new Kruskal<Edge>(g);
            foreach (var e in kruskal.MSTEdges)
                DrawLine(cv, vx[e.StartV], vy[e.StartV], vx[e.EndV], vy[e.EndV], Pal[2], 3);

            var prim = new Prim<Edge>(g);
            var boruvka = new Boruvka<Edge>(g);

            var sb = new StringBuilder();
            sb.AppendLine($"Граф: {n} вершин, плотность {density:F2}");
            sb.AppendLine($"Kruskal MST: вес = {kruskal.TotalWeight:F1}, рёбер = {kruskal.MSTEdges.Count}");
            sb.AppendLine($"Prim MST:    вес = {prim.TotalWeight:F1}");
            sb.AppendLine($"Borůvka MST: вес = {boruvka.TotalWeight:F1}");
            return sb.ToString();
        }
    }
}
