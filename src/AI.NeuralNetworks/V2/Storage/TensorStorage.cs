using System;
using System.Threading;

namespace AI.ML.NeuralNetworks.V2.Storage;

/// <summary>
/// Абстрактное хранилище данных тензора — единый «лист» памяти, поверх которого
/// несколько <see cref="Tensor"/>-view могут существовать одновременно.
/// </summary>
/// <remarks>
/// Аналог <c>at::Storage</c> в PyTorch. Storage отделён от Tensor — это позволяет
/// делать zero-copy reshape/permute/slice (новый Tensor с тем же storage и другими
/// strides/offset). Время жизни управляется GC: каждый <see cref="Tensor"/> держит
/// сильную ссылку на storage; финализатор гарантирует возврат pooled-буферов и
/// освобождение device-памяти даже при отсутствии явного <see cref="Dispose()"/>.
/// </remarks>
public abstract class TensorStorage : IDisposable
{
    private int _disposed;

    /// <summary>Тип элементов хранилища.</summary>
    public DType DType { get; }

    /// <summary>Устройство, на котором лежит память.</summary>
    public Device Device { get; }

    /// <summary>Количество элементов (не байт).</summary>
    public long Length { get; }

    /// <summary>Размер в байтах.</summary>
    public long ByteSize => Length * DType.ElementSize();

    /// <summary>True, если <see cref="Dispose()"/> уже был вызван.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Базовый конструктор.</summary>
    protected TensorStorage(DType dtype, Device device, long length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        DType = dtype;
        Device = device;
        Length = length;
    }

    /// <summary>
    /// Получить типизированный <see cref="Span{T}"/> на ВСЕ элементы хранилища (CPU only).
    /// Для GPU storage метод бросает <see cref="NotSupportedException"/>.
    /// </summary>
    public abstract Span<T> AsSpan<T>() where T : unmanaged;

    /// <summary>
    /// Тот же span, но read-only — для read-from-multiple-threads сценариев.
    /// </summary>
    public abstract ReadOnlySpan<T> AsReadOnlySpan<T>() where T : unmanaged;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { DisposeCore(disposing: true); }
        finally { GC.SuppressFinalize(this); }
    }

    /// <summary>
    /// Финализатор: на случай, если пользователь не вызвал <see cref="Dispose()"/>.
    /// Возвращает pooled-буферы и освобождает device-память без обращения к managed-объектам.
    /// </summary>
    ~TensorStorage()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { DisposeCore(disposing: false); }
        catch { /* финализатор не должен бросать */ }
    }

    /// <summary>
    /// Освободить ресурсы, специфичные для backend-а.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> при явном вызове <see cref="Dispose()"/> (можно трогать managed-объекты);
    /// <c>false</c> из финализатора (только unmanaged ресурсы).
    /// </param>
    protected abstract void DisposeCore(bool disposing);
}
