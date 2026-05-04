namespace AI.ML.NeuralNetworks.V2.Storage;

/// <summary>
/// Контракт для storage-ов, которые умеют двунаправленный обмен с host (CPU) memory.
/// Реализуется backend-ами не-CPU устройств: GPU, MPS и т.д.
/// </summary>
/// <remarks>
/// Минимальная поверхность: <see cref="CopyFromHost"/> (H2D) и <see cref="CopyToHost"/>
/// (D2H). Все детали (асинхронность, pinned-memory, streams) — внутренние для backend-а.
/// </remarks>
public interface IHostCopyable
{
    /// <summary>
    /// Скопировать <paramref name="length"/> элементов из CPU-storage <paramref name="hostSrc"/>
    /// в этот device-storage, начиная с <paramref name="dstOffset"/>.
    /// </summary>
    void CopyFromHost(TensorStorage hostSrc, long dstOffset, long length);

    /// <summary>
    /// Скопировать <paramref name="length"/> элементов из этого device-storage
    /// в CPU-storage <paramref name="hostDst"/>, начиная с <paramref name="srcOffset"/>.
    /// </summary>
    void CopyToHost(TensorStorage hostDst, long srcOffset, long length);
}
