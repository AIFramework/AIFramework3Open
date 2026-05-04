using ILGPU;
using ILGPU.Algorithms;

namespace AI.ML.NeuralNetworks.Gpu.V2;

public static partial class V2Kernels
{
    #region Унарные float32

    public static void NegFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = -x[i];
    public static void AbsFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Abs(x[i]);
    public static void ExpFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Exp(x[i]);
    public static void LogFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Log(x[i]);
    public static void SqrtFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Sqrt(x[i]);
    public static void SinFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Sin(x[i]);
    public static void CosFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Cos(x[i]);
    public static void ReluFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = x[i] > 0f ? x[i] : 0f;

    public static void SigmoidFwd(Index1D i, ArrayView<float> x, ArrayView<float> y)
    {
        float ex = XMath.Exp(-x[i]);
        y[i] = 1f / (1f + ex);
    }

    public static void TanhFwd(Index1D i, ArrayView<float> x, ArrayView<float> y) => y[i] = XMath.Tanh(x[i]);

    public static void SiluFwd(Index1D i, ArrayView<float> x, ArrayView<float> y)
    {
        float v = x[i];
        float s = 1f / (1f + XMath.Exp(-v));
        y[i] = v * s;
    }

    public static void GeluFwd(Index1D i, ArrayView<float> x, ArrayView<float> y)
    {
        const float k0 = 0.7978845608028654f; // sqrt(2/π)
        const float k1 = 0.044715f;
        float v = x[i];
        float u = k0 * (v + k1 * v * v * v);
        y[i] = 0.5f * v * (1f + XMath.Tanh(u));
    }

    #endregion Унарные float32

    #region Бинарные float32 (без broadcast — работаем с contiguous-входами)

    public static void AddFwd(Index1D i, ArrayView<float> a, ArrayView<float> b, ArrayView<float> y) => y[i] = a[i] + b[i];
    public static void SubFwd(Index1D i, ArrayView<float> a, ArrayView<float> b, ArrayView<float> y) => y[i] = a[i] - b[i];
    public static void MulFwd(Index1D i, ArrayView<float> a, ArrayView<float> b, ArrayView<float> y) => y[i] = a[i] * b[i];
    public static void DivFwd(Index1D i, ArrayView<float> a, ArrayView<float> b, ArrayView<float> y) => y[i] = a[i] / b[i];
    public static void PowFwd(Index1D i, ArrayView<float> a, ArrayView<float> b, ArrayView<float> y) => y[i] = XMath.Pow(a[i], b[i]);

    #endregion Бинарные float32 (без broadcast — работаем с contiguous-входами)

    #region Бинарные float32 с broadcasting
    // Поддерживаем до 6 осей; для большего ranka — fallback на CPU. Strides=0
    // означают broadcasted-ось (значение повторяется). Параметры упакованы в
    // struct, чтобы не упереться в максимум аргументов ILGPU.

    /// <summary>Element-wise binary с broadcasting (rank ≤ 6).</summary>
    public static void BinaryBroadcast6D(Index1D i,
        ArrayView<float> a, ArrayView<float> b, ArrayView<float> y,
        BroadcastArgs args)
    {
        long lin = i;
        int i5 = (int)(lin % args.O5); lin /= args.O5;
        int i4 = (int)(lin % args.O4); lin /= args.O4;
        int i3 = (int)(lin % args.O3); lin /= args.O3;
        int i2 = (int)(lin % args.O2); lin /= args.O2;
        int i1 = (int)(lin % args.O1); lin /= args.O1;
        int i0 = (int)lin;
        int aIdx = args.AOffset + i0 * args.SA0 + i1 * args.SA1 + i2 * args.SA2
                                 + i3 * args.SA3 + i4 * args.SA4 + i5 * args.SA5;
        int bIdx = args.BOffset + i0 * args.SB0 + i1 * args.SB1 + i2 * args.SB2
                                 + i3 * args.SB3 + i4 * args.SB4 + i5 * args.SB5;
        float va = a[aIdx];
        float vb = b[bIdx];
        float r;
        switch (args.Op)
        {
            case 0: r = va + vb; break;
            case 1: r = va - vb; break;
            case 2: r = va * vb; break;
            case 3: r = va / vb; break;
            default: r = XMath.Pow(va, vb); break;
        }
        y[i] = r;
    }

