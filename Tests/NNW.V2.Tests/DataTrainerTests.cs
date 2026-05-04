using System;
using System.Collections.Generic;
using System.Linq;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Data;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using AI.ML.NeuralNetworks.V2.Train;
using Xunit;

namespace NNW.V2.Tests;

public class DataTrainerTests
{
    [Fact]
    public void TensorDataset_IndexAndLength()
    {
        var x = Tensor.From(Enumerable.Range(0, 12).Select(i => (float)i).ToArray(), new Shape(4, 3));
        var y = Tensor.From(new float[] { 0, 1, 2, 3 }, new Shape(4));
        var ds = new TensorDataset(x, y);
        Assert.Equal(4, ds.Count);
        var (x0, y0) = ds.Get(0);
        Assert.Equal(new Shape(3), x0.Shape);
        Assert.Equal(0f, y0.AsReadOnlySpan<float>()[0]);
    }

    [Fact]
    public void RandomSampler_PermutesAllIndices()
    {
        var s = new RandomSampler(10, seed: 42);
        var indices = s.Iterate().ToArray();
        Assert.Equal(10, indices.Length);
        Assert.Equal(Enumerable.Range(0, 10), indices.OrderBy(i => i));
    }

    [Fact]
    public void BatchSampler_GroupsCorrectly()
    {
        var s = new BatchSampler(new SequentialSampler(7), batchSize: 3, dropLast: false);
        var batches = s.IterateBatches().ToArray();
        Assert.Equal(3, batches.Length);
        Assert.Equal(3, batches[0].Length);
        Assert.Equal(1, batches[2].Length);  // last partial
    }

    [Fact]
    public void DataLoader_Sync_YieldsAllBatches()
    {
        var x = Tensor.From(Enumerable.Range(0, 20).Select(i => (float)i).ToArray(), new Shape(10, 2));
        var y = Tensor.From(Enumerable.Range(0, 10).Select(i => (float)i).ToArray(), new Shape(10));
        var ds = new TensorDataset(x, y);
        var dl = new DataLoader<(Tensor, Tensor), (Tensor, Tensor)>(
            ds, batchSize: 3, collateFn: Collate.StackPair, shuffle: false, dropLast: false, numWorkers: 0);
        int seen = 0;
        foreach (var (xb, yb) in dl) seen += xb.Shape[0];
        Assert.Equal(10, seen);
    }

    [Fact]
    public void DataLoader_MultiWorker_PreservesOrder()
    {
        var x = Tensor.From(Enumerable.Range(0, 50).Select(i => (float)i).ToArray(), new Shape(50));
        var y = Tensor.From(Enumerable.Range(0, 50).Select(i => (float)i).ToArray(), new Shape(50));
        var ds = new TensorDataset(x, y);
        var dl = new DataLoader<(Tensor, Tensor), (Tensor, Tensor)>(
            ds, batchSize: 5, collateFn: Collate.StackPair, shuffle: false, dropLast: false, numWorkers: 4);
        var seenY = new List<float>();
        foreach (var (xb, yb) in dl)
            foreach (var v in yb.AsReadOnlySpan<float>().ToArray()) seenY.Add(v);
        Assert.Equal(Enumerable.Range(0, 50).Select(i => (float)i), seenY);
    }

    [Fact]
    public void Trainer_FitsLinearRegression()
    {
        // Учим y = 2x на [0..N).
        int N = 32;
        var x = Tensor.From(Enumerable.Range(0, N).Select(i => (float)i).ToArray(), new Shape(N, 1));
        var y = Tensor.From(Enumerable.Range(0, N).Select(i => 2f * i).ToArray(), new Shape(N, 1));
        var ds = new TensorDataset(x, y);
        var dl = new DataLoader<(Tensor, Tensor), (Tensor, Tensor)>(
            ds, batchSize: 8, collateFn: Collate.StackPair, shuffle: false, numWorkers: 0);

        var model = new Linear(1, 1, bias: false, rng: new Random(0));
        var opt = new SGD(model.Parameters(), lr: 1e-3f);
        var step = new LambdaTrainStep<(Tensor, Tensor)>((m, batch) =>
        {
            var (xb, yb) = batch;
            var pred = m.Forward(xb);
            var loss = RegressionLosses.MSE(pred, yb);
            loss.Backward();
            return loss.AsReadOnlySpan<float>()[0];
        });
        var trainer = new Trainer<(Tensor, Tensor)>(model, opt, step);
        for (int e = 0; e < 50; e++) trainer.TrainEpoch(dl);

        // Вес должен быть ≈ 2.
        float w = ((Linear)model).Weight.Tensor.AsReadOnlySpan<float>()[0];
        Assert.InRange(w, 1.9f, 2.1f);
    }

    [Fact]
    public void Trainer_GradAccumSteps_AccumulatesGradients()
    {
        int N = 8;
        var x = Tensor.From(new float[N], new Shape(N, 1));
        var y = Tensor.From(new float[N], new Shape(N, 1));
        var ds = new TensorDataset(x, y);
        var dl = new DataLoader<(Tensor, Tensor), (Tensor, Tensor)>(
            ds, batchSize: 1, collateFn: Collate.StackPair, numWorkers: 0);
        var model = new Linear(1, 1, rng: new Random(0));
        var opt = new SGD(model.Parameters(), lr: 0.001f);
        int callCount = 0;
        var step = new LambdaTrainStep<(Tensor, Tensor)>((m, batch) =>
        {
            callCount++;
            var (xb, yb) = batch;
            var pred = m.Forward(xb);
            var loss = RegressionLosses.MSE(pred, yb);
            loss.Backward();
            return loss.AsReadOnlySpan<float>()[0];
        });
        var trainer = new Trainer<(Tensor, Tensor)>(model, opt, step) { GradAccumSteps = 4 };
        var res = trainer.TrainEpoch(dl);
        Assert.Equal(8, callCount);
        Assert.Equal(2, res.OptimizerSteps);  // 8/4 = 2 шага
    }

    [Fact]
    public void GradUtils_ClipNorm_LimitsTotal()
    {
        // Реальный сценарий: leaf-параметр + backward.
        var p = new Parameter(Tensor.From(new float[] { 1f, 2f, 3f }, new Shape(3)));
        var a = Tensor.From(new float[] { 3f, 4f, 0f }, new Shape(3));
        var y = (p.Tensor * a).Sum();
        y.Backward();
        // ‖g‖ = ‖a‖ = 5.
        float origNorm = GradUtils.ClipGradNorm(new[] { p }, maxNorm: 1f);
        Assert.InRange(origNorm, 4.99f, 5.01f);
        var s = p.Tensor.Grad.AsReadOnlySpan<float>();
        float n2 = MathF.Sqrt(s[0] * s[0] + s[1] * s[1] + s[2] * s[2]);
        Assert.InRange(n2, 0.99f, 1.01f);
    }

    [Fact]
    public void EMA_SmoothesParameters()
    {
        var p = new Parameter(Tensor.From(new float[] { 1f }, new Shape(1)));
        var ema = new ParameterEMA(new[] { p }, decay: 0.5f);
        // Меняем параметр и обновляем EMA.
        p.Tensor.AsSpan<float>()[0] = 3f;
        ema.Update();
        // EMA = 0.5*1 + 0.5*3 = 2.
        Assert.Equal(2f, ema.Get(p).AsReadOnlySpan<float>()[0], 4);
    }
}
