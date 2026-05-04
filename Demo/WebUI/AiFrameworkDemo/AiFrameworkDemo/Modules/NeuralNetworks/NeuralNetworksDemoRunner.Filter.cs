using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Filter (MLP-only) sequence prediction

    private static void RunFilterCase(
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int trainLen = Math.Max(150, (int)N("trainLen", 240));
        int predLen  = Math.Max(10,  (int)N("predLen",   40));
        int window   = Math.Clamp((int)N("window", 8), 4, 20);
        double freq  = Math.Clamp(N("freq", 0.12), 0.02, 0.49);
        int hidden   = Math.Clamp((int)N("hidden", 16), 8, 64);
        int epochs   = Math.Clamp((int)N("epochs", 80), 10, 200);
        float lr     = (float)Math.Clamp(N("lr", 5e-3), 1e-4, 0.05);
        var rng      = new Random(42);

        var series = new double[trainLen];
        for (int i = 0; i < trainLen; i++)
            series[i] = Math.Sin(2 * Math.PI * freq * i) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * i);

        int samples = trainLen - window;
        var xFlat = new float[samples * window];
        var yFlat = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            for (int j = 0; j < window; j++)
                xFlat[i * window + j] = (float)series[i + j];
            yFlat[i] = (float)series[i + window];
        }
        var xTrain = V2T.From(xFlat, new V2S(samples, window));
        var yTrain = V2T.From(yFlat, new V2S(samples, 1));

        var model = new Sequential(
            new Linear(window, hidden, true, rng),
            new ReLU(),
            new Linear(hidden, hidden, true, rng),
            new ReLU(),
            new Linear(hidden, 1, true, rng));

        var optim = new Adam(model.Parameters(), lr: lr);
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            optim.ZeroGrad();
            RegressionLosses.MSE(model.Forward(xTrain), yTrain).Backward();
            optim.Step();
        }

        double[] buf = series.Skip(trainLen - window).ToArray();
        var predicted = new double[trainLen + predLen];
        for (int i = 0; i < trainLen; i++) predicted[i] = series[i];
        for (int step = 0; step < predLen; step++)
        {
            using var _ = TapeContext.NoGrad();
            var inp = new float[window];
            for (int j = 0; j < window; j++) inp[j] = (float)buf[j];
            float next = model.Forward(V2T.From(inp, new V2S(1, window))).AsReadOnlySpan<float>()[0];
            predicted[trainLen + step] = next;
            for (int j = 0; j < window - 1; j++) buf[j] = buf[j + 1];
            buf[window - 1] = next;
        }

        var trainT = new Vector(trainLen); var trainY = new Vector(trainLen);
        var predT  = new Vector(predLen);  var predY  = new Vector(predLen);
        var trueT  = new Vector(predLen);  var trueY  = new Vector(predLen);
        for (int i = 0; i < trainLen; i++) { trainT[i] = i; trainY[i] = series[i]; }
        for (int i = 0; i < predLen; i++)
        {
            int t = trainLen + i;
            predT[i] = trueT[i] = t;
            predY[i] = predicted[t];
            trueY[i] = Math.Sin(2 * Math.PI * freq * t) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * t);
        }
        double mse = 0;
        for (int i = 0; i < predLen; i++) { double d = predY[i] - trueY[i]; mse += d * d; }
        mse /= predLen;

        cv.ChartName = $"Filter (MLP {window}->{hidden}->1)  —  окно {window}, MSE={mse:F4}";
        cv.LabelX = "Время"; cv.LabelY = "Значение";
        cv.AddPlot(trainT, trainY, "Обучение", Palette[0], width: 2);
        cv.AddPlot(trueT,  trueY,  "Истина",   Palette[2], width: 2);
        cv.AddPlot(predT,  predY,  "Прогноз",  Palette[1], width: 3);
        textOut = $"Filter (MLP)  hidden={hidden}  window={window}  epochs={epochs}  lr={lr}  MSE={mse:F5}";
    }

    #endregion
}
