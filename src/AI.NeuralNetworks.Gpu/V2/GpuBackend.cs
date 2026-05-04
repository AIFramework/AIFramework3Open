using System;
using System.Collections.Concurrent;
using System.Threading;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.Gpu.V2;

/// <summary>
/// Точка входа V2-GPU-адаптера.
/// </summary>
/// <remarks>
/// <para>
/// Один раз при старте приложения вызовите <see cref="Initialize"/> — это:
/// </para>
/// <list type="number">
///   <item>создаст <see cref="GpuContext"/> для CUDA-устройства,</item>
///   <item>зарегистрирует <see cref="CudaStorage"/> как backend для <see cref="DeviceType.Cuda"/>,</item>
///   <item>скомпилирует и зарегистрирует ILGPU-ядра в <see cref="V2.Ops.OpRegistry"/>.</item>
/// </list>
/// <para>
/// После этого можно использовать <c>tensor.To(Device.Cuda(0))</c> и стандартный V2-API
/// (<c>TensorOps.Add</c>, <c>MatMul</c> и т.д.) — диспатч на GPU будет автоматическим.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> <see cref="Initialize"/> идемпотентен и потокобезопасен.
/// Повторные вызовы возвращают тот же контекст.
/// </para>
/// </remarks>
public static class GpuBackend
{
    private static readonly ConcurrentDictionary<int, GpuContext> _contexts = new();
    private static readonly ConcurrentDictionary<int, GpuOps> _ops = new();
    private static int _initialized;
    private static readonly object _initLock = new();

    /// <summary>
    /// Инициализировать GPU-backend для устройства <paramref name="deviceIndex"/>.
    /// При повторном вызове возвращает тот же контекст (идемпотентно).
    /// </summary>
    public static GpuContext Initialize(int deviceIndex = 0)
    {
        var ctx = _contexts.GetOrAdd(deviceIndex, idx => new GpuContext(idx));
        var ops = _ops.GetOrAdd(deviceIndex, _ => new GpuOps(ctx));

        // Регистрация storage-фабрики и kernel-ов — один раз глобально.
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            lock (_initLock)
            {
                StorageBackends.Register(DeviceType.Cuda, (dt, n, idx) =>
                {
                    var c = _contexts.GetOrAdd(idx, i => new GpuContext(i));
                    return CudaStorage.Allocate(c, dt, n);
                });
                ops.Register();
            }
        }
        else
        {
            // Если уже инициализированы, но появилось новое устройство — register операции его GpuOps.
            ops.Register();
        }
        return ctx;
    }

    /// <summary>
    /// Получить контекст устройства (после <see cref="Initialize"/>).
    /// Бросает, если устройство не было инициализировано.
    /// </summary>
    public static GpuContext GetContext(int deviceIndex = 0)
    {
        if (!_contexts.TryGetValue(deviceIndex, out var ctx))
            throw new InvalidOperationException(
                $"GPU device {deviceIndex} не инициализирован. Вызовите GpuBackend.Initialize({deviceIndex}).");
        return ctx;
    }

    /// <summary>Освободить все GPU-контексты (тестовый сценарий).</summary>
    public static void Shutdown()
    {
        foreach (var c in _contexts.Values) c.Dispose();
        _contexts.Clear();
        _ops.Clear();
        Interlocked.Exchange(ref _initialized, 0);
    }
}
