using System;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Losses;

/// <summary>
/// Контрастивные / метрические потери: Triplet, Cosine*, MarginRanking, HingeEmbedding,
/// PairwiseDistance.
/// </summary>
/// <remarks>
/// Все функции реализованы через композицию <see cref="TensorOps"/> (а не через
/// прямой обход <c>AsSpan&lt;float&gt;()</c>): autograd собирается естественно,
/// градиенты текут к каждому входу, поддержка нестандартных устройств — за счёт
/// зарегистрированных kernel-ов в <see cref="OpRegistry"/>.
/// </remarks>
public static class EmbeddingLosses
{
    /// <summary>
    /// Triplet margin loss: <c>max(0, d(a,p) − d(a,n) + margin)</c>, где d — L_p норма
    /// по последней оси.
    /// </summary>
    public static Tensor TripletMargin(Tensor anchor, Tensor positive, Tensor negative,
        float margin = 1f, float p = 2f, Reduction reduction = Reduction.Mean)
    {
        if (anchor == null) throw new ArgumentNullException(nameof(anchor));
        if (positive == null) throw new ArgumentNullException(nameof(positive));
        if (negative == null) throw new ArgumentNullException(nameof(negative));
        var dPos = PairwiseDistance(anchor, positive, p);
        var dNeg = PairwiseDistance(anchor, negative, p);
        var diff = dPos - dNeg;
        // hinge = relu(diff + margin)
        var diffPlus = TensorOps.AddScalar(diff, margin);
        var loss = TensorOps.Relu(diffPlus);
        return RegressionLosses.Reduce(loss, reduction);
    }

    /// <summary>
    /// Cosine Embedding loss:
    /// <list type="bullet">
    ///   <item>y &gt; 0: <c>1 − cos(x1, x2)</c>;</item>
    ///   <item>y &lt; 0: <c>max(0, cos(x1, x2) − margin)</c>.</item>
    /// </list>
    /// </summary>
    public static Tensor CosineEmbedding(Tensor x1, Tensor x2, Tensor y,
        float margin = 0f, Reduction reduction = Reduction.Mean)
    {
        if (x1 == null) throw new ArgumentNullException(nameof(x1));
        if (x2 == null) throw new ArgumentNullException(nameof(x2));
        if (y == null) throw new ArgumentNullException(nameof(y));
        var cos = CosineSimilarity(x1, x2);
        if (!cos.Shape.Equals(y.Shape))
            throw new ArgumentException(
                $"CosineEmbedding: y.Shape={y.Shape} должен совпадать с cos.Shape={cos.Shape}.");

        // y > 0: 1 - cos     (= relu(1 - cos), всегда >= 0)
        // y < 0: max(0, cos - margin) = relu(cos - margin)
        // Соберём через бинарную маску (y > 0): mask_pos, mask_neg = 1 - mask_pos.
        // Маска — leaf-тензор без grad, чтобы не попасть в граф.
        var maskPos = MaskGreaterThanZero(y);                       // 1 при y>0, иначе 0
        var maskNeg = TensorOps.AddScalar(TensorOps.MulScalar(maskPos, -1f), 1f);

        var oneMinusCos = TensorOps.AddScalar(TensorOps.MulScalar(cos, -1f), 1f); // 1 - cos
        var negPart = TensorOps.Relu(TensorOps.AddScalar(cos, -margin));          // max(0, cos - margin)
        var loss = oneMinusCos * maskPos + negPart * maskNeg;
        return RegressionLosses.Reduce(loss, reduction);
    }

    /// <summary>
    /// Margin Ranking: <c>max(0, -y * (x1 - x2) + margin)</c>.
    /// </summary>
    public static Tensor MarginRanking(Tensor x1, Tensor x2, Tensor y, float margin = 0f,
        Reduction reduction = Reduction.Mean)
    {
        if (x1 == null) throw new ArgumentNullException(nameof(x1));
        if (x2 == null) throw new ArgumentNullException(nameof(x2));
        if (y == null) throw new ArgumentNullException(nameof(y));
        if (!x1.Shape.Equals(x2.Shape))
            throw new ArgumentException("MarginRanking: x1.Shape должен совпадать с x2.Shape.");
        if (!x1.Shape.Equals(y.Shape))
            throw new ArgumentException("MarginRanking: y.Shape должен совпадать с x1.Shape.");
        var diff = x1 - x2;
        // -y * diff: y используется как вес-знак, без grad относительно y.
        var yNoGrad = AsLeaf(y);
        var weighted = TensorOps.Neg(yNoGrad * diff);
        var loss = TensorOps.Relu(TensorOps.AddScalar(weighted, margin));
        return RegressionLosses.Reduce(loss, reduction);
    }

    /// <summary>
    /// Hinge Embedding:
    /// <list type="bullet">
    ///   <item>y &gt; 0: <c>x</c>;</item>
    ///   <item>y &lt; 0: <c>max(0, margin − x)</c>.</item>
    /// </list>
    /// </summary>
    public static Tensor HingeEmbedding(Tensor x, Tensor y, float margin = 1f,
        Reduction reduction = Reduction.Mean)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (y == null) throw new ArgumentNullException(nameof(y));
        if (!x.Shape.Equals(y.Shape))
            throw new ArgumentException("HingeEmbedding: y.Shape должен совпадать с x.Shape.");

