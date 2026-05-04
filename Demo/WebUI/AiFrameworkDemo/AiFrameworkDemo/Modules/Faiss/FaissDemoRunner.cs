using AI.Charts;
using AI.DataStructs.Algebraic;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Faiss
{
    /// <summary>
    /// Демонстрация алгоритмов AI.Faiss (KNN, Batch Search, метрики, Assign).
    /// Реализовано на чистом C# — нативная faiss_c.dll необязательна.
    /// Показывает API-примеры для реальной библиотеки AI.Faiss.
    /// </summary>
    public static class FaissDemoRunner
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
            string txt;
            try
            {
                txt = key switch
                {
                    "knn_search"     => DoKnnSearch(p, cv),
                    "batch_search"   => DoBatchSearch(p, cv),
                    "metric_compare" => DoMetricCompare(p, cv),
                    "assign_demo"    => DoAssign(p, cv),
                    _                => $"Неизвестный ключ «{key}»",
                };
            }
            catch (Exception ex)
            {
                txt = $"Ошибка: {ex.GetType().Name}: {ex.Message}";
            }
            return Png(cv, s, textOutput: txt);
        }

        // -- 1. KNN-поиск (brute-force L2 / IP) ---------------------------------

        private static string DoKnnSearch(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n      = I(p, "n",     100);
            int k      = I(p, "k",       5);
            int idxTyp = I(p, "index",   0);
            int metTyp = I(p, "metric",  0);
            int seed   = I(p, "seed",   42);
            const int dim = 2;

            var rng     = new Random(seed);
            var vectors = GenVecs(n, dim, rng);
            var query   = GenVec(dim, rng);

            k = Math.Min(k, n);
            bool useL2 = metTyp == 0;
            var (labels, dists) = KnnSearch(vectors, query, k, useL2);

            PlotBackground(cv, vectors);
            PlotNeighbors(cv, vectors, labels);
            PlotQuery(cv, query);

            var sb = new StringBuilder();
            sb.AppendLine($"Индекс: {(idxTyp == 0 ? "Flat (точный)" : "HNSW32 (приближённый)")} [C# brute-force]");
            sb.AppendLine($"Метрика: {(useL2 ? "L2" : "Inner Product")}");
            sb.AppendLine($"Векторов: {n}, Размерность: {dim}");
            sb.AppendLine($"Запрос: ({query[0]:F3}, {query[1]:F3})");
            sb.AppendLine();
            sb.AppendLine($"Топ-{k} ближайших соседей:");
            for (int i = 0; i < k; i++)
            {
                int id = labels[i];
                sb.AppendLine($"  #{i+1}: id={id,4}  {(useL2 ? "dist" : "score")}={dists[i]:F4}  ({vectors[id][0]:F3}, {vectors[id][1]:F3})");
            }

            sb.AppendLine();
            AppendApiExample(sb, "KNN Search",
                """
                using var idx = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
                idx.Add(vectors);
                var (dists, labels) = idx.Search(query, k);
                """);

            return sb.ToString();
        }

        // -- 2. Пакетный поиск ----------------------------------------------------

        private static string DoBatchSearch(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n       = I(p, "n",       200);
            int queries = I(p, "queries",   5);
            int k       = I(p, "k",         3);
            int seed    = I(p, "seed",     42);
            const int dim = 2;

            var rng  = new Random(seed);
            var vecs = GenVecs(n, dim, rng);
            var qs   = GenVecs(queries, dim, rng);
            k = Math.Min(k, n);

            PlotBackground(cv, vecs);

            var sb = new StringBuilder();
            sb.AppendLine($"Векторов: {n}, Запросов: {queries}, K={k}");
            sb.AppendLine();

            for (int qi = 0; qi < queries; qi++)
            {
                var (labels, dists) = KnnSearch(vecs, qs[qi], k, useL2: true);
                PlotNeighbors(cv, vecs, labels, $"Q{qi+1} соседи", C(qi + 2));
                PlotQuery(cv, qs[qi], $"Q{qi+1}", C(qi + 2));

                sb.AppendLine($"Запрос {qi+1}: ({qs[qi][0]:F3}, {qs[qi][1]:F3})");
                for (int i = 0; i < k; i++)
                    sb.AppendLine($"  #{i+1}: id={labels[i],4}  dist={dists[i]:F4}");
                sb.AppendLine();
            }

            AppendApiExample(sb, "Batch Search",
                """
                using var idx = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
                idx.Add(vectors);
                var (dists, labels) = idx.Search(queryBatch, k);
                """);

            return sb.ToString();
        }

        // -- 3. Сравнение метрик -------------------------------------------------

        private static string DoMetricCompare(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n    = I(p, "n",   50);
            int k    = I(p, "k",    5);
            int seed = I(p, "seed", 42);
            const int dim = 2;

            var rng  = new Random(seed);
            var vecs = GenNormalized(n, dim, rng);
            var query = GenNormalized(1, dim, rng)[0];
            k = Math.Min(k, n);

            var (l2Labels, l2Dists) = KnnSearch(vecs, query, k, useL2: true);
            var (ipLabels, ipDists) = KnnSearch(vecs, query, k, useL2: false);

            var xIdx = new Vector(k);
            var yL2  = new Vector(k);
            var yIP  = new Vector(k);
            for (int i = 0; i < k; i++)
            {
                xIdx[i] = i + 1;
                yL2[i]  = l2Dists[i];
                yIP[i]  = ipDists[i];
            }

            cv.AddPlot(xIdx, yL2, "L2 расстояние", C(0), 2);
            cv.AddScatter(xIdx, yIP, "IP score", C(1));

            var sb = new StringBuilder();
            sb.AppendLine($"Размерность: {dim}, нормализованные векторы");
            sb.AppendLine($"Запрос: ({query[0]:F3}, {query[1]:F3})");
            sb.AppendLine();

            sb.AppendLine("Топ-K по L2 (меньше = ближе):");
            for (int i = 0; i < k; i++)
                sb.AppendLine($"  id={l2Labels[i],4}  dist={l2Dists[i]:F4}  ({vecs[l2Labels[i]][0]:F3}, {vecs[l2Labels[i]][1]:F3})");

            sb.AppendLine();
            sb.AppendLine("Топ-K по Inner Product (больше = ближе):");
            for (int i = 0; i < k; i++)
                sb.AppendLine($"  id={ipLabels[i],4}  score={ipDists[i]:F4}  ({vecs[ipLabels[i]][0]:F3}, {vecs[ipLabels[i]][1]:F3})");

            var l2Set = new HashSet<int>(l2Labels);
            var common = l2Set.Intersect(ipLabels).Count();
            sb.AppendLine();
            sb.AppendLine($"Пересечение топ-{k}: {common} из {k} совпадают");

            AppendApiExample(sb, "Metric Compare",
                """
                using var l2 = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
                using var ip = FaissIndex.Create(dim, "Flat", MetricType.METRIC_INNER_PRODUCT);
                l2.Add(vectors); ip.Add(vectors);
                var (l2D, l2L) = l2.Search(query, k);
                var (ipD, ipL) = ip.Search(query, k);
                """);

            return sb.ToString();
        }

        // -- 4. Кластеризация Assign ---------------------------------------------

        private static string DoAssign(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n        = I(p, "n",        150);
            int clusters = I(p, "clusters",   5);
            int spread   = I(p, "spread",     3);
            int seed     = I(p, "seed",      42);
            const int dim = 2;

            var rng     = new Random(seed);
            var centers = GenVecs(clusters, dim, rng, scale: 8.0f);

            var data       = new float[n][];
            var trueLabels = new int[n];
            for (int i = 0; i < n; i++)
            {
                int c = i % clusters;
                trueLabels[i] = c;
                data[i] = new float[dim];
                for (int d = 0; d < dim; d++)
                    data[i][d] = centers[c][d] + (float)(rng.NextDouble() * 2 - 1) * spread;
            }

            var assigned = AssignNearest(data, centers);

            var groupsX = Enumerable.Range(0, clusters).Select(_ => new List<double>()).ToArray();
            var groupsY = Enumerable.Range(0, clusters).Select(_ => new List<double>()).ToArray();

            for (int i = 0; i < n; i++)
            {
                int c = Math.Clamp(assigned[i], 0, clusters - 1);
                groupsX[c].Add(data[i][0]);
                groupsY[c].Add(data[i][1]);
            }

            for (int c = 0; c < clusters; c++)
            {
                if (groupsX[c].Count == 0) continue;
                cv.AddScatter(
                    new Vector(groupsX[c].ToArray()),
                    new Vector(groupsY[c].ToArray()),
                    $"Кластер {c+1}", C(c));
            }

            var cxV = new Vector(clusters);
            var cyV = new Vector(clusters);
            for (int c = 0; c < clusters; c++) { cxV[c] = centers[c][0]; cyV[c] = centers[c][1]; }
            cv.AddScatter(cxV, cyV, "Центроиды", new SKColor(0xFF, 0xFF, 0xFF));

            var sb = new StringBuilder();
            sb.AppendLine($"Точек данных: {n}, Кластеров: {clusters}");
            sb.AppendLine();

            var acc = new int[clusters];
            for (int i = 0; i < n; i++)
            {
                int asgn = Math.Clamp(assigned[i], 0, clusters - 1);
                if (asgn == trueLabels[i]) acc[trueLabels[i]]++;
            }

            sb.AppendLine("Центроиды (x, y):");
            for (int c = 0; c < clusters; c++)
                sb.AppendLine($"  C{c+1}: ({centers[c][0]:F2}, {centers[c][1]:F2})");

            sb.AppendLine();
            sb.AppendLine("Точки на кластер / Совпадения с исходными:");
            int totalCorrect = 0;
            for (int c = 0; c < clusters; c++)
            {
                int cnt = groupsX[c].Count;
                totalCorrect += acc[c];
                sb.AppendLine($"  Кластер {c+1}: {cnt,4} точек  (из «своих»: {acc[c]})");
            }
            sb.AppendLine();
            sb.AppendLine($"Общая точность: {(double)totalCorrect / n:P1}");

            AppendApiExample(sb, "Assign (кластеризация)",
                """
                using var idx = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
                idx.Add(centroids);
                long[] assigned = idx.Assign(n, flatVectors);
                """);

            return sb.ToString();
        }

        // -- Чистый C# brute-force KNN --------------------------------------------

        private static (int[] labels, float[] dists) KnnSearch(
            float[][] db, float[] query, int k, bool useL2)
        {
            int n = db.Length;
            var scores = new (float score, int id)[n];

            for (int i = 0; i < n; i++)
            {
                if (useL2)
                {
                    float dist = 0;
                    for (int d = 0; d < query.Length; d++)
                    {
                        float diff = db[i][d] - query[d];
                        dist += diff * diff;
                    }
                    scores[i] = (dist, i);
                }
                else
                {
                    float dot = 0;
                    for (int d = 0; d < query.Length; d++)
                        dot += db[i][d] * query[d];
                    scores[i] = (-dot, i);  // отрицательный для сортировки по убыванию
                }
            }

            Array.Sort(scores, (a, b) => a.score.CompareTo(b.score));

            var labels = new int[k];
            var dists  = new float[k];
            for (int i = 0; i < k; i++)
            {
                labels[i] = scores[i].id;
                dists[i]  = useL2 ? scores[i].score : -scores[i].score;
            }
            return (labels, dists);
        }

        private static int[] AssignNearest(float[][] data, float[][] centers)
        {
            var result = new int[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                float bestDist = float.MaxValue;
                int bestC = 0;
                for (int c = 0; c < centers.Length; c++)
                {
                    float dist = 0;
                    for (int d = 0; d < data[i].Length; d++)
                    {
                        float diff = data[i][d] - centers[c][d];
                        dist += diff * diff;
                    }
                    if (dist < bestDist) { bestDist = dist; bestC = c; }
                }
                result[i] = bestC;
            }
            return result;
        }

        // -- Генерация данных -----------------------------------------------------

        private static float[] GenVec(int dim, Random rng, float scale = 1f)
        {
            var v = new float[dim];
            for (int d = 0; d < dim; d++) v[d] = (float)rng.NextDouble() * scale;
            return v;
        }

        private static float[][] GenVecs(int n, int dim, Random rng, float scale = 1f)
        {
            var vecs = new float[n][];
            for (int i = 0; i < n; i++) vecs[i] = GenVec(dim, rng, scale);
            return vecs;
        }

        private static float[][] GenNormalized(int n, int dim, Random rng)
        {
            var vecs = new float[n][];
            for (int i = 0; i < n; i++)
            {
                vecs[i] = new float[dim];
                double norm = 0;
                for (int d = 0; d < dim; d++)
                {
                    vecs[i][d] = (float)(rng.NextDouble() * 2 - 1);
                    norm += vecs[i][d] * vecs[i][d];
                }
                norm = Math.Sqrt(norm);
                if (norm > 1e-9)
                    for (int d = 0; d < dim; d++) vecs[i][d] /= (float)norm;
            }
            return vecs;
        }

        // -- Визуализация ---------------------------------------------------------

        private static void PlotBackground(ChartView cv, float[][] vecs)
        {
            int n  = vecs.Length;
            var xV = new Vector(n);
            var yV = new Vector(n);
            for (int i = 0; i < n; i++) { xV[i] = vecs[i][0]; yV[i] = vecs[i][1]; }
            cv.AddScatter(xV, yV, "Индекс", new SKColor(0x80, 0x80, 0x80, 0xA0));
        }

        private static void PlotNeighbors(ChartView cv, float[][] vecs, int[] labels,
            string label = "K соседей", SKColor? color = null)
        {
            if (labels.Length == 0) return;
            var xN = new Vector(labels.Length);
            var yN = new Vector(labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                xN[i] = vecs[labels[i]][0];
                yN[i] = vecs[labels[i]][1];
            }
            cv.AddScatter(xN, yN, label, color ?? C(1));
        }

        private static void PlotQuery(ChartView cv, float[] q,
            string label = "Запрос", SKColor? color = null)
        {
            var xQ = new Vector(1) { [0] = q[0] };
            var yQ = new Vector(1) { [0] = q[1] };
            cv.AddScatter(xQ, yQ, label, color ?? C(2));
        }

        private static void AppendApiExample(StringBuilder sb, string title, string code)
        {
            sb.AppendLine();
            sb.AppendLine($"--- AI.Faiss API ({title}) ---");
            sb.AppendLine(code);
        }
    }
}
