using ILGPU.Runtime.Cuda;
using System;
using System.Runtime.InteropServices;

namespace AI.ML.NeuralNetworks.Gpu.CuBlas;

/// <summary>
/// Lightweight P/Invoke wrapper around cuBLAS for SGEMM/SGEMV.
/// Graceful fallback: if cublas64_*.dll is not found, <see cref="IsAvailable"/> returns false
/// and all operations fall back to custom ILGPU kernels.
/// </summary>
/// <remarks>
/// <para>
/// При попытке загрузить cuBLAS DLL сначала пробуется набор имён для разных
/// версий CUDA (12/11/10), чтобы поддержать смешанные окружения. Если ни одна
/// не найдена — <see cref="IsAvailable"/> возвращает false и весь GEMM
/// прозрачно идёт через ILGPU-fallback.
/// </para>
/// <para>
/// Все вызовы cuBLAS проверяют возвращённый <c>cublasStatus_t</c>; ненулевой
/// статус транслируется в <see cref="CuBlasException"/> с человеческим описанием.
/// </para>
/// </remarks>
internal sealed class CuBlasHandle : IDisposable
{
    private IntPtr _handle;
    private readonly ICuBlasBindings _bindings;

    public bool IsAvailable { get; }

    public CuBlasHandle()
    {
        // Перебираем известные имена DLL в порядке предпочтения. Первое успешное
        // создание handle принимаем за рабочий backend.
        foreach (var name in CuBlasBindings.CandidateNames)
        {
            try
            {
                var b = CuBlasBindings.For(name);
                int status = b.cublasCreate_v2(out _handle);
                if (status == 0 && _handle != IntPtr.Zero)
                {
                    _bindings = b;
                    IsAvailable = true;
                    // По умолчанию cuBLAS на Ampere+ (CC 8.0+) использует TF32 для SGEMM
                    // (CUBLAS_DEFAULT_MATH ⇒ allow TF32). Это снижает мантиссу до 10 бит
                    // и может давать ~5% ошибку в backward через несколько шагов RNN/LSTM/GRU.
                    // Принудительно включаем CUBLAS_PEDANTIC_MATH (mode=2) для чистого FP32.
                    // Можно отключить через AI_GPU_ALLOW_TF32=1 (быстрее, но менее точно).
                    if (Environment.GetEnvironmentVariable("AI_GPU_ALLOW_TF32") != "1")
                    {
                        try { _bindings.cublasSetMathMode(_handle, 2); } catch { /* старые версии */ }
                    }
                    return;
                }
            }
            catch (DllNotFoundException) { /* try next */ }
            catch (EntryPointNotFoundException) { /* try next */ }
            catch (BadImageFormatException) { /* try next */ }
        }
        IsAvailable = false;
    }

    public void SetStream(CudaStream stream)
    {
        if (!IsAvailable) return;
        Check(_bindings.cublasSetStream_v2(_handle, stream.StreamPtr), "cublasSetStream");
    }

    /// <summary>
    /// SGEMM: C = alpha * op(A) * op(B) + beta * C.
    /// All pointers are device pointers from ILGPU ArrayView.
    /// Row-major trick: to compute C = A*B in row-major,
    /// call cuBLAS as C^T = B^T * A^T, i.e. swap A/B and use column-major layout.
    /// </summary>
    public unsafe void Sgemm(
        CublasOp transA, CublasOp transB,
        int m, int n, int k,
        float alpha,
        IntPtr A, int lda,
        IntPtr B, int ldb,
        float beta,
        IntPtr C, int ldc)
    {
        if (!IsAvailable) return;
        int s = _bindings.cublasSgemm_v2(_handle, (int)transA, (int)transB, m, n, k,
            &alpha, A, lda, B, ldb, &beta, C, ldc);
        Check(s, "cublasSgemm");
    }

