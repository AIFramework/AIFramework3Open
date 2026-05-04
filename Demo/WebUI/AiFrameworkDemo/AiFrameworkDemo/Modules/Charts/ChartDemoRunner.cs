using AI.Charts;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.Charts.JS;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Charts;

public static class ChartDemoRunner
{
    public static (string png, string? plotlyJson, ChartView cv) Render(string key, IReadOnlyDictionary<string, double> p, int width, int height, bool darkTheme)
    {
        double N(string k, double def = 0) => p != null && p.TryGetValue(k, out var v) ? v : def;
        PlotlyBuilder? plotly = null;

        var cv = MakeView(width, height, darkTheme);

        switch (key)
        {
            case "line_sin_cos":
            {
                var x = Vector.Seq(0, 0.05, 2 * Math.PI);
                cv.ChartName = "Sin и Cos";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddPlot(x, x.Transform(Math.Sin), "sin(x)", new SKColor(99, 180, 255));
                cv.AddPlot(x, x.Transform(Math.Cos), "cos(x)", new SKColor(255, 140, 80));
                break;
            }
            case "line_spline":
            {
                var x = Vector.Seq(0, 0.4, 2 * Math.PI);
                var y = x.Transform(t => Math.Sin(t) + 0.3 * Math.Sin(3 * t));
                cv.ChartName = "Сплайн vs линия";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddPlot(x, y, "Линия",  new SKColor(150, 150, 255), 1, false);
                cv.AddPlot(x, y, "Сплайн", new SKColor(99,  220, 140), 2, true);
                break;
            }
            case "line_decay":
            {
                var x = Vector.Seq(0, 0.04, 4 * Math.PI);
                var y = x.Transform(t => Math.Exp(-0.3 * t) * Math.Sin(t));
                cv.ChartName = "Затухающий сигнал  e⁻⁰·³ᵗ · sin(t)";
                cv.LabelX = "t"; cv.LabelY = "y";
                cv.AddPlot(x, y, "e^(-0.3t)·sin(t)", new SKColor(255, 180, 80));
                break;
            }
            case "line_complex":
            {
                var x = Vector.Seq(0, 0.05, 4 * Math.PI);
                var re = x.Transform(t => Math.Exp(-0.2 * t) * Math.Cos(t));
                var im = x.Transform(t => Math.Exp(-0.2 * t) * Math.Sin(t));
                var cv2 = new ComplexVector(re.Count);
                for (int i = 0; i < re.Count; i++) cv2[i] = new Complex(re[i], im[i]);
                cv.PlotComplex(x, cv2, "z(t)");
                cv.ChartName = "Комплексный сигнал z(t)";
                cv.LabelX = "t"; cv.LabelY = "";
                break;
            }

            case "bar_basic":
            {
                int n = 10;
                var x = Vector.SeqBeginsWithZero(1, n);
                var y = new Vector(new double[]{4.2, 7.1, 5.5, 8.9, 6.3, 3.8, 7.4, 9.0, 5.1, 6.8});
                cv.ChartName = "Столбчатая диаграмма";
                cv.LabelX = "Категория"; cv.LabelY = "Значение";
                cv.AddBar(x, y, "Данные", new SKColor(99, 160, 255));
                break;
            }
            case "bar_area":
            {
                var x = Vector.Seq(0, 0.05, 2 * Math.PI);
                var y = x.Transform(t => 0.5 + 0.5 * Math.Sin(t));
                cv.ChartName = "Area — площадь под кривой";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddArea(x, y, "sin(x)/2 + 0.5", new SKColor(99, 210, 140));
                break;
            }
            case "bar_histogram":
            {
                var rng = new Random(42);
                double[] data = new double[500];
                for (int i = 0; i < data.Length; i++)
                    data[i] = rng.NextGaussian() * 1.0 + 3.0;
                cv.ChartName = "Гистограмма (N=500, μ=3, σ=1)";
                cv.LabelX = "x"; cv.LabelY = "частота";
                cv.AddHistoramm(new Vector(data), new SKColor(180, 100, 255), "Нормальное распределение");
                break;
            }

            case "sc_clusters":
            {
                var rng = new Random(7);
                var (x1, y1) = Cluster(rng, 0.5,  1.2, 0.3, 80);
                var (x2, y2) = Cluster(rng, -0.8, -0.5, 0.4, 80);
                cv.ChartName = "Два кластера";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddScatter(x1, y1, "Кластер A", new SKColor(99, 180, 255));
                cv.AddScatter(x2, y2, "Кластер B", new SKColor(255, 130, 80));
                break;
            }
            case "sc_spiral":
            {
                int n = 400;
                var xv = new Vector(n); var yv = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double t = i * 4 * Math.PI / n;
                    double r = 0.1 * t;
                    xv[i] = r * Math.Cos(t);
                    yv[i] = r * Math.Sin(t);
                }
                cv.ChartName = "Спираль Архимеда";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddScatter(xv, yv, "r = 0.1θ", new SKColor(120, 230, 170));
                break;
            }
            case "sc_complex":
            {
                int n = 100;
                var cvec = new ComplexVector(n);
                for (int i = 0; i < n; i++)
                {
                    double a = i * 2 * Math.PI / n;
                    double r = 0.85 + 0.12 * Math.Sin(5 * a);
                    cvec[i] = new Complex(r * Math.Cos(a), r * Math.Sin(a));
                }
                cv.ScatterComplexPlaneWithRing1(cvec, "z = r(θ)·e^iθ");
                cv.ChartName = "Комплексная плоскость с единичной окружностью";
                break;
            }

            case "pol_rose4":
            {
                int n = 720;
                var theta = Vector.Seq(0, 360.0 / n, 360);
                var r = theta.Transform(d => Math.Abs(Math.Cos(4 * d * Math.PI / 180.0)));
                cv.ChartName = "Роза (4 лепестка)  r = |cos(4θ)|";
                cv.LabelY = "r";
                cv.AddRadialDegPlot(theta, r, "r=|cos(4θ)|", new SKColor(220, 80, 160));
                break;
            }
            case "pol_cardioid":
            {
                int n = 720;
                var theta = Vector.Seq(0, 360.0 / n, 360);
                var r = theta.Transform(d => 1 + Math.Cos(d * Math.PI / 180.0));
                cv.ChartName = "Кардиоида  r = 1 + cos(θ)";
                cv.LabelY = "r";
                cv.AddRadialDegPlot(theta, r, "r=1+cos(θ)", new SKColor(80, 200, 220));
                break;
            }
            case "pol_vector":
            {
                int n = 36;
                double[] vals = new double[n];
                for (int i = 0; i < n; i++) vals[i] = 0.5 + 0.5 * Math.Sin(i * Math.PI / 6.0);
                cv.RadPlotBlueDeg(new Vector(vals), "Вектор полярно");
                cv.ChartName = "Произвольный вектор в полярных координатах";
                break;
            }

            case "pie_basic":
            {
                var labels = Vector.SeqBeginsWithZero(1, 5);
                var values = new Vector(new double[] { 35, 25, 20, 12, 8 });
                cv.ChartName = "Доли рынка (%)";
                cv.LabelX = "сегмент"; cv.LabelY = "%";
                cv.AddCircul(labels, values, "Сегменты");
                break;
            }

            case "sig_spectrum":
            {
                int n = 512;
                double fs = 200.0;
                double dt = 1.0 / fs;
                var t  = Vector.SeqBeginsWithZero(dt, n);
                var y  = t.Transform(ti => Math.Sin(2 * Math.PI * 10 * ti) + 0.4 * Math.Sin(2 * Math.PI * 30 * ti));
                cv.ChartName = "FFT-спектр (f₁=10 Гц, f₂=30 Гц)";
                cv.LabelX = "Частота, Гц"; cv.LabelY = "Амплитуда";
                cv.AddSpectrum(t, y, new SKColor(255, 200, 60), "Спектр");
                break;
            }
            case "sig_diff":
            {
                var x = Vector.Seq(0, 0.05, 2 * Math.PI);
                var y = x.Transform(Math.Sin);
                cv.ChartName = "Производная sin(x)";
                cv.LabelX = "x"; cv.LabelY = "dy/dx";
                cv.AddPlot(x, y, "sin(x)",    new SKColor(130, 180, 255), 1);
                cv.AddDiff(x, y, new SKColor(255, 140, 60), "d/dx sin(x)", 2);
                break;
            }
            case "sig_integ":
            {
                var x = Vector.Seq(0, 0.05, 2 * Math.PI);
                var y = x.Transform(Math.Cos);
                cv.ChartName = "Интеграл cos(x)";
                cv.LabelX = "x"; cv.LabelY = "∫cos dx";
                cv.AddPlot(x, y, "cos(x)",   new SKColor(130, 180, 255), 1);
                cv.AddIntegr(x, y, new SKColor(100, 220, 140), "∫cos(x)dx", 2);
                break;
            }

            case "multi_4sin":
            {
                var x = Vector.Seq(0, 0.04, 2 * Math.PI);
                cv.ChartName = "4 синусоиды (авто-палитра)";
                cv.LabelX = "x"; cv.LabelY = "y";
                for (int k = 0; k < 4; k++)
                {
                    double phase = k * Math.PI / 4;
                    cv.AddPlot(x, x.Transform(t => Math.Sin(t + phase)), $"sin(x + {k}π/4)");
                }
                break;
            }
            case "multi_log":
            {
                var x = Vector.Seq(0.1, 0.1, 20);
                var y = x.Transform(t => Math.Exp(-0.3 * t));
                cv.IsLogScale = true;
                cv.ChartName = "Логарифмическая ось Y — e^(-0.3t)";
                cv.LabelX = "t"; cv.LabelY = "log y";
                cv.AddPlot(x, y, "e^(-0.3t)", new SKColor(255, 200, 80));
                break;
            }

            // -- 3D CHARTS -----------------------------------------------------
            case "3d_surface":
            {
                int n = 40;
                var xg = Vector.Seq(-3, 6.0 / (n - 1), 3);
                var yg = Vector.Seq(-3, 6.0 / (n - 1), 3);
                var z = new double[xg.Count, yg.Count];
                for (int i = 0; i < xg.Count; i++)
                for (int j = 0; j < yg.Count; j++)
                    z[i, j] = Math.Sin(xg[i]) * Math.Cos(yg[j]);
                cv.ChartName = "Surface: sin(x)·cos(y)";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.AddSurface(xg, yg, z, "sin·cos");
                cv.Camera3D.Azimuth = N("azimuth", 45);
                cv.Camera3D.Elevation = N("elevation", 30);
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                plotly.AddSurface(ToArray(xg), ToArray(yg), z, "sin·cos", "Jet");
                break;
            }
            case "3d_wireframe":
            {
                int n = 30;
                var xg = Vector.Seq(-2, 4.0 / (n - 1), 2);
                var yg = Vector.Seq(-2, 4.0 / (n - 1), 2);
                var z = new double[xg.Count, yg.Count];
                for (int i = 0; i < xg.Count; i++)
                for (int j = 0; j < yg.Count; j++)
                    z[i, j] = xg[i] * xg[i] + yg[j] * yg[j];
                cv.ChartName = "Wireframe: x² + y²";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.AddWireframe(xg, yg, z, "Парабалоид");
                cv.Camera3D.Azimuth = N("azimuth", 45);
                cv.Camera3D.Elevation = N("elevation", 30);
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                plotly.AddSurface(ToArray(xg), ToArray(yg), z, "Параболоид", "Viridis", 0.6);
                break;
            }
            case "3d_scatter":
            {
                int n = 500;
                var xs = new Vector(n); var ys = new Vector(n); var zs = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double t = i * 6.0 * Math.PI / n;
                    double r = 0.5 + t * 0.15;
                    xs[i] = r * Math.Cos(t);
                    ys[i] = r * Math.Sin(t);
                    zs[i] = t * 0.3;
                }
                cv.ChartName = "Scatter 3D: спираль";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.AddScatter3D(xs, ys, zs, "Спираль");
                cv.Camera3D.Azimuth = N("azimuth", 45);
                cv.Camera3D.Elevation = N("elevation", 30);
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                plotly.AddScatter3D(ToArray(xs), ToArray(ys), ToArray(zs), "Спираль", colorByZ: true, markerSize: 3);
                break;
            }
            case "3d_peaks":
            {
                int n = 50;
                var xg = Vector.Seq(-3, 6.0 / (n - 1), 3);
                var yg = Vector.Seq(-3, 6.0 / (n - 1), 3);
                var z = new double[xg.Count, yg.Count];
                for (int i = 0; i < xg.Count; i++)
                for (int j = 0; j < yg.Count; j++)
                {
                    double xi = xg[i], yj = yg[j];
                    z[i, j] =  3 * Math.Pow(1 - xi, 2) * Math.Exp(-(xi * xi) - (yj + 1) * (yj + 1))
                              - 10 * (xi / 5 - xi * xi * xi - Math.Pow(yj, 5)) * Math.Exp(-xi * xi - yj * yj)
                              - 1.0 / 3 * Math.Exp(-(xi + 1) * (xi + 1) - yj * yj);
                }
                cv.ChartName = "Peaks (MATLAB-style surface)";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.AddSurface(xg, yg, z, "peaks", ColormapKind.Jet);
                cv.Camera3D.Azimuth = N("azimuth", 55);
                cv.Camera3D.Elevation = N("elevation", 25);
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                plotly.AddSurface(ToArray(xg), ToArray(yg), z, "peaks", "Jet");
                break;
            }

            default:
                cv.ChartName = key;
                break;
        }

        return (ToPngDataUrl(cv, width, height), plotly?.Build() ?? PlotlyChartRenderer.ToPlotlyJson(cv), cv);
    }

    // -------------------------------------------------------------------------
    private static ChartView MakeView(int width, int height, bool darkTheme)
        => DemoRunnerBase.MakeView(width, height, darkTheme);

    private static string ToPngDataUrl(ChartView cv, int width, int height)
        => RenderPng(cv, width, height);

    private static (Vector x, Vector y) Cluster(Random rng, double mx, double my, double s, int n)
    {
        var xv = new Vector(n); var yv = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            xv[i] = mx + rng.NextGaussian() * s;
            yv[i] = my + rng.NextGaussian() * s;
        }
        return (xv, yv);
    }

    private static double[] ToArray(Vector v)
        => DemoRunnerBase.ToArray(v);
}

file static class RandomExt
{
    public static double NextGaussian(this Random rng)
    {
        double u1 = 1 - rng.NextDouble();
        double u2 = 1 - rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
    }
}