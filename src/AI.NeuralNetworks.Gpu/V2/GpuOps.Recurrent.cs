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
    #region Cat / Scatter / Recurrent fused

    /// <summary>
    /// Native GPU Cat: аллоцируем dst contiguous, scatter каждый input в свой slice
    /// без D2H/H2D. Backward навешивается на уровне <see cref="IndexingOps.Cat"/>.
    /// </summary>
    private Tensor[] CatOp(Tensor[] ins, CatAttrs attrs)
    {
        if (ins == null || ins.Length == 0)
            throw new ArgumentException("CatOp(GPU): нет входов.");
        int axis = attrs.Axis;
        int rank = ins[0].Rank;
        var firstShape = ins[0].Shape;
        // Проверка совместимости форм (на всякий случай — IndexingOps.Cat уже проверял).
        int catSize = 0;
        for (int k = 0; k < ins.Length; k++)
        {
            var t = ins[k];
            if (t.Rank != rank) throw new ArgumentException("CatOp: рангу всех входов должны совпадать.");
            for (int j = 0; j < rank; j++)
                if (j != axis && t.Shape[j] != firstShape[j])
                    throw new ArgumentException($"CatOp: shape mismatch на оси {j}: {firstShape} vs {t.Shape}.");
            catSize += t.Shape[axis];
        }
        var outDims = firstShape.ToArray();
        outDims[axis] = catSize;
        var y = Tensor.Empty(new Shape(outDims), DType.Float32, ins[0].Device);

        // Compute dst strides (row-major contiguous).
        var dstStrides = new int[rank];
        long s = 1;
        for (int k = rank - 1; k >= 0; k--) { dstStrides[k] = (int)s; s *= outDims[k]; }

        const int MaxRank = 6;
        if (rank > MaxRank)
        {
            // Очень редкий случай — fallback на CPU.
            var cpuList = new Tensor[ins.Length];
            for (int k = 0; k < ins.Length; k++) cpuList[k] = ins[k].ToCpu();
            var yCpu = IndexingOps.Cat(cpuList, axis);
            return new[] { yCpu.To(ins[0].Device) };
        }

        int offsetAlongAxis = 0;
        var dstViewFull = ((CudaStorage)y.Storage).AsView<float>();
        for (int k = 0; k < ins.Length; k++)
        {
            var t = ins[k];
            var tc = t.IsContiguous ? t : t.Contiguous();
            int len = tc.Shape[axis];

            // Padding слева единицами до MaxRank (как и в ContiguousOp).
            var dims = new int[MaxRank];
            var ss = new int[MaxRank];
            for (int j = 0; j < MaxRank; j++) { dims[j] = 1; ss[j] = 0; }
            for (int j = 0; j < rank; j++)
            {
                dims[MaxRank - rank + j] = tc.Shape[j];
                ss[MaxRank - rank + j] = dstStrides[j];
            }
            int dstOffset = y.Offset + offsetAlongAxis * dstStrides[axis];

            var args = new V2Kernels.StridedCopyArgs
            {
                SrcOffset = dstOffset, // переинтерпретация: это offset в dst
                O0 = dims[0], O1 = dims[1], O2 = dims[2], O3 = dims[3], O4 = dims[4], O5 = dims[5],
                SS0 = ss[0], SS1 = ss[1], SS2 = ss[2], SS3 = ss[3], SS4 = ss[4], SS5 = ss[5],
            };
            _scatter((int)tc.NumElements, ViewOf(tc), dstViewFull, args);
            offsetAlongAxis += len;
        }
        return new[] { y };
    }

    /// <summary>
    /// In-place scatter <paramref name="src"/> в <paramref name="dst"/>[..., start:start+len, ...]
    /// вдоль <see cref="ScatterAttrs.Axis"/>. dst должен быть contiguous (gradient zeros).
    /// </summary>
    private Tensor[] ScatterSliceOp(Tensor dst, Tensor src, ScatterAttrs attrs)
    {
        if (dst.DType != DType.Float32 || src.DType != DType.Float32)
            throw new ArgumentException("ScatterSliceOp(GPU): только Float32.");
        if (!dst.IsContiguous)
            throw new ArgumentException("ScatterSliceOp(GPU): dst должен быть contiguous.");
        if (dst.Device != src.Device)
            throw new ArgumentException("ScatterSliceOp(GPU): dst и src должны быть на одном устройстве.");

        int rank = dst.Rank;
        const int MaxRank = 6;
        var srcC = src.IsContiguous ? src : src.Contiguous();
        // Размеры src должны совпадать с dst, кроме оси (там — attrs.Length).
        for (int j = 0; j < rank; j++)
        {
            int expected = j == attrs.Axis ? attrs.Length : dst.Shape[j];
            if (srcC.Shape[j] != expected)
                throw new ArgumentException(
                    $"ScatterSliceOp: src.Shape[{j}]={srcC.Shape[j]} != ожидаемого {expected} (dst.Shape={dst.Shape}, axis={attrs.Axis}, len={attrs.Length}).");
        }
        if (rank > MaxRank)
            throw new InvalidOperationException("ScatterSliceOp(GPU): rank > 6 не поддержан.");

        // dst row-major contiguous strides (от Shape).
        var dstStrides = new int[rank];
        long s = 1;
        for (int k = rank - 1; k >= 0; k--) { dstStrides[k] = (int)s; s *= dst.Shape[k]; }

        var dims = new int[MaxRank];
        var ss = new int[MaxRank];
        for (int j = 0; j < MaxRank; j++) { dims[j] = 1; ss[j] = 0; }
        for (int j = 0; j < rank; j++)
        {
            dims[MaxRank - rank + j] = srcC.Shape[j];
            ss[MaxRank - rank + j] = dstStrides[j];
        }
        int dstOffset = dst.Offset + attrs.Start * dstStrides[attrs.Axis];

        var args = new V2Kernels.StridedCopyArgs
        {
            SrcOffset = dstOffset,
            O0 = dims[0], O1 = dims[1], O2 = dims[2], O3 = dims[3], O4 = dims[4], O5 = dims[5],
            SS0 = ss[0], SS1 = ss[1], SS2 = ss[2], SS3 = ss[3], SS4 = ss[4], SS5 = ss[5],
        };
        _scatter((int)srcC.NumElements, ViewOf(srcC),
            ((CudaStorage)dst.Storage).AsView<float>(), args);
        return new[] { dst };
    }

    /// <summary>
    /// Native GPU LSTM step: один kernel-launch вычисляет (h_new, c_new) для всего
    /// батча. Backward хранит (i, f, g, o, tanh(c)) и cPrev и тоже идёт одним
    /// kernel-launch. Возвращает packed (2, B, H): [0]=h_new, [1]=c_new — symmetric
    /// с CPU-вариантом RecurrentFused.LstmStep.
    /// </summary>
    private Tensor[] LstmStepOp(Tensor preact, Tensor cPrev, LstmStepAttrs attrs)
    {
        if (preact.DType != DType.Float32 || cPrev.DType != DType.Float32)
            throw new ArgumentException("LstmStepOp(GPU): только Float32.");
        int B = attrs.B, H = attrs.H, H4 = 4 * H;
        if (preact.Rank != 2 || preact.Shape[0] != B || preact.Shape[1] != H4)
            throw new ArgumentException($"LstmStepOp: preact={preact.Shape}, ожидалось ({B},{H4}).");
        if (cPrev.Rank != 2 || cPrev.Shape[0] != B || cPrev.Shape[1] != H)
            throw new ArgumentException($"LstmStepOp: cPrev={cPrev.Shape}, ожидалось ({B},{H}).");

        var preC = preact.IsContiguous ? preact : preact.Contiguous();
        var cPC = cPrev.IsContiguous ? cPrev : cPrev.Contiguous();
        var packed = Tensor.Empty(new Shape(2, B, H), DType.Float32, preact.Device);
        int planeBH = B * H;

        bool needGrad = TapeContext.IsGradEnabled && (preact.RequiresGrad || cPrev.RequiresGrad);
        Tensor saved = needGrad
            ? Tensor.Empty(new Shape(5, B, H), DType.Float32, preact.Device)
            : Tensor.Empty(new Shape(1), DType.Float32, preact.Device); // dummy view

        var packedView = ViewOf(packed);
        var hOut = packedView.SubView(0, planeBH);
        var cOut = packedView.SubView(planeBH, planeBH);

        _lstmStepFwd(B * H,
            ViewOf(preC), ViewOf(cPC),
            hOut, cOut,
            ViewOf(saved),
            H, planeBH, needGrad ? 1 : 0);

        if (needGrad)
        {
            var fn = new GpuLstmStepFn(this, saved, cPC, B, H);
            fn.RegisterInput(preact);
            fn.RegisterInput(cPrev);
            packed.GradFn = fn;
        }
        return new[] { packed };
    }

    /// <summary>
    /// Native GPU GRU step: один kernel-launch вычисляет h_new для всего батча.
    /// Backward хранит (r, z, n, nh) и hPrev и тоже идёт одним kernel-launch.
    /// </summary>
    private Tensor[] GruStepOp(Tensor gx, Tensor gh, Tensor hPrev, GruStepAttrs attrs)
    {
        if (gx.DType != DType.Float32 || gh.DType != DType.Float32 || hPrev.DType != DType.Float32)
            throw new ArgumentException("GruStepOp(GPU): только Float32.");
        int B = attrs.B, H = attrs.H, H3 = 3 * H;
        if (gx.Shape[0] != B || gx.Shape[1] != H3 || gh.Shape[0] != B || gh.Shape[1] != H3)
            throw new ArgumentException($"GruStepOp: gx/gh должны быть ({B},{H3}), получено gx={gx.Shape}, gh={gh.Shape}.");
        if (hPrev.Shape[0] != B || hPrev.Shape[1] != H)
            throw new ArgumentException($"GruStepOp: hPrev={hPrev.Shape}, ожидалось ({B},{H}).");

        var gxC = gx.IsContiguous ? gx : gx.Contiguous();
        var ghC = gh.IsContiguous ? gh : gh.Contiguous();
        var hPC = hPrev.IsContiguous ? hPrev : hPrev.Contiguous();
        var hOut = Tensor.Empty(new Shape(B, H), DType.Float32, gx.Device);
        int planeBH = B * H;

        bool needGrad = TapeContext.IsGradEnabled
            && (gx.RequiresGrad || gh.RequiresGrad || hPrev.RequiresGrad);
        Tensor saved = needGrad
            ? Tensor.Empty(new Shape(4, B, H), DType.Float32, gx.Device)
            : Tensor.Empty(new Shape(1), DType.Float32, gx.Device);

        _gruStepFwd(B * H,
            ViewOf(gxC), ViewOf(ghC), ViewOf(hPC),
            ViewOf(hOut), ViewOf(saved),
            H, planeBH, needGrad ? 1 : 0);

        if (needGrad)
        {
            var fn = new GpuGruStepFn(this, saved, hPC, B, H);
            fn.RegisterInput(gx);
            fn.RegisterInput(gh);
            fn.RegisterInput(hPrev);
            hOut.GradFn = fn;
        }
        return new[] { hOut };
    }

    /// <summary>Backward fused LSTM step: один kernel-launch.</summary>
    private sealed class GpuLstmStepFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _saved, _cPrev;
        private readonly int _B, _H;
        public GpuLstmStepFn(GpuOps ops, Tensor saved, Tensor cPrev, int b, int h)
        { _ops = ops; _saved = saved; _cPrev = cPrev; _B = b; _H = h; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                if (gradOutput.Rank != 3 || gradOutput.Shape[0] != 2
                    || gradOutput.Shape[1] != _B || gradOutput.Shape[2] != _H)
                    throw new InvalidOperationException(
                        $"GpuLstmStepFn: неверная форма gradOutput {gradOutput.Shape}, ожидалось (2,{_B},{_H}).");
                var gOutC = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                int planeBH = _B * _H;
                int H4 = 4 * _H;
                var dPre = Tensor.Empty(new Shape(_B, H4), DType.Float32, gOutC.Device);
                var dCPrev = Tensor.Empty(new Shape(_B, _H), DType.Float32, gOutC.Device);

                var goView = ViewOf(gOutC);
                var dh = goView.SubView(0, planeBH);
                var dc = goView.SubView(planeBH, planeBH);

                _ops._lstmStepBwd(_B * _H,
                    dh, dc,
                    ViewOf(_saved), ViewOf(_cPrev),
                    ViewOf(dPre), ViewOf(dCPrev),
                    _H, planeBH);
                return new[] { dPre, dCPrev };
            }
        }
    }

    /// <summary>Backward fused GRU step: один kernel-launch.</summary>
    private sealed class GpuGruStepFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _saved, _hPrev;
        private readonly int _B, _H;
        public GpuGruStepFn(GpuOps ops, Tensor saved, Tensor hPrev, int b, int h)
        { _ops = ops; _saved = saved; _hPrev = hPrev; _B = b; _H = h; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                if (gradOutput.Rank != 2 || gradOutput.Shape[0] != _B || gradOutput.Shape[1] != _H)
                    throw new InvalidOperationException(
                        $"GpuGruStepFn: неверная форма gradOutput {gradOutput.Shape}, ожидалось ({_B},{_H}).");
                var gOutC = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                int planeBH = _B * _H;
                int H3 = 3 * _H;
                var dGx = Tensor.Empty(new Shape(_B, H3), DType.Float32, gOutC.Device);
                var dGh = Tensor.Empty(new Shape(_B, H3), DType.Float32, gOutC.Device);
                var dHPrev = Tensor.Empty(new Shape(_B, _H), DType.Float32, gOutC.Device);

                _ops._gruStepBwd(_B * _H,
                    ViewOf(gOutC), ViewOf(_saved), ViewOf(_hPrev),
                    ViewOf(dGx), ViewOf(dGh), ViewOf(dHPrev),
                    _H, planeBH);
                return new[] { dGx, dGh, dHPrev };
            }
        }
    }

    #endregion Cat / Scatter / Recurrent fused
}
