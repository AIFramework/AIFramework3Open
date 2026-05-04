using ILGPU;
using ILGPU.Algorithms;

namespace AI.ML.NeuralNetworks.Gpu.V2;

/// <summary>
/// ILGPU-ядра под V2-Tensor (Float32).
/// </summary>
/// <remarks>
/// <para>
/// Эти ядра потребляют GPU-байты как <c>ArrayView&lt;float&gt;</c> через
/// <see cref="CudaStorage.AsView{T}"/>. Бинарные op'ы — <i>contiguous fast-path</i>
/// (без broadcasting); broadcasting в V2 разворачивается во view'ы со stride=0,
/// и для GPU это не работает напрямую — поэтому broadcast-входы предварительно
/// материализуются через CPU-fallback или extension <c>Expand+Contiguous</c>.
/// </para>
/// </remarks>
// Внимание: класс должен оставаться public — ILGPU 1.5.3 на .NET 9 при JIT-сборке
// делегата эмитит код в отдельный dynamic-assembly, который попадает под strict
// access-checks CLR. Если класс/вложенный тип (например, BroadcastArgs) имеет
// effective-accessibility=internal, при загрузке kernel'а вылетает
// TypeLoadException: "Access is denied". Для kernel-контейнеров изоляция не нужна.
public static partial class V2Kernels
{
    #region Structs

    /// <summary>Параметры broadcast-бинарного kernel (16 байт-выровнены).</summary>
    public struct BroadcastArgs
    {
        public int Op;          // 0:Add, 1:Sub, 2:Mul, 3:Div, 4:Pow
        public int AOffset;
        public int BOffset;
        public int O0, O1, O2, O3, O4, O5;
        public int SA0, SA1, SA2, SA3, SA4, SA5;
        public int SB0, SB1, SB2, SB3, SB4, SB5;
    }

    /// <summary>Параметры strided-копирования (16-byte aligned).</summary>
    public struct StridedCopyArgs
    {
        public int SrcOffset;
        public int O0, O1, O2, O3, O4, O5;
        public int SS0, SS1, SS2, SS3, SS4, SS5;
    }

    #endregion Structs

    #region Helpers

    /// <summary>Численно стабильная sigmoid: ветвление по знаку x во избежание overflow.</summary>
    /// <remarks>
    /// Для x ≥ 0: 1/(1 + exp(-x)) — exp(-x) ∈ (0, 1], без overflow.
    /// Для x &lt; 0: exp(x)/(1 + exp(x)) — exp(x) ∈ (0, 1], без overflow.
    /// Соответствует CPU-варианту в <c>RecurrentFused.Sigmoid</c>; иначе
    /// для preact с большими отрицательными значениями получаются потери точности
    /// (1/(1 + huge) -> денормал/0 на GPU vs тонкий positive результат на CPU).
    /// </remarks>
    private static float StableSigmoid(float x)
    {
        if (x >= 0f) { float e = XMath.Exp(-x); return 1f / (1f + e); }
        else { float e = XMath.Exp(x); return e / (1f + e); }
    }

    #endregion Helpers

    #region Заполнение / копия

    public static void Fill(Index1D i, ArrayView<float> x, float v) => x[i] = v;
    public static void ScalarMul(Index1D i, ArrayView<float> x, float s, ArrayView<float> y) => y[i] = x[i] * s;

    #endregion Заполнение / копия
}
