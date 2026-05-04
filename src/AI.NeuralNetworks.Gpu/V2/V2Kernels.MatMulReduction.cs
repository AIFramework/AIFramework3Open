using ILGPU;
using ILGPU.Algorithms;

namespace AI.ML.NeuralNetworks.Gpu.V2;

public static partial class V2Kernels
{
    #region MatMul

    /// <summary>
    /// y[m, n] = sum_k a[m, k] * b[k, n]; i.x = m, i.y = n. Naïve, без shared memory —
    /// для серьёзных размеров используйте cuBLAS (см. <see cref="GpuOps.MatMulCuBlas"/>).
    /// </summary>
    public static void GemmNaive(Index2D idx,
        ArrayView<float> a, ArrayView<float> b, ArrayView<float> y,
        int M, int N, int K)
    {
        int m = idx.X, n = idx.Y;
        if (m >= M || n >= N) return;
        float acc = 0f;
        int aBase = m * K;
        for (int k = 0; k < K; k++) acc += a[aBase + k] * b[k * N + n];
        y[m * N + n] = acc;
    }

    /// <summary>Batched GEMM (B × M × K) × (B × K × N).</summary>
    public static void BatchedGemmNaive(Index3D idx,
        ArrayView<float> a, ArrayView<float> b, ArrayView<float> y,
        int B, int M, int N, int K)
    {
        int p = idx.X, m = idx.Y, n = idx.Z;
        if (p >= B || m >= M || n >= N) return;
        float acc = 0f;
        int aBase = p * M * K + m * K;
        int bBase = p * K * N;
        for (int k = 0; k < K; k++) acc += a[aBase + k] * b[bBase + k * N + n];
        y[p * M * N + m * N + n] = acc;
    }

    #endregion MatMul

    #region Reductions

    /// <summary>Atomic-сумма всех элементов; out[0] += x[i].</summary>
    public static void SumAll(Index1D i, ArrayView<float> x, ArrayView<float> outScalar)
        => Atomic.Add(ref outScalar[0], x[i]);

    /// <summary>
    /// Sum по оси: один поток на (outer, inner) -> дает y[(o, n)] = Σ_a x[(o, a, n)].
    /// Совершает <paramref name="dim"/> читок памяти на поток. Не идеал для огромных
    /// dim, но достаточно для типичных случаев в LayerNorm/loss/Mean.
    /// </summary>
    public static void SumAxis(Index1D i,
        ArrayView<float> x, ArrayView<float> y,
        int dim, int inner)
    {
        // i пробегает [0, outer*inner). Раскладываем на (o, n).
        int n = (int)(i % inner);
        int o = (int)(i / inner);
        long baseSrc = (long)o * dim * inner + n;
        float acc = 0f;
        for (int a = 0; a < dim; a++) acc += x[baseSrc + (long)a * inner];
        y[i] = acc;
    }

    /// <summary>
    /// Broadcast-fill «обратной» формы: dst[i] = src[reduceIdx(i)], где reduceIdx
    /// «свёрнутая» позиция (axes с stride=0 повторяются). Используется как backward
    /// для Sum/Mean — заливаем градиент со «сжатой» формы обратно в исходную.
    /// </summary>
    public static void BroadcastFill6D(Index1D i,
        ArrayView<float> src, ArrayView<float> dst,
        StridedCopyArgs args)
    {
        // dims = O0..O5 — output (исходная); strides = SS0..SS5 — strides ИСТОЧНИКА
        // (свёрнутые оси имеют stride=0, ось keepDim=1 уже свёрнута).
        long lin = i;
        int i5 = (int)(lin % args.O5); lin /= args.O5;
        int i4 = (int)(lin % args.O4); lin /= args.O4;
        int i3 = (int)(lin % args.O3); lin /= args.O3;
        int i2 = (int)(lin % args.O2); lin /= args.O2;
        int i1 = (int)(lin % args.O1); lin /= args.O1;
        int i0 = (int)lin;
        int srcIdx = args.SrcOffset
                     + i0 * args.SS0 + i1 * args.SS1 + i2 * args.SS2
                     + i3 * args.SS3 + i4 * args.SS4 + i5 * args.SS5;
        dst[i] = src[srcIdx];
    }