        var maskPos = MaskGreaterThanZero(y);
        var maskNeg = TensorOps.AddScalar(TensorOps.MulScalar(maskPos, -1f), 1f);
        var negPart = TensorOps.Relu(TensorOps.AddScalar(TensorOps.Neg(x), margin)); // max(0, margin - x)
        var loss = x * maskPos + negPart * maskNeg;
        return RegressionLosses.Reduce(loss, reduction);
    }

    /// <summary>
    /// Pairwise L_p distance вдоль последней оси:
    /// <c>d(x1, x2) = (sum |x1 - x2|^p + eps)^(1/p)</c>.
    /// </summary>
    /// <remarks>
    /// <c>eps</c> — внутри суммы, чтобы избежать <c>0^(1/p)</c>-проблем при идентичных
    /// векторах и сохранить корректный градиент.
    /// </remarks>
    public static Tensor PairwiseDistance(Tensor x1, Tensor x2, float p = 2f, float eps = 1e-6f)
    {
        if (x1 == null) throw new ArgumentNullException(nameof(x1));
        if (x2 == null) throw new ArgumentNullException(nameof(x2));
        if (!x1.Shape.Equals(x2.Shape))
            throw new ArgumentException("PairwiseDistance: shapes должны совпадать.");
        if (p <= 0f) throw new ArgumentOutOfRangeException(nameof(p), "p должно быть > 0.");
        if (eps < 0f) throw new ArgumentOutOfRangeException(nameof(eps));

        var diff = x1 - x2;
        // |diff|
        var ad = TensorOps.Abs(diff);
        // |diff|^p
        Tensor adP;
        if (p == 2f)
        {
            adP = ad * ad;
        }
        else if (p == 1f)
        {
            adP = ad;
        }
        else
        {
            var pT = Tensor.Full(Shape.Scalar, p, ad.DType, ad.Device);
            adP = TensorOps.Pow(ad, pT);
        }
        // sum по последней оси
        int axis = diff.Rank - 1;
        var summed = TensorOps.Sum(adP, axis: axis, keepDim: false);
        var withEps = TensorOps.AddScalar(summed, eps);
        if (p == 2f)
            return TensorOps.Sqrt(withEps);
        if (p == 1f)
            return withEps;
        // (sum + eps)^(1/p)
        var invP = Tensor.Full(Shape.Scalar, 1f / p, withEps.DType, withEps.Device);
        return TensorOps.Pow(withEps, invP);
    }

    /// <summary>
    /// Cosine similarity по последней оси:
    /// <c>cos(x1, x2) = sum(x1*x2) / (||x1|| * ||x2|| + eps)</c>.
    /// </summary>
    public static Tensor CosineSimilarity(Tensor x1, Tensor x2, float eps = 1e-8f)
    {
        if (x1 == null) throw new ArgumentNullException(nameof(x1));
        if (x2 == null) throw new ArgumentNullException(nameof(x2));
        if (!x1.Shape.Equals(x2.Shape))
            throw new ArgumentException("CosineSimilarity: shapes должны совпадать.");
        int axis = x1.Rank - 1;
        var dot = TensorOps.Sum(x1 * x2, axis: axis, keepDim: false);
        var n1 = TensorOps.Sqrt(TensorOps.AddScalar(
            TensorOps.Sum(x1 * x1, axis: axis, keepDim: false), eps * eps));
        var n2 = TensorOps.Sqrt(TensorOps.AddScalar(
            TensorOps.Sum(x2 * x2, axis: axis, keepDim: false), eps * eps));
        // знаменатель: n1 * n2 (>= eps^2 > 0).
        var denom = n1 * n2;
        return dot / denom;
    }

    #region Хелперы

    /// <summary>
    /// Бинарная маска (1.0 если y &gt; 0, иначе 0.0). Создаётся как leaf-тензор
    /// без autograd, чтобы не «протекать» через y (y — это метка/вес-знак).
    /// </summary>
    private static Tensor MaskGreaterThanZero(Tensor y)
    {
        // Считаем на той же device, через копию на CPU и обратно — это путь по умолчанию.
        // (Реализовано без OpCode.Compare, чтобы не плодить новые ops.)
        var src = y.Device.Type == DeviceType.Cpu ? y.Contiguous() : y.ToCpu().Contiguous();
        var ys = src.AsReadOnlySpan<float>();
        var mask = Tensor.Empty(y.Shape, y.DType, Device.Cpu);
        var ms = mask.AsSpan<float>();
        for (int i = 0; i < ms.Length; i++)
            ms[i] = ys[i] > 0f ? 1f : 0f;
        if (y.Device.Type != DeviceType.Cpu) mask = mask.To(y.Device);
        return mask; // leaf, requires_grad=false
    }

    /// <summary>
    /// Получить leaf-копию тензора без autograd-связи (для использования в роли
    /// «веса» или «метки» в потерях).
    /// </summary>
    private static Tensor AsLeaf(Tensor t)
    {
        if (t.GradFn == null && !t.RequiresGrad) return t;
        // Make a contiguous detached copy.
        var c = t.Contiguous();
        // Если c == t (уже contiguous и leaf без grad) — вернуть c. Иначе detach
        // через создание нового тензора с теми же данными (RequiresGrad=false
        // по умолчанию у Tensor.Empty + копирование).
        if (c.GradFn == null && !c.RequiresGrad) return c;
        var copy = Tensor.Empty(c.Shape, c.DType, c.Device);
        if (c.Device.Type == DeviceType.Cpu)
            c.AsReadOnlySpan<float>().CopyTo(copy.AsSpan<float>());
        else
        {
            // через CPU
            var cpuC = c.ToCpu();
            var cpuCopy = Tensor.Empty(c.Shape, c.DType, Device.Cpu);
            cpuC.AsReadOnlySpan<float>().CopyTo(cpuCopy.AsSpan<float>());
            copy = cpuCopy.To(c.Device);
        }
        return copy;
    }
    #endregion Хелперы

}