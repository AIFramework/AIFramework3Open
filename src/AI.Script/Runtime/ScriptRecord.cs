namespace AI.Script.Runtime;

/// <summary>
/// Неизменяемая запись «поле → значение» с сохранением порядка полей.
/// </summary>
/// <remarks>
/// Порядок сохраняется, потому что запись печатается человеку и уходит в <c>emit</c>: поля,
/// перетасованные хэш-таблицей, читаются хуже, а разница между двумя прогонами становится
/// шумной там, где данные не менялись.
/// </remarks>
public sealed class ScriptRecord
{
    private readonly string[] _keys;
    private readonly ScriptValue[] _values;
    private readonly Dictionary<string, int> _index;

    /// <summary>Пустая запись.</summary>
    public static readonly ScriptRecord Empty = new([], []);

    private ScriptRecord(string[] keys, ScriptValue[] values)
    {
        _keys = keys;
        _values = values;
        _index = new Dictionary<string, int>(keys.Length, StringComparer.Ordinal);

        for (int i = 0; i < keys.Length; i++) _index[keys[i]] = i;
    }

    /// <summary>Создаёт запись из пар в заданном порядке; повтор ключа перекрывает прежнее значение.</summary>
    public static ScriptRecord From(IEnumerable<KeyValuePair<string, ScriptValue>> fields)
    {
        var keys = new List<string>();
        var values = new List<ScriptValue>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (index.TryGetValue(field.Key, out int existing))
            {
                values[existing] = field.Value;
                continue;
            }

            index[field.Key] = keys.Count;
            keys.Add(field.Key);
            values.Add(field.Value);
        }

        return keys.Count == 0 ? Empty : new ScriptRecord([.. keys], [.. values]);
    }

    /// <summary>Число полей.</summary>
    public int Count => _keys.Length;

    /// <summary>Имена полей в порядке объявления.</summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>Значения полей в порядке объявления.</summary>
    public IReadOnlyList<ScriptValue> Values => _values;

    /// <summary>Есть ли поле.</summary>
    public bool Has(string name) => _index.ContainsKey(name);

    /// <summary>Пытается получить значение поля.</summary>
    public bool TryGet(string name, out ScriptValue value)
    {
        if (_index.TryGetValue(name, out int position))
        {
            value = _values[position];
            return true;
        }

        value = ScriptValue.None;
        return false;
    }

    /// <summary>Пары «имя → значение» в порядке объявления.</summary>
    public IEnumerable<KeyValuePair<string, ScriptValue>> Pairs()
    {
        for (int i = 0; i < _keys.Length; i++)
            yield return new KeyValuePair<string, ScriptValue>(_keys[i], _values[i]);
    }

    /// <summary>Копия с добавленным либо заменённым полем.</summary>
    public ScriptRecord With(string name, ScriptValue value)
    {
        var fields = new List<KeyValuePair<string, ScriptValue>>(Pairs())
        {
            new(name, value),
        };

        return From(fields);
    }
}
