using AI.DataStructs.Algebraic;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Binding;

/// <summary>
/// Перевод значений между языком и C#.
/// </summary>
/// <remarks>
/// Таблица соответствий короткая потому, что публичные сигнатуры фреймворка по стандарту кода
/// используют только типы <c>AI.DataStructs</c>. Это и делает привязку сотен функций
/// реалистичной по трудозатратам: перевод почти всюду тождественный.
/// <para>
/// Неподдерживаемый тип в сигнатуре — ошибка регистрации модуля, а не тихий пропуск функции:
/// иначе модуль молча терял бы часть функций, и обнаружилось бы это по отсутствующему имени
/// в чужом скрипте.
/// </para>
/// </remarks>
public static class Marshaller
{
    /// <summary>Допуск при переводе вещественного значения в целочисленный параметр.</summary>
    private const double IntegerTolerance = 1e-9;

    /// <summary>Тип языка, соответствующий типу C#.</summary>
    public static ScriptType TypeOf(Type type)
    {
        if (type == typeof(ScriptValue)) return ScriptType.Any;
        if (type == typeof(void)) return ScriptType.None;

        if (type == typeof(double) || type == typeof(float) || type == typeof(int)
            || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(decimal)) return ScriptType.Num;

        if (type == typeof(bool)) return ScriptType.Bool;
        if (type == typeof(string)) return ScriptType.Str;
        if (type == typeof(DateTime)) return ScriptType.Date;
        if (type == typeof(TimeSpan)) return ScriptType.Dur;
        if (type == typeof(Vector) || type == typeof(double[])) return ScriptType.Vec;
        if (type == typeof(Matrix)) return ScriptType.Mat;
        if (type == typeof(ScriptTable)) return ScriptType.Table;
        if (type == typeof(NDTensor)) return ScriptType.Tensor;
        if (type == typeof(ScriptList) || type == typeof(ScriptValue[]) || type == typeof(string[])) return ScriptType.List;
        if (type == typeof(ScriptRecord)) return ScriptType.Record;
        if (type == typeof(ScriptRange)) return ScriptType.Range;
        if (typeof(ScriptCallable).IsAssignableFrom(type)) return ScriptType.Fn;
        if (type == typeof(ScriptHandle)) return ScriptType.Handle;

        return ScriptType.Handle;
    }

    /// <summary>Поддерживается ли тип в сигнатуре привязки.</summary>
    public static bool IsSupported(Type type) => !type.IsPointer && !type.IsByRef;

    /// <summary>Переводит значение языка в значение C# для параметра.</summary>
    public static object? ToClr(ScriptValue value, Type target, string what)
    {
        if (target == typeof(ScriptValue)) return value;

        // 'none' в ссылочном параметре — это отсутствие значения, а не ошибка типа: так
        // работают необязательные аргументы вроде 'by: none' у core.sort.
        if (value.IsNone && !target.IsValueType) return null;

        if (target == typeof(double)) return ExpectNumber(value, what);
        if (target == typeof(float)) return (float)ExpectNumber(value, what);
        if (target == typeof(decimal)) return (decimal)ExpectNumber(value, what);
        if (target == typeof(int)) return (int)ExpectInteger(value, what);
        if (target == typeof(long)) return ExpectInteger(value, what);
        if (target == typeof(short)) return (short)ExpectInteger(value, what);
        if (target == typeof(byte)) return (byte)ExpectInteger(value, what);

        if (target == typeof(bool)) return value.AsBool(what);
        if (target == typeof(string)) return value.AsString(what);
        if (target == typeof(DateTime)) return value.AsDate(what);
        if (target == typeof(TimeSpan)) return value.AsDuration(what);

        if (target == typeof(Vector)) return ToVector(value, what);
        if (target == typeof(double[])) return ToVector(value, what).ToArray();
        if (target == typeof(Matrix)) return ToMatrix(value, what);
        if (target == typeof(ScriptTable)) return value.AsTable(what);
        if (target == typeof(ScriptList)) return ToList(value, what);
        if (target == typeof(ScriptRecord)) return value.AsRecord(what);
        if (target == typeof(ScriptRange)) return value.AsRange(what);
        if (target == typeof(ScriptValue[])) return ToArray(ToList(value, what));
        if (target == typeof(string[])) return ToStringArray(value, what);
        if (typeof(ScriptCallable).IsAssignableFrom(target)) return value.AsCallable(what);
        if (target == typeof(ScriptHandle)) return value.AsHandle(what);

