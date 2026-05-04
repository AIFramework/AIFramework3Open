using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;

namespace AI.ML.NeuralNetworks.Gpu;

/// <summary>
/// Управление жизненным циклом ILGPU Context и CUDA Accelerator.
/// </summary>
public sealed class GpuContext : IDisposable
{
    /// <summary>
    /// ILGPU Context (владеет всеми ресурсами).
    /// </summary>
    public Context Context { get; }

    /// <summary>
    /// CUDA-ускоритель, через который запускаются ядра и аллоцируется память.
    /// </summary>
    public Accelerator Accelerator { get; }

    /// <summary>
    /// cuBLAS handle (graceful fallback if not available).
    /// </summary>
    internal CuBlas.CuBlasHandle CuBlas { get; }

    /// <summary>
    /// Имя устройства.
    /// </summary>
    public string DeviceName => Accelerator.Device.Name;

    /// <summary>
    /// Индекс CUDA-устройства, переданный в конструктор. Используется для построения
    /// корректного <see cref="AI.ML.NeuralNetworks.V2.Device"/> у тензоров и для
    /// диспатча операций по multi-GPU.
    /// </summary>
    public int DeviceIndex { get; }

    /// <summary>
    /// Создаёт контекст для CUDA-устройства с указанным индексом.
    /// </summary>
    public GpuContext(int deviceIndex = 0)
    {
        DeviceIndex = deviceIndex;
        Context = Context.Create(builder => builder.Cuda().EnableAlgorithms());
        var devices = Context.GetCudaDevices();
        int count = 0;
        CudaDevice selected = null;
        foreach (var d in devices)
        {
            if (count == deviceIndex) selected = d;
            count++;
        }
        if (count == 0)
            throw new InvalidOperationException("CUDA-устройства не найдены. Убедитесь, что установлен NVIDIA-драйвер.");
        if (selected == null)
            throw new ArgumentOutOfRangeException(nameof(deviceIndex),
                $"Запрошено устройство {deviceIndex}, доступно {count}.");

        Accelerator = selected.CreateAccelerator(Context);
        CuBlas = new CuBlas.CuBlasHandle();
        if (CuBlas.IsAvailable && Accelerator is CudaAccelerator cudaAcc
            && cudaAcc.DefaultStream is CudaStream cudaStream)
            CuBlas.SetStream(cudaStream);
    }

    /// <summary>
    /// Монитор формы тренировочного шага (см. <see cref="CudaGraphs.StepShapeMonitor"/>).
    /// Используется для эвристик «warm graph» при будущей поддержке CUDA Graph capture.
    /// </summary>
    public CudaGraphs.StepShapeMonitor StepRecorder { get; } = new();

    /// <summary>
    /// Синхронное ожидание завершения всех операций на GPU.
    /// </summary>
    public void Synchronize() => Accelerator.Synchronize();

    /// <summary>
    /// Второй CUDA-стрим для асинхронных копий H2D/D2H, параллельных с вычислениями.
    /// </summary>
    public AcceleratorStream CopyStream => _copyStream;

    private AcceleratorStream _copyStream;
    private readonly object _copyStreamLock = new();

    /// <summary>
    /// Инициализирует второй стрим (lazy, потокобезопасно).
    /// Идемпотентно: повторные вызовы не создают дополнительных стримов.
    /// </summary>
    public void InitCopyStream()
    {
        if (_copyStream != null) return;
        lock (_copyStreamLock)
        {
            if (_copyStream != null) return;
            // Записываем через локальную переменную и Volatile, чтобы другой поток
            // не увидел частично сконструированный объект через свойство CopyStream.
            var stream = Accelerator.CreateStream();
            System.Threading.Volatile.Write(ref _copyStream, stream);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _copyStream?.Dispose();
        CuBlas?.Dispose();
        Accelerator?.Dispose();
        Context?.Dispose();
    }
}
