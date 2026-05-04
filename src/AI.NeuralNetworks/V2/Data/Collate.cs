using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Data;

/// <summary>
/// Функция склейки: список одиночных элементов -> один батч-элемент.
/// </summary>
public delegate TBatch CollateFn<TItem, TBatch>(IReadOnlyList<TItem> items);

/// <summary>
/// Готовые collate-функции для типичных случаев.
/// </summary>
public static class Collate
{
    /// <summary>
    /// Склеить пары (x, y) в (X, Y) тензоры путём <see cref="IndexingOps.Stack"/>.
    /// </summary>
    public static (Tensor x, Tensor y) StackPair(IReadOnlyList<(Tensor x, Tensor y)> items)
    {
        var xs = new Tensor[items.Count];
        var ys = new Tensor[items.Count];
        for (int i = 0; i < items.Count; i++) { xs[i] = items[i].x; ys[i] = items[i].y; }
        return (IndexingOps.Stack(xs, 0), IndexingOps.Stack(ys, 0));
    }

    /// <summary>Склеить просто список тензоров вдоль новой 0-й оси.</summary>
    public static Tensor StackTensors(IReadOnlyList<Tensor> items) => IndexingOps.Stack(items, 0);

    /// <summary>
    /// Склеить разноразмерные последовательности (rank ≥ 1) вдоль новой 0-й оси,
    /// предварительно padding до общей длины каждой оси.
    /// </summary>
    /// <param name="items">Тензоры одного ранга (например, [T_i, F]) и одного DType/Device.</param>
    /// <param name="padValue">Значение заполнителя (по умолчанию 0).</param>
    /// <returns>Тензор формы <c>[N, max(d0), max(d1), ...]</c>.</returns>
    public static Tensor PadStack(IReadOnlyList<Tensor> items, float padValue = 0f)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("PadStack: нужен хотя бы один тензор.", nameof(items));
        var first = items[0] ?? throw new ArgumentException("PadStack: items[0] == null.");
        int rank = first.Rank;
        if (rank == 0) throw new ArgumentException("PadStack: нельзя стэкать скаляры.");
        var dt = first.DType;
        var dev = first.Device;
        if (dev.Type != DeviceType.Cpu)
            throw new InvalidOperationException(
                "PadStack пока работает только с CPU-тензорами; для GPU соберите батч на CPU и сделайте .ToDevice() после.");
        var maxDims = new int[rank];
        for (int i = 0; i < rank; i++) maxDims[i] = first.Shape[i];
        for (int n = 1; n < items.Count; n++)
        {
            var t = items[n] ?? throw new ArgumentException($"PadStack: items[{n}] == null.");
            if (t.Rank != rank)
                throw new ArgumentException($"PadStack: rank mismatch: items[0]={rank}, items[{n}]={t.Rank}.");
            if (t.DType != dt)
                throw new ArgumentException($"PadStack: DType mismatch: {dt} vs {t.DType}.");
            if (t.Device != dev)
                throw new ArgumentException($"PadStack: Device mismatch: {dev} vs {t.Device}.");
            for (int i = 0; i < rank; i++)
                if (t.Shape[i] > maxDims[i]) maxDims[i] = t.Shape[i];
        }

        var outDims = new int[rank + 1];
        outDims[0] = items.Count;
        for (int i = 0; i < rank; i++) outDims[i + 1] = maxDims[i];
        var output = Tensor.Full(new Shape(outDims), padValue, dt, dev);

        // Копируем каждый элемент в нулевой угол вдоль батч-оси.
        // Используем Narrow по каждой оси, чтобы получить view нужной формы.
        for (int n = 0; n < items.Count; n++)
        {
            var dst = IndexingOps.Select(output, 0, n); // [max(d0), ...]
            var src = items[n];
            for (int i = 0; i < rank; i++)
                dst = IndexingOps.Narrow(dst, i, 0, src.Shape[i]);
            // Поэлементное копирование view->view: используем Tensor.CopyFrom-подобную семантику
            // через сложение нулевого тензора вид-в-вид (без autograd: collate выполняется до forward).
            CopyView(src, dst);
        }
        return output;
    }

    /// <summary>
    /// PadStack для последовательностей переменной длины + возврат маски валидности
    /// (1 — реальные данные, 0 — паддинг) формы <c>[N, max_len]</c>.
    /// </summary>
    /// <remarks>
    /// Маска покрывает только первую timestep-ось; для дополнительных осей
    /// используется паддинг без отдельной маски.
    /// </remarks>
    public static (Tensor padded, Tensor mask) PadSequence(
        IReadOnlyList<Tensor> items, float padValue = 0f)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("PadSequence: нужен хотя бы один тензор.", nameof(items));
        var padded = PadStack(items, padValue);
        int N = items.Count;
        int Tmax = padded.Shape[1];
        var mask = Tensor.Zeros(new Shape(N, Tmax), DType.Float32, items[0].Device);
        var maskSpan = mask.AsSpan<float>();
        for (int n = 0; n < N; n++)
        {
            int tn = items[n].Shape[0];
            int rowOff = n * Tmax;
            for (int t = 0; t < tn; t++) maskSpan[rowOff + t] = 1f;
        }
        return (padded, mask);
    }

    private static void CopyView(Tensor src, Tensor dst)
    {
        if (src.Shape != dst.Shape)
            throw new InvalidOperationException("PadStack: внутренняя ошибка — формы view не совпали.");
        // Делегируем покомпонентному копированию через Contiguous (CPU/GPU агностично).
        var srcC = src.Contiguous();
        switch (src.DType)
        {
            case DType.Float32:
                CopyFlat<float>(srcC, dst);
                break;
            case DType.Float64:
                CopyFlat<double>(srcC, dst);
                break;
            case DType.Int32:
                CopyFlat<int>(srcC, dst);
                break;
            case DType.Int64:
                CopyFlat<long>(srcC, dst);
                break;
            case DType.Int16:
                CopyFlat<short>(srcC, dst);
                break;
            case DType.Int8:
                CopyFlat<sbyte>(srcC, dst);
                break;
            case DType.UInt8:
                CopyFlat<byte>(srcC, dst);
                break;
            case DType.Bool:
                CopyFlat<bool>(srcC, dst);
                break;
            default:
                throw new NotSupportedException($"PadStack: DType {src.DType} не поддерживается.");
        }
    }

    private static void CopyFlat<T>(Tensor srcContig, Tensor dstView) where T : unmanaged
    {
        // Идём по логическим индексам dstView и копируем поэлементно из srcContig.
        // dstView может быть не-contiguous; используем построчный shape walk.
        var srcSpan = srcContig.AsReadOnlySpan<T>();
        int rank = dstView.Rank;
        if (rank == 0)
        {
            dstView.AsSpan<T>()[0] = srcSpan[0];
            return;
        }
        var idx = new int[rank];
        var dims = new int[rank];
        for (int i = 0; i < rank; i++) dims[i] = dstView.Shape[i];
        long total = srcContig.NumElements;
        for (long lin = 0; lin < total; lin++)
        {
            // dst index -> linear in dstView storage
            int dstOff = dstView.Offset;
            for (int i = 0; i < rank; i++) dstOff += idx[i] * dstView.Strides[i];
            dstView.Storage.AsSpan<T>()[dstOff] = srcSpan[(int)lin];
            // ++idx
            for (int i = rank - 1; i >= 0; i--)
            {
                idx[i]++;
                if (idx[i] < dims[i]) break;
                idx[i] = 0;
            }
        }
    }
}
