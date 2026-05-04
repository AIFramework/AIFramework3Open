using System;
using AI.ML.NeuralNetworks.Gpu.CuBlas;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace AI.ML.NeuralNetworks.Gpu.V2;

internal sealed partial class GpuOps
{
    #region RNN / GRU full-sequence

    /// <summary>
    /// Forward + autograd для полной vanilla-RNN-последовательности.
    /// inputs: [xProj (T,B,H), wHhT (H,H), h0OrEmpty (B,H or empty)].
    /// </summary>
    private Tensor[] RnnSeqOp(Tensor[] ins, RnnSeqAttrs a)
    {
        int T = a.T, B = a.B, H = a.H;
        if (ins.Length != 3)
            throw new ArgumentException("RnnSeqOp: ожидалось 3 входа: [xProj, wHhT, h0OrEmpty].");
        var xProj = ins[0]; var wHhT = ins[1]; var h0 = ins[2];

        if (xProj.DType != DType.Float32 || wHhT.DType != DType.Float32)
            throw new ArgumentException("RnnSeqOp: только Float32.");
        if (xProj.Rank != 3 || xProj.Shape[0] != T || xProj.Shape[1] != B || xProj.Shape[2] != H)
            throw new ArgumentException($"RnnSeqOp: xProj={xProj.Shape}, ожидалось ({T},{B},{H}).");
        if (wHhT.Rank != 2 || wHhT.Shape[0] != H || wHhT.Shape[1] != H)
            throw new ArgumentException($"RnnSeqOp: wHhT={wHhT.Shape}, ожидалось ({H},{H}).");
        if (a.HasH0 && (h0.Rank != 2 || h0.Shape[0] != B || h0.Shape[1] != H))
            throw new ArgumentException($"RnnSeqOp: h0={h0.Shape}, ожидалось ({B},{H}).");
        if (a.Nonlinearity != 0 && a.Nonlinearity != 1)
            throw new ArgumentException("RnnSeqOp: nonlinearity ∈ {0:tanh, 1:relu}.");

        var dev = xProj.Device;
        int planeBH = B * H;

        var xProjC = xProj.IsContiguous ? xProj : xProj.Contiguous();
        var wHhTc = wHhT.IsContiguous ? wHhT : wHhT.Contiguous();

        bool needGrad = TapeContext.IsGradEnabled
            && (xProj.RequiresGrad || wHhT.RequiresGrad || (a.HasH0 && h0.RequiresGrad));

        using (TapeContext.NoGrad())
        {
            var hAll = Tensor.Empty(new Shape(T + 1, B, H), DType.Float32, dev);
            var outputs = Tensor.Empty(new Shape(T, B, H), DType.Float32, dev);
            var tmpGh = Tensor.Empty(new Shape(B, H), DType.Float32, dev);
            var tmpPre = Tensor.Empty(new Shape(B, H), DType.Float32, dev);

            var hAllV = ((CudaStorage)hAll.Storage).AsView<float>();
            var outputsV = ((CudaStorage)outputs.Storage).AsView<float>();
            var xProjV = ViewOf(xProjC);
            var wHhTV = ViewOf(wHhTc);
            var tmpGhV = ViewOf(tmpGh);
            var tmpPreV = ViewOf(tmpPre);

            if (a.HasH0)
            {
                var h0c = h0.IsContiguous ? h0 : h0.Contiguous();
                hAllV.SubView(0, planeBH).CopyFrom(ViewOf(h0c));
            }
            else
            {
                hAllV.SubView(0, planeBH).MemSetToZero(_gpu.Accelerator.DefaultStream);
            }

            for (int t = 0; t < T; t++)
            {
                var hPrev = hAllV.SubView((long)t * planeBH, planeBH);
                var hCur = hAllV.SubView((long)(t + 1) * planeBH, planeBH);
                var xProjT = xProjV.SubView((long)t * planeBH, planeBH);
                var outT = outputsV.SubView((long)t * planeBH, planeBH);

                SgemmRm(hPrev, B, H, false, wHhTV, H, H, false, 1f, 0f, tmpGhV, B, H);
                _add(planeBH, xProjT, tmpGhV, tmpPreV);
                if (a.Nonlinearity == 0)
                    _tanh(planeBH, tmpPreV, hCur);
                else
                    _relu(planeBH, tmpPreV, hCur);
                outT.CopyFrom(hCur);
            }

            tmpGh.Storage.Dispose();
            tmpPre.Storage.Dispose();
            if (needGrad)
            {
                var fn = new GpuRnnSeqFn(this, xProjC, wHhTc, hAll, T, B, H, a.Nonlinearity, a.HasH0);
                fn.RegisterInput(xProj);
                fn.RegisterInput(wHhT);
                if (a.HasH0) fn.RegisterInput(h0);
                outputs.GradFn = fn;
            }
            else
            {
                hAll.Storage.Dispose();
            }
            return new[] { outputs };
        }
    }

