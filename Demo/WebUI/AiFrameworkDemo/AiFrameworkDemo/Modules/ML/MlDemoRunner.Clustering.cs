using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AI.ML.Clustering;
using SkiaSharp;
using Vector = AI.DataStructs.Algebraic.Vector;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.ML;

public static partial class MlDemoRunner
{
    #region Кластеризация — случаи

    private static void RunClusteringCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "kmeans":
            {
                int k = Math.Max(2, (int)N("k", 3));
                int n = Math.Max(30, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                var data = MakeClusterData(n, k, seed, (int)N("dataset", 0));
                var km = new KMeans(k);
                km.Train(data, seed);
                var bounds = GetBounds(data, padding: 0.6);
                var centroids = ExtractCentroids(km.Centroids, k);
                var clLabels = km.Classify(data);
                cv.SetBackgroundImage(RenderVoronoi(centroids, bounds, 380, 320, Palette, 55));
                cv.ChartName = $"K-Means  —  {k} кластера, {n} точек";
                cv.LabelX = "x₁"; cv.LabelY = "x₂";
                PlotClusters(cv, data, clLabels, k);
                PlotCentroids(cv, centroids, "Центроиды");
                cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
                plotly = BuildVoronoiPlotly(centroids, bounds, data, clLabels, k, cv.ChartName, cv.LabelX, cv.LabelY);
                break;
            }
            case "fast_kmeans":
            {
                int k = Math.Max(2, (int)N("k", 3));
                int n = Math.Max(30, (int)N("n", 150));
                int seed = (int)N("seed", 42);
                var data = MakeClusterData(n, k, seed, (int)N("dataset", 0));
                var fkm = new FastKMeans(k);
                fkm.Train(data, seed);
                var bounds = GetBounds(data, padding: 0.6);
                var centroids = ExtractCentroids(fkm.Centroids, k);
                var clLabels = fkm.Classify(data);
                cv.SetBackgroundImage(RenderVoronoi(centroids, bounds, 380, 320, Palette, 55));
                cv.ChartName = $"Fast K-Means (BallTree)  —  {k} кластера, {n} точек";
                cv.LabelX = "x₁"; cv.LabelY = "x₂";
                PlotClusters(cv, data, clLabels, k);
                PlotCentroids(cv, centroids, "Центроиды");
                cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
                plotly = BuildVoronoiPlotly(centroids, bounds, data, clLabels, k, cv.ChartName, cv.LabelX, cv.LabelY);
                break;
            }
            case "forel":
            {
                int n = Math.Max(30, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                var data = MakeClusterData(n, 3, seed, (int)N("dataset", 0));
                var forel = new Forel();
                forel.Train(data);
                int numCl = forel.Clusters.Length;
                var bounds = GetBounds(data, padding: 0.6);
                var centroids = ExtractCentroids(forel.Centroids, numCl);
                var clLabels = forel.Classify(data);
                cv.SetBackgroundImage(RenderVoronoi(centroids, bounds, 380, 320, Palette, 55));
                cv.ChartName = $"FOREL  —  автоматически найдено {numCl} кластеров";
                cv.LabelX = "x₁"; cv.LabelY = "x₂";
                PlotClusters(cv, data, clLabels, numCl);
                PlotCentroids(cv, centroids, "Центроиды");
                cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
                plotly = BuildVoronoiPlotly(centroids, bounds, data, clLabels, numCl, cv.ChartName, cv.LabelX, cv.LabelY);
                break;
            }
            case "kohonen":
            {
                int k = Math.Max(2, (int)N("k", 4));
                int n = Math.Max(30, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                int steps = Math.Max(10, (int)N("steps", 50));
                double eta0 = Math.Clamp(N("eta0", 0.3), 0.01, 0.99);
                var data = MakeClusterData(n, k, seed, (int)N("dataset", 0));
                var som = new KohonenNet(k, inpDim: 2, seed: seed) { Steps = steps, Eta0 = eta0 };
                som.Train(data, 0);
                var bounds = GetBounds(data, padding: 0.6);
                var origCentroids = som.GetOriginalCentroids();
                var centroids = new Vector[k];
                for (int i = 0; i < k; i++) centroids[i] = new Vector(new[] { origCentroids[i][0], origCentroids[i][1] });
                var clLabels = som.Classify(data);
                cv.SetBackgroundImage(RenderVoronoi(centroids, bounds, 380, 320, Palette, 55));
                cv.ChartName = $"Сеть Кохонена  —  {k} нейрона  |  η: {eta0:F2} -> {som.EtaFinal:F4}";
                cv.LabelX = "x₁"; cv.LabelY = "x₂";
                PlotClusters(cv, data, clLabels, k);
                PlotCentroids(cv, centroids, "Нейроны");
                cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
                plotly = BuildVoronoiPlotly(centroids, bounds, data, clLabels, k, cv.ChartName, cv.LabelX, cv.LabelY);
                break;
            }
            case "kmeans_3d":
            {
                int k = Math.Clamp((int)N("k", 3), 2, 6);
                int n = Math.Max(30, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                double azimuth = N("azimuth", -35);
                double elevation = N("elevation", 25);
                var rng = new Random(seed);
                var xs = new Vector(n); var ys = new Vector(n); var zs = new Vector(n);
                var data2d = new Vector[n];
                int perCluster = n / k;
                for (int c = 0; c < k; c++)
                {
                    double cx = (rng.NextDouble() - 0.5) * 6;
                    double cy = (rng.NextDouble() - 0.5) * 6;
                    double cz = (rng.NextDouble() - 0.5) * 6;
                    for (int j = 0; j < perCluster && c * perCluster + j < n; j++)
                    {
                        int idx = c * perCluster + j;
                        xs[idx] = cx + (rng.NextDouble() - 0.5) * 2;
                        ys[idx] = cy + (rng.NextDouble() - 0.5) * 2;
                        zs[idx] = cz + (rng.NextDouble() - 0.5) * 2;
                        data2d[idx] = new Vector(new[] { xs[idx], ys[idx], zs[idx] });
                    }
                }
                int rem = n - k * perCluster;
                for (int j = 0; j < rem; j++)
                {
                    int idx = k * perCluster + j;
                    xs[idx] = (rng.NextDouble() - 0.5) * 6;
                    ys[idx] = (rng.NextDouble() - 0.5) * 6;
                    zs[idx] = (rng.NextDouble() - 0.5) * 6;
                    data2d[idx] = new Vector(new[] { xs[idx], ys[idx], zs[idx] });
                }
                var km = new KMeans(k);
                km.Train(data2d, seed);
                var labels = new int[n];
                for (int i = 0; i < n; i++) labels[i] = km.Classify(data2d[i]);
                cv.ChartName = $"3D K-Means  K={k},  n={n}";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.Camera3D.Azimuth = azimuth;
                cv.Camera3D.Elevation = elevation;
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                string[] clusterColors = { "#6366f1", "#f59e0b", "#10b981", "#ef4444", "#8b5cf6", "#ec4899" };
                for (int c = 0; c < k; c++)
                {
                    var cxL = new List<double>(); var cyL = new List<double>(); var czL = new List<double>();
                    for (int i = 0; i < n; i++)
                        if (labels[i] == c) { cxL.Add(xs[i]); cyL.Add(ys[i]); czL.Add(zs[i]); }
                    if (cxL.Count > 0)
                    {
                        cv.AddScatter3D(new Vector(cxL.ToArray()), new Vector(cyL.ToArray()), new Vector(czL.ToArray()), $"Кластер {c + 1}");
                        plotly.AddScatter3D(cxL.ToArray(), cyL.ToArray(), czL.ToArray(), $"Кластер {c + 1}", clusterColors[c % clusterColors.Length], 4);
                    }
                }
                break;
            }
        }
    }

    #endregion

    #region Кластеризация — вспомогательные методы

    private static SKImage RenderVoronoi(
        Vector[] centroids, (double xMin, double xMax, double yMin, double yMax) b,
        int gridW, int gridH, SKColor[] colors, byte tintAlpha)
    {
        var bmp = new SKBitmap(gridW, gridH);
        for (int py = 0; py < gridH; py++)
        {
            double yCoord = b.yMax - (b.yMax - b.yMin) * py / Math.Max(1, gridH - 1);
            for (int px = 0; px < gridW; px++)
            {
                double xCoord = b.xMin + (b.xMax - b.xMin) * px / Math.Max(1, gridW - 1);
                int nearest = 0;
                double best = double.MaxValue;
                for (int i = 0; i < centroids.Length; i++)
                {
                    double dx = xCoord - centroids[i][0], dy = yCoord - centroids[i][1];
                    double d = dx * dx + dy * dy;
                    if (d < best) { best = d; nearest = i; }
                }
                var c = colors[nearest % colors.Length];
                bmp.SetPixel(px, py, new SKColor(c.Red, c.Green, c.Blue, tintAlpha));
            }
        }
        return SKImage.FromBitmap(bmp);
    }

    private static PlotlyBuilder BuildVoronoiPlotly(
        Vector[] centroids, (double xMin, double xMax, double yMin, double yMax) bounds,
        Vector[] allPoints, int[] labels, int k,
        string title, string labelX, string labelY)
    {
        var pb = new PlotlyBuilder { Title = title, AxisX = labelX, AxisY = labelY };
        const int gridRes = 80;
        double[] xGrid = new double[gridRes];
        double[] yGrid = new double[gridRes];
        double dx = (bounds.xMax - bounds.xMin) / (gridRes - 1);
        double dy = (bounds.yMax - bounds.yMin) / (gridRes - 1);
        for (int i = 0; i < gridRes; i++) { xGrid[i] = bounds.xMin + i * dx; yGrid[i] = bounds.yMin + i * dy; }
        int[][] classGrid = new int[gridRes][];
        for (int j = 0; j < gridRes; j++)
        {
            classGrid[j] = new int[gridRes];
            for (int i = 0; i < gridRes; i++)
            {
                double px = xGrid[i], py = yGrid[j];
                int best = 0; double bestD = double.MaxValue;
                for (int c = 0; c < centroids.Length; c++)
                {
                    double d = (px - centroids[c][0]) * (px - centroids[c][0])
                             + (py - centroids[c][1]) * (py - centroids[c][1]);
                    if (d < bestD) { bestD = d; best = c; }
                }
                classGrid[j][i] = best;
            }
        }
        pb.AddHeatmapDiscrete(xGrid, yGrid, classGrid);
        for (int c = 0; c < k; c++)
        {
            var xPts = new List<double>(); var yPts = new List<double>();
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] == c) { xPts.Add(allPoints[i][0]); yPts.Add(allPoints[i][1]); }
            if (xPts.Count > 0)
                pb.AddScatter2D(xPts.ToArray(), yPts.ToArray(), $"Кластер {c + 1}", PlotlyColors[c % PlotlyColors.Length], 6);
        }
        return pb;
    }

