using AI.ML.NeuralNetworks.V2;
using Xunit;

namespace NNW.V2.Tests;

/// <summary>
/// Базовые свойства <see cref="Tensor"/>: создание, формы, dtype, view-операции.
/// </summary>
public class TensorBasicsTests
{
    [Fact]
    public void Zeros_HasCorrectShape_DType_AndContents()
    {
        var t = Tensor.Zeros(2, 3);
        Assert.Equal(new Shape(2, 3), t.Shape);
        Assert.Equal(DType.Float32, t.DType);
        Assert.Equal(Device.Cpu, t.Device);
        Assert.True(t.IsContiguous);
        var span = t.AsSpan<float>();
        Assert.Equal(6, span.Length);
        for (int i = 0; i < span.Length; i++)
            Assert.Equal(0f, span[i]);
    }

    [Fact]
    public void From_PreservesData_AndStride()
    {
        var t = Tensor.From(new float[] { 1, 2, 3, 4 }, new Shape(2, 2));
        Assert.Equal(new Shape(2, 2), t.Shape);
        Assert.Equal(1f, t.GetFloat(0, 0));
        Assert.Equal(2f, t.GetFloat(0, 1));
        Assert.Equal(3f, t.GetFloat(1, 0));
        Assert.Equal(4f, t.GetFloat(1, 1));
    }

    [Fact]
    public void Reshape_KeepsData_ZeroCopy()
    {
        var t = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var r = t.Reshape(3, 2);
        Assert.Equal(new Shape(3, 2), r.Shape);
        Assert.Same(t.Storage, r.Storage);
        Assert.Equal(1f, r.GetFloat(0, 0));
        Assert.Equal(6f, r.GetFloat(2, 1));
    }

    [Fact]
    public void Reshape_WithMinusOne_InfersDimension()
    {
        var t = Tensor.Zeros(2, 3, 4);
        var r = t.Reshape(6, -1);
        Assert.Equal(new Shape(6, 4), r.Shape);
    }

    [Fact]
    public void Transpose_SwapsAxes_ZeroCopy_AndIsNotContiguous()
    {
        var t = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var tT = t.Transpose(0, 1);
        Assert.Equal(new Shape(3, 2), tT.Shape);
        Assert.Same(t.Storage, tT.Storage);
        Assert.False(tT.IsContiguous);
        // (0,0) in tT == (0,0) in t == 1; (0,1) in tT == (1,0) in t == 4
        Assert.Equal(1f, tT.GetFloat(0, 0));
        Assert.Equal(4f, tT.GetFloat(0, 1));
        Assert.Equal(2f, tT.GetFloat(1, 0));
    }

    [Fact]
    public void Contiguous_OnNonContiguous_CopiesAndPreservesValues()
    {
        var t = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var c = t.Transpose(0, 1).Contiguous();
        Assert.True(c.IsContiguous);
        Assert.Equal(new Shape(3, 2), c.Shape);
        var s = c.AsReadOnlySpan<float>();
        Assert.Equal(new float[] { 1, 4, 2, 5, 3, 6 }, s.ToArray());
    }

    [Fact]
    public void Squeeze_Unsqueeze_RoundTrip()
    {
        var t = Tensor.Zeros(3, 1, 4);
        var s = t.Squeeze(1);
        Assert.Equal(new Shape(3, 4), s.Shape);
        var u = s.Unsqueeze(1);
        Assert.Equal(new Shape(3, 1, 4), u.Shape);
    }

    [Fact]
    public void Permute_ReordersAxes()
    {
        var t = Tensor.Zeros(2, 3, 4);
        var p = t.Permute(2, 0, 1);
        Assert.Equal(new Shape(4, 2, 3), p.Shape);
    }

    [Fact]
    public void Expand_BroadcastsViaZeroStride()
    {
        var t = Tensor.From(new float[] { 1, 2, 3 }, new Shape(1, 3));
        var e = t.Expand(4, 3);
        Assert.Equal(new Shape(4, 3), e.Shape);
        // Все строки одинаковы.
        Assert.Equal(2f, e.GetFloat(0, 1));
        Assert.Equal(2f, e.GetFloat(3, 1));
    }
}