    /// <summary>Backward для vanilla-RNN.</summary>
    private sealed class GpuRnnSeqFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _xProj, _wHhT, _hAll;
        private readonly int _T, _B, _H, _nonlin;
        private readonly bool _hasH0;
        public GpuRnnSeqFn(GpuOps ops, Tensor xProj, Tensor wHhT, Tensor hAll,
            int t, int b, int h, int nonlin, bool hasH0)
        {
            _ops = ops; _xProj = xProj; _wHhT = wHhT; _hAll = hAll;
            _T = t; _B = b; _H = h; _nonlin = nonlin; _hasH0 = hasH0;
        }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                if (gradOutput.Rank != 3 || gradOutput.Shape[0] != _T
                    || gradOutput.Shape[1] != _B || gradOutput.Shape[2] != _H)
                    throw new InvalidOperationException(
                        $"GpuRnnSeqFn: gradOutput={gradOutput.Shape}, ожидалось ({_T},{_B},{_H}).");
                var gOut = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var dev = gOut.Device;
                int H = _H;
                int planeBH = _B * H;

                var dxProj = Tensor.Zeros(new Shape(_T, _B, H), DType.Float32, dev);
                var dWhhT = Tensor.Zeros(new Shape(H, H), DType.Float32, dev);
                Tensor dh0 = _hasH0 ? Tensor.Zeros(new Shape(_B, H), DType.Float32, dev) : null;