    #endregion Reductions

    #region Softmax / LogSoftmax (fwd + bwd)
    // Вектор softmax длины <paramref name="dim"/>; outer*inner групп параллельно.
    // Один поток обрабатывает один такой вектор: 3 прохода (max -> expSum -> divide).
    // Для размерных <i>dim</i> это compute-bound, не memory-bound — ОК.

    /// <summary>Forward softmax по оси: один поток на (outer, inner) пару.</summary>
    public static void SoftmaxFwd(Index1D i,
        ArrayView<float> x, ArrayView<float> y,
        int dim, int inner)
    {
        int n = (int)(i % inner);
        int o = (int)(i / inner);
        long baseIdx = (long)o * dim * inner + n;

        // 1) max-trick: numeric stability.
        float m = x[baseIdx];
        for (int a = 1; a < dim; a++)
        {
            float v = x[baseIdx + (long)a * inner];
            if (v > m) m = v;
        }
        // 2) expSum.
        float sum = 0f;
        for (int a = 0; a < dim; a++)
        {
            float e = XMath.Exp(x[baseIdx + (long)a * inner] - m);
            y[baseIdx + (long)a * inner] = e;
            sum += e;
        }
        // 3) normalize. Underflow — стабильный 0 (не NaN).
        if (sum > 0f && !XMath.IsInfinity(sum))
        {
            float inv = 1f / sum;
            for (int a = 0; a < dim; a++)
                y[baseIdx + (long)a * inner] *= inv;
        }
        else
        {
            for (int a = 0; a < dim; a++)
                y[baseIdx + (long)a * inner] = 0f;
        }
    }

    /// <summary>
    /// Backward softmax: gx_k = y_k · (gy_k − Σ_j y_j·gy_j).
    /// </summary>
    public static void SoftmaxBwd(Index1D i,
        ArrayView<float> y, ArrayView<float> gy, ArrayView<float> gx,
        int dim, int inner)
    {
        int n = (int)(i % inner);
        int o = (int)(i / inner);
        long baseIdx = (long)o * dim * inner + n;
        float dot = 0f;
        for (int a = 0; a < dim; a++)
            dot += y[baseIdx + (long)a * inner] * gy[baseIdx + (long)a * inner];
        for (int a = 0; a < dim; a++)
        {
            long off = baseIdx + (long)a * inner;
            gx[off] = y[off] * (gy[off] - dot);
        }
    }

    /// <summary>Forward log-softmax: y_k = x_k − (max + log(Σ exp(x − max))).</summary>
    public static void LogSoftmaxFwd(Index1D i,
        ArrayView<float> x, ArrayView<float> y,
        int dim, int inner)
    {
        int n = (int)(i % inner);
        int o = (int)(i / inner);
        long baseIdx = (long)o * dim * inner + n;

        float m = x[baseIdx];
        for (int a = 1; a < dim; a++)
        {
            float v = x[baseIdx + (long)a * inner];
            if (v > m) m = v;
        }
        float sumExp = 0f;
        for (int a = 0; a < dim; a++)
            sumExp += XMath.Exp(x[baseIdx + (long)a * inner] - m);
        float logSum = (sumExp > 0f && !XMath.IsInfinity(sumExp))
            ? m + XMath.Log(sumExp)
            : m;
        for (int a = 0; a < dim; a++)
        {
            long off = baseIdx + (long)a * inner;
            y[off] = x[off] - logSum;
        }
    }

    /// <summary>
    /// Backward log-softmax: gx_k = gy_k − exp(y_k) · Σ_j gy_j.
    /// </summary>
    public static void LogSoftmaxBwd(Index1D i,
        ArrayView<float> y, ArrayView<float> gy, ArrayView<float> gx,
        int dim, int inner)
    {
        int n = (int)(i % inner);
        int o = (int)(i / inner);
        long baseIdx = (long)o * dim * inner + n;
        float sumGy = 0f;
        for (int a = 0; a < dim; a++)
            sumGy += gy[baseIdx + (long)a * inner];
        for (int a = 0; a < dim; a++)
        {
            long off = baseIdx + (long)a * inner;
            gx[off] = gy[off] - XMath.Exp(y[off]) * sumGy;
        }
    }

