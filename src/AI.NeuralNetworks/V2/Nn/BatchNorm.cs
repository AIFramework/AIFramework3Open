using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Batch Normalization, общий случай. Нормализует по «батч-осям» (всем кроме
/// канальной), используя running mean/var в eval-режиме.
/// </summary>
/// <remarks>
/// <para>
/// Канальная ось — <see cref="ChannelAxis"/>. Например, BatchNorm1d работает на
/// (N, C) и (N, C, L) — канал на оси 1. BatchNorm2d работает на (N, C, H, W).
/// </para>
/// <para>
/// running_mean, running_var — буферы (не учатся), обновляются в train-режиме
/// по формуле rm = (1-momentum)*rm + momentum*batch_mean.
/// </para>
/// </remarks>
public abstract class BatchNormBase : Module
{
    /// <summary>Число каналов.</summary>
    public int NumFeatures { get; }

    /// <summary>Эпсилон.</summary>
    public float Eps { get; }

    /// <summary>Momentum для running stats (PyTorch-стиль: 0.1 default).</summary>
    public float Momentum { get; }

    /// <summary>Аффинная gamma/beta.</summary>
    public bool Affine { get; }

    /// <summary>Использовать ли running stats для inference.</summary>
    public bool TrackRunningStats { get; }

    /// <summary>Канальная ось во входном тензоре.</summary>
    public int ChannelAxis { get; }

    /// <summary>gamma (если affine).</summary>
    public Parameter Weight { get; }
    /// <summary>beta (если affine).</summary>
    public Parameter Bias { get; }

    private readonly Buffer _runningMean;
    private readonly Buffer _runningVar;

    /// <summary>Running mean.</summary>
    public Tensor RunningMean => _runningMean;
    /// <summary>Running var.</summary>
    public Tensor RunningVar => _runningVar;

    /// <summary>Создать BN.</summary>
    protected BatchNormBase(int numFeatures, int channelAxis, float eps, float momentum,
        bool affine, bool trackRunningStats)
    {
        NumFeatures = numFeatures;
        ChannelAxis = channelAxis;
        Eps = eps;
        Momentum = momentum;
        Affine = affine;
        TrackRunningStats = trackRunningStats;

        if (affine)
        {
            var w = Tensor.Empty(new Shape(numFeatures));
            Init.Constant_(w, 1f);
            Weight = RegisterParameter("weight", w);

            var b = Tensor.Empty(new Shape(numFeatures));
            Init.Zeros_(b);
            Bias = RegisterParameter("bias", b);
        }
        if (trackRunningStats)
        {
            _runningMean = RegisterBuffer("running_mean", Tensor.Zeros(new Shape(numFeatures)));
            _runningVar = RegisterBuffer("running_var", Tensor.Ones(new Shape(numFeatures)));
        }
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Rank <= ChannelAxis)
            throw new ArgumentException(
                $"BN: ожидается rank > {ChannelAxis}, фактически {input.Rank}.");
        if (input.Shape[ChannelAxis] != NumFeatures)
            throw new ArgumentException(
                $"BN: канал={input.Shape[ChannelAxis]}, ожидалось {NumFeatures}.");
        return BatchNorm.Apply(input, ChannelAxis, NumFeatures, Eps, Momentum,
            Weight?.Tensor, Bias?.Tensor, RunningMean, RunningVar, Training, TrackRunningStats);
    }
}

/// <summary>BatchNorm1d: вход (N, C) или (N, C, L).</summary>
public sealed class BatchNorm1d : BatchNormBase
{
    /// <summary>Создать BatchNorm1d.</summary>
    public BatchNorm1d(int numFeatures, float eps = 1e-5f, float momentum = 0.1f,
        bool affine = true, bool trackRunningStats = true)
        : base(numFeatures, channelAxis: 1, eps, momentum, affine, trackRunningStats) { }
}

/// <summary>BatchNorm2d: вход (N, C, H, W).</summary>
public sealed class BatchNorm2d : BatchNormBase
{
    /// <summary>Создать BatchNorm2d.</summary>
    public BatchNorm2d(int numFeatures, float eps = 1e-5f, float momentum = 0.1f,
        bool affine = true, bool trackRunningStats = true)
        : base(numFeatures, channelAxis: 1, eps, momentum, affine, trackRunningStats) { }
}

