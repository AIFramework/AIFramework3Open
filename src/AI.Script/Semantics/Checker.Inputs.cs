using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Syntax.Ast;

namespace AI.Script.Semantics;

/// <summary>Вход скрипта: что он просит у хоста.</summary>
/// <param name="Name">Имя, по которому хост подаёт данные.</param>
/// <param name="Kind">Чего скрипт ждёт: <c>table</c>, <c>doc</c>, <c>image</c>, <c>text</c>; пусто — что угодно.</param>
/// <param name="About">Пояснение для того, кто будет искать эти данные.</param>
public sealed record ScriptInput(string Name, string Kind, string About);

/// <summary>
/// Сбор входов, которые скрипт просит у хоста.
/// </summary>
/// <remarks>
/// Скрипт пишется раньше, чем найдены файлы: модель описывает работу, а данные к ней подаёт
/// хост. Объявленный вход делает этот разрыв видимым — проверка возвращает список того, чего
/// не хватает, и хост решает, где это взять, не запуская прогон и не тратя ни секунды счёта.
/// <para>
/// Имя обязано быть литералом: вход, имя которого вычисляется, нельзя ни показать в списке, ни
/// проверить до запуска — а именно ради этого он и объявляется.
/// </para>
/// </remarks>
public sealed partial class Checker
{
    /// <summary>Полное имя функции, объявляющей вход.</summary>
    private const string InputFunction = "core.input";

    private void RecordInput(CallExpr call, ScriptFunction function, Expr? pipedSource)
    {
        if (!string.Equals(function.FullName, InputFunction, StringComparison.Ordinal)) return;

        if (StringLiteral(Argument(call, function, 0, pipedSource)) is not { } name)
        {
            _diagnostics.Warning(
                DiagnosticCodes.NotImplementedYet, call.Span,
                "имя входа вычисляется, поэтому в список входов он не попал",
                "пишите имя литералом: input(\"продажи\"), иначе хост не узнает, что подать");

            return;
        }

        foreach (ScriptInput known in _inputs)
        {
            if (string.Equals(known.Name, name, StringComparison.Ordinal)) return;
        }

        _inputs.Add(new ScriptInput(
            name,
            StringLiteral(Argument(call, function, 1, pipedSource)) ?? string.Empty,
            StringLiteral(Argument(call, function, 2, pipedSource)) ?? string.Empty));
    }

    /// <summary>Колонки входа, если хост уже сказал, что подаст.</summary>
    private IReadOnlyList<string>? InputColumns(CallExpr call, ScriptFunction function, Expr? pipedSource)
    {
        if (!string.Equals(function.FullName, InputFunction, StringComparison.Ordinal)) return null;

        return StringLiteral(Argument(call, function, 0, pipedSource)) is { } name
            && _seededColumns.TryGetValue(name, out IReadOnlyList<string>? columns)
            ? columns
            : null;
    }
}
