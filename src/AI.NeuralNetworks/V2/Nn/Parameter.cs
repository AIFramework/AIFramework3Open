using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Parameter — обучаемый тензор модуля. По существу обёртка-маркер вокруг
/// <see cref="Tensor"/> с включённым <c>requires_grad</c>.
/// </summary>
/// <remarks>
/// <para>
/// Аналог <c>torch.nn.Parameter</c>. Выделен в отдельный тип, чтобы:
/// <list type="bullet">
///   <item>модуль мог отличить «обучаемые» тензоры от «буферов» (running stats);</item>
///   <item>API <see cref="Module.Parameters"/> возвращал именно параметры;</item>
///   <item>сериализация и optimizer работали по типу.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class Parameter
{
    /// <summary>Тензор-носитель.</summary>
    public Tensor Tensor { get; private set; }

    /// <summary>Имя параметра (в иерархии модулей; устанавливается при регистрации).</summary>
    public string Name { get; internal set; }

    /// <summary>Создать параметр из тензора. Автоматически выставит requires_grad=true.</summary>
    public Parameter(Tensor tensor)
    {
        if (tensor == null) throw new ArgumentNullException(nameof(tensor));
        if (tensor.GradFn != null)
            throw new ArgumentException(
                "Parameter должен быть leaf-тензором (без GradFn).", nameof(tensor));
        Tensor = tensor.SetRequiresGrad(true);
    }

    /// <summary>Переместить параметр на другое устройство (in-place в обёртке).</summary>
    internal void MoveTo(Device device)
    {
        if (Tensor.Device == device) return;
        Tensor = Tensor.To(device).SetRequiresGrad(true);
    }

    /// <summary>Неявное приведение к <see cref="Tensor"/> для удобства в forward.</summary>
    public static implicit operator Tensor(Parameter p) => p?.Tensor;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Parameter(name={Name ?? "?"}, shape={Tensor.Shape}, dtype={Tensor.DType})";
}
