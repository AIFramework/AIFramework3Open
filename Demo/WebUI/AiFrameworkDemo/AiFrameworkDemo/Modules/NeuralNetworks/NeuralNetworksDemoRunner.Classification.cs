using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using SkiaSharp;
using System.Text;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Классификация — MLP

    private static void RunClassificationCase(
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int n       = Math.Max(40,  (int)N("n",       160));
        int hidden  = Math.Max(4,   (int)N("hidden",   16));
        int layers  = Math.Clamp((int)N("layers", 2), 1, 3);
        int epochs  = Math.Max(10,  (int)N("epochs",   80));
        float lr    = (float)Math.Clamp(N("lr", 0.01), 0.0001, 0.5);
        int ds      = Math.Clamp((int)N("dataset", 1), 0, 3);
        int seed    = (int)N("seed", 42);
        int batchSz = Math.Clamp(n / 8, 8, 64);

        (var feats, var labels) = MakeClassificationData(n, seed, ds);
        var rng  = new Random(seed);
        var mods = new List<Module> { new Linear(2, hidden, true, rng), new ReLU() };
        for (int l = 1; l < layers; l++) { mods.Add(new Linear(hidden, hidden, true, rng)); mods.Add(new ReLU()); }
        mods.Add(new Linear(hidden, 2, true, rng));
        var net = new Sequential([.. mods]);

        var xArr = new float[n * 2]; var yArr = new int[n];
        for (int i = 0; i < n; i++) { xArr[i * 2] = (float)feats[i][0]; xArr[i * 2 + 1] = (float)feats[i][1]; yArr[i] = labels[i]; }
        var xFull = V2T.From(xArr, new V2S(n, 2));
        var yFull = V2T.From(yArr, new V2S(n));
        var optim = new Adam(net.Parameters(), lr: lr);
        var batchRng = new Random(seed + 1);

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            int[] idx = [.. Enumerable.Range(0, n).OrderBy(_ => batchRng.Next())];
            for (int start = 0; start < n; start += batchSz)
            {
                int bsz = Math.Min(batchSz, n - start);
                var bx = new float[bsz * 2]; var by = new int[bsz];
                for (int bi = 0; bi < bsz; bi++)
                {
                    int ii = idx[start + bi];
                    bx[bi * 2] = xArr[ii * 2]; bx[bi * 2 + 1] = xArr[ii * 2 + 1]; by[bi] = yArr[ii];
                }
                optim.ZeroGrad();
                var logits = net.Forward(V2T.From(bx, new V2S(bsz, 2)));
                ClassificationLosses.CrossEntropy(logits, V2T.From(by, new V2S(bsz))).Backward();
                optim.Step();
            }
        }

        var pred = new int[n];
        using (var _ = TapeContext.NoGrad())
        {
            var ls = net.Forward(xFull).AsReadOnlySpan<float>();
            for (int i = 0; i < n; i++) pred[i] = ls[i * 2] >= ls[i * 2 + 1] ? 0 : 1;
        }

        var (acc, metricsText) = ClassifierMetrics(labels, pred);
        textOut = metricsText;

        var bounds = GetBounds(feats, padding: 0.6);
        cv.SetBackgroundImage(RenderDecisionBoundaryBatched(net, bounds, 380, 320, Palette, tintAlpha: 65));

        int gridRes = 60;
        double[] xGrid = new double[gridRes], yGrid = new double[gridRes];
        double dx = (bounds.xMax - bounds.xMin) / (gridRes - 1), dy = (bounds.yMax - bounds.yMin) / (gridRes - 1);
        for (int i = 0; i < gridRes; i++) { xGrid[i] = bounds.xMin + i * dx; yGrid[i] = bounds.yMin + i * dy; }
        var inputData = new float[gridRes * gridRes * 2];
        for (int j = 0; j < gridRes; j++)
            for (int i = 0; i < gridRes; i++) { int idx = (j * gridRes + i) * 2; inputData[idx] = (float)xGrid[i]; inputData[idx + 1] = (float)yGrid[j]; }
        V2T output;
        using (var _ = TapeContext.NoGrad()) { output = net.Forward(V2T.From(inputData, new V2S(gridRes * gridRes, 2))); }
        float[] outArr = output.AsReadOnlySpan<float>().ToArray();
        int outCols = output.Shape[1];
        int[][] classGrid = new int[gridRes][];
        for (int j = 0; j < gridRes; j++)
        {
            classGrid[j] = new int[gridRes];
            for (int i = 0; i < gridRes; i++)
            {
                int ii = j * gridRes + i; int best = 0; float bestV = outArr[ii * outCols];
                for (int c = 1; c < outCols; c++) { float v = outArr[ii * outCols + c]; if (v > bestV) { bestV = v; best = c; } }
                classGrid[j][i] = best;
            }
        }

        string arch = string.Join("->", Enumerable.Repeat(hidden.ToString(), layers));
        plotly = new PlotlyBuilder { Title = $"MLP 2->{arch}->2", AxisX = "x₁", AxisY = "x₂" };
        plotly.AddHeatmapDiscrete(xGrid, yGrid, classGrid);
        for (int cls = 0; cls < 2; cls++)
        {
            var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < n; i++) if (labels[i] == cls) { xs.Add(feats[i][0]); ys.Add(feats[i][1]); }
            if (xs.Count > 0) plotly.AddScatter2D(xs.ToArray(), ys.ToArray(), $"Класс {cls}", cls == 0 ? "#3B82F6" : "#F97316", markerSize: 5);
        }

        cv.ChartName = $"MLP 2->{arch}->2  —  точность {acc:F1}%   (epochs={epochs}, lr={lr:F3})";
        cv.LabelX = "x₁"; cv.LabelY = "x₂";
        PlotTwoClassesResult(cv, feats, labels, pred);
        cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
    }

    #endregion

    #region Визуализация классификатора

    private static SKImage RenderDecisionBoundaryBatched(
        Module net, (double xMin, double xMax, double yMin, double yMax) b,
        int gridW, int gridH, SKColor[] colors, byte tintAlpha)
    {
        int total = gridW * gridH;
        var grid = new float[total * 2];
        int idx = 0;
        for (int py = 0; py < gridH; py++)
        {
            double yCoord = b.yMax - (b.yMax - b.yMin) * py / Math.Max(1, gridH - 1);
            for (int px = 0; px < gridW; px++) { double xCoord = b.xMin + (b.xMax - b.xMin) * px / Math.Max(1, gridW - 1); grid[idx++] = (float)xCoord; grid[idx++] = (float)yCoord; }
        }
        float[] logits;
        using (var _ = TapeContext.NoGrad())
            logits = net.Forward(V2T.From(grid, new V2S(total, 2))).AsReadOnlySpan<float>().ToArray();
        var bmp = new SKBitmap(gridW, gridH);
        int pi = 0;
        for (int py = 0; py < gridH; py++)
            for (int px = 0; px < gridW; px++) { int cls = logits[pi] >= logits[pi + 1] ? 0 : 1; pi += 2; var c = colors[cls % colors.Length]; bmp.SetPixel(px, py, new SKColor(c.Red, c.Green, c.Blue, tintAlpha)); }
        return SKImage.FromBitmap(bmp);
    }

    private static void PlotTwoClassesResult(ChartView cv, Vector[] feats, int[] labels, int[] pred)
    {
        for (int c = 0; c < 2; c++)
        {
            var correct = new List<(double, double)>(); var wrong = new List<(double, double)>();
            for (int i = 0; i < feats.Length; i++)
            {
                if (labels[i] != c) continue;
                if (pred[i] == labels[i]) correct.Add((feats[i][0], feats[i][1]));
                else                       wrong.Add((feats[i][0], feats[i][1]));
            }
            if (correct.Count > 0) { var px = new Vector(correct.Count); var py = new Vector(correct.Count); for (int i = 0; i < correct.Count; i++) { px[i] = correct[i].Item1; py[i] = correct[i].Item2; } cv.AddScatterMark3(px, py, $"Класс {c}", Palette[c]); }
            if (wrong.Count > 0) { var px = new Vector(wrong.Count); var py = new Vector(wrong.Count); for (int i = 0; i < wrong.Count; i++) { px[i] = wrong[i].Item1; py[i] = wrong[i].Item2; } var baseC = Palette[c]; cv.AddScatterMark6(px, py, $"Класс {c} (ошибочные)", new SKColor((byte)(baseC.Red / 3), (byte)(baseC.Green / 3), (byte)(baseC.Blue / 3))); }
        }
    }

    #endregion

    #region Метрики классификации

    private static (double accuracy, string text) ClassifierMetrics(int[] labels, int[] pred)
    {
        int n = labels.Length, tp = 0, tn = 0, fp = 0, fn = 0;
        for (int i = 0; i < n; i++)
        {
            if      (labels[i] == 1 && pred[i] == 1) tp++;
            else if (labels[i] == 0 && pred[i] == 0) tn++;
            else if (labels[i] == 0 && pred[i] == 1) fp++;
            else fn++;
        }
        double acc   = 100.0 * (tp + tn) / n;
        double prec0 = tn + fn > 0 ? 100.0 * tn / (tn + fn) : 0, rec0 = tn + fp > 0 ? 100.0 * tn / (tn + fp) : 0;
        double prec1 = tp + fp > 0 ? 100.0 * tp / (tp + fp) : 0, rec1 = tp + fn > 0 ? 100.0 * tp / (tp + fn) : 0;
        double f0    = prec0 + rec0 > 0 ? 2 * prec0 * rec0 / (prec0 + rec0) : 0;
        double f1    = prec1 + rec1 > 0 ? 2 * prec1 * rec1 / (prec1 + rec1) : 0;
        var sb = new StringBuilder();
        sb.AppendLine("> Метрики нейросетевой классификации");
        sb.AppendLine();
        sb.AppendLine($"  Accuracy:    {acc,6:F1}%");
        sb.AppendLine($"  Macro F1:    {(f0 + f1) / 2,6:F1}%");
        sb.AppendLine();
        sb.AppendLine("  +---------+----------+----------+----------+");
        sb.AppendLine("  |  Класс  | Precision|  Recall  |    F1    |");
        sb.AppendLine("  |---------+----------+----------+----------|");
        sb.AppendLine($"  |    0    | {prec0,6:F1}%  | {rec0,6:F1}%  | {f0,6:F1}%  |");
        sb.AppendLine($"  |    1    | {prec1,6:F1}%  | {rec1,6:F1}%  | {f1,6:F1}%  |");
        sb.AppendLine("  +---------+----------+----------+----------+");
        sb.AppendLine();
        sb.AppendLine($"  True 0:  {HeatCell(tn, n)}  {HeatCell(fp, n)}");
        sb.AppendLine($"  True 1:  {HeatCell(fn, n)}  {HeatCell(tp, n)}");
        sb.AppendLine();
        sb.AppendLine($"  Всего: {n}    TP={tp}  TN={tn}  FP={fp}  FN={fn}");
        return (acc, sb.ToString());
    }

    private static string HeatCell(int value, int total)
    {
        double frac = total > 0 ? (double)value / total : 0;
        string shade = frac switch { < 0.05 => "·····", < 0.15 => ".....", < 0.30 => ":::::", < 0.50 => "#####", _ => "#####" };
        return $"{shade}{value,4}".PadRight(12);
    }

    #endregion
}
