using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

public static partial class TensorOps
{
    #region Унарные

    /// <summary>Поэлементное отрицание.</summary>
    public static Tensor Neg(Tensor x) => Float(x, "Neg",
        (s) => TryDispatch(OpCode.Neg, s) ?? ElementwiseDispatch.Unary<Float32Ops.Neg, float>(s, "Neg"));

    /// <summary>Модуль.</summary>
    public static Tensor Abs(Tensor x) => Float(x, "Abs",
        (s) => TryDispatch(OpCode.Abs, s) ?? ElementwiseDispatch.Unary<Float32Ops.Abs, float>(s, "Abs"));

    /// <summary>Экспонента.</summary>
    public static Tensor Exp(Tensor x) => Float(x, "Exp",
        (s) => TryDispatch(OpCode.Exp, s) ?? ElementwiseDispatch.Unary<Float32Ops.Exp, float>(s, "Exp"));

    /// <summary>Натуральный логарифм.</summary>
    public static Tensor Log(Tensor x) => Float(x, "Log",
        (s) => TryDispatch(OpCode.Log, s) ?? ElementwiseDispatch.Unary<Float32Ops.Log, float>(s, "Log"));

    /// <summary>Квадратный корень.</summary>
    public static Tensor Sqrt(Tensor x) => Float(x, "Sqrt",
        (s) => TryDispatch(OpCode.Sqrt, s) ?? ElementwiseDispatch.Unary<Float32Ops.Sqrt, float>(s, "Sqrt"));

    /// <summary>Синус.</summary>
    public static Tensor Sin(Tensor x) => Float(x, "Sin",
        (s) => TryDispatch(OpCode.Sin, s) ?? ElementwiseDispatch.Unary<Float32Ops.Sin, float>(s, "Sin"));

    /// <summary>Косинус.</summary>
    public static Tensor Cos(Tensor x) => Float(x, "Cos",
        (s) => TryDispatch(OpCode.Cos, s) ?? ElementwiseDispatch.Unary<Float32Ops.Cos, float>(s, "Cos"));

    /// <summary>ReLU.</summary>
    public static Tensor Relu(Tensor x) => Float(x, "Relu",
        (s) => TryDispatch(OpCode.Relu, s) ?? ElementwiseDispatch.Unary<Float32Ops.Relu, float>(s, "Relu"));

    /// <summary>Сигмоида.</summary>
    public static Tensor Sigmoid(Tensor x) => Float(x, "Sigmoid",
        (s) => TryDispatch(OpCode.Sigmoid, s) ?? ElementwiseDispatch.Unary<Float32Ops.Sigmoid, float>(s, "Sigmoid"));

    /// <summary>Гиперболический тангенс.</summary>
    public static Tensor Tanh(Tensor x) => Float(x, "Tanh",
        (s) => TryDispatch(OpCode.Tanh, s) ?? ElementwiseDispatch.Unary<Float32Ops.Tanh, float>(s, "Tanh"));

    /// <summary>SiLU (Swish): x * sigmoid(x).</summary>
    public static Tensor Silu(Tensor x) => Float(x, "Silu",
        (s) => TryDispatch(OpCode.Silu, s) ?? ElementwiseDispatch.Unary<Float32Ops.Silu, float>(s, "Silu"));

    /// <summary>GELU (точный, через tanh-аппроксимацию).</summary>
    public static Tensor Gelu(Tensor x) => Float(x, "Gelu",
        (s) => TryDispatch(OpCode.Gelu, s) ?? ElementwiseDispatch.Unary<Float32Ops.Gelu, float>(s, "Gelu"));

    #endregion Унарные

    #region Бинарные

    /// <summary>Поэлементное сложение с broadcasting.</summary>
    public static Tensor Add(Tensor a, Tensor b) => Float2(a, b, "Add",
        (x, y) => TryDispatch(OpCode.Add, x, y) ?? ElementwiseDispatch.Binary<Float32Ops.Add, float>(x, y, "Add"));

    /// <summary>Поэлементное вычитание с broadcasting.</summary>
    public static Tensor Sub(Tensor a, Tensor b) => Float2(a, b, "Sub",
        (x, y) => TryDispatch(OpCode.Sub, x, y) ?? ElementwiseDispatch.Binary<Float32Ops.Sub, float>(x, y, "Sub"));

