using System;
using System.Threading;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Storage;
using ILGPU;
using ILGPU.Runtime;

namespace AI.ML.NeuralNetworks.Gpu.V2;

/// <summary>
/// V2-CUDA-хранилище: типизированный <see cref="MemoryBuffer1D{T,TStride}"/> на GPU.
/// Реализует <see cref="TensorStorage"/> и <see cref="IHostCopyable"/> — чтобы V2-Tensor
/// мог переноситься между CPU и GPU через <c>tensor.To(Device.Cuda(0))</c>.
/// </summary>
/// <remarks>
/// <para>
/// Хранит данные как <c>byte[]</c>-эквивалент в GPU-памяти (через <c>MemoryBuffer1D&lt;byte&gt;</c>).
/// Это позволяет использовать тот же storage для разных dtype через
/// <c>MemoryMarshal.Cast</c>-style преобразование view'ов.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> все операции с <c>MemoryBuffer1D</c> — потокобезопасны
/// со стороны ILGPU. <see cref="TensorStorage.Dispose()"/> атомарен; финализатор базового
/// класса гарантирует освобождение GPU-памяти, если пользователь забыл явно вызвать Dispose.
/// </para>
/// </remarks>
public sealed class CudaStorage : TensorStorage, IHostCopyable
{
    private MemoryBuffer1D<byte, Stride1D.Dense> _buffer;
    private readonly GpuContext _gpu;

    /// <summary>GPU-контекст, владеющий буфером.</summary>
    public GpuContext Gpu => _gpu;

    /// <summary>Прямой доступ к ILGPU-buffer view (байты).</summary>
    internal ArrayView<byte> ByteView => _buffer.View.BaseView;

    /// <summary>Получить типизированный view (без копии).</summary>
    public ArrayView<T> AsView<T>() where T : unmanaged
        => _buffer.View.BaseView.Cast<T>();

    private CudaStorage(GpuContext gpu, MemoryBuffer1D<byte, Stride1D.Dense> buffer, DType dtype, long length)
        : base(dtype, AI.ML.NeuralNetworks.V2.Device.Cuda(gpu.DeviceIndex), length)
    {
        _gpu = gpu;
        _buffer = buffer;
    }

    /// <summary>
    /// Создать GPU-storage указанного dtype/длины. Память зануляется (zero-init) для
    /// безопасности при чтении неинициализированных тензоров; стоимость пренебрежимо
    /// мала по сравнению с обучением.
    /// </summary>
    public static CudaStorage Allocate(GpuContext gpu, DType dtype, long length)
    {
        if (gpu == null) throw new ArgumentNullException(nameof(gpu));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        long bytes = checked(length * dtype.ElementSize());
        if (bytes > int.MaxValue)
            throw new InvalidOperationException(
                $"Запрошено {bytes} байт — выше int.MaxValue.");
        var buf = gpu.Accelerator.Allocate1D<byte>(bytes);
        buf.MemSetToZero();
        return new CudaStorage(gpu, buf, dtype, length);
    }

    /// <inheritdoc/>
    public override Span<T> AsSpan<T>()
        => throw new NotSupportedException(
            "CudaStorage.AsSpan не поддерживается. Используйте AsView<T>() или ToCpu().");

    /// <inheritdoc/>
    public override ReadOnlySpan<T> AsReadOnlySpan<T>()
        => throw new NotSupportedException(
            "CudaStorage.AsReadOnlySpan не поддерживается. Используйте AsView<T>() или ToCpu().");

    /// <inheritdoc/>
    protected override void DisposeCore(bool disposing)
    {
        var b = Interlocked.Exchange(ref _buffer, null);
        b?.Dispose();
    }

    #region IHostCopyable

    /// <inheritdoc/>
    public void CopyFromHost(TensorStorage hostSrc, long dstOffset, long length)
    {
        if (hostSrc == null) throw new ArgumentNullException(nameof(hostSrc));
        if (hostSrc.Device.Type != DeviceType.Cpu)
            throw new ArgumentException("CopyFromHost: source должен быть на CPU.", nameof(hostSrc));
        if (hostSrc.DType != DType)
            throw new ArgumentException($"CopyFromHost: dtype mismatch ({hostSrc.DType} vs {DType}).", nameof(hostSrc));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (dstOffset < 0) throw new ArgumentOutOfRangeException(nameof(dstOffset));
        if (dstOffset + length > Length)
            throw new ArgumentException(
                $"CopyFromHost: диапазон [{dstOffset}..{dstOffset + length}) выходит за Length={Length}.");
        if (length > hostSrc.Length)
            throw new ArgumentException(
                $"CopyFromHost: source имеет {hostSrc.Length} элементов, запрошено {length}.");
        if (length == 0) return;

        long bytes = checked(length * DType.ElementSize());
        long byteOffset = checked(dstOffset * DType.ElementSize());
        if (bytes > int.MaxValue)
            throw new OverflowException($"CopyFromHost: запрошено {bytes} байт — больше int.MaxValue.");

        var srcBytes = hostSrc.AsReadOnlySpan<byte>().Slice(0, (int)bytes);
        var slice = _buffer.View.SubView(byteOffset, bytes);
        // Прямая pinned-копия через CopyStream (если он инициализирован) — без
        // промежуточного массива, который раньше делал .ToArray(). При отсутствии
        // CopyStream используем DefaultStream — поведение по умолчанию.
        var stream = _gpu.CopyStream ?? _gpu.Accelerator.DefaultStream;
        slice.CopyFromCPU(stream, srcBytes);
    }

    /// <inheritdoc/>
    public void CopyToHost(TensorStorage hostDst, long srcOffset, long length)
    {
        if (hostDst == null) throw new ArgumentNullException(nameof(hostDst));
        if (hostDst.Device.Type != DeviceType.Cpu)
            throw new ArgumentException("CopyToHost: destination должен быть на CPU.", nameof(hostDst));
        if (hostDst.DType != DType)
            throw new ArgumentException($"CopyToHost: dtype mismatch ({hostDst.DType} vs {DType}).", nameof(hostDst));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (srcOffset < 0) throw new ArgumentOutOfRangeException(nameof(srcOffset));
        if (srcOffset + length > Length)
            throw new ArgumentException(
                $"CopyToHost: диапазон [{srcOffset}..{srcOffset + length}) выходит за Length={Length}.");
        if (length > hostDst.Length)
            throw new ArgumentException(
                $"CopyToHost: destination имеет {hostDst.Length} элементов, запрошено {length}.");
        if (length == 0) return;

        long bytes = checked(length * DType.ElementSize());
        long byteOffset = checked(srcOffset * DType.ElementSize());
        if (bytes > int.MaxValue)
            throw new OverflowException($"CopyToHost: запрошено {bytes} байт — больше int.MaxValue.");

        var slice = _buffer.View.SubView(byteOffset, bytes);
        // Прямая копия в host-span без промежуточного массива.
        var stream = _gpu.CopyStream ?? _gpu.Accelerator.DefaultStream;
        var dstSpan = hostDst.AsSpan<byte>().Slice(0, (int)bytes);
        slice.CopyToCPU(stream, dstSpan);
    }
    #endregion IHostCopyable

}