    /// <summary>
    /// SGEMV: y = alpha * op(A) * x + beta * y.
    /// </summary>
    public unsafe void Sgemv(
        CublasOp trans, int m, int n,
        float alpha,
        IntPtr A, int lda,
        IntPtr x, int incx,
        float beta,
        IntPtr y, int incy)
    {
        if (!IsAvailable) return;
        int s = _bindings.cublasSgemv_v2(_handle, (int)trans, m, n,
            &alpha, A, lda, x, incx, &beta, y, incy);
        Check(s, "cublasSgemv");
    }

    /// <summary>
    /// Strided batched SGEMM for batched matmul.
    /// </summary>
    public unsafe void SgemmStridedBatched(
        CublasOp transA, CublasOp transB,
        int m, int n, int k,
        float alpha,
        IntPtr A, int lda, long strideA,
        IntPtr B, int ldb, long strideB,
        float beta,
        IntPtr C, int ldc, long strideC,
        int batchCount)
    {
        if (!IsAvailable) return;
        int s = _bindings.cublasSgemmStridedBatched_v2(_handle, (int)transA, (int)transB, m, n, k,
            &alpha, A, lda, strideA, B, ldb, strideB, &beta, C, ldc, strideC, batchCount);
        Check(s, "cublasSgemmStridedBatched");
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            _bindings?.cublasDestroy_v2(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private static void Check(int status, string fn)
    {
        if (status == 0) return;
        throw new CuBlasException(fn, status);
    }
}

/// <summary>Ошибка вызова cuBLAS.</summary>
internal sealed class CuBlasException : Exception
{
    public int Status { get; }
    public string Function { get; }
    public CuBlasException(string function, int status)
        : base($"cuBLAS {function} вернул статус {status} ({StatusName(status)}).")
    {
        Function = function;
        Status = status;
    }
    private static string StatusName(int s) => s switch
    {
        0 => "SUCCESS",
        1 => "NOT_INITIALIZED",
        3 => "ALLOC_FAILED",
        7 => "INVALID_VALUE",
        8 => "ARCH_MISMATCH",
        11 => "MAPPING_ERROR",
        13 => "EXECUTION_FAILED",
        14 => "INTERNAL_ERROR",
        15 => "NOT_SUPPORTED",
        16 => "LICENSE_ERROR",
        _ => "UNKNOWN",
    };
}

/// <summary>
/// Перебор известных имён cuBLAS-DLL для разных версий CUDA toolkit.
/// </summary>
internal static class CuBlasBindings
{
    /// <summary>Порядок проб: новейшие версии сначала.</summary>
    public static readonly string[] CandidateNames =
    {
        "cublas64_12",
        "cublas64_11",
        "cublas64_10",
    };

    public static ICuBlasBindings For(string name) => name switch
    {
        "cublas64_12" => new CuBlas12(),
        "cublas64_11" => new CuBlas11(),
        "cublas64_10" => new CuBlas10(),
        _ => throw new ArgumentException($"Unknown cuBLAS lib: {name}", nameof(name)),
    };
}

internal interface ICuBlasBindings
{
    int cublasCreate_v2(out IntPtr handle);
    int cublasDestroy_v2(IntPtr handle);
    int cublasSetStream_v2(IntPtr handle, IntPtr stream);
    int cublasSetMathMode(IntPtr handle, int mode);
    unsafe int cublasSgemm_v2(IntPtr handle, int transa, int transb,
        int m, int n, int k, float* alpha, IntPtr A, int lda, IntPtr B, int ldb,
        float* beta, IntPtr C, int ldc);
    unsafe int cublasSgemv_v2(IntPtr handle, int trans, int m, int n,
        float* alpha, IntPtr A, int lda, IntPtr x, int incx,
        float* beta, IntPtr y, int incy);
    unsafe int cublasSgemmStridedBatched_v2(IntPtr handle, int transa, int transb,
        int m, int n, int k, float* alpha, IntPtr A, int lda, long strideA,
        IntPtr B, int ldb, long strideB, float* beta, IntPtr C, int ldc, long strideC,
        int batchCount);
}

file sealed class CuBlas12 : ICuBlasBindings
{
    private const string Lib = "cublas64_12";
    [DllImport(Lib)] private static extern int cublasCreate_v2(out IntPtr handle);
    [DllImport(Lib)] private static extern int cublasDestroy_v2(IntPtr handle);
    [DllImport(Lib)] private static extern int cublasSetStream_v2(IntPtr handle, IntPtr stream);
    [DllImport(Lib)] private static extern int cublasSetMathMode(IntPtr handle, int mode);
    [DllImport(Lib)] private static extern unsafe int cublasSgemm_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, IntPtr B, int ldb, float* b, IntPtr C, int ldc);
    [DllImport(Lib)] private static extern unsafe int cublasSgemv_v2(IntPtr h, int t, int m, int n, float* a, IntPtr A, int lda, IntPtr x, int incx, float* b, IntPtr y, int incy);
    // ВАЖНО: реальная exported-функция в cublas64_*.dll называется
    // cublasSgemmStridedBatched (БЕЗ суффикса _v2). cuBLAS никогда не выпускал
    // _v2-вариант для batched-API; см. NVIDIA cuBLAS docs / cublas_v2.h.
    // Без EntryPoint=… P/Invoke ищет имя метода, что и приводит к
    // EntryPointNotFoundException при первом же batched MatMul (Attention/MHA).
    [DllImport(Lib, EntryPoint = "cublasSgemmStridedBatched")]
    private static extern unsafe int cublasSgemmStridedBatched_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, long sa, IntPtr B, int ldb, long sb, float* b, IntPtr C, int ldc, long sc, int bc);

