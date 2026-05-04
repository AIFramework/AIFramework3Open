using System;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Position-wise FeedForward: <c>Linear -> activation -> Dropout -> Linear -> Dropout</c>.
/// </summary>
public sealed class FeedForward : Module
{
    /// <summary>Имя активации (нужно для копирования слоя).</summary>
    public string ActivationName { get; }
    /// <summary>Линейная экспансия.</summary>
    public Linear Up { get; }
    /// <summary>Обратное сжатие.</summary>
    public Linear Down { get; }
    /// <summary>Dropout после активации.</summary>
    public Dropout DropAct { get; }
    /// <summary>Dropout после второго Linear.</summary>
    public Dropout DropOut { get; }
    /// <summary>Активация (модуль, чтобы можно было заменить).</summary>
    public Module Activation { get; }

    /// <summary>Создать FF.</summary>
    public FeedForward(int dModel, int dFf, float dropout = 0.1f, string activation = "gelu",
        Random rng = null)
    {
        ActivationName = activation;
        Up = RegisterModule("up", new Linear(dModel, dFf, true, rng));
        Down = RegisterModule("down", new Linear(dFf, dModel, true, rng));
        DropAct = RegisterModule("drop_act", new Dropout(dropout, rng));
        DropOut = RegisterModule("drop_out", new Dropout(dropout, rng));
        Activation = RegisterModule("act", activation switch
        {
            "gelu" => (Module)new GELU(),
            "relu" => new ReLU(),
            "silu" or "swish" => new SiLU(),
            _ => throw new ArgumentException($"Неизвестная активация '{activation}'.")
        });
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        var h = Up.Forward(input);
        h = Activation.Forward(h);
        h = DropAct.Forward(h);
        h = Down.Forward(h);
        return DropOut.Forward(h);
    }
}

/// <summary>
/// Один слой Transformer-encoder: Self-Attention + FF с residual+LN.
/// </summary>
/// <remarks>
/// Поддерживает Pre-LN (по умолчанию) и Post-LN — параметром <see cref="NormFirst"/>.
/// Pre-LN чаще даёт стабильное обучение.
/// </remarks>
public sealed class TransformerEncoderLayer : Module
{
    /// <summary>Self-attention.</summary>
    public MultiHeadAttention SelfAttn { get; }
    /// <summary>FFN.</summary>
    public FeedForward FFN { get; }
    /// <summary>Норма перед/после attention.</summary>
    public LayerNorm Norm1 { get; }
    /// <summary>Норма перед/после FFN.</summary>
    public LayerNorm Norm2 { get; }
    /// <summary>Dropout после residual.</summary>
    public Dropout Dropout1 { get; }
    /// <summary>Dropout после FFN residual.</summary>
    public Dropout Dropout2 { get; }
    /// <summary>Использовать Pre-LN (true) или Post-LN (false).</summary>
    public bool NormFirst { get; }
    /// <summary>Доля dropout (для копирования слоя).</summary>
    public float DropoutP { get; }
    /// <summary>Имя активации (для копирования слоя).</summary>
    public string ActivationName { get; }

