using AI.Script.Runtime;

namespace AI.Script.Syntax.Ast;

/// <summary>Блок <c>options</c> в начале файла.</summary>
public sealed class OptionsStmt : Stmt
{
    /// <summary>Поля блока.</summary>
    public List<OptionFieldNode> Fields { get; } = [];
}

/// <summary>Связывание нового имени: <c>let x = ...</c>.</summary>
public sealed class LetStmt : Stmt
{
    /// <summary>Имя.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Отрезок имени.</summary>
    public TextSpan NameSpan { get; set; }

    /// <summary>Объявленный тип; <c>null</c>, если не указан.</summary>
    public ScriptType? DeclaredType { get; set; }

    /// <summary>Значение.</summary>
    public Expr Value { get; set; } = null!;
}

/// <summary>Присваивание уже связанному имени: <c>set x = ...</c>.</summary>
public sealed class SetStmt : Stmt
{
    /// <summary>Цель присваивания: имя, элемент по индексу либо поле.</summary>
    public Expr Target { get; set; } = null!;

    /// <summary>Оператор составного присваивания; <c>null</c> для простого <c>=</c>.</summary>
    public BinaryOperator? Compound { get; set; }

    /// <summary>Присваиваемое значение.</summary>
    public Expr Value { get; set; } = null!;
}

/// <summary>Объявление функции либо стадии.</summary>
public sealed class FunctionDeclStmt : Stmt
{
    /// <summary>Имя.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Отрезок имени.</summary>
    public TextSpan NameSpan { get; set; }

    /// <summary>Параметры.</summary>
    public List<ParameterNode> Parameters { get; } = [];

    /// <summary>Объявленный тип результата.</summary>
    public ScriptType? ReturnType { get; set; }

    /// <summary>Тело.</summary>
    public BlockExpr Body { get; set; } = null!;

    /// <summary>Является ли объявление стадией конвейера.</summary>
    public bool IsStage { get; set; }

    /// <summary>Атрибуты перед объявлением.</summary>
    public List<AttributeNode> Attributes { get; } = [];

    /// <summary>Документирующий комментарий <c>#|</c>.</summary>
    public string? Documentation { get; set; }
}

/// <summary>Цикл по последовательности.</summary>
public sealed class ForStmt : Stmt
{
    /// <summary>Имена переменных цикла; их больше одного при деструктуризации пары.</summary>
    public List<string> Names { get; } = [];

    /// <summary>Перебираемое выражение.</summary>
    public Expr Iterable { get; set; } = null!;

    /// <summary>Шаг для диапазона; <c>null</c>, если не указан.</summary>
    public Expr? By { get; set; }

    /// <summary>Тело.</summary>
    public BlockExpr Body { get; set; } = null!;
}

/// <summary>Цикл с условием.</summary>
public sealed class WhileStmt : Stmt
{
    /// <summary>Условие.</summary>
    public Expr Condition { get; set; } = null!;

    /// <summary>Тело.</summary>
    public BlockExpr Body { get; set; } = null!;
}

/// <summary>Выход из цикла.</summary>
public sealed class BreakStmt : Stmt
{
}

/// <summary>Переход к следующей итерации.</summary>
public sealed class ContinueStmt : Stmt
{
}

/// <summary>Возврат из функции.</summary>
public sealed class ReturnStmt : Stmt
{
    /// <summary>Возвращаемое значение; <c>null</c> — <c>none</c>.</summary>
    public Expr? Value { get; set; }
}

/// <summary>Перехват отказа.</summary>
public sealed class TryStmt : Stmt
{
    /// <summary>Защищаемый блок.</summary>
    public BlockExpr Body { get; set; } = null!;

    /// <summary>Имя переменной с описанием отказа.</summary>
    public string ErrorName { get; set; } = "e";

    /// <summary>Обработчик.</summary>
    public BlockExpr Handler { get; set; } = null!;
}

/// <summary>Псевдоним пространства имён.</summary>
public sealed class UseStmt : Stmt
{
    /// <summary>Имя пространства.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Псевдоним.</summary>
    public string Alias { get; set; } = string.Empty;
}

/// <summary>Именованный результат прогона.</summary>
public sealed class EmitStmt : Stmt
{
    /// <summary>Имя результата.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Отрезок имени.</summary>
    public TextSpan NameSpan { get; set; }

    /// <summary>Значение.</summary>
    public Expr Value { get; set; } = null!;
}

/// <summary>Показ значения пользователю.</summary>
public sealed class ShowStmt : Stmt
{
    /// <summary>Значение.</summary>
    public Expr Value { get; set; } = null!;
}

/// <summary>Проверка инварианта.</summary>
public sealed class AssertStmt : Stmt
{
    /// <summary>Условие.</summary>
    public Expr Condition { get; set; } = null!;

    /// <summary>Пояснение; <c>null</c>, если не указано.</summary>
    public Expr? Message { get; set; }
}

/// <summary>Выражение в позиции инструкции.</summary>
public sealed class ExpressionStmt : Stmt
{
    /// <summary>Выражение.</summary>
    public Expr Expression { get; set; } = null!;
}
