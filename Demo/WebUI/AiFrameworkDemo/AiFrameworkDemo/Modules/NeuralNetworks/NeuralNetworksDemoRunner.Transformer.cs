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
    #region Transformer sequence prediction

    private static void RunTransformerCase(
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int trainLen = Math.Max(150, (int)N("trainLen", 240));
        int predLen  = Math.Max(10,  (int)N("predLen",   40));
        int window   = Math.Clamp((int)N("window", 8), 4, 20);
        double freq  = Math.Clamp(N("freq", 0.12), 0.02, 0.49);
        int dModel   = Math.Clamp((int)N("dModel", 16), 8, 64);
        int nHead    = Math.Clamp((int)N("nHead", 2), 1, 4);
        int epochs   = Math.Clamp((int)N("epochs", 80), 10, 200);
        float lr     = (float)Math.Clamp(N("lr", 5e-3), 1e-4, 0.05);
        var rng      = new Random(42);

        // dModel must be divisible by nHead
        if (dModel % nHead != 0)
            dModel = nHead * Math.Max(1, dModel / nHead);

        var series = new double[trainLen];
        for (int i = 0; i < trainLen; i++)
            series[i] = Math.Sin(2 * Math.PI * freq * i) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * i);

        int samples = trainLen - window;
        var xWin = new float[samples * window];
        var yWin = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            for (int j = 0; j < window; j++)
                xWin[i * window + j] = (float)series[i + j];
            yWin[i] = (float)series[i + window];
        }
        var xTrain = V2T.From(xWin, new V2S(samples, window, 1));
        var yTrain = V2T.From(yWin, new V2S(samples, 1));

        var model = new TransformerSeqPredictor(dModel, nHead, dFf: dModel * 4, window, rng);
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
            float next = model.Forward(V2T.From(inp, new V2S(1, window, 1))).AsReadOnlySpan<float>()[0];
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

        cv.ChartName = $"Transformer (d={dModel}, h={nHead})  —  окно {window}, MSE={mse:F4}";
        cv.LabelX = "Время"; cv.LabelY = "Значение";
        cv.AddPlot(trainT, trainY, "Обучение", Palette[0], width: 2);
        cv.AddPlot(trueT,  trueY,  "Истина",   Palette[2], width: 2);
        cv.AddPlot(predT,  predY,  "Прогноз",  Palette[1], width: 3);
        textOut = $"Transformer  d_model={dModel}  nHead={nHead}  window={window}  epochs={epochs}  lr={lr}  MSE={mse:F5}";
    }

    /// <summary>
    /// Linear(1->dModel) -> PositionalEncoding -> TransformerEncoderLayer -> last-step -> Linear(dModel->1).
    /// </summary>
    private sealed class TransformerSeqPredictor : Module
    {
        private readonly Linear _proj;
        private readonly SinusoidalPositionalEncoding _pe;
        private readonly TransformerEncoderLayer _enc;
        private readonly Linear _head;
        private readonly int _window;

        public TransformerSeqPredictor(int dModel, int nHead, int dFf, int window, Random rng)
        {
            _window = window;
            _proj = RegisterModule("proj", new Linear(1, dModel, true, rng));
            _pe   = RegisterModule("pe",   new SinusoidalPositionalEncoding(dModel, maxLen: 1024));
            _enc  = RegisterModule("enc",  new TransformerEncoderLayer(
                dModel, nHead, dimFeedforward: dFf, dropout: 0f,
                activation: "gelu", normFirst: true, rng: rng));
            _head = RegisterModule("head", new Linear(dModel, 1, true, rng));
        }

        public override V2T Forward(V2T input)
        {
            var h = _proj.Forward(input);
            h = _pe.Forward(h);
            h = _enc.Forward(h);
            var last = AI.ML.NeuralNetworks.V2.Ops.IndexingOps.Narrow(h, 1, _window - 1, 1).Squeeze(1);
            return _head.Forward(last);
        }
    }

    #endregion
}
