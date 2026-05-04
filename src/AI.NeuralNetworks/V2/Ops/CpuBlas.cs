using System;
using OpenBlasSharp;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// CPU BLAS wrappers: Sgemm via OpenBLAS, im2col/col2im for convolution.
/// Silently falls back when native library is unavailable.
/// </summary>
internal static class CpuBlas
{
    internal const long GemmThreshold = 512;

    private static readonly bool _blasAvailable = ProbeBlas();

    private static bool ProbeBlas()
    {
        try
        {
            _ = OpenBlas.GetNumThreads();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool ShouldUseBlas(long M, long N, long K)
        => _blasAvailable && M * N * K > GemmThreshold;

    /// <summary>
    /// C = alpha * op(A) * op(B) + beta * C.
    /// op(A) is M×K, op(B) is K×N, C is M×N. All row-major.
    /// </summary>
    internal static unsafe void Sgemm(
        ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c,
        int M, int N, int K,
        bool transA = false, bool transB = false,
        float alpha = 1f, float beta = 0f)
    {
        if (M == 0 || N == 0 || K == 0) return;

        var tA = transA ? Transpose.Trans : Transpose.NoTrans;
        var tB = transB ? Transpose.Trans : Transpose.NoTrans;
        int lda = transA ? M : K;
        int ldb = transB ? K : N;
        int ldc = N;

        fixed (float* pA = a)
        fixed (float* pB = b)
        fixed (float* pC = c)
        {
            Blas.Sgemm(Order.RowMajor, tA, tB,
                M, N, K, alpha, pA, lda, pB, ldb, beta, pC, ldc);
        }
    }

    /// <summary>
    /// Im2Col: unfold (C, H, W) input into (C*kH*kW, Hout*Wout) column matrix.
    /// </summary>
    internal static void Im2Col(
        ReadOnlySpan<float> input, Span<float> cols,
        int C, int H, int W,
        int kH, int kW,
        int strideH, int strideW,
        int padH, int padW,
        int dilH, int dilW)
    {
        if (strideH <= 0 || strideW <= 0)
            throw new ArgumentException($"Im2Col: stride должен быть > 0, получено ({strideH},{strideW}).");
        if (kH <= 0 || kW <= 0)
            throw new ArgumentException($"Im2Col: kernel должен быть > 0, получено ({kH},{kW}).");
        if (dilH <= 0 || dilW <= 0)
            throw new ArgumentException($"Im2Col: dilation должен быть > 0, получено ({dilH},{dilW}).");
        int Hout = (H + 2 * padH - dilH * (kH - 1) - 1) / strideH + 1;
        int Wout = (W + 2 * padW - dilW * (kW - 1) - 1) / strideW + 1;
        if (Hout <= 0 || Wout <= 0)
            throw new ArgumentException(
                $"Im2Col: результирующий выход ({Hout}x{Wout}) <= 0 — несовместимая геометрия " +
                $"(input HxW={H}x{W}, kernel={kH}x{kW}, stride=({strideH},{strideW}), " +
                $"pad=({padH},{padW}), dil=({dilH},{dilW})).");

        for (int c = 0; c < C; c++)
        {
            int inputC = c * H * W;
            for (int kh = 0; kh < kH; kh++)
            for (int kw = 0; kw < kW; kw++)
            {
                int colRow = (c * kH * kW + kh * kW + kw) * Hout * Wout;
                for (int ho = 0; ho < Hout; ho++)
                {
                    int ih = ho * strideH + kh * dilH - padH;
                    if ((uint)ih >= (uint)H)
                    {
                        for (int wo = 0; wo < Wout; wo++)
                            cols[colRow + ho * Wout + wo] = 0f;
                        continue;
                    }
                    int xRow = inputC + ih * W;
                    int cBase = colRow + ho * Wout;
                    for (int wo = 0; wo < Wout; wo++)
                    {
                        int iw = wo * strideW + kw * dilW - padW;
                        cols[cBase + wo] =
                            (uint)iw < (uint)W ? input[xRow + iw] : 0f;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Col2Im: fold (C*kH*kW, numCols) columns back into (C, H, W) with scatter-add.
    /// Output must be pre-zeroed; values are accumulated.
    /// </summary>
    internal static void Col2Im(
        ReadOnlySpan<float> cols, Span<float> output,
        int C, int H, int W,
        int kH, int kW,
        int strideH, int strideW,
        int padH, int padW,
        int dilH, int dilW)
    {
        if (strideH <= 0 || strideW <= 0)
            throw new ArgumentException($"Col2Im: stride должен быть > 0, получено ({strideH},{strideW}).");
        if (kH <= 0 || kW <= 0)
            throw new ArgumentException($"Col2Im: kernel должен быть > 0, получено ({kH},{kW}).");
        if (dilH <= 0 || dilW <= 0)
            throw new ArgumentException($"Col2Im: dilation должен быть > 0, получено ({dilH},{dilW}).");
        int Hout = (H + 2 * padH - dilH * (kH - 1) - 1) / strideH + 1;
        int Wout = (W + 2 * padW - dilW * (kW - 1) - 1) / strideW + 1;
        if (Hout <= 0 || Wout <= 0)
            throw new ArgumentException(
                $"Col2Im: ({Hout}x{Wout}) <= 0 — несовместимая геометрия.");

        for (int c = 0; c < C; c++)
        {
            int outC = c * H * W;
            for (int kh = 0; kh < kH; kh++)
            for (int kw = 0; kw < kW; kw++)
            {
                int colRow = (c * kH * kW + kh * kW + kw) * Hout * Wout;
                for (int ho = 0; ho < Hout; ho++)
                {
                    int ih = ho * strideH + kh * dilH - padH;
                    if ((uint)ih >= (uint)H) continue;
                    int oRow = outC + ih * W;
                    int cBase = colRow + ho * Wout;
                    for (int wo = 0; wo < Wout; wo++)
                    {
                        int iw = wo * strideW + kw * dilW - padW;
                        if ((uint)iw < (uint)W)
                            output[oRow + iw] += cols[cBase + wo];
                    }
                }
            }
        }
    }
}
