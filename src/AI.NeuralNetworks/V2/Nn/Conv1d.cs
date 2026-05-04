using System;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// 1D-свёртка над последовательностями. Вход (N, C_in, L), выход (N, C_out, L_out).
/// </summary>
/// <remarks>
/// Реализована как обёртка вокруг <see cref="Conv2d"/> с kH=1: Conv1d на CPU
/// и GPU будет работать через те же оптимизации, что и 2D, без дублирования логики.
/// </remarks>
public sealed class Conv1d : Module
{
    /// <summary>Каналы входа.</summary>
    public int InChannels { get; }
    /// <summary>Каналы выхода.</summary>
    public int OutChannels { get; }
    /// <summary>Размер ядра.</summary>
    public int KernelSize { get; }
    /// <summary>Шаг.</summary>
    public int Stride { get; }
    /// <summary>Padding.</summary>
    public int Padding { get; }
    /// <summary>Dilation.</summary>
    public int Dilation { get; }
    /// <summary>Groups.</summary>
    public int Groups { get; }

    private readonly Conv2d _inner;

    /// <summary>weight (C_out, C_in/groups, K).</summary>
    public Parameter Weight => _inner.Weight; // shape (C_out, C_in/groups, 1, K) под капотом
    /// <summary>bias (C_out).</summary>
    public Parameter Bias => _inner.Bias;

    /// <summary>Создать Conv1d.</summary>
    public Conv1d(int inChannels, int outChannels, int kernelSize,
        int stride = 1, int padding = 0, int dilation = 1, int groups = 1, bool bias = true,
        Random rng = null)
    {
        InChannels = inChannels; OutChannels = outChannels; KernelSize = kernelSize;
        Stride = stride; Padding = padding; Dilation = dilation; Groups = groups;
        _inner = RegisterModule("c2d", new Conv2d(
            inChannels, outChannels,
            (1, kernelSize), (1, stride), (0, padding), (1, dilation), groups, bias, rng));
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Rank != 3)
            throw new ArgumentException("Conv1d: вход (N, C, L).");
        // (N, C, L) -> (N, C, 1, L)
        var x4 = input.Unsqueeze(2);
        var y4 = _inner.Forward(x4);
        // (N, C_out, 1, L_out) -> (N, C_out, L_out)
        return y4.Squeeze(2);
    }
}
