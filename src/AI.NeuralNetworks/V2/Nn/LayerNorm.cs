using System;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Layer Normalization. Нормализует по последним <c>NormalizedShape</c> осям
/// (как PyTorch).
/// </summary>
/// <remarks>
/// y = (x - mean) / sqrt(var + eps) * gamma + beta, где mean/var считаются по
/// нормализуемым осям независимо для каждого элемента батча.
/// Backward — single-pass, O(N).
/// </remarks>
public sealed class LayerNorm : Module
{
    /// <summary>Размеры осей, по которым нормализуем (последние k осей входа).</summary>
    public int[] NormalizedShape { get; }

    /// <summary>Эпсилон для численной стабильности.</summary>
    public float Eps { get; }

    /// <summary>Аффинные параметры (если ElementwiseAffine=true).</summary>
    public Parameter Weight { get; }

    /// <summary>Сдвиг (если ElementwiseAffine=true).</summary>
    public Parameter Bias { get; }

    /// <summary>Использовать ли обучаемые gamma/beta.</summary>
    public bool ElementwiseAffine { get; }

    private readonly int _normSize;

    /// <summary>Создать LayerNorm.</summary>
    public LayerNorm(int[] normalizedShape, float eps = 1e-5f, bool elementwiseAffine = true)
    {
        if (normalizedShape == null || normalizedShape.Length == 0)
            throw new ArgumentException("normalizedShape не может быть пустым.");
        NormalizedShape = (int[])normalizedShape.Clone();
        Eps = eps;
        ElementwiseAffine = elementwiseAffine;
        _normSize = 1;
        for (int i = 0; i < NormalizedShape.Length; i++) _normSize *= NormalizedShape[i];

        if (elementwiseAffine)
        {
            var w = Tensor.Empty(new Shape(NormalizedShape));
            Init.Constant_(w, 1f);
            Weight = RegisterParameter("weight", w);

            var b = Tensor.Empty(new Shape(NormalizedShape));
            Init.Zeros_(b);
            Bias = RegisterParameter("bias", b);
        }
    }

    /// <summary>Удобный конструктор для одной размерности (как PyTorch).</summary>
    public LayerNorm(int normalizedFeatures, float eps = 1e-5f, bool elementwiseAffine = true)
        : this(new[] { normalizedFeatures }, eps, elementwiseAffine) { }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Rank < NormalizedShape.Length)
            throw new ArgumentException("Rank входа меньше длины normalizedShape.");
        for (int i = 0; i < NormalizedShape.Length; i++)
        {
            int axis = input.Rank - NormalizedShape.Length + i;
            if (input.Shape[axis] != NormalizedShape[i])
                throw new ArgumentException(
                    $"Размер по оси {axis} = {input.Shape[axis]}, ожидалось {NormalizedShape[i]}.");
        }
        return Apply(input, _normSize, Eps, Weight?.Tensor, Bias?.Tensor);
    }

    /// <summary>
    /// Функциональная форма LayerNorm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Реализована как <b>композиция базовых TensorOps</b> (Sum/Sub/Mul/Sqrt) — это
    /// автоматически работает на любом устройстве, для которого зарегистрированы
    /// эти ops в <see cref="OpRegistry"/>. Графа autograd собирается естественно
    /// через TensorOps, отдельный <c>Function</c>-узел не нужен.
    /// </para>
    /// <para>
    /// Если backend (например, GPU) зарегистрировал специализированный fused-kernel
    /// под <c>OpCode.LayerNorm</c>, он будет вызван вместо композиции — см. dispatch
    /// в начале метода.
    /// </para>
    /// </remarks>
    public static Tensor Apply(Tensor input, int normSize, float eps, Tensor weight, Tensor bias)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (normSize <= 0) throw new ArgumentOutOfRangeException(nameof(normSize));
        if (input.NumElements % normSize != 0)
            throw new ArgumentException(
                $"LayerNorm: NumElements({input.NumElements}) не делится на normSize({normSize}).");

        // 1) Если backend зарегистрировал fused LayerNorm-kernel — используем его.
        var kernel = OpRegistry.TryGet(OpCode.LayerNorm, input.DType, input.Device);
        if (kernel != null)
        {
            var attrs = new LayerNormAttrs(normSize, eps);
            int arity = 1 + (weight != null ? 1 : 0) + (bias != null ? 1 : 0);
            var inputs = new Tensor[arity];
            int j = 0;
            inputs[j++] = input;
            if (weight != null) inputs[j++] = weight;
            if (bias != null) inputs[j++] = bias;
            var outs = kernel(inputs, attrs);
            if (outs == null || outs.Length == 0)
                throw new InvalidOperationException("LayerNorm-kernel вернул пустой результат.");
            return outs[0];
        }

        // 2) Универсальный путь: считаем mean/var вдоль «свёрнутой» оси normSize.
        //    Reshape: (... , normSize). Считаем по последней оси.
        long batches = input.NumElements / normSize;
        if (batches > int.MaxValue)
            throw new OverflowException("LayerNorm: число батчей не помещается в int.");

        var x = input.Contiguous();
        var x2 = x.Reshape((int)batches, normSize);

        // mean: (B, 1)
        var mean = TensorOps.Mean(x2, axis: 1, keepDim: true);
        var centered = x2 - mean;                                        // (B, N)
        var sq = centered * centered;                                    // (B, N)
        var var_ = TensorOps.Mean(sq, axis: 1, keepDim: true);           // (B, 1)
        var rstd = TensorOps.Sqrt(TensorOps.AddScalar(var_, eps));       // (B, 1)
        // нам нужен 1/rstd, а не rstd — поэтому делим:
        var normed = centered / rstd;                                    // (B, N)

        var y = normed.Reshape(input.Shape.ToArray());
        if (weight != null)
        {
            // weight/bias имеют форму NormalizedShape; broadcast по ведущим осям.
            int[] bShape = new int[input.Rank];
            int diff = input.Rank - weight.Rank;
            for (int i = 0; i < diff; i++) bShape[i] = 1;
            for (int i = 0; i < weight.Rank; i++) bShape[diff + i] = weight.Shape[i];
            var wB = weight.Reshape(bShape);
            y = y * wB;
        }
        if (bias != null)
        {
            int[] bShape = new int[input.Rank];
            int diff = input.Rank - bias.Rank;
            for (int i = 0; i < diff; i++) bShape[i] = 1;
            for (int i = 0; i < bias.Rank; i++) bShape[diff + i] = bias.Shape[i];
            var bB = bias.Reshape(bShape);
            y = y + bB;
        }
        return y;
    }

    /// <summary>Атрибуты для backend-fused LayerNorm-kernel-а.</summary>
    public readonly struct LayerNormAttrs
    {
        /// <summary>Число элементов на батч-вектор (= произведение NormalizedShape).</summary>
        public int NormSize { get; }
        /// <summary>Эпсилон.</summary>
        public float Eps { get; }
        /// <summary>Создать атрибуты.</summary>
        public LayerNormAttrs(int normSize, float eps) { NormSize = normSize; Eps = eps; }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"LayerNorm(normShape=[{string.Join(",", NormalizedShape)}], eps={Eps}, affine={ElementwiseAffine})";
}
