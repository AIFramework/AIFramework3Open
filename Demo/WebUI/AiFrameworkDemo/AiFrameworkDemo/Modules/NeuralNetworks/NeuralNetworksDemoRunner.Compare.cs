using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Optim;
using SkiaSharp;
using System.Diagnostics;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Architecture comparison

    private static void RunCompareCase(
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int trainLen = Math.Max(150, (int)N("trainLen", 240));
        int predLen  = Math.Max(10,  (int)N("predLen",   40));
        int window   = Math.Clamp((int)N("window", 8), 4, 20);
        int epochs   = Math.Clamp((int)N("epochs", 60), 10, 200);
        const double freq = 0.12;
        const int H  = 16;
        var rng      = new Random(42);

        var series = new double[trainLen];
        for (int i = 0; i < trainLen; i++)
            series[i] = Math.Sin(2 * Math.PI * freq * i) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * i);

        int samples = trainLen - window;
        var xWin = new float[samples * window];
        var xFlat = new float[samples * window];
        var yWin = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            for (int j = 0; j < window; j++)
            {
                xWin[i * window + j] = (float)series[i + j];
                xFlat[i * window + j] = (float)series[i + j];
            }
            yWin[i] = (float)series[i + window];
        }
        var xSeq  = V2T.From(xWin, new V2S(samples, window, 1));
        var xMlp  = V2T.From(xFlat, new V2S(samples, window));
        var yTrain = V2T.From(yWin, new V2S(samples, 1));

        var truth = new double[predLen];
        for (int i = 0; i < predLen; i++)
        {
            int t = trainLen + i;
            truth[i] = Math.Sin(2 * Math.PI * freq * t) + 0.3 * Math.Sin(2 * Math.PI * freq * 3 * t);
        }

        var archNames = new[] { "Filter", "RNN", "LSTM", "GRU", "Transformer" };
        var results   = new (double[] pred, double mse, long trainMs, long inferMs)[archNames.Length];

        for (int a = 0; a < archNames.Length; a++)
        {
            rng = new Random(42);
            Module model;
            bool isMlp = false;

            switch (archNames[a])
            {
                case "Filter":
                    model = new Sequential(
                        new Linear(window, H, true, rng), new ReLU(),
                        new Linear(H, H, true, rng), new ReLU(),
                        new Linear(H, 1, true, rng));
                    isMlp = true;
                    break;
                case "RNN":
                    model = new RnnPredictor(new RNN(1, H, "tanh", true, true, rng), H, rng);
                    break;
                case "LSTM":
                    model = new LstmPredictor(1, H, rng);
                    break;
                case "GRU":
                    model = new GruPredictor(1, H, rng);
                    break;
                default: // Transformer
                    model = new TransformerSeqPredictor(H, 2, H * 4, window, rng);
                    break;
            }

            var xIn = isMlp ? xMlp : xSeq;
            var optim = new Adam(model.Parameters(), lr: archNames[a] == "Transformer" ? 5e-3f : 1e-2f);

            var sw = Stopwatch.StartNew();
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                optim.ZeroGrad();
                RegressionLosses.MSE(model.Forward(xIn), yTrain).Backward();
                optim.Step();
            }
            sw.Stop();
            long trainMs = sw.ElapsedMilliseconds;

            sw.Restart();
            double[] buf = series.Skip(trainLen - window).ToArray();
            var pred = new double[predLen];
            for (int step = 0; step < predLen; step++)
            {
                using var _ = TapeContext.NoGrad();
                V2T inp;
                if (isMlp)
                {
                    var f = new float[window];
                    for (int j = 0; j < window; j++) f[j] = (float)buf[j];
                    inp = V2T.From(f, new V2S(1, window));
                }
                else
                {
                    var f = new float[window];
                    for (int j = 0; j < window; j++) f[j] = (float)buf[j];
                    inp = V2T.From(f, new V2S(1, window, 1));
                }
                float next = model.Forward(inp).AsReadOnlySpan<float>()[0];
                pred[step] = next;
                for (int j = 0; j < window - 1; j++) buf[j] = buf[j + 1];
                buf[window - 1] = next;
            }
            sw.Stop();
            long inferMs = sw.ElapsedMilliseconds;

            double mse = 0;
            for (int i = 0; i < predLen; i++) { double d = pred[i] - truth[i]; mse += d * d; }
            mse /= predLen;
            results[a] = (pred, mse, trainMs, inferMs);
        }

        var trainT = new Vector(trainLen); var trainY = new Vector(trainLen);
        for (int i = 0; i < trainLen; i++) { trainT[i] = i; trainY[i] = series[i]; }
        var trueT = new Vector(predLen); var trueY = new Vector(predLen);
        for (int i = 0; i < predLen; i++) { trueT[i] = trainLen + i; trueY[i] = truth[i]; }

        cv.ChartName = "Сравнение 5 архитектур на прогнозе временного ряда";
        cv.LabelX = "Время"; cv.LabelY = "Значение";
        cv.AddPlot(trainT, trainY, "Обучение", new SKColor(0x94, 0xA3, 0xB8), width: 1);
        cv.AddPlot(trueT,  trueY,  "Истина",   new SKColor(0x64, 0x74, 0x8B), width: 2);

        for (int a = 0; a < archNames.Length; a++)
        {
            var predT = new Vector(predLen); var predY = new Vector(predLen);
            for (int i = 0; i < predLen; i++) { predT[i] = trainLen + i; predY[i] = results[a].pred[i]; }
            cv.AddPlot(predT, predY, archNames[a], Palette[a % Palette.Length], width: 2);
        }

        var sb = new StringBuilder();
        sb.AppendLine("> Сравнение архитектур нейросетей");
        sb.AppendLine();
        sb.AppendLine($"  Общие параметры: window={window}  epochs={epochs}  trainLen={trainLen}  predLen={predLen}");
        sb.AppendLine();
        sb.AppendLine($"  {"Архитектура",-16} {"MSE",10} {"Обучение",12} {"Инференс",12}");
        sb.AppendLine($"  {new string('-', 52)}");
        for (int a = 0; a < archNames.Length; a++)
        {
            var r = results[a];
            sb.AppendLine($"  {archNames[a],-16} {r.mse,10:F5} {r.trainMs + " мс",12} {r.inferMs + " мс",12}");
        }

        int bestIdx = 0;
        for (int a = 1; a < archNames.Length; a++)
            if (results[a].mse < results[bestIdx].mse) bestIdx = a;
        sb.AppendLine();
        sb.AppendLine($"  Лучшая точность: {archNames[bestIdx]} (MSE={results[bestIdx].mse:F5})");

        textOut = sb.ToString();
    }

    /// <summary>Simple RNN predictor: RNN -> last hidden -> Linear(H,1).</summary>
    private sealed class RnnPredictor : Module
    {
        private readonly RNN    _rnn;
        private readonly Linear _head;

        public RnnPredictor(RNN rnn, int hiddenSize, Random rng)
        {
            _rnn  = RegisterModule("rnn",  rnn);
            _head = RegisterModule("head", new Linear(hiddenSize, 1, true, rng));
        }

        public override V2T Forward(V2T input)
        {
            int T = input.Shape[1];
            var (outputs, _) = _rnn.ForwardSeq(input);
            var last = IndexingOps.Narrow(outputs, 1, T - 1, 1).Squeeze(1);
            return _head.Forward(last);
        }
    }

    #endregion
}
