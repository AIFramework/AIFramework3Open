using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

public static partial class TensorOps
{
    #region Reductions

    /// <summary>
    /// Сумма всех элементов (возвращает скаляр) или по конкретной оси.
    /// </summary>
    public static Tensor Sum(Tensor x, int? axis = null, bool keepDim = false)
    {
        if (x.DType != DType.Float32)
            throw new NotSupportedException($"Sum: только Float32, dtype={x.DType}.");

        // GPU dispatch (если зарегистрирован native kernel) — без D2H/H2D round-trip.
        if (x.Device.Type != DeviceType.Cpu)
        {
            int? normAxis = axis.HasValue ? x.Shape.NormalizeAxis(axis.Value) : (int?)null;
            var k = OpRegistry.TryGet(OpCode.Sum, x.DType, x.Device);
            if (k != null)
            {
                var attrs = new ReduceAttrs(normAxis, keepDim);
                var y = k(new[] { x }, attrs)[0];
                if (TapeContext.IsGradEnabled && x.RequiresGrad)
                {
                    Function fn = normAxis.HasValue
                        ? new SumAxisFunction(x, normAxis.Value, keepDim)
                        : new SumAllFunction(x);
                    fn.RegisterInput(x);
                    y.GradFn = fn;
                }
                return y;
            }
        }

        if (axis == null)
        {
            float total = 0f;
            var c = x.Device.Type != DeviceType.Cpu ? x.ToCpu().Contiguous() : x.Contiguous();
            var s = c.AsReadOnlySpan<float>();
            for (int i = 0; i < s.Length; i++) total += s[i];
            var y = Tensor.Empty(Shape.Scalar, x.DType, Device.Cpu);
            y.AsSpan<float>()[0] = total;
            if (x.Device.Type != DeviceType.Cpu) y = y.To(x.Device);
            if (TapeContext.IsGradEnabled && x.RequiresGrad)
            {
                var fn = new SumAllFunction(x);
                fn.RegisterInput(x);
                y.GradFn = fn;
            }
            return y;
        }
        else
        {
            int ax = x.Shape.NormalizeAxis(axis.Value);
            var y = SumAxisForward(x, ax, keepDim);
            if (TapeContext.IsGradEnabled && x.RequiresGrad)
            {
                var fn = new SumAxisFunction(x, ax, keepDim);
                fn.RegisterInput(x);
                y.GradFn = fn;
            }
            return y;
        }
    }

