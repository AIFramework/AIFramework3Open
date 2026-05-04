using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using AiFrameworkDemo.Core;
using SkiaSharp;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.Ai;

public static partial class AiDemoRunner
{
    public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var cv     = MakeView(s.Width, s.Height, s.DarkTheme);
        string?  textOut = null;
        PlotlyBuilder? plotly  = null;

        switch (key)
        {
            case "descriptive":
            case "histogram_pdf":
            case "quantiles":
            case "moments_scan":
            case "confidence_interval":
            case "hypothesis_test":
            case "normality_test":
                RunStatisticsCase(key, p, cv, ref textOut);
                break;

            case "uniform_normal":
            case "clt":
            case "mle":
            case "exponential":
            case "gamma_beta":
            case "cauchy_laplace":
            case "weibull_poisson":
            case "mixture_em":
            case "monte_carlo":
            case "monte_carlo_nd":
            case "gauss2d":
            case "mixture2d":
            case "rayleigh_rice":
            case "heterogeneous_mixture":
            case "heterogeneous_mixture_nd":
                RunDistributionsCase(key, p, cv, ref textOut, ref plotly);
                break;

            case "pearson":
            case "corr_matrix":
            case "autocorr":
            case "crosscorr":
            case "convolution":
                RunCorrelationCase(key, p, cv, ref textOut, ref plotly);
                break;

            case "metric_balls":
            case "metric_balls_3d":
            case "kl_divergence":
            case "projection":
                RunGeometryCase(key, p, cv, ref textOut, ref plotly);
                break;

            case "diff_integr":
            case "moving_stats":
            case "form_factors":
                RunSeriesCase(key, p, cv, ref textOut);
                break;

            default:
                cv.ChartName = "Неизвестный ключ: " + key;
                break;
        }

        return new DemoResult
        {
            PngDataUrl = RenderPng(cv, s.Width, s.Height),
            TextOutput = textOut,
            PlotlyJson = plotly?.Build() ?? PlotlyChartRenderer.ToPlotlyJson(cv),
            SourceChart = cv
        };
    }

    #region Палитра

    internal static readonly SKColor[] Palette =
    [
        new SKColor(0x3B, 0x82, 0xF6),
        new SKColor(0xF9, 0x73, 0x16),
        new SKColor(0x10, 0xB9, 0x81),
        new SKColor(0xA8, 0x55, 0xF7),
        new SKColor(0xEC, 0x48, 0x99),
        new SKColor(0x06, 0xB6, 0xD4),
        new SKColor(0xEA, 0xB3, 0x08),
        new SKColor(0xEF, 0x44, 0x44),
    ];

    #endregion

    #region Общие утилиты

    internal static double NormalPdf(double x, double mu, double sig)
    {
        double z = (x - mu) / sig;
        return Math.Exp(-0.5 * z * z) / (sig * Math.Sqrt(2 * Math.PI));
    }

    internal static void DrawVLine(ChartView cv, double x, double yMax, string name, SKColor color, int width)
    {
        var xV = new Vector(2); xV[0] = x; xV[1] = x;
        var yV = new Vector(2); yV[0] = 0; yV[1] = yMax;
        cv.AddPlot(xV, yV, name, color, width);
    }

    internal static SKColor WithAlpha(SKColor c, byte alpha) => new SKColor(c.Red, c.Green, c.Blue, alpha);

    internal static double TrapzRef(Func<double, double> f, double a, double b, int n)
    {
        double h = (b - a) / n;
        double s = 0.5 * (f(a) + f(b));
        for (int i = 1; i < n; i++) s += f(a + i * h);
        return s * h;
    }

    private static ChartView MakeView(int w, int h, bool dark)
        => DemoRunnerBase.MakeView(w, h, dark);

    private static string RenderPng(ChartView cv, int w, int h)
        => DemoRunnerBase.RenderPng(cv, w, h);

    private static double[] ToArray(Vector v)
        => DemoRunnerBase.ToArray(v);

    #endregion
}
