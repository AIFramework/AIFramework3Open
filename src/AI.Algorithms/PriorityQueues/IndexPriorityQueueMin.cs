using System;
using System.Collections.Generic;

namespace AI.Algorithms.PriorityQueues;

/// <summary>
/// Минимальная очередь с приоритетом по индексам: двоичная куча с картой «индекс → позиция»
/// </summary>
/// <remarks>
/// Прежняя реализация искала минимум перебором и при обмене элементов не обновляла карту позиций:
/// после извлечения минимума изменение приоритета писало в чужую ячейку, и Дейкстра, Прим, A*
/// и алгоритм Йена теряли вершины. Здесь карта обновляется при каждом перемещении, а все
/// операции стоят O(log n). Ёмкость в конструкторе — только начальный размер: очередь растёт.
/// </remarks>
/// <typeparam name="T">Тип приоритета</typeparam>
[Serializable]
public class IndexPriorityQueueMin<T>
    where T : IComparable<T>
{
    private readonly List<Tuple<int, T>> _heap;
    private readonly Dictionary<int, int> _position;

    /// <summary>
    /// Создаёт пустую очередь
    /// </summary>
    /// <param name="capasity">Ожидаемое число элементов</param>
    public IndexPriorityQueueMin(int capasity)
    {
        _heap = new List<Tuple<int, T>>(Math.Max(0, capasity));
        _position = new Dictionary<int, int>(Math.Max(0, capasity));
    }

    /// <summary>
    /// Извлекает элемент с наименьшим приоритетом
    /// </summary>
    /// <returns>Пара (индекс, приоритет)</returns>
    public Tuple<int, T> DelMin()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Очередь пуста");

        Tuple<int, T> min = _heap[0];
        int last = _heap.Count - 1;

        Swap(0, last);
        _heap.RemoveAt(last);
        _ = _position.Remove(min.Item1);

        if (_heap.Count > 0)
            SiftDown(0);

        return min;
    }

    /// <summary>
    /// Извлекает элемент с наименьшим приоритетом и возвращает его индекс
    /// </summary>
    public int DelMinGetIndex()
    {
        return DelMin().Item1;
    }

    /// <summary>
    /// Извлекает элемент с наименьшим приоритетом и возвращает приоритет
    /// </summary>
    public T DelMinGetValue()
    {
        return DelMin().Item2;
    }

    /// <summary>
    /// Пуста ли очередь
    /// </summary>
    public bool IsEmpty()
    {
        return _heap.Count == 0;
    }

    /// <summary>
    /// Добавляет индекс с приоритетом
    /// </summary>
    /// <param name="index">Индекс; не должен уже быть в очереди</param>
    /// <param name="element">Приоритет</param>
    public void Insert(int index, T element)
    {
        if (_position.ContainsKey(index))
            throw new ArgumentException($"Индекс {index} уже есть в очереди", nameof(index));

        _heap.Add(new Tuple<int, T>(index, element));
        _position[index] = _heap.Count - 1;
        SiftUp(_heap.Count - 1);
    }

    /// <summary>
    /// Обновить значение
    /// </summary>
    /// <param name="index">Индекс, уже находящийся в очереди</param>
    /// <param name="element">Новый приоритет — меньше или больше прежнего</param>
    public void Update(int index, T element)
    {
        int at = _position[index];
        _heap[at] = new Tuple<int, T>(index, element);

        SiftUp(at);
        SiftDown(_position[index]);
    }

    /// <summary>
    /// Проверяет есть ли индекс
    /// </summary>
    /// <param name="index">Индекс</param>
    public bool IsContain(int index)
    {
        return _position.ContainsKey(index);
    }

    /// <summary>
    /// Наименьший приоритет без извлечения
    /// </summary>
    public T KeyMin()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Очередь пуста");

        return _heap[0].Item2;
    }

    /// <summary>
    /// Число элементов
    /// </summary>
    public int Size()
    {
        return _heap.Count;
    }

    private void SiftUp(int k)
    {
        while (k > 0)
        {
            int parent = (k - 1) / 2;

            if (!Less(k, parent))
                break;

            Swap(k, parent);
            k = parent;
        }
    }

    private void SiftDown(int k)
    {
        while (true)
        {
            int child = (2 * k) + 1;

            if (child >= _heap.Count)
                break;

            if (child + 1 < _heap.Count && Less(child + 1, child))
                child++;

            if (!Less(child, k))
                break;

            Swap(k, child);
            k = child;
        }
    }

    private bool Less(int a, int b)
    {
        return _heap[a].Item2.CompareTo(_heap[b].Item2) < 0;
    }

    // Каждое перемещение обновляет карту позиций — именно этого не делала прежняя реализация
    private void Swap(int a, int b)
    {
        (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
        _position[_heap[a].Item1] = a;
        _position[_heap[b].Item1] = b;
    }
}
