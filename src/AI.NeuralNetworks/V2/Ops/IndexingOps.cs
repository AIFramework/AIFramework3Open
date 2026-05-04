using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Операции для нарезки и склейки тензоров: Narrow, Slice, Cat, Stack.
/// Все поддерживают autograd через специализированные <see cref="Function"/>-узлы.
/// </summary>
public static class IndexingOps
{
    /// <summary>
    /// Узкая срезка вдоль оси <paramref name="axis"/>: возвращает view на
    /// <paramref name="length"/> элементов начиная с <paramref name="start"/>.
    /// </summary>
    /// <remarks>
    /// Zero-copy: меняет shape и offset. Backward аккумулирует grad в zero-padded
    /// тензор того же размера, что вход.
    /// </remarks>
    public static Tensor Narrow(Tensor input, int axis, int start, int length)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        int a = input.Shape.NormalizeAxis(axis);
        int dim = input.Shape[a];
        if (start < 0 || length < 0 || start + length > dim)
            throw new ArgumentOutOfRangeException(
                $"Narrow: некорректный диапазон [{start}, {start + length}) для оси длиной {dim}.");

        var newDims = input.Shape.ToArray();
        newDims[a] = length;
        var newStrides = new int[input.Rank];
        var oldStrides = input.Strides;
        for (int i = 0; i < input.Rank; i++) newStrides[i] = oldStrides[i];
        int newOffset = input.Offset + start * oldStrides[a];
        var y = new Tensor(input.Storage, new Shape(newDims), newStrides, newOffset);

        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new NarrowFunction(input.Shape, a, start);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// Выбрать один элемент вдоль оси: Narrow(axis, index, 1).Squeeze(axis).
    /// Fused-версия: один autograd-узел вместо двух (важно для RNN/LSTM при большом T).
    /// </summary>
    public static Tensor Select(Tensor input, int axis, int index)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        int a = input.Shape.NormalizeAxis(axis);
        int dim = input.Shape[a];
        if (index < 0 || index >= dim)
            throw new ArgumentOutOfRangeException(
                $"Select: индекс {index} за пределами оси длиной {dim}.");

