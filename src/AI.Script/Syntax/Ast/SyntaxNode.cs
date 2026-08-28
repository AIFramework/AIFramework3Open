using AI.Script.Runtime;

namespace AI.Script.Syntax.Ast;

/// <summary>Узел синтаксического дерева.</summary>
public abstract class SyntaxNode
{
    /// <summary>Отрезок исходника, покрытый узлом.</summary>
    public TextSpan Span { get; set; }
}

/// <summary>Выражение — узел, дающий значение.</summary>
public abstract class Expr : SyntaxNode
{
}

/// <summary>Инструкция — узел, исполняемый ради действия.</summary>
public abstract class Stmt : SyntaxNode
{
}

/// <summary>Разобранный скрипт целиком.</summary>
public sealed class ScriptUnit : SyntaxNode
{
    /// <summary>Блок <c>options</c>, если он есть.</summary>
    public OptionsStmt? Options { get; set; }

    /// <summary>Инструкции верхнего уровня.</summary>
    public List<Stmt> Statements { get; } = [];
}

/// <summary>Параметр функции или стадии.</summary>
public sealed class ParameterNode : SyntaxNode
{
    /// <summary>Имя параметра.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Объявленный тип; <c>null</c>, если не указан.</summary>
    public ScriptType? DeclaredType { get; set; }

    /// <summary>Значение по умолчанию; <c>null</c>, если параметр обязателен.</summary>
    public Expr? Default { get; set; }
}

/// <summary>Атрибут перед объявлением: <c>@cache</c>, <c>@retry(3)</c>.</summary>
public sealed class AttributeNode : SyntaxNode
{
    /// <summary>Имя атрибута без <c>@</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Аргументы атрибута.</summary>
    public List<Expr> Arguments { get; } = [];
}

/// <summary>Аргумент вызова: позиционный, именованный либо плейсхолдер <c>_</c>.</summary>
public sealed class ArgumentNode : SyntaxNode
{
    /// <summary>Имя аргумента; <c>null</c> для позиционного.</summary>
    public string? Name { get; set; }

    /// <summary>Отрезок имени; используется в диагностике.</summary>
    public TextSpan NameSpan { get; set; }

    /// <summary>Значение аргумента; <c>null</c> для плейсхолдера.</summary>
    public Expr? Value { get; set; }

    /// <summary>Является ли аргумент плейсхолдером конвейера.</summary>
    public bool IsPlaceholder { get; set; }
}

/// <summary>Элемент индексации: одиночный индекс, срез либо <c>:</c>.</summary>
/// <remarks>
/// Срез не имеет собственного узла: <c>xs[2..5]</c> — это обычное выражение-диапазон в позиции
/// индекса. Один способ записать диапазон, одна реализация, одно место для ошибок.
/// </remarks>
public sealed class IndexArgument : SyntaxNode
{
    /// <summary>Выражение индекса; <c>null</c> для <c>:</c>.</summary>
    public Expr? Value { get; set; }

    /// <summary>Является ли элемент «весь диапазон» (<c>:</c>).</summary>
    public bool IsAll { get; set; }
}

/// <summary>Поле литерала записи либо распаковка <c>...</c>.</summary>
public sealed class RecordFieldNode : SyntaxNode
{
    /// <summary>Имя поля; <c>null</c> для распаковки.</summary>
    public string? Name { get; set; }

    /// <summary>Значение поля либо распаковываемая запись.</summary>
    public Expr Value { get; set; } = null!;

    /// <summary>Является ли элемент распаковкой.</summary>
    public bool IsSpread { get; set; }
}

/// <summary>Часть интерполированной строки.</summary>
public sealed class InterpolationPart
{
    /// <summary>Готовый текст; <c>null</c> для подстановки.</summary>
    public string? Text { get; set; }

    /// <summary>Выражение подстановки; <c>null</c> для текста.</summary>
    public Expr? Expression { get; set; }
}

/// <summary>Поле блока <c>options</c>.</summary>
public sealed class OptionFieldNode : SyntaxNode
{
    /// <summary>Имя поля.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Значение поля; только литерал.</summary>
    public ScriptValue Value { get; set; }
}
