using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Nn;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Diagnostics;
using System.Text;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp, DemoSettings s)
    {
        var cv     = MakeView(s.Width, s.Height, s.DarkTheme);
        string?  textOut = null;
        PlotlyBuilder? plotly  = null;
        var sw = Stopwatch.StartNew();

        switch (key)
        {
            case "mlp_cls":
                RunClassificationCase(p, cv, ref textOut, ref plotly);
                break;

            case "mlp_reg_1d":
            case "mlp_reg_2d":
            case "mlp_reg_2d_3d":
                RunRegressionCase(key, p, cv, ref textOut, ref plotly);
                break;

            case "gru_predict":
                RunSequenceCase(p, cv, ref textOut);
                break;

            case "lstm_predict":
                RunLstmSequenceCase(p, cv, ref textOut);
                break;

            case "filter_predict":
                RunFilterCase(p, cv, ref textOut);
                break;

            case "transformer_predict":
                RunTransformerCase(p, cv, ref textOut);
                break;

            case "rnn_compare":
                RunCompareCase(p, cv, ref textOut);
                break;

            case "lstm_lm":
                RunLanguageModelCase(p, tp, cv, ref textOut);
                break;

            case "autoencoder":
                RunFeaturesCase(p, cv, ref textOut);
                break;

            default:
                cv.ChartName = "Неизвестный ключ: " + key;
                break;
        }

        sw.Stop();
        textOut = ComposeHeader(sw.ElapsedMilliseconds) + (textOut ?? "");
        return new DemoResult
        {
            PngDataUrl = RenderPng(cv, s.Width, s.Height),
            TextOutput = textOut,
            PlotlyJson = plotly?.Build() ?? PlotlyChartRenderer.ToPlotlyJson(cv),
            SourceChart = cv
        };
    }

    #region Палитра и утилиты

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

    private static string ComposeHeader(long elapsedMs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("> Нейронные сети V2 (PyTorch-style)");
        sb.AppendLine();
        sb.AppendLine("  Бэкенд:          CPU (V2 Autograd)");
        sb.AppendLine($"  Время выполнения: {elapsedMs} мс");
        sb.AppendLine();
        return sb.ToString();
    }

    private static ChartView MakeView(int w, int h, bool dark)
        => DemoRunnerBase.MakeView(w, h, dark);

    private static string RenderPng(ChartView cv, int w, int h)
        => DemoRunnerBase.RenderPng(cv, w, h);

    private static double[] ToArray(Vector v)
        => DemoRunnerBase.ToArray(v);

    #endregion
}