    int ICuBlasBindings.cublasCreate_v2(out IntPtr h) => cublasCreate_v2(out h);
    int ICuBlasBindings.cublasDestroy_v2(IntPtr h) => cublasDestroy_v2(h);
    int ICuBlasBindings.cublasSetStream_v2(IntPtr h, IntPtr s) => cublasSetStream_v2(h, s);
    int ICuBlasBindings.cublasSetMathMode(IntPtr h, int m) => cublasSetMathMode(h, m);
    unsafe int ICuBlasBindings.cublasSgemm_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, IntPtr B, int ldb, float* b, IntPtr C, int ldc)
        => cublasSgemm_v2(h, ta, tb, m, n, k, a, A, lda, B, ldb, b, C, ldc);
    unsafe int ICuBlasBindings.cublasSgemv_v2(IntPtr h, int t, int m, int n, float* a, IntPtr A, int lda, IntPtr x, int incx, float* b, IntPtr y, int incy)
        => cublasSgemv_v2(h, t, m, n, a, A, lda, x, incx, b, y, incy);
    unsafe int ICuBlasBindings.cublasSgemmStridedBatched_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, long sa, IntPtr B, int ldb, long sb, float* b, IntPtr C, int ldc, long sc, int bc)
        => cublasSgemmStridedBatched_v2(h, ta, tb, m, n, k, a, A, lda, sa, B, ldb, sb, b, C, ldc, sc, bc);
}

