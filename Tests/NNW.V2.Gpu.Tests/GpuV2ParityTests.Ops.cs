using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.Gpu.V2;
using Xunit;

namespace NNW.V2.Gpu.Tests;

public partial class GpuV2ParityTests
{
    #region New ops parity (Sum / Softmax / LogSoftmax / LayerNorm / Gelu bwd / BMM bwd / broadcast-Add bwd / MulScalar)

    [SkippableFact]
    public void Sum_All_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(8, 16), new Random(10));
        var cpu = TensorOps.Sum(x);
        var gpu = TensorOps.Sum(x.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu, 1e-3f);
    }

    [SkippableFact]
    public void Sum_Axis_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(4, 8, 16), new Random(11));
        var devX = x.To(Device.Cuda(0));
        for (int axis = 0; axis < 3; axis++)
        {
            AssertClose(TensorOps.Sum(x, axis),
                TensorOps.Sum(devX, axis).ToCpu(), 1e-3f);
            AssertClose(TensorOps.Sum(x, axis, keepDim: true),
                TensorOps.Sum(devX, axis, keepDim: true).ToCpu(), 1e-3f);
        }
    }

    [SkippableFact]
    public void Mean_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(4, 16), new Random(12));
        AssertClose(TensorOps.Mean(x), TensorOps.Mean(x.To(Device.Cuda(0))).ToCpu(), 1e-4f);
        AssertClose(TensorOps.Mean(x, 1), TensorOps.Mean(x.To(Device.Cuda(0)), 1).ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void Softmax_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(4, 8, 16), new Random(13));
        var devX = x.To(Device.Cuda(0));
        AssertClose(SoftmaxOps.Softmax(x, axis: -1),
            SoftmaxOps.Softmax(devX, axis: -1).ToCpu(), 1e-4f);
        AssertClose(SoftmaxOps.Softmax(x, axis: 1),
            SoftmaxOps.Softmax(devX, axis: 1).ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void LogSoftmax_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(4, 8, 16), new Random(14));
        var devX = x.To(Device.Cuda(0));
        AssertClose(SoftmaxOps.LogSoftmax(x, axis: -1),
            SoftmaxOps.LogSoftmax(devX, axis: -1).ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void Softmax_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(15);
        var x = Tensor.Randn(new Shape(4, 8), rng);

        // CPU
        var xc = x.SetRequiresGrad(true);
        var yc = SoftmaxOps.Softmax(xc, axis: -1);
        var lc = TensorOps.Sum(yc * yc); // произвольная скалярная функция
        lc.Backward();

        // GPU
        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = SoftmaxOps.Softmax(xg, axis: -1);
        var lg = TensorOps.Sum(yg * yg);
        lg.Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void LayerNorm_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(4, 16), new Random(16));
        var w = Tensor.Randn(new Shape(16), new Random(17));
        var b = Tensor.Randn(new Shape(16), new Random(18));
        var cpuY = LayerNorm.Apply(x, normSize: 16, eps: 1e-5f, w, b);
        var gpuY = LayerNorm.Apply(x.To(Device.Cuda(0)), normSize: 16, eps: 1e-5f,
            w.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpuY, gpuY, 1e-3f);
    }

    [SkippableFact]
    public void LayerNorm_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(19);
        var x = Tensor.Randn(new Shape(4, 16), rng);
        var w = Tensor.Randn(new Shape(16), rng);
        var b = Tensor.Randn(new Shape(16), rng);

        var xc = x.SetRequiresGrad(true);
        var wc = w.SetRequiresGrad(true);
        var bc = b.SetRequiresGrad(true);
        var yc = LayerNorm.Apply(xc, 16, 1e-5f, wc, bc);
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var bg = b.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = LayerNorm.Apply(xg, 16, 1e-5f, wg, bg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-3f);
        AssertClose(wc.Grad, wg.Grad.ToCpu(), 1e-2f);
        AssertClose(bc.Grad, bg.Grad.ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void BatchedMatMul_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(20);
        var a = Tensor.Randn(new Shape(2, 4, 6), rng);
        var b = Tensor.Randn(new Shape(2, 6, 5), rng);

        var ac = a.SetRequiresGrad(true);
        var bc = b.SetRequiresGrad(true);
        var yc = TensorOps.MatMul(ac, bc);
        TensorOps.Sum(yc).Backward();

        var ag = a.To(Device.Cuda(0)).SetRequiresGrad(true);
        var bg = b.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = TensorOps.MatMul(ag, bg);
        TensorOps.Sum(yg).Backward();

        AssertClose(ac.Grad, ag.Grad.ToCpu(), 1e-3f);
        AssertClose(bc.Grad, bg.Grad.ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void Gelu_Backward_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(64), new Random(21));
        var xc = x.SetRequiresGrad(true);
        var yc = TensorOps.Gelu(xc);
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = TensorOps.Gelu(xg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void BroadcastAdd_Backward_Parity()
    {
        SkipIfNoCuda();
        // Bias-add: x:[B, F] + b:[F]
        var rng = new Random(22);
        var x = Tensor.Randn(new Shape(8, 12), rng);
        var b = Tensor.Randn(new Shape(12), rng);

        var xc = x.SetRequiresGrad(true);
        var bc = b.SetRequiresGrad(true);
        var yc = xc + bc;
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var bg = b.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = xg + bg;
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-3f);
        AssertClose(bc.Grad, bg.Grad.ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void MulScalar_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(64), new Random(23));
        var xc = x.SetRequiresGrad(true);
        var yc = TensorOps.MulScalar(xc, 0.7071f);
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = TensorOps.MulScalar(xg, 0.7071f);
        TensorOps.Sum(yg).Backward();

        AssertClose(yc, yg.ToCpu(), 1e-4f);
        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-4f);
    }

    #endregion New ops parity (Sum / Softmax / LogSoftmax / LayerNorm / Gelu bwd / BMM bwd / broadcast-Add bwd / MulScalar)

    #region Cat / Stack / Narrow / Select GPU parity

    [SkippableFact]
    public void Cat_Forward_Parity_Axis0()
    {
        SkipIfNoCuda();
        var rng = new Random(30);
        var a = Tensor.Randn(new Shape(2, 3, 5), rng);
        var b = Tensor.Randn(new Shape(4, 3, 5), rng);
        var c = Tensor.Randn(new Shape(1, 3, 5), rng);

        var cpu = IndexingOps.Cat(new[] { a, b, c }, axis: 0);
        var gpu = IndexingOps.Cat(new[] {
            a.To(Device.Cuda(0)), b.To(Device.Cuda(0)), c.To(Device.Cuda(0))
        }, axis: 0).ToCpu();
        AssertClose(cpu, gpu);
    }

    [SkippableFact]
    public void Cat_Forward_Parity_Axis1()
    {
        SkipIfNoCuda();
        var rng = new Random(31);
        var a = Tensor.Randn(new Shape(3, 4, 6), rng);
        var b = Tensor.Randn(new Shape(3, 2, 6), rng);
        var c = Tensor.Randn(new Shape(3, 1, 6), rng);

        var cpu = IndexingOps.Cat(new[] { a, b, c }, axis: 1);
        var gpu = IndexingOps.Cat(new[] {
            a.To(Device.Cuda(0)), b.To(Device.Cuda(0)), c.To(Device.Cuda(0))
        }, axis: 1).ToCpu();
        AssertClose(cpu, gpu);
    }

    [SkippableFact]
    public void Stack_Forward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(32);
        var ts = new Tensor[5];
        for (int i = 0; i < ts.Length; i++) ts[i] = Tensor.Randn(new Shape(3, 4), rng);

        var cpu = IndexingOps.Stack(ts, axis: 0);
        var tsG = new Tensor[ts.Length];
        for (int i = 0; i < ts.Length; i++) tsG[i] = ts[i].To(Device.Cuda(0));
        var gpu = IndexingOps.Stack(tsG, axis: 0).ToCpu();
        AssertClose(cpu, gpu);
    }

    [SkippableFact]
    public void Cat_Backward_Parity_Axis1()
    {
        SkipIfNoCuda();
        var rng = new Random(33);
        var a = Tensor.Randn(new Shape(3, 4), rng);
        var b = Tensor.Randn(new Shape(3, 2), rng);

        var ac = a.SetRequiresGrad(true);
        var bc = b.SetRequiresGrad(true);
        var yc = IndexingOps.Cat(new[] { ac, bc }, axis: 1);
        TensorOps.Sum(yc).Backward();

        var ag = a.To(Device.Cuda(0)).SetRequiresGrad(true);
        var bg = b.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = IndexingOps.Cat(new[] { ag, bg }, axis: 1);
        TensorOps.Sum(yg).Backward();

        AssertClose(ac.Grad, ag.Grad.ToCpu());
        AssertClose(bc.Grad, bg.Grad.ToCpu());
    }

    [SkippableFact]
    public void Narrow_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(34);
        var x = Tensor.Randn(new Shape(4, 12, 3), rng);

        var xc = x.SetRequiresGrad(true);
        var yc = IndexingOps.Narrow(xc, axis: 1, start: 3, length: 5);
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = IndexingOps.Narrow(xg, axis: 1, start: 3, length: 5);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu());
    }

    [SkippableFact]
    public void Select_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(35);
        var x = Tensor.Randn(new Shape(6, 4, 3), rng);

        var xc = x.SetRequiresGrad(true);
        var yc = IndexingOps.Select(xc, axis: 0, index: 2);
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = IndexingOps.Select(xg, axis: 0, index: 2);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu());
    }

    #endregion Cat / Stack / Narrow / Select GPU parity
}
