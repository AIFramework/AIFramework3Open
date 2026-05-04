using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Optim;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Последовательности — GRU

    private static void RunSequenceCase(
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int trainLen = Math.Max(150, (int)N("trainLen", 240));
        int predLen  = Math.Max(10,  (int)N("predLen",   40));
        int window   = Math.Clamp((int)N("window", 8), 4, 20);
        double freq  = Math.Clamp(N("freq", 0.12), 0.02, 0.49);
        const int H  = 16;
        var rng      = new Random(42);

        var series = new double[trainLen];
        for (int i = 0; i < trainLen; i++)
            series[i] = Math.Sin(2 * Math.PI * freq * i) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * i);

        int samples = trainLen - window;
        var xWin = new float[samples * window]; var yWin = new float[samples];
        for (int i = 0; i < samples; i++) { for (int j = 0; j < window; j++) xWin[i * window + j] = (float)series[i + j]; yWin[i] = (float)series[i + window]; }
        var xTrain = V2T.From(xWin, new V2S(samples, window, 1));
        var yTrain = V2T.From(yWin, new V2S(samples, 1));

        var model = new GruPredictor(inputSize: 1, hiddenSize: H, rng: rng);
        var optim = new Adam(model.Parameters(), lr: 5e-3f);
        for (int epoch = 0; epoch < 80; epoch++) { optim.ZeroGrad(); RegressionLosses.MSE(model.Forward(xTrain), yTrain).Backward(); optim.Step(); }

        double[] buf = series.Skip(trainLen - window).ToArray();
        var predicted = new double[trainLen + predLen];
        for (int i = 0; i < trainLen; i++) predicted[i] = series[i];
        for (int step = 0; step < predLen; step++)
        {
            using var _ = TapeContext.NoGrad();
            var inp = new float[window]; for (int j = 0; j < window; j++) inp[j] = (float)buf[j];
            float next = model.Forward(V2T.From(inp, new V2S(1, window, 1))).AsReadOnlySpan<float>()[0];
            predicted[trainLen + step] = next;
            for (int j = 0; j < window - 1; j++) buf[j] = buf[j + 1]; buf[window - 1] = next;
        }

        var trainT = new Vector(trainLen); var trainY = new Vector(trainLen);
        var predT  = new Vector(predLen);  var predY  = new Vector(predLen);
        var trueT  = new Vector(predLen);  var trueY  = new Vector(predLen);
        for (int i = 0; i < trainLen; i++) { trainT[i] = i; trainY[i] = series[i]; }
        for (int i = 0; i < predLen; i++) { int t = trainLen + i; predT[i] = trueT[i] = t; predY[i] = predicted[t]; trueY[i] = Math.Sin(2 * Math.PI * freq * t) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * t); }
        double mse = 0; for (int i = 0; i < predLen; i++) { double d = predY[i] - trueY[i]; mse += d * d; } mse /= predLen;

        cv.ChartName = $"GRU({H})  —  окно {window}, горизонт {predLen}, MSE={mse:F4}";
        cv.LabelX = "Время"; cv.LabelY = "Значение";
        cv.AddPlot(trainT, trainY, "Обучение",  Palette[0], width: 2);
        cv.AddPlot(trueT,  trueY,  "Истина",    Palette[2], width: 2);
        cv.AddPlot(predT,  predY,  "Прогноз",   Palette[1], width: 3);
        textOut = $"GRU({H})  window={window}  predLen={predLen}  freq={freq:F3}  MSE={mse:F5}";
    }

    #endregion

    #region GruPredictor (вложенная модель)

    /// <summary>GRU(inputSize, H) -> last-step hidden -> Linear(H, 1).</summary>
    private sealed class GruPredictor : Module
    {
        private readonly GRU    _gru;
        private readonly Linear _head;

        public GruPredictor(int inputSize, int hiddenSize, Random rng)
        {
            _gru  = RegisterModule("gru",  new GRU(inputSize, hiddenSize, true, true, rng));
            _head = RegisterModule("head", new Linear(hiddenSize, 1, true, rng));
        }

        public override V2T Forward(V2T input)
        {
            int T = input.Shape[1];
            var (outputs, _) = _gru.ForwardSeq(input);
            var last = IndexingOps.Narrow(outputs, 1, T - 1, 1).Squeeze(1);
            return _head.Forward(last);
        }
    }

    #endregion
}