    /// <summary>Создать слой.</summary>
    public TransformerEncoderLayer(int dModel, int nHead, int dimFeedforward = 2048,
        float dropout = 0.1f, string activation = "gelu", bool normFirst = true,
        Random rng = null)
    {
        DropoutP = dropout;
        ActivationName = activation;
        SelfAttn = RegisterModule("self_attn",
            new MultiHeadAttention(dModel, nHead, dropout, true, rng));
        FFN = RegisterModule("ffn",
            new FeedForward(dModel, dimFeedforward, dropout, activation, rng));
        Norm1 = RegisterModule("norm1", new LayerNorm(dModel));
        Norm2 = RegisterModule("norm2", new LayerNorm(dModel));
        Dropout1 = RegisterModule("dropout1", new Dropout(dropout, rng));
        Dropout2 = RegisterModule("dropout2", new Dropout(dropout, rng));
        NormFirst = normFirst;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => Forward(input, srcMask: null, isCausal: false);

    /// <summary>Forward с опциональной маской.</summary>
    public Tensor Forward(Tensor input, Tensor srcMask, bool isCausal)
    {
        if (NormFirst)
        {
            var h = Norm1.Forward(input);
            var attn = SelfAttn.SelfAttention(h, srcMask, isCausal);
            input = input + Dropout1.Forward(attn);

            h = Norm2.Forward(input);
            var ff = FFN.Forward(h);
            input = input + Dropout2.Forward(ff);
        }
        else
        {
            var attn = SelfAttn.SelfAttention(input, srcMask, isCausal);
            input = Norm1.Forward(input + Dropout1.Forward(attn));
            var ff = FFN.Forward(input);
            input = Norm2.Forward(input + Dropout2.Forward(ff));
        }
        return input;
    }
}

/// <summary>
/// Один слой Transformer-decoder: Self-Attn + Cross-Attn + FF с residual+LN.
/// </summary>
public sealed class TransformerDecoderLayer : Module
{
    /// <summary>Self-attention с causal-маской.</summary>
    public MultiHeadAttention SelfAttn { get; }
    /// <summary>Cross-attention над encoder-памятью.</summary>
    public MultiHeadAttention CrossAttn { get; }
    /// <summary>FFN.</summary>
    public FeedForward FFN { get; }
    /// <summary>Норма перед self-attn.</summary>
    public LayerNorm Norm1 { get; }
    /// <summary>Норма перед cross-attn.</summary>
    public LayerNorm Norm2 { get; }
    /// <summary>Норма перед FFN.</summary>
    public LayerNorm Norm3 { get; }
    /// <summary>Dropout self-attn.</summary>
    public Dropout Dropout1 { get; }
    /// <summary>Dropout cross-attn.</summary>
    public Dropout Dropout2 { get; }
    /// <summary>Dropout FFN.</summary>
    public Dropout Dropout3 { get; }
    /// <summary>Pre-LN или Post-LN.</summary>
    public bool NormFirst { get; }
    /// <summary>Доля dropout (для копирования слоя).</summary>
    public float DropoutP { get; }
    /// <summary>Имя активации (для копирования слоя).</summary>
    public string ActivationName { get; }

    /// <summary>Создать decoder-слой.</summary>
    public TransformerDecoderLayer(int dModel, int nHead, int dimFeedforward = 2048,
        float dropout = 0.1f, string activation = "gelu", bool normFirst = true,
        Random rng = null)
    {
        DropoutP = dropout;
        ActivationName = activation;
        SelfAttn = RegisterModule("self_attn",
            new MultiHeadAttention(dModel, nHead, dropout, true, rng));
        CrossAttn = RegisterModule("cross_attn",
            new MultiHeadAttention(dModel, nHead, dropout, true, rng));
        FFN = RegisterModule("ffn",
            new FeedForward(dModel, dimFeedforward, dropout, activation, rng));
        Norm1 = RegisterModule("norm1", new LayerNorm(dModel));
        Norm2 = RegisterModule("norm2", new LayerNorm(dModel));
        Norm3 = RegisterModule("norm3", new LayerNorm(dModel));
        Dropout1 = RegisterModule("dropout1", new Dropout(dropout, rng));
        Dropout2 = RegisterModule("dropout2", new Dropout(dropout, rng));
        Dropout3 = RegisterModule("dropout3", new Dropout(dropout, rng));
        NormFirst = normFirst;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        throw new InvalidOperationException(
            "TransformerDecoderLayer.Forward требует memory-аргумент. Используйте перегрузку с (tgt, memory).");
    }

    /// <summary>Forward decoder-слоя.</summary>
    public Tensor Forward(Tensor tgt, Tensor memory, Tensor tgtMask = null,
        Tensor memMask = null, bool tgtCausal = true)
    {
        Tensor x = tgt;
        if (NormFirst)
        {
            var h = Norm1.Forward(x);
            x = x + Dropout1.Forward(SelfAttn.SelfAttention(h, tgtMask, tgtCausal));

            h = Norm2.Forward(x);
            x = x + Dropout2.Forward(CrossAttn.ForwardMHA(h, memory, memory, memMask, false));

            h = Norm3.Forward(x);
            x = x + Dropout3.Forward(FFN.Forward(h));
        }
        else
        {
            x = Norm1.Forward(x + Dropout1.Forward(SelfAttn.SelfAttention(x, tgtMask, tgtCausal)));
            x = Norm2.Forward(x + Dropout2.Forward(CrossAttn.ForwardMHA(x, memory, memory, memMask, false)));
            x = Norm3.Forward(x + Dropout3.Forward(FFN.Forward(x)));
        }
        return x;
    }
}

/// <summary>
/// Стек из <see cref="TransformerEncoderLayer"/> с финальной нормой.
/// </summary>
public sealed class TransformerEncoder : Module
{
    /// <summary>Слои.</summary>
    public ModuleList Layers { get; }
    /// <summary>Финальная норма (опционально).</summary>
    public LayerNorm FinalNorm { get; }

