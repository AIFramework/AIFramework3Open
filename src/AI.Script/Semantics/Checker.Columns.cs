using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;

namespace AI.Script.Semantics;

/// <summary>
/// Проверка имён колонок у таблиц, схема которых известна до запуска.
/// </summary>
/// <remarks>
/// Схема известна, когда таблица собрана литералом (<c>table.of({...})</c>), подана хостом,
/// возвращена функцией с объявленными колонками либо получена из такой таблицы функцией
/// <c>table</c>, чей результат выводится из входа (<c>filter</c>, <c>select</c>, <c>derive</c>,
/// <c>group_by</c>…). Во всех остальных случаях схема неизвестна и проверка молчит: проверка,
/// которая ошибается, хуже отсутствующей.
/// <para>
/// Колонки лежат рядом с областями видимости, и ключ — сама область. Стадия подменяет набор
/// областей целиком и потом возвращает прежний; колонки уходят и возвращаются вместе с ним,
/// без отдельного учёта.
/// </para>
/// </remarks>
public sealed partial class Checker
{
    /// <summary>Функции <c>table</c>, лямбда которых получает строку входа либо подтаблицу группы.</summary>
    private static readonly HashSet<string> s_rowLambdas = new(StringComparer.Ordinal)
    {
        "filter", "sort", "derive", "group_by",
    };

    /// <summary>Функции <c>table</c>, результат которых имеет те же колонки, что и вход.</summary>
    private static readonly HashSet<string> s_sameColumns = new(StringComparer.Ordinal)
    {
        "filter", "sort", "head", "tail", "shuffle", "drop_na", "fill_na", "distinct", "rows",
    };

    /// <summary>Параметр функции <c>table</c>, который называет уже существующие колонки входа.</summary>
    private static readonly Dictionary<string, string> s_columnParameters = new(StringComparer.Ordinal)
    {
        ["select"] = "cols",
        ["drop"] = "cols",
        ["one_hot"] = "cols",
        ["encode"] = "cols",
        ["drop_na"] = "cols",
        ["fill_na"] = "cols",
        ["distinct"] = "by",
        ["sort"] = "by",
        ["group_by"] = "by",
        ["column"] = "name",
        ["rename"] = "from",
    };

    private readonly Dictionary<Dictionary<string, ScriptType?>, Dictionary<string, IReadOnlyList<string>>> _columns = new();
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _seededColumns;

    /// <summary>Вход конвейера для звена, которое сейчас проверяется.</summary>
    private Expr? _pipedSource;

    /// <summary>Колонки строки для лямбды, стоящей прямо в аргументах функции <c>table</c>.</summary>
    private IReadOnlyList<string>? _rowColumns;

    private void DeclareColumns(string name, IReadOnlyList<string>? columns)
    {
        if (columns == null || string.IsNullOrEmpty(name) || _scopes.Count == 0) return;

        SetColumns(_scopes[^1], name, columns);
    }

    private void SetColumns(Dictionary<string, ScriptType?> scope, string name, IReadOnlyList<string>? columns)
    {
        if (columns == null)
        {
            if (_columns.TryGetValue(scope, out var known)) _ = known.Remove(name);
            return;
        }

        if (!_columns.TryGetValue(scope, out var names))
        {
            names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            _columns[scope] = names;
        }

        names[name] = columns;
    }

    private IReadOnlyList<string>? ColumnsOfName(string name)
    {
        for (int i = _scopes.Count - 1; i >= 0; i--)
        {
            if (!_scopes[i].ContainsKey(name)) continue;

            return _columns.TryGetValue(_scopes[i], out var names) && names.TryGetValue(name, out var columns)
                ? columns
                : null;
        }

        return null;
    }

    /// <summary>Колонки таблицы либо строки, которую даёт выражение; <c>null</c> — неизвестны.</summary>
    private IReadOnlyList<string>? ColumnsOf(Expr? expression) => expression switch
    {
        NameExpr name => ColumnsOfName(name.Name),
        PipeExpr pipe => ColumnsOfCall(pipe.Right, pipe.Left),
        CallExpr call => ColumnsOfCall(call, null),
        IndexExpr { Arguments.Count: 1 } index
            when index.Arguments[0].Value is { } key && InferQuietly(key) is ScriptType.Num or ScriptType.Range
            => ColumnsOf(index.Target),
        _ => null,
    };

