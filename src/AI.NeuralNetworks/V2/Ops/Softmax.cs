using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Softmax и LogSoftmax по произвольной оси с численно-стабильной формулой.
/// </summary>
/// <remarks>
/// softmax(x)_i = exp(x_i - max) / sum_j exp(x_j - max).
/// Backward: gx_i = softmax_i * (gy_i - sum_j softmax_j * gy_j).
/// При <c>dim == 0</c> (пустая ось) возвращает тензор тех же размерностей, заполненный нулями.
/// При вырожденной ситуации <c>sum == 0</c> (только при численном underflow по всем
/// слагаемым) выход также зануляется — это даёт стабильный градиент 0 вместо NaN.
/// </remarks>
public static class SoftmaxOps
{
    /// <summary>Softmax по оси <paramref name="axis"/>.</summary>
    public static Tensor Softmax(Tensor input, int axis = -1)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        int a = input.Shape.NormalizeAxis(axis);

        // Native GPU dispatch — без D2H/H2D round-trip.
        if (input.Device.Type != DeviceType.Cpu)
        {
            int dimG = input.Shape[a];
            long outerG = 1;
            for (int i = 0; i < a; i++) outerG *= input.Shape[i];
            long innerG = 1;
            for (int i = a + 1; i < input.Rank; i++) innerG *= input.Shape[i];
            var k = OpRegistry.TryGet(OpCode.Softmax, input.DType, input.Device);
            if (k != null && dimG > 0)
            {
                var attrs = new SoftmaxAttrs(a, outerG, dimG, innerG);
                return k(new[] { input }, attrs)[0];
            }
        }

        bool onGpu = input.Device.Type != DeviceType.Cpu;
        var x = onGpu ? input.ToCpu().Contiguous() : input.Contiguous();
        int dim = x.Shape[a];
        long outer = 1;
        for (int i = 0; i < a; i++) outer *= x.Shape[i];
        long inner = 1;
        for (int i = a + 1; i < x.Rank; i++) inner *= x.Shape[i];

        // dim == 0: пустая ось — на выходе те же размеры, заполняем нулями.
        // dim == 1: softmax = 1 для единственного элемента.
        Tensor y = dim == 0
            ? Tensor.Zeros(input.Shape, input.DType, Device.Cpu)
            : Tensor.Empty(input.Shape, input.DType, Device.Cpu);

        if (dim > 0)
        {
            var xs = x.AsReadOnlySpan<float>();
            var ys = y.AsSpan<float>();
            for (long o = 0; o < outer; o++)
            for (long n = 0; n < inner; n++)
            {
                float max = float.NegativeInfinity;
                for (int k = 0; k < dim; k++)
                {
                    long off = (o * dim + k) * inner + n;
                    float v = xs[(int)off];
                    if (v > max) max = v;
                }
                float sum = 0f;
                for (int k = 0; k < dim; k++)
                {
                    long off = (o * dim + k) * inner + n;
                    float e = MathF.Exp(xs[(int)off] - max);
                    ys[(int)off] = e;
                    sum += e;
                }
                if (sum > 0f && !float.IsInfinity(sum))
                {
                    float invSum = 1f / sum;
                    for (int k = 0; k < dim; k++)
                    {
                        long off = (o * dim + k) * inner + n;
                        ys[(int)off] *= invSum;
                    }
                }
                else
                {
                    // Underflow/Overflow: задаём ноль (стабильный backward = 0).
                    for (int k = 0; k < dim; k++)
                    {
                        long off = (o * dim + k) * inner + n;
                        ys[(int)off] = 0f;
                    }
                }
            }
        }

        if (onGpu) y = y.To(input.Device);
        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new SoftmaxFunction(y, a, outer, dim, inner);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>LogSoftmax по оси.</summary>
    public static Tensor LogSoftmax(Tensor input, int axis = -1)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        int a = input.Shape.NormalizeAxis(axis);

        // Native GPU dispatch — без D2H/H2D round-trip.
        if (input.Device.Type != DeviceType.Cpu)
        {
            int dimG = input.Shape[a];
            long outerG = 1;
            for (int i = 0; i < a; i++) outerG *= input.Shape[i];
            long innerG = 1;
            for (int i = a + 1; i < input.Rank; i++) innerG *= input.Shape[i];
            var k = OpRegistry.TryGet(OpCode.LogSoftmax, input.DType, input.Device);
            if (k != null && dimG > 0)
            {
                var attrs = new SoftmaxAttrs(a, outerG, dimG, innerG);
                return k(new[] { input }, attrs)[0];
            }
        }

        bool onGpu = input.Device.Type != DeviceType.Cpu;
        var x = onGpu ? input.ToCpu().Contiguous() : input.Contiguous();
        int dim = x.Shape[a];
        long outer = 1;
        for (int i = 0; i < a; i++) outer *= x.Shape[i];
        long inner = 1;
        for (int i = a + 1; i < x.Rank; i++) inner *= x.Shape[i];

