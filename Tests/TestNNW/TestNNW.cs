using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;

namespace TestNNW;

/// <summary>
/// Демо обучения простой V2-сети (Sequential + Linear + ReLU + Adam + MSE).
/// Прямой аналог старого <c>NeuralNetworkManager.TrainNet</c> на двух парах вход/выход.
/// </summary>
public static class TestNNW
{
    public static void Execute()
    {
        Console.WriteLine("=== V2 train: 2-input -> hidden(130, ReLU) -> 3-output ===");

        var net = new Sequential(
            new Linear(2, 130),
            new ReLU(),
            new Linear(130, 3));

        Tensor x = Tensor.From(new float[]
        {
            0.9f, 0.1f,
            0.1f, 0.9f
        }, new Shape(2, 2));

        Tensor target = Tensor.From(new float[]
        {
            0.23f, -0.10f, 0.60f,
            -0.90f, 0.80f, 0.40f
        }, new Shape(2, 3));

        var optim = new Adam(net.Parameters(), lr: 1e-2f);

        const int epochs = 200;
        float loss0 = float.NaN;
        float lossN = float.NaN;
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            optim.ZeroGrad();
            Tensor y = net.Forward(x);
            Tensor loss = RegressionLosses.MSE(y, target);
            loss.Backward();
            optim.Step();
            float l = loss.AsReadOnlySpan<float>()[0];
            if (epoch == 0) loss0 = l;
            lossN = l;
        }
        Console.WriteLine($"  loss[0]={loss0:F6}, loss[{epochs - 1}]={lossN:F6}, ratio={lossN / loss0:E2}");
        Console.WriteLine();
    }
}
