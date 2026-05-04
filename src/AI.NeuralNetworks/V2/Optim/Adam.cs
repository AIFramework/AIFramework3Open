using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Optim;

/// <summary>
/// Семейство Adam: классический Adam, AdamW (decoupled weight decay), NAdam, RAdam, Adamax.
/// </summary>
/// <remarks>
/// Все варианты делят общую логику моментов m, v и шаг bias-correction.
/// Конкретный вариант определяется параметром <see cref="Variant"/> и
/// дополнительно <see cref="DecoupledWeightDecay"/>.
/// </remarks>
public sealed class Adam : Optimizer
{
    /// <summary>Вариант Adam.</summary>
    public AdamVariant Variant { get; }
    /// <summary>Использовать decoupled weight decay (как AdamW).</summary>
    public bool DecoupledWeightDecay { get; }
    /// <summary>β₁.</summary>
    public float Beta1 { get; }
    /// <summary>β₂.</summary>
    public float Beta2 { get; }
    /// <summary>ε.</summary>
    public float Eps { get; }
    /// <summary>Weight decay.</summary>
    public float WeightDecay { get; }
    /// <summary>
    /// Включить AMSGrad (Reddi et al., 2018): использовать v̂ = max(v̂_prev, v)
    /// вместо v как знаменатель шага. Гарантирует невозрастающий effective LR
    /// и устраняет знаменатель из контр-примера сходимости Adam.
    /// </summary>
    public bool AmsGrad { get; }

    /// <summary>Создать Adam-style оптимизатор.</summary>
    public Adam(IEnumerable<Parameter> parameters, float lr = 1e-3f,
        float beta1 = 0.9f, float beta2 = 0.999f, float eps = 1e-8f,
        float weightDecay = 0f, AdamVariant variant = AdamVariant.Adam,
        bool decoupledWeightDecay = false, bool amsGrad = false)
        : base(parameters, lr)
    {
        if (beta1 < 0 || beta1 >= 1) throw new ArgumentOutOfRangeException(nameof(beta1));
        if (beta2 < 0 || beta2 >= 1) throw new ArgumentOutOfRangeException(nameof(beta2));
        Beta1 = beta1; Beta2 = beta2; Eps = eps;
        WeightDecay = weightDecay;
        Variant = variant;
        DecoupledWeightDecay = decoupledWeightDecay;
        AmsGrad = amsGrad;
    }

    /// <summary>Удобная фабрика AdamW.</summary>
    public static Adam AdamW(IEnumerable<Parameter> parameters, float lr = 1e-3f,
        float beta1 = 0.9f, float beta2 = 0.999f, float eps = 1e-8f,
        float weightDecay = 1e-2f, bool amsGrad = false)
        => new Adam(parameters, lr, beta1, beta2, eps, weightDecay,
            AdamVariant.Adam, decoupledWeightDecay: true, amsGrad: amsGrad);

    /// <inheritdoc/>
    public override void Step()
    {
        StepCount++;
        int t = StepCount;
        float bc1 = 1f - MathF.Pow(Beta1, t);
        float bc2 = 1f - MathF.Pow(Beta2, t);

        foreach (var p in Parameters)
        {
            if (!GradHelpers.TryGetGrad(p, out var grad)) continue;

            // Fast-path: native FusedAdamW kernel на не-CPU устройстве. Условия:
            //   * variant=Adam, !AmsGrad, Float32;
            //   * либо DecoupledWeightDecay=true (родная формула AdamW),
            //   * либо WeightDecay=0 (тогда AdamW и classic Adam математически
            //     идентичны: theta -= lr·m̂/(√v̂+eps)).
            // Это покрывает самый частый сценарий — `new Adam(params, lr)` на GPU.
            if (Variant == AdamVariant.Adam && !AmsGrad
                && p.Tensor.DType == DType.Float32
                && p.Tensor.Device.Type != DeviceType.Cpu
                && (DecoupledWeightDecay || WeightDecay == 0f))
            {
                var fused = OpRegistry.TryGet(OpCode.FusedAdamW, DType.Float32, p.Tensor.Device);
                if (fused != null)
                {
                    var mGpu = GetOrCreateState(p, "m",
                        () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, p.Tensor.Device));
                    var vGpu = GetOrCreateState(p, "v",
                        () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, p.Tensor.Device));
                    var gContig = grad.IsContiguous ? grad : grad.Contiguous();
                    var attrs = new FusedAdamWAttrs(
                        LearningRate, Beta1, Beta2, Eps, WeightDecay, bc1, bc2);
                    fused(new[] { p.Tensor, gContig, mGpu, vGpu }, attrs);
                    continue;
                }
            }

