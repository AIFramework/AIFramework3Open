using AI.Script.Hosting;
using AI.Script.Semantics;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;

namespace AI.Script.Runtime;

/// <summary>
/// Отладка: сообщения отладчику и контрольные значения.
/// </summary>
/// <remarks>
/// Без отладчика здесь ничего не исполняется: интерпретатор проверяет <c>_context.Debugger</c>
/// до вызова, и прогон без отладки платит одной проверкой на инструкцию.
/// </remarks>
public sealed partial class Interpreter
{
    /// <summary>Сообщает отладчику, где стоит прогон; отладчик вправе здесь же поставить его на паузу.</summary>
    private void NotifyDebugger(DebugEventKind kind, TextSpan span, Scope scope, ScriptError? error)
    {
        if (_quiet || _context.Debugger is not IScriptDebugger debugger) return;

        _frame ??= new DebugFrame("скрипт", false, default, null, _global);
        _frame.Span = span;
        _frame.Scope = scope;

        debugger.OnEvent(new DebugContext(kind, this, _frame, _thread, _branchIndex, error, _source, _context.Cancellation));
    }

    /// <summary>
    /// Вычисляет выражение в области приостановленного кадра.
    /// </summary>
    /// <remarks>
    /// Отдельный интерпретатор-ветвь, а не новый: новый перехватил бы <c>context.Interpreter</c>.
    /// Ветвь «тихая» — точки останова внутри контрольного значения не срабатывают, иначе
    /// отладчик остановился бы сам в себе. Вычисление обязано завершиться сразу: оно идёт в
    /// потоке, который отладчик держит на паузе.
    /// </remarks>
    internal ScriptValue EvaluateWatch(string text, Scope scope)
    {
        var source = new SourceText(text ?? string.Empty, "<наблюдение>");
        var diagnostics = new DiagnosticBag(source);
        ScriptUnit unit = new Parser(source, diagnostics).ParseUnit();

        if (diagnostics.HasErrors || unit.Statements.Count != 1 || unit.Statements[0] is not ExpressionStmt { Expression: Expr expression })
        {
            throw new ScriptError(
                DiagnosticCodes.WatchNotExpression,
                "контрольное значение — одно выражение",
                "например: total, xs[0], len(rows), x * 2");
        }

        Interpreter probe = Branch();
        probe._quiet = true;

        ValueTask<ScriptValue> pending = probe.EvaluateAsync(expression, scope);

        if (!pending.IsCompleted)
        {
            throw new ScriptError(
                DiagnosticCodes.WatchNotImmediate,
                "контрольное значение обратилось к файлам или сети",
                "на паузе вычисляются только выражения без ввода-вывода");
        }

        return pending.GetAwaiter().GetResult();
    }
}
