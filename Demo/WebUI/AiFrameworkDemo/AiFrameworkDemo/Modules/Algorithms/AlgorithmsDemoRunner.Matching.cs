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
        private static string DoHungarian(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 5);
            var rng = new Random(I(p, "seed", 42));
            var cost = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    cost[i, j] = rng.Next(1, 20);

            var hung = new Hungarian(cost);

            var sb = new StringBuilder();
            sb.AppendLine("Матрица стоимостей:");
            for (int i = 0; i < n; i++)
            {
                var row = new List<string>();
                for (int j = 0; j < n; j++) row.Add($"{cost[i, j],4:F0}");
                sb.AppendLine(string.Join(" ", row));
            }
            sb.AppendLine($"\nНазначение (строка->столбец):");
            for (int i = 0; i < n; i++)
                sb.AppendLine($"  {i} -> {hung.Assignment[i]} (стоимость {cost[i, hung.Assignment[i]]:F0})");
            sb.AppendLine($"Общая стоимость: {hung.TotalCost:F0}");

            var lx = new double[n]; var ly = new double[n];
            for (int i = 0; i < n; i++) { lx[i] = -1; ly[i] = i; }
            var rx = new double[n]; var ry = new double[n];
            for (int i = 0; i < n; i++) { rx[i] = 1; ry[i] = i; }

            var lxv = new AI.DataStructs.Algebraic.Vector(n);
            var lyv = new AI.DataStructs.Algebraic.Vector(n);
            var rxv = new AI.DataStructs.Algebraic.Vector(n);
            var ryv = new AI.DataStructs.Algebraic.Vector(n);
            for (int i = 0; i < n; i++) { lxv[i] = lx[i]; lyv[i] = ly[i]; rxv[i] = rx[i]; ryv[i] = ry[i]; }
            cv.AddScatter(lxv, lyv, "Строки", Pal[0]);
            cv.AddScatter(rxv, ryv, "Столбцы", Pal[1]);

            for (int i = 0; i < n; i++)
                DrawLine(cv, lx[i], ly[i], rx[hung.Assignment[i]], ry[hung.Assignment[i]], Pal[2], 2);

            return sb.ToString();
        }

        private static string DoBipartiteMatching(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int algoIdx = I(p, "algo", 0);
            int left = I(p, "left", 5), right = I(p, "right", 6);
            double density = N(p, "density", 0.4);
            var rng = new Random(I(p, "seed", 42));

            int matchCount;
            int[] matchL;
            string algoName;

            if (algoIdx == 1)
            {
                var hk = new HopcroftKarp(left, right);
                for (int i = 0; i < left; i++)
                    for (int j = 0; j < right; j++)
                        if (rng.NextDouble() < density) hk.AddEdge(i, j);
                matchCount = hk.MaxMatching();
                matchL = hk.MatchLeft;
                algoName = "Hopcroft-Karp";
            }
            else
            {
                var kuhn = new KuhnMatching(left, right);
                for (int i = 0; i < left; i++)
                    for (int j = 0; j < right; j++)
                        if (rng.NextDouble() < density) kuhn.AddEdge(i, j);
                matchCount = kuhn.Solve();
                matchL = kuhn.MatchLeft;
                algoName = "Kuhn";
            }

            var lx = new double[left]; var ly = new double[left];
            for (int i = 0; i < left; i++) { lx[i] = -1; ly[i] = i; }
            var rx = new double[right]; var ry = new double[right];
            for (int i = 0; i < right; i++) { rx[i] = 1; ry[i] = i; }

            var lxv = new AI.DataStructs.Algebraic.Vector(left);
            var lyv = new AI.DataStructs.Algebraic.Vector(left);
            var rxv = new AI.DataStructs.Algebraic.Vector(right);
            var ryv = new AI.DataStructs.Algebraic.Vector(right);
            for (int i = 0; i < left; i++) { lxv[i] = lx[i]; lyv[i] = ly[i]; }
            for (int i = 0; i < right; i++) { rxv[i] = rx[i]; ryv[i] = ry[i]; }
            cv.AddScatter(lxv, lyv, "Левая доля", Pal[0]);
            cv.AddScatter(rxv, ryv, "Правая доля", Pal[1]);

            for (int i = 0; i < left; i++)
                if (matchL[i] >= 0)
                    DrawLine(cv, lx[i], ly[i], rx[matchL[i]], ry[matchL[i]], Pal[2], 2);

            var sb = new StringBuilder();
            sb.AppendLine($"Алгоритм: {algoName}");
            sb.AppendLine($"Двудольный граф: |L|={left}, |R|={right}");
            sb.AppendLine($"Максимальное паросочетание: {matchCount}");
            return sb.ToString();
        }

        private static string DoGeneralMatching(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 8);
            double density = N(p, "density", 0.35);
            var rng = new Random(I(p, "seed", 42));

            var blossom = new EdmondsBlossom(n);
            var g = new Graph(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rng.NextDouble() < density) { blossom.AddEdge(i, j); g.AddEdge(i, j); }

            int matchCount = blossom.MaxMatching();
            var vx = CircleX(n); var vy = CircleY(n);

            DrawGraphEdges(cv, vx, vy, g, new SKColor(0x44, 0x44, 0x66));
            for (int i = 0; i < n; i++)
                if (blossom.Match[i] > i)
                    DrawLine(cv, vx[i], vy[i], vx[blossom.Match[i]], vy[blossom.Match[i]], Pal[2], 3);
            DrawVertices(cv, vx, vy, new SKColor(0xC9, 0xD0, 0xE0));

            var sb = new StringBuilder();
            sb.AppendLine($"Edmonds Blossom: {n} вершин");
            sb.AppendLine($"Максимальное паросочетание: {matchCount}");
            return sb.ToString();
        }

        private static string DoStableMarriage(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n = I(p, "n", 5);
            var rng = new Random(I(p, "seed", 42));

            var menPrefs = new int[n][];
            var womenPrefs = new int[n][];
            for (int i = 0; i < n; i++)
            {
                menPrefs[i] = Enumerable.Range(0, n).OrderBy(_ => rng.Next()).ToArray();
                womenPrefs[i] = Enumerable.Range(0, n).OrderBy(_ => rng.Next()).ToArray();
            }

            var gs = new GaleShapley(menPrefs, womenPrefs);

            var lx = new double[n]; var ly = new double[n];
            for (int i = 0; i < n; i++) { lx[i] = -1.5; ly[i] = i; }
            var rx = new double[n]; var ry = new double[n];
            for (int i = 0; i < n; i++) { rx[i] = 1.5; ry[i] = i; }

            var lxv = new AI.DataStructs.Algebraic.Vector(n);
            var lyv = new AI.DataStructs.Algebraic.Vector(n);
            var rxv = new AI.DataStructs.Algebraic.Vector(n);
            var ryv = new AI.DataStructs.Algebraic.Vector(n);
            for (int i = 0; i < n; i++) { lxv[i] = lx[i]; lyv[i] = ly[i]; rxv[i] = rx[i]; ryv[i] = ry[i]; }
            cv.AddScatter(lxv, lyv, "Мужчины", Pal[0]);
            cv.AddScatter(rxv, ryv, "Женщины", Pal[1]);

            for (int m = 0; m < n; m++)
                if (gs.ManPartner[m] >= 0)
                    DrawLine(cv, lx[m], ly[m], rx[gs.ManPartner[m]], ry[gs.ManPartner[m]], Pal[2], 2);

            var sb = new StringBuilder();
            sb.AppendLine($"Гейл—Шепли: {n} пар");
            sb.AppendLine("Устойчивые пары (мужчина -> женщина):");
            for (int m = 0; m < n; m++)
                sb.AppendLine($"  M{m} -> W{gs.ManPartner[m]}");
            return sb.ToString();
        }
    }
}
