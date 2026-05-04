using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.V2;

public sealed partial class Tensor
{
    #region Фабрики

    /// <summary>
    /// Создать новый contiguous-тензор с указанной формой и dtype, заполненный нулями.
    /// </summary>
    public static Tensor Zeros(Shape shape, DType dt = DType.Float32, Device? device = null)
    {
        var dev = device ?? Device.Cpu;
        var storage = AllocateStorage(dt, dev, shape.NumElements);
        // Allocate уже зануляет.
        return new Tensor(storage, shape, V2.Strides.RowMajor(shape.AsSpan()), 0);
    }

    /// <summary>Алиас <see cref="Zeros(Shape, DType, Device?)"/> с раскрытием dims.</summary>
    public static Tensor Zeros(params int[] dims) => Zeros(new Shape(dims));

    /// <summary>Создать тензор без инициализации памяти (быстрее, осторожно использовать).</summary>
    public static Tensor Empty(Shape shape, DType dt = DType.Float32, Device? device = null)
    {
        var dev = device ?? Device.Cpu;
        var storage = AllocateStorage(dt, dev, shape.NumElements);
        return new Tensor(storage, shape, V2.Strides.RowMajor(shape.AsSpan()), 0);
    }

    /// <summary>Тензор, заполненный единицами.</summary>
    public static Tensor Ones(Shape shape, DType dt = DType.Float32, Device? device = null)
    {
        var t = Empty(shape, dt, device);
        Fill(t, 1.0);
        return t;
    }

    /// <summary>Тензор, заполненный значением <paramref name="value"/>.</summary>
    public static Tensor Full(Shape shape, double value, DType dt = DType.Float32, Device? device = null)
    {
        var t = Empty(shape, dt, device);
        Fill(t, value);
        return t;
    }

    /// <summary>
    /// Создать тензор из существующего массива.
    /// </summary>
    /// <remarks>
    /// Не zero-copy: данные копируются в собственный буфер тензора, поэтому изменения
    /// исходного массива не влияют на тензор и наоборот.
    /// </remarks>
    public static Tensor From<T>(T[] data, Shape shape) where T : unmanaged
    {
        if (shape.NumElements != data.Length)
            throw new ArgumentException(
                $"Длина данных {data.Length} не совпадает с shape {shape} ({shape.NumElements}).");
        var storage = CpuStorage.From(data);
        return new Tensor(storage, shape, V2.Strides.RowMajor(shape.AsSpan()), 0);
    }

    /// <summary>Скаляр (rank-0 тензор).</summary>
    public static Tensor Scalar(float value)
    {
        var t = Empty(Shape.Scalar);
        t.Storage.AsSpan<float>()[0] = value;
        return t;
    }

    /// <summary>1D-тензор из массива (rank=1).</summary>
    public static Tensor From(float[] data) => From(data, new Shape(data.Length));

    /// <summary>
    /// Создать тензор с нормально-распределёнными значениями (mean=0, std=1).
    /// </summary>
    public static Tensor Randn(Shape shape, Random rng = null, DType dt = DType.Float32, Device? device = null)
    {
        rng ??= Random.Shared;
        var t = Empty(shape, dt, device);
        if (dt == DType.Float32)
        {
            var span = t.Storage.AsSpan<float>();
            for (int i = 0; i < span.Length; i++)
            {
                // Box–Muller (без кэша, ради простоты).
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                span[i] = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
            }
        }
        else if (dt == DType.Float64)
        {
            var span = t.Storage.AsSpan<double>();
            for (int i = 0; i < span.Length; i++)
            {
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                span[i] = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            }
        }
        else throw new NotSupportedException($"Randn для {dt} ещё не реализован.");
        return t;
    }

    /// <summary>Равномерно распределённые значения в [0,1).</summary>
    public static Tensor Rand(Shape shape, Random rng = null, DType dt = DType.Float32, Device? device = null)
    {
        rng ??= Random.Shared;
        var t = Empty(shape, dt, device);
        if (dt == DType.Float32)
        {
            var span = t.Storage.AsSpan<float>();
            for (int i = 0; i < span.Length; i++) span[i] = (float)rng.NextDouble();
        }
        else throw new NotSupportedException($"Rand для {dt} ещё не реализован.");
        return t;
    }

    /// <summary>0..n-1.</summary>
    public static Tensor Arange(int n, DType dt = DType.Float32, Device? device = null)
    {
        var t = Empty(new Shape(n), dt, device);
        if (dt == DType.Float32)
        {
            var span = t.Storage.AsSpan<float>();
            for (int i = 0; i < n; i++) span[i] = i;
        }
        else if (dt == DType.Int32)
        {
            var span = t.Storage.AsSpan<int>();
            for (int i = 0; i < n; i++) span[i] = i;
        }
        else throw new NotSupportedException($"Arange для {dt} ещё не реализован.");
        return t;
    }

    private static TensorStorage AllocateStorage(DType dt, Device dev, long length)
        => StorageBackends.Allocate(dt, dev, length);

    private static void Fill(Tensor t, double value)
    {
        if (t.Device.Type != DeviceType.Cpu)
        {
            // Сначала пробуем native fill (если backend умеет).
            if (t.Storage is Storage.IDeviceFillable df && df.TryFill(value))
                return;
            // Fallback: создать host-буфер только для повторов value (через ArrayPool).
            // Для не-fillable backend'а аллокации избежать нельзя.
            var cpu = Empty(t.Shape, t.DType, Device.Cpu);
            Fill(cpu, value);
            if (t.Storage is Storage.IHostCopyable dst)
                dst.CopyFromHost(cpu.Storage, 0, t.NumElements);
            else
                throw new NotSupportedException(
                    $"Storage {t.Storage.GetType().Name} не поддерживает Fill: " +
                    "реализуйте IDeviceFillable или IHostCopyable.");
            return;
        }
        switch (t.DType)
        {
            case DType.Float32:
                t.Storage.AsSpan<float>().Fill((float)value);
                break;
            case DType.Float64:
                t.Storage.AsSpan<double>().Fill(value);
                break;
            case DType.Int32:
                t.Storage.AsSpan<int>().Fill((int)value);
                break;
            case DType.Int64:
                t.Storage.AsSpan<long>().Fill((long)value);
                break;
            default:
                throw new NotSupportedException($"Fill для {t.DType} ещё не реализован.");
        }
    }

    #endregion Фабрики
}
