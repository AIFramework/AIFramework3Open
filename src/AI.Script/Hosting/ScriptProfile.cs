using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;
using System.Text.Json.Nodes;

namespace AI.Script.Hosting;

/// <summary>
/// Профиль языка: закрытый набор конструкций и пространств имен для хоста, который обязан
/// заранее знать, что сделает скрипт.
/// </summary>
/// <remarks>
/// Язык целиком годится для расчетов, но не для плана действий на чужом компьютере: с
/// <c>while</c>, функциями и лямбдами итог прогона не показать человеку до исполнения. Профиль
/// оставляет линейный конечный скрипт: <c>let</c>, <c>if</c>, <c>try</c>, <c>emit</c>,
/// <c>assert</c>, <c>for</c> с потолком проходов и вызовы только своих пространств. Проверка
/// идет обходом дерева разбора до первого вызова и одинакова у сервера и у клиента.
/// </remarks>
public sealed class ScriptProfile
{
    /// <summary>
    /// Профиль планов для компьютера: пространство <c>pc</c>, 8 КБ текста, 20 проходов цикла,
    /// 5 минут без учета ожидания человека, служебные функции рядом с действиями машины.
    /// </summary>
    public static readonly ScriptProfile Pc1 = new("pc-1", ["pc"], 8 * 1024, 20, TimeSpan.FromMinutes(5),
    [
        new("has", "Есть ли действие на этой машине", Schema(("token", "string", true))),
        new("ask", "Спросить человека окном приложения и вернуть выбор", Schema(("question", "string", true), ("options", "array", false))),
        new("say", "Короткое уведомление человеку", Schema(("text", "string", true))),
        new("wait", "Пауза до 60 секунд", Schema(("seconds", "number", true))),
        new("wait_window", "Ждать окно программы до 60 секунд", Schema(("program", "string", true), ("seconds", "number", true))),
        new("asset", "Локальный путь файла из ответа сервера", Schema(("id", "string", true))),
        new("slot", "Значение метки из команды: подставляет компьютер", Schema(("id", "string", true))),
    ]);

    private static readonly IReadOnlySet<string> BareFunctions = new HashSet<string>(StringComparer.Ordinal) { "len", "range" };

    private static readonly ScriptModule Core = new("core", "Длина и диапазон для профилей без стандартной библиотеки", "1",
    [
        new ScriptFunction
        {
            Namespace = "core",
            Name = "len",
            Description = "Длина строки, списка, записи или диапазона",
            Parameters = [new ScriptParameter { Name = "value", Type = ScriptType.Any }],
            ReturnType = ScriptType.Num,
            Invoke = (arguments, _) => ValueTask.FromResult(ScriptValue.Num(Length(arguments[0]))),
        },
        new ScriptFunction
        {
            Namespace = "core",
            Name = "range",
            Description = "Диапазон от нуля до заданного числа",
            Parameters = [new ScriptParameter { Name = "count", Type = ScriptType.Num }],
            ReturnType = ScriptType.Range,
            Invoke = (arguments, _) => ValueTask.FromResult(ScriptValue.Range(new ScriptRange(0, arguments[0].AsNumber("граница"), 1))),
        },
    ]);

    /// <summary>Создает профиль.</summary>
    /// <param name="services">Служебные функции профиля: одна сигнатура у проверки на сервере и у исполнения на клиенте.</param>
    public ScriptProfile(string name, IReadOnlyCollection<string> namespaces, int maxChars, int loopPasses, TimeSpan timeout, IReadOnlyList<SchemaTool>? services = null)
    {
        Name = name;
        Services = services ?? [];
        Namespaces = new HashSet<string>(namespaces, StringComparer.Ordinal);
        MaxChars = maxChars;
        LoopPasses = loopPasses;
        Timeout = timeout;
    }

    /// <summary>Имя профиля, например <c>pc-1</c>.</summary>
    public string Name { get; }

