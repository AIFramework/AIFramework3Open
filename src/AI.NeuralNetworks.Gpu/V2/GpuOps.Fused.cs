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
    #region Fused operations

    /// <summary>In-place AdamW шаг на GPU. inputs: [p, g, m, v]. Возвращает [p].</summary>
    private Tensor[] FusedAdamWOp(Tensor[] ins, FusedAdamWAttrs a)
    {
        var p = ins[0]; var g = ins[1]; var m = ins[2]; var v = ins[3];
        if (p.DType != DType.Float32 || g.DType != DType.Float32
            || m.DType != DType.Float32 || v.DType != DType.Float32)
            throw new ArgumentException("FusedAdamW: поддерживается только Float32.");
        if (!p.Shape.Equals(g.Shape) || !p.Shape.Equals(m.Shape) || !p.Shape.Equals(v.Shape))
            throw new ArgumentException("FusedAdamW: формы p,g,m,v должны совпадать.");
        if (!p.IsContiguous || !g.IsContiguous || !m.IsContiguous || !v.IsContiguous)
            throw new ArgumentException("FusedAdamW: все операнды должны быть contiguous.");
        if (p.Device != g.Device || p.Device != m.Device || p.Device != v.Device)
            throw new ArgumentException("FusedAdamW: устройства должны совпадать.");

        _adamW((int)p.NumElements,
            ViewOf(p), ViewOf(g), ViewOf(m), ViewOf(v),
            a.Lr, a.Beta1, a.Beta2, a.Eps, a.WeightDecay, a.Bc1, a.Bc2);
        return new[] { p };
    }

    /// <summary>Forward y = gelu(x · W^T + b). inputs: [x:[M,K], W:[N,K], b:[N]].</summary>
    private Tensor[] FusedLinearGeluOp(Tensor[] ins)
    {
        var x = ins[0]; var W = ins[1]; var b = ins[2];
        if (x.DType != DType.Float32 || W.DType != DType.Float32 || b.DType != DType.Float32)
            throw new ArgumentException("FusedLinearGelu: только Float32.");
        if (x.Rank != 2 || W.Rank != 2 || b.Rank != 1)
            throw new ArgumentException("FusedLinearGelu: ожидается x:[M,K], W:[N,K], b:[N].");
        int M = x.Shape[0], K = x.Shape[1];
        int N = W.Shape[0];
        if (W.Shape[1] != K) throw new ArgumentException($"FusedLinearGelu: K mismatch x={x.Shape}, W={W.Shape}.");
        if (b.Shape[0] != N) throw new ArgumentException($"FusedLinearGelu: bias.Shape={b.Shape}, N={N}.");

        var xc = x.IsContiguous ? x : x.Contiguous();
        var Wc = W.IsContiguous ? W : W.Contiguous();
        var bc = b.IsContiguous ? b : b.Contiguous();
        var y = Tensor.Zeros(new Shape(M, N), DType.Float32, x.Device);

        _linearGelu(new Index2D(M, N), ViewOf(xc), ViewOf(Wc), ViewOf(bc), ViewOf(y), M, N, K);
        if (TapeContext.IsGradEnabled && (x.RequiresGrad || W.RequiresGrad || b.RequiresGrad))
        {
            var fn = new CpuFallbackLinearGeluFn(x, W, b);
            fn.RegisterInput(x); fn.RegisterInput(W); fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>Forward y = relu(x + bias_broadcast). inputs: [x, bias].</summary>
    private Tensor[] FusedAddBiasReluOp(Tensor[] ins)
    {
        var x = ins[0]; var bias = ins[1];
        if (x.DType != DType.Float32 || bias.DType != DType.Float32)
            throw new ArgumentException("FusedAddBiasRelu: только Float32.");
        if (bias.Rank != 1) throw new ArgumentException("FusedAddBiasRelu: bias должен быть 1D.");
        int stride = bias.Shape[0];
        FusedKernels.EnsureValidStride(stride);
        if (x.NumElements % stride != 0)
            throw new ArgumentException($"FusedAddBiasRelu: x.NumElements={x.NumElements} не кратно bias.Shape={stride}.");

        var xc = x.IsContiguous ? x : x.Contiguous();
        var biasC = bias.IsContiguous ? bias : bias.Contiguous();
        var y = Tensor.Zeros(x.Shape, DType.Float32, x.Device);
        _addBiasRelu((int)x.NumElements, ViewOf(xc), ViewOf(biasC), ViewOf(y), stride);
        if (TapeContext.IsGradEnabled && (x.RequiresGrad || bias.RequiresGrad))
        {
            var fn = new CpuFallbackAddBiasReluFn(x, bias);
            fn.RegisterInput(x); fn.RegisterInput(bias);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>
    /// Native contiguous-копия для GPU (rank ≤ 6) одним ILGPU-проходом — без D2H/H2D
    /// round-trip.
    /// </summary>
    private Tensor[] ContiguousOp(Tensor x)
    {
        const int MaxRank = 6;
        if (x.DType != DType.Float32)
            throw new ArgumentException("ContiguousOp(GPU): пока только Float32.");
        if (x.IsContiguous)
            return new[] { x };
        int rank = x.Rank;
        if (rank == 0)
            return new[] { x };
        if (rank > MaxRank)
        {
            return new[] { x.ToCpu().Contiguous().To(x.Device) };
        }

        var dims = new int[MaxRank];
        var strides = new int[MaxRank];
        for (int i = 0; i < MaxRank; i++) { dims[i] = 1; strides[i] = 0; }
        for (int k = 0; k < rank; k++)
        {
            dims[MaxRank - rank + k] = x.Shape[k];
            strides[MaxRank - rank + k] = x.Strides[k];
        }

        var y = Tensor.Empty(x.Shape, x.DType, x.Device);
        var srcView = ((CudaStorage)x.Storage).AsView<float>();
        var dstView = ViewOf(y);
        var args = new V2Kernels.StridedCopyArgs
        {
            SrcOffset = x.Offset,
            O0 = dims[0], O1 = dims[1], O2 = dims[2], O3 = dims[3], O4 = dims[4], O5 = dims[5],
            SS0 = strides[0], SS1 = strides[1], SS2 = strides[2], SS3 = strides[3], SS4 = strides[4], SS5 = strides[5],
        };
        _contig((int)x.NumElements, srcView, dstView, args);
        return new[] { y };
    }

    #endregion Fused operations
}
