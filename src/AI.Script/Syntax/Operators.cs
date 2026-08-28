namespace AI.Script.Syntax;

/// <summary>Унарный оператор.</summary>
public enum UnaryOperator
{
    /// <summary>Смена знака: <c>-x</c>.</summary>
    Negate,

    /// <summary>Логическое отрицание: <c>!x</c>.</summary>
    Not,
}

/// <summary>Бинарный оператор.</summary>
public enum BinaryOperator
{
    /// <summary><c>+</c></summary>
    Add,

    /// <summary><c>-</c></summary>
    Subtract,

    /// <summary><c>*</c></summary>
    Multiply,

    /// <summary><c>/</c></summary>
    Divide,

    /// <summary><c>%</c></summary>
    Modulo,

    /// <summary><c>^</c></summary>
    Power,

    /// <summary><c>==</c></summary>
    Equal,

    /// <summary><c>!=</c></summary>
    NotEqual,

    /// <summary><c>&lt;</c></summary>
    Less,

    /// <summary><c>&gt;</c></summary>
    Greater,

    /// <summary><c>&lt;=</c></summary>
    LessOrEqual,

    /// <summary><c>&gt;=</c></summary>
    GreaterOrEqual,

    /// <summary><c>&amp;&amp;</c></summary>
    And,

    /// <summary><c>||</c></summary>
    Or,
}

/// <summary>Текстовое представление операторов для диагностики.</summary>
public static class OperatorText
{
    /// <summary>Знак бинарного оператора.</summary>
    public static string Of(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Power => "^",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.Greater => ">",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.And => "&&",
        _ => "||",
    };

    /// <summary>Знак унарного оператора.</summary>
    public static string Of(UnaryOperator op) => op == UnaryOperator.Negate ? "-" : "!";

    /// <summary>Является ли оператор сравнением (они не ассоциативны).</summary>
    public static bool IsComparison(BinaryOperator op) => op
        is BinaryOperator.Equal
        or BinaryOperator.NotEqual
        or BinaryOperator.Less
        or BinaryOperator.Greater
        or BinaryOperator.LessOrEqual
        or BinaryOperator.GreaterOrEqual;
}
