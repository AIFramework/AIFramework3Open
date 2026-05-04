using System;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// numpy-/PyTorch-style broadcasting: вычисляет результирующую форму при
/// бинарной операции и страйды для каждого из тензоров после виртуального expand.
/// </summary>
/// <remarks>
/// Правила:
/// <list type="number">
///   <item>Выравниваем формы по правому краю, отсутствующие оси слева = 1.</item>
///   <item>Размеры по каждой оси должны совпадать или один из них быть 1.</item>
///   <item>Размеры 1 «вещаются» через stride=0 — без копирования памяти.</item>
/// </list>
/// </remarks>
public static class Broadcasting
{
    /// <summary>
    /// Результат broadcasting: новая форма + страйды для a и b с учётом expand.
    /// </summary>
    public readonly struct Result
    {
        /// <summary>Результирующая форма.</summary>
        public Shape Shape { get; }
        /// <summary>Страйды первого операнда (после виртуального expand).</summary>
        public int[] StridesA { get; }
        /// <summary>Страйды второго операнда (после виртуального expand).</summary>
        public int[] StridesB { get; }

        internal Result(Shape s, int[] sa, int[] sb)
        { Shape = s; StridesA = sa; StridesB = sb; }
    }

    /// <summary>
    /// Вычислить broadcast-результат для двух тензоров.
    /// </summary>
    public static Result Compute(Tensor a, Tensor b)
    {
        var aShape = a.Shape.AsSpan();
        var bShape = b.Shape.AsSpan();
        var aStr = a.Strides;
        var bStr = b.Strides;

        int rank = Math.Max(aShape.Length, bShape.Length);
        var dims = new int[rank];
        var stridesA = new int[rank];
        var stridesB = new int[rank];

        for (int i = 0; i < rank; i++)
        {
            // Индекс с конца для выравнивания вправо.
            int ai = aShape.Length - 1 - i;
            int bi = bShape.Length - 1 - i;
            int outIdx = rank - 1 - i;

            int da = ai >= 0 ? aShape[ai] : 1;
            int db = bi >= 0 ? bShape[bi] : 1;

            int sa = ai >= 0 ? aStr[ai] : 0;
            int sb = bi >= 0 ? bStr[bi] : 0;

            int outDim;
            if (da == db) outDim = da;
            else if (da == 1) { outDim = db; sa = 0; }
            else if (db == 1) { outDim = da; sb = 0; }
            else
                throw new ArgumentException(
                    $"Несовместимые формы для broadcast: {a.Shape} и {b.Shape}.");

            dims[outIdx] = outDim;
            stridesA[outIdx] = sa;
            stridesB[outIdx] = sb;
        }

        return new Result(new Shape(dims), stridesA, stridesB);
    }

    /// <summary>
    /// Свернуть градиент <paramref name="grad"/> с broadcasted-shape обратно в
    /// исходную <paramref name="originalShape"/> через суммирование «вещанных» осей.
    /// </summary>
    /// <remarks>
    /// При forward бинарная операция расширила исходный тензор формы N до broadcast-shape M.
    /// При backward градиент имеет shape M; нужно его свернуть назад в N — это значит
    /// просуммировать по тем осям, где был broadcast (либо размер ушёл, либо был 1).
    /// </remarks>
    public static Tensor ReduceForBroadcast(Tensor grad, Shape originalShape)
    {
        if (grad.Shape.Equals(originalShape)) return grad;
        var gradDims = grad.Shape.AsSpan();
        var origDims = originalShape.AsSpan();
        int leadingOnes = gradDims.Length - origDims.Length;
        // 1) Сжать ведущие оси (которых в исходной форме не было) суммой.
        Tensor result = grad;
        for (int i = 0; i < leadingOnes; i++)
            result = SumAlongAxis(result, axis: 0, keepDim: false);
        // 2) Для оставшихся осей, где origDims[i] == 1, а result.shape[i] > 1 — сумма с keepdim.
        for (int i = 0; i < origDims.Length; i++)
        {
            if (origDims[i] == 1 && result.Shape[i] != 1)
                result = SumAlongAxis(result, axis: i, keepDim: true);
        }
        return result;
    }

    private static Tensor SumAlongAxis(Tensor src, int axis, bool keepDim)
    {
        var origDev = src.Device;
        var cpuSrc = origDev.Type != DeviceType.Cpu ? src.ToCpu() : src;
        var srcShape = cpuSrc.Shape.AsSpan();
        int rank = cpuSrc.Rank;
        int axisSize = srcShape[axis];

        long outer = 1;
        for (int i = 0; i < axis; i++) outer *= srcShape[i];
        long inner = 1;
        for (int i = axis + 1; i < rank; i++) inner *= srcShape[i];

        var c = cpuSrc.Contiguous();
        var srcSpan = c.AsReadOnlySpan<float>();

        int[] outDims;
        if (keepDim)
        {
            outDims = cpuSrc.Shape.ToArray();
            outDims[axis] = 1;
        }
        else
        {
            outDims = new int[rank - 1];
            for (int i = 0, j = 0; i < rank; i++)
                if (i != axis) outDims[j++] = srcShape[i];
        }
        var dst = Tensor.Zeros(new Shape(outDims), cpuSrc.DType, Device.Cpu);
        var dstSpan = dst.AsSpan<float>();

        for (long o = 0; o < outer; o++)
        {
            for (int a = 0; a < axisSize; a++)
            {
                long srcBase = (o * axisSize + a) * inner;
                long dstBase = o * inner;
                for (long n = 0; n < inner; n++)
                    dstSpan[(int)(dstBase + n)] += srcSpan[(int)(srcBase + n)];
            }
        }
        if (origDev.Type != DeviceType.Cpu) dst = dst.To(origDev);
        return dst;
    }
}
