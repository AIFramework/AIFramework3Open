using System;
using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.Extensions;
using V2 = AI.ML.NeuralNetworks.V2;

namespace TestNNW;

/// <summary>
/// IO-демо для core-структур (Vector/Matrix/NDTensor) и V2 <see cref="V2.Tensor"/>.
/// </summary>
public static class TestIO
{
    private static readonly Random _rand = new(42);

    public static void Execute()
    {
        TestVector();
        TestMatrix();
        TestLegacyTensor();
        TestV2Tensor();
    }

    private static void TestVector()
    {
        Console.WriteLine("=== IO: Vector ===");
        var vector = new Vector(3);
        vector.Clear();
        for (int i = 0; i < _rand.Next(4, 10); i++)
            vector.Add(_rand.Next(10));
        Console.WriteLine($"Vector: {vector}");

        InMemoryDataStream stream = vector.ToDataStream();
        byte[] bytes = stream.AsByteArray();
        Console.WriteLine($"Bytes: [{bytes.Length}]");
        Console.WriteLine($"Bytes zipped: [{stream.Zip().AsByteArray().Length}]");
        Console.WriteLine($"Bytes unzipped: [{stream.UnZip().AsByteArray().Length}]");

        Vector fromBytes = stream.ReadVector();
        Console.WriteLine($"Vector from bytes: {fromBytes}");
        Console.WriteLine();
    }

    private static void TestMatrix()
    {
        Console.WriteLine("=== IO: Matrix ===");
        var matrix = new Matrix(_rand.Next(2, 7), _rand.Next(2, 7));
        for (int i = 0; i < matrix.Data.Length; i++)
            matrix.Data[i] = _rand.Next(10);

        Console.WriteLine($"Matrix:{Environment.NewLine}{matrix}");
        byte[] bytes = matrix.GetBytes();
        Console.WriteLine($"Bytes: [{bytes.Length}]");

        var fromBytes = Matrix.FromBytes(bytes);
        Console.WriteLine($"Matrix from bytes:{Environment.NewLine}{fromBytes}");
        Console.WriteLine();
    }

    private static void TestLegacyTensor()
    {
        Console.WriteLine("=== IO: Tensor (3D, AI.DataStructs.Algebraic) ===");
        var tensor = new Tensor(_rand.Next(2, 5), _rand.Next(2, 5), _rand.Next(1, 3));
        for (int i = 0; i < tensor.Data.Length; i++)
            tensor.Data[i] = _rand.Next(10);

        Console.WriteLine($"Tensor:{Environment.NewLine}{tensor}");
        byte[] bytes = tensor.GetBytes();
        Console.WriteLine($"Bytes: [{bytes.Length}]");

        var fromBytes = Tensor.FromBytes(bytes);
        Console.WriteLine($"Tensor from bytes:{Environment.NewLine}{fromBytes}");
        Console.WriteLine();
    }

    private static void TestV2Tensor()
    {
        Console.WriteLine("=== IO: V2 Tensor (PyTorch-style) ===");
        var data = new float[] { 1, 2, 3, 4, 5, 6 };
        var tensor = V2.Tensor.From(data, new V2.Shape(2, 3));
        Console.WriteLine($"V2 Tensor shape: {tensor.Shape}, dtype: {tensor.DType}, device: {tensor.Device}, contiguous: {tensor.IsContiguous}");
        Console.Write("V2 Tensor data: [");
        var span = tensor.AsReadOnlySpan<float>();
        for (int i = 0; i < span.Length; i++)
            Console.Write(i == 0 ? $"{span[i]}" : $", {span[i]}");
        Console.WriteLine("]");

        var reshaped = tensor.Reshape(3, 2);
        Console.WriteLine($"Reshaped: {reshaped.Shape}, share storage: {ReferenceEquals(tensor.Storage, reshaped.Storage)}");
        Console.WriteLine();
    }
}
