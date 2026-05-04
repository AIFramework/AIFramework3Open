using ILGPU;
using ILGPU.Algorithms;

namespace AI.ML.NeuralNetworks.Gpu.V2;

/// <summary>
/// Fused (slipped) ILGPU-ядра для V2: одно ядро вместо нескольких — экономит
/// global memory traffic и улучшает throughput. Сейчас:
/// <list type="bullet">
///   <item><see cref="LinearGeluFwd"/> — y = gelu(x @ W^T + b).</item>
///   <item><see cref="AddBiasReluFwd"/> — y = relu(x + bias) (bias broadcast по rows).</item>
///   <item><see cref="AdamWStep"/> — Adam/AdamW update в одном kernel-проходе.</item>
/// </list>
/// </summary>
// См. комментарий в V2Kernels: класс должен быть public, иначе ILGPU JIT-emit
// валится с TypeLoadException на .NET 9 при первой загрузке kernel'а.
public static class FusedKernels
{
    #region Linear + GELU forward (только forward; backward тривиально через GpuOps)

    /// <summary>
    /// y[m,n] = gelu( sum_k x[m,k]*W[n,k] + b[n] ). Запускается на (M, N).
    /// W в row-major (N, K) — чтобы шаг по k был contiguous.
    /// </summary>
    public static void LinearGeluFwd(Index2D idx,
        ArrayView<float> x, ArrayView<float> W, ArrayView<float> b, ArrayView<float> y,
        int M, int N, int K)
    {
        int m = idx.X, n = idx.Y;
        if (m >= M || n >= N) return;
        float acc = b[n];
        int xBase = m * K, wBase = n * K;
        for (int k = 0; k < K; k++) acc += x[xBase + k] * W[wBase + k];
        // GELU (tanh-аппроксимация).
        const float c0 = 0.7978845608028654f;
        const float c1 = 0.044715f;
        float u = c0 * (acc + c1 * acc * acc * acc);
        y[m * N + n] = 0.5f * acc * (1f + XMath.Tanh(u));
    }

    /// <summary>
    /// y[i] = max(0, x[i] + bias[i % stride]). Требуется <c>stride &gt; 0</c>; вызывающая
    /// сторона (host) обязана это валидировать, иначе ядро уйдёт в деление на ноль на GPU.
    /// </summary>
    public static void AddBiasReluFwd(Index1D i, ArrayView<float> x, ArrayView<float> bias, ArrayView<float> y, int stride)
    {
        // ВАЖНО: на host-стороне gate'ируется stride > 0 (см. вспомогательные методы
        // в GpuOps/AddBiasReluLaunch). Здесь предполагаем stride >= 1.
        float v = x[i] + bias[i % stride];
        y[i] = v > 0f ? v : 0f;
    }

    /// <summary>
    /// Вспомогательный launcher, гарантирующий <c>stride &gt; 0</c>. Использовать при
    /// вызове <see cref="AddBiasReluFwd"/> из host-кода.
    /// </summary>
    public static void EnsureValidStride(int stride)
    {
        if (stride <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(stride),
                "AddBiasReluFwd: stride должен быть > 0 (число каналов bias).");
    }

    #endregion Linear + GELU forward (только forward; backward тривиально через GpuOps)

    #region Fused AdamW step

    /// <summary>
    /// p <- p − lr · ( m̂/(√v̂+eps) + wd·p ),  m=β1·m+(1-β1)·g,  v=β2·v+(1-β2)·g²,
    /// m̂=m/(1-β1^t), v̂=v/(1-β2^t). Decoupled weight-decay (AdamW).
    /// </summary>
    public static void AdamWStep(Index1D i,
        ArrayView<float> p, ArrayView<float> g, ArrayView<float> m, ArrayView<float> v,
        float lr, float beta1, float beta2, float eps, float wd,
        float bc1, float bc2)
    {
        float gi = g[i];
        float mi = beta1 * m[i] + (1f - beta1) * gi;
        float vi = beta2 * v[i] + (1f - beta2) * gi * gi;
        m[i] = mi; v[i] = vi;
        float mhat = mi / bc1;
        float vhat = vi / bc2;
        p[i] -= lr * (mhat / (XMath.Sqrt(vhat) + eps) + wd * p[i]);
    }
    #endregion Fused AdamW step

}