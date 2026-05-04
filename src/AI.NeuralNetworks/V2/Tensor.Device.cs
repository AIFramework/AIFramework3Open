using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.V2;

public sealed partial class Tensor
{
    #region Миграция между устройствами

    /// <summary>
    /// Перенести тензор на указанное устройство. Если тензор уже на нём — вернёт <c>this</c>.
    /// На целевом устройстве должен быть зарегистрирован backend (см. <see cref="StorageBackends"/>).
    /// </summary>
    /// <remarks>
    /// Перенос — copy-операция (через CPU, если backend сам не предоставил
    /// быстрый путь H2D/D2H). Для GPU storage-факторий рекомендуется в будущем
    /// добавить специализированный <c>CopyFromCpu</c>/<c>CopyToCpu</c>.
    /// Перенос автоградной leaf-семантики: возвращённый тензор — новый leaf
    /// (без GradFn). Если требуется grad — вызовите <see cref="SetRequiresGrad(bool)"/>.
    /// </remarks>
    public Tensor To(Device device)
    {
        if (device == Device) return this;
        // 1) Сначала собираем contiguous-CPU-копию с offset=0. Для CPU-views с
        //    non-trivial offset (например, Select-view, у которого IsContiguous=true,
        //    но Offset≠0) Contiguous() возвращает self — этого недостаточно, иначе
        //    CopyFromHost/CopyDtypeBytes ниже копируют из неверного смещения.
        Tensor cpuSrc;
        if (Device.Type == DeviceType.Cpu)
        {
            cpuSrc = this.Contiguous();
            if (cpuSrc._offset != 0)
            {
                // Принудительно материализуем offset=0 копию.
                var fresh = Empty(cpuSrc.Shape, cpuSrc.DType, Device.Cpu);
                CopyOffsetBytes(cpuSrc.Storage, cpuSrc._offset, fresh.Storage, 0,
                    cpuSrc.NumElements, cpuSrc.DType);
                cpuSrc = fresh;
            }
        }
        else
        {
            cpuSrc = ToCpu(); // ToCpu теперь корректно учитывает Offset.
        }
        // 2) Аллоцируем storage на целевом устройстве и копируем по байтам.
        var dstStorage = StorageBackends.Allocate(DType, device, NumElements);
        if (dstStorage is Storage.IHostCopyable hc)
        {
            hc.CopyFromHost(cpuSrc.Storage, 0, NumElements);
        }
        else if (device.Type == DeviceType.Cpu)
        {
            CopyDtypeBytes(cpuSrc.Storage, dstStorage, NumElements, DType);
        }
        else
        {
            throw new NotSupportedException(
                $"Backend для {device.Type} не поддерживает host-copy. " +
                "Реализуйте IHostCopyable в storage.");
        }
        return new Tensor(dstStorage, Shape, V2.Strides.RowMajor(Shape.AsSpan()), 0);
    }

    private static void CopyOffsetBytes(TensorStorage src, long srcOffset,
        TensorStorage dst, long dstOffset, long n, DType dt)
    {
        switch (dt)
        {
            case DType.Float32:
                src.AsReadOnlySpan<float>().Slice((int)srcOffset, (int)n)
                    .CopyTo(dst.AsSpan<float>().Slice((int)dstOffset, (int)n));
                break;
            case DType.Float64:
                src.AsReadOnlySpan<double>().Slice((int)srcOffset, (int)n)
                    .CopyTo(dst.AsSpan<double>().Slice((int)dstOffset, (int)n));
                break;
            case DType.Int32:
                src.AsReadOnlySpan<int>().Slice((int)srcOffset, (int)n)
                    .CopyTo(dst.AsSpan<int>().Slice((int)dstOffset, (int)n));
                break;
            case DType.Int64:
                src.AsReadOnlySpan<long>().Slice((int)srcOffset, (int)n)
                    .CopyTo(dst.AsSpan<long>().Slice((int)dstOffset, (int)n));
                break;
            default: throw new NotSupportedException($"CopyOffsetBytes: dtype {dt} не поддержан.");
        }
    }

    /// <summary>Перенести тензор на CPU (через download из device-storage, если нужно).</summary>
    /// <remarks>
    /// <para>
    /// Корректно обрабатывает view-тензоры:
    /// </para>
    /// <list type="bullet">
    /// <item>Если <see cref="IsContiguous"/> и нужна только сдвижка (offset≠0,
    /// например <c>Select(packed, 0, 1)</c>) — скачиваем нужные <c>NumElements</c>
    /// начиная с <see cref="Offset"/>.</item>
    /// <item>Если strides не row-major (Permute/Transpose/Narrow с middle-axis) —
    /// сначала материализуем contiguous-копию на устройстве через
    /// <see cref="Contiguous"/>, чтобы download получил элементы в логическом
    /// порядке, а не «как лежат в памяти».</item>
    /// </list>
    /// <para>
    /// Без этого <c>Select(packed, 0, 1).ToCpu()</c> возвращал первые B·H элементов
    /// (h-плоскость), а не вторую (c-плоскость) — баг ломал LSTM cN-парность с CPU.
    /// </para>
    /// </remarks>
    public Tensor ToCpu()
    {
        if (Device.Type == DeviceType.Cpu) return this.Contiguous();
        // Для views с non-contiguous strides сначала материализуем contiguous на device.
        var src = IsContiguous ? this : Contiguous();
        var dstStorage = CpuStorage.Allocate(DType, src.NumElements);
        if (src.Storage is Storage.IHostCopyable hc)
        {
            hc.CopyToHost(dstStorage, src.Offset, src.NumElements);
        }
        else
        {
            throw new NotSupportedException(
                $"Storage {src.Storage.GetType().Name} не поддерживает download на CPU.");
        }
        return new Tensor(dstStorage, src.Shape, V2.Strides.RowMajor(src.Shape.AsSpan()), 0);
    }

    private static void CopyDtypeBytes(TensorStorage src, TensorStorage dst, long n, DType dt)
    {
        switch (dt)
        {
            case DType.Float32: src.AsReadOnlySpan<float>().Slice(0, (int)n).CopyTo(dst.AsSpan<float>()); break;
            case DType.Float64: src.AsReadOnlySpan<double>().Slice(0, (int)n).CopyTo(dst.AsSpan<double>()); break;
            case DType.Int32: src.AsReadOnlySpan<int>().Slice(0, (int)n).CopyTo(dst.AsSpan<int>()); break;
            case DType.Int64: src.AsReadOnlySpan<long>().Slice(0, (int)n).CopyTo(dst.AsSpan<long>()); break;
            default: throw new NotSupportedException($"Copy для {dt} ещё не реализован.");
        }
    }

    /// <summary>Алиас <see cref="To(Device)"/>: tensor.Cuda(0).</summary>
    public Tensor Cuda(int index = 0) => To(V2.Device.Cuda(index));

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Tensor(shape={Shape}, dtype={DType}, device={Device}" +
               (RequiresGrad ? ", requires_grad=True" : "") + ")";
    }
    #endregion Миграция между устройствами
}
