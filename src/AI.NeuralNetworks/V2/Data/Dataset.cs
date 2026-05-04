using System;
using System.Collections.Generic;

namespace AI.ML.NeuralNetworks.V2.Data;

/// <summary>
/// Базовый интерфейс датасета: индексированный random-access.
/// Аналог <c>torch.utils.data.Dataset</c>.
/// </summary>
/// <typeparam name="T">Тип одного элемента (например, <c>(Tensor x, int y)</c>).</typeparam>
public interface IDataset<out T>
{
    /// <summary>Число элементов.</summary>
    int Count { get; }
    /// <summary>Получить элемент по индексу.</summary>
    T Get(int index);
}

/// <summary>
/// In-memory tensor-датасет: пара (X, Y) тензоров; индексирует по первой оси.
/// </summary>
public sealed class TensorDataset : IDataset<(Tensor x, Tensor y)>
{
    private readonly Tensor _x;
    private readonly Tensor _y;
    /// <summary>Создать.</summary>
    public TensorDataset(Tensor x, Tensor y)
    {
        if (x.Shape[0] != y.Shape[0])
            throw new ArgumentException("X и Y должны иметь одинаковый размер по 0-й оси.");
        _x = x; _y = y;
    }
    /// <inheritdoc/>
    public int Count => _x.Shape[0];
    /// <inheritdoc/>
    public (Tensor x, Tensor y) Get(int index)
    {
        // Срез по первой оси (zero-copy view).
        return (Ops.IndexingOps.Narrow(_x, 0, index, 1).Squeeze(0),
                Ops.IndexingOps.Narrow(_y, 0, index, 1).Squeeze(0));
    }
}

/// <summary>
/// Адаптер: превращает <see cref="IList{T}"/> в <see cref="IDataset{T}"/>.
/// </summary>
public sealed class ListDataset<T> : IDataset<T>
{
    private readonly IList<T> _items;
    /// <summary>Создать.</summary>
    public ListDataset(IList<T> items) { _items = items; }
    /// <inheritdoc/>
    public int Count => _items.Count;
    /// <inheritdoc/>
    public T Get(int index) => _items[index];
}
