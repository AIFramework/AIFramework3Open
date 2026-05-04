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
    #region Sum / Softmax / LayerNorm GPU ops

    /// <summary>
    /// Native GPU Sum: либо по конкретной оси, либо по всему тензору.
    /// </summary>
    private Tensor[] SumOp(Tensor x, ReduceAttrs attrs)
    {
        if (x.DType != DType.Float32)
            throw new ArgumentException("SumOp(GPU): пока только Float32.");
        var xc = x.IsContiguous ? x : x.Contiguous();

        if (attrs.Axis is null)
        {
            var y = Tensor.Zeros(Shape.Scalar, x.DType, x.Device);
            int N = (int)xc.NumElements;
            if (xc.NumElements > int.MaxValue)
            {
                var cpu = xc.ToCpu();
                float total = 0f;
                var sp = cpu.AsReadOnlySpan<float>();
                for (int i = 0; i < sp.Length; i++) total += sp[i];
                var yh = Tensor.Empty(Shape.Scalar, x.DType, AI.ML.NeuralNetworks.V2.Device.Cpu);
                yh.AsSpan<float>()[0] = total;
                return new[] { yh.To(x.Device) };
            }
            _sumAxis(1, ViewOf(xc), ViewOf(y), N, 1);
            return new[] { y };
        }
        else
        {
            int axis = attrs.Axis.Value;
            int rank = xc.Rank;
            int dim = xc.Shape[axis];
            long outer = 1;
            for (int i = 0; i < axis; i++) outer *= xc.Shape[i];
            long inner = 1;
            for (int i = axis + 1; i < rank; i++) inner *= xc.Shape[i];
            if (outer * inner > int.MaxValue)
                throw new InvalidOperationException("SumOp(GPU): outer*inner overflow.");

            int[] outDims;
            if (attrs.KeepDim)
            {
                outDims = xc.Shape.ToArray();
                outDims[axis] = 1;
            }
            else
            {
                outDims = new int[rank - 1];
                for (int i = 0, j = 0; i < rank; i++) if (i != axis) outDims[j++] = xc.Shape[i];
            }
            var y = Tensor.Zeros(new Shape(outDims), x.DType, x.Device);
            _sumAxis((int)(outer * inner), ViewOf(xc), ViewOf(y), dim, (int)inner);
            return new[] { y };
        }
    }

    /// <summary>
    /// Native GPU softmax по нормализованной оси.
    /// </summary>
    private Tensor[] SoftmaxOp(Tensor x, SoftmaxAttrs attrs)
    {
        if (x.DType != DType.Float32)
            throw new ArgumentException("SoftmaxOp(GPU): пока только Float32.");
        var xc = x.IsContiguous ? x : x.Contiguous();
        var y = Tensor.Empty(xc.Shape, x.DType, x.Device);
        long groups = attrs.Outer * attrs.Inner;
        if (groups > int.MaxValue)
            throw new InvalidOperationException("SoftmaxOp(GPU): outer*inner overflow.");
        _softmaxFwd((int)groups, ViewOf(xc), ViewOf(y), attrs.Dim, (int)attrs.Inner);
        if (TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            var fn = new GpuSoftmaxFn(this, y, attrs.Dim, (int)attrs.Inner);
            fn.RegisterInput(x);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>Native GPU log-softmax: forward + backward без D2H.</summary>
    private Tensor[] LogSoftmaxOp(Tensor x, SoftmaxAttrs attrs)
    {
        if (x.DType != DType.Float32)
            throw new ArgumentException("LogSoftmaxOp(GPU): пока только Float32.");
        var xc = x.IsContiguous ? x : x.Contiguous();
        var y = Tensor.Empty(xc.Shape, x.DType, x.Device);
        long groups = attrs.Outer * attrs.Inner;
        if (groups > int.MaxValue)
            throw new InvalidOperationException("LogSoftmaxOp(GPU): outer*inner overflow.");
        _logSoftmaxFwd((int)groups, ViewOf(xc), ViewOf(y), attrs.Dim, (int)attrs.Inner);
        if (TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            var fn = new GpuLogSoftmaxFn(this, y, attrs.Dim, (int)attrs.Inner);
            fn.RegisterInput(x);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>
    /// Fused LayerNorm на GPU.
    /// </summary>
    private Tensor[] LayerNormOp(Tensor[] ins, LayerNorm.LayerNormAttrs attrs)
    {
        var x = ins[0];
        Tensor w = ins.Length > 1 ? ins[1] : null;
        Tensor b = ins.Length > 2 ? ins[2] : null;
        if (x.DType != DType.Float32 || (w != null && w.DType != DType.Float32) || (b != null && b.DType != DType.Float32))
            throw new ArgumentException("LayerNormOp(GPU): только Float32.");
        var xc = x.IsContiguous ? x : x.Contiguous();
        long batchesL = xc.NumElements / attrs.NormSize;
        if (batchesL > int.MaxValue) throw new OverflowException("LayerNorm(GPU): batches overflow.");
        int batches = (int)batchesL;
        bool hasAffine = w != null && b != null;

        var y = Tensor.Empty(xc.Shape, x.DType, x.Device);
        var mean = Tensor.Empty(new Shape(batches), x.DType, x.Device);
        var rstd = Tensor.Empty(new Shape(batches), x.DType, x.Device);

        var dummy = Tensor.Empty(new Shape(1), x.DType, x.Device);
        var wc = hasAffine ? (w.IsContiguous ? w : w.Contiguous()) : dummy;
        var bc = hasAffine ? (b.IsContiguous ? b : b.Contiguous()) : dummy;

        _layerNormFwd(batches,
            ViewOf(xc), ViewOf(wc), ViewOf(bc),
            ViewOf(y), ViewOf(mean), ViewOf(rstd),
            attrs.NormSize, attrs.Eps, hasAffine ? 1 : 0);

        bool needGrad = TapeContext.IsGradEnabled
            && (x.RequiresGrad || (w?.RequiresGrad ?? false) || (b?.RequiresGrad ?? false));
        if (needGrad)
        {
            var fn = new GpuLayerNormFn(this, x, w, b, mean, rstd, attrs.NormSize, hasAffine);
            fn.RegisterInput(x);
            if (w != null) fn.RegisterInput(w);
            if (b != null) fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>Backward softmax.</summary>
    private sealed class GpuSoftmaxFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _y;
        private readonly int _dim, _inner;
        public GpuSoftmaxFn(GpuOps ops, Tensor y, int dim, int inner)
        { _ops = ops; _y = y; _dim = dim; _inner = inner; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var yc = _y.IsContiguous ? _y : _y.Contiguous();
                var gx = Tensor.Empty(yc.Shape, yc.DType, yc.Device);
                long groups = (long)yc.NumElements / _dim;
                _ops._softmaxBwd((int)groups, ViewOf(yc), ViewOf(gy), ViewOf(gx), _dim, _inner);
                return new[] { gx };
            }
        }
    }

    /// <summary>Backward log-softmax.</summary>
    private sealed class GpuLogSoftmaxFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _y;
        private readonly int _dim, _inner;
        public GpuLogSoftmaxFn(GpuOps ops, Tensor y, int dim, int inner)
        { _ops = ops; _y = y; _dim = dim; _inner = inner; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var yc = _y.IsContiguous ? _y : _y.Contiguous();
                var gx = Tensor.Empty(yc.Shape, yc.DType, yc.Device);
                long groups = (long)yc.NumElements / _dim;
                _ops._logSoftmaxBwd((int)groups, ViewOf(yc), ViewOf(gy), ViewOf(gx), _dim, _inner);
                return new[] { gx };
            }
        }
    }

    /// <summary>Backward LayerNorm на GPU.</summary>
    private sealed class GpuLayerNormFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _x, _w, _b, _mean, _rstd;
        private readonly int _normSize;
        private readonly bool _hasAffine;
        public GpuLayerNormFn(GpuOps ops, Tensor x, Tensor w, Tensor b,
            Tensor mean, Tensor rstd, int normSize, bool hasAffine)
        {
            _ops = ops; _x = x; _w = w; _b = b;
            _mean = mean; _rstd = rstd;
            _normSize = normSize; _hasAffine = hasAffine;
        }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var xc = _x.IsContiguous ? _x : _x.Contiguous();
                long batchesL = xc.NumElements / _normSize;
                int batches = (int)batchesL;

                var dummyW = Tensor.Empty(new Shape(1), xc.DType, xc.Device);
                var wc = _hasAffine ? (_w.IsContiguous ? _w : _w.Contiguous()) : dummyW;

                var gx = Tensor.Empty(xc.Shape, xc.DType, xc.Device);
                _ops._layerNormBwdX(batches,
                    ViewOf(xc), ViewOf(wc), ViewOf(gy),
                    ViewOf(_mean), ViewOf(_rstd), ViewOf(gx),
                    _normSize, _hasAffine ? 1 : 0);

                Tensor gw = null, gb = null;
                if (_hasAffine && (_w.RequiresGrad || _b.RequiresGrad))
                {
                    gw = Tensor.Zeros(new Shape(_normSize), xc.DType, xc.Device);
                    gb = Tensor.Zeros(new Shape(_normSize), xc.DType, xc.Device);
                    _ops._layerNormBwdWB((int)xc.NumElements,
                        ViewOf(xc), ViewOf(gy),
                        ViewOf(_mean), ViewOf(_rstd),
                        ViewOf(gw), ViewOf(gb), _normSize);
                    if (_w.Rank != 1) gw = gw.Reshape(_w.Shape.ToArray());
                    if (_b.Rank != 1) gb = gb.Reshape(_b.Shape.ToArray());
                }

                if (_hasAffine)
                {
                    return new[]
                    {
                        _x.RequiresGrad ? gx : null,
                        _w.RequiresGrad ? gw : null,
                        _b.RequiresGrad ? gb : null,
                    };
                }
                return new[] { _x.RequiresGrad ? gx : null };
            }
        }
    }

    /// <summary>
    /// cuBLAS-путь GEMM, который ПИШЕТ результат в уже выделенный <paramref name="y"/>.
    /// </summary>
    internal void MatMulCuBlas(Tensor a, Tensor b, Tensor y)
    {
        if (a.DType != DType.Float32 || b.DType != DType.Float32 || y.DType != DType.Float32)
            throw new ArgumentException("MatMulCuBlas: поддерживается только Float32.");
        if (a.Rank != 2 || b.Rank != 2 || y.Rank != 2)
            throw new ArgumentException("MatMulCuBlas: ожидаются 2D-тензоры.");
        int M = a.Shape[0], K = a.Shape[1], N = b.Shape[1];
        if (b.Shape[0] != K) throw new ArgumentException($"MatMulCuBlas: K mismatch {a.Shape} × {b.Shape}.");
        if (y.Shape[0] != M || y.Shape[1] != N)
            throw new ArgumentException($"MatMulCuBlas: y.Shape {y.Shape} не совпадает с (M,N)=({M},{N}).");
        if (!a.IsContiguous || !b.IsContiguous || !y.IsContiguous)
            throw new ArgumentException("MatMulCuBlas: все операнды должны быть contiguous.");
        if (a.Device != b.Device || a.Device != y.Device)
            throw new ArgumentException($"MatMulCuBlas: устройства должны совпадать (a={a.Device}, b={b.Device}, y={y.Device}).");
        if (!_gpu.CuBlas.IsAvailable)
        {
            _gemm(new Index2D(M, N), ViewOf(a), ViewOf(b), ViewOf(y), M, N, K);
            return;
        }
        _gpu.CuBlas.Sgemm(
            CublasOp.N, CublasOp.N, N, M, K, 1f,
            ((CudaStorage)b.Storage).AsView<float>().SubView(b.Offset, (int)b.NumElements).GetDevicePointer(), N,
            ((CudaStorage)a.Storage).AsView<float>().SubView(a.Offset, (int)a.NumElements).GetDevicePointer(), K,
            0f,
            ((CudaStorage)y.Storage).AsView<float>().SubView(y.Offset, (int)y.NumElements).GetDevicePointer(), N);
    }
    #endregion Sum / Softmax / LayerNorm GPU ops
}
