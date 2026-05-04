using System;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Стандартное синусоидальное позиционное кодирование (Vaswani et al., 2017).
/// </summary>
/// <remarks>
/// Не имеет обучаемых параметров; кэш создаётся под заданный максимум длины.
/// При forward результат складывается со входом (..., L, E).
/// </remarks>
public sealed class SinusoidalPositionalEncoding : Module
{
    private readonly int _embedDim;
    private readonly Buffer _pe;  // (max_len, embed_dim)

    /// <summary>Создать кодирование с буфером длины <paramref name="maxLen"/>.</summary>
    public SinusoidalPositionalEncoding(int embedDim, int maxLen = 5000)
    {
        if (embedDim <= 0) throw new ArgumentOutOfRangeException(nameof(embedDim));
        if (embedDim % 2 != 0) throw new ArgumentException("embedDim должен быть чётным.");
        _embedDim = embedDim;
        var pe = Tensor.Empty(new Shape(maxLen, embedDim));
        var s = pe.AsSpan<float>();
        for (int pos = 0; pos < maxLen; pos++)
        for (int i = 0; i < embedDim; i += 2)
        {
            double div = Math.Exp(-Math.Log(10000.0) * i / embedDim);
            s[pos * embedDim + i] = (float)Math.Sin(pos * div);
            s[pos * embedDim + i + 1] = (float)Math.Cos(pos * div);
        }
        _pe = RegisterBuffer("pe", pe);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Rank < 2) throw new ArgumentException("Ожидается (..., L, E).");
        int L = input.Shape[input.Rank - 2];
        int E = input.Shape[input.Rank - 1];
        if (E != _embedDim) throw new ArgumentException(
            $"Несовпадение embed_dim: {E} vs {_embedDim}.");
        Tensor peTensor = _pe;
        if (L > peTensor.Shape[0]) throw new ArgumentException(
            $"Длина {L} превышает max_len={peTensor.Shape[0]}.");
        var pe = Ops.IndexingOps.Narrow(peTensor, axis: 0, start: 0, length: L);
        return input + pe;
    }
}

/// <summary>
/// Rotary Positional Embedding (RoPE) — Su et al., 2021.
/// </summary>
/// <remarks>
/// <para>
/// Кодирует позицию через поворот пар координат на угол, зависящий от индекса:
/// для пары (x_{2i}, x_{2i+1}) применяется матрица [[cos θ, -sin θ], [sin θ, cos θ]],
/// где θ = pos / (base ^ (2i/d)). Эквивалентно умножению комплексного числа
/// (x_{2i} + j x_{2i+1}) на e^{j θ}.
/// </para>
/// <para>
/// Применяется к Q и K непосредственно перед attention. Не добавляется к V.
/// </para>
/// </remarks>
public static class RoPE
{
    /// <summary>
    /// Применить RoPE к тензору формы (..., L, D), где D — чётное.
    /// </summary>
    /// <param name="x">Tensor (..., L, D).</param>
    /// <param name="positionStart">С какой позиции начинать (для KV-cache).</param>
    /// <param name="theta">База частот (по умолчанию 10000).</param>
    public static Tensor Apply(Tensor x, int positionStart = 0, float theta = 10000f)
    {
        if (x.Rank < 2) throw new ArgumentException("RoPE: rank должен быть ≥ 2.");
        int D = x.Shape[x.Rank - 1];
        int L = x.Shape[x.Rank - 2];
        if (D % 2 != 0) throw new ArgumentException("RoPE: последняя ось должна быть чётной.");

        var xc = x.Contiguous();
        var src = xc.AsReadOnlySpan<float>();
        var y = Tensor.Empty(x.Shape, x.DType, x.Device);
        var dst = y.AsSpan<float>();

        int halfD = D / 2;
        long outer = 1;
        for (int i = 0; i < x.Rank - 2; i++) outer *= x.Shape[i];

        // Прекомпьют углов (быстро для маленьких D, иначе можно кэшировать).
        var cosT = new float[L * halfD];
        var sinT = new float[L * halfD];
        for (int p = 0; p < L; p++)
        for (int i = 0; i < halfD; i++)
        {
            double angle = (positionStart + p) / Math.Pow(theta, 2.0 * i / D);
            cosT[p * halfD + i] = (float)Math.Cos(angle);
            sinT[p * halfD + i] = (float)Math.Sin(angle);
        }

        for (long o = 0; o < outer; o++)
        {
            long obase = o * L * D;
            for (int p = 0; p < L; p++)
            {
                long lbase = obase + (long)p * D;
                int tbase = p * halfD;
                for (int i = 0; i < halfD; i++)
                {
                    float xa = src[(int)(lbase + 2 * i)];
                    float xb = src[(int)(lbase + 2 * i + 1)];
                    float c = cosT[tbase + i], s = sinT[tbase + i];
                    dst[(int)(lbase + 2 * i)]     = xa * c - xb * s;
                    dst[(int)(lbase + 2 * i + 1)] = xa * s + xb * c;
                }
            }
        }

        if (Autograd.TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            int posStart = positionStart;
            float th = theta;
            // Backward: применить rotate(-θ).
            var fn = new Autograd.ViewFunction(g => ApplyConj(g, posStart, th));
            fn.RegisterInput(x);
            y.GradFn = fn;
        }
        return y;
    }

