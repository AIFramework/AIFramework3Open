using System.Linq;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Nn;
using Xunit;

namespace NNW.V2.Tests;

public class ModuleTests
{
    [Fact]
    public void Linear_ShapeAndForward()
    {
        var lin = new Linear(3, 2, bias: true, rng: new System.Random(0));
        var x = Tensor.From(new float[] { 1, 2, 3, 4, 5, 6 }, new Shape(2, 3));
        var y = lin.Forward(x);
        Assert.Equal(2, y.Shape[0]);
        Assert.Equal(2, y.Shape[1]);
    }

    [Fact]
    public void Linear_Backward_GradCheck()
    {
        var lin = new Linear(3, 2, bias: true, rng: new System.Random(1));
        var x = Tensor.Randn(new Shape(2, 3), new System.Random(2)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => lin.Forward(t).Sum(), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void Linear_3D_Input_Works()
    {
        var lin = new Linear(4, 5, rng: new System.Random(0));
        var x = Tensor.Randn(new Shape(2, 3, 4), new System.Random(1));
        var y = lin.Forward(x);
        Assert.Equal(new Shape(2, 3, 5), y.Shape);
    }

    [Fact]
    public void Sequential_ChainsModules()
    {
        var seq = new Sequential(
            new Linear(3, 4, rng: new System.Random(0)),
            new ReLU(),
            new Linear(4, 1, rng: new System.Random(1))
        );
        var x = Tensor.Randn(new Shape(2, 3), new System.Random(2));
        var y = seq.Forward(x);
        Assert.Equal(new Shape(2, 1), y.Shape);

        // 4 параметра: 2x weight + 2x bias.
        Assert.Equal(4, seq.Parameters().Count());
    }

    [Fact]
    public void Module_StateDict_Roundtrip()
    {
        var l1 = new Linear(2, 3, rng: new System.Random(0));
        var seq = new Sequential(l1, new Linear(3, 1, rng: new System.Random(1)));
        var state = seq.StateDict();

        var seq2 = new Sequential(
            new Linear(2, 3, rng: new System.Random(99)),
            new Linear(3, 1, rng: new System.Random(99)));
        seq2.LoadStateDict(state);
        var x = Tensor.Randn(new Shape(2, 2), new System.Random(7));
        var y1 = seq.Forward(x);
        var y2 = seq2.Forward(x);
        var s1 = y1.AsReadOnlySpan<float>();
        var s2 = y2.AsReadOnlySpan<float>();
        for (int i = 0; i < s1.Length; i++)
            Assert.Equal(s1[i], s2[i], 5);
    }

    [Fact]
    public void Dropout_TrainEval_Behaviour()
    {
        var dr = new Dropout(0.5f, new System.Random(0));
        var x = Tensor.Ones(new Shape(100));
        dr.Train();
        var yTrain = dr.Forward(x);
        Assert.True(yTrain.AsReadOnlySpan<float>().ToArray().Any(v => v == 0f));

        dr.Eval();
        var yEval = dr.Forward(x);
        var span = yEval.AsReadOnlySpan<float>();
        for (int i = 0; i < span.Length; i++) Assert.Equal(1f, span[i]);
    }

    [Fact]
    public void Embedding_Lookup_Works()
    {
        var emb = new Embedding(10, 4, rng: new System.Random(0));
        var idx = Tensor.From(new int[] { 1, 3, 5 }, new Shape(3));
        var y = emb.Forward(idx);
        Assert.Equal(new Shape(3, 4), y.Shape);
    }

    [Fact]
    public void Embedding_Backward_AccumulatesGrad()
    {
        var emb = new Embedding(5, 3, rng: new System.Random(0));
        var idx = Tensor.From(new int[] { 0, 1, 1 }, new Shape(3));
        var y = emb.Forward(idx);
        y.Sum().Backward();
        // Все 1s в grad на строки 0 и 1 (1: дважды).
        var g = emb.Weight.Tensor.Grad.AsReadOnlySpan<float>();
        Assert.Equal(1f, g[0]); Assert.Equal(1f, g[1]); Assert.Equal(1f, g[2]); // row 0
        Assert.Equal(2f, g[3]); Assert.Equal(2f, g[4]); Assert.Equal(2f, g[5]); // row 1
        Assert.Equal(0f, g[6]); // row 2
    }

    [Fact]
    public void LayerNorm_Forward_Stats()
    {
        var ln = new LayerNorm(4);
        var x = Tensor.Randn(new Shape(3, 4), new System.Random(0));
        var y = ln.Forward(x);
        var ys = y.AsReadOnlySpan<float>();
        for (int b = 0; b < 3; b++)
        {
            float m = 0f;
            for (int i = 0; i < 4; i++) m += ys[b * 4 + i];
            m /= 4;
            Assert.Equal(0, m, 4);
        }
    }

    [Fact]
    public void LayerNorm_Backward_GradCheck()
    {
        var ln = new LayerNorm(3);
        var x = Tensor.Randn(new Shape(2, 3), new System.Random(0)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => ln.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void RMSNorm_Backward_GradCheck()
    {
        var rms = new RMSNorm(4);
        var x = Tensor.Randn(new Shape(2, 4), new System.Random(0)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => rms.Forward(t).Sum(), eps: 1e-3, atol: 5e-3);
        Assert.True(ok);
    }

    [Fact]
    public void BatchNorm1d_Train_ReducesVariance()
    {
        var bn = new BatchNorm1d(4);
        bn.Train();
        var x = Tensor.Randn(new Shape(8, 4), new System.Random(0));
        var y = bn.Forward(x);
        // По каждой колонке среднее ~ 0.
        var ys = y.AsReadOnlySpan<float>();
        for (int c = 0; c < 4; c++)
        {
            float m = 0f;
            for (int n = 0; n < 8; n++) m += ys[n * 4 + c];
            m /= 8;
            Assert.Equal(0, m, 4);
        }
    }

    [Fact]
    public void BatchNorm2d_Backward_GradCheck()
    {
        var bn = new BatchNorm2d(2);
        bn.Train();
        var x = Tensor.Randn(new Shape(3, 2, 2, 2), new System.Random(0)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => bn.Forward(t).Sum(), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void GroupNorm_Backward_GradCheck()
    {
        var gn = new GroupNorm(numGroups: 2, numChannels: 4);
        var x = Tensor.Randn(new Shape(2, 4, 3, 3), new System.Random(0)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => gn.Forward(t).Sum(), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }
}
