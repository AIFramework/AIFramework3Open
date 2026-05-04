using System;
using AI.ML.NeuralNetworks.V2.Nn;

namespace TestNNW;

/// <summary>
/// Демо ToString-вывода для V2-модулей.
/// </summary>
public static class TestLayersToString
{
    public static void Execute()
    {
        Console.WriteLine("=== V2 Modules ToString ===");

        Print(new Linear(64, 32));
        Print(new Linear(32, 10, bias: false));
        Print(new ReLU());
        Print(new Sigmoid());
        Print(new Tanh());
        Print(new GELU());
        Print(new SiLU());
        Print(new Dropout(0.25f));
        Print(new LayerNorm(new[] { 32 }));
        Print(new RMSNorm(64));
        Print(new BatchNorm1d(32));
        Print(new BatchNorm2d(8));
        Print(new GroupNorm(4, 32));
        Print(new InstanceNorm(8));
        Print(new Embedding(1000, 64));
        Print(new Conv1d(3, 16, kernelSize: 3));
        Print(new Conv2d(3, 16, kernelSize: 3));
        Print(new ConvTranspose2d(16, 8, kernelSize: 2));
        Print(new MaxPool2d(2));
        Print(new AvgPool2d(2));
        Print(new AdaptiveAvgPool2d((7, 7)));
        Print(new SinusoidalPositionalEncoding(embedDim: 64, maxLen: 512));
        Print(new RNNCell(32, 64));
        Print(new LSTMCell(32, 64));
        Print(new GRUCell(32, 64));
        Print(new RNN(32, 64));
        Print(new LSTM(32, 64));
        Print(new GRU(32, 64));
        Print(new MultiHeadAttention(embedDim: 64, numHeads: 4));
        Print(new TransformerEncoderLayer(dModel: 64, nHead: 4, dimFeedforward: 128));
        Print(new TransformerDecoderLayer(dModel: 64, nHead: 4, dimFeedforward: 128));

        var sequential = new Sequential(
            new Linear(28 * 28, 128),
            new ReLU(),
            new Dropout(0.2f),
            new Linear(128, 64),
            new ReLU(),
            new Linear(64, 10));
        Print(sequential);
        Console.WriteLine();
    }

    private static void Print(Module module)
    {
        long paramCount = 0;
        foreach (var (_, p) in module.NamedParameters())
            paramCount += p.Tensor.Shape.NumElements;
        Console.WriteLine($"  {module.GetType().Name,-30} params={paramCount}");
    }
}
