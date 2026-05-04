using System;
using System.Linq;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using Xunit;

namespace NNW.V2.Tests;

public class OptimTests
{
    /// <summary>
    /// Проверка: SGD на простой задаче (минимум x^2 от точки x=5) сходится к 0.
    /// </summary>
    [Fact]
    public void SGD_Converges_OnQuadratic()
    {
        // x — leaf-параметр, инициализирован 5.
        var p = new Parameter(Tensor.From(new float[] { 5f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 0.1f);
        for (int i = 0; i < 100; i++)
        {
            opt.ZeroGrad();
            // y = x^2; dy/dx = 2x.
            var y = p.Tensor * p.Tensor;
            y.Backward();
            opt.Step();
        }
        Assert.True(MathF.Abs(p.Tensor.AsReadOnlySpan<float>()[0]) < 1e-3f);
    }

    [Fact]
    public void SGD_Momentum_Converges()
    {
        var p = new Parameter(Tensor.From(new float[] { 5f }, new Shape(1)));
        // momentum + меньший lr, чтобы избежать осцилляций.
        var opt = new SGD(new[] { p }, lr: 0.01f, momentum: 0.9f);
        for (int i = 0; i < 500; i++)
        {
            opt.ZeroGrad();
            var y = p.Tensor * p.Tensor;
            y.Backward();
            opt.Step();
        }
        Assert.True(MathF.Abs(p.Tensor.AsReadOnlySpan<float>()[0]) < 1e-1f);
    }

    [Fact]
    public void Adam_Converges()
    {
        var p = new Parameter(Tensor.From(new float[] { 5f }, new Shape(1)));
        var opt = new Adam(new[] { p }, lr: 0.5f);
        for (int i = 0; i < 100; i++)
        {
            opt.ZeroGrad();
            var y = p.Tensor * p.Tensor;
            y.Backward();
            opt.Step();
        }
        Assert.True(MathF.Abs(p.Tensor.AsReadOnlySpan<float>()[0]) < 1e-1f);
    }

    [Fact]
    public void AdamW_AppliesDecoupledDecay()
    {
        // Нет градиентов от потерь — только weight decay.
        // У AdamW decoupled WD напрямую уменьшает θ; у Adam g=0 и L2 не сработает (m=v=0).
        var pAdamW = new Parameter(Tensor.From(new float[] { 1f }, new Shape(1)));
        var optW = Adam.AdamW(new[] { pAdamW }, lr: 0.1f, weightDecay: 0.1f);
        for (int i = 0; i < 5; i++)
        {
            optW.ZeroGrad();
            // Создаём dummy grad = 0 через скалярное умножение.
            var z = pAdamW.Tensor * Tensor.Full(new Shape(), 0f, DType.Float32, Device.Cpu);
            z.Backward();
            optW.Step();
        }
        // 5 шагов: θ_t+1 = θ_t * (1 - lr*wd) = θ * 0.99 ⇒ θ_5 ≈ 0.951
        float v = pAdamW.Tensor.AsReadOnlySpan<float>()[0];
        Assert.InRange(v, 0.94f, 0.96f);
    }

    [Fact]
    public void RMSProp_Converges()
    {
        var p = new Parameter(Tensor.From(new float[] { 3f }, new Shape(1)));
        var opt = new RMSProp(new[] { p }, lr: 0.1f);
        for (int i = 0; i < 200; i++)
        {
            opt.ZeroGrad();
            var y = p.Tensor * p.Tensor;
            y.Backward();
            opt.Step();
        }
        Assert.True(MathF.Abs(p.Tensor.AsReadOnlySpan<float>()[0]) < 0.5f);
    }

    [Fact]
    public void Adagrad_Converges()
    {
        var p = new Parameter(Tensor.From(new float[] { 3f }, new Shape(1)));
        var opt = new Adagrad(new[] { p }, lr: 1f);
        for (int i = 0; i < 100; i++)
        {
            opt.ZeroGrad();
            var y = p.Tensor * p.Tensor;
            y.Backward();
            opt.Step();
        }
        Assert.True(MathF.Abs(p.Tensor.AsReadOnlySpan<float>()[0]) < 1f);
    }

    #region Schedulers

    [Fact]
    public void StepLR_DecaysAtSteps()
    {
        var p = new Parameter(Tensor.From(new float[] { 0f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 1f);
        var sch = new StepLR(opt, stepSize: 2, gamma: 0.1f);
        sch.Step(); Assert.Equal(1f, opt.LearningRate, 4);  // epoch 0
        sch.Step(); Assert.Equal(1f, opt.LearningRate, 4);  // epoch 1
        sch.Step(); Assert.Equal(0.1f, opt.LearningRate, 4); // epoch 2
        sch.Step(); Assert.Equal(0.1f, opt.LearningRate, 4); // epoch 3
        sch.Step(); Assert.Equal(0.01f, opt.LearningRate, 4); // epoch 4
    }

    [Fact]
    public void MultiStepLR_DecaysAtMilestones()
    {
        var p = new Parameter(Tensor.From(new float[] { 0f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 1f);
        var sch = new MultiStepLR(opt, new[] { 3, 5 }, gamma: 0.5f);
        for (int i = 0; i < 7; i++) sch.Step();
        // After steps 0..6 (LastEpoch=6), milestones 3 and 5 reached -> lr = 1 * 0.25
        Assert.Equal(0.25f, opt.LearningRate, 4);
    }

    [Fact]
    public void CosineAnnealingLR_ReachesEtaMin()
    {
        var p = new Parameter(Tensor.From(new float[] { 0f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 1f);
        var sch = new CosineAnnealingLR(opt, tMax: 10, etaMin: 0f);
        for (int i = 0; i < 11; i++) sch.Step();  // 11 шагов: LastEpoch = 10
        Assert.InRange(opt.LearningRate, -1e-5f, 1e-5f);
    }

    [Fact]
    public void OneCycleLR_PeaksAtPctStart()
    {
        var p = new Parameter(Tensor.From(new float[] { 0f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 0.01f);
        var sch = new OneCycleLR(opt, maxLR: 1f, totalSteps: 10, pctStart: 0.3f);
        // peakStep = 3; пик достигается, когда LastEpoch == 3, т.е. на 4-м Step().
        for (int i = 0; i < 4; i++) sch.Step();
        Assert.InRange(opt.LearningRate, 0.99f, 1.01f);
    }

    [Fact]
    public void ReduceLROnPlateau_ReducesAfterNoImprovement()
    {
        var p = new Parameter(Tensor.From(new float[] { 0f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 1f);
        var sch = new ReduceLROnPlateau(opt, factor: 0.5f, patience: 2, threshold: 1e-4f);
        // Семантика: bad >= patience -> reduce. С patience=2 уменьшение случается
        // на втором подряд «плохом» шаге.
        Assert.False(sch.Step(metric: 1f));  // best=1, bad=0
        Assert.False(sch.Step(metric: 1f));  // bad=1 < patience
        Assert.True(sch.Step(metric: 1f));   // bad=2 >= patience -> reduce
        Assert.Equal(0.5f, opt.LearningRate, 4);
    }

    [Fact]
    public void LinearWarmupLR_ReachesPeak()
    {
        var p = new Parameter(Tensor.From(new float[] { 0f }, new Shape(1)));
        var opt = new SGD(new[] { p }, lr: 0.001f);
        var sch = new LinearWarmupLR(opt, warmupSteps: 5, peakLR: 1f);
        for (int i = 0; i < 5; i++) sch.Step();
        // LastEpoch=4: pct = 5/5 = 1.0 -> peakLR
        Assert.InRange(opt.LearningRate, 0.99f, 1.01f);
        sch.Step();  // LastEpoch=5: hold
        Assert.Equal(1f, opt.LearningRate, 4);
    }
    #endregion Schedulers

}