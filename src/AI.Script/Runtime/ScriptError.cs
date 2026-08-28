using AI.Script.Semantics;
using AI.Script.Syntax;

namespace AI.Script.Runtime;

/// <summary>
/// Отказ во время исполнения скрипта.
/// </summary>
/// <remarks>
/// Отрезок исходника заполняет интерпретатор на том узле, где отказ всплыл: сами операции над
/// значениями о разметке ничего не знают и знать не должны.
/// </remarks>
public class ScriptError : Exception
{
    /// <summary>Код диагностики.</summary>
    public string Code { get; }

    /// <summary>Что делать.</summary>
    public string? Hint { get; }

    /// <summary>Отрезок исходника; заполняется интерпретатором.</summary>
    public TextSpan? Span { get; set; }

    /// <summary>Создаёт отказ.</summary>
    public ScriptError(string code, string message, string? hint = null)
        : base(message)
    {
        Code = code;
        Hint = hint;
    }

    /// <summary>Создаёт отказ поверх исключения из вызванной функции фреймворка.</summary>
    public ScriptError(string code, string message, Exception inner, string? hint = null)
        : base(message, inner)
    {
        Code = code;
        Hint = hint;
    }

    /// <summary>Отказ по несовпадению типа.</summary>
    public static ScriptError Type(string what, ScriptType expected, ScriptType actual) =>
        new(DiagnosticCodes.BadOperand,
            $"{what}: ожидался тип {expected.ToName()}, получен {actual.ToName()}");
}

/// <summary>
/// Прерывание прогона по лимиту либо отмене: перехватывать в скрипте нельзя.
/// </summary>
/// <remarks>
/// Отдельный тип нужен ровно затем, чтобы <c>try/catch</c> скрипта его не поймал. Иначе скрипт
/// мог бы проигнорировать собственный таймаут, а лимит перестал бы быть лимитом.
/// </remarks>
public sealed class ScriptAbort : ScriptError
{
    /// <summary>Создаёт прерывание.</summary>
    public ScriptAbort(string code, string message, string? hint = null)
        : base(code, message, hint)
    {
    }
}
