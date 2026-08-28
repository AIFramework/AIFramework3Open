using System.Collections;

namespace AI.Script.Runtime;

/// <summary>
/// Неизменяемый разнородный список.
/// </summary>
/// <remarks>
/// Неизменяемость — не идеология, а требование кэша стадий и параллельного <c>core.map</c>:
/// кэшировать результат по хэшу входа можно только если вход после этого не меняется.
/// Изменение элемента возвращает новый список.
/// </remarks>
public sealed class ScriptList : IReadOnlyList<ScriptValue>
{
    private readonly ScriptValue[] _items;

    /// <summary>Пустой список.</summary>
    public static readonly ScriptList Empty = new([]);

    private ScriptList(ScriptValue[] items) => _items = items;

    /// <summary>Создаёт список, забирая массив во владение.</summary>
    public static ScriptList Own(ScriptValue[] items) => items.Length == 0 ? Empty : new ScriptList(items);

    /// <summary>Создаёт список копией последовательности.</summary>
    public static ScriptList From(IEnumerable<ScriptValue> items) => Own([.. items]);

    /// <summary>Число элементов.</summary>
    public int Count => _items.Length;

    /// <summary>Элемент по индексу.</summary>
    public ScriptValue this[int index] => _items[index];

    /// <summary>Копия с заменённым элементом.</summary>
    public ScriptList SetItem(int index, ScriptValue value)
    {
        var copy = (ScriptValue[])_items.Clone();
        copy[index] = value;
        return new ScriptList(copy);
    }

    /// <summary>Копия с добавленным в конец элементом.</summary>
    public ScriptList Append(ScriptValue value)
    {
        var copy = new ScriptValue[_items.Length + 1];
        Array.Copy(_items, copy, _items.Length);
        copy[^1] = value;
        return new ScriptList(copy);
    }

    /// <summary>Конкатенация двух списков.</summary>
    public static ScriptList Concat(ScriptList left, ScriptList right)
    {
        var copy = new ScriptValue[left.Count + right.Count];
        Array.Copy(left._items, copy, left.Count);
        Array.Copy(right._items, 0, copy, left.Count, right.Count);
        return new ScriptList(copy);
    }

    /// <summary>Срез [start, end).</summary>
    public ScriptList Slice(int start, int end) => Own(_items[start..end]);

    /// <inheritdoc/>
    public IEnumerator<ScriptValue> GetEnumerator() => ((IEnumerable<ScriptValue>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
