using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Nn;

namespace AI.ML.NeuralNetworks.V2.Optim;

/// <summary>
/// SGD с momentum/nesterov/weight_decay/dampening (PyTorch-совместимая формула).
/// </summary>
/// <remarks>
/// <para>v_{t+1} = μ · v_t + (1 − τ) · g; θ <- θ − lr · (g_nesterov ? g + μ·v : v).</para>
/// </remarks>
public sealed class SGD : Optimizer
{
    /// <summary>Коэффициент momentum.</summary>
    public float Momentum { get; }
    /// <summary>L2 weight decay.</summary>
    public float WeightDecay { get; }
    /// <summary>Dampening τ.</summary>
    public float Dampening { get; }
    /// <summary>Использовать Nesterov.</summary>
    public bool Nesterov { get; }

    /// <summary>Создать SGD-оптимизатор.</summary>
    public SGD(IEnumerable<Parameter> parameters, float lr,
        float momentum = 0f, float weightDecay = 0f, float dampening = 0f, bool nesterov = false)
        : base(parameters, lr)
    {
        if (nesterov && (momentum <= 0 || dampening != 0))
            throw new ArgumentException("Nesterov требует momentum>0 и dampening=0.");
        Momentum = momentum; WeightDecay = weightDecay; Dampening = dampening; Nesterov = nesterov;
    }

    /// <inheritdoc/>
    public override void Step()
    {
        StepCount++;
        foreach (var p in Parameters)
        {
            if (!GradHelpers.TryGetGrad(p, out var grad)) continue;
            // Mirror на CPU для безопасного in-place AsSpan на любом устройстве.
            var thetaT = OptimHostMirror.DownloadInplace(p.Tensor, out var commitTheta);
            var theta = thetaT.AsSpan<float>();
            var g = OptimHostMirror.DownloadReadOnly(grad.Contiguous()).AsReadOnlySpan<float>();
            float wd = WeightDecay;
            float mu = Momentum;
            float tau = Dampening;
            bool useMomentum = mu > 0f;
            Span<float> v = default;
            if (useMomentum)
            {
                Device sd = OptimHostMirror.StateDeviceForFallback(p.Tensor);
                var vT = GetOrCreateState(p, "momentum_buffer",
                    () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
                v = vT.AsSpan<float>();
            }
            for (int i = 0; i < theta.Length; i++)
            {
                float gi = g[i];
                if (wd != 0f) gi = gi + wd * theta[i];
                float update;
                if (useMomentum)
                {
                    v[i] = mu * v[i] + (1f - tau) * gi;
                    update = Nesterov ? gi + mu * v[i] : v[i];
                }
                else update = gi;
                theta[i] -= LearningRate * update;
            }
            commitTheta();
        }
    }
}
