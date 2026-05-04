using System;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Buffer — необучаемый тензор модуля (running stats, positional encoding, маски и т.д.).
/// Аналог буферов в <c>torch.nn.Module</c>.
/// </summary>
/// <remarks>
/// Обёртка вокруг <see cref="Tensor"/>, симметричная <see cref="Parameter"/>.
/// При <see cref="Module.To(Device)"/> вызывается <see cref="MoveTo"/>, обновляя
/// внутреннюю ссылку <see cref="Tensor"/>. Любые поля модуля, хранящие <c>Buffer</c>,
/// автоматически видят тензор на новом устройстве — без пересинхронизации.
/// </remarks>
public sealed class Buffer
{
    /// <summary>Тензор-носитель.</summary>
    public Tensor Tensor { get; private set; }

    /// <summary>Имя буфера (в иерархии модулей; устанавливается при регистрации).</summary>
    public string Name { get; internal set; }

    /// <summary>Создать буфер из тензора.</summary>
    public Buffer(Tensor tensor)
    {
        Tensor = tensor ?? throw new ArgumentNullException(nameof(tensor));
    }

    /// <summary>Переместить буфер на другое устройство (in-place в обёртке).</summary>
    internal void MoveTo(Device device)
    {
        if (Tensor.Device == device) return;
        Tensor = Tensor.To(device);
    }

    /// <summary>Неявное приведение к <see cref="Tensor"/> для удобства в forward.</summary>
    public static implicit operator Tensor(Buffer b) => b?.Tensor;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Buffer(name={Name ?? "?"}, shape={Tensor.Shape}, dtype={Tensor.DType}, device={Tensor.Device})";
}
