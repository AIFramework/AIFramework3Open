using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

public static partial class TensorOps
{
    #region Matmul

    /// <summary>
    /// Матричное умножение. Поддерживаются 2D × 2D (M×K * K×N = M×N) и 3D batched
    /// (B×M×K * B×K×N или B×M×K * K×N с broadcast батча).
    /// </summary>
    public static Tensor MatMul(Tensor a, Tensor b)
    {
        if (a.DType != b.DType)
            throw new ArgumentException(
                $"MatMul: dtype mismatch ({a.DType} vs {b.DType}).");
        EnsureSameDevice(a, b, "MatMul");
        // Device dispatch — для не-CPU устройств берём kernel из реестра.
        if (a.Device.Type != DeviceType.Cpu)
        {
            var code = a.Rank == 3 && b.Rank == 3 ? OpCode.BatchedMatMul : OpCode.MatMul;
            var k = OpRegistry.TryGet(code, a.DType, a.Device);
            if (k != null) return k(new[] { a, b }, null)[0];
        }
        if (a.Rank == 2 && b.Rank == 2)
            return MatMul2D(a, b);
        if (a.Rank == 3 && b.Rank == 3)
            return BatchedMatMul(a, b);
        if (a.Rank == 3 && b.Rank == 2)
        {
            // Развернём batch как (B*M, K) × (K, N) -> (B*M, N) -> (B, M, N).
            int B = a.Shape[0], M = a.Shape[1], K = a.Shape[2];
            var aReshape = a.Reshape(B * M, K);
            var y = MatMul2D(aReshape, b);
            return y.Reshape(B, M, b.Shape[1]);
        }
        throw new NotSupportedException(
            $"MatMul: неподдерживаемые формы {a.Shape} × {b.Shape}.");
    }

    /// <summary>
    /// True if t is a simple Transpose(0,1) of a contiguous 2D tensor.
    /// Allows passing raw storage + transA/transB to BLAS, avoiding Contiguous() copy.
    /// </summary>
    private static bool IsTranspose2D(Tensor t)
        => t.Rank == 2 && !t.IsContiguous
           && t.Strides[0] == 1 && t.Strides[1] == t.Shape[0];

    /// <summary>
    /// True if t is Permute(0,2,1) of a contiguous 3D tensor (batched transpose).
    /// </summary>
    private static bool IsBatchedTranspose(Tensor t)
        => t.Rank == 3 && !t.IsContiguous
           && t.Strides[0] == t.Shape[1] * t.Shape[2]
           && t.Strides[1] == 1 && t.Strides[2] == t.Shape[1];

    private static ReadOnlySpan<float> RawSpan(Tensor t)
        => t.Storage.AsReadOnlySpan<float>().Slice(t.Offset, (int)t.NumElements);

    private static Tensor MatMul2D(Tensor a, Tensor b)
    {
        int M = a.Shape[0], K = a.Shape[1];
        if (b.Shape[0] != K)
            throw new ArgumentException($"MatMul: K-мерности не совпадают ({a.Shape}, {b.Shape}).");
        int N = b.Shape[1];

        var y = Tensor.Zeros(new Shape(M, N), a.DType, a.Device);
        var yS = y.AsSpan<float>();

        bool tA = IsTranspose2D(a);
        bool tB = IsTranspose2D(b);

        if ((tA || tB) && CpuBlas.ShouldUseBlas(M, N, K))
        {
            var ac = tA ? a : a.Contiguous();
            var bc = tB ? b : b.Contiguous();
            var aSpan = tA ? RawSpan(a) : ac.AsReadOnlySpan<float>();
            var bSpan = tB ? RawSpan(b) : bc.AsReadOnlySpan<float>();
            CpuBlas.Sgemm(aSpan, bSpan, yS, M, N, K, tA, tB);
        }
        else
        {
            var ac = a.Contiguous();
            var bc = b.Contiguous();
            var aSpan = ac.AsReadOnlySpan<float>();
            var bSpan = bc.AsReadOnlySpan<float>();

            if (CpuBlas.ShouldUseBlas(M, N, K))
            {
                CpuBlas.Sgemm(aSpan, bSpan, yS, M, N, K);
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRow = i * K;
                    int yRow = i * N;
                    for (int k = 0; k < K; k++)
                    {
                        float aik = aSpan[aRow + k];
                        int bRow = k * N;
                        for (int j = 0; j < N; j++)
                            yS[yRow + j] += aik * bSpan[bRow + j];
                    }
                }
            }
        }