/// <summary>Низкоуровневый функциональный API для BN.</summary>
internal static class BatchNorm
{
    public static Tensor Apply(Tensor input, int channelAxis, int C, float eps, float momentum,
        Tensor weight, Tensor bias, Tensor runningMean, Tensor runningVar,
        bool training, bool trackRunningStats)
    {
        var x = input.Contiguous();
        int rank = x.Rank;
        // Размеры outer/inner относительно канальной оси.
        long outer = 1;
        for (int i = 0; i < channelAxis; i++) outer *= x.Shape[i];
        long inner = 1;
        for (int i = channelAxis + 1; i < rank; i++) inner *= x.Shape[i];
        long N = outer * inner;

        var y = Tensor.Empty(input.Shape, input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        ReadOnlySpan<float> ws = default, bs = default;
        if (weight != null) ws = weight.Contiguous().AsReadOnlySpan<float>();
        if (bias != null) bs = bias.Contiguous().AsReadOnlySpan<float>();
        bool affine = weight != null;

        // Stride по каналу (в элементах): inner.
        // Stride по outer: C * inner.
        var mean = new float[C];
        var rstd = new float[C];

        // Если eval-режим, но running stats отключены — используем batch-stats
        // (без обновления буферов): иначе нечем нормализовать.
        bool useBatchStats = training || !trackRunningStats;

        if (useBatchStats)
        {
            var var = new float[C];
            for (long o = 0; o < outer; o++)
            {
                long oOff = o * C * inner;
                for (int c = 0; c < C; c++)
                {
                    long off = oOff + c * inner;
                    for (long n = 0; n < inner; n++) mean[c] += xs[(int)(off + n)];
                }
            }
            for (int c = 0; c < C; c++) mean[c] /= N;

            for (long o = 0; o < outer; o++)
            {
                long oOff = o * C * inner;
                for (int c = 0; c < C; c++)
                {
                    long off = oOff + c * inner;
                    for (long n = 0; n < inner; n++)
                    {
                        float d = xs[(int)(off + n)] - mean[c];
                        var[c] += d * d;
                    }
                }
            }
            for (int c = 0; c < C; c++)
            {
                var[c] /= N;
                rstd[c] = 1f / MathF.Sqrt(var[c] + eps);
            }

            // Обновляем running stats только в обучении (не в eval-fallback).
            if (training && trackRunningStats && runningMean != null)
            {
                var rm = runningMean.AsSpan<float>();
                var rv = runningVar.AsSpan<float>();
                // Bessel's correction: unbiased var для running (PyTorch-стиль).
                float biasCorr = N > 1 ? (float)N / (N - 1) : 1f;
                for (int c = 0; c < C; c++)
                {
                    rm[c] = (1f - momentum) * rm[c] + momentum * mean[c];
                    rv[c] = (1f - momentum) * rv[c] + momentum * var[c] * biasCorr;
                }
            }
        }
        else
        {
            // eval-mode + trackRunningStats=true: используем буферы.
            if (runningMean == null)
                throw new InvalidOperationException("BN.eval требует runningMean/runningVar.");
            var rm = runningMean.AsReadOnlySpan<float>();
            var rv = runningVar.AsReadOnlySpan<float>();
            for (int c = 0; c < C; c++)
            {
                mean[c] = rm[c];
                rstd[c] = 1f / MathF.Sqrt(rv[c] + eps);
            }
        }

        // forward
        for (long o = 0; o < outer; o++)
        {
            long oOff = o * C * inner;
            for (int c = 0; c < C; c++)
            {
                long off = oOff + c * inner;
                float m = mean[c], rs = rstd[c];
                float w = affine ? ws[c] : 1f;
                float b = affine ? bs[c] : 0f;
                for (long n = 0; n < inner; n++)
                {
                    float xh = (xs[(int)(off + n)] - m) * rs;
                    ys[(int)(off + n)] = xh * w + b;
                }
            }
        }

        // Autograd только в training (eval — по running, без графа).
        bool requiresGrad = training && TapeContext.IsGradEnabled &&
                            (input.RequiresGrad ||
                             (weight?.RequiresGrad ?? false) ||
                             (bias?.RequiresGrad ?? false));
        if (requiresGrad)
        {
            var fn = new BNFunction(x, weight, bias, mean, rstd, C, (int)outer, (int)inner);
            fn.RegisterInput(input);
            if (weight != null) fn.RegisterInput(weight);
            if (bias != null) fn.RegisterInput(bias);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class BNFunction : Function
    {
        private readonly Tensor _x, _w, _b;
        private readonly float[] _mean;
        private readonly float[] _rstd;
        private readonly int _C, _outer, _inner;

        public BNFunction(Tensor x, Tensor w, Tensor b, float[] mean, float[] rstd, int C, int outer, int inner)
        {
            _x = x; _w = w; _b = b;
            _mean = mean; _rstd = rstd; _C = C; _outer = outer; _inner = inner;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var xs = _x.AsReadOnlySpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            ReadOnlySpan<float> ws = default;
            if (_w != null) ws = _w.Contiguous().AsReadOnlySpan<float>();
            bool affine = _w != null;
            long N = (long)_outer * _inner;

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

            // Per-channel: sum_gy, sum_gy_xh
            var sumGy = new float[_C];
            var sumGyXh = new float[_C];
            for (int o = 0; o < _outer; o++)
            {
                long oOff = (long)o * _C * _inner;
                for (int c = 0; c < _C; c++)
                {
                    long off = oOff + (long)c * _inner;
                    float m = _mean[c], rs = _rstd[c];
                    for (int n = 0; n < _inner; n++)
                    {
                        float xh = (xs[(int)(off + n)] - m) * rs;
                        float gy = gys[(int)(off + n)];
                        sumGy[c] += gy;
                        sumGyXh[c] += gy * xh;
                        if (!dWs.IsEmpty) dWs[c] += gy * xh;
                        if (!dBs.IsEmpty) dBs[c] += gy;
                    }
                }
            }

            for (int o = 0; o < _outer; o++)
            {
                long oOff = (long)o * _C * _inner;
                for (int c = 0; c < _C; c++)
                {
                    long off = oOff + (long)c * _inner;
                    float m = _mean[c], rs = _rstd[c];
                    float w = affine ? ws[c] : 1f;
                    for (int n = 0; n < _inner; n++)
                    {
                        float xh = (xs[(int)(off + n)] - m) * rs;
                        float gy = gys[(int)(off + n)];
                        // Стандартная формула BN: gx = (1/N) * w * rs * (N*gy - sum_gy - xh * sum_gy_xh)
                        dxs[(int)(off + n)] = (1f / N) * w * rs * (N * gy - sumGy[c] - xh * sumGyXh[c]);
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
}
