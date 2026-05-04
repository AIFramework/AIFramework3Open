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
    #region LSTM full-sequence

    /// <summary>
    /// Helper: row-major SGEMM C[M,N] = α · op(A) · op(B) + β · C через cuBLAS.
    /// Если cuBLAS недоступен — fallback на ILGPU naive gemm (только для no-trans).
    /// </summary>
    private void SgemmRm(
        ArrayView<float> A, int aRows, int aCols, bool transA,
        ArrayView<float> B, int bRows, int bCols, bool transB,
        float alpha, float beta,
        ArrayView<float> C, int cRows, int cCols)
    {
        int M = transA ? aCols : aRows;
        int K = transA ? aRows : aCols;
        int Mb = transB ? bCols : bRows;
        int N = transB ? bRows : bCols;
        if (Mb != K) throw new ArgumentException($"SgemmRm: K mismatch ({K} vs {Mb}).");
        if (M != cRows || N != cCols)
            throw new ArgumentException($"SgemmRm: C shape ({cRows},{cCols}) != ({M},{N}).");

        if (_gpu.CuBlas.IsAvailable)
        {
            var aPtr = A.GetDevicePointer();
            var bPtr = B.GetDevicePointer();
            var cPtr = C.GetDevicePointer();
            var opA = transA ? CublasOp.T : CublasOp.N;
            var opB = transB ? CublasOp.T : CublasOp.N;
            // Row-major трюк: cuBLAS считает в col-major. Подставляем (B, A) и инвертируем
            // op'ы в правильную строну. Ldы — это leading dims в row-major (число столбцов).
            int lda = aCols;       // row-major leading dim of A
            int ldb = bCols;       // row-major leading dim of B
            int ldc = cCols;       // row-major leading dim of C
            _gpu.CuBlas.Sgemm(opB, opA, N, M, K, alpha, bPtr, ldb, aPtr, lda, beta, cPtr, ldc);
            return;
        }

        // Fallback: только no-trans поддерживает наш naive gemm. Для transA/B пришлось бы
        // материализовать Contiguous-транспонированную копию — пока не тратимся (cuBLAS почти всегда есть).
        if (transA || transB)
            throw new NotSupportedException("SgemmRm fallback: транспозиции без cuBLAS не поддержаны.");
        if (beta != 0f)
            throw new NotSupportedException("SgemmRm fallback: beta != 0 без cuBLAS не поддержан.");
        _gemm(new Index2D(M, N), A, B, C, M, N, K);
    }

    /// <summary>
    /// Forward + autograd для полной LSTM-последовательности.
    /// inputs: [xProj (T,B,4H), wHhT (H,4H), h0OrEmpty (B,H or empty), c0OrEmpty (B,H or empty)].
    /// outputs: <c>packed (T+1, B, H)</c>. Планы [0..T-1] = h_t, план T = c_T.
    /// </summary>
    private Tensor[] LstmSeqOp(Tensor[] ins, LstmSeqAttrs a)
    {
        int T = a.T, B = a.B, H = a.H, H4 = 4 * H;
        if (ins.Length != 4)
            throw new ArgumentException("LstmSeqOp: ожидалось 4 входа: [xProj, wHhT, h0OrEmpty, c0OrEmpty].");
        var xProj = ins[0];
        var wHhT = ins[1];
        var h0 = ins[2];
        var c0 = ins[3];

        if (xProj.DType != DType.Float32 || wHhT.DType != DType.Float32)
            throw new ArgumentException("LstmSeqOp: только Float32.");
        if (xProj.Rank != 3 || xProj.Shape[0] != T || xProj.Shape[1] != B || xProj.Shape[2] != H4)
            throw new ArgumentException($"LstmSeqOp: xProj={xProj.Shape}, ожидалось ({T},{B},{H4}).");
        if (wHhT.Rank != 2 || wHhT.Shape[0] != H || wHhT.Shape[1] != H4)
            throw new ArgumentException($"LstmSeqOp: wHhT={wHhT.Shape}, ожидалось ({H},{H4}).");
        if (a.HasH0 && (h0.Rank != 2 || h0.Shape[0] != B || h0.Shape[1] != H))
            throw new ArgumentException($"LstmSeqOp: h0={h0.Shape}, ожидалось ({B},{H}).");
        if (a.HasC0 && (c0.Rank != 2 || c0.Shape[0] != B || c0.Shape[1] != H))
            throw new ArgumentException($"LstmSeqOp: c0={c0.Shape}, ожидалось ({B},{H}).");

        var dev = xProj.Device;
        int planeBH = B * H;
        int planeB4H = B * H4;
        int planeSaved = 5 * B * H;

        var xProjC = xProj.IsContiguous ? xProj : xProj.Contiguous();
        var wHhTc = wHhT.IsContiguous ? wHhT : wHhT.Contiguous();

        bool needGrad = TapeContext.IsGradEnabled
            && (xProj.RequiresGrad || wHhT.RequiresGrad
                || (a.HasH0 && h0.RequiresGrad) || (a.HasC0 && c0.RequiresGrad));

        using (TapeContext.NoGrad())
        {
            var packed = Tensor.Empty(new Shape(T + 1, B, H), DType.Float32, dev);
            var hAll = Tensor.Empty(new Shape(T + 1, B, H), DType.Float32, dev);
            var cAll = Tensor.Empty(new Shape(T + 1, B, H), DType.Float32, dev);
            var savedAll = Tensor.Empty(new Shape(T, 5, B, H), DType.Float32, dev);
            var tmpGh = Tensor.Empty(new Shape(B, H4), DType.Float32, dev);
            var tmpPre = Tensor.Empty(new Shape(B, H4), DType.Float32, dev);

            var packedV = ((CudaStorage)packed.Storage).AsView<float>();
            var hAllV = ((CudaStorage)hAll.Storage).AsView<float>();
            var cAllV = ((CudaStorage)cAll.Storage).AsView<float>();
            var savedAllV = ((CudaStorage)savedAll.Storage).AsView<float>();
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
            if (a.HasC0)
            {
                var c0c = c0.IsContiguous ? c0 : c0.Contiguous();
                cAllV.SubView(0, planeBH).CopyFrom(ViewOf(c0c));
            }
            else
            {
                cAllV.SubView(0, planeBH).MemSetToZero(_gpu.Accelerator.DefaultStream);
            }

            for (int t = 0; t < T; t++)
            {
                var hPrev = hAllV.SubView((long)t * planeBH, planeBH);
                var cPrev = cAllV.SubView((long)t * planeBH, planeBH);
                var hCur = hAllV.SubView((long)(t + 1) * planeBH, planeBH);
                var cCur = cAllV.SubView((long)(t + 1) * planeBH, planeBH);
                var savedT = savedAllV.SubView((long)t * planeSaved, planeSaved);
                var xProjT = xProjV.SubView((long)t * planeB4H, planeB4H);
                var packedT = packedV.SubView((long)t * planeBH, planeBH);

                SgemmRm(hPrev, B, H, false, wHhTV, H, H4, false, 1f, 0f, tmpGhV, B, H4);
                _add(planeB4H, xProjT, tmpGhV, tmpPreV);
                _lstmStepFwd(planeBH, tmpPreV, cPrev, hCur, cCur, savedT, H, planeBH, /*needSave=*/1);
                packedT.CopyFrom(hCur);
            }
            // Plane T = c_T.
            packedV.SubView((long)T * planeBH, planeBH).CopyFrom(cAllV.SubView((long)T * planeBH, planeBH));

            tmpGh.Storage.Dispose();
            tmpPre.Storage.Dispose();

            if (needGrad)
            {
                var fn = new GpuLstmSeqFn(this, xProjC, wHhTc, hAll, cAll, savedAll, T, B, H,
                    a.HasH0, a.HasC0);
                fn.RegisterInput(xProj);
                fn.RegisterInput(wHhT);
                if (a.HasH0) fn.RegisterInput(h0);
                if (a.HasC0) fn.RegisterInput(c0);
                packed.GradFn = fn;
            }
            else
            {
                hAll.Storage.Dispose();
                cAll.Storage.Dispose();
                savedAll.Storage.Dispose();
            }

            return new[] { packed };
        }
    }

    /// <summary>Backward для полной LSTM-последовательности — один Function на все T шагов.</summary>
    private sealed class GpuLstmSeqFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _xProj, _wHhT;
        private readonly Tensor _hAll, _cAll, _savedAll;
        private readonly int _T, _B, _H;
        private readonly bool _hasH0, _hasC0;
        public GpuLstmSeqFn(GpuOps ops, Tensor xProj, Tensor wHhT,
            Tensor hAll, Tensor cAll, Tensor savedAll,
            int t, int b, int h, bool hasH0, bool hasC0)
        {
            _ops = ops; _xProj = xProj; _wHhT = wHhT;
            _hAll = hAll; _cAll = cAll; _savedAll = savedAll;
            _T = t; _B = b; _H = h; _hasH0 = hasH0; _hasC0 = hasC0;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                if (gradOutput.Rank != 3 || gradOutput.Shape[0] != _T + 1
                    || gradOutput.Shape[1] != _B || gradOutput.Shape[2] != _H)
                    throw new InvalidOperationException(
                        $"GpuLstmSeqFn: gradOutput={gradOutput.Shape}, ожидалось ({_T + 1},{_B},{_H}).");
                var gOut = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var dev = gOut.Device;
                int H = _H, H4 = 4 * H;
                int planeBH = _B * H;
                int planeB4H = _B * H4;
                int planeSaved = 5 * _B * H;

                var dxProj = Tensor.Zeros(new Shape(_T, _B, H4), DType.Float32, dev);
                var dWhhT = Tensor.Zeros(new Shape(H, H4), DType.Float32, dev);
                Tensor dh0 = _hasH0 ? Tensor.Zeros(new Shape(_B, H), DType.Float32, dev) : null;
                Tensor dc0 = _hasC0 ? Tensor.Zeros(new Shape(_B, H), DType.Float32, dev) : null;

                var dh = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dc = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dPre = Tensor.Empty(new Shape(_B, H4), DType.Float32, dev);
                var dhPrev = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);
                var dCPrev = Tensor.Empty(new Shape(_B, H), DType.Float32, dev);

                var hAllV = ((CudaStorage)_hAll.Storage).AsView<float>();
                var cAllV = ((CudaStorage)_cAll.Storage).AsView<float>();
                var savedAllV = ((CudaStorage)_savedAll.Storage).AsView<float>();
                var gOutV = ViewOf(gOut);
                var dxProjV = ViewOf(dxProj);
                var dWhhTV = ViewOf(dWhhT);
                var wHhTV = ViewOf(_wHhT);
                var dhV = ViewOf(dh);
                var dcV = ViewOf(dc);
                var dPreV = ViewOf(dPre);
                var dhPrevV = ViewOf(dhPrev);
                var dCPrevV = ViewOf(dCPrev);

                dhV.CopyFrom(gOutV.SubView((long)(_T - 1) * planeBH, planeBH));
                dcV.CopyFrom(gOutV.SubView((long)_T * planeBH, planeBH));

                for (int t = _T - 1; t >= 0; t--)
                {
                    var hPrev = hAllV.SubView((long)t * planeBH, planeBH);
                    var cPrev = cAllV.SubView((long)t * planeBH, planeBH);
                    var savedT = savedAllV.SubView((long)t * planeSaved, planeSaved);
                    var dxProjT = dxProjV.SubView((long)t * planeB4H, planeB4H);

                    _ops._lstmStepBwd(planeBH,
                        dhV, dcV,
                        savedT, cPrev,
                        dPreV, dCPrevV,
                        H, planeBH);

                    dxProjT.CopyFrom(dPreV);
                    _ops.SgemmRm(hPrev, _B, H, true, dPreV, _B, H4, false,
                        1f, 1f, dWhhTV, H, H4);

                    if (t > 0)
                    {
                        _ops.SgemmRm(dPreV, _B, H4, false, wHhTV, H, H4, true,
                            1f, 0f, dhPrevV, _B, H);
                        _ops._add(planeBH,
                            gOutV.SubView((long)(t - 1) * planeBH, planeBH), dhPrevV, dhV);
                        dcV.CopyFrom(dCPrevV);
                    }
                    else
                    {
                        if (_hasH0)
                        {
                            _ops.SgemmRm(dPreV, _B, H4, false, wHhTV, H, H4, true,
                                1f, 0f, ViewOf(dh0), _B, H);
                        }
                        if (_hasC0)
                        {
                            ViewOf(dc0).CopyFrom(dCPrevV);
                        }
                    }
                }

                var grads = new System.Collections.Generic.List<Tensor>(4) { dxProj, dWhhT };
                if (_hasH0) grads.Add(dh0);
                if (_hasC0) grads.Add(dc0);
                return grads.ToArray();
            }
        }
    }

    #endregion LSTM full-sequence
}
