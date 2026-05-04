using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI.ML.NeuralNetworks.V2.Storage;

/// <summary>
/// CPU-хранилище данных — массив <see cref="byte"/>, поверх которого даётся
/// типизированный <see cref="Span{T}"/> через <see cref="MemoryMarshal"/>.
/// </summary>
/// <remarks>
/// Нетипизировано на уровне поля (хранит byte[]), но типизировано на уровне доступа —
/// один и тот же storage можно view'ить как <c>float[]</c>, <c>int[]</c> и т.д.
/// Это даёт zero-cost type punning внутри одного устройства и упрощает
/// реализацию <c>Tensor.View(dtype)</c>.
///
/// Память берём из <see cref="ArrayPool{T}.Shared"/> когда возможно — это
/// существенно снижает нагрузку на GC при тренировке. Финализатор базового
/// <see cref="TensorStorage"/> гарантирует возврат буфера в пул, даже если
/// пользователь не вызвал <see cref="TensorStorage.Dispose()"/> явно.
/// </remarks>
public sealed class CpuStorage : TensorStorage
{
    /// <summary>Максимально допустимый размер CPU-storage (ограничение CLR на массив).</summary>
    public const long MaxByteSize = int.MaxValue;

    private byte[] _bytes;
    private readonly bool _pooled;

    private CpuStorage(byte[] bytes, DType dt, long length, bool pooled)
        : base(dt, Device.Cpu, length)
    {
        _bytes = bytes;
        _pooled = pooled;
    }

    /// <summary>
    /// Аллоцировать новое CPU-хранилище для <paramref name="length"/> элементов
    /// типа <paramref name="dt"/>. Содержимое всегда зануляется.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">При <paramref name="length"/> &lt; 0.</exception>
    /// <exception cref="OverflowException">Если общий размер превышает <see cref="MaxByteSize"/>.</exception>
    public static CpuStorage Allocate(DType dt, long length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        long bytes = checked(length * dt.ElementSize());
        if (bytes > MaxByteSize)
            throw new OverflowException(
                $"CpuStorage: запрошено {bytes} байт, но max = {MaxByteSize}. " +
                "Используйте сегментированную аллокацию или GPU.");
        const long PoolThreshold = 1L << 20;
        if (bytes <= PoolThreshold)
        {
            byte[] arr = ArrayPool<byte>.Shared.Rent((int)bytes);
            arr.AsSpan(0, (int)bytes).Clear();
            return new CpuStorage(arr, dt, length, pooled: true);
        }
        return new CpuStorage(new byte[(int)bytes], dt, length, pooled: false);
    }

    /// <summary>
    /// Создать хранилище-копию из существующего массива (данные копируются).
    /// </summary>
    /// <remarks>
    /// Не zero-copy: <see cref="CpuStorage"/> хранит данные в собственном <c>byte[]</c>;
    /// последующие изменения исходного массива не отражаются в тензоре.
    /// </remarks>
    public static CpuStorage From<T>(T[] data) where T : unmanaged
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var dt = DTypes.FromManaged<T>();
        long byteLen = checked((long)data.Length * Unsafe.SizeOf<T>());
        if (byteLen > MaxByteSize)
            throw new OverflowException(
                $"CpuStorage.From: входной массив занимает {byteLen} байт, max = {MaxByteSize}.");
        byte[] bytes = new byte[(int)byteLen];
        MemoryMarshal.AsBytes(data.AsSpan()).CopyTo(bytes);
        return new CpuStorage(bytes, dt, data.Length, pooled: false);
    }

    /// <inheritdoc/>
    public override Span<T> AsSpan<T>()
    {
        ThrowIfDisposed();
        long byteLen = Length * DType.ElementSize();
        if (byteLen > int.MaxValue)
            throw new OverflowException($"AsSpan: размер {byteLen} байт превышает int.MaxValue.");
        return MemoryMarshal.Cast<byte, T>(_bytes.AsSpan(0, (int)byteLen));
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<T> AsReadOnlySpan<T>()
    {
        ThrowIfDisposed();
        long byteLen = Length * DType.ElementSize();
        if (byteLen > int.MaxValue)
            throw new OverflowException($"AsReadOnlySpan: размер {byteLen} байт превышает int.MaxValue.");
        return MemoryMarshal.Cast<byte, T>(_bytes.AsSpan(0, (int)byteLen));
    }

    private void ThrowIfDisposed()
    {
        if (_bytes == null)
            throw new ObjectDisposedException(nameof(CpuStorage));
    }

    /// <inheritdoc/>
    protected override void DisposeCore(bool disposing)
    {
        var b = _bytes;
        _bytes = null;
        if (b != null && _pooled)
            ArrayPool<byte>.Shared.Return(b, clearArray: false);
    }
}
