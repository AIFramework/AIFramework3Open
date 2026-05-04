using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Ops;
using Xunit;

namespace NNW.V2.Tests;

public class LossesTests
{
    [Fact]
    public void MSE_BasicValue()
    {
        var x = Tensor.From(new float[] { 1, 2, 3, 4 }, new Shape(4));
        var t = Tensor.From(new float[] { 0, 0, 0, 0 }, new Shape(4));
        var loss = RegressionLosses.MSE(x, t);
        // mean(1+4+9+16)/4 = 7.5
        Assert.Equal(7.5f, loss.AsReadOnlySpan<float>()[0], 4);
    }

    [Fact]
    public void MSE_GradCheck()
    {
        var x = Tensor.Randn(new Shape(2, 3), new Random(0)).SetRequiresGrad(true);
        var t = Tensor.Randn(new Shape(2, 3), new Random(1));
        bool ok = GradCheck.Check(x, xx => RegressionLosses.MSE(xx, t), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void L1_GradCheck()
    {
        var x = Tensor.Randn(new Shape(3), new Random(0)).SetRequiresGrad(true);
        var t = Tensor.Randn(new Shape(3), new Random(1));
        bool ok = GradCheck.Check(x, xx => RegressionLosses.L1(xx, t), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void SmoothL1_GradCheck()
    {
        var x = Tensor.Randn(new Shape(4), new Random(0)).SetRequiresGrad(true);
        var t = Tensor.Randn(new Shape(4), new Random(1));
        bool ok = GradCheck.Check(x, xx => RegressionLosses.SmoothL1(xx, t, 1f), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void CrossEntropy_BasicValue()
    {
        var logits = Tensor.From(new float[] { 1f, 2f, 3f }, new Shape(1, 3));
        var targets = Tensor.From(new int[] { 2 }, new Shape(1));
        var loss = ClassificationLosses.CrossEntropy(logits, targets);
        // log_softmax: 3 - log(e^1+e^2+e^3) = 3 - log(30.19...)
        // = 3 - 3.4076 = -0.4076; loss = 0.4076
        Assert.Equal(0.4076f, loss.AsReadOnlySpan<float>()[0], 3);
    }

    [Fact]
    public void CrossEntropy_GradCheck()
    {
        var logits = Tensor.Randn(new Shape(2, 3), new Random(0)).SetRequiresGrad(true);
        var targets = Tensor.From(new int[] { 0, 2 }, new Shape(2));
        bool ok = GradCheck.Check(logits, l =>
            ClassificationLosses.CrossEntropy(l, targets), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void CrossEntropy_LabelSmoothing_Lower()
    {
        var logits = Tensor.From(new float[] { 10f, 0f, 0f }, new Shape(1, 3));
        var targets = Tensor.From(new int[] { 0 }, new Shape(1));
        var l1 = ClassificationLosses.CrossEntropy(logits, targets, labelSmoothing: 0f);
        var l2 = ClassificationLosses.CrossEntropy(logits, targets, labelSmoothing: 0.1f);
        // labelSmoothing > 0 -> больший loss даже на правильном предикте
        Assert.True(l2.AsReadOnlySpan<float>()[0] > l1.AsReadOnlySpan<float>()[0]);
    }

    [Fact]
    public void CrossEntropy_IgnoreIndex_Skipped()
    {
        var logits = Tensor.Randn(new Shape(3, 4), new Random(0));
        var targets = Tensor.From(new int[] { 0, -1, 2 }, new Shape(3));
        var loss = ClassificationLosses.CrossEntropy(logits, targets, ignoreIndex: -1);
        // Только N=2 valid элементов -> mean делится на 2.
        Assert.True(float.IsFinite(loss.AsReadOnlySpan<float>()[0]));
    }

    [Fact]
    public void BCEWithLogits_BasicValue()
    {
        // x=0, y=1: loss = log(2) ≈ 0.693
        var x = Tensor.From(new float[] { 0f }, new Shape(1));
        var y = Tensor.From(new float[] { 1f }, new Shape(1));
        var loss = ClassificationLosses.BCEWithLogits(x, y);
        Assert.Equal(MathF.Log(2f), loss.AsReadOnlySpan<float>()[0], 4);
    }

    [Fact]
    public void BCEWithLogits_GradCheck()
    {
        var x = Tensor.Randn(new Shape(4), new Random(0)).SetRequiresGrad(true);
        var y = Tensor.From(new float[] { 1f, 0f, 1f, 0f }, new Shape(4));
        bool ok = GradCheck.Check(x, xx => ClassificationLosses.BCEWithLogits(xx, y), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void NLL_GradCheck()
    {
        var lp = SoftmaxOps.LogSoftmax(Tensor.Randn(new Shape(2, 3), new Random(0))).Contiguous().SetRequiresGrad(true);
        // Note: setting requires_grad on intermediate is non-standard, but here works as leaf
        var targets = Tensor.From(new int[] { 0, 1 }, new Shape(2));
        bool ok = GradCheck.Check(lp, l => ClassificationLosses.NLL(l, targets), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void TripletMargin_BasicValue()
    {
        // anchor=positive: dPos=0, anchor far from negative: dNeg большое.
        // Loss должен быть ≈ 0.
        var a = Tensor.From(new float[] { 1, 0 }, new Shape(1, 2));
        var p = Tensor.From(new float[] { 1, 0 }, new Shape(1, 2));
        var n = Tensor.From(new float[] { -1, 0 }, new Shape(1, 2));
        var loss = EmbeddingLosses.TripletMargin(a, p, n, margin: 0.1f);
        Assert.Equal(0f, loss.AsReadOnlySpan<float>()[0], 3);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalIsZero()
    {
        var a = Tensor.From(new float[] { 1, 0 }, new Shape(1, 2));
        var b = Tensor.From(new float[] { 0, 1 }, new Shape(1, 2));
        var sim = EmbeddingLosses.CosineSimilarity(a, b);
        Assert.Equal(0f, sim.AsReadOnlySpan<float>()[0], 4);
    }

    [Fact]
    public void MarginRanking_BasicValue()
    {
        // x1 > x2, y=1: loss = 0
        var x1 = Tensor.From(new float[] { 2 }, new Shape(1));
        var x2 = Tensor.From(new float[] { 1 }, new Shape(1));
        var y = Tensor.From(new float[] { 1 }, new Shape(1));
        var loss = EmbeddingLosses.MarginRanking(x1, x2, y);
        Assert.Equal(0f, loss.AsReadOnlySpan<float>()[0], 4);
    }

    [Fact]
    public void KLDiv_NonNegative()
    {
        var input = SoftmaxOps.LogSoftmax(Tensor.Randn(new Shape(2, 4), new Random(0)));
        var target = SoftmaxOps.Softmax(Tensor.Randn(new Shape(2, 4), new Random(1)));
        var kl = ClassificationLosses.KLDiv(input, target, Reduction.Sum);
        Assert.True(kl.AsReadOnlySpan<float>()[0] >= -1e-3f);
    }
}
