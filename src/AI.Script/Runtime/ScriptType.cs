namespace AI.Script.Runtime;

/// <summary>
/// Тип значения языка. Он же используется в аннотациях типов и в сигнатурах привязок.
/// </summary>
/// <remarks>
/// Один перечислимый тип на рантайм и на аннотации — сознательное упрощение: в языке без
/// пользовательских типов «тег значения» и «объявленный тип» совпадают везде, кроме
/// <see cref="Any"/>, который в рантайме не встречается.
/// </remarks>
public enum ScriptType
{
    /// <summary>Отсутствие значения.</summary>
    None = 0,

    /// <summary>Число двойной точности.</summary>
    Num,

    /// <summary>Логическое значение.</summary>
    Bool,

    /// <summary>Строка.</summary>
    Str,

    /// <summary>Момент времени.</summary>
    Date,

    /// <summary>Длительность.</summary>
    Dur,

    /// <summary>Числовой вектор (<c>AI.DataStructs.Algebraic.Vector</c>).</summary>
    Vec,

    /// <summary>Комплексный вектор.</summary>
    CVec,

    /// <summary>Матрица.</summary>
    Mat,

    /// <summary>Многомерный тензор.</summary>
    Tensor,

    /// <summary>Разнородный список.</summary>
    List,

    /// <summary>Запись «поле → значение».</summary>
    Record,

    /// <summary>Колоночная таблица.</summary>
    Table,

    /// <summary>Диапазон чисел.</summary>
    Range,

    /// <summary>Функция или лямбда.</summary>
    Fn,

    /// <summary>Дескриптор объекта фреймворка.</summary>
    Handle,

    /// <summary>Любой тип; только в аннотациях и сигнатурах.</summary>
    Any,
}

/// <summary>Имена типов языка.</summary>
public static class ScriptTypeNames
{
    /// <summary>Имя типа, как оно пишется в скрипте.</summary>
    public static string ToName(this ScriptType type) => type switch
    {
        ScriptType.None => "none",
        ScriptType.Num => "num",
        ScriptType.Bool => "bool",
        ScriptType.Str => "str",
        ScriptType.Date => "date",
        ScriptType.Dur => "dur",
        ScriptType.Vec => "vec",
        ScriptType.CVec => "cvec",
        ScriptType.Mat => "mat",
        ScriptType.Tensor => "tensor",
        ScriptType.List => "list",
        ScriptType.Record => "record",
        ScriptType.Table => "table",
        ScriptType.Range => "range",
        ScriptType.Fn => "fn",
        ScriptType.Handle => "handle",
        _ => "any",
    };

    /// <summary>Разбирает имя типа; <c>null</c>, если такого типа нет.</summary>
    public static ScriptType? Parse(string name) => name switch
    {
        "none" => ScriptType.None,
        "num" => ScriptType.Num,
        "bool" => ScriptType.Bool,
        "str" => ScriptType.Str,
        "date" => ScriptType.Date,
        "dur" => ScriptType.Dur,
        "vec" => ScriptType.Vec,
        "cvec" => ScriptType.CVec,
        "mat" => ScriptType.Mat,
        "tensor" => ScriptType.Tensor,
        "list" => ScriptType.List,
        "record" => ScriptType.Record,
        "table" => ScriptType.Table,
        "range" => ScriptType.Range,
        "fn" => ScriptType.Fn,
        "handle" => ScriptType.Handle,
        "any" => ScriptType.Any,
        _ => null,
    };
}