        if (value.Type == ScriptType.Handle)
        {
            ScriptHandle handle = value.AsHandle(what);

            if (target.IsInstanceOfType(handle.Target)) return handle.Target;

            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"{what}: дескриптор '{handle.TypeName}' не подходит по типу",
                $"ожидался {target.Name}");
        }

        if (target == typeof(object)) return Unwrap(value);

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: значение типа {value.Type.ToName()} не переводится в {target.Name}");
    }

    /// <summary>Переводит значение C# в значение языка.</summary>
    /// <param name="value">Значение C#.</param>
    /// <param name="handleType">Тип-тег для дескриптора, если тип неизвестен языку.</param>
    public static ScriptValue FromClr(object? value, string? handleType = null) => value switch
    {
        null => ScriptValue.None,
        ScriptValue script => script,
        double number => ScriptValue.Num(number),
        float number => ScriptValue.Num(number),
        int number => ScriptValue.Num(number),
        long number => ScriptValue.Num(number),
        short number => ScriptValue.Num(number),
        byte number => ScriptValue.Num(number),
        decimal number => ScriptValue.Num((double)number),
        bool flag => ScriptValue.Bool(flag),
        string text => ScriptValue.Str(text),
        DateTime moment => ScriptValue.Date(moment),
        TimeSpan duration => ScriptValue.Dur(duration),
        Vector vector => ScriptValue.Vec(vector),
        Matrix matrix => ScriptValue.Mat(matrix),
        ScriptTable table => ScriptValue.Table(table),
        ScriptColumn column => column.AsValue(),
        ScriptList list => ScriptValue.List(list),
        ScriptRecord record => ScriptValue.Record(record),
        ScriptRange range => ScriptValue.Range(range),
        ScriptCallable callable => ScriptValue.Fn(callable),
        ScriptHandle handle => ScriptValue.Handle(handle),
        ScriptValue[] values => ScriptValue.List(ScriptList.Own(values)),
        double[] numbers => ScriptValue.Vec(new Vector(numbers)),
        int[] numbers => ScriptValue.Vec(new Vector(Array.ConvertAll(numbers, x => (double)x))),
        string[] strings => ScriptValue.List(ScriptList.Own(Array.ConvertAll(strings, ScriptValue.Str))),

        // Словарь и список объектов — это то, во что превращает значения языка Unwrap.
        // Обратный перевод нужен и для round-trip результатов, и для того, чтобы хост мог
        // подать в скрипт запись или разнородный список, а не только число и вектор.
        // Порядок важен: string[] ковариантен к IReadOnlyList<object>, и без явного случая
        // выше массив строк уходил бы в общую ветку.
        IReadOnlyDictionary<string, object?> map => ScriptValue.Record(ScriptRecord.From(
            map.Select(pair => new KeyValuePair<string, ScriptValue>(pair.Key, FromClr(pair.Value))))),
        IReadOnlyList<object?> objects => ScriptValue.List(ScriptList.From(objects.Select(item => FromClr(item)))),
        IEnumerable<double> numbers => ScriptValue.Vec(new Vector(numbers)),
        IEnumerable<string> strings => ScriptValue.List(ScriptList.From(strings.Select(ScriptValue.Str))),
        IEnumerable<ScriptValue> values => ScriptValue.List(ScriptList.From(values)),
        _ => ScriptValue.Handle(new ScriptHandle(handleType ?? value.GetType().Name, value)),
    };

    /// <summary>Разворачивает значение языка в «обычный» объект C# для <c>emit</c>.</summary>
    public static object? Unwrap(ScriptValue value) => value.Type switch
    {
        ScriptType.None => null,
        ScriptType.Num => value.RawNumber,
        ScriptType.Bool => value.RawNumber != 0,
        ScriptType.Str => value.AsString(),
        ScriptType.Date => value.AsDate(),
        ScriptType.Dur => value.AsDuration(),
        ScriptType.Vec => value.AsVector(),
        ScriptType.Mat => value.AsMatrix(),
        ScriptType.Table => UnwrapTable(value.AsTable()),
        ScriptType.Range => new Vector(value.AsRange().Values()),
        ScriptType.List => UnwrapList(value.AsList()),
        ScriptType.Record => UnwrapRecord(value.AsRecord()),
        ScriptType.Handle => value.AsHandle(),
        _ => value.RawReference,
    };

    private static List<object?> UnwrapList(ScriptList list)
    {
        var result = new List<object?>(list.Count);
        foreach (ScriptValue item in list) result.Add(Unwrap(item));
        return result;
    }

    private static Dictionary<string, object?> UnwrapRecord(ScriptRecord record)
    {
        var result = new Dictionary<string, object?>(record.Count, StringComparer.Ordinal);
        foreach (var pair in record.Pairs()) result[pair.Key] = Unwrap(pair.Value);
        return result;
    }

    /// <summary>
    /// Разворачивает таблицу в словарь «колонка → данные».
    /// </summary>
    /// <remarks>
    /// Числовая колонка становится <see cref="Vector"/>, остальные — списком: вызывающему
    /// нужны данные в типах фреймворка, а не наш внутренний тип.
    /// </remarks>
    private static Dictionary<string, object?> UnwrapTable(ScriptTable table)
    {
        var result = new Dictionary<string, object?>(table.ColumnCount, StringComparer.Ordinal);

        foreach (ScriptColumn column in table.Columns) result[column.Name] = Unwrap(column.AsValue());

        return result;
    }

    private static Matrix ToMatrix(ScriptValue value, string what)
    {
        if (value.Type == ScriptType.Mat) return value.AsMatrix(what);
        if (value.Type == ScriptType.Table) return value.AsTable(what).ToMatrix();

        // Вектор — это одна строка матрицы: так функция, ожидающая выборку объектов,
        // принимает и одиночный объект без ручного оборачивания.
        if (value.Type == ScriptType.Vec)
        {
            Vector row = value.AsVector();
            var matrix = new Matrix(1, row.Count);

            for (int j = 0; j < row.Count; j++) matrix[0, j] = row[j];

            return matrix;
        }

        throw ScriptError.Type(what, ScriptType.Mat, value.Type);
    }

    private static double ExpectNumber(ScriptValue value, string what) => value.AsNumber(what);

    private static long ExpectInteger(ScriptValue value, string what)
    {
        double number = value.AsNumber(what);
        double rounded = Math.Round(number);

        if (Math.Abs(number - rounded) > IntegerTolerance)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"{what}: ожидалось целое число, получено {ScriptFormatter.Number(number)}",
                "округлите значение явно: core.round(x)");
        }

        return (long)rounded;
    }

    private static Vector ToVector(ScriptValue value, string what)
    {
        if (value.Type == ScriptType.Vec) return value.AsVector(what);
        if (value.Type == ScriptType.Range) return new Vector(value.AsRange().Values());

        if (value.Type == ScriptType.List)
        {
            ScriptList list = value.AsList(what);
            var vector = new Vector(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type != ScriptType.Num)
                {
                    throw new ScriptError(
                        DiagnosticCodes.TypeMismatch,
                        $"{what}: элемент {i} списка имеет тип {list[i].Type.ToName()}, а нужен num",
                        "вектор собирается только из чисел");
                }

                vector[i] = list[i].RawNumber;
            }

            return vector;
        }

        throw ScriptError.Type(what, ScriptType.Vec, value.Type);
    }

    private static ScriptList ToList(ScriptValue value, string what)
    {
        if (value.Type == ScriptType.List) return value.AsList(what);

        if (value.Type == ScriptType.Vec)
        {
            Vector vector = value.AsVector();
            var items = new ScriptValue[vector.Count];

            for (int i = 0; i < vector.Count; i++) items[i] = ScriptValue.Num(vector[i]);

            return ScriptList.Own(items);
        }

        if (value.Type == ScriptType.Range)
        {
            var items = new List<ScriptValue>();
            foreach (double number in value.AsRange().Values()) items.Add(ScriptValue.Num(number));

            return ScriptList.From(items);
        }

        throw ScriptError.Type(what, ScriptType.List, value.Type);
    }

    private static ScriptValue[] ToArray(ScriptList list)
    {
        var items = new ScriptValue[list.Count];
        for (int i = 0; i < list.Count; i++) items[i] = list[i];
        return items;
    }

    private static string[] ToStringArray(ScriptValue value, string what)
    {
        ScriptList list = ToList(value, what);
        var items = new string[list.Count];

        for (int i = 0; i < list.Count; i++) items[i] = list[i].AsString($"{what}: элемент {i}");

        return items;
    }
}
