using System;
using System.Diagnostics;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;

namespace V2.MNIST;

/// <summary>
/// Демо: классификация на синтетическом MNIST-like датасете (28×28 grayscale, 10 классов).
/// </summary>
/// <remarks>
/// <para>
/// Образ генерируется как «цифра» — гауссовское пятно в случайной позиции,
/// помеченное классом 0..9 по углу относительно центра.
/// Это даёт нелинейную, но обучаемую задачу без зависимости от внешних данных.
/// </para>
/// <para>
/// Архитектура — классическая MLP <c>784 -> 128 -> 64 -> 10</c> с ReLU и AdamW.
/// </para>
/// </remarks>
internal static class Program
{
    private const int ImgDim = 28;
    private const int NumClasses = 10;
    private const int Train = 4096;
    private const int Test = 1024;
    private const int BatchSize = 128;
    private const int Epochs = 5;

    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var rng = new Random(42);

        Console.WriteLine($"V2.MNIST demo — синтетический датасет {Train} train + {Test} test.");

        var (xTrain, yTrain) = GenerateDataset(Train, rng);
        var (xTest, yTest) = GenerateDataset(Test, rng);

        var model = new Sequential(
            new Linear(ImgDim * ImgDim, 128, bias: true, rng: rng),
            new ReLU(),
            new Linear(128, 64, bias: true, rng: rng),
            new ReLU(),
            new Linear(64, NumClasses, bias: true, rng: rng));

        var optim = Adam.AdamW(model.Parameters(), lr: 1e-3f, weightDecay: 1e-4f);

        Console.WriteLine($"Параметров: {CountParameters(model)}");

        var sw = Stopwatch.StartNew();
        for (int epoch = 1; epoch <= Epochs; epoch++)
        {
            float epochLoss = 0f;
            int batches = 0;
            ShuffleInPlace(xTrain, yTrain, rng);

            for (int start = 0; start + BatchSize <= xTrain.GetLength(0); start += BatchSize)
            {
                var (xb, yb) = MakeBatch(xTrain, yTrain, start, BatchSize);
                var logits = model.Forward(xb);
                var loss = ClassificationLosses.CrossEntropy(logits, yb);
                loss.Backward();
                optim.Step();
                optim.ZeroGrad();
                epochLoss += loss.AsReadOnlySpan<float>()[0];
                batches++;
            }

            float testAcc = Evaluate(model, xTest, yTest);
            Console.WriteLine(
                $"Epoch {epoch}: loss={epochLoss / batches:F4}, test_acc={testAcc * 100:F2}%, " +
                $"elapsed={sw.Elapsed.TotalSeconds:F1}s");
        }
        Console.WriteLine("Готово.");
    }

    private static int CountParameters(Module m)
    {
        int n = 0;
        foreach (var p in m.Parameters()) n += (int)p.Tensor.Shape.NumElements;
        return n;
    }

    private static float Evaluate(Module model, float[,] x, int[] y)
    {
        int N = x.GetLength(0);
        int correct = 0;
        for (int start = 0; start < N; start += BatchSize)
        {
            int bs = Math.Min(BatchSize, N - start);
            var (xb, yb) = MakeBatch(x, y, start, bs);
            var logits = model.Forward(xb);
            var ls = logits.AsReadOnlySpan<float>();
            var ys = yb.AsReadOnlySpan<int>();
            for (int n = 0; n < bs; n++)
            {
                int best = 0;
                float bv = ls[n * NumClasses];
                for (int c = 1; c < NumClasses; c++)
                {
                    if (ls[n * NumClasses + c] > bv) { bv = ls[n * NumClasses + c]; best = c; }
                }
                if (best == ys[n]) correct++;
            }
        }
        return (float)correct / N;
    }

    private static (Tensor, Tensor) MakeBatch(float[,] x, int[] y, int start, int bs)
    {
        var xb = new float[bs * ImgDim * ImgDim];
        var yb = new int[bs];
        for (int i = 0; i < bs; i++)
        {
            for (int p = 0; p < ImgDim * ImgDim; p++)
                xb[i * ImgDim * ImgDim + p] = x[start + i, p];
            yb[i] = y[start + i];
        }
        return (Tensor.From(xb, new Shape(bs, ImgDim * ImgDim)),
                Tensor.From(yb, new Shape(bs)));
    }

    private static void ShuffleInPlace(float[,] x, int[] y, Random rng)
    {
        int N = x.GetLength(0), D = x.GetLength(1);
        for (int i = N - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            for (int k = 0; k < D; k++) (x[i, k], x[j, k]) = (x[j, k], x[i, k]);
            (y[i], y[j]) = (y[j], y[i]);
        }
    }

    /// <summary>
    /// Синтетика: гауссовский blob в случайной позиции.
    /// Метка — индекс из 10 равных угловых секторов вокруг центра.
    /// Картинки нормализованы в [0,1].
    /// </summary>
    private static (float[,], int[]) GenerateDataset(int count, Random rng)
    {
        var x = new float[count, ImgDim * ImgDim];
        var y = new int[count];
        const float center = (ImgDim - 1) / 2f;
        for (int n = 0; n < count; n++)
        {
            // Радиус 5..12 от центра, угол — равномерно.
            double r = 5 + rng.NextDouble() * 7;
            double ang = rng.NextDouble() * 2 * Math.PI;
            double cx = center + r * Math.Cos(ang);
            double cy = center + r * Math.Sin(ang);
            float sigma = 1.5f + 0.5f * (float)rng.NextDouble();
            for (int v = 0; v < ImgDim; v++)
            for (int u = 0; u < ImgDim; u++)
            {
                double du = u - cx, dv = v - cy;
                float val = MathF.Exp(-(float)((du * du + dv * dv) / (2.0 * sigma * sigma)));
                x[n, v * ImgDim + u] = val;
            }
            // Класс — сектор угла (детерминированно).
            int cls = (int)(((ang + 2 * Math.PI) % (2 * Math.PI)) / (2 * Math.PI / NumClasses));
            if (cls >= NumClasses) cls = NumClasses - 1;
            y[n] = cls;
        }
        return (x, y);
    }
}
