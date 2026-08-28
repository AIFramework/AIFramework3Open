using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Semantics;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;

namespace AI.Script.Runtime;

/// <summary>Как завершилось исполнение инструкции.</summary>
internal enum Flow
{
    /// <summary>Обычное завершение.</summary>
    Normal,

    /// <summary>Выход из цикла.</summary>
    Break,

    /// <summary>Переход к следующей итерации.</summary>
    Continue,

    /// <summary>Возврат из функции.</summary>
    Return,
}

/// <summary>Итог исполнения инструкции или блока.</summary>
internal readonly struct Completion
{
    /// <summary>Как завершилось исполнение.</summary>
    public Flow Flow { get; }

    /// <summary>Значение: результат последнего выражения либо возвращаемое.</summary>
    public ScriptValue Value { get; }

    /// <summary>Создаёт итог.</summary>
    public Completion(Flow flow, ScriptValue value)
    {
        Flow = flow;
        Value = value;
    }

    /// <summary>Обычное завершение без значения.</summary>
    public static readonly Completion None = new(Flow.Normal, ScriptValue.None);

    /// <summary>Обычное завершение со значением.</summary>
    public static Completion Ok(ScriptValue value) => new(Flow.Normal, value);
}

/// <summary>
/// Древесный интерпретатор AIScript.
/// </summary>
/// <remarks>
/// Обход дерева, а не компиляция: вся тяжёлая работа происходит внутри вызовов фреймворка, где
/// уже есть OpenBLAS и GPU, а цикл на миллион итераций в самом скрипте — ошибка
/// проектирования скрипта, а не повод писать генератор кода.
/// <para>
/// Управляющий поток (<c>break</c>, <c>continue</c>, <c>return</c>) передаётся возвращаемым
/// значением, а не исключением: <c>continue</c> в цикле по ста тысячам элементов — это сто
/// тысяч исключений, то есть секунды на ровном месте.
/// </para>
/// </remarks>
public sealed partial class Interpreter
{
    private readonly RunContext _context;
    private readonly FunctionRegistry _registry;
    private readonly DiagnosticBag _diagnostics;
    private readonly SourceText? _source;
    private readonly Dictionary<string, string> _aliases;

    private Scope _global = new();
    private int _depth;

    /// <summary>Создаёт интерпретатор прогона.</summary>
    public Interpreter(RunContext context, FunctionRegistry registry, DiagnosticBag diagnostics, SourceText? source = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _source = source;
        _aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        _context.Interpreter = this;
    }

    /// <summary>
    /// Создаёт интерпретатор ветви параллельного участка.
    /// </summary>
    /// <remarks>
    /// У ветви своя глубина вызовов и своя текущая стадия — это её личное состояние, и одно на
    /// всех превратило бы вложенность в счётчик гонок. Область имён и псевдонимы общие: ветвь
    /// обязана видеть те же имена, что и код, который её запустил. Прогон ветвь не
    /// перехватывает: <c>context.Interpreter</c> по-прежнему указывает на главный.
    /// </remarks>
    private Interpreter(Interpreter parent)
    {
        _context = parent._context;
        _registry = parent._registry;
        _diagnostics = parent._diagnostics;
        _source = parent._source;
        _aliases = parent._aliases;
        _global = parent._global;
        _depth = parent._depth;
        _currentStage = parent._currentStage;
    }

    /// <summary>Создаёт интерпретатор для ветви параллельного участка.</summary>
    private Interpreter Branch() => new(this);

    /// <summary>Глобальная область прогона.</summary>
    public Scope Global => _global;

    /// <summary>Исполняет разобранный скрипт.</summary>
    public async Task RunAsync(ScriptUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        _global = new Scope();

        foreach (var constant in ScriptConstants.All) _global.Declare(constant.Key, constant.Value);

        SeedHostData();
        Hoist(unit);

        foreach (Stmt statement in unit.Statements)
        {
            _context.Cancellation.ThrowIfCancellationRequested();

            Completion completion = await ExecuteAsync(statement, _global).ConfigureAwait(false);

            if (completion.Flow == Flow.Return)
            {
                throw new ScriptError(
                    DiagnosticCodes.ReturnOutsideFunction,
                    "'return' вне функции",
                    "значение из скрипта отдаётся через 'emit имя = значение'")
                {
                    Span = statement.Span,
                };
            }
        }
    }

