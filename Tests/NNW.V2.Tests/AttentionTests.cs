using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using Xunit;

namespace NNW.V2.Tests;

public class AttentionTests
{
    [Fact]
    public void Softmax_SumsToOne()
    {
        var x = Tensor.Randn(new Shape(3, 4), new Random(0));
        var y = SoftmaxOps.Softmax(x, axis: -1);
        var s = y.AsReadOnlySpan<float>();
        for (int i = 0; i < 3; i++)
        {
            float sum = 0f;
            for (int j = 0; j < 4; j++) sum += s[i * 4 + j];
            Assert.InRange(sum, 0.999f, 1.001f);
        }
    }

    [Fact]
    public void Softmax_GradCheck()
    {
        var x = Tensor.Randn(new Shape(2, 3), new Random(1)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => SoftmaxOps.Softmax(t, -1).Sum(), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void LogSoftmax_GradCheck()
    {
        var x = Tensor.Randn(new Shape(2, 3), new Random(2)).SetRequiresGrad(true);
        bool ok = GradCheck.Check(x, t => SoftmaxOps.LogSoftmax(t, -1).Sum(), eps: 1e-3, atol: 1e-2);
        Assert.True(ok);
    }

    [Fact]
    public void SDPA_ShapeIsCorrect()
    {
        var Q = Tensor.Randn(new Shape(2, 4, 8), new Random(0));   // (B, Lq, d)
        var K = Tensor.Randn(new Shape(2, 6, 8), new Random(1));   // (B, Lk, d)
        var V = Tensor.Randn(new Shape(2, 6, 16), new Random(2));  // (B, Lk, dv)
        var y = ScaledDotProductAttention.Apply(Q, K, V);
        Assert.Equal(new Shape(2, 4, 16), y.Shape);
    }

    [Fact]
    public void SDPA_Causal_GivesLowerTriangularAttention()
    {
        // Если causal-маска работает, attention[i, j>i] == 0 после softmax.
        var x = Tensor.Randn(new Shape(1, 4, 8), new Random(0));
        // Q=K=V=x; собираем веса вручную, проверяя через градиент бесполезно. 
        // Тут проверим через output: значения зависят только от позиций ≤ i.
        var y1 = ScaledDotProductAttention.Apply(x, x, x, isCausal: true);
        // Изменим последнее значение во входе и посмотрим, что выход в позиции 0 не изменился.
        var x2Data = x.AsReadOnlySpan<float>().ToArray();
        x2Data[3 * 8 + 0] += 100f;
        var x2 = Tensor.From(x2Data, x.Shape);
        var y2 = ScaledDotProductAttention.Apply(x2, x2, x2, isCausal: true);
        var s1 = y1.AsReadOnlySpan<float>();
        var s2 = y2.AsReadOnlySpan<float>();
        // Позиция 0 не должна зависеть от позиции 3.
        for (int j = 0; j < 8; j++)
            Assert.Equal(s1[j], s2[j], 4);
    }

    [Fact]
    public void MultiHeadAttention_ShapeIsCorrect()
    {
        var mha = new MultiHeadAttention(embedDim: 8, numHeads: 2, dropout: 0f, rng: new Random(0));
        var x = Tensor.Randn(new Shape(2, 5, 8), new Random(1));
        var y = mha.SelfAttention(x);
        Assert.Equal(new Shape(2, 5, 8), y.Shape);
    }

    [Fact]
    public void MultiHeadAttention_KVCache_AppendsCorrectly()
    {
        var mha = new MultiHeadAttention(embedDim: 8, numHeads: 2, dropout: 0f, rng: new Random(0)).Eval() as MultiHeadAttention;
        var cache = new KVCache();
        var x1 = Tensor.Randn(new Shape(1, 3, 8), new Random(1));
        _ = mha.SelfAttention(x1, cache: cache);
        Assert.Equal(3, cache.Length);
        var x2 = Tensor.Randn(new Shape(1, 2, 8), new Random(2));
        _ = mha.SelfAttention(x2, cache: cache);
        Assert.Equal(5, cache.Length);
    }

    [Fact]
    public void TransformerEncoderLayer_ShapeIsCorrect()
    {
        var layer = new TransformerEncoderLayer(dModel: 8, nHead: 2, dimFeedforward: 16,
            dropout: 0f, rng: new Random(0)).Eval() as TransformerEncoderLayer;
        var x = Tensor.Randn(new Shape(2, 4, 8), new Random(1));
        var y = layer.Forward(x);
        Assert.Equal(new Shape(2, 4, 8), y.Shape);
    }

    [Fact]
    public void TransformerEncoder_StacksLayers()
    {
        var prototype = new TransformerEncoderLayer(8, 2, 16, dropout: 0f, rng: new Random(0));
        var enc = new TransformerEncoder(prototype, numLayers: 3, finalNorm: new LayerNorm(8));
        enc.Eval();
        var x = Tensor.Randn(new Shape(1, 4, 8), new Random(1));
        var y = enc.Forward(x);
        Assert.Equal(new Shape(1, 4, 8), y.Shape);
    }

    [Fact]
    public void TransformerDecoderLayer_ShapeIsCorrect()
    {
        var layer = new TransformerDecoderLayer(8, 2, 16, dropout: 0f, rng: new Random(0)).Eval() as TransformerDecoderLayer;
        var tgt = Tensor.Randn(new Shape(1, 3, 8), new Random(1));
        var mem = Tensor.Randn(new Shape(1, 5, 8), new Random(2));
        var y = layer.Forward(tgt, mem);
        Assert.Equal(new Shape(1, 3, 8), y.Shape);
    }

    [Fact]
    public void RoPE_PreservesShape()
    {
        var x = Tensor.Randn(new Shape(2, 4, 8), new Random(0));
        var y = RoPE.Apply(x);
        Assert.Equal(x.Shape, y.Shape);
    }

    [Fact]
    public void RoPE_PreservesNorm()
    {
        // Поворот сохраняет L2-норму каждой пары координат.
        var x = Tensor.Randn(new Shape(1, 3, 4), new Random(0));
        var y = RoPE.Apply(x);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsReadOnlySpan<float>();
        for (int p = 0; p < 3; p++)
        {
            float n1 = 0, n2 = 0;
            for (int i = 0; i < 4; i++) { n1 += xs[p * 4 + i] * xs[p * 4 + i]; n2 += ys[p * 4 + i] * ys[p * 4 + i]; }
            Assert.InRange(n2 / n1, 0.999f, 1.001f);
        }
    }

    [Fact]
    public void SinusoidalPositionalEncoding_AddsToInput()
    {
        var pe = new SinusoidalPositionalEncoding(embedDim: 8, maxLen: 16);
        var x = Tensor.Zeros(new Shape(1, 4, 8));
        var y = pe.Forward(x);
        // y == pe[:4] (т.к. x = 0)
        Assert.Equal(new Shape(1, 4, 8), y.Shape);
        var ys = y.AsReadOnlySpan<float>();
        // pe[0,1] = cos(0) = 1, pe[0,0] = sin(0) = 0
        Assert.Equal(0f, ys[0], 4);
        Assert.Equal(1f, ys[1], 4);
    }

    [Fact]
    public void AdaLN_PreservesShape()
    {
        var x = Tensor.Randn(new Shape(2, 8), new Random(0));
        var gamma = Tensor.Randn(new Shape(8), new Random(1));
        var beta = Tensor.Randn(new Shape(8), new Random(2));
        var y = AdaLN.Apply(x, gamma, beta);
        Assert.Equal(x.Shape, y.Shape);
    }
}
