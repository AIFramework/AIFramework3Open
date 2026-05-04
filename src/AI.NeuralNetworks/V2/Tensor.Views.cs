using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.V2;

public sealed partial class Tensor
{
    #region View-операции (zero-copy)

    /// <summary>
    /// Возвращает тензор той же физической памяти с новой формой.
    /// Требует contiguous-источник (иначе вызовите <see cref="Contiguous"/>).
    /// </summary>
    public Tensor Reshape(params int[] newDims)
    {
        var newShape = ResolveReshape(newDims);
        if (!IsContiguous)
            throw new InvalidOperationException(
                "Reshape требует contiguous-тензор. Используйте Contiguous().Reshape(...).");
        var y = new Tensor(_storage, newShape, V2.Strides.RowMajor(newShape.AsSpan()), _offset);
        var inputShape = Shape;
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            var fn = new ViewFunction(g => g.Contiguous().Reshape(inputShape.ToArray()));
            fn.RegisterInput(this);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>Алиас для совместимости с torch.</summary>
    public Tensor View(params int[] newDims) => Reshape(newDims);

    private Shape ResolveReshape(int[] newDims)
    {
        long total = NumElements;
        int negIdx = -1;
        long known = 1;
        for (int i = 0; i < newDims.Length; i++)
        {
            if (newDims[i] == -1)
            {
                if (negIdx >= 0) throw new ArgumentException("Только одна -1 разрешена в reshape.");
                negIdx = i;
            }
            else
            {
                if (newDims[i] < 0) throw new ArgumentException($"Размер {newDims[i]} некорректен.");
                known = checked(known * newDims[i]);
            }
        }
        if (negIdx >= 0)
        {
            if (known == 0)
                throw new ArgumentException("Reshape с -1 невозможен, если другие размеры дают 0.");
            if (total % known != 0)
                throw new ArgumentException(
                    $"Reshape: total={total} не делится на known={known} нацело.");
            long inferred = total / known;
            if (inferred > int.MaxValue)
                throw new OverflowException(
                    $"Reshape: выведенный размер {inferred} превышает int.MaxValue.");
            newDims = (int[])newDims.Clone();
            newDims[negIdx] = (int)inferred;
        }
        else if (known != total)
            throw new ArgumentException($"Произведение {known} != {total}.");
        return new Shape(newDims);
    }

    /// <summary>
    /// Поменять две оси местами (zero-copy, страйды свапаются).
    /// </summary>
    public Tensor Transpose(int dim0, int dim1)
    {
        int a = Shape.NormalizeAxis(dim0);
        int b = Shape.NormalizeAxis(dim1);
        var newDims = (int[])_shape.Clone();
        var newStrides = (int[])_strides.Clone();
        (newDims[a], newDims[b]) = (newDims[b], newDims[a]);
        (newStrides[a], newStrides[b]) = (newStrides[b], newStrides[a]);
        var y = new Tensor(_storage, new Shape(newDims), newStrides, _offset);
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            int da = a, db = b;
            var fn = new ViewFunction(g => g.Transpose(da, db));
            fn.RegisterInput(this);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// Перестановка осей по полному вектору перестановки (zero-copy).
    /// </summary>
    public Tensor Permute(params int[] perm)
    {
        if (perm.Length != Rank)
            throw new ArgumentException("Длина перестановки должна совпадать с Rank.");
        var seen = new bool[Rank];
        var newDims = new int[Rank];
        var newStrides = new int[Rank];
        var normPerm = new int[Rank];
        for (int i = 0; i < Rank; i++)
        {
            int p = Shape.NormalizeAxis(perm[i]);
            if (seen[p]) throw new ArgumentException($"Ось {p} повторяется в перестановке.");
            seen[p] = true;
            normPerm[i] = p;
            newDims[i] = _shape[p];
            newStrides[i] = _strides[p];
        }
        var y = new Tensor(_storage, new Shape(newDims), newStrides, _offset);
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            // Inverse permutation: inv[normPerm[i]] = i.
            var inv = new int[Rank];
            for (int i = 0; i < Rank; i++) inv[normPerm[i]] = i;
            var fn = new ViewFunction(g => g.Permute(inv));
            fn.RegisterInput(this);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>Удалить размерность 1 на оси <paramref name="axis"/>.</summary>
    public Tensor Squeeze(int axis)
    {
        int a = Shape.NormalizeAxis(axis);
        if (_shape[a] != 1) throw new ArgumentException($"Ось {a} имеет размер {_shape[a]} != 1.");
        var newDims = new int[Rank - 1];
        var newStrides = new int[Rank - 1];
        for (int i = 0, j = 0; i < Rank; i++)
        {
            if (i == a) continue;
            newDims[j] = _shape[i];
            newStrides[j] = _strides[i];
            j++;
        }
        var y = new Tensor(_storage, new Shape(newDims), newStrides, _offset);
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            int da = a;
            var fn = new ViewFunction(g => g.Unsqueeze(da));
            fn.RegisterInput(this);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>Вставить размерность 1 на позицию <paramref name="axis"/>.</summary>
    public Tensor Unsqueeze(int axis)
    {
        int a = axis < 0 ? Rank + 1 + axis : axis;
        if (a < 0 || a > Rank) throw new ArgumentOutOfRangeException(nameof(axis));
        var newDims = new int[Rank + 1];
        var newStrides = new int[Rank + 1];
        for (int i = 0, j = 0; i < Rank + 1; i++)
        {
            if (i == a)
            {
                newDims[i] = 1;
                // Stride = stride[a] если ось не последняя, иначе 1.
                newStrides[i] = a < Rank ? _strides[a] : 1;
            }
            else
            {
                newDims[i] = _shape[j];
                newStrides[i] = _strides[j];
                j++;
            }
        }
        var y = new Tensor(_storage, new Shape(newDims), newStrides, _offset);
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            int da = a;
            var fn = new ViewFunction(g => g.Squeeze(da));
            fn.RegisterInput(this);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// Расширить тензор по осям размера 1 (broadcasting через stride=0, zero-copy).
    /// Размер -1 означает «не менять».
    /// </summary>
    public Tensor Expand(params int[] newDims)
    {
        if (newDims.Length != Rank)
            throw new ArgumentException("Длина newDims должна совпадать с Rank.");
        var resultDims = new int[Rank];
        var resultStrides = new int[Rank];
        for (int i = 0; i < Rank; i++)
        {
            int target = newDims[i] == -1 ? _shape[i] : newDims[i];
            if (target == _shape[i])
            {
                resultDims[i] = target;
                resultStrides[i] = _strides[i];
            }
            else if (_shape[i] == 1)
            {
                // broadcast: stride=0
                resultDims[i] = target;
                resultStrides[i] = 0;
            }
            else
                throw new ArgumentException(
                    $"Невозможно расширить ось {i} с {_shape[i]} до {target}.");
        }
        var y = new Tensor(_storage, new Shape(resultDims), resultStrides, _offset);
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            var origShape = Shape;
            var fn = new ViewFunction(g => Ops.Broadcasting.ReduceForBroadcast(g, origShape));
            fn.RegisterInput(this);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// Вернуть contiguous-копию тензора (если уже contiguous — вернёт this).
    /// </summary>
    /// <remarks>
    /// На любом устройстве регистрируется identity-<see cref="ViewFunction"/>,
    /// чтобы contiguous-копия не рвала автоград-граф для тензоров с
    /// <see cref="RequiresGrad"/>=true (например, в LSTM/GRU.ForwardSeq на пути
    /// <c>Stack().Permute().Contiguous()</c>). До этого на GPU использовался
    /// early return через D2H/H2D round-trip, который давал свежий тензор без
    /// <see cref="GradFn"/> — backward не доходил до параметров -> loss не падал.
    /// </remarks>
    public Tensor Contiguous()
    {
        if (IsContiguous) return this;
        Tensor dst;
        if (Device.Type != DeviceType.Cpu)
        {
            // Device-native контигуас-копии пока нет (см. backlog: ILGPU strided-copy
            // kernel), поэтому используем D2H -> host-side strided copy -> H2D round-trip.
            // Медленно, но корректно по форме данных. Backend может зарегистрировать
            // OpCode.Contiguous, чтобы перехватить этот путь — тогда round-trip
            // не делается. Autograd ниже вешается ВСЕГДА, независимо от backend'а.
            dst = OpRegistry.TryGet(OpCode.Contiguous, DType, Device) is { } op
                ? op(new[] { this }, null)[0]
                : ToCpu().Contiguous().To(Device);
        }
        else
        {
            dst = Empty(Shape, DType, Device);
            if (DType == DType.Float32)
                CopyStridedToContiguous<float>(this, dst);
            else if (DType == DType.Float64)
                CopyStridedToContiguous<double>(this, dst);
            else if (DType == DType.Int32)
                CopyStridedToContiguous<int>(this, dst);
            else if (DType == DType.Int64)
                CopyStridedToContiguous<long>(this, dst);
            else
                throw new NotSupportedException($"Contiguous для {DType} ещё не реализован.");
        }
        // Сохраняем autograd-связь: contiguous-копия имеет ту же форму, что и оригинал,
        // поэтому grad напрямую перенаправляется во вход (identity). Без этого граф
        // рвётся на пути LSTM/GRU.ForwardSeq -> Permute().Contiguous().
        if (TapeContext.IsGradEnabled && RequiresGrad)
        {
            var fn = new ViewFunction(g => g);
            fn.RegisterInput(this);
            dst.GradFn = fn;
        }
        return dst;
    }

    private static void CopyStridedToContiguous<T>(Tensor src, Tensor dst) where T : unmanaged
    {
        long n = src.NumElements;
        if (n > int.MaxValue)
            throw new OverflowException(
                $"Contiguous: NumElements={n} превышает int.MaxValue.");
        if (n == 0) return;
        var srcSpan = src.Storage.AsReadOnlySpan<T>();
        var dstSpan = dst.Storage.AsSpan<T>();
        if (src.Rank == 0)
        {
            dstSpan[0] = srcSpan[src._offset];
            return;
        }

        // Найти суффикс осей с непрерывным stride=expected, чтобы скопировать его одним
        // memcpy-блоком (а внешние оси обходить N-D-индексом).
        int rank = src.Rank;
        int innerAxes = 0;
        long expected = 1;
        long innerLen = 1;
        for (int i = rank - 1; i >= 0; i--)
        {
            int dim = src._shape[i];
            if (dim == 0) return;
            int stride = src._strides[i];
            if (dim == 1)
            {
                innerAxes++;
                continue;
            }
            if (stride != expected) break;
            innerAxes++;
            innerLen = checked(innerLen * dim);
            expected = checked(expected * dim);
        }

        int outerRank = rank - innerAxes;
        long outerCount = n / innerLen;
        Span<int> idx = outerRank <= 16 ? stackalloc int[outerRank] : new int[outerRank];
        for (int i = 0; i < outerRank; i++) idx[i] = 0;

        int dstPos = 0;
        int blockLen = (int)innerLen;
        for (long o = 0; o < outerCount; o++)
        {
            int srcOff = src._offset;
            for (int i = 0; i < outerRank; i++)
                srcOff += idx[i] * src._strides[i];
            srcSpan.Slice(srcOff, blockLen).CopyTo(dstSpan.Slice(dstPos, blockLen));
            dstPos += blockLen;

            for (int k = outerRank - 1; k >= 0; k--)
            {
                idx[k]++;
                if (idx[k] < src._shape[k]) break;
                idx[k] = 0;
            }
        }
    }

    #endregion View-операции (zero-copy)
}