        Tensor y = dim == 0
            ? Tensor.Zeros(input.Shape, input.DType, Device.Cpu)
            : Tensor.Empty(input.Shape, input.DType, Device.Cpu);

        if (dim > 0)
        {
            var xs = x.AsReadOnlySpan<float>();
            var ys = y.AsSpan<float>();
            for (long o = 0; o < outer; o++)
            for (long n = 0; n < inner; n++)
            {
                float max = float.NegativeInfinity;
                for (int k = 0; k < dim; k++)
                {
                    long off = (o * dim + k) * inner + n;
                    float v = xs[(int)off];
                    if (v > max) max = v;
                }
                float sumExp = 0f;
                for (int k = 0; k < dim; k++)
                {
                    long off = (o * dim + k) * inner + n;
                    sumExp += MathF.Exp(xs[(int)off] - max);
                }
                float logSum = sumExp > 0f && !float.IsInfinity(sumExp)
                    ? max + MathF.Log(sumExp)
                    : max; // underflow: log(0+eps)≈-inf — возвращаем −∞ в виде огромного отрицательного через max.
                for (int k = 0; k < dim; k++)
                {
                    long off = (o * dim + k) * inner + n;
                    ys[(int)off] = xs[(int)off] - logSum;
                }
            }
        }

        if (onGpu) y = y.To(input.Device);
        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new LogSoftmaxFunction(y, a, outer, dim, inner);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class SoftmaxFunction : Function
    {
        private readonly Tensor _y;
        private readonly int _axis, _dim;
        private readonly long _outer, _inner;
        public SoftmaxFunction(Tensor y, int axis, long outer, int dim, long inner)
        { _y = y; _axis = axis; _outer = outer; _dim = dim; _inner = inner; }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dev = _y.Device;
            var cpuY = dev.Type != DeviceType.Cpu ? _y.ToCpu() : _y;
            var cpuGy = gradOutput.Device.Type != DeviceType.Cpu ? gradOutput.ToCpu() : gradOutput;
            var ys = cpuY.AsReadOnlySpan<float>();
            var gys = (cpuGy.IsContiguous ? cpuGy : cpuGy.Contiguous()).AsReadOnlySpan<float>();
            var dx = Tensor.Zeros(_y.Shape, _y.DType, Device.Cpu);
            if (_dim > 0)
            {
                var dxs = dx.AsSpan<float>();
                for (long o = 0; o < _outer; o++)
                for (long n = 0; n < _inner; n++)
                {
                    float dot = 0f;
                    for (int k = 0; k < _dim; k++)
                    {
                        long off = (o * _dim + k) * _inner + n;
                        dot += ys[(int)off] * gys[(int)off];
                    }
                    for (int k = 0; k < _dim; k++)
                    {
                        long off = (o * _dim + k) * _inner + n;
                        dxs[(int)off] = ys[(int)off] * (gys[(int)off] - dot);
                    }
                }
            }
            if (dev.Type != DeviceType.Cpu) dx = dx.To(dev);
            return new[] { dx };
        }
    }

    private sealed class LogSoftmaxFunction : Function
    {
        private readonly Tensor _y;
        private readonly int _axis, _dim;
        private readonly long _outer, _inner;
        public LogSoftmaxFunction(Tensor y, int axis, long outer, int dim, long inner)
        { _y = y; _axis = axis; _outer = outer; _dim = dim; _inner = inner; }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dev = _y.Device;
            var cpuY = dev.Type != DeviceType.Cpu ? _y.ToCpu() : _y;
            var cpuGy = gradOutput.Device.Type != DeviceType.Cpu ? gradOutput.ToCpu() : gradOutput;
            var ys = cpuY.AsReadOnlySpan<float>();
            var gys = (cpuGy.IsContiguous ? cpuGy : cpuGy.Contiguous()).AsReadOnlySpan<float>();
            var dx = Tensor.Zeros(_y.Shape, _y.DType, Device.Cpu);
            if (_dim > 0)
            {
                var dxs = dx.AsSpan<float>();
                for (long o = 0; o < _outer; o++)
                for (long n = 0; n < _inner; n++)
                {
                    float sumGy = 0f;
                    for (int k = 0; k < _dim; k++)
                    {
                        long off = (o * _dim + k) * _inner + n;
                        sumGy += gys[(int)off];
                    }
                    for (int k = 0; k < _dim; k++)
                    {
                        long off = (o * _dim + k) * _inner + n;
                        float p = MathF.Exp(ys[(int)off]);
                        dxs[(int)off] = gys[(int)off] - p * sumGy;
                    }
                }
            }
            if (dev.Type != DeviceType.Cpu) dx = dx.To(dev);
            return new[] { dx };
        }
    }
}
