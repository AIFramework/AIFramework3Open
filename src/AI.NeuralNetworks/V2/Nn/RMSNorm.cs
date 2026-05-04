using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// RMS Normalization (как в LLaMA, T5): y = x / sqrt(mean(x^2) + eps) * gamma.
/// Без вычитания среднего и без bias — быстрее LN, лучше численная стабильность.
/// </summary>
public sealed class RMSNorm : Module
{
    /// <summary>Размер последней оси, по которой нормализуется тензор.</summary>
    public int NormalizedFeatures { get; }

    /// <summary>Эпсилон.</summary>
    public float Eps { get; }

    /// <summary>Аффинная масштабирующая параметризация.</summary>
    public Parameter Weight { get; }

    /// <summary>Создать RMSNorm.</summary>
    public RMSNorm(int normalizedFeatures, float eps = 1e-6f, bool elementwiseAffine = true)
    {
        if (normalizedFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(normalizedFeatures));
        NormalizedFeatures = normalizedFeatures;
        Eps = eps;

        if (elementwiseAffine)
        {
            var w = Tensor.Empty(new Shape(normalizedFeatures));
            Init.Constant_(w, 1f);
            Weight = RegisterParameter("weight", w);
        }
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Shape[input.Rank - 1] != NormalizedFeatures)
            throw new ArgumentException(
                $"RMSNorm: ожидалась последняя ось={NormalizedFeatures}, фактически {input.Shape[input.Rank - 1]}.");
        return Apply(input, NormalizedFeatures, Eps, Weight?.Tensor);
    }

    /// <summary>Функциональная форма.</summary>
    public static Tensor Apply(Tensor input, int normSize, float eps, Tensor weight)
    {
        var x = input.Contiguous();
        long total = x.NumElements;
        long batches = total / normSize;
        var y = Tensor.Empty(input.Shape, input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        ReadOnlySpan<float> ws = default;
        if (weight != null) ws = weight.Contiguous().AsReadOnlySpan<float>();
        bool affine = weight != null;

        var rms = new float[batches]; // 1/sqrt(mean(x^2)+eps)

        for (long b = 0; b < batches; b++)
        {
            int off = (int)(b * normSize);
            float ss = 0f;
            for (int i = 0; i < normSize; i++) { float v = xs[off + i]; ss += v * v; }
            float r = 1f / MathF.Sqrt(ss / normSize + eps);
            rms[b] = r;
            for (int i = 0; i < normSize; i++)
            {
                float val = xs[off + i] * r;
                if (affine) val *= ws[i];
                ys[off + i] = val;
            }
        }

        bool requiresGrad = TapeContext.IsGradEnabled &&
                            (input.RequiresGrad || (weight?.RequiresGrad ?? false));
        if (requiresGrad)
        {
            var fn = new RMSNormFunction(x, weight, rms, normSize, eps);
            fn.RegisterInput(input);
            if (weight != null) fn.RegisterInput(weight);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class RMSNormFunction : Function
    {
        private readonly Tensor _x, _w;
        private readonly float[] _rms;
        private readonly int _N;
        private readonly float _eps;

        public RMSNormFunction(Tensor x, Tensor w, float[] rms, int n, float eps)
        {
            _x = x; _w = w; _rms = rms; _N = n; _eps = eps;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var xs = _x.AsReadOnlySpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            ReadOnlySpan<float> ws = default;
            if (_w != null) ws = _w.Contiguous().AsReadOnlySpan<float>();
            bool affine = _w != null;

            var dx = Tensor.Zeros(_x.Shape, _x.DType, _x.Device);
            var dxs = dx.AsSpan<float>();
            Tensor dW = null;
            Span<float> dWs = default;
            if (affine && _w.RequiresGrad)
            {
                dW = Tensor.Zeros(_w.Shape, _w.DType, _w.Device);
                dWs = dW.AsSpan<float>();
            }

            long batches = _x.NumElements / _N;
            for (long b = 0; b < batches; b++)
            {
                int off = (int)(b * _N);
                float r = _rms[b];

                // Сначала dW: dW_i += gy_i * x_i * r
                if (!dWs.IsEmpty)
                    for (int i = 0; i < _N; i++) dWs[i] += gys[off + i] * xs[off + i] * r;

                // dx = (gy * w * r) - x * (sum(gy * w * x) * r^3 / N)
                float sumGyWX = 0f;
                for (int i = 0; i < _N; i++)
                {
                    float gyw = affine ? gys[off + i] * ws[i] : gys[off + i];
                    sumGyWX += gyw * xs[off + i];
                }
                float coef = sumGyWX * r * r * r / _N;
                for (int i = 0; i < _N; i++)
                {
                    float gyw = affine ? gys[off + i] * ws[i] : gys[off + i];
                    dxs[off + i] = gyw * r - xs[off + i] * coef;
                }
            }

            return affine ? new[] { dx, dW } : new[] { dx };
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"RMSNorm(features={NormalizedFeatures}, eps={Eps})";
}
