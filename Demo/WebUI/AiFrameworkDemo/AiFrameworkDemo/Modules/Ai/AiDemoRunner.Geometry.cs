using AI;
using AI.Charts;
using AI.Charts.JS;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.Distances;
using AI.HighLevelFunctions;
using SkiaSharp;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Ai;

public static partial class AiDemoRunner
{
    #region Геометрия и расстояния — случаи

    private static void RunGeometryCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "metric_balls":
            {
                int pp = Math.Clamp((int)N("p", 2), 1, 8);
                int res = Math.Max(50, (int)N("res", 200));
                var bounds = (xMin: -1.5, xMax: 1.5, yMin: -1.5, yMax: 1.5);
                cv.SetBackgroundImage(RenderMetricBalls(res, res, pp));
                int gridRes = 100;
                double[] xGrid = new double[gridRes], yGrid = new double[gridRes];
                for (int i = 0; i < gridRes; i++) { xGrid[i] = -1.5 + 3.0 * i / (gridRes - 1); yGrid[i] = -1.5 + 3.0 * i / (gridRes - 1); }
                var zGrid = new double[gridRes][];
                for (int j = 0; j < gridRes; j++)
                {
                    zGrid[j] = new double[gridRes];
                    for (int i = 0; i < gridRes; i++)
                    {
                        double ax = Math.Abs(xGrid[i]), ay = Math.Abs(yGrid[j]);
                        int val = 0;
                        if (ax + ay <= 1) val |= 1;
                        if (Math.Sqrt(ax * ax + ay * ay) <= 1) val |= 2;
                        if (Math.Max(ax, ay) <= 1) val |= 4;
                        zGrid[j][i] = val;
                    }
                }
                plotly = new PlotlyBuilder { Title = $"Единичные сферы (p={pp}): жёлт. L₁, красн. L₂, зел. L∞", AxisX = "x", AxisY = "y" };
                plotly.AddHeatmap(xGrid, yGrid, zGrid, "Viridis", opacity: 0.7, showScale: false, zMin: 0, zMax: 7);
                cv.ChartName = $"Единичные сферы (p={pp}): жёлт. L₁, красн. L₂, зел. L∞, син. cos·L₂";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
                var t = Vector.Seq(0, 2 * Math.PI / 400, 2 * Math.PI);
                cv.AddPlot(t.Transform(Math.Cos), t.Transform(Math.Sin), "L₂ (окружность)", Palette[1], width: 2);
                cv.AddPlot(new Vector(5) { [0] = 1, [1] = -1, [2] = -1, [3] = 1, [4] = 1 },
                           new Vector(5) { [0] = 1, [1] =  1, [2] = -1, [3] = -1, [4] = 1 }, "L∞ (квадрат)", Palette[2], width: 2);
                cv.AddPlot(new Vector(5) { [0] = 1, [1] =  0, [2] = -1, [3] =  0, [4] = 1 },
                           new Vector(5) { [0] = 0, [1] =  1, [2] =  0, [3] = -1, [4] = 0 }, "L₁ (ромб)", Palette[3], width: 2);
                var lx = new Vector(400); var ly = new Vector(400);
                for (int i = 0; i < 400; i++)
                {
                    double th = 2 * Math.PI * i / 399.0, ct = Math.Cos(th), st = Math.Sin(th);
                    double denom = Math.Pow(Math.Pow(Math.Abs(ct), pp) + Math.Pow(Math.Abs(st), pp), 1.0 / pp);
                    lx[i] = ct / denom; ly[i] = st / denom;
                }
                cv.AddPlot(lx, ly, $"Lp (p={pp})", Palette[0], width: 3);
                break;
            }
            case "metric_balls_3d":
            {
                int pp = Math.Clamp((int)N("p", 2), 1, 8);
                const int G = 40;
                var xGrid = new Vector(G); var yGrid = new Vector(G);
                for (int i = 0; i < G; i++) { xGrid[i] = -1.2 + 2.4 * i / (G - 1); yGrid[i] = -1.2 + 2.4 * i / (G - 1); }
                var zUp = new double[G, G]; var zDown = new double[G, G];
                for (int ix = 0; ix < G; ix++)
                    for (int iy = 0; iy < G; iy++)
                    {
                        double xyp = Math.Pow(Math.Abs(xGrid[ix]), pp) + Math.Pow(Math.Abs(yGrid[iy]), pp);
                        if (xyp < 1.0) { zUp[ix, iy] = Math.Pow(1.0 - xyp, 1.0 / pp); zDown[ix, iy] = -zUp[ix, iy]; }
                        else { zUp[ix, iy] = double.NaN; zDown[ix, iy] = double.NaN; }
                    }
                cv.ChartName = $"3D единичная сфера L{pp}:  |x|^{pp} + |y|^{pp} + |z|^{pp} = 1";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.Camera3D.Azimuth = N("azimuth", -35); cv.Camera3D.Elevation = N("elevation", 25);
                cv.AddSurface(xGrid, yGrid, zUp,   $"L{pp}  верх", ColormapKind.Jet);
                cv.AddSurface(xGrid, yGrid, zDown, $"L{pp}  низ",  ColormapKind.Jet);
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                plotly.AddSurface(ToArray(xGrid), ToArray(xGrid), zUp,   $"L{pp} верх", "Jet", 0.9);
                plotly.AddSurface(ToArray(xGrid), ToArray(xGrid), zDown, $"L{pp} низ",  "Jet", 0.9);
                break;
            }
            case "kl_divergence":
            {
                double mu1 = N("mu1", 0), sig1 = Math.Max(0.05, N("sig1", 1.0));
                double mu2 = N("mu2", 1.5), sig2 = Math.Max(0.05, N("sig2", 1.2));
                int gridN = 400;
                double xMin = Math.Min(mu1 - 4 * sig1, mu2 - 4 * sig2);
                double xMax = Math.Max(mu1 + 4 * sig1, mu2 + 4 * sig2);
                var xG = Vector.Seq(xMin, (xMax - xMin) / (gridN - 1), xMax);
                var pd = xG.Transform(xi => NormalPdf(xi, mu1, sig1));
                var qd = xG.Transform(xi => NormalPdf(xi, mu2, sig2));
                double klAnalytic = Math.Log(sig2 / sig1) + (sig1 * sig1 + (mu1 - mu2) * (mu1 - mu2)) / (2 * sig2 * sig2) - 0.5;
                double klNum = ProbabilityDistances.DKL(pd, qd);
                double klSym = ProbabilityDistances.DKLSymmetrical(pd, qd);
                cv.ChartName = $"KL(p‖q) аналит. = {klAnalytic:F4}   числ. = {klNum:F4}   симм. = {klSym:F4}";
                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddPlot(xG, pd, $"p = N({mu1:F2}, {sig1:F2})", Palette[0], width: 3);
                cv.AddPlot(xG, qd, $"q = N({mu2:F2}, {sig2:F2})", Palette[1], width: 3);
                textOut =
                    "KL-дивергенция между двумя нормальными распределениями\n" +
                    "----------------------------------------\n\n" +
                    $"  p = N({mu1:F3}, {sig1:F3})\n" +
                    $"  q = N({mu2:F3}, {sig2:F3})\n\n" +
                    $"  KL(p ‖ q) аналитически  = {klAnalytic:F5}\n" +
                    $"  KL(p ‖ q) по плотностям = {klNum:F5}\n" +
                    $"  Симм. KL (численно)     = {klSym:F5}\n\n" +
                    $"  Евклидово: {BaseDist.EuclideanDistance(pd, qd):F5}\n" +
                    $"  Косинусное сходство: {BaseDist.Cos(pd, qd):F5}";
                break;
            }
            case "projection":
            {
                double ax = N("ax", 3), ay = N("ay", 2);
                double bx = N("bx", 4), by = N("by", 1);
                var A = new Vector(new[] { ax, ay });
                var B = new Vector(new[] { bx, by });
                var proj = AnalyticGeometryFunctions.ProjectionAtoB(A, B);
                double nrm = AnalyticGeometryFunctions.NormVect(proj);
                double ang = AnalyticGeometryFunctions.AngleVect(A, B) * 180 / Math.PI;
                double dot = AnalyticGeometryFunctions.Dot(A, B);
                double dst = AnalyticGeometryFunctions.DistanceFromAToB(A, B);
                double maxAbs = Math.Max(Math.Max(Math.Abs(ax), Math.Abs(ay)), Math.Max(Math.Abs(bx), Math.Abs(by))) + 1;
                cv.ChartName = $"Проекция A->B:  |proj|={nrm:F3}  угол={ang:F2}°  A·B={dot:F3}";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.SetAxisRange(-maxAbs, maxAbs, -maxAbs, maxAbs);
                cv.AddPlot(new Vector(new[] { 0.0, ax }), new Vector(new[] { 0.0, ay }), "A",  Palette[0], width: 3);
                cv.AddPlot(new Vector(new[] { 0.0, bx }), new Vector(new[] { 0.0, by }), "B",  Palette[1], width: 3);
                cv.AddPlot(new Vector(new[] { 0.0, proj[0] }), new Vector(new[] { 0.0, proj[1] }), "proj_B(A)", Palette[2], width: 4);
                cv.AddPlot(new Vector(new[] { ax, proj[0] }), new Vector(new[] { ay, proj[1] }), "перпендикуляр", Palette[3], width: 2);
                textOut =
                    $"A = ({ax:F3}, {ay:F3}),  |A| = {AnalyticGeometryFunctions.NormVect(A):F3}\n" +
                    $"B = ({bx:F3}, {by:F3}),  |B| = {AnalyticGeometryFunctions.NormVect(B):F3}\n\n" +
                    $"proj_B(A) = ({proj[0]:F3}, {proj[1]:F3})\n" +
                    $"|proj|    = {nrm:F4}\nA·B       = {dot:F4}\n" +
                    $"cos θ     = {AnalyticGeometryFunctions.Cos(A, B):F4}\n" +
                    $"θ         = {ang:F3}°\n|A − B|   = {dst:F4}";
                break;
            }
        }
    }

    #endregion

    #region Рендеринг метрических шаров

    private static SKImage RenderMetricBalls(int w, int h, int p)
    {
        var bmp = new SKBitmap(w, h);
        for (int py = 0; py < h; py++)
        {
            double y = -1.5 + 3.0 * py / Math.Max(1, h - 1);
            for (int px = 0; px < w; px++)
            {
                double x = -1.5 + 3.0 * px / Math.Max(1, w - 1);
                double ax = Math.Abs(x), ay = Math.Abs(y);
                double lp = Math.Pow(Math.Pow(ax, p) + Math.Pow(ay, p), 1.0 / p);
                double linf = Math.Max(ax, ay);
                double l2 = Math.Sqrt(x * x + y * y);
                double l1 = ax + ay;
                byte alpha = 0, r = 0, g = 0, b = 0;
                if (lp   <= 1) { r = 0xF5; g = 0x9E; b = 0x0B; alpha = 90; }
                if (linf <= 1) { r = 0x10; g = 0xB9; b = 0x81; alpha = 60; }
                if (l2   <= 1) { r = 0xEF; g = 0x44; b = 0x44; alpha = 70; }
                if (l1   <= 1) { r = 0xEA; g = 0xB3; b = 0x08; alpha = 110; }
                bmp.SetPixel(px, h - 1 - py, new SKColor(r, g, b, alpha));
            }
        }
        return SKImage.FromBitmap(bmp);
    }

    #endregion
}
