namespace AI.ML.NeuralNetworks.V2.Storage;

/// <summary>
/// Опциональный контракт storage: умеет заполнять весь буфер скалярным значением
/// без обхода через CPU. Реализуется backend-ами, у которых есть native memset/fill
/// (например, ILGPU <c>MemSetToZero</c>, CUDA <c>cudaMemsetD32</c>, fill-kernel и т.п.).
/// </summary>
/// <remarks>
/// Возвращает <c>true</c>, если backend выполнил заполнение; <c>false</c> — если
/// потребовалось пройти через стандартный H2D-путь (вызывающий код обязан сделать
/// fallback). Это позволяет добавлять оптимизированные fill постепенно без поломок.
/// </remarks>
public interface IDeviceFillable
{
    /// <summary>
    /// Заполнить буфер значением <paramref name="value"/> (приведённым к <see cref="TensorStorage.DType"/>).
    /// </summary>
    /// <returns><c>true</c>, если заполнение выполнено backend-ом; иначе <c>false</c>.</returns>
    bool TryFill(double value);
}
