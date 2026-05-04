using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Nn;

namespace AI.ML.NeuralNetworks.V2.Optim;

/// <summary>
/// RMSProp оптимизатор. v_t = α v_{t-1} + (1-α) g². θ <- θ − lr · g / (√v + ε).
/// </summary>
public sealed class RMSProp : Optimizer
{
    /// <summary>α (decay).</summary>
    public float Alpha { get; }
    /// <summary>ε.</summary>
    public float Eps { get; }
    /// <summary>L2 weight decay.</summary>
    public float WeightDecay { get; }
    /// <summary>Momentum.</summary>
    public float Momentum { get; }
    /// <summary>Centered RMSProp (v − mean²).</summary>
    public bool Centered { get; }

    /// <summary>Создать RMSProp.</summary>
    public RMSProp(IEnumerable<Parameter> parameters, float lr = 1e-2f,
        float alpha = 0.99f, float eps = 1e-8f, float weightDecay = 0f,
        float momentum = 0f, bool centered = false)
        : base(parameters, lr)
    {
        Alpha = alpha; Eps = eps; WeightDecay = weightDecay;
        Momentum = momentum; Centered = centered;
    }

    /// <inheritdoc/>
    public override void Step()
    {
        StepCount++;
        foreach (var p in Parameters)
        {
            if (!GradHelpers.TryGetGrad(p, out var grad)) continue;
            var thetaT = OptimHostMirror.DownloadInplace(p.Tensor, out var commitTheta);
            var theta = thetaT.AsSpan<float>();
            var g = OptimHostMirror.DownloadReadOnly(grad.Contiguous()).AsReadOnlySpan<float>();
            Device sd = OptimHostMirror.StateDeviceForFallback(p.Tensor);
            var vT = GetOrCreateState(p, "square_avg",
                () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
            var v = vT.AsSpan<float>();
            Span<float> mean = default, mom = default;
            if (Centered)
            {
                var mT = GetOrCreateState(p, "grad_avg",
                    () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
                mean = mT.AsSpan<float>();
            }
            if (Momentum > 0f)
            {
                var bufT = GetOrCreateState(p, "momentum_buffer",
                    () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
                mom = bufT.AsSpan<float>();
            }

            for (int i = 0; i < theta.Length; i++)
            {
                float gi = g[i];
                if (WeightDecay != 0f) gi += WeightDecay * theta[i];
                v[i] = Alpha * v[i] + (1f - Alpha) * gi * gi;
                float denom;
                if (Centered)
                {
                    mean[i] = Alpha * mean[i] + (1f - Alpha) * gi;
                    denom = MathF.Sqrt(v[i] - mean[i] * mean[i]) + Eps;
                }
                else denom = MathF.Sqrt(v[i]) + Eps;

                if (Momentum > 0f)
                {
                    mom[i] = Momentum * mom[i] + gi / denom;
                    theta[i] -= LearningRate * mom[i];
                }
                else
                {
                    theta[i] -= LearningRate * gi / denom;
                }
            }
            commitTheta();
        }
    }
}

/// <summary>
/// Adagrad: накапливает квадраты градиентов; lr делится на √(sum_g²+ε).
/// </summary>
public sealed class Adagrad : Optimizer
{
    /// <summary>ε.</summary>
    public float Eps { get; }
    /// <summary>L2 weight decay.</summary>
    public float WeightDecay { get; }
    /// <summary>LR decay.</summary>
    public float LRDecay { get; }

    /// <summary>Создать Adagrad.</summary>
    public Adagrad(IEnumerable<Parameter> parameters, float lr = 1e-2f,
        float lrDecay = 0f, float weightDecay = 0f, float eps = 1e-10f)
        : base(parameters, lr)
    {
        Eps = eps; WeightDecay = weightDecay; LRDecay = lrDecay;
    }

    /// <inheritdoc/>
    public override void Step()
    {
        StepCount++;
        float lr = LearningRate / (1f + (StepCount - 1) * LRDecay);
        foreach (var p in Parameters)
        {
            if (!GradHelpers.TryGetGrad(p, out var grad)) continue;
            var thetaT = OptimHostMirror.DownloadInplace(p.Tensor, out var commitTheta);
            var theta = thetaT.AsSpan<float>();
            var g = OptimHostMirror.DownloadReadOnly(grad.Contiguous()).AsReadOnlySpan<float>();
            Device sd = OptimHostMirror.StateDeviceForFallback(p.Tensor);
            var sumT = GetOrCreateState(p, "sum",
                () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
            var sum = sumT.AsSpan<float>();
            for (int i = 0; i < theta.Length; i++)
            {
                float gi = g[i];
                if (WeightDecay != 0f) gi += WeightDecay * theta[i];
                sum[i] += gi * gi;
                theta[i] -= lr * gi / (MathF.Sqrt(sum[i]) + Eps);
            }
            commitTheta();
        }
    }
}

/// <summary>
/// Adadelta: scale-invariant adaptive learning rate.
/// </summary>
public sealed class Adadelta : Optimizer
{
    /// <summary>ρ.</summary>
    public float Rho { get; }
    /// <summary>ε.</summary>
    public float Eps { get; }
    /// <summary>Weight decay.</summary>
    public float WeightDecay { get; }

    /// <summary>Создать Adadelta.</summary>
    public Adadelta(IEnumerable<Parameter> parameters, float lr = 1f,
        float rho = 0.9f, float eps = 1e-6f, float weightDecay = 0f)
        : base(parameters, lr)
    {
        Rho = rho; Eps = eps; WeightDecay = weightDecay;
    }

    /// <inheritdoc/>
    public override void Step()
    {
        StepCount++;
        foreach (var p in Parameters)
        {
            if (!GradHelpers.TryGetGrad(p, out var grad)) continue;
            var thetaT = OptimHostMirror.DownloadInplace(p.Tensor, out var commitTheta);
            var theta = thetaT.AsSpan<float>();
            var g = OptimHostMirror.DownloadReadOnly(grad.Contiguous()).AsReadOnlySpan<float>();
            Device sd = OptimHostMirror.StateDeviceForFallback(p.Tensor);
            var sqAvgT = GetOrCreateState(p, "square_avg",
                () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
            var accDeltaT = GetOrCreateState(p, "acc_delta",
                () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
            var sqAvg = sqAvgT.AsSpan<float>();
            var accDelta = accDeltaT.AsSpan<float>();
            for (int i = 0; i < theta.Length; i++)
            {
                float gi = g[i];
                if (WeightDecay != 0f) gi += WeightDecay * theta[i];
                sqAvg[i] = Rho * sqAvg[i] + (1f - Rho) * gi * gi;
                float std = MathF.Sqrt(sqAvg[i] + Eps);
                float deltaStd = MathF.Sqrt(accDelta[i] + Eps);
                float delta = deltaStd / std * gi;
                accDelta[i] = Rho * accDelta[i] + (1f - Rho) * delta * delta;
                theta[i] -= LearningRate * delta;
            }
            commitTheta();
        }
    }
}
