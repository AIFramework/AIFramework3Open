using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Data;

namespace TestNNW;

/// <summary>
/// Демо V2 <see cref="TensorDataset"/> + <see cref="DataLoader{TItem,TBatch}"/>.
/// </summary>
public static class TestDataSets
{
    public static void Execute()
    {
        Console.WriteLine("=== V2 Dataset / DataLoader ===");

        Tensor x = Tensor.From(new float[]
        {
            0.9f, 0.1f,
            0.1f, 0.9f,
            0.5f, 0.5f,
            0.2f, 0.8f
        }, new Shape(4, 2));
        Tensor y = Tensor.From(new float[]
        {
            0.23f, -0.10f, 0.60f,
            -0.90f, 0.80f, 0.40f,
            0.50f, 0.50f, 0.50f,
            -0.30f, 0.20f, 0.70f
        }, new Shape(4, 3));

        var dataset = new TensorDataset(x, y);
        Console.WriteLine($"Dataset: {dataset.Count} samples, X[0].shape={dataset.Get(0).x.Shape}, Y[0].shape={dataset.Get(0).y.Shape}");

        int batches = 0;
        var loader = new DataLoader<(Tensor x, Tensor y), (Tensor x, Tensor y)>(
            dataset,
            batchSize: 2,
            Collate.StackPair,
            shuffle: false);

        foreach (var (bx, by) in loader)
        {
            batches++;
            Console.WriteLine($"  Batch #{batches}: X.shape={bx.Shape}, Y.shape={by.Shape}");
        }
        Console.WriteLine($"Total batches: {batches}");
        Console.WriteLine();
    }
}