        var newDims = new int[input.Rank - 1];
        var newStrides = new int[input.Rank - 1];
        int j = 0;
        for (int i = 0; i < input.Rank; i++)
        {
            if (i == a) continue;
            newDims[j] = input.Shape[i];
            newStrides[j] = input.Strides[i];
            j++;
        }
        int newOffset = input.Offset + index * input.Strides[a];
        var y = new Tensor(input.Storage, new Shape(newDims), newStrides, newOffset);

        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new SelectFunction(input.Shape, a, index);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// Конкатенация тензоров вдоль оси <paramref name="axis"/>. Все тензоры
    /// должны иметь одинаковую форму (кроме указанной оси), одинаковый <see cref="DType"/>
    /// и одно и то же устройство.
    /// </summary>
    public static Tensor Cat(IReadOnlyList<Tensor> tensors, int axis)
    {
        if (tensors == null || tensors.Count == 0)
            throw new ArgumentException("Cat: нужен хотя бы один тензор.");
        var first = tensors[0]
            ?? throw new ArgumentException("Cat: входной список содержит null.");
        int a = first.Shape.NormalizeAxis(axis);
        int rank = first.Rank;
        var dt = first.DType;
        var dev = first.Device;

        for (int i = 1; i < tensors.Count; i++)
        {
            var t = tensors[i] ?? throw new ArgumentException($"Cat: tensors[{i}] == null.");
            if (t.DType != dt)
                throw new ArgumentException(
                    $"Cat: dtype mismatch: tensors[0]={dt}, tensors[{i}]={t.DType}.");
            if (t.Device != dev)
                throw new ArgumentException(
                    $"Cat: device mismatch: tensors[0]={dev}, tensors[{i}]={t.Device}. " +
                    "Перенесите все тензоры на одно устройство через .To(device).");
        }

        // GPU fast-path: native scatter без D2H/H2D round-trip.
        if (dev.Type != DeviceType.Cpu && dt == DType.Float32 &&
            Environment.GetEnvironmentVariable("AI_GPU_DISABLE_CAT") != "1")
        {
            var k = OpRegistry.TryGet(OpCode.Cat, dt, dev);
            if (k != null)
            {
                int catSizeGpu = 0;
                for (int i = 0; i < tensors.Count; i++)
                {
                    var t = tensors[i];
                    if (t.Rank != rank) throw new ArgumentException("Cat: все ранги должны совпадать.");
                    for (int j = 0; j < rank; j++)
                        if (j != a && t.Shape[j] != first.Shape[j])
                            throw new ArgumentException(
                                $"Cat: формы должны совпадать кроме оси {a} ({t.Shape} vs {first.Shape}).");
                    catSizeGpu += t.Shape[a];
                }
                var insArr = new Tensor[tensors.Count];
                for (int i = 0; i < tensors.Count; i++) insArr[i] = tensors[i];
                var attrsGpu = new CatAttrs(a, GetAxisSizes(insArr, a));
                var yGpu = k(insArr, attrsGpu)[0];
                bool anyGradGpu = false;
                for (int i = 0; i < tensors.Count; i++)
                    if (tensors[i].RequiresGrad) { anyGradGpu = true; break; }
                if (TapeContext.IsGradEnabled && anyGradGpu)
                {
                    var fn = new CatFunction(a, attrsGpu.Sizes);
                    for (int i = 0; i < tensors.Count; i++) fn.RegisterInput(tensors[i]);
                    yGpu.GradFn = fn;
                }
                return yGpu;
            }
        }

        bool onGpu = dev.Type != DeviceType.Cpu;
        IReadOnlyList<Tensor> cpuTensors = tensors;
        if (onGpu)
        {
            var list = new Tensor[tensors.Count];
            for (int i = 0; i < tensors.Count; i++) list[i] = tensors[i].ToCpu();
            cpuTensors = list;
        }

        int catSize = 0;
        for (int i = 0; i < cpuTensors.Count; i++)
        {
            var t = cpuTensors[i];
            if (t.Rank != rank) throw new ArgumentException("Cat: все ранги должны совпадать.");
            for (int k = 0; k < rank; k++)
                if (k != a && t.Shape[k] != first.Shape[k])
                    throw new ArgumentException(
                        $"Cat: формы должны совпадать кроме оси {a} ({t.Shape} vs {first.Shape}).");
            catSize += t.Shape[a];
        }
        var outDims = first.Shape.ToArray();
        outDims[a] = catSize;
        var y = Tensor.Empty(new Shape(outDims), dt, Device.Cpu);

        long outer = 1;
        for (int i = 0; i < a; i++) outer *= outDims[i];
        long inner = 1;
        for (int i = a + 1; i < rank; i++) inner *= outDims[i];

        var offsets = new int[cpuTensors.Count];
        int run = 0;
        for (int i = 0; i < cpuTensors.Count; i++) { offsets[i] = run; run += cpuTensors[i].Shape[a]; }

        switch (dt)
        {
            case DType.Float32: CatBlocks<float>(cpuTensors, y, a, outer, catSize, inner, offsets); break;
            case DType.Float64: CatBlocks<double>(cpuTensors, y, a, outer, catSize, inner, offsets); break;
            case DType.Int32: CatBlocks<int>(cpuTensors, y, a, outer, catSize, inner, offsets); break;
            case DType.Int64: CatBlocks<long>(cpuTensors, y, a, outer, catSize, inner, offsets); break;
            default: throw new NotSupportedException($"Cat: dtype {dt} не поддержан.");
        }
        if (onGpu) y = y.To(dev);

        bool anyGrad = false;
        for (int i = 0; i < tensors.Count; i++)
            if (tensors[i].RequiresGrad) { anyGrad = true; break; }
        if (TapeContext.IsGradEnabled && anyGrad)
        {
            var sizes = new int[tensors.Count];
            for (int i = 0; i < tensors.Count; i++) sizes[i] = tensors[i].Shape[a];
            var fn = new CatFunction(a, sizes);
            for (int i = 0; i < tensors.Count; i++) fn.RegisterInput(tensors[i]);
            y.GradFn = fn;
        }
        return y;
    }

    private static int[] GetAxisSizes(Tensor[] tensors, int axis)
    {
        var sizes = new int[tensors.Length];
        for (int i = 0; i < tensors.Length; i++) sizes[i] = tensors[i].Shape[axis];
        return sizes;
    }

    private static void CatBlocks<T>(IReadOnlyList<Tensor> cpuTensors, Tensor y, int axis,
        long outer, int catSize, long inner, int[] offsets) where T : unmanaged
    {
        var ys = y.AsSpan<T>();
        for (int i = 0; i < cpuTensors.Count; i++)
        {
            var t = cpuTensors[i].Contiguous();
            var ts = t.AsReadOnlySpan<T>();
            int len = t.Shape[axis];
            long block = (long)len * inner;
            for (long o = 0; o < outer; o++)
            {
                long sOff = o * block;
                long dOff = (o * catSize + offsets[i]) * inner;
                ts.Slice((int)sOff, (int)block).CopyTo(ys.Slice((int)dOff, (int)block));
            }
        }
    }

    /// <summary>
    /// Stack: добавить новую ось <paramref name="axis"/> и сложить тензоры по ней.
    /// Эквивалентно Unsqueeze(axis) + Cat(axis).
    /// </summary>
    public static Tensor Stack(IReadOnlyList<Tensor> tensors, int axis)
    {
        if (tensors == null || tensors.Count == 0)
            throw new ArgumentException("Stack: нужен хотя бы один тензор.");
        var unsqueezed = new Tensor[tensors.Count];
        for (int i = 0; i < tensors.Count; i++) unsqueezed[i] = tensors[i].Unsqueeze(axis);
        return Cat(unsqueezed, axis);
    }

