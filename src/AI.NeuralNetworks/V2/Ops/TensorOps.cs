using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Публичный фасад поэлементных и линейных операций над <see cref="Tensor"/>.
/// </summary>
/// <remarks>
/// Все методы — generic-диспатч через <see cref="ElementwiseDispatch"/> или
/// прямые реализации для редукций/matmul. Каждый метод поддерживает autograd
/// автоматически (если <see cref="Tensor.RequiresGrad"/> у входов).
/// </remarks>
public static partial class TensorOps
{
    #region Device dispatch

    /// <summary>
    /// Попытка диспатча через OpRegistry для нестандартных устройств (GPU, TPU…).
    /// Возвращает null — значит, идём CPU-fallback.
    /// </summary>
    private static Tensor TryDispatch(OpCode code, Tensor x)
    {
        if (x.Device.Type == DeviceType.Cpu) return null;
        var k = OpRegistry.TryGet(code, x.DType, x.Device);
        return k?.Invoke(new[] { x }, null)?[0];
    }

    private static Tensor TryDispatch(OpCode code, Tensor a, Tensor b)
    {
        EnsureSameDevice(a, b, code.ToString());
        if (a.Device.Type == DeviceType.Cpu) return null;
        var k = OpRegistry.TryGet(code, a.DType, a.Device);
        return k?.Invoke(new[] { a, b }, null)?[0];
    }

    private static void EnsureSameDevice(Tensor a, Tensor b, string opName)
    {
        if (a.Device != b.Device)
            throw new ArgumentException(
                $"{opName}: операнды должны быть на одном устройстве (a={a.Device}, b={b.Device}). " +
                "Перенесите тензор через .To(device).");
    }

    #endregion Device dispatch

    #region Type guards

    private static Tensor Float(Tensor x, string op, Func<Tensor, Tensor> fn)
    {
        if (x.DType != DType.Float32)
            throw new NotSupportedException(
                $"{op}: пока поддерживается только Float32 (Phase 1). DType={x.DType}.");
        return fn(x);
    }

    private static Tensor Float2(Tensor a, Tensor b, string op, Func<Tensor, Tensor, Tensor> fn)
    {
        if (a.DType != DType.Float32 || b.DType != DType.Float32)
            throw new NotSupportedException(
                $"{op}: пока поддерживается только Float32 (Phase 1).");
        return fn(a, b);
    }

    #endregion Type guards
}