    private IReadOnlyList<string>? ColumnsOfCall(CallExpr call, Expr? pipedSource)
    {
        ScriptFunction? function = NativeFunction(call);

        if (function == null) return null;
        if (InputColumns(call, function, pipedSource) is { } seeded) return seeded;
        if (function.Columns.Count > 0) return function.Columns;
        if (!string.Equals(function.Namespace, "table", StringComparison.Ordinal)) return null;

        IReadOnlyList<string>? source = ColumnsOf(Argument(call, function, 0, pipedSource));

        if (s_sameColumns.Contains(function.Name)) return source;

        Expr? second = Argument(call, function, 1, pipedSource);

        switch (function.Name)
        {
            case "of":
                // Пустая таблица: колонок пока нет, но их добавят (concat в цикле), и «колонок нет
                // никаких» проверке утверждать нельзя.
                return RecordFieldNames(Argument(call, function, 0, pipedSource)) is { Count: > 0 } fields ? fields : null;

            case "concat":
                // К пустой таблице приклеивается вторая целиком, и колонки берутся у нее.
                return source is { Count: > 0 } ? source : ColumnsOf(second);

            case "select":
                return StringList(second);

            case "drop":
                return source != null && StringList(second) is { } dropped
                    ? [.. source.Where(column => !dropped.Contains(column))]
                    : null;

            case "derive":
                return source != null && RecordFieldNames(second) is { } added ? Union(source, added) : null;

            case "with":
                return source != null && StringLiteral(second) is { } column ? Union(source, [column]) : null;

            case "rename":
                return source != null && StringLiteral(second) is { } oldName
                    && StringLiteral(Argument(call, function, 2, pipedSource)) is { } newName
                    ? [.. source.Select(name => string.Equals(name, oldName, StringComparison.Ordinal) ? newName : name)]
                    : null;

            case "group_by":
                {
                    IReadOnlyList<string>? keys = StringLiteral(second) is { } key ? [key] : StringList(second);

                    return keys != null && RecordFieldNames(Argument(call, function, 2, pipedSource)) is { } aggregates
                        ? Union(keys, aggregates)
                        : null;
                }

            default:
                return null;
        }
    }

    /// <summary>Поле строки через точку: <c>row.amount</c>.</summary>
    private void CheckField(NameExpr root, MemberExpr member, ScriptType? bound)
    {
        // У таблицы полей нет, колонка берётся индексом: t["amount"].
        if (bound == ScriptType.Table) return;

        if (ColumnsOfName(root.Name) is { } columns) RequireColumn(member.Name, member.NameSpan, columns);
    }

    /// <summary>Колонка по строковому индексу: <c>t["amount"]</c>, <c>row["amount"]</c>.</summary>
    private void CheckColumnIndex(IndexExpr index)
    {
        if (index.Arguments.Count != 1 || StringLiteral(index.Arguments[0].Value) is not { } name) return;

        if (ColumnsOf(index.Target) is { } columns) RequireColumn(name, index.Arguments[0].Span, columns);
    }

    /// <summary>Имена колонок в аргументах: <c>table.select(["x"])</c>, <c>table.sort(by: "x")</c>.</summary>
    private void CheckColumnArguments(CallExpr call, ScriptFunction function, Expr? pipedSource)
    {
        if (!string.Equals(function.Namespace, "table", StringComparison.Ordinal)) return;
        if (!s_columnParameters.TryGetValue(function.Name, out string? parameter)) return;
        if (ColumnsOf(Argument(call, function, 0, pipedSource)) is not { } columns) return;

        int position = -1;

        for (int i = 0; i < function.Parameters.Count; i++)
        {
            if (string.Equals(function.Parameters[i].Name, parameter, StringComparison.Ordinal)) position = i;
        }

        Expr? value = Argument(call, function, position, pipedSource);

        if (StringLiteral(value) is { } single)
        {
            RequireColumn(single, value!.Span, columns);
            return;
        }

        if (value is not ListExpr list) return;

        foreach (Expr item in list.Items)
        {
            if (StringLiteral(item) is { } name) RequireColumn(name, item.Span, columns);
        }
    }

