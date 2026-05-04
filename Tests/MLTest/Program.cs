using System;
using AI.DataStructs.Algebraic;
using AI.ML.Classification;
using AI.ML.DataHandling.FeaturesTransforms;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using AI.Statistics;
using Tensor = AI.ML.NeuralNetworks.V2.Tensor;

namespace MLTest;

internal static class Program
{
    private static void Main()
    {
        AutoEncoderV2Test();
        PcaTest();
        SVMTest();
    }

    /// <summary>
    /// Линейный автоэнкодер, написанный поверх V2 (PyTorch-style),
    /// заменяющий устаревший <c>AI.ML.DataHandling.FeaturesTransforms.AutoEncoder</c>.
    /// </summary>
    private static void AutoEncoderV2Test()
    {
        const int Features = 30;
        const int Latent = 4;
        const int Epochs = 50;

        var random = new Random(110);
        Matrix corr = Statistic.UniformDistribution(Features, Features, random);
        Matrix data = Statistic.UniformDistribution(1690, Features, random) * corr;

        var encoder = new Sequential(
            new Linear(Features, 2 * Latent),
            new ReLU(),
            new Linear(2 * Latent, Latent));

        var decoder = new Sequential(
            new ReLU(),
            new Linear(Latent, Features));

        var model = new Sequential(encoder, decoder);

        var optim = new Adam(model.Parameters(), lr: 1e-2f);

        Tensor x = MatrixToTensor(data);

        for (int epoch = 0; epoch < Epochs; epoch++)
        {
            optim.ZeroGrad();
            Tensor y = model.Forward(x);
            Tensor loss = RegressionLosses.MSE(y, x);
            loss.Backward();
            optim.Step();
        }

        Tensor latent;
        using (TapeContext.NoGrad())
            latent = encoder.Forward(x);

        Matrix latentMatrix = TensorToMatrix(latent);

        Console.WriteLine("\n=== AutoEncoder (V2, MSE) ===");
        Console.WriteLine("Ковариационная матрица до автоэнкодера:");
        Console.WriteLine(Matrix.GetCovMatrixFromColumns(data).Round(3));
        Console.WriteLine("\nКовариационная матрица в латентном пространстве:");
        Console.WriteLine(Matrix.GetCovMatrixFromColumns(latentMatrix).Round(3));
    }

    private static void PcaTest()
    {
        const int Samples = 300;
        const int Dim = 5;

        var random = new Random(42);
        var matrix = new Matrix(Samples, Dim);
        for (int i = 0; i < Samples; i++)
        {
            double t = random.NextDouble();
            // первые две оси несут сигнал, остальные — шум.
            matrix[i, 0] = t;
            matrix[i, 1] = 2 * t + 0.05 * (random.NextDouble() - 0.5);
            for (int j = 2; j < Dim; j++)
                matrix[i, j] = 0.01 * (random.NextDouble() - 0.5);
        }

        var pca = new PCA(2) { Iterations = 200, Eps = 1 };
        pca.Train(matrix);

        Console.WriteLine("\n=== PCA ===");
        Console.WriteLine("Ковариационная матрица до PCA:");
        Console.WriteLine(Matrix.GetCovMatrixFromColumns(matrix).Round(3));
        Console.WriteLine("\nКовариационная матрица после PCA:");
        Console.WriteLine(Matrix.GetCovMatrixFromColumns(pca.Transform(matrix, true)).Round(3));
        Console.WriteLine($"\nПроцент сохранённой энергии: {pca.Info.InfoSaveEnergy * 100:0.##}%");
    }

    private static void SVMTest()
    {
        int[] t = { 0, 1 };
        Vector x = new[] { 222.0, 993, 110 };
        Vector x2 = new[] { 222.0, 993, 109 };
        Vector[] X = { x, x2 };

        X = Vector.ScaleData(X);

        var svm = new SVMBinary(3)
        {
            MinimalMargin = 0.2,
            L2 = 0.1,
            L1 = 0.01,
            C = 2,
            LearningRate = 0.1,
            NumSupportVectors = 1,
            EpochesToPass = 200
        };

        svm.Train(X, t);

        double y = svm.Classify(X[0]);
        double y2 = svm.Classify(X[1]);
        Console.WriteLine("\n=== SVMBinary ===");
        Console.WriteLine($"cl:0 y = {y}; cl:1 y = {y2}");
    }

    private static Tensor MatrixToTensor(Matrix m)
    {
        int rows = m.Height;
        int cols = m.Width;
        float[] flat = new float[rows * cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                flat[i * cols + j] = (float)m[i, j];
        return Tensor.From(flat, new Shape(rows, cols));
    }

    private static Matrix TensorToMatrix(Tensor t)
    {
        int rows = t.Shape[0];
        int cols = t.Shape[1];
        var contig = t.IsContiguous ? t : t.Contiguous();
        var span = contig.AsSpan<float>();
        var m = new Matrix(rows, cols);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                m[i, j] = span[i * cols + j];
        return m;
    }
}