            // CPU-loop путь. Если параметр на устройстве (GPU) — скачиваем его, grad
            // и state на CPU, выполняем шаг и выгружаем параметр обратно. State-тензоры
            // моментов аллоцируются сразу на CPU (см. OptimHostMirror.StateDeviceForFallback)
            // и больше не катаются между устройствами.
            var thetaT = OptimHostMirror.DownloadInplace(p.Tensor, out var commitTheta);
            var theta = thetaT.AsSpan<float>();
            var g = OptimHostMirror.DownloadReadOnly(grad.Contiguous()).AsReadOnlySpan<float>();
            Device sd = OptimHostMirror.StateDeviceForFallback(p.Tensor);
            var mT = GetOrCreateState(p, "m",
                () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
            var vT = GetOrCreateState(p, "v",
                () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
            var m = mT.AsSpan<float>();
            var v = vT.AsSpan<float>();
            // AmsGrad-only: running max(v̂).
            Span<float> vmax = default;
            if (AmsGrad && Variant != AdamVariant.Adamax)
            {
                var vmaxT = GetOrCreateState(p, "vmax",
                    () => Tensor.Zeros(p.Tensor.Shape, p.Tensor.DType, sd));
                vmax = vmaxT.AsSpan<float>();
            }

            for (int i = 0; i < theta.Length; i++)
            {
                float gi = g[i];

                // L2 weight decay (Adam-style)
                if (!DecoupledWeightDecay && WeightDecay != 0f)
                    gi += WeightDecay * theta[i];

                m[i] = Beta1 * m[i] + (1f - Beta1) * gi;
                if (Variant == AdamVariant.Adamax)
                {
                    // u = max(β₂·u, |g|)
                    v[i] = MathF.Max(Beta2 * v[i], MathF.Abs(gi) + Eps);
                }
                else
                {
                    v[i] = Beta2 * v[i] + (1f - Beta2) * gi * gi;
                }

                float mHat = m[i] / bc1;
                float update;

                // Эффективное v для построения знаменателя.
                // AMSGrad: ведём v̂_t = max(v̂_{t-1}, v_t), которое монотонно
                // не убывает, и используем именно его (без bias-correction —
                // как в оригинальной статье).
                float vEff;
                if (AmsGrad && Variant != AdamVariant.Adamax)
                {
                    if (v[i] > vmax[i]) vmax[i] = v[i];
                    vEff = vmax[i];
                }
                else
                {
                    vEff = v[i];
                }

                switch (Variant)
                {
                    case AdamVariant.Adam:
                        update = AmsGrad
                            ? mHat / (MathF.Sqrt(vEff) + Eps)
                            : mHat / (MathF.Sqrt(vEff / bc2) + Eps);
                        break;

                    case AdamVariant.NAdam:
                        // Nesterov-Adam: m_nadam = β₁ * m_hat + (1-β₁) * g / bc1
                        float mNadam = Beta1 * mHat + (1f - Beta1) * gi / bc1;
                        update = AmsGrad
                            ? mNadam / (MathF.Sqrt(vEff) + Eps)
                            : mNadam / (MathF.Sqrt(vEff / bc2) + Eps);
                        break;

                    case AdamVariant.RAdam:
                        // Rectified Adam: when variance estimator is reliable, use Adam;
                        // otherwise — SGD with momentum.
                        float rhoInf = 2f / (1f - Beta2) - 1f;
                        float rhoT = rhoInf - 2f * t * MathF.Pow(Beta2, t) / bc2;
                        if (rhoT > 4f)
                        {
                            float rt = MathF.Sqrt((rhoT - 4f) * (rhoT - 2f) * rhoInf /
                                                  ((rhoInf - 4f) * (rhoInf - 2f) * rhoT));
                            update = AmsGrad
                                ? rt * mHat / (MathF.Sqrt(vEff) + Eps)
                                : rt * mHat / (MathF.Sqrt(vEff / bc2) + Eps);
                        }
                        else
                        {
                            update = mHat;
                        }
                        break;

                    case AdamVariant.Adamax:
                        // Adamax не использует AMSGrad: vEff здесь = v[i] (∞-норма).
                        update = mHat / v[i];
                        break;

                    default: throw new InvalidOperationException();
                }

                if (DecoupledWeightDecay && WeightDecay != 0f)
                    theta[i] -= LearningRate * (update + WeightDecay * theta[i]);
                else
                    theta[i] -= LearningRate * update;
            }
            commitTheta();
        }
    }
}

/// <summary>Конкретный вариант Adam-семейства.</summary>
public enum AdamVariant
{
    /// <summary>Классический Adam (Kingma &amp; Ba, 2014).</summary>
    Adam,
    /// <summary>Nesterov-Adam (Dozat, 2016).</summary>
    NAdam,
    /// <summary>Rectified Adam (Liu et al., 2019).</summary>
    RAdam,
    /// <summary>Adamax — Adam с ∞-нормой.</summary>
    Adamax,
}
