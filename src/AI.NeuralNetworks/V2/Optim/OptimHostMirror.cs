using System;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.V2.Optim;

/// <summary>
/// Внутренний хелпер для оптимизаторов: даёт CPU-«зеркало» тензора, который
/// может находиться на не-CPU устройстве (GPU/CUDA), и позволяет записать
/// обновлённые данные обратно в исходный device-storage.
/// </summary>
/// <remarks>
/// <para>
/// Все CPU-loop оптимизаторы выполняют in-place обновление через
/// <see cref="Tensor.AsSpan{T}"/>, что требует CPU-storage. Для тензоров на
/// GPU прямой <c>AsSpan</c> бросает <see cref="NotSupportedException"/> (см.
/// <c>CudaStorage.AsSpan</c>). Чтобы оптимизатор работал на любом устройстве,
/// его шаг оборачивается в:
/// </para>
/// <list type="number">
///   <item>скачать тензоры на CPU (D2H);</item>
///   <item>выполнить обычный CPU-цикл;</item>
///   <item>выгрузить изменённые тензоры обратно (H2D).</item>
/// </list>
/// <para>
/// Это медленный, но универсальный fallback для конфигураций оптимизатора,
/// у которых нет специализированного device-kernel. State-тензоры моментов
/// при этом аллоцируются на CPU (<see cref="StateDeviceForFallback"/>) и
/// больше не двигаются между устройствами на каждом шаге.
/// </para>
/// </remarks>
internal static class OptimHostMirror
{
    /// <summary>
    /// Если тензор на CPU — возвращается как есть (предполагая contiguous-leaf).
    /// Иначе — создаётся CPU-копия данных, а в <paramref name="commit"/>
    /// возвращается callback, который при вызове перезапишет device-storage
    /// данными из этой CPU-копии.
    /// </summary>
    /// <param name="t">Тензор-параметр (или state-тензор), требующий мутации.</param>
    /// <param name="commit">Callback, выгружающий данные обратно на устройство.</param>
    /// <returns>CPU-tensor, безопасный для <c>AsSpan</c>.</returns>
    public static Tensor DownloadInplace(Tensor t, out Action commit)
    {
        if (t.Device.Type == DeviceType.Cpu)
        {
            commit = static () => { };
            return t;
        }
        var cpu = t.ToCpu();
        var dstStorage = t.Storage;
        long n = t.NumElements;
        commit = () =>
        {
            if (dstStorage is IHostCopyable hc)
                hc.CopyFromHost(cpu.Storage, 0, n);
            else
                throw new NotSupportedException(
                    $"Storage {dstStorage.GetType().Name} не реализует IHostCopyable; " +
                    "невозможно выгрузить обновлённые данные оптимизатора обратно на устройство.");
        };
        return cpu;
    }

    /// <summary>
    /// Read-only вариант: скачивает тензор на CPU (если он на устройстве) без
    /// commit-callback. Для CPU-тензоров возвращает contiguous-копию (или сам
    /// тензор, если он уже contiguous).
    /// </summary>
    public static Tensor DownloadReadOnly(Tensor t)
    {
        if (t.Device.Type == DeviceType.Cpu)
            return t.IsContiguous ? t : t.Contiguous();
        return t.ToCpu();
    }

    /// <summary>
    /// Устройство, на котором следует аллоцировать persistent state-тензоры
    /// (моменты, накопители) в CPU-fallback пути оптимизатора.
    /// </summary>
    /// <remarks>
    /// Для GPU-параметров state живёт на CPU, чтобы избежать ненужного
    /// D2H/H2D трафика на каждом шаге.
    /// </remarks>
    public static Device StateDeviceForFallback(Tensor referenceParam)
        => referenceParam.Device.Type == DeviceType.Cpu ? referenceParam.Device : Device.Cpu;
}