        if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
        {
            var fn = new MatMulFunction(a, b);
            fn.RegisterInput(a);
            fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return y;
    }

    private static Tensor BatchedMatMul(Tensor a, Tensor b)
    {
        int B = a.Shape[0];
        if (b.Shape[0] != B)
            throw new ArgumentException("BatchedMatMul: размеры батчей не совпадают.");
        int M = a.Shape[1], K = a.Shape[2];
        if (b.Shape[1] != K)
            throw new ArgumentException("BatchedMatMul: K-мерности не совпадают.");
        int N = b.Shape[2];

        var y = Tensor.Zeros(new Shape(B, M, N), a.DType, a.Device);
        var yS = y.AsSpan<float>();

        bool useBlas = CpuBlas.ShouldUseBlas(M, N, K);
        bool tA = useBlas && IsBatchedTranspose(a);
        bool tB = useBlas && IsBatchedTranspose(b);

        ReadOnlySpan<float> aSp, bSp;
        Tensor acKeep = null, bcKeep = null;
        if (tA || tB)
        {
            if (!tA) { acKeep = a.Contiguous(); aSp = acKeep.AsReadOnlySpan<float>(); }
            else aSp = RawSpan(a);
            if (!tB) { bcKeep = b.Contiguous(); bSp = bcKeep.AsReadOnlySpan<float>(); }
            else bSp = RawSpan(b);
        }
        else
        {
            acKeep = a.Contiguous(); bcKeep = b.Contiguous();
            aSp = acKeep.AsReadOnlySpan<float>();
            bSp = bcKeep.AsReadOnlySpan<float>();
        }

        int aBatch = M * K, bBatch = K * N, yBatch = M * N;
        for (int p = 0; p < B; p++)
        {
            int aBase = p * aBatch;
            int bBase = p * bBatch;
            int yBase = p * yBatch;
            if (useBlas)
            {
                CpuBlas.Sgemm(
                    aSp.Slice(aBase, aBatch),
                    bSp.Slice(bBase, bBatch),
                    yS.Slice(yBase, yBatch),
                    M, N, K, tA, tB);
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRow = aBase + i * K;
                    int yRow = yBase + i * N;
                    for (int k = 0; k < K; k++)
                    {
                        float aik = aSp[aRow + k];
                        int bRow = bBase + k * N;
                        for (int j = 0; j < N; j++)
                            yS[yRow + j] += aik * bSp[bRow + j];
                    }
                }
            }
        }
        GC.KeepAlive(acKeep); GC.KeepAlive(bcKeep);

        if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
        {
            var fn = new BatchedMatMulFunction(a, b);
            fn.RegisterInput(a);
            fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class MatMulFunction : Function
    {
        private readonly Tensor _a, _b;
        public MatMulFunction(Tensor a, Tensor b) { _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            // dA = gy @ B^T; dB = A^T @ gy
            using (TapeContext.NoGrad())
            {
                Tensor da = null, db = null;
                if (_a.RequiresGrad)
                    da = MatMul2DRaw(gradOutput, _b.Transpose(0, 1));
                if (_b.RequiresGrad)
                    db = MatMul2DRaw(_a.Transpose(0, 1), gradOutput);
                return new[] { da, db };
            }
        }
    }

    private sealed class BatchedMatMulFunction : Function
    {
        private readonly Tensor _a, _b;
        public BatchedMatMulFunction(Tensor a, Tensor b) { _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                Tensor da = null, db = null;
                if (_a.RequiresGrad)
                    da = BatchedMatMulRaw(gradOutput, _b.Permute(0, 2, 1));
                if (_b.RequiresGrad)
                    db = BatchedMatMulRaw(_a.Permute(0, 2, 1), gradOutput);
                return new[] { da, db };
            }
        }
    }

