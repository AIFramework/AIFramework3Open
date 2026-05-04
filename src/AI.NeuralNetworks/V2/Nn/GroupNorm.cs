using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Group Normalization (Wu &amp; He, 2018). Делит каналы на <see cref="NumGroups"/> групп
/// и нормализует каждую группу отдельно по spatial-осям внутри одного примера батча.
/// </summary>
/// <remarks>
/// <para>
/// Не зависит от размера батча, в отличие от BN, и не требует running stats.
/// При num_groups=num_features ≡ InstanceNorm; при num_groups=1 ≡ LayerNorm
/// по каналам и spatial.
/// </para>
/// <para>Вход формы (N, C, *): C должен делиться на NumGroups.</para>
/// </remarks>
public sealed class GroupNorm : Module
{
    /// <summary>Количество групп.</summary>
    public int NumGroups { get; }
    /// <summary>Количество каналов.</summary>
    public int NumChannels { get; }
    /// <summary>Эпсилон.</summary>
    public float Eps { get; }
    /// <summary>Аффинная gamma/beta.</summary>
    public bool Affine { get; }

    /// <summary>gamma (если affine).</summary>
    public Parameter Weight { get; }
    /// <summary>beta (если affine).</summary>
    public Parameter Bias { get; }

    /// <summary>Создать GroupNorm.</summary>
    public GroupNorm(int numGroups, int numChannels, float eps = 1e-5f, bool affine = true)
    {
        if (numGroups <= 0) throw new ArgumentOutOfRangeException(nameof(numGroups));
        if (numChannels <= 0) throw new ArgumentOutOfRangeException(nameof(numChannels));
        if (numChannels % numGroups != 0)
            throw new ArgumentException("numChannels должен делиться на numGroups.");
        NumGroups = numGroups;
        NumChannels = numChannels;
        Eps = eps;
        Affine = affine;

        if (affine)
        {
            var w = Tensor.Empty(new Shape(numChannels));
            Init.Constant_(w, 1f);
            Weight = RegisterParameter("weight", w);

            var b = Tensor.Empty(new Shape(numChannels));
            Init.Zeros_(b);
            Bias = RegisterParameter("bias", b);
        }
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Rank < 2)
            throw new ArgumentException("GroupNorm: ожидается вход с rank>=2 (N, C, *).");
        if (input.Shape[1] != NumChannels)
            throw new ArgumentException(
                $"GN: канал={input.Shape[1]}, ожидалось {NumChannels}.");
        return Apply(input, NumGroups, NumChannels, Eps, Weight?.Tensor, Bias?.Tensor);
    }

    /// <summary>Функциональная форма.</summary>
    public static Tensor Apply(Tensor input, int G, int C, float eps, Tensor weight, Tensor bias)
    {
        var x = input.Contiguous();
        int rank = x.Rank;
        int N = x.Shape[0];
        long inner = 1;
        for (int i = 2; i < rank; i++) inner *= x.Shape[i];
        int chPerGroup = C / G;
        long groupSize = chPerGroup * inner;

        var y = Tensor.Empty(input.Shape, input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        ReadOnlySpan<float> ws = default, bs = default;
        if (weight != null) ws = weight.Contiguous().AsReadOnlySpan<float>();
        if (bias != null) bs = bias.Contiguous().AsReadOnlySpan<float>();
        bool affine = weight != null;

        var mean = new float[N * G];
        var rstd = new float[N * G];

        for (int n = 0; n < N; n++)
        {
            long nOff = (long)n * C * inner;
            for (int g = 0; g < G; g++)
            {
                long gOff = nOff + (long)g * groupSize;
                float m = 0f;
                for (long k = 0; k < groupSize; k++) m += xs[(int)(gOff + k)];
                m /= groupSize;
                float v = 0f;
                for (long k = 0; k < groupSize; k++) { float d = xs[(int)(gOff + k)] - m; v += d * d; }
                v /= groupSize;
                float rs = 1f / MathF.Sqrt(v + eps);
                mean[n * G + g] = m;
                rstd[n * G + g] = rs;
                // forward
                int chBase = g * chPerGroup;
                for (int cInG = 0; cInG < chPerGroup; cInG++)
                {
                    int c = chBase + cInG;
                    long cOff = gOff + (long)cInG * inner;
                    float w = affine ? ws[c] : 1f;
                    float b = affine ? bs[c] : 0f;
                    for (long s = 0; s < inner; s++)
                    {
                        float xh = (xs[(int)(cOff + s)] - m) * rs;
                        ys[(int)(cOff + s)] = xh * w + b;
                    }
                }
            }
        }

        bool requiresGrad = TapeContext.IsGradEnabled &&
                            (input.RequiresGrad || (weight?.RequiresGrad ?? false) || (bias?.RequiresGrad ?? false));
        if (requiresGrad)
        {
            var fn = new GNFunction(x, weight, bias, mean, rstd, G, C, N, (int)inner);
            fn.RegisterInput(input);
            if (weight != null) fn.RegisterInput(weight);
            if (bias != null) fn.RegisterInput(bias);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class GNFunction : Function
    {
        private readonly Tensor _x, _w, _b;
        private readonly float[] _mean, _rstd;
        private readonly int _G, _C, _N, _inner;

        public GNFunction(Tensor x, Tensor w, Tensor b, float[] mean, float[] rstd,
            int G, int C, int N, int inner)
        {
            _x = x; _w = w; _b = b;
            _mean = mean; _rstd = rstd;
            _G = G; _C = C; _N = N; _inner = inner;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var xs = _x.AsReadOnlySpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            ReadOnlySpan<float> ws = default;
            if (_w != null) ws = _w.Contiguous().AsReadOnlySpan<float>();
            bool affine = _w != null;
            int chPerGroup = _C / _G;
            long groupSize = (long)chPerGroup * _inner;

            var dx = Tensor.Zeros(_x.Shape, _x.DType, _x.Device);
            var dxs = dx.AsSpan<float>();
            Tensor dW = null, dB = null;
            Span<float> dWs = default, dBs = default;
            if (affine && _w.RequiresGrad)
            {
                dW = Tensor.Zeros(_w.Shape, _w.DType, _w.Device);
                dWs = dW.AsSpan<float>();
            }
            if (_b != null && _b.RequiresGrad)
            {
                dB = Tensor.Zeros(_b.Shape, _b.DType, _b.Device);
                dBs = dB.AsSpan<float>();
            }

            for (int n = 0; n < _N; n++)
            {
                long nOff = (long)n * _C * _inner;
                for (int g = 0; g < _G; g++)
                {
                    long gOff = nOff + (long)g * groupSize;
                    float m = _mean[n * _G + g], rs = _rstd[n * _G + g];

                    // sum_gy_w, sum_gy_w_xh по группе
                    float sumGyW = 0f, sumGyWXh = 0f;
                    int chBase = g * chPerGroup;
                    for (int cInG = 0; cInG < chPerGroup; cInG++)
                    {
                        int c = chBase + cInG;
                        long cOff = gOff + (long)cInG * _inner;
                        float w = affine ? ws[c] : 1f;
                        for (int s = 0; s < _inner; s++)
                        {
                            float gy = gys[(int)(cOff + s)];
                            float xh = (xs[(int)(cOff + s)] - m) * rs;
                            sumGyW += gy * w;
                            sumGyWXh += gy * w * xh;
                            if (!dWs.IsEmpty) dWs[c] += gy * xh;
                            if (!dBs.IsEmpty) dBs[c] += gy;
                        }
                    }

                    for (int cInG = 0; cInG < chPerGroup; cInG++)
                    {
                        int c = chBase + cInG;
                        long cOff = gOff + (long)cInG * _inner;
                        float w = affine ? ws[c] : 1f;
                        for (int s = 0; s < _inner; s++)
                        {
                            float gy = gys[(int)(cOff + s)];
                            float xh = (xs[(int)(cOff + s)] - m) * rs;
                            float gyw = gy * w;
                            dxs[(int)(cOff + s)] =
                                (1f / groupSize) * rs * (groupSize * gyw - sumGyW - xh * sumGyWXh);
                        }
                    }
                }
            }

            int outArity = 1 + (affine ? 1 : 0) + (_b != null ? 1 : 0);
            var grads = new Tensor[outArity];
            grads[0] = dx;
            int idx = 1;
            if (affine) grads[idx++] = dW;
            if (_b != null) grads[idx++] = dB;
            return grads;
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GroupNorm(groups={NumGroups}, channels={NumChannels}, eps={Eps})";
}