    /// <summary>Колонки строки для лямбды в аргументах вызова; <c>null</c>, если это не функция строк.</summary>
    private IReadOnlyList<string>? RowColumns(CallExpr call, Expr? pipedSource)
    {
        ScriptFunction? function = NativeFunction(call);

        if (function == null || !string.Equals(function.Namespace, "table", StringComparison.Ordinal)) return null;

        if (!s_rowLambdas.Contains(function.Name)) return null;

        IReadOnlyList<string>? source = ColumnsOf(Argument(call, function, 0, pipedSource));

        // derive строит колонки по очереди, и лямбда следующей видит уже добавленные предыдущими.
        return function.Name == "derive" && source != null && RecordFieldNames(Argument(call, function, 1, pipedSource)) is { } added
            ? Union(source, added)
            : source;
    }

    private void RequireColumn(string name, TextSpan span, IReadOnlyList<string> columns)
    {
        if (columns.Contains(name)) return;

        IReadOnlyList<string> nearest = Suggestions.Nearest(name, columns);
        string list = string.Join(", ", columns);

        _diagnostics.Error(DiagnosticCodes.UnknownColumn, span,
            $"нет колонки '{name}'",
            nearest.Count > 0 ? $"возможно, имелось в виду '{nearest[0]}'; колонки: {list}" : $"колонки: {list}");
    }

    /// <summary>Функция модуля, которую вызывает выражение; <c>null</c> — вызов не функции модуля.</summary>
    /// <remarks>
    /// Имя без пространства — это функция <c>core</c>: так пишут <c>input("продажи")</c>, и без
    /// этой ветки колонки поданной хостом таблицы остались бы непроверенными.
    /// </remarks>
    private ScriptFunction? NativeFunction(CallExpr call)
    {
        if (call.Callee is NameExpr bare)
        {
            return TryLookup(bare.Name, out _) || _declared.ContainsKey(bare.Name)
                ? null
                : _registry.Find($"core.{bare.Name}");
        }

        if (call.Callee is not MemberExpr { Target: NameExpr root } member || TryLookup(root.Name, out _)) return null;

        string? ns = ResolveNamespace(root.Name);

        return ns == null ? null : _registry.Find($"{ns}.{member.Name}");
    }

    /// <summary>
    /// Выражение, переданное параметру с данным номером, с учётом входа конвейера и плейсхолдера.
    /// </summary>
    private static Expr? Argument(CallExpr call, ScriptFunction function, int index, Expr? pipedSource)
    {
        if (index < 0 || index >= function.Parameters.Count) return null;

        bool placeholder = call.Arguments.Any(argument => argument.IsPlaceholder || argument.Value is PlaceholderExpr);

        if (pipedSource != null && !placeholder && index == 0) return pipedSource;

        string name = function.Parameters[index].Name;
        int cursor = pipedSource != null && !placeholder ? 1 : 0;

        foreach (ArgumentNode argument in call.Arguments)
        {
            bool isPlaceholder = argument.IsPlaceholder || argument.Value is PlaceholderExpr;

            if (argument.Name != null)
            {
                if (string.Equals(argument.Name, name, StringComparison.Ordinal)) return isPlaceholder ? pipedSource : argument.Value;
                continue;
            }

            if (cursor == index) return isPlaceholder ? pipedSource : argument.Value;

            cursor++;
        }

        return null;
    }

    private static IReadOnlyList<string>? RecordFieldNames(Expr? expression)
    {
        if (expression is not RecordExpr record) return null;

        var names = new List<string>(record.Fields.Count);

        foreach (RecordFieldNode field in record.Fields)
        {
            if (field.IsSpread || field.Name == null) return null;

            names.Add(field.Name);
        }

        return names;
    }

    private static string? StringLiteral(Expr? expression) =>
        expression is LiteralExpr { Value.Type: ScriptType.Str } literal ? literal.Value.AsString() : null;

    private static IReadOnlyList<string>? StringList(Expr? expression)
    {
        if (expression is not ListExpr list) return null;

        var names = new List<string>(list.Items.Count);

        foreach (Expr item in list.Items)
        {
            if (StringLiteral(item) is not { } name) return null;

            names.Add(name);
        }

        return names;
    }

    private static List<string> Union(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        var union = new List<string>(first);

        foreach (string name in second)
        {
            if (!union.Contains(name)) union.Add(name);
        }

        return union;
    }
}
