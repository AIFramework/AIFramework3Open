using System;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Scaled Dot-Product Attention: softmax((Q K^T) / sqrt(d)) V.
/// </summary>
public static class ScaledDotProductAttention
{
    /// <summary>
    /// Применить SDPA. Q/K/V форм (..., L_q, d) и (..., L_k, d).
    /// Поддерживается batched-mode: ведущие оси трактуются как батч.
    /// </summary>
    /// <param name="query">(..., Lq, d).</param>
    /// <param name="key">(..., Lk, d).</param>
    /// <param name="value">(..., Lk, dv).</param>
    /// <param name="attnMask">Опциональная additive-маска: <c>0</c> — пропустить,
    /// большое отрицательное (<c>-inf</c>) — занулить attention. Добавляется к scores.
    /// Маска должна быть broadcast-compatible с формой scores
    /// (<c>(..., Lq, Lk)</c>).</param>
    /// <param name="isCausal">Если true, применяет causal-маску (Lq=Lk).</param>
    /// <param name="dropoutP">Dropout на attention-весах (0 — выкл).</param>
    /// <param name="training">Применять dropout только в training-режиме.</param>
    /// <param name="rng">RNG для dropout-маски.</param>
    public static Tensor Apply(Tensor query, Tensor key, Tensor value,
        Tensor attnMask = null, bool isCausal = false, float dropoutP = 0f,
        bool training = true, Random rng = null)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (query.Rank < 3 || key.Rank < 3 || value.Rank < 3)
            throw new ArgumentException("SDPA: rank Q/K/V должны быть ≥ 3.");
        int d = query.Shape[query.Rank - 1];
        int dk = key.Shape[key.Rank - 1];
        int Lq = query.Shape[query.Rank - 2];
        int Lk = key.Shape[key.Rank - 2];
        if (d != dk) throw new ArgumentException("SDPA: dim(Q) должен совпадать с dim(K).");
        if (value.Shape[value.Rank - 2] != Lk)
            throw new ArgumentException("SDPA: длина K и V должна совпадать.");

        var kT = key.Transpose(key.Rank - 2, key.Rank - 1); // (..., d, Lk)
        var scores = query.MatMul(kT) * (1f / MathF.Sqrt(d));

        if (isCausal)
        {
            if (Lq != Lk)
                throw new ArgumentException("Causal-маска требует Lq == Lk.");
            scores = scores + CausalMask(Lq, scores.DType, scores.Device);
        }
        if (attnMask != null) scores = scores + attnMask;

        var attn = scores.Softmax(-1);
        if (training && dropoutP > 0f)
        {
            var mask = Tensor.Empty(attn.Shape, attn.DType, Device.Cpu);
            var ms = mask.AsSpan<float>();
            float keep = 1f - dropoutP;
            float invKeep = 1f / keep;
            var r = rng ?? Random.Shared;
            lock (r)
                for (int i = 0; i < ms.Length; i++)
                    ms[i] = r.NextDouble() < keep ? invKeep : 0f;
            if (attn.Device.Type != DeviceType.Cpu) mask = mask.To(attn.Device);
            attn = attn * mask;
        }

        return attn.MatMul(value);
    }

    /// <summary>
    /// Создать additive causal-маску (L×L) с -inf над диагональю.
    /// Поддерживает Float32/Float64; для интегральных типов бросает.
    /// </summary>
    public static Tensor CausalMask(int L, DType dt, Device dev)
    {
        if (L <= 0) throw new ArgumentOutOfRangeException(nameof(L));
        var m = Tensor.Empty(new Shape(L, L), dt, Device.Cpu);
        switch (dt)
        {
            case DType.Float32:
            {
                var s = m.AsSpan<float>();
                for (int i = 0; i < L; i++)
                    for (int j = 0; j < L; j++)
                        s[i * L + j] = j > i ? float.NegativeInfinity : 0f;
                break;
            }
            case DType.Float64:
            {
                var s = m.AsSpan<double>();
                for (int i = 0; i < L; i++)
                    for (int j = 0; j < L; j++)
                        s[i * L + j] = j > i ? double.NegativeInfinity : 0.0;
                break;
            }
            default:
                throw new NotSupportedException(
                    $"CausalMask: dtype {dt} не поддерживается (нужны Float32/Float64 для -inf).");
        }
        if (dev.Type != DeviceType.Cpu) m = m.To(dev);
        return m;
    }
}