    #endregion Бинарные float32 с broadcasting

    #region Backward kernels (element-wise, без broadcast)
    // gx[i] = (∂y/∂x at i) · gy[i]. Для y=σ(x) сохраняем y и считаем dx через y;
    // для остальных — сохраняем x. Парные backward (Mul/Div) пишут оба градиента
    // одним проходом, чтобы экономить запуски kernel'ов.

    public static void SigmoidBwdY(Index1D i, ArrayView<float> y, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = y[i] * (1f - y[i]) * gy[i];

    public static void TanhBwdY(Index1D i, ArrayView<float> y, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = (1f - y[i] * y[i]) * gy[i];

    public static void ExpBwdY(Index1D i, ArrayView<float> y, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = y[i] * gy[i];

    public static void SqrtBwdY(Index1D i, ArrayView<float> y, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = 0.5f / y[i] * gy[i];

    public static void ReluBwdX(Index1D i, ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = x[i] > 0f ? gy[i] : 0f;

    public static void NegBwd(Index1D i, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = -gy[i];

    public static void LogBwdX(Index1D i, ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = gy[i] / x[i];

    public static void AbsBwdX(Index1D i, ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = (x[i] > 0f ? 1f : (x[i] < 0f ? -1f : 0f)) * gy[i];

    public static void SinBwdX(Index1D i, ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = XMath.Cos(x[i]) * gy[i];

    public static void CosBwdX(Index1D i, ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
        => gx[i] = -XMath.Sin(x[i]) * gy[i];

    public static void SiluBwdX(Index1D i, ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
    {
        float s = 1f / (1f + XMath.Exp(-x[i]));
        gx[i] = (s + x[i] * s * (1f - s)) * gy[i];
    }

    /// <summary>Mul backward: ga[i] = b[i]·gy[i], gb[i] = a[i]·gy[i] (одним проходом).</summary>
    public static void MulBwd(Index1D i,
        ArrayView<float> a, ArrayView<float> b, ArrayView<float> gy,
        ArrayView<float> ga, ArrayView<float> gb)
    {
        float gi = gy[i];
        ga[i] = b[i] * gi;
        gb[i] = a[i] * gi;
    }

    /// <summary>Div backward: ga = gy/b, gb = -a·gy/b² (одним проходом).</summary>
    public static void DivBwd(Index1D i,
        ArrayView<float> a, ArrayView<float> b, ArrayView<float> gy,
        ArrayView<float> ga, ArrayView<float> gb)
    {
        float bi = b[i];
        float gi = gy[i];
        ga[i] = gi / bi;
        gb[i] = -a[i] * gi / (bi * bi);
    }

    #endregion Backward kernels (element-wise, без broadcast)

    #region Gelu backward

    /// <summary>
    /// Производная GELU (tanh-аппроксимация): сохраняем x, считаем dy/dx.
    /// dy/dx = 0.5*(1+t) + 0.5*x*(1-t²)*sqrt(2/π)*(1 + 3*0.044715*x²),
    /// где t = tanh(sqrt(2/π) * (x + 0.044715*x³)).
    /// </summary>
    public static void GeluBwdX(Index1D i,
        ArrayView<float> x, ArrayView<float> gy, ArrayView<float> gx)
    {
        const float k0 = 0.7978845608028654f; // sqrt(2/π)
        const float k1 = 0.044715f;
        float v = x[i];
        float v2 = v * v;
        float u = k0 * (v + k1 * v * v2);
        float t = XMath.Tanh(u);
        float dt = 1f - t * t;          // d(tanh)/du
        float du = k0 * (1f + 3f * k1 * v2);
        float deriv = 0.5f * (1f + t) + 0.5f * v * dt * du;
        gx[i] = deriv * gy[i];
    }

    #endregion Gelu backward
}