    /// <summary>Поэлементное умножение с broadcasting.</summary>
    public static Tensor Mul(Tensor a, Tensor b) => Float2(a, b, "Mul",
        (x, y) => TryDispatch(OpCode.Mul, x, y) ?? ElementwiseDispatch.Binary<Float32Ops.Mul, float>(x, y, "Mul"));

    /// <summary>Поэлементное деление с broadcasting.</summary>
    public static Tensor Div(Tensor a, Tensor b) => Float2(a, b, "Div",
        (x, y) => TryDispatch(OpCode.Div, x, y) ?? ElementwiseDispatch.Binary<Float32Ops.Div, float>(x, y, "Div"));

    /// <summary>Поэлементное возведение в степень.</summary>
    public static Tensor Pow(Tensor a, Tensor b) => Float2(a, b, "Pow",
        (x, y) => TryDispatch(OpCode.Pow, x, y) ?? ElementwiseDispatch.Binary<Float32Ops.Pow, float>(x, y, "Pow"));

    #endregion Бинарные

    #region Скалярные хелперы

    /// <summary>
    /// y = x + s. Fused: не аллоцирует временный broadcasted-tensor для скаляра.
    /// </summary>
    public static Tensor AddScalar(Tensor x, float s)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (x.DType != DType.Float32)
            throw new NotSupportedException(
                $"AddScalar: пока поддерживается только Float32, dtype={x.DType}.");
        // Для не-CPU устройств: при отсутствии fused-kernel падаем на путь через Add+Full.
        if (x.Device.Type != DeviceType.Cpu)
        {
            var sT = Tensor.Full(new Shape(), s, x.DType, x.Device);
            return Add(x, sT);
        }
        var src = x.IsContiguous ? x : x.Contiguous();
        var y = Tensor.Empty(x.Shape, x.DType, x.Device);
        var xs = src.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        for (int i = 0; i < ys.Length; i++) ys[i] = xs[i] + s;
        if (TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            var fn = new ScalarShiftFunction();
            fn.RegisterInput(x);
            y.GradFn = fn;
        }
        return y;
    }

    /// <summary>
    /// y = x * s. Fused: не аллоцирует временный broadcasted-tensor для скаляра.
    /// </summary>
    public static Tensor MulScalar(Tensor x, float s)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (x.DType != DType.Float32)
            throw new NotSupportedException(
                $"MulScalar: пока поддерживается только Float32, dtype={x.DType}.");

        // Native GPU fast-path: вызываем _smul напрямую (без broadcast'а через Mul).
        // Backward тоже на GPU через тот же _smul. Убирает broadcast и
        // CpuFallbackBinaryFn для всех `tensor * float` (typical: scores * (1/√d)).
        if (x.Device.Type != DeviceType.Cpu)
        {
            var k = OpRegistry.TryGet(OpCode.MulScalar, x.DType, x.Device);
            if (k != null)
            {
                var attrs = new ScalarAttrs(s);
                var y = k(new[] { x }, attrs)[0];
                if (TapeContext.IsGradEnabled && x.RequiresGrad)
                {
                    var fn = new ScalarMulFunction(s);
                    fn.RegisterInput(x);
                    y.GradFn = fn;
                }
                return y;
            }
            // Fallback на путь через Mul+broadcast (CPU-fallback в backward).
            var sT = Tensor.Full(new Shape(), s, x.DType, x.Device);
            return Mul(x, sT);
        }
        var src = x.IsContiguous ? x : x.Contiguous();
        var yc = Tensor.Empty(x.Shape, x.DType, x.Device);
        var xs = src.AsReadOnlySpan<float>();
        var ys = yc.AsSpan<float>();
        for (int i = 0; i < ys.Length; i++) ys[i] = xs[i] * s;
        if (TapeContext.IsGradEnabled && x.RequiresGrad)
        {
            var fn = new ScalarMulFunction(s);
            fn.RegisterInput(x);
            yc.GradFn = fn;
        }
        return yc;
    }

    /// <summary>Backward для AddScalar: gx = gy.</summary>
    private sealed class ScalarShiftFunction : Function
    {
        public override Tensor[] Backward(Tensor gradOutput) => new[] { gradOutput };
    }

    /// <summary>Backward для MulScalar: gx = s * gy.</summary>
    private sealed class ScalarMulFunction : Function
    {
        private readonly float _s;
        public ScalarMulFunction(float s) { _s = s; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
                return new[] { MulScalar(gradOutput, _s) };
        }
    }

    #endregion Скалярные хелперы
}