    private sealed class NarrowFunction : Function
    {
        private readonly Shape _xShape;
        private readonly int _axis;
        private readonly int _start;

        public NarrowFunction(Shape xShape, int axis, int start)
        {
            _xShape = xShape; _axis = axis; _start = start;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dev = gradOutput.Device;
            int len = gradOutput.Shape[_axis];

            // GPU fast-path: scatter градиента в zero-padded dx прямо на GPU.
            if (dev.Type != DeviceType.Cpu && gradOutput.DType == DType.Float32 &&
                Environment.GetEnvironmentVariable("AI_GPU_DISABLE_SCATTER") != "1")
            {
                var k = OpRegistry.TryGet(OpCode.ScatterSlice, gradOutput.DType, dev);
                if (k != null)
                {
                    using (TapeContext.NoGrad())
                    {
                        var dxGpu = Tensor.Zeros(_xShape, gradOutput.DType, dev);
                        var attrs = new ScatterAttrs(_axis, _start, len);
                        k(new[] { dxGpu, gradOutput }, attrs);
                        return new[] { dxGpu };
                    }
                }
            }

            var cpuGrad = dev.Type != DeviceType.Cpu ? gradOutput.ToCpu() : gradOutput;
            var dx = Tensor.Zeros(_xShape, cpuGrad.DType, Device.Cpu);
            int rank = _xShape.Rank;
            long outer = 1;
            for (int i = 0; i < _axis; i++) outer *= _xShape[i];
            long inner = 1;
            for (int i = _axis + 1; i < rank; i++) inner *= _xShape[i];
            int dim = _xShape[_axis];
            var dxs = dx.AsSpan<float>();
            var gys = cpuGrad.Contiguous().AsReadOnlySpan<float>();
            for (long o = 0; o < outer; o++)
            {
                long srcBase = o * len * inner;
                long dstBase = (o * dim + _start) * inner;
                for (int kk = 0; kk < len; kk++)
                {
                    long sOff = srcBase + (long)kk * inner;
                    long dOff = dstBase + (long)kk * inner;
                    for (long n = 0; n < inner; n++)
                        dxs[(int)(dOff + n)] = gys[(int)(sOff + n)];
                }
            }
            if (dev.Type != DeviceType.Cpu) dx = dx.To(dev);
            return new[] { dx };
        }
    }

    private sealed class SelectFunction : Function
    {
        private readonly Shape _xShape;
        private readonly int _axis;
        private readonly int _index;

        public SelectFunction(Shape xShape, int axis, int index)
        {
            _xShape = xShape; _axis = axis; _index = index;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dev = gradOutput.Device;

            // GPU fast-path: scatter одной «толщины» (length=1) с unsqueeze оси.
            if (dev.Type != DeviceType.Cpu && gradOutput.DType == DType.Float32 &&
                Environment.GetEnvironmentVariable("AI_GPU_DISABLE_SCATTER") != "1")
            {
                var k = OpRegistry.TryGet(OpCode.ScatterSlice, gradOutput.DType, dev);
                if (k != null)
                {
                    using (TapeContext.NoGrad())
                    {
                        var dxGpu = Tensor.Zeros(_xShape, gradOutput.DType, dev);
                        var gExpanded = gradOutput.Unsqueeze(_axis);
                        var attrs = new ScatterAttrs(_axis, _index, 1);
                        k(new[] { dxGpu, gExpanded }, attrs);
                        return new[] { dxGpu };
                    }
                }
            }

            var cpuGrad = dev.Type != DeviceType.Cpu ? gradOutput.ToCpu() : gradOutput;
            var dx = Tensor.Zeros(_xShape, cpuGrad.DType, Device.Cpu);
            int rank = _xShape.Rank;
            long outer = 1;
            for (int i = 0; i < _axis; i++) outer *= _xShape[i];
            long inner = 1;
            for (int i = _axis + 1; i < rank; i++) inner *= _xShape[i];

            var dxs = dx.AsSpan<float>();
            var gys = cpuGrad.Contiguous().AsReadOnlySpan<float>();
            for (long o = 0; o < outer; o++)
            {
                long srcBase = o * inner;
                long dstBase = (o * _xShape[_axis] + _index) * inner;
                for (long n = 0; n < inner; n++)
                    dxs[(int)(dstBase + n)] = gys[(int)(srcBase + n)];
            }
            if (dev.Type != DeviceType.Cpu) dx = dx.To(dev);
            return new[] { dx };
        }
    }

    private sealed class CatFunction : Function
    {
        private readonly int _axis;
        private readonly int[] _sizes;

        public CatFunction(int axis, int[] sizes) { _axis = axis; _sizes = sizes; }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            int n = _sizes.Length;
            var grads = new Tensor[n];
            int start = 0;
            for (int i = 0; i < n; i++)
            {
                using (TapeContext.NoGrad())
                    grads[i] = Narrow(gradOutput, _axis, start, _sizes[i]).Contiguous();
                start += _sizes[i];
            }
            return grads;
        }
    }
}
