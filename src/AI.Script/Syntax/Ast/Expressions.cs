using AI.Script.Runtime;

namespace AI.Script.Syntax.Ast;

/// <summary>Литерал: число, строка без подстановок, логическое значение, дата, длительность, <c>none</c>.</summary>
public sealed class LiteralExpr : Expr
{
    /// <summary>Значение литерала.</summary>
    public ScriptValue Value { get; set; }
}

/// <summary>Строка с подстановками <c>${...}</c>.</summary>
public sealed class InterpolationExpr : Expr
{
    /// <summary>Части строки в порядке следования.</summary>
    public List<InterpolationPart> Parts { get; } = [];
}

/// <summary>Одиночное имя.</summary>
public sealed class NameExpr : Expr
{
    /// <summary>Имя.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>Плейсхолдер конвейера <c>_</c>.</summary>
public sealed class PlaceholderExpr : Expr
{
}

/// <summary>
/// Обращение через точку: поле записи, функция пространства имён либо метод дескриптора.
/// </summary>
/// <remarks>
/// Разрешение откладывается до семантики: синтаксически <c>ml.kmeans</c> и <c>cfg.temp</c>
/// неотличимы, а различает их то, связано ли имя слева с переменной.
/// </remarks>
public sealed class MemberExpr : Expr
{
    /// <summary>Выражение слева от точки.</summary>
    public Expr Target { get; set; } = null!;

    /// <summary>Имя справа от точки.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Отрезок имени справа от точки.</summary>
    public TextSpan NameSpan { get; set; }
}

/// <summary>Индексация и срезы.</summary>
public sealed class IndexExpr : Expr
{
    /// <summary>Индексируемое выражение.</summary>
    public Expr Target { get; set; } = null!;

    /// <summary>Элементы индекса.</summary>
    public List<IndexArgument> Arguments { get; } = [];
}

/// <summary>Вызов функции.</summary>
public sealed class CallExpr : Expr
{
    /// <summary>Вызываемое выражение.</summary>
    public Expr Callee { get; set; } = null!;

    /// <summary>Аргументы.</summary>
    public List<ArgumentNode> Arguments { get; } = [];

    /// <summary>Отрезок скобок вызова; используется в диагностике по аргументам.</summary>
    public TextSpan ArgumentsSpan { get; set; }
}

/// <summary>Унарная операция.</summary>
public sealed class UnaryExpr : Expr
{
    /// <summary>Оператор.</summary>
    public UnaryOperator Operator { get; set; }

    /// <summary>Операнд.</summary>
    public Expr Operand { get; set; } = null!;
}

/// <summary>Бинарная операция.</summary>
public sealed class BinaryExpr : Expr
{
    /// <summary>Оператор.</summary>
    public BinaryOperator Operator { get; set; }

    /// <summary>Левый операнд.</summary>
    public Expr Left { get; set; } = null!;

    /// <summary>Правый операнд.</summary>
    public Expr Right { get; set; } = null!;

    /// <summary>Отрезок знака оператора.</summary>
    public TextSpan OperatorSpan { get; set; }
}

/// <summary>Звено конвейера: <c>left |&gt; right(...)</c>.</summary>
public sealed class PipeExpr : Expr
{
    /// <summary>Значение, подаваемое в звено.</summary>
    public Expr Left { get; set; } = null!;

    /// <summary>Вызов, принимающий значение.</summary>
    public CallExpr Right { get; set; } = null!;

    /// <summary>Отрезок знака <c>|&gt;</c>.</summary>
    public TextSpan OperatorSpan { get; set; }
}

/// <summary>Диапазон <c>a..b</c> с необязательным шагом.</summary>
public sealed class RangeExpr : Expr
{
    /// <summary>Начало.</summary>
    public Expr From { get; set; } = null!;

    /// <summary>Конец (исключительно).</summary>
    public Expr To { get; set; } = null!;

    /// <summary>Шаг; <c>null</c> — единица.</summary>
    public Expr? By { get; set; }
}

/// <summary>Лямбда.</summary>
public sealed class LambdaExpr : Expr
{
    /// <summary>Имена параметров.</summary>
    public List<string> Parameters { get; } = [];

    /// <summary>Тело: выражение либо блок.</summary>
    public Expr Body { get; set; } = null!;
}

/// <summary>Условное выражение; в позиции инструкции ветвь <c>else</c> необязательна.</summary>
public sealed class IfExpr : Expr
{
    /// <summary>Условие.</summary>
    public Expr Condition { get; set; } = null!;

    /// <summary>Ветвь истины.</summary>
    public BlockExpr Then { get; set; } = null!;

    /// <summary>Ветвь лжи: блок либо вложенный <c>if</c>; <c>null</c>, если её нет.</summary>
    public Expr? Else { get; set; }
}

/// <summary>Блок инструкций; его значение — значение последнего выражения.</summary>
public sealed class BlockExpr : Expr
{
    /// <summary>Инструкции блока.</summary>
    public List<Stmt> Statements { get; } = [];
}

/// <summary>Литерал списка <c>[...]</c>.</summary>
public sealed class ListExpr : Expr
{
    /// <summary>Элементы.</summary>
    public List<Expr> Items { get; } = [];
}

/// <summary>Литерал вектора <c>&lt;...&gt;</c>.</summary>
public sealed class VectorExpr : Expr
{
    /// <summary>Элементы.</summary>
    public List<Expr> Items { get; } = [];
}

/// <summary>Литерал записи <c>{...}</c>.</summary>
public sealed class RecordExpr : Expr
{
    /// <summary>Поля и распаковки в порядке записи.</summary>
    public List<RecordFieldNode> Fields { get; } = [];
}
