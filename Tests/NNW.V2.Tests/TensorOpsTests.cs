using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Ops;
using Xunit;

namespace NNW.V2.Tests;

/// <summary>
/// Forward-проверки поэлементных операций и матричных умножений.
/// </summary>
public class TensorOpsTests
{
    [Fact]
    public void Add_TwoTensors_ElementwiseSum()
    {
        var a = Tensor.From(new float[] { 1, 2, 3 }, new Shape(3));
        var b = Tensor.From(new float[] { 4, 5, 6 }, new Shape(3));
        var c = TensorOps.Add(a, b);
        Assert.Equal(new float[] { 5, 7, 9 }, c.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Add_BroadcastsRowVector()
    {
        var a = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var b = Tensor.From(new float[] { 10, 20, 30 }, new Shape(3));
        var c = TensorOps.Add(a, b);
        Assert.Equal(new Shape(2, 3), c.Shape);
        Assert.Equal(new float[] { 11, 22, 33, 14, 25, 36 }, c.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Mul_BroadcastsColumnVector()
    {
        var a = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var b = Tensor.From(new float[] { 10, 100 }, new Shape(2, 1));
        var c = TensorOps.Mul(a, b);
        Assert.Equal(new float[] { 10, 20, 30, 400, 500, 600 }, c.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Relu_ZerosNegatives()
    {
        var x = Tensor.From(new float[] { -1, 2, -3, 4 }, new Shape(4));
        var y = TensorOps.Relu(x);
        Assert.Equal(new float[] { 0, 2, 0, 4 }, y.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Sigmoid_AtZero_ReturnsHalf()
    {
        var x = Tensor.From(new float[] { 0 }, new Shape(1));
        var y = TensorOps.Sigmoid(x);
        Assert.Equal(0.5f, y.AsReadOnlySpan<float>()[0], 5);
    }

    [Fact]
    public void Sum_ReducesAllElementsToScalar()
    {
        var x = Tensor.From(new float[] { 1, 2, 3, 4 }, new Shape(2, 2));
        var s = TensorOps.Sum(x);
        Assert.Equal(0, s.Rank);
        Assert.Equal(10f, s.AsReadOnlySpan<float>()[0]);
    }

    [Fact]
    public void Sum_AlongAxis_KeepDim()
    {
        var x = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var s = TensorOps.Sum(x, axis: 1, keepDim: true);
        Assert.Equal(new Shape(2, 1), s.Shape);
        Assert.Equal(new float[] { 6, 15 }, s.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Mean_AllElements_DividesBySize()
    {
        var x = Tensor.From(new float[] { 2, 4, 6 }, new Shape(3));
        var m = TensorOps.Mean(x);
        Assert.Equal(4f, m.AsReadOnlySpan<float>()[0], 5);
    }

    [Fact]
    public void MatMul_2x3_Times_3x2_Gives_2x2()
    {
        var a = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var b = Tensor.From(new float[] { 7, 8, 9, 10, 11, 12 }, new Shape(3, 2));
        var c = TensorOps.MatMul(a, b);
        Assert.Equal(new Shape(2, 2), c.Shape);
        // Manual: [[1*7+2*9+3*11, 1*8+2*10+3*12], [4*7+5*9+6*11, 4*8+5*10+6*12]]
        //       = [[58, 64], [139, 154]]
        Assert.Equal(new float[] { 58, 64, 139, 154 }, c.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void MatMul_BatchedShapes_Work()
    {
        var a = Tensor.Ones(new Shape(2, 3, 4));
        var b = Tensor.Ones(new Shape(2, 4, 5));
        var c = TensorOps.MatMul(a, b);
        Assert.Equal(new Shape(2, 3, 5), c.Shape);
        // Каждый элемент = sum_{k=0..3}(1*1) = 4.
        var s = c.AsReadOnlySpan<float>();
        for (int i = 0; i < s.Length; i++) Assert.Equal(4f, s[i]);
    }

    [Fact]
    public void Operators_PlusMinusMul_CallTensorOps()
    {
        var a = Tensor.From(new float[] { 1, 2, 3 }, new Shape(3));
        var b = Tensor.From(new float[] { 10, 20, 30 }, new Shape(3));
        var sum = a + b;
        var diff = a - b;
        var prod = a * b;
        Assert.Equal(new float[] { 11, 22, 33 }, sum.AsReadOnlySpan<float>().ToArray());
        Assert.Equal(new float[] { -9, -18, -27 }, diff.AsReadOnlySpan<float>().ToArray());
        Assert.Equal(new float[] { 10, 40, 90 }, prod.AsReadOnlySpan<float>().ToArray());
    }
}