    /// <summary>
    /// Свернуть тензор <paramref name="x"/> до формы <paramref name="targetShape"/>
    /// путём суммирования по «лишним» осям (broadcast-обратная операция).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Если <c>targetShape.Rank &lt; x.Rank</c>, ведущие оси <c>x</c> сворачиваются полностью.
    /// Затем для каждой оси, где <c>targetShape</c> имеет dim=1, делается axis-Sum c keepDim=true.
    /// </para>
    /// <para>
    /// Для GPU все Sum-вызовы идут через GPU-kernel (см. <see cref="Sum"/>) — без D2H/H2D.
    /// </para>
    /// </remarks>
    public static Tensor SumToShape(Tensor x, Shape targetShape)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (x.Shape.Equals(targetShape)) return x;
        int diff = x.Rank - targetShape.Rank;
        if (diff < 0)
            throw new ArgumentException($"SumToShape: target rank {targetShape.Rank} > source rank {x.Rank}.");
        var y = x;
        // 1) Сворачиваем «лишние» ведущие оси (полная сумма по каждой).
        for (int i = 0; i < diff; i++)
        {
            // Каждая итерация снимает axis=0 (после предыдущего без-keepDim сжатия).
            y = Sum(y, axis: 0, keepDim: false);
        }
        // 2) По осям, где target имеет dim=1, сворачиваем с keepDim=true.
        for (int k = 0; k < targetShape.Rank; k++)
        {
            if (targetShape[k] == 1 && y.Shape[k] != 1)
                y = Sum(y, axis: k, keepDim: true);
        }
        if (!y.Shape.Equals(targetShape))
            y = y.Reshape(targetShape.ToArray());
        return y;
    }

    /// <summary>Среднее всех элементов или по оси.</summary>
    /// <exception cref="ArgumentException">При <c>count == 0</c> (пустая ось/тензор).</exception>
    public static Tensor Mean(Tensor x, int? axis = null, bool keepDim = false)
    {
        long count = axis == null ? x.NumElements : x.Shape[x.Shape.NormalizeAxis(axis.Value)];
        if (count == 0)
            throw new ArgumentException(
                axis == null
                    ? "Mean: тензор пуст (NumElements=0)."
                    : $"Mean: ось {axis.Value} имеет размер 0 — деление на ноль.");
        var s = Sum(x, axis, keepDim);
        return MulScalar(s, 1f / count);
    }

    private static Tensor SumAxisForward(Tensor src, int axis, bool keepDim)
    {
        var srcShape = src.Shape.AsSpan();
        int rank = src.Rank;
        int axisSize = srcShape[axis];
        long outer = 1;
        for (int i = 0; i < axis; i++) outer *= srcShape[i];
        long inner = 1;
        for (int i = axis + 1; i < rank; i++) inner *= srcShape[i];

        var cpuSrc = src.Device.Type != DeviceType.Cpu ? src.ToCpu() : src;
        var c = cpuSrc.Contiguous();
        var sSpan = c.AsReadOnlySpan<float>();

        int[] outDims;
        if (keepDim)
        {
            outDims = src.Shape.ToArray();
            outDims[axis] = 1;
        }
        else
        {
            outDims = new int[rank - 1];
            for (int i = 0, j = 0; i < rank; i++) if (i != axis) outDims[j++] = srcShape[i];
        }
        var dst = Tensor.Zeros(new Shape(outDims), src.DType, Device.Cpu);
        var dSpan = dst.AsSpan<float>();
        for (long o = 0; o < outer; o++)
        for (int a = 0; a < axisSize; a++)
        {
            long sb = (o * axisSize + a) * inner;
            long db = o * inner;
            for (long n = 0; n < inner; n++)
                dSpan[(int)(db + n)] += sSpan[(int)(sb + n)];
        }
        if (src.Device.Type != DeviceType.Cpu) dst = dst.To(src.Device);
        return dst;
    }

    private sealed class SumAllFunction : Function
    {
        private readonly Shape _xShape;
        private readonly DType _dt;
        private readonly Device _dev;
        public SumAllFunction(Tensor x) { _xShape = x.Shape; _dt = x.DType; _dev = x.Device; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            // Если grad на GPU — Expand+Contiguous идёт целиком на GPU (через
            // OpCode.Contiguous из OpRegistry). На CPU — обычный путь.
            using (TapeContext.NoGrad())
            {
                var g = gradOutput;
                // Делаем shape совместимым с (1,...,1) и Expand до _xShape.
                var ones = new int[_xShape.Rank];
                for (int i = 0; i < ones.Length; i++) ones[i] = 1;
                var gKeep = g.Reshape(ones);
                var dx = gKeep.Expand(_xShape.ToArray()).Contiguous();
                return new[] { dx };
            }
        }
    }

    private sealed class SumAxisFunction : Function
    {
        private readonly Shape _xShape;
        private readonly DType _dt;
        private readonly Device _dev;
        private readonly int _axis;
        private readonly bool _keepDim;
        public SumAxisFunction(Tensor x, int axis, bool keepDim)
        {
            _xShape = x.Shape; _dt = x.DType; _dev = x.Device;
            _axis = axis; _keepDim = keepDim;
        }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            // Расширение градиента до _xShape остаётся на родном устройстве:
            // Expand через ViewFunction + Contiguous() через OpCode.Contiguous (GPU).
            using (TapeContext.NoGrad())
            {
                Tensor g = gradOutput;
                if (!_keepDim) g = g.Unsqueeze(_axis);
                var expandDims = _xShape.ToArray();
                var dx = g.Expand(expandDims).Contiguous();
                return new[] { dx };
            }
        }
    }

    #endregion Reductions
}