    private static void PlotClusters(ChartView cv, Vector[] data, int[] labels, int k)
    {
        for (int c = 0; c < k; c++)
        {
            var pts = data.Zip(labels, (d, l) => (d, l)).Where(t => t.l == c).Select(t => t.d).ToArray();
            if (pts.Length == 0) continue;
            var px = new Vector(pts.Length); var py = new Vector(pts.Length);
            for (int i = 0; i < pts.Length; i++) { px[i] = pts[i][0]; py[i] = pts[i][1]; }
            cv.AddScatterMark3(px, py, $"Кластер {c + 1}", Palette[c % Palette.Length]);
        }
    }

    private static void PlotCentroids(ChartView cv, Vector[] centroids, string label)
    {
        var cx = new Vector(centroids.Length); var cy = new Vector(centroids.Length);
        for (int i = 0; i < centroids.Length; i++) { cx[i] = centroids[i][0]; cy[i] = centroids[i][1]; }
        cv.AddScatterMark6(cx, cy, label, SKColors.White);
    }

    private static Vector[] ExtractCentroids(Vector[] source, int count)
    {
        var result = new Vector[count];
        for (int i = 0; i < count; i++)
            result[i] = new Vector(new[] { source[i][0], source[i][1] });
        return result;
    }

    private static (double xMin, double xMax, double yMin, double yMax) GetBounds(Vector[] data, double padding = 0.5)
    {
        double xMin = double.MaxValue, xMax = double.MinValue;
        double yMin = double.MaxValue, yMax = double.MinValue;
        foreach (var d in data)
        {
            xMin = Math.Min(xMin, d[0]); xMax = Math.Max(xMax, d[0]);
            yMin = Math.Min(yMin, d[1]); yMax = Math.Max(yMax, d[1]);
        }
        if (xMax - xMin < 1e-6) { xMin -= 1; xMax += 1; }
        if (yMax - yMin < 1e-6) { yMin -= 1; yMax += 1; }
        return (xMin - padding, xMax + padding, yMin - padding, yMax + padding);
    }

    #endregion
}