    /// <summary>Создать стек.</summary>
    public TransformerEncoder(TransformerEncoderLayer prototype, int numLayers,
        LayerNorm finalNorm = null, Random rng = null)
    {
        if (prototype == null) throw new ArgumentNullException(nameof(prototype));
        if (numLayers <= 0) throw new ArgumentOutOfRangeException(nameof(numLayers));
        Layers = RegisterModule("layers", new ModuleList());
        for (int i = 0; i < numLayers; i++)
        {
            // Создаём независимую копию слоя (новые параметры) с теми же гиперпараметрами,
            // включая activation/dropout/rng — иначе клоны теряют конфигурацию прототипа.
            var l = new TransformerEncoderLayer(
                dModel: prototype.SelfAttn.EmbedDim,
                nHead: prototype.SelfAttn.NumHeads,
                dimFeedforward: prototype.FFN.Up.OutFeatures,
                dropout: prototype.DropoutP,
                activation: prototype.ActivationName,
                normFirst: prototype.NormFirst,
                rng: rng);
            Layers.Add(l);
        }
        if (finalNorm != null) FinalNorm = RegisterModule("final_norm", finalNorm);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => Forward(input, mask: null, isCausal: false);

    /// <summary>Forward через все слои.</summary>
    public Tensor Forward(Tensor input, Tensor mask, bool isCausal)
    {
        var x = input;
        for (int i = 0; i < Layers.Count; i++)
            x = ((TransformerEncoderLayer)Layers[i]).Forward(x, mask, isCausal);
        if (FinalNorm != null) x = FinalNorm.Forward(x);
        return x;
    }
}

/// <summary>
/// Стек из <see cref="TransformerDecoderLayer"/> с финальной нормой.
/// </summary>
public sealed class TransformerDecoder : Module
{
    /// <summary>Слои.</summary>
    public ModuleList Layers { get; }
    /// <summary>Финальная норма (опционально).</summary>
    public LayerNorm FinalNorm { get; }

    /// <summary>Создать стек.</summary>
    public TransformerDecoder(TransformerDecoderLayer prototype, int numLayers,
        LayerNorm finalNorm = null, Random rng = null)
    {
        if (prototype == null) throw new ArgumentNullException(nameof(prototype));
        if (numLayers <= 0) throw new ArgumentOutOfRangeException(nameof(numLayers));
        Layers = RegisterModule("layers", new ModuleList());
        for (int i = 0; i < numLayers; i++)
        {
            var l = new TransformerDecoderLayer(
                dModel: prototype.SelfAttn.EmbedDim,
                nHead: prototype.SelfAttn.NumHeads,
                dimFeedforward: prototype.FFN.Up.OutFeatures,
                dropout: prototype.DropoutP,
                activation: prototype.ActivationName,
                normFirst: prototype.NormFirst,
                rng: rng);
            Layers.Add(l);
        }
        if (finalNorm != null) FinalNorm = RegisterModule("final_norm", finalNorm);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        throw new InvalidOperationException(
            "TransformerDecoder.Forward требует memory. Используйте Forward(tgt, memory, ...).");
    }

    /// <summary>Forward decoder.</summary>
    public Tensor Forward(Tensor tgt, Tensor memory, Tensor tgtMask = null,
        Tensor memMask = null, bool tgtCausal = true)
    {
        var x = tgt;
        for (int i = 0; i < Layers.Count; i++)
            x = ((TransformerDecoderLayer)Layers[i]).Forward(x, memory, tgtMask, memMask, tgtCausal);
        if (FinalNorm != null) x = FinalNorm.Forward(x);
        return x;
    }
}