/// <summary>
/// MultiHead Attention.
/// </summary>
/// <remarks>
/// <para>
/// Стандартная PyTorch-форма:
/// <list type="bullet">
///   <item>Q/K/V проецируются через <see cref="Linear"/> до <see cref="EmbedDim"/>.</item>
///   <item>Затем re-shape в (B, T, H, dh) и transpose в (B, H, T, dh).</item>
///   <item>SDPA по (B, H, T, dh).</item>
///   <item>Concat heads и output-проекция.</item>
/// </list>
/// </para>
/// <para>
/// Поддерживает <see cref="KVCache"/>: при инкрементальном decoding вы передаёте
/// предыдущий cache, и attention автоматически склеивает новые K/V с прошлыми.
/// </para>
/// </remarks>
public sealed class MultiHeadAttention : Module
{
    /// <summary>Размер эмбеддинга (= heads * head_dim).</summary>
    public int EmbedDim { get; }
    /// <summary>Число голов.</summary>
    public int NumHeads { get; }
    /// <summary>Размер на голову.</summary>
    public int HeadDim { get; }
    /// <summary>Dropout на attention-весах.</summary>
    public float Dropout { get; }
    /// <summary>Использовать ли bias в проекциях.</summary>
    public bool Bias { get; }

    /// <summary>Q-проекция.</summary>
    public Linear QProj { get; }
    /// <summary>K-проекция.</summary>
    public Linear KProj { get; }
    /// <summary>V-проекция.</summary>
    public Linear VProj { get; }
    /// <summary>Output-проекция.</summary>
    public Linear OutProj { get; }

    /// <summary>Создать MHA.</summary>
    public MultiHeadAttention(int embedDim, int numHeads, float dropout = 0f, bool bias = true,
        Random rng = null)
    {
        if (embedDim % numHeads != 0)
            throw new ArgumentException("embedDim должен делиться на numHeads.");
        EmbedDim = embedDim;
        NumHeads = numHeads;
        HeadDim = embedDim / numHeads;
        Dropout = dropout;
        Bias = bias;
        QProj = RegisterModule("q_proj", new Linear(embedDim, embedDim, bias, rng));
        KProj = RegisterModule("k_proj", new Linear(embedDim, embedDim, bias, rng));
        VProj = RegisterModule("v_proj", new Linear(embedDim, embedDim, bias, rng));
        OutProj = RegisterModule("out_proj", new Linear(embedDim, embedDim, bias, rng));
    }

    /// <summary>
    /// Forward MHA. <paramref name="query"/>/<paramref name="key"/>/<paramref name="value"/>
    /// формы (B, T, E). Маска: (Lq, Lk) или (B, H, Lq, Lk) или null.
    /// </summary>
    public Tensor ForwardMHA(Tensor query, Tensor key, Tensor value,
        Tensor attnMask = null, bool isCausal = false, KVCache cache = null,
        Random rng = null)
    {
        int B = query.Shape[0], Lq = query.Shape[1];
        int Lk = key.Shape[1];

        Tensor Q, K, V;

        if (ReferenceEquals(query, key) && ReferenceEquals(key, value))
        {
            // Self-attention: fused QKV — один MatMul вместо трёх.
            var qkvW = IndexingOps.Cat(new Tensor[]
                { QProj.Weight.Tensor, KProj.Weight.Tensor, VProj.Weight.Tensor }, axis: 0);
            var flat = query.Reshape(B * Lq, EmbedDim);
            var qkvOut = flat.MatMul(qkvW.Transpose(0, 1));
            if (QProj.Bias != null)
            {
                var qkvB = IndexingOps.Cat(new Tensor[]
                    { QProj.Bias.Tensor, KProj.Bias.Tensor, VProj.Bias.Tensor }, axis: 0);
                qkvOut = qkvOut + qkvB;
            }
            qkvOut = qkvOut.Reshape(B, Lq, 3 * EmbedDim);
            Q = IndexingOps.Narrow(qkvOut, 2, 0, EmbedDim).Contiguous();
            K = IndexingOps.Narrow(qkvOut, 2, EmbedDim, EmbedDim).Contiguous();
            V = IndexingOps.Narrow(qkvOut, 2, 2 * EmbedDim, EmbedDim).Contiguous();
        }
        else
        {
            Q = QProj.Forward(query);
            K = KProj.Forward(key);
            V = VProj.Forward(value);
        }

        // (B, L, E) -> (B, L, H, dh) -> (B, H, L, dh) -> contiguous.
        Q = Q.Reshape(B, Lq, NumHeads, HeadDim).Permute(0, 2, 1, 3).Contiguous();
        K = K.Reshape(B, Lk, NumHeads, HeadDim).Permute(0, 2, 1, 3).Contiguous();
        V = V.Reshape(B, Lk, NumHeads, HeadDim).Permute(0, 2, 1, 3).Contiguous();

        if (cache != null)
        {
            (K, V) = cache.AppendAndGet(K, V);
            Lk = K.Shape[2];
        }

        // SDPA на 3D-батчах: (BH, Lq, dh) × (BH, Lk, dh).
        int BH = B * NumHeads;
        var Qf = Q.Reshape(BH, Lq, HeadDim);
        var Kf = K.Reshape(BH, Lk, HeadDim);
        var Vf = V.Reshape(BH, Lk, HeadDim);

        var maskFlat = ReshapeMaskForSDPA(attnMask, B, NumHeads, Lq, Lk);

        var ctx = ScaledDotProductAttention.Apply(Qf, Kf, Vf, maskFlat, isCausal,
            Dropout, Training, rng);
        // (BH, Lq, dh) -> (B, H, Lq, dh) -> (B, Lq, H, dh) -> (B, Lq, E)
        var ctx4 = ctx.Reshape(B, NumHeads, Lq, HeadDim).Permute(0, 2, 1, 3);
        var ctxFlat = ctx4.Contiguous().Reshape(B, Lq, EmbedDim);
        return OutProj.Forward(ctxFlat);
    }