    private static Tensor ApplyConj(Tensor x, int positionStart, float theta)
    {
        // Inverse: применить с -θ. Просто: cos(-θ)=cos(θ), sin(-θ)=-sin(θ).
        int D = x.Shape[x.Rank - 1];
        int L = x.Shape[x.Rank - 2];
        var xc = x.Contiguous();
        var src = xc.AsReadOnlySpan<float>();
        var y = Tensor.Empty(x.Shape, x.DType, x.Device);
        var dst = y.AsSpan<float>();
        int halfD = D / 2;
        long outer = 1;
        for (int i = 0; i < x.Rank - 2; i++) outer *= x.Shape[i];

        for (long o = 0; o < outer; o++)
        {
            long obase = o * L * D;
            for (int p = 0; p < L; p++)
            {
                long lbase = obase + (long)p * D;
                for (int i = 0; i < halfD; i++)
                {
                    double angle = (positionStart + p) / Math.Pow(theta, 2.0 * i / D);
                    float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
                    float xa = src[(int)(lbase + 2 * i)];
                    float xb = src[(int)(lbase + 2 * i + 1)];
                    // Rotation by -θ.
                    dst[(int)(lbase + 2 * i)]     =  xa * c + xb * s;
                    dst[(int)(lbase + 2 * i + 1)] = -xa * s + xb * c;
                }
            }
        }
        return y;
    }
}

/// <summary>
/// Adaptive LayerNorm: scale/shift приходят извне (например, от модулирующей сети
/// в DiT/StyleGAN). Обычная LN заморожена (без weight/bias), а scale=1+gamma, shift=beta
/// предсказываются отдельным MLP.
/// </summary>
/// <remarks>
/// Использование: сначала применить LayerNorm без affine, затем умножить на (1+gamma)
/// и добавить beta. Вход: (..., E), gamma/beta: (..., E) — broadcast'ятся к норме.
/// </remarks>
public static class AdaLN
{
    /// <summary>
    /// Применить AdaLN: <c>y = LN(x) * (1 + gamma) + beta</c>.
    /// </summary>
    public static Tensor Apply(Tensor x, Tensor gamma, Tensor beta, float eps = 1e-5f)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        int E = x.Shape[x.Rank - 1];
        var ln = LayerNorm.Apply(x, normSize: E, eps: eps, weight: null, bias: null);
        return ln * (Tensor.Full(new Shape(), 1f, x.DType, x.Device) + gamma) + beta;
    }
}
