using AI.Charts;
using AI.Charts.JS;
using AiFrameworkDemo.Core;
using SkiaSharp;
using Vector = AI.DataStructs.Algebraic.Vector;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.ML;

public static partial class MlDemoRunner
{
    public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        try
        {
            var cv     = MakeView(s.Width, s.Height, s.DarkTheme);
            string?  textOut = null;
            PlotlyBuilder? plotly  = null;

            switch (key)
            {
                case "kmeans":
                case "fast_kmeans":
                case "forel":
                case "kohonen":
                case "kmeans_3d":
                    RunClusteringCase(key, p, cv, ref plotly);
                    break;

                case "bayes_cls":
                case "nn_cls":
                case "linear_cls":
                case "svm_binary":
                case "corr_cls":
                    RunClassificationCase(key, p, cv, out textOut, out plotly);
                    break;

                case "lin_reg":
                case "poly_reg":
                case "multiple_reg":
                case "pca_2d":
                case "ar_predict":
                case "genetic":
                case "genetic_fit":
                case "multiple_reg_3d":
                case "pca_3d":
                case "genetic_landscape":
                    RunRegressionCase(key, p, cv, ref plotly);
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
        catch (Exception ex)
        {
            return new DemoResult { Error = ex.Message };
        }
    }

    #region Палитра (Tailwind-like)

    internal static readonly SKColor[] Palette =
    [
        new SKColor(0x3B, 0x82, 0xF6), // blue-500
        new SKColor(0xF9, 0x73, 0x16), // orange-500
        new SKColor(0x10, 0xB9, 0x81), // emerald-500
        new SKColor(0xA8, 0x55, 0xF7), // purple-500
        new SKColor(0xEC, 0x48, 0x99), // pink-500
        new SKColor(0x06, 0xB6, 0xD4), // cyan-500
        new SKColor(0xEA, 0xB3, 0x08), // amber-500
        new SKColor(0xEF, 0x44, 0x44), // red-500
    ];

    internal static readonly string[] PlotlyColors =
    [
        "rgba(59,130,246,0.85)",
        "rgba(249,115,22,0.85)",
        "rgba(16,185,129,0.85)",
        "rgba(168,85,247,0.85)",
        "rgba(236,72,153,0.85)",
        "rgba(6,182,212,0.85)",
        "rgba(234,179,8,0.85)",
        "rgba(239,68,68,0.85)",
    ];

    #endregion

    #region Делегаты к DemoRunnerBase

    private static ChartView MakeView(int w, int h, bool dark)
        => DemoRunnerBase.MakeView(w, h, dark);

    private static string RenderPng(ChartView cv, int w, int h)
        => DemoRunnerBase.RenderPng(cv, w, h);

    private static double[] ToArray(Vector v)
        => DemoRunnerBase.ToArray(v);

    #endregion
}
