using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;
using Xunit;

namespace NNW.V2.Tests;

/// <summary>
/// Тесты autograd: backward для базовых операций + численная проверка
/// через <see cref="GradCheck"/>.
/// </summary>
public class AutogradTests
{
    [Fact]
    public void Backward_Add_FlowsGradEqually()
    {
        var a = Tensor.From(new float[] { 1, 2, 3 }, new Shape(3)).SetRequiresGrad();
        var b = Tensor.From(new float[] { 4, 5, 6 }, new Shape(3)).SetRequiresGrad();
        var y = (a + b).Sum();
        y.Backward();
        Assert.Equal(new float[] { 1, 1, 1 }, a.Grad.AsReadOnlySpan<float>().ToArray());
        Assert.Equal(new float[] { 1, 1, 1 }, b.Grad.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Backward_Mul_GradByA_EqualsB()
    {
        var a = Tensor.From(new float[] { 1, 2, 3 }, new Shape(3)).SetRequiresGrad();
        var b = Tensor.From(new float[] { 4, 5, 6 }, new Shape(3)).SetRequiresGrad();
        var y = (a * b).Sum();
        y.Backward();
        // dy/da[i] = b[i]
        Assert.Equal(new float[] { 4, 5, 6 }, a.Grad.AsReadOnlySpan<float>().ToArray());
        Assert.Equal(new float[] { 1, 2, 3 }, b.Grad.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Backward_Relu_GradZeroForNegative()
    {
        var x = Tensor.From(new float[] { -1, 0.5f, -2, 3 }, new Shape(4)).SetRequiresGrad();
        var y = x.Relu().Sum();
        y.Backward();
        Assert.Equal(new float[] { 0, 1, 0, 1 }, x.Grad.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Backward_BroadcastReduces()
    {
        // a: (2,3) + b: (3) -> sum -> scalar; backward для b должен суммировать по строкам.
        var a = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3)).SetRequiresGrad();
        var b = Tensor.From(new float[] { 10, 20, 30 }, new Shape(3)).SetRequiresGrad();
        var y = (a + b).Sum();
        y.Backward();
        Assert.Equal(new float[] { 1, 1, 1, 1, 1, 1 }, a.Grad.AsReadOnlySpan<float>().ToArray());
        // b повторяется по строкам — grad суммируется: (1+1) на каждом элементе.
        Assert.Equal(new float[] { 2, 2, 2 }, b.Grad.AsReadOnlySpan<float>().ToArray());
    }

    [Fact]
    public void Backward_MatMul_GradShapes_Correct()
    {
        var a = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3)).SetRequiresGrad();
        var b = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(3, 2)).SetRequiresGrad();
        var y = a.MatMul(b).Sum();
        y.Backward();
        Assert.Equal(new Shape(2, 3), a.Grad.Shape);
        Assert.Equal(new Shape(3, 2), b.Grad.Shape);
    }

    [Fact]
    public void NoGrad_Disables_Recording()
    {
        var x = Tensor.From(new float[] { 1, 2 }, new Shape(2)).SetRequiresGrad();
        Tensor y;
        using (TapeContext.NoGrad())
        {
            y = x.Relu();
        }
        Assert.Null(y.GradFn);
    }

    #region GradCheck

    [Fact]
    public void GradCheck_Mul_Passes()
    {
        var a = Tensor.Randn(new Shape(2, 3), new System.Random(42)).SetRequiresGrad();
        Assert.True(GradCheck.Check(a, t => (t * t).Sum()));
    }

    [Fact]
    public void GradCheck_Sigmoid_Passes()
    {
        var x = Tensor.Randn(new Shape(5), new System.Random(7)).SetRequiresGrad();
        Assert.True(GradCheck.Check(x, t => t.Sigmoid().Sum()));
    }

    [Fact]
    public void GradCheck_Tanh_Passes()
    {
        var x = Tensor.Randn(new Shape(5), new System.Random(13)).SetRequiresGrad();
        Assert.True(GradCheck.Check(x, t => t.Tanh().Sum()));
    }

    [Fact]
    public void GradCheck_Gelu_Passes()
    {
        var x = Tensor.Randn(new Shape(4), new System.Random(21)).SetRequiresGrad();
        Assert.True(GradCheck.Check(x, t => t.Gelu().Sum(), eps: 1e-3, rtol: 5e-2, atol: 5e-3));
    }

    [Fact]
    public void GradCheck_MatMul_Passes()
    {
        var a = Tensor.Randn(new Shape(3, 4), new System.Random(31)).SetRequiresGrad();
        var b = Tensor.Randn(new Shape(4, 2), new System.Random(32));
        Assert.True(GradCheck.Check(a, t => t.MatMul(b).Sum()));
    }
    #endregion GradCheck

}