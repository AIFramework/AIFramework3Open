using System;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Instance Normalization: нормализация по spatial-осям внутри каждого
/// (batch, channel). Эквивалентно <see cref="GroupNorm"/> с G=C.
/// </summary>
/// <remarks>
/// Часто используется в style-transfer и GAN. Работает с любым rank ≥ 3 (после
/// канальной оси идут произвольные spatial-оси). Здесь — реализация для (N, C, *).
/// </remarks>
public sealed class InstanceNorm : Module
{
    /// <summary>Число каналов.</summary>
    public int NumFeatures { get; }
    /// <summary>Эпсилон.</summary>
    public float Eps { get; }
    /// <summary>Аффинные gamma/beta.</summary>
    public bool Affine { get; }

    /// <summary>gamma (если affine).</summary>
    public Parameter Weight { get; }
    /// <summary>beta (если affine).</summary>
    public Parameter Bias { get; }

    /// <summary>Создать InstanceNorm.</summary>
    public InstanceNorm(int numFeatures, float eps = 1e-5f, bool affine = false)
    {
        if (numFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(numFeatures));
        NumFeatures = numFeatures;
        Eps = eps;
        Affine = affine;
        if (affine)
        {
            var w = Tensor.Empty(new Shape(numFeatures));
            Init.Constant_(w, 1f);
            Weight = RegisterParameter("weight", w);

            var b = Tensor.Empty(new Shape(numFeatures));
            Init.Zeros_(b);
            Bias = RegisterParameter("bias", b);
        }
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        GroupNorm.Apply(input, G: NumFeatures, C: NumFeatures, Eps,
            Weight?.Tensor, Bias?.Tensor);

    /// <inheritdoc/>
    public override string ToString() =>
        $"InstanceNorm(features={NumFeatures}, eps={Eps}, affine={Affine})";
}
