using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AI.ML.Classification;
using SkiaSharp;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.ML;

public static partial class MlDemoRunner
{
    #region Классификация — случаи

    private static void RunClassificationCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, out string? textOut, out PlotlyBuilder? plotly)
    {
        textOut = null;
        plotly  = null;
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "bayes_cls":
            {
                int n = Math.Max(40, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                int ds = (int)N("dataset", 1);
                (var feats, var labels) = MakeClassificationData(n, seed, ds);
                var cls = new BayesianClassifier();
                cls.Train(feats, labels);
                RunClassifierDemo(cv, cls, feats, labels, "Байесовский классификатор", ds, out textOut, out plotly);
                break;
            }
            case "nn_cls":
            {
                int n = Math.Max(40, (int)N("n", 120));
                int seed = (int)N("seed", 7);
                int ds = (int)N("dataset", 1);
                (var feats, var labels) = MakeClassificationData(n, seed, ds);
                var cls = new NN();
                cls.Train(feats, labels);
                RunClassifierDemo(cv, cls, feats, labels, "Ближайший эталон (NN)", ds, out textOut, out plotly);
                break;
            }
            case "linear_cls":
            {
                int n = Math.Max(40, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                int epochs = Math.Max(5, (int)N("epochs", 40));
                int ds = (int)N("dataset", 0);
                (var feats, var labels) = MakeClassificationData(n, seed, ds);
                var cls = new LinearClassifierBinarry(2) { EpochesToPass = epochs, LearningRate = 0.02 };
                cls.Train(feats, labels);
                RunClassifierDemo(cv, cls, feats, labels, $"Линейный классификатор  —  {epochs} эпох", ds, out textOut, out plotly);
                break;
            }
            case "svm_binary":
            {
                int n = Math.Max(40, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                int epochs = Math.Max(5, (int)N("epochs", 40));
                int numSv = Math.Max(2, (int)N("numSv", 6));
                int ds = (int)N("dataset", 0);
                (var feats, var labels) = MakeClassificationData(n, seed, ds);
                var cls = new SVMBinary(2) { EpochesToPass = epochs, LearningRate = 0.02, NumSupportVectors = numSv };
                cls.Train(feats, labels);
                RunClassifierDemo(cv, cls, feats, labels, $"SVM  —  {numSv} опорных векторов, {epochs} эпох", ds, out textOut, out plotly);
                break;
            }
            case "corr_cls":
            {
                int n = Math.Max(40, (int)N("n", 120));
                int seed = (int)N("seed", 42);
                (var feats, var labels) = MakeCorrData(n, seed);
                var cls = new CorrelationClassifier();
                cls.Train(feats, labels);
                RunClassifierDemo(cv, cls, feats, labels, "Корреляционный классификатор", 0, out textOut, out plotly);
                break;
            }
        }
    }

    #endregion

    #region Классификация — вспомогательные методы

    private static void RunClassifierDemo(
        ChartView cv, IClassifier cls, Vector[] feats, int[] labels,
        string title, int datasetKind, out string? textOut, out PlotlyBuilder? plotlyOut)
    {
        var pred = new int[feats.Length];
        for (int i = 0; i < feats.Length; i++) pred[i] = cls.Classify(feats[i]);

        var (acc, metricsText) = ClassifierMetrics(labels, pred);
        textOut = metricsText;

        var bounds = GetBounds(feats, padding: 0.6);
        int maxClass = 0;
        for (int i = 0; i < labels.Length; i++) maxClass = Math.Max(maxClass, labels[i]);
        for (int i = 0; i < pred.Length;   i++) maxClass = Math.Max(maxClass, pred[i]);
        int numClasses = maxClass + 1;

        cv.SetBackgroundImage(RenderDecisionBoundary(
            (x, y) =>
            {
                try { return cls.Classify(new Vector(new[] { x, y })); }
                catch { return 0; }
            },
            bounds, 380, 320, Palette, tintAlpha: 65, numClasses));

        cv.ChartName = $"{title}  —  точность {acc:F1}%";
        cv.LabelX = "x₁"; cv.LabelY = "x₂";
        PlotTwoClassesResult(cv, feats, labels, pred, numClasses);
        cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);

        plotlyOut = BuildDecisionBoundaryPlotly(
            (x, y) =>
            {
                try { return cls.Classify(new Vector(new[] { x, y })); }
                catch { return 0; }
            },
            bounds, feats, labels, numClasses, cv.ChartName, cv.LabelX, cv.LabelY);
    }

    private static SKImage RenderDecisionBoundary(
        Func<double, double, int> predict,
        (double xMin, double xMax, double yMin, double yMax) b,
        int gridW, int gridH, SKColor[] colors, byte tintAlpha, int numClasses)
    {
        var bmp = new SKBitmap(gridW, gridH);
        for (int py = 0; py < gridH; py++)
        {
            double yCoord = b.yMax - (b.yMax - b.yMin) * py / Math.Max(1, gridH - 1);
            for (int px = 0; px < gridW; px++)
            {
                double xCoord = b.xMin + (b.xMax - b.xMin) * px / Math.Max(1, gridW - 1);
                int cls = Math.Clamp(predict(xCoord, yCoord), 0, Math.Max(0, numClasses - 1));
                var c = colors[cls % colors.Length];
                bmp.SetPixel(px, py, new SKColor(c.Red, c.Green, c.Blue, tintAlpha));
            }
        }
        return SKImage.FromBitmap(bmp);
    }

    private static PlotlyBuilder BuildDecisionBoundaryPlotly(
        Func<double, double, int> predict,
        (double xMin, double xMax, double yMin, double yMax) bounds,
        Vector[] feats, int[] labels, int numClasses,
        string title, string labelX, string labelY)
    {
        var pb = new PlotlyBuilder { Title = title, AxisX = labelX, AxisY = labelY };
        const int gridRes = 80;
        double[] xGrid = new double[gridRes]; double[] yGrid = new double[gridRes];
        double dx = (bounds.xMax - bounds.xMin) / (gridRes - 1);
        double dy = (bounds.yMax - bounds.yMin) / (gridRes - 1);
        for (int i = 0; i < gridRes; i++) { xGrid[i] = bounds.xMin + i * dx; yGrid[i] = bounds.yMin + i * dy; }
        int[][] classGrid = new int[gridRes][];
        for (int j = 0; j < gridRes; j++)
        {
            classGrid[j] = new int[gridRes];
            for (int i = 0; i < gridRes; i++)
            {
                try { classGrid[j][i] = Math.Clamp(predict(xGrid[i], yGrid[j]), 0, Math.Max(0, numClasses - 1)); }
                catch { classGrid[j][i] = 0; }
            }
        }
        pb.AddHeatmapDiscrete(xGrid, yGrid, classGrid);
        for (int c = 0; c < numClasses; c++)
        {
            var xPts = new List<double>(); var yPts = new List<double>();
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] == c) { xPts.Add(feats[i][0]); yPts.Add(feats[i][1]); }
            if (xPts.Count > 0)
                pb.AddScatter2D(xPts.ToArray(), yPts.ToArray(), $"Класс {c}", PlotlyColors[c % PlotlyColors.Length], 6);
        }
        return pb;
    }

    private static void PlotTwoClassesResult(ChartView cv, Vector[] feats, int[] labels, int[] pred, int numClasses)
    {
        for (int c = 0; c < numClasses; c++)
        {
            var correct = new List<(double, double)>(); var wrong = new List<(double, double)>();
            for (int i = 0; i < feats.Length; i++)
            {
                if (labels[i] != c) continue;
                if (pred[i] == labels[i]) correct.Add((feats[i][0], feats[i][1]));
                else                       wrong.Add((feats[i][0], feats[i][1]));
            }
            if (correct.Count > 0)
            {
                var px = new Vector(correct.Count); var py = new Vector(correct.Count);
                for (int i = 0; i < correct.Count; i++) { px[i] = correct[i].Item1; py[i] = correct[i].Item2; }
                cv.AddScatterMark3(px, py, $"Класс {c}", Palette[c % Palette.Length]);
            }
            if (wrong.Count > 0)
            {
                var px = new Vector(wrong.Count); var py = new Vector(wrong.Count);
                for (int i = 0; i < wrong.Count; i++) { px[i] = wrong[i].Item1; py[i] = wrong[i].Item2; }
                var baseC = Palette[c % Palette.Length];
                var dark  = new SKColor((byte)(baseC.Red / 3), (byte)(baseC.Green / 3), (byte)(baseC.Blue / 3));
                cv.AddScatterMark6(px, py, $"Класс {c} (ошибочные)", dark);
            }
        }
    }

    #endregion

    #region Метрики классификации

    private static (double accuracy, string text) ClassifierMetrics(int[] labels, int[] pred)
    {
        int n = labels.Length;
        int tp = 0, tn = 0, fp = 0, fn = 0;
        for (int i = 0; i < n; i++)
        {
            if      (labels[i] == 1 && pred[i] == 1) tp++;
            else if (labels[i] == 0 && pred[i] == 0) tn++;
            else if (labels[i] == 0 && pred[i] == 1) fp++;
            else                                       fn++;
        }
        double acc     = 100.0 * (tp + tn) / n;
        double prec0   = tn + fn > 0 ? 100.0 * tn / (tn + fn) : 0;
        double rec0    = tn + fp > 0 ? 100.0 * tn / (tn + fp) : 0;
        double prec1   = tp + fp > 0 ? 100.0 * tp / (tp + fp) : 0;
        double rec1    = tp + fn > 0 ? 100.0 * tp / (tp + fn) : 0;
        double f1c0    = prec0 + rec0 > 0 ? 2 * prec0 * rec0 / (prec0 + rec0) : 0;
        double f1c1    = prec1 + rec1 > 0 ? 2 * prec1 * rec1 / (prec1 + rec1) : 0;
        double macroF1 = (f1c0 + f1c1) / 2;
        double mcc     = ComputeMcc(tp, tn, fp, fn);
        double bal     = ComputeBalancedAccuracy(tp, tn, fp, fn);

        var sb = new StringBuilder();
        sb.AppendLine("> Метрики классификации");
        sb.AppendLine();
        sb.AppendLine($"  Accuracy:           {acc,6:F1}%");
        sb.AppendLine($"  Balanced Accuracy:  {bal,6:F1}%");
        sb.AppendLine($"  Macro F1:           {macroF1,6:F1}%");
        sb.AppendLine($"  MCC (Matthews):     {mcc,6:F3}");
        sb.AppendLine();
        sb.AppendLine("  +-------------+----------+----------+----------+");
        sb.AppendLine("  |   Класс     | Precision|  Recall  |    F1    |");
        sb.AppendLine("  |-------------+----------+----------+----------|");
        sb.AppendLine($"  | Класс 0     | {prec0,6:F1}%  | {rec0,6:F1}%  | {f1c0,6:F1}%  |");
        sb.AppendLine($"  | Класс 1     | {prec1,6:F1}%  | {rec1,6:F1}%  | {f1c1,6:F1}%  |");
        sb.AppendLine("  +-------------+----------+----------+----------+");
        sb.AppendLine();
        sb.AppendLine("  Матрица ошибок (цветовая заливка — доля):");
        sb.AppendLine();
        int total = Math.Max(1, n);
        sb.AppendLine($"                   Предсказан 0     Предсказан 1");
        sb.AppendLine($"  Истинный 0:  {HeatCell(tn, total)}  {HeatCell(fp, total)}");
        sb.AppendLine($"  Истинный 1:  {HeatCell(fn, total)}  {HeatCell(tp, total)}");
        sb.AppendLine();
        sb.AppendLine($"  Всего: {n}     TP={tp}   TN={tn}   FP={fp}   FN={fn}");
        return (acc, sb.ToString());
    }

    private static string HeatCell(int value, int total)
    {
        double frac = (double)value / total;
        string shade = frac switch
        {
            < 0.05 => "·····",
            < 0.15 => ".....",
            < 0.30 => ":::::",
            < 0.50 => "#####",
            _      => "#####"
        };
        return $"{shade}{value,4}".PadRight(14);
    }

    private static double ComputeMcc(int tp, int tn, int fp, int fn)
    {
        double num = (double)tp * tn - (double)fp * fn;
        double den = Math.Sqrt((double)(tp + fp) * (tp + fn) * (tn + fp) * (tn + fn));
        return den < 1e-12 ? 0 : num / den;
    }

    private static double ComputeBalancedAccuracy(int tp, int tn, int fp, int fn)
    {
        double sens = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        double spec = tn + fp > 0 ? (double)tn / (tn + fp) : 0;
        return 100.0 * (sens + spec) / 2;
    }

    #endregion
}
