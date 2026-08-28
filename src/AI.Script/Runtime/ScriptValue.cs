using AI.DataStructs.Algebraic;
using AI.Script.Semantics;

namespace AI.Script.Runtime;

/// <summary>
/// Значение языка: тег типа, числовая ячейка и ссылка.
/// </summary>
/// <remarks>
/// Структура, а не класс: числа и логические значения составляют подавляющее большинство
/// значений в любом цикле, и упаковка каждого из них в объект была бы платой ни за что.
/// Числовая ячейка используется для <c>num</c> и <c>bool</c>, ссылка — для всего остального.
/// </remarks>
public readonly struct ScriptValue : IEquatable<ScriptValue>
{
    private readonly double _number;
    private readonly object? _reference;

    /// <summary>Тип значения.</summary>
    public ScriptType Type { get; }

    private ScriptValue(ScriptType type, double number, object? reference)
    {
        Type = type;
        _number = number;
        _reference = reference;
    }

    /// <summary>Отсутствие значения.</summary>
    public static readonly ScriptValue None = new(ScriptType.None, 0, null);

    /// <summary>Логическая истина.</summary>
    public static readonly ScriptValue True = new(ScriptType.Bool, 1, null);

    /// <summary>Логическая ложь.</summary>
    public static readonly ScriptValue False = new(ScriptType.Bool, 0, null);

    /// <summary>Создаёт число.</summary>
    public static ScriptValue Num(double value) => new(ScriptType.Num, value, null);

    /// <summary>Создаёт логическое значение.</summary>
    public static ScriptValue Bool(bool value) => value ? True : False;

    /// <summary>Создаёт строку.</summary>
    public static ScriptValue Str(string value) => new(ScriptType.Str, 0, value ?? string.Empty);

    /// <summary>Создаёт дату.</summary>
    public static ScriptValue Date(DateTime value) => new(ScriptType.Date, 0, value);

    /// <summary>Создаёт длительность.</summary>
    public static ScriptValue Dur(TimeSpan value) => new(ScriptType.Dur, 0, value);

    /// <summary>Создаёт вектор.</summary>
    public static ScriptValue Vec(Vector value) => new(ScriptType.Vec, 0, value ?? new Vector());

    /// <summary>Создаёт матрицу.</summary>
    public static ScriptValue Mat(Matrix value) => new(ScriptType.Mat, 0, value);

    /// <summary>Создаёт таблицу.</summary>
    public static ScriptValue Table(ScriptTable value) => new(ScriptType.Table, 0, value ?? ScriptTable.Empty);

    /// <summary>Создаёт список.</summary>
    public static ScriptValue List(ScriptList value) => new(ScriptType.List, 0, value ?? ScriptList.Empty);

    /// <summary>Создаёт запись.</summary>
    public static ScriptValue Record(ScriptRecord value) => new(ScriptType.Record, 0, value ?? ScriptRecord.Empty);

    /// <summary>Создаёт диапазон.</summary>
    public static ScriptValue Range(ScriptRange value) => new(ScriptType.Range, 0, value);

    /// <summary>Создаёт функцию.</summary>
    public static ScriptValue Fn(ScriptCallable value) => new(ScriptType.Fn, 0, value);

    /// <summary>Создаёт дескриптор.</summary>
    public static ScriptValue Handle(ScriptHandle value) => new(ScriptType.Handle, 0, value);

    /// <summary>Является ли значение отсутствующим.</summary>
    public bool IsNone => Type == ScriptType.None;

    /// <summary>Число без проверки типа; осмысленно только для <c>num</c>.</summary>
    public double RawNumber => _number;

    /// <summary>Ссылка без проверки типа.</summary>
    public object? RawReference => _reference;

    /// <summary>Число; отказ, если тип другой.</summary>
    public double AsNumber(string what = "значение") => Type == ScriptType.Num
        ? _number
        : throw Mismatch(what, ScriptType.Num);

    /// <summary>Логическое значение; отказ, если тип другой.</summary>
    public bool AsBool(string what = "значение") => Type == ScriptType.Bool
        ? _number != 0
        : throw Mismatch(what, ScriptType.Bool);

    /// <summary>Строка; отказ, если тип другой.</summary>
    public string AsString(string what = "значение") => Type == ScriptType.Str
        ? (string)_reference!
        : throw Mismatch(what, ScriptType.Str);

    /// <summary>Дата; отказ, если тип другой.</summary>
    public DateTime AsDate(string what = "значение") => Type == ScriptType.Date
        ? (DateTime)_reference!
        : throw Mismatch(what, ScriptType.Date);

    /// <summary>Длительность; отказ, если тип другой.</summary>
    public TimeSpan AsDuration(string what = "значение") => Type == ScriptType.Dur
        ? (TimeSpan)_reference!
        : throw Mismatch(what, ScriptType.Dur);

    /// <summary>Вектор; отказ, если тип другой.</summary>
    public Vector AsVector(string what = "значение") => Type == ScriptType.Vec
        ? (Vector)_reference!
        : throw Mismatch(what, ScriptType.Vec);

    /// <summary>Матрица; отказ, если тип другой.</summary>
    public Matrix AsMatrix(string what = "значение") => Type == ScriptType.Mat
        ? (Matrix)_reference!
        : throw Mismatch(what, ScriptType.Mat);

    /// <summary>Таблица; отказ, если тип другой.</summary>
    public ScriptTable AsTable(string what = "значение") => Type == ScriptType.Table
        ? (ScriptTable)_reference!
        : throw Mismatch(what, ScriptType.Table);

    /// <summary>Список; отказ, если тип другой.</summary>
    public ScriptList AsList(string what = "значение") => Type == ScriptType.List
        ? (ScriptList)_reference!
        : throw Mismatch(what, ScriptType.List);

    /// <summary>Запись; отказ, если тип другой.</summary>
    public ScriptRecord AsRecord(string what = "значение") => Type == ScriptType.Record
        ? (ScriptRecord)_reference!
        : throw Mismatch(what, ScriptType.Record);

    /// <summary>Диапазон; отказ, если тип другой.</summary>
    public ScriptRange AsRange(string what = "значение") => Type == ScriptType.Range
        ? (ScriptRange)_reference!
        : throw Mismatch(what, ScriptType.Range);

    /// <summary>Функция; отказ, если тип другой.</summary>
    public ScriptCallable AsCallable(string what = "значение") => Type == ScriptType.Fn
        ? (ScriptCallable)_reference!
        : throw Mismatch(what, ScriptType.Fn);

    /// <summary>Дескриптор; отказ, если тип другой.</summary>
    public ScriptHandle AsHandle(string what = "значение") => Type == ScriptType.Handle
        ? (ScriptHandle)_reference!
        : throw Mismatch(what, ScriptType.Handle);

    private ScriptError Mismatch(string what, ScriptType expected) =>
        new(DiagnosticCodes.TypeMismatch,
            $"{what}: ожидался тип {expected.ToName()}, получен {Type.ToName()}",
            expected == ScriptType.Bool && Type == ScriptType.Num
                ? "неявного приведения числа к логическому значению нет: напишите явное сравнение, например 'x > 0'"
                : null);

    /// <inheritdoc/>
    public bool Equals(ScriptValue other)
    {
        if (Type != other.Type) return false;

        return Type switch
        {
            ScriptType.None => true,
            ScriptType.Num or ScriptType.Bool => _number.Equals(other._number),
            ScriptType.Str => string.Equals((string)_reference!, (string)other._reference!, StringComparison.Ordinal),
            ScriptType.Date => ((DateTime)_reference!).Equals((DateTime)other._reference!),
            ScriptType.Dur => ((TimeSpan)_reference!).Equals((TimeSpan)other._reference!),
            ScriptType.Vec => VectorsEqual((Vector)_reference!, (Vector)other._reference!),
            ScriptType.List => ListsEqual((ScriptList)_reference!, (ScriptList)other._reference!),
            ScriptType.Record => RecordsEqual((ScriptRecord)_reference!, (ScriptRecord)other._reference!),
            _ => ReferenceEquals(_reference, other._reference),
        };
    }

    private static bool VectorsEqual(Vector left, Vector right)
    {
        if (left.Count != right.Count) return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i])) return false;
        }

        return true;
    }

    private static bool ListsEqual(ScriptList left, ScriptList right)
    {
        if (left.Count != right.Count) return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i])) return false;
        }

        return true;
    }

    private static bool RecordsEqual(ScriptRecord left, ScriptRecord right)
    {
        if (left.Count != right.Count) return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left.Keys[i], right.Keys[i], StringComparison.Ordinal)) return false;
            if (!left.Values[i].Equals(right.Values[i])) return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ScriptValue value && Equals(value);

    /// <inheritdoc/>
    public override int GetHashCode() => Type switch
    {
        ScriptType.None => 0,
        ScriptType.Num or ScriptType.Bool => HashCode.Combine(Type, _number),
        ScriptType.Str or ScriptType.Date or ScriptType.Dur => HashCode.Combine(Type, _reference),
        _ => HashCode.Combine(Type, _reference?.GetHashCode() ?? 0),
    };

    /// <inheritdoc/>
    public override string ToString() => ScriptFormatter.Format(this);
}
