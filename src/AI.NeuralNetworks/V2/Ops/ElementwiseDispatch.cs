using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Generic-диспатчер для поэлементных операций. Один метод обслуживает все
/// унарные op-ы; ещё один — все бинарные с broadcasting. Пример «фрактального»
/// дизайна: 60 операций сворачиваются в ~150 строк инфраструктуры.
/// </summary>
public static class ElementwiseDispatch
{
    /// <summary>
    /// Forward + autograd-record для унарной операции <typeparamref name="TOp"/>.
    /// </summary>
    public static Tensor Unary<TOp, T>(Tensor x, string opName)
        where TOp : struct, IUnaryOp<T>
        where T : unmanaged
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        var op = default(TOp);
        var y = Tensor.Empty(x.Shape, x.DType, x.Device);

        // Forward — fast-path для contiguous; иначе через индексы.
        var xRef = x.IsContiguous ? x : x.Contiguous();
        var xs = xRef.AsReadOnlySpan<T>();
        var ys = y.AsSpan<T>();
        for (int i = 0; i < ys.Length; i++) ys[i] = op.Forward(xs[i]);

        if (TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            var fn = new UnaryFunction<TOp, T>(op, xRef, y);
            fn.RegisterInput(x);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// Forward + autograd-record для бинарной операции <typeparamref name="TOp"/>
    /// с numpy-style broadcasting.
    /// </summary>
    public static Tensor Binary<TOp, T>(Tensor a, Tensor b, string opName)
        where TOp : struct, IBinaryOp<T>
        where T : unmanaged
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));
        if (a.DType != b.DType)
            throw new ArgumentException(
                $"DType mismatch для {opName}: {a.DType} vs {b.DType}.");

        var bc = Broadcasting.Compute(a, b);
        var op = default(TOp);
        var y = Tensor.Empty(bc.Shape, a.DType, a.Device);

        // Iterate по результирующей форме; индексирование с учётом страйдов
        // (страйды могут содержать 0 для broadcast).
        var ys = y.AsSpan<T>();
        var aSpan = a.Storage.AsReadOnlySpan<T>();
        var bSpan = b.Storage.AsReadOnlySpan<T>();
        int aOff = a.Offset;
        int bOff = b.Offset;
        long n = y.NumElements;
        int rank = y.Rank;
        var outDims = y.Shape.AsSpan();

        // Fast-path: если оба тензора contiguous и формы идентичны с y, идём по
        // плоскому индексу — это покрывает 90% случаев и устраняет N-D обход.
        bool fastPath = a.IsContiguous && b.IsContiguous &&
                        a.Shape.Equals(y.Shape) && b.Shape.Equals(y.Shape);
        if (fastPath)
        {
            var aSp = a.AsReadOnlySpan<T>();
            var bSp = b.AsReadOnlySpan<T>();
            for (int i = 0; i < ys.Length; i++) ys[i] = op.Forward(aSp[i], bSp[i]);
        }
        else
        {
            Span<int> idx = rank <= 16 ? stackalloc int[rank] : new int[rank];
            for (int i = 0; i < rank; i++) idx[i] = 0;
            for (long flat = 0; flat < n; flat++)
            {
                int ai = aOff, bi = bOff;
                for (int k = 0; k < rank; k++)
                {
                    ai += idx[k] * bc.StridesA[k];
                    bi += idx[k] * bc.StridesB[k];
                }
                ys[(int)flat] = op.Forward(aSpan[ai], bSpan[bi]);
                for (int k = rank - 1; k >= 0; k--)
                {
                    idx[k]++;
                    if (idx[k] < outDims[k]) break;
                    idx[k] = 0;
                }
            }
        }

        if (TapeContext.IsGradEnabled && (a.RequiresGrad || b.RequiresGrad))
        {
            var fn = new BinaryFunction<TOp, T>(op, a, b, y);
            fn.RegisterInput(a);
            fn.RegisterInput(b);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class UnaryFunction<TOp, T> : Function
        where TOp : struct, IUnaryOp<T>
        where T : unmanaged
    {
        private readonly TOp _op;
        private readonly Tensor _x;
        private readonly Tensor _y;

        public UnaryFunction(TOp op, Tensor x, Tensor y)
        {
            _op = op; _x = x; _y = y;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dx = Tensor.Empty(_x.Shape, _x.DType, _x.Device);
            var x = _x.IsContiguous ? _x : _x.Contiguous();
            var y = _y.IsContiguous ? _y : _y.Contiguous();
            var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
            var xs = x.AsReadOnlySpan<T>();
            var ys = y.AsReadOnlySpan<T>();
            var gys = gy.AsReadOnlySpan<T>();
            var dxs = dx.AsSpan<T>();
            for (int i = 0; i < dxs.Length; i++)
                dxs[i] = _op.Backward(xs[i], ys[i], gys[i]);
            return new[] { dx };
        }
    }

    private sealed class BinaryFunction<TOp, T> : Function
        where TOp : struct, IBinaryOp<T>
        where T : unmanaged
    {
        private readonly TOp _op;
        private readonly Tensor _a, _b, _y;

        public BinaryFunction(TOp op, Tensor a, Tensor b, Tensor y)
        {
            _op = op; _a = a; _b = b; _y = y;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            // Broadcast-aware backward: вычисляем grad по каждой ячейке broadcasted-shape
            // и сразу аккумулируем в исходные формы a/b — без промежуточного Tensor.Zeros(y.Shape)
            // (сэкономили 2 тензорных аллокации размером y).
            var bc = Broadcasting.Compute(_a, _b);
            var aSpan = _a.Storage.AsReadOnlySpan<T>();
            var bSpan = _b.Storage.AsReadOnlySpan<T>();

            var yC = _y.IsContiguous ? _y : _y.Contiguous();
            var gyC = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
            var ySpan = yC.AsReadOnlySpan<T>();
            var gySpan = gyC.AsReadOnlySpan<T>();

            Tensor gradA = null, gradB = null;
            Span<T> gAs = default, gBs = default;
            if (_a.RequiresGrad)
            {
                gradA = Tensor.Zeros(_a.Shape, _a.DType, _a.Device);
                gAs = gradA.AsSpan<T>();
            }
            if (_b.RequiresGrad)
            {
                gradB = Tensor.Zeros(_b.Shape, _b.DType, _b.Device);
                gBs = gradB.AsSpan<T>();
            }

            int rank = _y.Rank;
            int aBaseOff = _a.Offset, bBaseOff = _b.Offset;
            int aReducedOff = _a.Offset; // нулевой offset для contiguous gradA
            int bReducedOff = _b.Offset;
            // Strides для записи в (contiguous) gradA/gradB по координатам broadcasted-shape:
            // если на оси был broadcast (исходный размер = 1), пишем в индекс 0 (stride=0);
            // иначе используем contiguous-stride исходной формы.
            int[] gAStrides = _a.RequiresGrad ? BuildReducedStrides(_a.Shape, _y.Shape) : null;
            int[] gBStrides = _b.RequiresGrad ? BuildReducedStrides(_b.Shape, _y.Shape) : null;

            Span<int> idx = rank <= 16 ? stackalloc int[rank] : new int[rank];
            for (int i = 0; i < rank; i++) idx[i] = 0;
            var outDims = _y.Shape.AsSpan();
            long n = _y.NumElements;

            for (long flat = 0; flat < n; flat++)
            {
                int ai = aBaseOff, bi = bBaseOff;
                int gAi = 0, gBi = 0;
                for (int k = 0; k < rank; k++)
                {
                    ai += idx[k] * bc.StridesA[k];
                    bi += idx[k] * bc.StridesB[k];
                    if (gAStrides != null) gAi += idx[k] * gAStrides[k];
                    if (gBStrides != null) gBi += idx[k] * gBStrides[k];
                }
                T av = aSpan[ai], bv = bSpan[bi];
                T yv = ySpan[(int)flat], gyv = gySpan[(int)flat];
                if (gradA != null) Add(ref gAs[gAi], _op.BackwardA(av, bv, yv, gyv));
                if (gradB != null) Add(ref gBs[gBi], _op.BackwardB(av, bv, yv, gyv));
                for (int k = rank - 1; k >= 0; k--)
                {
                    idx[k]++;
                    if (idx[k] < outDims[k]) break;
                    idx[k] = 0;
                }
            }

            return new[] { gradA, gradB };
        }

        private static void Add(ref T target, T add)
        {
            if (typeof(T) == typeof(float))
            {
                ref float t = ref System.Runtime.CompilerServices.Unsafe.As<T, float>(ref target);
                t += System.Runtime.CompilerServices.Unsafe.As<T, float>(ref add);
            }
            else if (typeof(T) == typeof(double))
            {
                ref double t = ref System.Runtime.CompilerServices.Unsafe.As<T, double>(ref target);
                t += System.Runtime.CompilerServices.Unsafe.As<T, double>(ref add);
            }
            else if (typeof(T) == typeof(int))
            {
                ref int t = ref System.Runtime.CompilerServices.Unsafe.As<T, int>(ref target);
                t += System.Runtime.CompilerServices.Unsafe.As<T, int>(ref add);
            }
            else if (typeof(T) == typeof(long))
            {
                ref long t = ref System.Runtime.CompilerServices.Unsafe.As<T, long>(ref target);
                t += System.Runtime.CompilerServices.Unsafe.As<T, long>(ref add);
            }
            else
                throw new NotSupportedException(
                    $"BinaryFunction.Add: тип {typeof(T)} не поддерживается.");
        }

        /// <summary>
        /// Построить «strides по координатам broadcasted-shape» для contiguous-тензора
        /// формы <paramref name="originalShape"/>: если на соответствующей (выровненной
        /// справа) оси исходный размер = 1 -> stride 0 (broadcast -> пишем в один и тот же элемент,
        /// что даёт суммирование). Иначе — обычный row-major stride исходной формы.
        /// </summary>
        private static int[] BuildReducedStrides(Shape originalShape, Shape broadcastShape)
        {
            int rank = broadcastShape.Rank;
            int origRank = originalShape.Rank;
            var origStrides = Strides.RowMajor(originalShape.AsSpan());
            var result = new int[rank];
            for (int i = 0; i < rank; i++)
            {
                int origIdx = i - (rank - origRank);
                if (origIdx < 0) { result[i] = 0; continue; }
                int origDim = originalShape[origIdx];
                int outDim = broadcastShape[i];
                result[i] = (origDim == 1 && outDim != 1) ? 0 : origStrides[origIdx];
            }
            return result;
        }
    }
}
