using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.Gpu.V2;
using Xunit;

namespace NNW.V2.Gpu.Tests;

public partial class GpuV2ParityTests
{
    #region Bisecting tests for RNN backward divergence

    [SkippableFact]
    public void Permute_Contiguous_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(401);
        var x = Tensor.Randn(new Shape(2, 4, 3), rng);

        var xc = x.SetRequiresGrad(true);
        var yc = xc.Permute(1, 0, 2).Contiguous();
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = xg.Permute(1, 0, 2).Contiguous();
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-5f);
    }

    [SkippableFact]
    public void Reshape_MatMul_Reshape_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(402);
        var x = Tensor.Randn(new Shape(2, 4, 3), rng);
        var w = Tensor.Randn(new Shape(5, 3), rng);

        var xc = x.SetRequiresGrad(true);
        var wc = w.SetRequiresGrad(true);
        var xfc = xc.Reshape(2 * 4, 3);
        var ycp = xfc.MatMul(wc.Transpose(0, 1)).Reshape(2, 4, 5);
        TensorOps.Sum(ycp).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var xfg = xg.Reshape(2 * 4, 3);
        var ygp = xfg.MatMul(wg.Transpose(0, 1)).Reshape(2, 4, 5);
        TensorOps.Sum(ygp).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-4f);
        AssertClose(wc.Grad, wg.Grad.ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void Linear_Tanh_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(701);
        const int B = 2, I = 3, H = 5;
        var x = Tensor.Randn(new Shape(B, I), rng);
        var w = Tensor.Randn(new Shape(H, I), rng);
        var b = Tensor.Randn(new Shape(H), rng);

        var xc = x.SetRequiresGrad(true);
        var wc = w.SetRequiresGrad(true);
        var bc = b.SetRequiresGrad(true);
        var yc = (xc.MatMul(wc.Transpose(0, 1)) + bc).Tanh();
        TensorOps.Sum(yc).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var bg = b.To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = (xg.MatMul(wg.Transpose(0, 1)) + bg).Tanh();
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-4f);
        AssertClose(wc.Grad, wg.Grad.ToCpu(), 1e-4f);
        AssertClose(bc.Grad, bg.Grad.ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void Reshape_Linear_Reshape_Tanh_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(702);
        const int B = 2, T = 1, I = 3, H = 5;
        var x = Tensor.Randn(new Shape(B, T, I), rng);
        var w = Tensor.Randn(new Shape(H, I), rng);
        var b = Tensor.Randn(new Shape(H), rng);

        var xc = x.SetRequiresGrad(true);
        var wc = w.SetRequiresGrad(true);
        var bc = b.SetRequiresGrad(true);
        var xPermC = xc.Permute(1, 0, 2).Contiguous();
        var xFlatC = xPermC.Reshape(T * B, I);
        var projC = (xFlatC.MatMul(wc.Transpose(0, 1)) + bc).Reshape(T, B, H);
        var slC = IndexingOps.Select(projC, 0, 0);
        var stackedC = IndexingOps.Stack(new[] { slC.Tanh() }, axis: 0);
        var ycp = stackedC.Permute(1, 0, 2).Contiguous();
        TensorOps.Sum(ycp).Backward();

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var bg = b.To(Device.Cuda(0)).SetRequiresGrad(true);
        var xPermG = xg.Permute(1, 0, 2).Contiguous();
        var xFlatG = xPermG.Reshape(T * B, I);
        var projG = (xFlatG.MatMul(wg.Transpose(0, 1)) + bg).Reshape(T, B, H);
        var slG = IndexingOps.Select(projG, 0, 0);
        var stackedG = IndexingOps.Stack(new[] { slG.Tanh() }, axis: 0);
        var ygp = stackedG.Permute(1, 0, 2).Contiguous();
        TensorOps.Sum(ygp).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-4f);
    }

    [SkippableTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Loop_TanhMatMul_Bug_Bisect(int T)
    {
        SkipIfNoCuda();
        const int B = 2, H = 5;
        var rng = new Random(70);
        var w = Tensor.Randn(new Shape(H, H), rng);
        var h0 = Tensor.Randn(new Shape(B, H), rng);

        var wc = w.SetRequiresGrad(true);
        var h0c = h0.SetRequiresGrad(true);
        var hC = h0c;
        for (int t = 0; t < T; t++)
            hC = hC.MatMul(wc).Tanh();
        TensorOps.Sum(hC).Backward();
        var gC = h0c.Grad;

        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var h0g = h0.To(Device.Cuda(0)).SetRequiresGrad(true);
        var hG = h0g;
        for (int t = 0; t < T; t++)
            hG = hG.MatMul(wg).Tanh();
        TensorOps.Sum(hG).Backward();
        var gG = h0g.Grad.ToCpu();

        // Tolerance 5e-4: накопление FP32-ошибки в MatMul+Tanh-цепочке
        // длиной T=3..4 даёт расхождение порядка 1.3e-4. Сам тест служит
        // регрессией на баг с offset в Engine.CopyTensorContents (раньше
        // расхождение было 0.05+ — в сотни раз больше).
        AssertClose(gC, gG, 5e-4f);
    }

    [SkippableTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SelectStack_Loop_Bug_Bisect(int T)
    {
        SkipIfNoCuda();
        const int B = 2, H = 5;
        var rng = new Random(70);
        var x = Tensor.Randn(new Shape(T, B, H), rng);
        var w = Tensor.Randn(new Shape(H, H), rng);

        // CPU
        var xc = x.SetRequiresGrad(true);
        var wc = w.SetRequiresGrad(true);
        var hc = Tensor.Zeros(new Shape(B, H));
        var outsC = new System.Collections.Generic.List<Tensor>();
        for (int t = 0; t < T; t++)
        {
            var xPt = IndexingOps.Select(xc, 0, t);
            var pre = xPt + hc.MatMul(wc);
            hc = pre.Tanh();
            outsC.Add(hc);
        }
        var stackedC = IndexingOps.Stack(outsC, 0);
        TensorOps.Sum(stackedC).Backward();
        var gC = xc.Grad;

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var hg = Tensor.Zeros(new Shape(B, H), DType.Float32, Device.Cuda(0));
        var outsG = new System.Collections.Generic.List<Tensor>();
        for (int t = 0; t < T; t++)
        {
            var xPt = IndexingOps.Select(xg, 0, t);
            var pre = xPt + hg.MatMul(wg);
            hg = pre.Tanh();
            outsG.Add(hg);
        }
        var stackedG = IndexingOps.Stack(outsG, 0);
        TensorOps.Sum(stackedG).Backward();
        var gG = xg.Grad.ToCpu();

        // Tolerance 5e-4 — см. комментарий в Loop_TanhMatMul_Bug_Bisect.
        AssertClose(gC, gG, 5e-4f);
    }

    [SkippableTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Select_Add_Tanh_NoStack_Bisect(int T)
    {
        SkipIfNoCuda();
        const int B = 2, H = 5;
        var rng = new Random(70);
        var x = Tensor.Randn(new Shape(T, B, H), rng);
        var w = Tensor.Randn(new Shape(H, H), rng);

        var xc = x.SetRequiresGrad(true);
        var wc = w.SetRequiresGrad(true);
        var hc = Tensor.Zeros(new Shape(B, H));
        for (int t = 0; t < T; t++)
        {
            var xPt = IndexingOps.Select(xc, 0, t);
            var pre = xPt + hc.MatMul(wc);
            hc = pre.Tanh();
        }
        TensorOps.Sum(hc).Backward();
        var gC = xc.Grad;

        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var wg = w.To(Device.Cuda(0)).SetRequiresGrad(true);
        var hg = Tensor.Zeros(new Shape(B, H), DType.Float32, Device.Cuda(0));
        for (int t = 0; t < T; t++)
        {
            var xPt = IndexingOps.Select(xg, 0, t);
            var pre = xPt + hg.MatMul(wg);
            hg = pre.Tanh();
        }
        TensorOps.Sum(hg).Backward();
        var gG = xg.Grad.ToCpu();

        AssertClose(gC, gG, 1e-4f);
    }

    [SkippableTheory]
    [InlineData(3, 1, 5)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 2, 3)]
    [InlineData(3, 2, 4)]
    [InlineData(3, 2, 5)]
    public void Manual_RNN_T1_Backward_Parity(int T, int B, int H)
    {
        SkipIfNoCuda();
        var xRng = new Random(70);
        const int I = 3;
        var x = Tensor.Randn(new Shape(B, T, I), xRng);
        var pRng = new Random(91);
        // Same init order as RNNCell ctor.
        float bound = 1f / MathF.Sqrt(H);
        var wIH = Init.Uniform_(Tensor.Empty(new Shape(H, I)), -bound, bound, pRng);
        var wHH = Init.Uniform_(Tensor.Empty(new Shape(H, H)), -bound, bound, pRng);
        var bIH = Init.Uniform_(Tensor.Empty(new Shape(H)), -bound, bound, pRng);
        var bHH = Init.Uniform_(Tensor.Empty(new Shape(H)), -bound, bound, pRng);

        Tensor RunCpu(out Tensor xRef)
        {
            var xc = x.SetRequiresGrad(true);
            xRef = xc;
            var wIHc = wIH.SetRequiresGrad(true);
            var wHHc = wHH.SetRequiresGrad(true);
            var bIHc = bIH.SetRequiresGrad(true);
            var bHHc = bHH.SetRequiresGrad(true);
            var xTb = xc.Permute(1, 0, 2).Contiguous();
            var xFlat = xTb.Reshape(T * B, I);
            var xProj = xFlat.MatMul(wIHc.Transpose(0, 1));
            xProj = xProj + bIHc;
            xProj = xProj + bHHc;
            xProj = xProj.Reshape(T, B, H);
            var wHhT = wHHc.Transpose(0, 1);
            var hLocal = Tensor.Zeros(new Shape(B, H));
            var outs = new System.Collections.Generic.List<Tensor>();
            for (int t = 0; t < T; t++)
            {
                var xPt = IndexingOps.Select(xProj, 0, t);
                var preact = xPt + hLocal.MatMul(wHhT);
                hLocal = preact.Tanh();
                outs.Add(hLocal);
            }
            var stacked = IndexingOps.Stack(outs, 0);
            var output = stacked.Permute(1, 0, 2).Contiguous();
            TensorOps.Sum(output).Backward();
            return xc.Grad;
        }

        Tensor RunGpu()
        {
            var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
            var wIHg = wIH.To(Device.Cuda(0)).SetRequiresGrad(true);
            var wHHg = wHH.To(Device.Cuda(0)).SetRequiresGrad(true);
            var bIHg = bIH.To(Device.Cuda(0)).SetRequiresGrad(true);
            var bHHg = bHH.To(Device.Cuda(0)).SetRequiresGrad(true);
            var xTb = xg.Permute(1, 0, 2).Contiguous();
            var xFlat = xTb.Reshape(T * B, I);
            var xProj = xFlat.MatMul(wIHg.Transpose(0, 1));
            xProj = xProj + bIHg;
            xProj = xProj + bHHg;
            xProj = xProj.Reshape(T, B, H);
            var wHhT = wHHg.Transpose(0, 1);
            var hLocal = Tensor.Zeros(new Shape(B, H), DType.Float32, Device.Cuda(0));
            var outs = new System.Collections.Generic.List<Tensor>();
            for (int t = 0; t < T; t++)
            {
                var xPt = IndexingOps.Select(xProj, 0, t);
                var preact = xPt + hLocal.MatMul(wHhT);
                hLocal = preact.Tanh();
                outs.Add(hLocal);
            }
            var stacked = IndexingOps.Stack(outs, 0);
            var output = stacked.Permute(1, 0, 2).Contiguous();
            TensorOps.Sum(output).Backward();
            return xg.Grad;
        }

        var gCpu = RunCpu(out _);
        var gGpu = RunGpu().ToCpu();
        AssertClose(gCpu, gGpu, 1e-3f);
    }

    [SkippableFact]
    public void RNN_T1_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(70);
        const int B = 1, T = 1, I = 2, H = 2;
        var x = Tensor.Randn(new Shape(B, T, I), rng);

        var rnnC = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(91));
        var xc = x.SetRequiresGrad(true);
        var (yc, _) = rnnC.ForwardSeq(xc);
        TensorOps.Sum(yc).Backward();

        var rnnG = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(91));
        rnnG.To(Device.Cuda(0));
        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var (yg, _) = rnnG.ForwardSeq(xg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-4f);
    }

    [SkippableTheory]
    [InlineData(1, 1, 2, 2)]
    [InlineData(1, 2, 2, 2)]
    [InlineData(2, 2, 3, 3)]
    [InlineData(2, 3, 3, 5)]
    [InlineData(2, 4, 3, 5)]
    public void RNN_TN_Backward_Bisect(int B, int T, int I, int H)
    {
        SkipIfNoCuda();
        var rng = new Random(70);
        var x = Tensor.Randn(new Shape(B, T, I), rng);

        var rnnC = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(91));
        var xc = x.SetRequiresGrad(true);
        var (yc, _) = rnnC.ForwardSeq(xc);
        TensorOps.Sum(yc).Backward();

        var rnnG = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(91));
        rnnG.To(Device.Cuda(0));
        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var (yg, _) = rnnG.ForwardSeq(xg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void Stack_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(403);
        var ts = new Tensor[3];
        for (int i = 0; i < ts.Length; i++) ts[i] = Tensor.Randn(new Shape(2, 4), rng);

        var tsC = new Tensor[ts.Length];
        for (int i = 0; i < tsC.Length; i++) tsC[i] = ts[i].SetRequiresGrad(true);
        var yc = IndexingOps.Stack(tsC, axis: 0);
        TensorOps.Sum(yc).Backward();

        var tsG = new Tensor[ts.Length];
        for (int i = 0; i < tsG.Length; i++) tsG[i] = ts[i].To(Device.Cuda(0)).SetRequiresGrad(true);
        var yg = IndexingOps.Stack(tsG, axis: 0);
        TensorOps.Sum(yg).Backward();

        for (int i = 0; i < ts.Length; i++)
            AssertClose(tsC[i].Grad, tsG[i].Grad.ToCpu(), 1e-5f);
    }

    #endregion Bisecting tests for RNN backward divergence
}
