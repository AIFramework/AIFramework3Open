using System;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Полносвязный линейный слой: y = x @ W^T + b.
/// </summary>
/// <remarks>
/// <para>
/// Соглашение PyTorch: weight имеет форму (out_features, in_features).
/// Это позволяет хранить параметры в строках для cache-friendly индексирования
/// при инференсе.
/// </para>
/// <para>
/// Вход <c>input</c> может быть rank-2 (batch, in_features) или
/// rank-3 (batch, seq, in_features) — последний случай покрывает Transformer.
/// </para>
/// </remarks>
public sealed class Linear : Module
{
    /// <summary>Размер входа.</summary>
    public int InFeatures { get; }

    /// <summary>Размер выхода.</summary>
    public int OutFeatures { get; }

    /// <summary>Веса (out_features, in_features).</summary>
    public Parameter Weight { get; }

    /// <summary>Bias (out_features) или null если bias=false.</summary>
    public Parameter Bias { get; }

    // Кэшированный view W^T. View разделяет storage с Weight.Tensor, поэтому
    // обновления параметра оптимизатором отражаются автоматически. Сбрасывается
    // при смене Weight.Tensor (например, при To(device)) и при смене режима
    // grad/no-grad, чтобы корректно строилась autograd-цепочка.
    private Tensor _wtCache;
    private Tensor _wtCacheBase;
    private bool _wtCacheGrad;

    /// <summary>
    /// Создать Linear-слой.
    /// </summary>
    public Linear(int inFeatures, int outFeatures, bool bias = true, Random rng = null)
    {
        if (inFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(inFeatures));
        if (outFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(outFeatures));
        InFeatures = inFeatures;
        OutFeatures = outFeatures;

        var w = Tensor.Empty(new Shape(outFeatures, inFeatures));
        Init.KaimingUniform_(w, a: MathF.Sqrt(5f), rng: rng);
        Weight = RegisterParameter("weight", w);

        if (bias)
        {
            var b = Tensor.Empty(new Shape(outFeatures));
            float bound = 1f / MathF.Sqrt(inFeatures);
            Init.Uniform_(b, -bound, bound, rng);
            Bias = RegisterParameter("bias", b);
        }
    }

    private Tensor GetWeightT()
    {
        var w = Weight.Tensor;
        bool grad = Autograd.TapeContext.IsGradEnabled && w.RequiresGrad;
        if (!ReferenceEquals(_wtCacheBase, w) || _wtCacheGrad != grad)
        {
            _wtCacheBase = w;
            _wtCacheGrad = grad;
            _wtCache = w.Transpose(0, 1);
        }
        return _wtCache;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.Shape[input.Rank - 1] != InFeatures)
            throw new ArgumentException(
                $"Linear: последняя ось входа должна быть {InFeatures}, фактически {input.Shape[input.Rank - 1]}.");

        // y = x @ W^T. W: (out, in); W^T view: (in, out). Транспонированный view
        // кэшируется, чтобы не создавать новый Tensor-объект на каждом forward;
        // MatMul2D далее увидит IsTranspose2D и передаст transB=true в SGEMM
        // без копии данных.
        var wT = GetWeightT();
        Tensor y;
        if (input.Rank == 2)
        {
            y = input.MatMul(wT);
        }
        else
        {
            // (..., in) -> (B, in) -> matmul -> (B, out) -> (..., out)
            int last = input.Shape[input.Rank - 1];
            var dims = input.Shape.ToArray();
            int batch = 1;
            for (int i = 0; i < input.Rank - 1; i++) batch *= dims[i];
            var flat = input.Reshape(batch, last);
            var yFlat = flat.MatMul(wT);
            var outDims = new int[input.Rank];
            for (int i = 0; i < input.Rank - 1; i++) outDims[i] = dims[i];
            outDims[input.Rank - 1] = OutFeatures;
            y = yFlat.Reshape(outDims);
        }

        if (Bias != null)
            y = y + Bias.Tensor;
        return y;
    }

    /// <summary>
    /// Forward с fused GELU: y = gelu(x · W^T + b). Использует device-native
    /// fused-kernel при наличии (см. <see cref="OpCode.FusedLinearGelu"/>); иначе —
    /// прозрачный fallback на обычный <see cref="Forward(Tensor)"/> + GELU. Требуется
    /// <see cref="Bias"/> != null и rank-2 вход (или 3D, который flatten'ится).
    /// </summary>
    public Tensor ForwardGelu(Tensor input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (Bias == null) return TensorOps.Gelu(Forward(input));
        if (input.Shape[input.Rank - 1] != InFeatures)
            throw new ArgumentException(
                $"Linear.ForwardGelu: последняя ось входа должна быть {InFeatures}, фактически {input.Shape[input.Rank - 1]}.");

        if (input.DType != DType.Float32 || input.Device.Type == DeviceType.Cpu)
            return TensorOps.Gelu(Forward(input));

        var fused = OpRegistry.TryGet(OpCode.FusedLinearGelu, DType.Float32, input.Device);
        if (fused == null) return TensorOps.Gelu(Forward(input));

        // Flatten (..., in) -> (B, in) и обратно как в обычном Forward.
        Tensor flat;
        if (input.Rank == 2) flat = input;
        else
        {
            int batch = 1;
            for (int i = 0; i < input.Rank - 1; i++) batch *= input.Shape[i];
            flat = input.Reshape(batch, InFeatures);
        }
        var outs = fused(new[] { flat, Weight.Tensor, Bias.Tensor }, null);
        var y = outs[0];
        if (input.Rank != 2)
        {
            var outDims = new int[input.Rank];
            for (int i = 0; i < input.Rank - 1; i++) outDims[i] = input.Shape[i];
            outDims[input.Rank - 1] = OutFeatures;
            y = y.Reshape(outDims);
        }
        return y;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Linear(in={InFeatures}, out={OutFeatures}, bias={Bias != null})";
}