    /// <summary>Пространства имен, чьи функции можно звать.</summary>
    public IReadOnlySet<string> Namespaces { get; }

    /// <summary>Потолок длины текста.</summary>
    public int MaxChars { get; }

    /// <summary>Потолок проходов одного цикла.</summary>
    public int LoopPasses { get; }

    /// <summary>Потолок времени прогона.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Служебные функции первого пространства профиля; хост дает им исполнение сам.</summary>
    public IReadOnlyList<SchemaTool> Services { get; }

    /// <summary>
    /// Хост профиля: модули хоста и две функции ядра, <c>len</c> и <c>range</c>.
    /// </summary>
    /// <remarks>Стандартная библиотека не подключается: ее пространства профиль все равно запрещает.</remarks>
    public ScriptHost Host(params IScriptModule[] modules)
    {
        var host = new ScriptHost().Use(Core);
        foreach (IScriptModule module in modules) host.Use(module);
        return host;
    }

    /// <summary>Параметры прогона с потолками профиля.</summary>
    public RunOptions Options() => new() { Limits = new ScriptLimits { LoopPasses = LoopPasses, Timeout = Timeout } };

    /// <summary>Проверка профиля и обычная проверка хоста одним итогом.</summary>
    public CheckResult Check(ScriptHost host, string source)
    {
        var own = Violations(source);
        if (own.Count > 0) return new CheckResult { Success = false, Diagnostics = own };

        return host.Check(source);
    }

    /// <summary>Нарушения профиля без обычной проверки хоста; пусто — профиль соблюден.</summary>
    public IReadOnlyList<Diagnostic> Violations(string source)
    {
        var text = new SourceText(source ?? string.Empty);
        var diagnostics = new DiagnosticBag(text);

        if (text.Length > MaxChars)
        {
            diagnostics.Error(DiagnosticCodes.ProfileTooLong, new TextSpan(0, 0),
                $"скрипт длиннее {MaxChars} знаков, а профиль {Name} больше не принимает", "разбейте задачу на несколько команд");
            return diagnostics.ToList();
        }

        ScriptUnit unit = new Parser(text, diagnostics).ParseUnit();
        if (diagnostics.HasErrors) return diagnostics.ToList();

        if (unit.Options != null) Forbid(diagnostics, unit.Options, "блок options");
        foreach (Stmt statement in unit.Statements) Visit(statement, diagnostics);

        return diagnostics.ToList();
    }

    private void Visit(Stmt statement, DiagnosticBag diagnostics)
    {
        switch (statement)
        {
            case LetStmt let:
                Visit(let.Value, diagnostics);
                break;
            case EmitStmt emit:
                Visit(emit.Value, diagnostics);
                break;
            case AssertStmt assert:
                Visit(assert.Condition, diagnostics);
                if (assert.Message != null) Visit(assert.Message, diagnostics);
                break;
            case ExpressionStmt expression:
                Visit(expression.Expression, diagnostics);
                break;
            case TryStmt attempt:
                Visit(attempt.Body, diagnostics);
                Visit(attempt.Handler, diagnostics);
                break;
            case ForStmt loop:
                Visit(loop.Iterable, diagnostics);
                if (loop.By != null) Visit(loop.By, diagnostics);
                Visit(loop.Body, diagnostics);
                break;
            default:
                Forbid(diagnostics, statement, StatementName(statement));
                break;
        }
    }

