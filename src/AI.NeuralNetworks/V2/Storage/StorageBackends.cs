using System;
using System.Collections.Concurrent;

namespace AI.ML.NeuralNetworks.V2.Storage;

/// <summary>
/// Делегат-фабрика хранилища: создать <see cref="TensorStorage"/> заданного
/// dtype и размера на конкретном устройстве.
/// </summary>
public delegate TensorStorage StorageFactory(DType dtype, long length, int deviceIndex);

/// <summary>
/// Реестр backend-ов хранилищ. Позволяет проектам уровня выше (например,
/// <c>AI.NeuralNetworks.Gpu</c>) регистрировать собственные реализации
/// <see cref="TensorStorage"/> для не-CPU устройств без циклической зависимости.
/// </summary>
/// <remarks>
/// <para>
/// CPU-backend (<see cref="CpuStorage"/>) зашит в ядро. Регистрация GPU/MPS/etc.
/// делается один раз при инициализации соответствующего адаптера, например:
/// <c>StorageBackends.Register(DeviceType.Cuda, (dt, n, idx) =&gt; new CudaStorage(...))</c>.
/// </para>
/// <para>
/// Регистрация повторно — заменяет предыдущий backend (полезно для тестов).
/// Потокобезопасно через <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </para>
/// </remarks>
public static class StorageBackends
{
    private static readonly ConcurrentDictionary<DeviceType, StorageFactory> _factories = new();

    /// <summary>Зарегистрировать фабрику для типа устройства.</summary>
    public static void Register(DeviceType deviceType, StorageFactory factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[deviceType] = factory;
    }

    /// <summary>Найти фабрику для устройства или null.</summary>
    public static StorageFactory TryGet(DeviceType deviceType)
        => _factories.TryGetValue(deviceType, out var f) ? f : null;

    /// <summary>Зарегистрирован ли backend для устройства.</summary>
    public static bool IsRegistered(DeviceType deviceType) => _factories.ContainsKey(deviceType);

    /// <summary>Аллоцировать storage через зарегистрированный backend.</summary>
    public static TensorStorage Allocate(DType dtype, Device device, long length)
    {
        if (device.Type == DeviceType.Cpu)
            return CpuStorage.Allocate(dtype, length);
        var f = TryGet(device.Type)
            ?? throw new InvalidOperationException(
                $"Backend для {device.Type} не зарегистрирован. " +
                "Подключите соответствующий адаптер (например, GpuBackend.Initialize).");
        return f(dtype, length, device.Index);
    }
}