                var dh = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dPre = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dhPrev = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);

                var hAllV = ((CudaStorage)_hAll.Storage).AsView<float>();
                var gOutV = ViewOf(gOut);
                var dxProjV = ViewOf(dxProj);
                var dWhhTV = ViewOf(dWhhT);
                var wHhTV = ViewOf(_wHhT);
                var dhV = ViewOf(dh);
                var dPreV = ViewOf(dPre);
                var dhPrevV = ViewOf(dhPrev);

                dhV.CopyFrom(gOutV.SubView((long)(_T - 1) * planeBH, planeBH));

                for (int t = _T - 1; t >= 0; t--)
                {
                    var hPrev = hAllV.SubView((long)t * planeBH, planeBH);
                    var hCur = hAllV.SubView((long)(t + 1) * planeBH, planeBH);
                    var dxProjT = dxProjV.SubView((long)t * planeBH, planeBH);

                    if (_nonlin == 0)
                        _ops._tanhBwd(planeBH, hCur, dhV, dPreV);
                    else
                        _ops._reluBwd(planeBH, hCur, dhV, dPreV);

                    dxProjT.CopyFrom(dPreV);

                    _ops.SgemmRm(hPrev, _B, H, true, dPreV, _B, H, false,
                        1f, 1f, dWhhTV, H, H);

                    if (t > 0)
                    {
                        _ops.SgemmRm(dPreV, _B, H, false, wHhTV, H, H, true,
                            1f, 0f, dhPrevV, _B, H);
                        _ops._add(planeBH,
                            gOutV.SubView((long)(t - 1) * planeBH, planeBH), dhPrevV, dhV);
                    }
                    else if (_hasH0)
                    {
                        _ops.SgemmRm(dPreV, _B, H, false, wHhTV, H, H, true,
                            1f, 0f, ViewOf(dh0), _B, H);
                    }
                }
                var grads = new System.Collections.Generic.List<Tensor>(3) { dxProj, dWhhT };
                if (_hasH0) grads.Add(dh0);
                return grads.ToArray();
            }
        }
    }

    /// <summary>
    /// Forward + autograd для полной GRU-последовательности.
    /// inputs: [xProj (T,B,3H), wHhT (H,3H), bHhOrEmpty (3H or empty), h0OrEmpty (B,H or empty)].
    /// </summary>
    private Tensor[] GruSeqOp(Tensor[] ins, GruSeqAttrs a)
    {
        int T = a.T, B = a.B, H = a.H, H3 = 3 * H;
        if (ins.Length != 4)
            throw new ArgumentException("GruSeqOp: ожидалось 4 входа: [xProj, wHhT, bHhOrEmpty, h0OrEmpty].");
        var xProj = ins[0]; var wHhT = ins[1]; var bHh = ins[2]; var h0 = ins[3];

        if (xProj.DType != DType.Float32 || wHhT.DType != DType.Float32)
            throw new ArgumentException("GruSeqOp: только Float32.");
        if (xProj.Rank != 3 || xProj.Shape[0] != T || xProj.Shape[1] != B || xProj.Shape[2] != H3)
            throw new ArgumentException($"GruSeqOp: xProj={xProj.Shape}, ожидалось ({T},{B},{H3}).");
        if (wHhT.Rank != 2 || wHhT.Shape[0] != H || wHhT.Shape[1] != H3)
            throw new ArgumentException($"GruSeqOp: wHhT={wHhT.Shape}, ожидалось ({H},{H3}).");
        if (a.HasBhh && (bHh.Rank != 1 || bHh.Shape[0] != H3))
            throw new ArgumentException($"GruSeqOp: bHh={bHh.Shape}, ожидалось ({H3}).");
        if (a.HasH0 && (h0.Rank != 2 || h0.Shape[0] != B || h0.Shape[1] != H))
            throw new ArgumentException($"GruSeqOp: h0={h0.Shape}, ожидалось ({B},{H}).");

        var dev = xProj.Device;
        int planeBH = B * H;
        int planeBH3 = B * H3;
        int planeSaved = 4 * B * H;

        var xProjC = xProj.IsContiguous ? xProj : xProj.Contiguous();
        var wHhTc = wHhT.IsContiguous ? wHhT : wHhT.Contiguous();
        Tensor bHhC = a.HasBhh ? (bHh.IsContiguous ? bHh : bHh.Contiguous()) : null;

        bool needGrad = TapeContext.IsGradEnabled
            && (xProj.RequiresGrad || wHhT.RequiresGrad
                || (a.HasBhh && bHh.RequiresGrad) || (a.HasH0 && h0.RequiresGrad));

        using (TapeContext.NoGrad())
        {
            var hAll = Tensor.Empty(new Shape(T + 1, B, H), DType.Float32, dev);
            var savedAll = Tensor.Empty(new Shape(T, 4, B, H), DType.Float32, dev);
            var outputs = Tensor.Empty(new Shape(T, B, H), DType.Float32, dev);
            var ghBuf = Tensor.Empty(new Shape(B, H3), DType.Float32, dev);

            var hAllV = ((CudaStorage)hAll.Storage).AsView<float>();
            var savedAllV = ((CudaStorage)savedAll.Storage).AsView<float>();
            var outputsV = ((CudaStorage)outputs.Storage).AsView<float>();
            var xProjV = ViewOf(xProjC);
            var wHhTV = ViewOf(wHhTc);
            var ghBufV = ViewOf(ghBuf);

            if (a.HasH0)
            {
                var h0c = h0.IsContiguous ? h0 : h0.Contiguous();
                hAllV.SubView(0, planeBH).CopyFrom(ViewOf(h0c));
            }
            else
            {
                hAllV.SubView(0, planeBH).MemSetToZero(_gpu.Accelerator.DefaultStream);
            }

            for (int t = 0; t < T; t++)
            {
                var hPrev = hAllV.SubView((long)t * planeBH, planeBH);
                var hCur = hAllV.SubView((long)(t + 1) * planeBH, planeBH);
                var savedT = savedAllV.SubView((long)t * planeSaved, planeSaved);
                var xProjT = xProjV.SubView((long)t * planeBH3, planeBH3);
                var outT = outputsV.SubView((long)t * planeBH, planeBH);

                SgemmRm(hPrev, B, H, false, wHhTV, H, H3, false, 1f, 0f, ghBufV, B, H3);
                if (a.HasBhh)
                {
                    AddBiasInplaceB3H(ghBufV, ViewOf(bHhC), B, H3);
                }
                _gruStepFwd(planeBH, xProjT, ghBufV, hPrev, hCur, savedT, H, planeBH, /*needSave=*/1);
                outT.CopyFrom(hCur);
            }

            ghBuf.Storage.Dispose();
            if (needGrad)
            {
                var fn = new GpuGruSeqFn(this, xProjC, wHhTc, hAll, savedAll,
                    T, B, H, a.HasBhh, a.HasH0);
                fn.RegisterInput(xProj);
                fn.RegisterInput(wHhT);
                if (a.HasBhh) fn.RegisterInput(bHh);
                if (a.HasH0) fn.RegisterInput(h0);
                outputs.GradFn = fn;
            }
            else
            {
                hAll.Storage.Dispose();
                savedAll.Storage.Dispose();
            }
            return new[] { outputs };
        }
    }

    /// <summary>
    /// In-place += broadcast bias (1D, длина = innerDim) к (outerDim, innerDim) тензору.
    /// </summary>
    private void AddBiasInplaceB3H(ArrayView<float> dst, ArrayView<float> bias, int outer, int inner)
    {
        var args = new V2Kernels.BroadcastArgs
        {
            Op = 0,
            AOffset = 0, BOffset = 0,
            O0 = 1, O1 = 1, O2 = 1, O3 = 1, O4 = outer, O5 = inner,
            SA0 = 0, SA1 = 0, SA2 = 0, SA3 = 0, SA4 = inner, SA5 = 1,
            SB0 = 0, SB1 = 0, SB2 = 0, SB3 = 0, SB4 = 0, SB5 = 1,
        };
        _bcast(outer * inner, dst, bias, dst, args);
    }

    /// <summary>Backward для полной GRU-последовательности.</summary>
    private sealed class GpuGruSeqFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _xProj, _wHhT, _hAll, _savedAll;
        private readonly int _T, _B, _H;
        private readonly bool _hasBhh, _hasH0;
        public GpuGruSeqFn(GpuOps ops, Tensor xProj, Tensor wHhT, Tensor hAll, Tensor savedAll,
            int t, int b, int h, bool hasBhh, bool hasH0)
        {
            _ops = ops; _xProj = xProj; _wHhT = wHhT; _hAll = hAll; _savedAll = savedAll;
            _T = t; _B = b; _H = h; _hasBhh = hasBhh; _hasH0 = hasH0;
        }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                if (gradOutput.Rank != 3 || gradOutput.Shape[0] != _T
                    || gradOutput.Shape[1] != _B || gradOutput.Shape[2] != _H)
                    throw new InvalidOperationException(
                        $"GpuGruSeqFn: gradOutput={gradOutput.Shape}, ожидалось ({_T},{_B},{_H}).");
                var gOut = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var dev = gOut.Device;
                int H = _H, H3 = 3 * H;
                int planeBH = _B * H;
                int planeBH3 = _B * H3;
                int planeSaved = 4 * _B * H;

                var dxProj = Tensor.Zeros(new Shape(_T, _B, H3), DType.Float32, dev);
                var dWhhT = Tensor.Zeros(new Shape(H, H3), DType.Float32, dev);
                Tensor dBhh = _hasBhh ? Tensor.Zeros(new Shape(H3), DType.Float32, dev) : null;
                Tensor dh0 = _hasH0 ? Tensor.Zeros(new Shape(_B, H), DType.Float32, dev) : null;

                var dh = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dGx = Tensor.Empty(new Shape(_B, H3), DType.Float32, dev);
                var dGh = Tensor.Empty(new Shape(_B, H3), DType.Float32, dev);
                var dHPrevStep = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dhAccum = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);

                var hAllV = ((CudaStorage)_hAll.Storage).AsView<float>();
                var savedAllV = ((CudaStorage)_savedAll.Storage).AsView<float>();
                var gOutV = ViewOf(gOut);
                var dxProjV = ViewOf(dxProj);
                var dWhhTV = ViewOf(dWhhT);
                var wHhTV = ViewOf(_wHhT);
                var dhV = ViewOf(dh);
                var dGxV = ViewOf(dGx);
                var dGhV = ViewOf(dGh);
                var dHPrevStepV = ViewOf(dHPrevStep);
                var dhAccumV = ViewOf(dhAccum);

                dhV.CopyFrom(gOutV.SubView((long)(_T - 1) * planeBH, planeBH));

                for (int t = _T - 1; t >= 0; t--)
                {
                    var hPrev = hAllV.SubView((long)t * planeBH, planeBH);
                    var savedT = savedAllV.SubView((long)t * planeSaved, planeSaved);
                    var dxProjT = dxProjV.SubView((long)t * planeBH3, planeBH3);

                    _ops._gruStepBwd(planeBH, dhV, savedT, hPrev,
                        dGxV, dGhV, dHPrevStepV, H, planeBH);

                    dxProjT.CopyFrom(dGxV);

                    if (_hasBhh)
                    {
                        SumOverBatchAdd(dGhV, ViewOf(dBhh), _B, H3);
                    }

                    _ops.SgemmRm(hPrev, _B, H, true, dGhV, _B, H3, false,
                        1f, 1f, dWhhTV, H, H3);

                    _ops.SgemmRm(dGhV, _B, H3, false, wHhTV, H, H3, true,
                        1f, 0f, dhAccumV, _B, H);
                    _ops._add(planeBH, dhAccumV, dHPrevStepV, dhAccumV);

                    if (t > 0)
                    {
                        _ops._add(planeBH,
                            gOutV.SubView((long)(t - 1) * planeBH, planeBH), dhAccumV, dhV);
                    }
                    else if (_hasH0)
                    {
                        ViewOf(dh0).CopyFrom(dhAccumV);
                    }
                }
                var grads = new System.Collections.Generic.List<Tensor>(4) { dxProj, dWhhT };
                if (_hasBhh) grads.Add(dBhh);
                if (_hasH0) grads.Add(dh0);
                return grads.ToArray();
            }
        }

        private void SumOverBatchAdd(ArrayView<float> src, ArrayView<float> dst, int batches, int features)
        {
            var dev = AI.ML.NeuralNetworks.V2.Device.Cuda(_ops._gpu.DeviceIndex);
            var tmp = Tensor.Empty(new Shape(features), DType.Float32, dev);
            var tmpV = ViewOf(tmp);
            _ops._sumAxis(features, src, tmpV, batches, features);
            _ops._add(features, dst, tmpV, dst);
        }
    }

    /// <summary>
    /// Native GPU x · s через _smul (один kernel, без broadcast'а через Full+Mul).
    /// </summary>
    private Tensor[] MulScalarOp(Tensor x, ScalarAttrs attrs)
    {
        if (x.DType != DType.Float32)
            throw new ArgumentException("MulScalarOp(GPU): только Float32.");
        var xc = x.IsContiguous ? x : x.Contiguous();
        var y = Tensor.Empty(xc.Shape, x.DType, x.Device);
        _smul((int)xc.NumElements, ViewOf(xc), attrs.Value, ViewOf(y));
        return new[] { y };
    }

    #endregion RNN / GRU full-sequence
}
