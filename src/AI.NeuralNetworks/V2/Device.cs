using System;

namespace AI.ML.NeuralNetworks.V2;

/// <summary>
/// Тип устройства, на котором живёт тензор.
/// </summary>
public enum DeviceType : byte
{
    /// <summary>CPU (host memory).</summary>
    Cpu = 0,
    /// <summary>NVIDIA CUDA GPU.</summary>
    Cuda = 1,
}

/// <summary>
/// Описывает устройство: тип + индекс (для multi-GPU).
/// </summary>
/// <remarks>
/// Структура value-типа, immutable. Сравнивается по значению, эффективна как ключ.
/// Аналог <c>torch.device</c>.
/// </remarks>
public readonly struct Device : IEquatable<Device>
{
    /// <summary>Default-устройство — CPU.</summary>
    public static readonly Device Cpu = new(DeviceType.Cpu, 0);

    /// <summary>CUDA-устройство 0 (cuda:0).</summary>
    public static Device Cuda(int index = 0) => new(DeviceType.Cuda, index);

    /// <summary>Тип устройства.</summary>
    public DeviceType Type { get; }

    /// <summary>Индекс устройства (для CUDA: 0,1,…; для CPU всегда 0).</summary>
    public int Index { get; }

    /// <summary>Создать устройство.</summary>
    public Device(DeviceType type, int index = 0)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        Type = type;
        Index = index;
    }

    /// <inheritdoc/>
    public bool Equals(Device other) => Type == other.Type && Index == other.Index;

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is Device d && Equals(d);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((int)Type, Index);

    /// <summary>Сравнение устройств.</summary>
    public static bool operator ==(Device a, Device b) => a.Equals(b);
    /// <summary>Сравнение устройств.</summary>
    public static bool operator !=(Device a, Device b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => Type == DeviceType.Cpu ? "cpu" : $"cuda:{Index}";
}