    /// <summary>
    /// Кладёт в область данные, подготовленные вызывающим.
    /// </summary>
    /// <remarks>
    /// Имя, непригодное в качестве переменной, молча пропускается: имя данным даёт не автор
    /// скрипта, а тот, кто их подаёт, и ронять из-за этого прогон незачем.
    /// </remarks>
    private void SeedHostData()
    {
        if (_context.Options.Seeded == null) return;

        foreach (var pair in _context.Options.Seeded)
        {
            if (!IsIdentifier(pair.Key)) continue;

            _global.Declare(pair.Key, Marshaller.FromClr(pair.Value));
        }
    }

    private static bool IsIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }

        return true;
    }

    /// <summary>
    /// Заворачивает объявление в замыкание.
    /// </summary>
    /// <remarks>
    /// Отпечаток текста считается здесь, один раз на объявление: он входит в ключ кэша стадии,
    /// и правка тела обязана обесценить прежний результат. Без исходника (скрипт мог прийти
    /// разобранным) отпечаток пуст — и тогда кэш опирается только на имя, версии и аргументы.
    /// </remarks>
    private ScriptClosure Close(FunctionDeclStmt declaration, Scope scope) => new(
        declaration.Name,
        declaration.Parameters,
        declaration.Body,
        scope,
        declaration.IsStage,
        declaration.Documentation,
        declaration.IsStage ? StageOptions.From(declaration.Attributes) : null,
        declaration.IsStage ? DigestOf(declaration) : null);

    private string DigestOf(FunctionDeclStmt declaration) =>
        _source == null ? string.Empty : ValueDigest.Hash(_source.GetText(declaration.Span));

    /// <summary>
    /// Объявляет функции и псевдонимы до исполнения: они видны во всём файле.
    /// </summary>
    private void Hoist(ScriptUnit unit)
    {
        foreach (Stmt statement in unit.Statements)
        {
            switch (statement)
            {
                case FunctionDeclStmt declaration:
                    _global.Declare(declaration.Name, ScriptValue.Fn(Close(declaration, _global)));
                    break;

                case UseStmt use:
                    if (_registry.HasNamespace(use.Namespace)) _aliases[use.Alias] = use.Namespace;
                    break;
            }
        }
    }

    // --- инструкции ---

    private async ValueTask<Completion> ExecuteAsync(Stmt statement, Scope scope)
    {
        _context.CountStep();
        _context.Cancellation.ThrowIfCancellationRequested();

        try
        {
            return await ExecuteCoreAsync(statement, scope).ConfigureAwait(false);
        }
        catch (ScriptError error) when (error.Span == null)
        {
            error.Span = statement.Span;
            throw;
        }
    }

    private async ValueTask<Completion> ExecuteCoreAsync(Stmt statement, Scope scope)
    {
        switch (statement)
        {
            case LetStmt let:
                {
                    ScriptValue value = await EvaluateAsync(let.Value, scope).ConfigureAwait(false);

                    if (scope.DeclaredHere(let.Name))
                    {
                        throw new ScriptError(
                            DiagnosticCodes.DuplicateLet,
                            $"имя '{let.Name}' уже связано в этой области",
                            $"изменить значение можно через 'set {let.Name} = ...'");
                    }

                    scope.Declare(let.Name, ApplyDeclaredType(let.DeclaredType, value, let.Name));

                    return Completion.None;
                }

            case SetStmt set:
                await ExecuteSetAsync(set, scope).ConfigureAwait(false);
                return Completion.None;

            case FunctionDeclStmt declaration:
                // Объявления верхнего уровня уже подняты; вложенных не бывает (проверяет парсер).
                if (!scope.DeclaredHere(declaration.Name))
                    scope.Declare(declaration.Name, ScriptValue.Fn(Close(declaration, scope)));

                return Completion.None;

            case ExpressionStmt { Expression: IfExpr conditional }:
                return await ExecuteIfAsync(conditional, scope).ConfigureAwait(false);

            case ExpressionStmt { Expression: BlockExpr block }:
                return await ExecuteBlockAsync(block, new Scope(scope)).ConfigureAwait(false);

            case ExpressionStmt expression:
                return Completion.Ok(await EvaluateAsync(expression.Expression, scope).ConfigureAwait(false));

            case ForStmt loop:
                return await ExecuteForAsync(loop, scope).ConfigureAwait(false);

            case WhileStmt loop:
                return await ExecuteWhileAsync(loop, scope).ConfigureAwait(false);

            case BreakStmt:
                return new Completion(Flow.Break, ScriptValue.None);

            case ContinueStmt:
                return new Completion(Flow.Continue, ScriptValue.None);

            case ReturnStmt returnStatement:
                {
                    ScriptValue value = returnStatement.Value == null
                        ? ScriptValue.None
                        : await EvaluateAsync(returnStatement.Value, scope).ConfigureAwait(false);

                    return new Completion(Flow.Return, value);
                }

            case TryStmt tryStatement:
                return await ExecuteTryAsync(tryStatement, scope).ConfigureAwait(false);

            case EmitStmt emit:
                await ExecuteEmitAsync(emit, scope).ConfigureAwait(false);
                return Completion.None;

            case ShowStmt show:
                _context.Show(await EvaluateAsync(show.Value, scope).ConfigureAwait(false));
                return Completion.None;

            case AssertStmt assert:
                await ExecuteAssertAsync(assert, scope).ConfigureAwait(false);
                return Completion.None;

            case UseStmt or OptionsStmt:
                return Completion.None;

            default:
                return Completion.None;
        }
    }

    /// <summary>
    /// Сверяет значение с объявленным типом и, где нужно, приводит его.
    /// </summary>
    /// <remarks>
    /// Аннотация типа обязана что-то менять, иначе она — комментарий. Список чисел там, где
    /// объявлен <c>vec</c>, становится вектором; это единственное неявное приведение языка, и
    /// оно есть потому, что <c>[1, 2, 3]</c> пишется чаще, чем <c>&lt;1, 2, 3&gt;</c>.
    /// </remarks>
    private static ScriptValue ApplyDeclaredType(ScriptType? declared, ScriptValue value, string name)
    {
        if (declared == null || declared == ScriptType.Any) return value;
        if (declared == value.Type) return value;

        if (declared == ScriptType.Vec && value.Type is ScriptType.List or ScriptType.Range)
            return Marshaller.FromClr(Marshaller.ToClr(value, typeof(Vector), $"'{name}'"));

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"'{name}': объявлен тип {declared.Value.ToName()}, а значение имеет тип {value.Type.ToName()}");
    }

    private async ValueTask<Completion> ExecuteBlockAsync(BlockExpr block, Scope scope)
    {
        ScriptValue last = ScriptValue.None;

        foreach (Stmt statement in block.Statements)
        {
            Completion completion = await ExecuteAsync(statement, scope).ConfigureAwait(false);

            if (completion.Flow != Flow.Normal) return completion;

            last = completion.Value;
        }

        return Completion.Ok(last);
    }

    private async ValueTask<Completion> ExecuteIfAsync(IfExpr conditional, Scope scope)
    {
        bool condition = (await EvaluateAsync(conditional.Condition, scope).ConfigureAwait(false))
            .AsBool("условие 'if'");

        if (condition) return await ExecuteBlockAsync(conditional.Then, new Scope(scope)).ConfigureAwait(false);

        return conditional.Else switch
        {
            null => Completion.None,
            IfExpr nested => await ExecuteIfAsync(nested, scope).ConfigureAwait(false),
            BlockExpr block => await ExecuteBlockAsync(block, new Scope(scope)).ConfigureAwait(false),
            _ => Completion.Ok(await EvaluateAsync(conditional.Else, scope).ConfigureAwait(false)),
        };
    }

    private async ValueTask<Completion> ExecuteForAsync(ForStmt loop, Scope scope)
    {
        ScriptValue iterable = await EvaluateAsync(loop.Iterable, scope).ConfigureAwait(false);

        double? step = null;

        if (loop.By != null)
        {
            step = (await EvaluateAsync(loop.By, scope).ConfigureAwait(false)).AsNumber("шаг цикла 'by'");

            if (iterable.Type != ScriptType.Range)
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"'by' применимо только к диапазону, а здесь {iterable.Type.ToName()}",
                    "для списка используйте core.filter либо индексы: for i in 0..len(xs) by 2");
            }
        }

        foreach (ScriptValue item in Iterate(iterable, step))
        {
            _context.Cancellation.ThrowIfCancellationRequested();
            _context.CountStep();

            var iterationScope = new Scope(scope);
            BindLoopVariables(loop, item, iterationScope);

            Completion completion = await ExecuteBlockAsync(loop.Body, iterationScope).ConfigureAwait(false);

            if (completion.Flow == Flow.Break) break;
            if (completion.Flow == Flow.Return) return completion;
        }

        return Completion.None;
    }

    private static void BindLoopVariables(ForStmt loop, ScriptValue item, Scope scope)
    {
        if (loop.Names.Count == 1)
        {
            scope.Declare(loop.Names[0], item);
            return;
        }

        if (item.Type != ScriptType.List || item.AsList().Count != loop.Names.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"для разбора на {loop.Names.Count} имени нужен список такой же длины, а получен {item.Type.ToName()}",
                "пары даёт core.pairs(запись)");
        }

        ScriptList parts = item.AsList();

        for (int i = 0; i < loop.Names.Count; i++) scope.Declare(loop.Names[i], parts[i]);
    }

    private static IEnumerable<ScriptValue> Iterate(ScriptValue value, double? step)
    {
        switch (value.Type)
        {
            case ScriptType.List:
                {
                    ScriptList list = value.AsList();
                    for (int i = 0; i < list.Count; i++) yield return list[i];
                    break;
                }

            case ScriptType.Vec:
                {
                    var vector = value.AsVector();
                    for (int i = 0; i < vector.Count; i++) yield return ScriptValue.Num(vector[i]);
                    break;
                }

            case ScriptType.Range:
                {
                    ScriptRange range = value.AsRange();
                    if (step != null) range = new ScriptRange(range.Start, range.End, step.Value);

                    foreach (double number in range.Values()) yield return ScriptValue.Num(number);
                    break;
                }

            case ScriptType.Table:
                {
                    // По таблице ходят строками-записями: 'for row in t { row.amount }' — это
                    // ровно то, чего от неё ждут, и другого осмысленного обхода у неё нет.
                    foreach (ScriptRecord row in value.AsTable().Rows()) yield return ScriptValue.Record(row);
                    break;
                }

            case ScriptType.Str:
                {
                    string text = value.AsString();
                    foreach (char c in text) yield return ScriptValue.Str(c.ToString());
                    break;
                }

            default:
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"по значению типа {value.Type.ToName()} нельзя пройти циклом",
                    "перебираются список, вектор, диапазон и строка; для записи используйте core.pairs(x)");
        }
    }

    private async ValueTask<Completion> ExecuteWhileAsync(WhileStmt loop, Scope scope)
    {
        while (true)
        {
            _context.Cancellation.ThrowIfCancellationRequested();
            _context.CountStep();

            bool condition = (await EvaluateAsync(loop.Condition, scope).ConfigureAwait(false))
                .AsBool("условие 'while'");

            if (!condition) break;

            Completion completion = await ExecuteBlockAsync(loop.Body, new Scope(scope)).ConfigureAwait(false);

            if (completion.Flow == Flow.Break) break;
            if (completion.Flow == Flow.Return) return completion;
        }

        return Completion.None;
    }

    /// <summary>
    /// Перехват отказа.
    /// </summary>
    /// <remarks>
    /// <see cref="ScriptAbort"/> сюда не попадает намеренно: иначе скрипт мог бы поймать
    /// собственный таймаут и продолжить работу, и лимит перестал бы быть лимитом.
    /// </remarks>
    private async ValueTask<Completion> ExecuteTryAsync(TryStmt statement, Scope scope)
    {
        try
        {
            return await ExecuteBlockAsync(statement.Body, new Scope(scope)).ConfigureAwait(false);
        }
        catch (ScriptError error) when (error is not ScriptAbort)
        {
            var handlerScope = new Scope(scope);
            handlerScope.Declare(statement.ErrorName, DescribeError(error));

            return await ExecuteBlockAsync(statement.Handler, handlerScope).ConfigureAwait(false);
        }
    }

    private ScriptValue DescribeError(ScriptError error)
    {
        var fields = new List<KeyValuePair<string, ScriptValue>>
        {
            new("code", ScriptValue.Str(error.Code)),
            new("message", ScriptValue.Str(error.Message)),
            new("where", ScriptValue.Str(Where(error.Span))),
        };

        return ScriptValue.Record(ScriptRecord.From(fields));
    }

    private string Where(TextSpan? span)
    {
        if (span == null || _source == null) return string.Empty;

        LinePosition position = _source.GetLinePosition(span.Value.Start);

        return $"строка {position.Line}";
    }

    private async ValueTask ExecuteEmitAsync(EmitStmt emit, Scope scope)
    {
        ScriptValue value = await EvaluateAsync(emit.Value, scope).ConfigureAwait(false);

        if (value.Type == ScriptType.Handle)
        {
            _diagnostics.Warning(
                DiagnosticCodes.NotImplementedYet, emit.Span,
                $"'{emit.Name}': дескриптор не переносится в результат прогона",
                "сохраните объект функцией модуля либо отдайте наружу его числовые характеристики");
        }

        if (_context.Emitted.ContainsKey(emit.Name))
        {
            _diagnostics.Warning(
                DiagnosticCodes.NotImplementedYet, emit.NameSpan,
                $"результат '{emit.Name}' объявлен повторно: прежнее значение потеряно");
        }

        _context.Emitted[emit.Name] = Marshaller.Unwrap(value);
    }

    /// <summary>
    /// Проверка инварианта.
    /// </summary>
    /// <remarks>
    /// Для сравнения операнды вычисляются по отдельности, чтобы положить в сообщение
    /// фактические значения. Отказ «assert не выполнен» без чисел заставляет автора скрипта
    /// печатать их вручную и запускать заново — то есть делать работу, которую можно сделать
    /// один раз здесь.
    /// </remarks>
    private async ValueTask ExecuteAssertAsync(AssertStmt assert, Scope scope)
    {
        string? detail = null;
        bool ok;

        if (assert.Condition is BinaryExpr binary && OperatorText.IsComparison(binary.Operator))
        {
            ScriptValue left = await EvaluateAsync(binary.Left, scope).ConfigureAwait(false);
            ScriptValue right = await EvaluateAsync(binary.Right, scope).ConfigureAwait(false);

            ok = Operations.Binary(binary.Operator, left, right).AsBool("условие 'assert'");
            detail = $"слева: {ScriptFormatter.Format(left, quoteStrings: true)}, " +
                     $"справа: {ScriptFormatter.Format(right, quoteStrings: true)}";
        }
        else
        {
            ok = (await EvaluateAsync(assert.Condition, scope).ConfigureAwait(false)).AsBool("условие 'assert'");
        }

        if (ok) return;

        string message = assert.Message == null
            ? "инвариант не выполнен"
            : (await EvaluateAsync(assert.Message, scope).ConfigureAwait(false)).AsString("пояснение 'assert'");

        throw new ScriptError(DiagnosticCodes.AssertionFailed, message, detail) { Span = assert.Span };
    }
}
