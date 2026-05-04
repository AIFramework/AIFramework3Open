using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using System.Text;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Признаки — Автоэнкодер

    private static void RunFeaturesCase(
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int n      = Math.Max(50, (int)N("n",       200));
        int latent = Math.Clamp((int)N("latent", 1), 1, 3);
        int epochs = Math.Max(10, (int)N("epochs",   80));
        float lr   = (float)Math.Clamp(N("lr", 0.01), 0.0001, 0.5);
        int ds     = Math.Clamp((int)N("dataset", 0), 0, 2);
        var rng    = new Random(42);

        var data  = MakeManifoldData(n, ds, 7);
        var xData = new float[n * 2];
        for (int i = 0; i < n; i++) { xData[i * 2] = (float)data[i][0]; xData[i * 2 + 1] = (float)data[i][1]; }
        var xFull = V2T.From(xData, new V2S(n, 2));

        var enc = new Sequential(new Linear(2, Math.Max(latent * 2, 4), true, rng), new ReLU(), new Linear(Math.Max(latent * 2, 4), latent, true, rng));
        var dec = new Sequential(new Linear(latent, Math.Max(latent * 2, 4), true, rng), new ReLU(), new Linear(Math.Max(latent * 2, 4), 2, true, rng));
        var ae  = new Sequential(enc, dec);
        var optim = new Adam(ae.Parameters(), lr: lr);
        double firstLoss = 0, lastLoss = 0;
        var lossHistory = new List<double>(epochs);

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            optim.ZeroGrad();
            var loss = RegressionLosses.MSE(ae.Forward(xFull), xFull);
            loss.Backward();
            optim.Step();
            double lv = loss.AsReadOnlySpan<float>()[0];
            lossHistory.Add(lv);
            if (epoch == 0) firstLoss = lv;
            lastLoss = lv;
        }

        double[] latentVals = new double[n];
        { using var _ = TapeContext.NoGrad(); var codes = enc.Forward(xFull).AsReadOnlySpan<float>(); for (int i = 0; i < n; i++) latentVals[i] = codes[i * latent]; }

        double lMin = latentVals.Min(), lMax = latentVals.Max();
        var origX = new Vector(n); var origY = new Vector(n);
        for (int i = 0; i < n; i++) { origX[i] = data[i][0]; origY[i] = data[i][1]; }

        cv.ChartName = $"Автоэнкодер 2->{latent} (латент) | потеря {lastLoss:F4}";
        cv.LabelX = "x₁"; cv.LabelY = "x₂";
        cv.AddScatterMark3(origX, origY, "Исходные точки", Palette[0]);

        int bins = 5;
        for (int bi = 0; bi < bins; bi++)
        {
            double lo = lMin + (lMax - lMin) * bi / bins;
            double hi = lMin + (lMax - lMin) * (bi + 1) / bins;
            var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < n; i++) if (latentVals[i] >= lo && (latentVals[i] < hi || bi == bins - 1)) { xs.Add(data[i][0]); ys.Add(data[i][1]); }
            if (xs.Count == 0) continue;
            var vx = new Vector(xs.Count); var vy = new Vector(xs.Count);
            for (int i = 0; i < xs.Count; i++) { vx[i] = xs[i]; vy[i] = ys[i]; }
            cv.AddScatterMark6(vx, vy, $"латент ∈ [{lo:F2}; {hi:F2}]", Palette[bi % Palette.Length]);
        }
        textOut = BuildAutoEncoderReport(firstLoss, lastLoss, lossHistory, latent, lMin, lMax);
    }

    private static string BuildAutoEncoderReport(
        double firstLoss, double lastLoss, List<double> history,
        int latent, double lMin, double lMax)
    {
        var sb = new StringBuilder();
        sb.AppendLine("> Автоэнкодер: отчёт");
        sb.AppendLine();
        sb.AppendLine($"  Размерность латента:  {latent}");
        sb.AppendLine($"  Диапазон латента:     [{lMin:F3}; {lMax:F3}]");
        sb.AppendLine();
        if (history.Count > 0)
        {
            sb.AppendLine($"  Потеря (эпоха   1):  {firstLoss:F5}");
            sb.AppendLine($"  Потеря (эпоха {history.Count,3}):  {lastLoss:F5}");
            sb.AppendLine($"  Снижение:            ×{firstLoss / Math.Max(1e-12, lastLoss):F2}");
            sb.AppendLine();
            sb.AppendLine("  Профиль обучения:");
            int stride = Math.Max(1, history.Count / 12);
            for (int i = 0; i < history.Count; i += stride)
            {
                double v  = history[i];
                int bar   = Math.Clamp((int)(40 * (v - lastLoss) / Math.Max(1e-12, firstLoss - lastLoss)), 0, 40);
                sb.AppendLine($"  эп.{i,4}:  {v,9:F5}  {new string('#', bar)}");
            }
        }
        return sb.ToString();
    }

    #endregion
}