    /// <summary>
    /// Привести attention-маску к форме, broadcast-совместимой со scores
    /// формы <c>(B*H, Lq, Lk)</c>. Поддерживаются формы:
    /// <list type="bullet">
    ///   <item><c>(Lq, Lk)</c> — общая для всех батчей и голов;</item>
    ///   <item><c>(B, Lq, Lk)</c> — broadcast по головам (расширяется до <c>(B*H, Lq, Lk)</c>);</item>
    ///   <item><c>(B, H, Lq, Lk)</c> — индивидуальная по голове (reshape в <c>(B*H, Lq, Lk)</c>).</item>
    /// </list>
    /// </summary>
    private static Tensor ReshapeMaskForSDPA(Tensor mask, int B, int H, int Lq, int Lk)
    {
        if (mask == null) return null;
        switch (mask.Rank)
        {
            case 2:
                if (mask.Shape[0] != Lq || mask.Shape[1] != Lk)
                    throw new ArgumentException(
                        $"MHA mask (Lq,Lk) должна быть ({Lq},{Lk}), фактически ({mask.Shape[0]},{mask.Shape[1]}).");
                return mask;
            case 3:
                if (mask.Shape[0] != B || mask.Shape[1] != Lq || mask.Shape[2] != Lk)
                    throw new ArgumentException(
                        $"MHA mask (B,Lq,Lk) должна быть ({B},{Lq},{Lk}), фактически {mask.Shape}.");
                // (B, Lq, Lk) -> (B, 1, Lq, Lk) -> expand H -> (B, H, Lq, Lk) -> (B*H, Lq, Lk)
                return mask
                    .Reshape(B, 1, Lq, Lk)
                    .Expand(B, H, Lq, Lk)
                    .Contiguous()
                    .Reshape(B * H, Lq, Lk);
            case 4:
                if (mask.Shape[0] != B || mask.Shape[1] != H ||
                    mask.Shape[2] != Lq || mask.Shape[3] != Lk)
                    throw new ArgumentException(
                        $"MHA mask (B,H,Lq,Lk) должна быть ({B},{H},{Lq},{Lk}), фактически {mask.Shape}.");
                return mask.Contiguous().Reshape(B * H, Lq, Lk);
            default:
                throw new ArgumentException(
                    $"MHA mask rank должен быть 2/3/4, фактически {mask.Rank}.");
        }
    }

    /// <summary>Self-attention сахар (query=key=value).</summary>
    public Tensor SelfAttention(Tensor x, Tensor attnMask = null, bool isCausal = false,
        KVCache cache = null, Random rng = null) =>
        ForwardMHA(x, x, x, attnMask, isCausal, cache, rng);

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => SelfAttention(input);
}

/// <summary>
/// KV-кэш для инкрементального decoding: сохраняет прошлые ключи/значения
/// между вызовами и просто конкатенирует с новыми.
/// </summary>
public sealed class KVCache
{
    private Tensor _k;  // (B, H, T, dh)
    private Tensor _v;  // (B, H, T, dh)

    /// <summary>Создать пустой кэш.</summary>
    public KVCache() { }

    /// <summary>Текущая длина накопленной последовательности.</summary>
    public int Length => _k?.Shape[2] ?? 0;

    /// <summary>Сбросить кэш.</summary>
    public void Reset() { _k = null; _v = null; }

    /// <summary>
    /// Добавить новые K/V и вернуть полные накопленные K/V для attention.
    /// </summary>
    public (Tensor K, Tensor V) AppendAndGet(Tensor newK, Tensor newV)
    {
        if (_k == null) { _k = newK; _v = newV; }
        else
        {
            _k = IndexingOps.Cat(new[] { _k, newK }, axis: 2);
            _v = IndexingOps.Cat(new[] { _v, newV }, axis: 2);
        }
        return (_k, _v);
    }
}
