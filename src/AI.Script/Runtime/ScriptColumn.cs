using AI.DataStructs.Algebraic;
using AI.Script.Semantics;

namespace AI.Script.Runtime;

/// <summary>
/// Колонка таблицы: имя, однородные (по возможности) значения и выведенный тип.
/// </summary>
/// <remarks>
/// Хранение — массив значений языка, а не <see cref="Vector"/>, потому что колонка бывает
/// строковой, а таблица должна одинаково хорошо держать и числа, и категории. Числовое
/// представление строится по требованию и кэшируется: мост в быстрый путь фреймворка — это
/// <c>table.to_matrix</c>, и платить за него один раз правильнее, чем усложнять хранение.
/// </remarks>
public sealed class ScriptColumn
{
    private readonly ScriptValue[] _values;
    private Vector? _numbers;

    /// <summary>Имя колонки.</summary>
    public string Name { get; }

    /// <summary>
    /// Тип колонки: <c>num</c>, <c>str</c>, <c>bool</c>, <c>date</c> либо <c>any</c> для разнородной.
    /// </summary>
    public ScriptType Type { get; }

    /// <summary>Число строк.</summary>
    public int Count => _values.Length;

    /// <summary>Значение по индексу строки.</summary>
    public ScriptValue this[int index] => _values[index];

    private ScriptColumn(string name, ScriptValue[] values, ScriptType type)
    {
        Name = name;
        _values = values;
        Type = type;
    }

    /// <summary>Создаёт колонку из значений, выводя её тип.</summary>
    public static ScriptColumn Own(string name, ScriptValue[] values) =>
        new(name, values, InferType(values));

    /// <summary>Создаёт колонку копией последовательности.</summary>
    public static ScriptColumn From(string name, IEnumerable<ScriptValue> values) => Own(name, [.. values]);

    /// <summary>Создаёт числовую колонку из вектора.</summary>
    public static ScriptColumn FromVector(string name, Vector values)
    {
        var items = new ScriptValue[values.Count];

        for (int i = 0; i < values.Count; i++) items[i] = ScriptValue.Num(values[i]);

        return new ScriptColumn(name, items, ScriptType.Num) { _numbers = values };
    }

    /// <summary>Значение колонки как значения языка: вектор для числовой, список для остальных.</summary>
    public ScriptValue AsValue() =>
        Type == ScriptType.Num ? ScriptValue.Vec(ToVector()) : ScriptValue.List(ScriptList.Own(Copy()));

    /// <summary>
    /// Числовое представление колонки; отказ, если колонка не числовая.
    /// </summary>
    public Vector ToVector()
    {
        if (_numbers != null) return _numbers;

        if (Type != ScriptType.Num)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"колонка '{Name}' имеет тип {Type.ToName()} и числом не является",
                "категории переводятся в числа функцией table.one_hot либо table.encode");
        }

        var vector = new Vector(_values.Length);

        // Пропуск становится nan, а не нулём: ноль — это значение, и подмена одного другим
        // тихо смещает любое среднее, посчитанное дальше по конвейеру.
        for (int i = 0; i < _values.Length; i++)
            vector[i] = _values[i].IsNone ? double.NaN : _values[i].RawNumber;

        _numbers = vector;
        return vector;
    }

    /// <summary>Копия значений.</summary>
    public ScriptValue[] Copy() => (ScriptValue[])_values.Clone();

    /// <summary>Та же колонка под другим именем.</summary>
    public ScriptColumn Renamed(string name) => new(name, _values, Type) { _numbers = _numbers };

    /// <summary>Колонка из указанных строк в указанном порядке.</summary>
    public ScriptColumn Take(IReadOnlyList<int> rows)
    {
        var items = new ScriptValue[rows.Count];

        for (int i = 0; i < rows.Count; i++) items[i] = _values[rows[i]];

        return new ScriptColumn(Name, items, Type);
    }

    /// <summary>Перечисляет значения колонки.</summary>
    public IEnumerable<ScriptValue> Values()
    {
        foreach (ScriptValue value in _values) yield return value;
    }

    /// <summary>
    /// Выводит тип колонки по её значениям.
    /// </summary>
    /// <remarks>
    /// <c>none</c> не сбивает вывод типа: пропуск в данных — это отсутствие значения, а не
    /// другой тип, и колонка из чисел с дыркой обязана остаться числовой.
    /// </remarks>
    private static ScriptType InferType(ScriptValue[] values)
    {
        ScriptType type = ScriptType.None;

        foreach (ScriptValue value in values)
        {
            if (value.IsNone) continue;
            if (type == ScriptType.None) { type = value.Type; continue; }
            if (type != value.Type) return ScriptType.Any;
        }

        return type == ScriptType.None ? ScriptType.Any : type;
    }
}