file sealed class CuBlas11 : ICuBlasBindings
{
    private const string Lib = "cublas64_11";
    [DllImport(Lib)] private static extern int cublasCreate_v2(out IntPtr handle);
    [DllImport(Lib)] private static extern int cublasDestroy_v2(IntPtr handle);
    [DllImport(Lib)] private static extern int cublasSetStream_v2(IntPtr handle, IntPtr stream);
    [DllImport(Lib)] private static extern int cublasSetMathMode(IntPtr handle, int mode);
    [DllImport(Lib)] private static extern unsafe int cublasSgemm_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, IntPtr B, int ldb, float* b, IntPtr C, int ldc);
    [DllImport(Lib)] private static extern unsafe int cublasSgemv_v2(IntPtr h, int t, int m, int n, float* a, IntPtr A, int lda, IntPtr x, int incx, float* b, IntPtr y, int incy);
    [DllImport(Lib, EntryPoint = "cublasSgemmStridedBatched")]
    private static extern unsafe int cublasSgemmStridedBatched_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, long sa, IntPtr B, int ldb, long sb, float* b, IntPtr C, int ldc, long sc, int bc);

    int ICuBlasBindings.cublasCreate_v2(out IntPtr h) => cublasCreate_v2(out h);
    int ICuBlasBindings.cublasDestroy_v2(IntPtr h) => cublasDestroy_v2(h);
    int ICuBlasBindings.cublasSetStream_v2(IntPtr h, IntPtr s) => cublasSetStream_v2(h, s);
    int ICuBlasBindings.cublasSetMathMode(IntPtr h, int m) => cublasSetMathMode(h, m);
    unsafe int ICuBlasBindings.cublasSgemm_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, IntPtr B, int ldb, float* b, IntPtr C, int ldc)
        => cublasSgemm_v2(h, ta, tb, m, n, k, a, A, lda, B, ldb, b, C, ldc);
    unsafe int ICuBlasBindings.cublasSgemv_v2(IntPtr h, int t, int m, int n, float* a, IntPtr A, int lda, IntPtr x, int incx, float* b, IntPtr y, int incy)
        => cublasSgemv_v2(h, t, m, n, a, A, lda, x, incx, b, y, incy);
    unsafe int ICuBlasBindings.cublasSgemmStridedBatched_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, long sa, IntPtr B, int ldb, long sb, float* b, IntPtr C, int ldc, long sc, int bc)
        => cublasSgemmStridedBatched_v2(h, ta, tb, m, n, k, a, A, lda, sa, B, ldb, sb, b, C, ldc, sc, bc);
}

file sealed class CuBlas10 : ICuBlasBindings
{
    private const string Lib = "cublas64_10";
    [DllImport(Lib)] private static extern int cublasCreate_v2(out IntPtr handle);
    [DllImport(Lib)] private static extern int cublasDestroy_v2(IntPtr handle);
    [DllImport(Lib)] private static extern int cublasSetStream_v2(IntPtr handle, IntPtr stream);
    [DllImport(Lib)] private static extern int cublasSetMathMode(IntPtr handle, int mode);
    [DllImport(Lib)] private static extern unsafe int cublasSgemm_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, IntPtr B, int ldb, float* b, IntPtr C, int ldc);
    [DllImport(Lib)] private static extern unsafe int cublasSgemv_v2(IntPtr h, int t, int m, int n, float* a, IntPtr A, int lda, IntPtr x, int incx, float* b, IntPtr y, int incy);
    [DllImport(Lib, EntryPoint = "cublasSgemmStridedBatched")]
    private static extern unsafe int cublasSgemmStridedBatched_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, long sa, IntPtr B, int ldb, long sb, float* b, IntPtr C, int ldc, long sc, int bc);

    int ICuBlasBindings.cublasCreate_v2(out IntPtr h) => cublasCreate_v2(out h);
    int ICuBlasBindings.cublasDestroy_v2(IntPtr h) => cublasDestroy_v2(h);
    int ICuBlasBindings.cublasSetStream_v2(IntPtr h, IntPtr s) => cublasSetStream_v2(h, s);
    int ICuBlasBindings.cublasSetMathMode(IntPtr h, int m) => cublasSetMathMode(h, m);
    unsafe int ICuBlasBindings.cublasSgemm_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, IntPtr B, int ldb, float* b, IntPtr C, int ldc)
        => cublasSgemm_v2(h, ta, tb, m, n, k, a, A, lda, B, ldb, b, C, ldc);
    unsafe int ICuBlasBindings.cublasSgemv_v2(IntPtr h, int t, int m, int n, float* a, IntPtr A, int lda, IntPtr x, int incx, float* b, IntPtr y, int incy)
        => cublasSgemv_v2(h, t, m, n, a, A, lda, x, incx, b, y, incy);
    unsafe int ICuBlasBindings.cublasSgemmStridedBatched_v2(IntPtr h, int ta, int tb, int m, int n, int k, float* a, IntPtr A, int lda, long sa, IntPtr B, int ldb, long sb, float* b, IntPtr C, int ldc, long sc, int bc)
        => cublasSgemmStridedBatched_v2(h, ta, tb, m, n, k, a, A, lda, sa, B, ldb, sb, b, C, ldc, sc, bc);
}

internal enum CublasOp
{
    N = 0,
    T = 1,
    C = 2
}
