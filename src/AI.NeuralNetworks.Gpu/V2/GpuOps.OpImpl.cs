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
    #region Op implementations

    private Tensor[] UnaryOp(Tensor[] ins, Action<Index1D, ArrayView<float>, ArrayView<float>> kernel, OpCode op)
    {
        var x = ins[0];
        var xc = x.IsContiguous ? x : x.Contiguous();
        var y = Empty(x);
        kernel((int)y.NumElements, ViewOf(xc), ViewOf(y));
        if (TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            var fn = HasNativeUnaryBwd(op)
                ? (Function)new GpuUnaryFn(this, op, x, y)
                : new CpuFallbackUnaryFn(op, x);
            fn.RegisterInput(x);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    private static bool HasNativeUnaryBwd(OpCode op) => op switch
    {
        OpCode.Sigmoid or OpCode.Tanh or OpCode.Exp or OpCode.Sqrt
            or OpCode.Relu or OpCode.Neg or OpCode.Log or OpCode.Abs
            or OpCode.Sin or OpCode.Cos or OpCode.Silu or OpCode.Gelu => true,
        _ => false,
    };

    private Tensor[] BinaryOp(Tensor[] ins, Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> kernel, OpCode op)
    {
        var a = ins[0]; var b = ins[1];
        if (!a.Shape.Equals(b.Shape))
        {
            var y = TryBroadcastBinaryOp(a, b, op);
            if (y == null)
            {
                bool needGrad = TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad);
                Tensor yCpu;
                using (TapeContext.NoGrad())
                {
                    var aCpu = a.ToCpu();
                    var bCpu = b.ToCpu();
                    yCpu = op switch
                    {
                        OpCode.Add => TensorOps.Add(aCpu, bCpu),
                        OpCode.Sub => TensorOps.Sub(aCpu, bCpu),
                        OpCode.Mul => TensorOps.Mul(aCpu, bCpu),
                        OpCode.Div => TensorOps.Div(aCpu, bCpu),
                        OpCode.Pow => TensorOps.Pow(aCpu, bCpu),
                        _ => throw new NotSupportedException()
                    };
                }
                y = yCpu.To(a.Device);
            }
            if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
            {
                Function fn = op == OpCode.Add
                    ? new GpuBroadcastAddFn(a.Shape, b.Shape)
                    : (Function)new CpuFallbackBinaryFn(op, a, b);
                fn.RegisterInput(a);
                fn.RegisterInput(b);
                y.GradFn = fn;
            }
            return new[] { y };
        }
        var ac = a.IsContiguous ? a : a.Contiguous();
        var bc = b.IsContiguous ? b : b.Contiguous();
        var yEq = Empty(a);
        kernel((int)yEq.NumElements, ViewOf(ac), ViewOf(bc), ViewOf(yEq));
        if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
        {
            Function fn = op switch
            {
                OpCode.Add => new GpuAddFn(this),
                OpCode.Sub => new GpuSubFn(this),
                OpCode.Mul => new GpuMulFn(this, a, b),
                OpCode.Div => new GpuDivFn(this, a, b),
                _ => new CpuFallbackBinaryFn(op, a, b),
            };
            fn.RegisterInput(a);
            fn.RegisterInput(b);
            yEq.GradFn = fn;
        }
        return new[] { yEq };
    }

    /// <summary>
    /// GPU-broadcasting для бинарных op (rank ≤ 6). Возвращает null, если ранг
    /// больше или формы несовместимы.
    /// </summary>
    private Tensor TryBroadcastBinaryOp(Tensor a, Tensor b, OpCode op)
    {
        const int MaxRank = 6;
        int ra = a.Rank, rb = b.Rank;
        int rank = Math.Max(ra, rb);
        if (rank > MaxRank) return null;
        var outDims = new int[MaxRank];
        var aStrides = new int[MaxRank];
        var bStrides = new int[MaxRank];
        for (int i = 0; i < MaxRank; i++) outDims[i] = 1;
        for (int i = 0; i < MaxRank; i++) aStrides[i] = 0;
        for (int i = 0; i < MaxRank; i++) bStrides[i] = 0;

        for (int k = 0; k < rank; k++)
        {
            int dimA = k < ra ? a.Shape[ra - 1 - k] : 1;
            int dimB = k < rb ? b.Shape[rb - 1 - k] : 1;
            int strA = k < ra ? a.Strides[ra - 1 - k] : 0;
            int strB = k < rb ? b.Strides[rb - 1 - k] : 0;
            int dimO;
            if (dimA == dimB) { dimO = dimA; }
            else if (dimA == 1) { dimO = dimB; strA = 0; }
            else if (dimB == 1) { dimO = dimA; strB = 0; }
            else return null;
            outDims[MaxRank - 1 - k] = dimO;
            aStrides[MaxRank - 1 - k] = strA;
            bStrides[MaxRank - 1 - k] = strB;
        }

        long total = 1;
        for (int i = 0; i < MaxRank; i++) total *= outDims[i];
        if (total > int.MaxValue) return null;

        var trimmedDims = new int[rank];
        for (int i = 0; i < rank; i++) trimmedDims[i] = outDims[MaxRank - rank + i];
        var y = Tensor.Zeros(new Shape(trimmedDims), DType.Float32, a.Device);

        int opCode = op switch
        {
            OpCode.Add => 0, OpCode.Sub => 1, OpCode.Mul => 2, OpCode.Div => 3, OpCode.Pow => 4,
            _ => -1,
        };
        if (opCode < 0) return null;

        var aViewFull = ((CudaStorage)a.Storage).AsView<float>();
        var bViewFull = ((CudaStorage)b.Storage).AsView<float>();
        var yView = ViewOf(y);
        var args = new V2Kernels.BroadcastArgs
        {
            Op = opCode,
            AOffset = a.Offset,
            BOffset = b.Offset,
            O0 = outDims[0], O1 = outDims[1], O2 = outDims[2], O3 = outDims[3], O4 = outDims[4], O5 = outDims[5],
            SA0 = aStrides[0], SA1 = aStrides[1], SA2 = aStrides[2], SA3 = aStrides[3], SA4 = aStrides[4], SA5 = aStrides[5],
            SB0 = bStrides[0], SB1 = bStrides[1], SB2 = bStrides[2], SB3 = bStrides[3], SB4 = bStrides[4], SB5 = bStrides[5],
        };
        _bcast((int)total, aViewFull, bViewFull, yView, args);
        return y;
    }

    private Tensor[] MatMulOp(Tensor a, Tensor b)
    {
        int M = a.Shape[0], K = a.Shape[1], N = b.Shape[1];
        if (b.Shape[0] != K) throw new ArgumentException($"MatMul: K mismatch {a.Shape} × {b.Shape}.");
        var ac = a.IsContiguous ? a : a.Contiguous();
        var bc = b.IsContiguous ? b : b.Contiguous();
        var y = Tensor.Zeros(new Shape(M, N), a.DType, a.Device);
        if (_gpu.CuBlas.IsAvailable)
        {
            _gpu.CuBlas.Sgemm(
                CublasOp.N, CublasOp.N,
                N, M, K,
                1f,
                ((CudaStorage)bc.Storage).AsView<float>().SubView(bc.Offset, (int)bc.NumElements).GetDevicePointer(), N,
                ((CudaStorage)ac.Storage).AsView<float>().SubView(ac.Offset, (int)ac.NumElements).GetDevicePointer(), K,
                0f,
                ((CudaStorage)y.Storage).AsView<float>().SubView(y.Offset, (int)y.NumElements).GetDevicePointer(), N);
        }
        else
        {
            _gemm(new Index2D(M, N), ViewOf(ac), ViewOf(bc), ViewOf(y), M, N, K);
        }
        if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
        {
            var fn = new MatMulGpuFn(this, a, b);
            fn.RegisterInput(a);
            fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>Внутренний MatMul без autograd-записи — для использования в backward.</summary>
    internal Tensor MatMulRaw(Tensor a, Tensor b)
    {
        int M = a.Shape[0], K = a.Shape[1], N = b.Shape[1];
        var ac = a.IsContiguous ? a : a.Contiguous();
        var bc = b.IsContiguous ? b : b.Contiguous();
        var y = Tensor.Zeros(new Shape(M, N), a.DType, a.Device);
        using (TapeContext.NoGrad())
        {
            if (_gpu.CuBlas.IsAvailable)
            {
                _gpu.CuBlas.Sgemm(
                    CublasOp.N, CublasOp.N, N, M, K, 1f,
                    ((CudaStorage)bc.Storage).AsView<float>().SubView(bc.Offset, (int)bc.NumElements).GetDevicePointer(), N,
                    ((CudaStorage)ac.Storage).AsView<float>().SubView(ac.Offset, (int)ac.NumElements).GetDevicePointer(), K,
                    0f,
                    ((CudaStorage)y.Storage).AsView<float>().SubView(y.Offset, (int)y.NumElements).GetDevicePointer(), N);
            }
            else
            {
                _gemm(new Index2D(M, N), ViewOf(ac), ViewOf(bc), ViewOf(y), M, N, K);
            }
        }
        return y;
    }

    private Tensor[] BatchedMatMulOp(Tensor a, Tensor b)
    {
        int B = a.Shape[0], M = a.Shape[1], K = a.Shape[2];
        if (b.Shape[0] != B || b.Shape[1] != K)
            throw new ArgumentException($"BMM: shapes {a.Shape} × {b.Shape}.");
        int N = b.Shape[2];
        var ac = a.IsContiguous ? a : a.Contiguous();
        var bc = b.IsContiguous ? b : b.Contiguous();
        var y = Tensor.Zeros(new Shape(B, M, N), a.DType, a.Device);

        if (_gpu.CuBlas.IsAvailable)
        {
            long strideA = (long)M * K;
            long strideB = (long)K * N;
            long strideC = (long)M * N;
            _gpu.CuBlas.SgemmStridedBatched(
                CublasOp.N, CublasOp.N,
                N, M, K,
                1f,
                ((CudaStorage)bc.Storage).AsView<float>().SubView(bc.Offset, (int)bc.NumElements).GetDevicePointer(), N, strideB,
                ((CudaStorage)ac.Storage).AsView<float>().SubView(ac.Offset, (int)ac.NumElements).GetDevicePointer(), K, strideA,
                0f,
                ((CudaStorage)y.Storage).AsView<float>().SubView(y.Offset, (int)y.NumElements).GetDevicePointer(), N, strideC,
                B);
        }
        else
        {
            _bgemm(new Index3D(B, M, N), ViewOf(ac), ViewOf(bc), ViewOf(y), B, M, N, K);
        }

        if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
        {
            var fn = new GpuBmmFn(this, a, b);
            fn.RegisterInput(a);
            fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return new[] { y };
    }

    /// <summary>
    /// Internal batched gemm: <c>C[b] = A[b]^[T?] · B[b]^[T?]</c>.
    /// </summary>
    internal void BmmRaw(Tensor A, bool transA, Tensor B, bool transB, Tensor C)
    {
        if (A.DType != DType.Float32 || B.DType != DType.Float32 || C.DType != DType.Float32)
            throw new ArgumentException("BmmRaw: только Float32.");
        if (A.Rank != 3 || B.Rank != 3 || C.Rank != 3)
            throw new ArgumentException("BmmRaw: ожидаются 3D-тензоры.");
        if (!A.IsContiguous || !B.IsContiguous || !C.IsContiguous)
            throw new ArgumentException("BmmRaw: все операнды должны быть contiguous.");
        int B_ = A.Shape[0];
        if (B.Shape[0] != B_ || C.Shape[0] != B_)
            throw new ArgumentException("BmmRaw: размеры батчей должны совпадать.");

        int M = transA ? A.Shape[2] : A.Shape[1];
        int K = transA ? A.Shape[1] : A.Shape[2];
        int Kb = transB ? B.Shape[2] : B.Shape[1];
        int N = transB ? B.Shape[1] : B.Shape[2];
        if (K != Kb) throw new ArgumentException($"BmmRaw: K mismatch ({K} vs {Kb}).");
        if (C.Shape[1] != M || C.Shape[2] != N)
            throw new ArgumentException($"BmmRaw: shape C={C.Shape} не совпадает с (B,{M},{N}).");

        long strideA = (long)A.Shape[1] * A.Shape[2];
        long strideB = (long)B.Shape[1] * B.Shape[2];
        long strideC = (long)M * N;

        if (_gpu.CuBlas.IsAvailable)
        {
            int lda = A.Shape[2];
            int ldb = B.Shape[2];
            int ldc = N;

            CublasOp opA_cu = transB ? CublasOp.T : CublasOp.N;
            CublasOp opB_cu = transA ? CublasOp.T : CublasOp.N;

            _gpu.CuBlas.SgemmStridedBatched(
                opA_cu, opB_cu,
                N, M, K,
                1f,
                ((CudaStorage)B.Storage).AsView<float>().SubView(B.Offset, (int)B.NumElements).GetDevicePointer(), ldb, strideB,
                ((CudaStorage)A.Storage).AsView<float>().SubView(A.Offset, (int)A.NumElements).GetDevicePointer(), lda, strideA,
                0f,
                ((CudaStorage)C.Storage).AsView<float>().SubView(C.Offset, (int)C.NumElements).GetDevicePointer(), ldc, strideC,
                B_);
            return;
        }

        var Aeff = transA ? A.Permute(0, 2, 1).Contiguous() : A;
        var Beff = transB ? B.Permute(0, 2, 1).Contiguous() : B;
        _bgemm(new Index3D(B_, M, N), ViewOf(Aeff), ViewOf(Beff), ViewOf(C), B_, M, N, K);
    }

    #endregion Op implementations
}
