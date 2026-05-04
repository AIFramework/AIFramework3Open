using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Nn;
using Xunit;

namespace NNW.V2.Tests;

public class ConvRnnTests
{
    [Fact]
    public void Conv2d_Forward_Shape()
    {
        var c = new Conv2d(3, 4, kernelSize: 3, stride: 1, padding: 1, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(2, 3, 5, 5), new System.Random(1));
        var y = c.Forward(x);
        Assert.Equal(new Shape(2, 4, 5, 5), y.Shape);
    }

    [Fact]
    public void Conv2d_Backward_GradCheck_Small()
    {
        var c = new Conv2d(2, 2, kernelSize: 2, stride: 1, padding: 0, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 2, 3, 3), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => c.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void Conv2d_Stride_Padding_Dilation_GradCheck()
    {
        var c = new Conv2d(2, 3, kernelSize: 3, stride: 2, padding: 1, dilation: 1, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 2, 5, 5), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => c.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void MaxPool2d_GradCheck()
    {
        var p = new MaxPool2d(2);
        var x = Tensor.Randn(new Shape(1, 2, 4, 4), new System.Random(0)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => p.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void AvgPool2d_GradCheck()
    {
        var p = new AvgPool2d(2);
        var x = Tensor.Randn(new Shape(1, 2, 4, 4), new System.Random(0)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => p.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void AdaptiveAvgPool_OutputShape()
    {
        var p = new AdaptiveAvgPool2d((1, 1));
        var x = Tensor.Randn(new Shape(2, 3, 7, 5), new System.Random(0));
        var y = p.Forward(x);
        Assert.Equal(new Shape(2, 3, 1, 1), y.Shape);
    }

    [Fact]
    public void ConvTranspose2d_GradCheck()
    {
        var c = new ConvTranspose2d(2, 2, kernelSize: 3, stride: 2, padding: 0, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 2, 3, 3), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => c.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void Conv1d_Forward_Shape()
    {
        var c = new Conv1d(3, 4, kernelSize: 3, padding: 1, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(2, 3, 7), new System.Random(1));
        var y = c.Forward(x);
        Assert.Equal(new Shape(2, 4, 7), y.Shape);
    }

    [Fact]
    public void RNN_Forward_Sequence_Shape()
    {
        var rnn = new RNN(4, 5, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(2, 3, 4), new System.Random(1));
        var (y, h) = rnn.ForwardSeq(x);
        Assert.Equal(new Shape(2, 3, 5), y.Shape);
        Assert.Equal(new Shape(2, 5), h.Shape);
    }

    [Fact]
    public void LSTM_Forward_Sequence_Shape()
    {
        var lstm = new LSTM(4, 5, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(2, 3, 4), new System.Random(1));
        var (y, h, c) = lstm.ForwardSeq(x);
        Assert.Equal(new Shape(2, 3, 5), y.Shape);
        Assert.Equal(new Shape(2, 5), h.Shape);
        Assert.Equal(new Shape(2, 5), c.Shape);
    }

    [Fact]
    public void GRU_Forward_Sequence_Shape()
    {
        var gru = new GRU(4, 5, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(2, 3, 4), new System.Random(1));
        var (y, h) = gru.ForwardSeq(x);
        Assert.Equal(new Shape(2, 3, 5), y.Shape);
    }

    [Fact]
    public void LSTM_Backward_PropagatesGradients()
    {
        var lstm = new LSTM(2, 3, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 2, 2), new System.Random(1)).SetRequiresGrad(true);
        var (y, _, _) = lstm.ForwardSeq(x);
        y.Sum().Backward();
        Assert.NotNull(x.Grad);
        // Хотя бы один параметр должен получить grad.
        bool any = false;
        foreach (var p in lstm.Parameters()) if (p.Tensor.Grad != null) { any = true; break; }
        Assert.True(any);
    }

    [Fact]
    public void RNN_GradCheck_AgainstNumeric()
    {
        var rnn = new RNN(2, 3, "tanh", true, true, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 3, 2), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => rnn.ForwardSeq(t).outputs.Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void LSTM_GradCheck_AgainstNumeric_Fused()
    {
        var lstm = new LSTM(2, 3, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 3, 2), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => lstm.ForwardSeq(t).outputs.Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void LSTM_GradCheck_BothOutputs_HAndC()
    {
        // Проверяем, что gradient по последнему h_T И c_T тоже проходит — оба идут
        // через packed (2,B,H) и Select; критично для корректности fused-Function.
        var lstm = new LSTM(2, 3, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 3, 2), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t =>
        {
            var (_, hT, cT) = lstm.ForwardSeq(t);
            return hT.Sum() + cT.Sum();
        }, eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void GRU_GradCheck_AgainstNumeric_Fused()
    {
        var gru = new GRU(2, 3, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(1, 3, 2), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => gru.ForwardSeq(t).outputs.Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void LSTM_TimeFirst_Layout_MatchesShape()
    {
        var lstm = new LSTM(4, 5, batchFirst: false, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(3, 2, 4), new System.Random(1));   // (T, B, I)
        var (y, h, c) = lstm.ForwardSeq(x);
        Assert.Equal(new Shape(3, 2, 5), y.Shape);                         // (T, B, H)
        Assert.Equal(new Shape(2, 5), h.Shape);
        Assert.Equal(new Shape(2, 5), c.Shape);
    }

    [Fact]
    public void InstanceNorm_Forward_Shape()
    {
        var inorm = new InstanceNorm(4, affine: true);
        var x = Tensor.Randn(new Shape(2, 4, 3, 3), new System.Random(0));
        var y = inorm.Forward(x);
        Assert.Equal(x.Shape, y.Shape);
    }

    [Fact]
    public void Cat_GradCheck()
    {
        var a = Tensor.Randn(new Shape(2, 3), new System.Random(0)).SetRequiresGrad(true);
        var b = Tensor.Randn(new Shape(2, 4), new System.Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(a,
            t => AI.ML.NeuralNetworks.V2.Ops.IndexingOps.Cat(new[] { t, b }, axis: 1).Sum(),
            eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }
}