    /// <summary>Прямой 2D-matmul без autograd-записи.</summary>
    internal static Tensor MatMul2DRaw(Tensor a, Tensor b)
    {
        int M = a.Shape[0], K = a.Shape[1], N = b.Shape[1];
        var y = Tensor.Zeros(new Shape(M, N), a.DType, a.Device);
        var yS = y.AsSpan<float>();

        bool tA = IsTranspose2D(a);
        bool tB = IsTranspose2D(b);

        if ((tA || tB) && CpuBlas.ShouldUseBlas(M, N, K))
        {
            var ac = tA ? a : a.Contiguous();
            var bc = tB ? b : b.Contiguous();
            var aSpan = tA ? RawSpan(a) : ac.AsReadOnlySpan<float>();
            var bSpan = tB ? RawSpan(b) : bc.AsReadOnlySpan<float>();
            CpuBlas.Sgemm(aSpan, bSpan, yS, M, N, K, tA, tB);
        }
        else
        {
            var ac = a.Contiguous(); var bc = b.Contiguous();
            var aSpan = ac.AsReadOnlySpan<float>();
            var bSpan = bc.AsReadOnlySpan<float>();
            if (CpuBlas.ShouldUseBlas(M, N, K))
            {
                CpuBlas.Sgemm(aSpan, bSpan, yS, M, N, K);
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRow = i * K, yRow = i * N;
                    for (int k = 0; k < K; k++)
                    {
                        float aik = aSpan[aRow + k];
                        int bRow = k * N;
                        for (int j = 0; j < N; j++) yS[yRow + j] += aik * bSpan[bRow + j];
                    }
                }
            }
        }
        return y;
    }

    /// <summary>Прямой batched-matmul без autograd-записи.</summary>
    internal static Tensor BatchedMatMulRaw(Tensor a, Tensor b)
    {
        int B = a.Shape[0], M = a.Shape[1], K = a.Shape[2], N = b.Shape[2];
        var y = Tensor.Zeros(new Shape(B, M, N), a.DType, a.Device);
        var yS = y.AsSpan<float>();

        bool useBlas = CpuBlas.ShouldUseBlas(M, N, K);
        bool tA = useBlas && IsBatchedTranspose(a);
        bool tB = useBlas && IsBatchedTranspose(b);

        ReadOnlySpan<float> aSp, bSp;
        Tensor acKeep = null, bcKeep = null;
        if (tA || tB)
        {
            if (!tA) { acKeep = a.Contiguous(); aSp = acKeep.AsReadOnlySpan<float>(); }
            else aSp = RawSpan(a);
            if (!tB) { bcKeep = b.Contiguous(); bSp = bcKeep.AsReadOnlySpan<float>(); }
            else bSp = RawSpan(b);
        }
        else
        {
            acKeep = a.Contiguous(); bcKeep = b.Contiguous();
            aSp = acKeep.AsReadOnlySpan<float>();
            bSp = bcKeep.AsReadOnlySpan<float>();
        }
        int aBatch = M * K, bBatch = K * N, yBatch = M * N;
        for (int p = 0; p < B; p++)
        {
            int aBase = p * aBatch, bBase = p * bBatch, yBase = p * yBatch;
            if (useBlas)
            {
                CpuBlas.Sgemm(
                    aSp.Slice(aBase, aBatch),
                    bSp.Slice(bBase, bBatch),
                    yS.Slice(yBase, yBatch),
                    M, N, K, tA, tB);
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRow = aBase + i * K, yRow = yBase + i * N;
                    for (int k = 0; k < K; k++)
                    {
                        float aik = aSp[aRow + k];
                        int bRow = bBase + k * N;
                        for (int j = 0; j < N; j++) yS[yRow + j] += aik * bSp[bRow + j];
                    }
                }
            }
        }
        GC.KeepAlive(acKeep); GC.KeepAlive(bcKeep);
        return y;
    }

    #endregion Matmul
}