    #endregion Softmax / LogSoftmax (fwd + bwd)

    #region LayerNorm (fwd + bwd)
    // Layout: x.Reshape(batches, normSize). Один поток на батч-строку.
    // Аффинные w/b формой (normSize). Сохраняем mean/rstd для backward.

    /// <summary>
    /// Forward LayerNorm с опциональным affine. <paramref name="hasAffine"/> = 1, если
    /// w/b валидны (1D длиной normSize); 0 — игнорируем w/b.
    /// </summary>
    public static void LayerNormFwd(Index1D i,
        ArrayView<float> x, ArrayView<float> w, ArrayView<float> b,
        ArrayView<float> y, ArrayView<float> meanOut, ArrayView<float> rstdOut,
        int normSize, float eps, int hasAffine)
    {
        long baseIdx = (long)(int)i * normSize;

        // 1) mean
        float sum = 0f;
        for (int k = 0; k < normSize; k++) sum += x[baseIdx + k];
        float mean = sum / normSize;

        // 2) var
        float vsum = 0f;
        for (int k = 0; k < normSize; k++)
        {
            float d = x[baseIdx + k] - mean;
            vsum += d * d;
        }
        float var_ = vsum / normSize;
        float rstd = 1f / XMath.Sqrt(var_ + eps);

        meanOut[i] = mean;
        rstdOut[i] = rstd;

        // 3) normalize + affine
        if (hasAffine != 0)
        {
            for (int k = 0; k < normSize; k++)
            {
                float xn = (x[baseIdx + k] - mean) * rstd;
                y[baseIdx + k] = xn * w[k] + b[k];
            }
        }
        else
        {
            for (int k = 0; k < normSize; k++)
                y[baseIdx + k] = (x[baseIdx + k] - mean) * rstd;
        }
    }

    /// <summary>
    /// Backward LayerNorm для входа x: один поток на батч-строку.
    /// Использует сохранённые mean/rstd.
    /// </summary>
    public static void LayerNormBwdX(Index1D i,
        ArrayView<float> x, ArrayView<float> w, ArrayView<float> gy,
        ArrayView<float> mean, ArrayView<float> rstd,
        ArrayView<float> gx,
        int normSize, int hasAffine)
    {
        long baseIdx = (long)(int)i * normSize;
        float mu = mean[i];
        float rs = rstd[i];

        // mean(g_y_norm), mean(g_y_norm * x_norm)
        float meanG = 0f, meanGY = 0f;
        for (int k = 0; k < normSize; k++)
        {
            float xn = (x[baseIdx + k] - mu) * rs;
            float gyn = hasAffine != 0 ? gy[baseIdx + k] * w[k] : gy[baseIdx + k];
            meanG += gyn;
            meanGY += gyn * xn;
        }
        meanG /= normSize;
        meanGY /= normSize;

        // gx = rs * (g_y_norm - meanG - x_norm * meanGY)
        for (int k = 0; k < normSize; k++)
        {
            float xn = (x[baseIdx + k] - mu) * rs;
            float gyn = hasAffine != 0 ? gy[baseIdx + k] * w[k] : gy[baseIdx + k];
            gx[baseIdx + k] = rs * (gyn - meanG - xn * meanGY);
        }
    }

    /// <summary>
    /// Backward LayerNorm для w/b: атомарные accumulate'ы по фичам.
    /// Один поток на (batch, feature), каждый аккумулирует свой вклад.
    /// gw[k] += gy[i,k] · x_norm[i,k]; gb[k] += gy[i,k].
    /// </summary>
    public static void LayerNormBwdWB(Index1D i,
        ArrayView<float> x, ArrayView<float> gy,
        ArrayView<float> mean, ArrayView<float> rstd,
        ArrayView<float> gw, ArrayView<float> gb,
        int normSize)
    {
        // i ∈ [0, batches*normSize). Раскладываем на (batch, k).
        int k = (int)(i % normSize);
        int batch = (int)(i / normSize);
        float xn = (x[i] - mean[batch]) * rstd[batch];
        float gyi = gy[i];
        Atomic.Add(ref gw[k], gyi * xn);
        Atomic.Add(ref gb[k], gyi);
    }

    #endregion LayerNorm (fwd + bwd)
}