    private void Visit(Expr expression, DiagnosticBag diagnostics)
    {
        switch (expression)
        {
            case LiteralExpr or NameExpr:
                break;
            case InterpolationExpr interpolation:
                foreach (InterpolationPart part in interpolation.Parts)
                {
                    if (part.Expression != null) Visit(part.Expression, diagnostics);
                }

                break;
            case MemberExpr member:
                Visit(member.Target, diagnostics);
                break;
            case IndexExpr index:
                Visit(index.Target, diagnostics);
                foreach (IndexArgument argument in index.Arguments)
                {
                    if (argument.Value != null) Visit(argument.Value, diagnostics);
                }

                break;
            case CallExpr call:
                VisitCall(call, diagnostics);
                break;
            case UnaryExpr unary:
                Visit(unary.Operand, diagnostics);
                break;
            case BinaryExpr binary:
                Visit(binary.Left, diagnostics);
                Visit(binary.Right, diagnostics);
                break;
            case RangeExpr range:
                Visit(range.From, diagnostics);
                Visit(range.To, diagnostics);
                if (range.By != null) Visit(range.By, diagnostics);
                break;
            case IfExpr conditional:
                Visit(conditional.Condition, diagnostics);
                Visit(conditional.Then, diagnostics);
                if (conditional.Else != null) Visit(conditional.Else, diagnostics);
                break;
            case BlockExpr block:
                foreach (Stmt statement in block.Statements) Visit(statement, diagnostics);
                break;
            case ListExpr list:
                foreach (Expr item in list.Items) Visit(item, diagnostics);
                break;
            case RecordExpr record:
                foreach (RecordFieldNode field in record.Fields) Visit(field.Value, diagnostics);
                break;
            default:
                Forbid(diagnostics, expression, ExpressionName(expression));
                break;
        }
    }

    /// <summary>Звать можно функцию своего пространства или <c>len</c> и <c>range</c>; методы значений и чужие пространства нельзя.</summary>
    private void VisitCall(CallExpr call, DiagnosticBag diagnostics)
    {
        bool allowed = call.Callee switch
        {
            MemberExpr { Target: NameExpr ns } => Namespaces.Contains(ns.Name),
            NameExpr bare => BareFunctions.Contains(bare.Name),
            _ => false,
        };

        if (!allowed)
        {
            diagnostics.Error(DiagnosticCodes.ProfileForbidden, call.Callee.Span,
                $"профиль {Name} разрешает звать только функции пространств {string.Join(", ", Namespaces)}, len и range",
                "уберите вызов или замените его функцией из списка этой машины");
        }

        foreach (ArgumentNode argument in call.Arguments)
        {
            if (argument.Value != null) Visit(argument.Value, diagnostics);
        }
    }

    private void Forbid(DiagnosticBag diagnostics, SyntaxNode node, string what) =>
        diagnostics.Error(DiagnosticCodes.ProfileForbidden, node.Span, $"{what} запрещено профилем {Name}",
            "план должен быть линейным: let, if, try, emit, assert и for по списку");

    private static string StatementName(Stmt statement) => statement switch
    {
        SetStmt => "присваивание set",
        WhileStmt => "цикл while",
        BreakStmt => "break",
        ContinueStmt => "continue",
        FunctionDeclStmt { IsStage: true } => "объявление stage",
        FunctionDeclStmt => "объявление fn",
        UseStmt => "use",
        ReturnStmt => "return",
        ShowStmt => "show",
        _ => statement.GetType().Name,
    };

    private static string ExpressionName(Expr expression) => expression switch
    {
        LambdaExpr => "лямбда",
        PipeExpr => "конвейер |>",
        VectorExpr => "вектор",
        PlaceholderExpr => "заполнитель _",
        _ => expression.GetType().Name,
    };

    private static JsonObject Schema(params (string Name, string Type, bool Required)[] fields) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(fields.Select(f => KeyValuePair.Create(f.Name, (JsonNode?)new JsonObject { ["type"] = f.Type }))),
        ["required"] = new JsonArray([.. fields.Where(f => f.Required).Select(f => (JsonNode)f.Name)]),
    };

    private static double Length(ScriptValue value) => value.Type switch
    {
        ScriptType.Str => value.AsString().Length,
        ScriptType.List => value.AsList().Count,
        ScriptType.Record => value.AsRecord().Count,
        ScriptType.Range => value.AsRange().Count,
        _ => throw new ScriptError(DiagnosticCodes.BadOperand, $"у значения типа {value.Type.ToName()} нет длины"),
    };
}
