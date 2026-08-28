using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;

namespace AI.Script.Semantics;

/// <summary>
/// Проверка скрипта до запуска: имена, функции, аргументы, области видимости и типы.
/// </summary>
/// <remarks>
/// Это главный ответ на вопрос «почему язык, а не просто вызовы инструментов»: опечатка в
/// имени аргумента или несовпадение типов обнаруживается за миллисекунды и до первого
/// вычисления, а не через двадцать минут обучения модели на последней строке скрипта.
/// Стоимость ошибки, а не вкус, и делает проверку обязательной частью языка.
/// <para>
/// Вывод типов частичный и намеренно осторожный: неизвестный тип (<c>null</c>) не порождает
/// диагностик. Проверка, которая ошибается, хуже отсутствующей — ей перестают верить и
/// начинают обходить.
/// </para>
/// </remarks>
public sealed class Checker
{
    private readonly DiagnosticBag _diagnostics;
    private readonly FunctionRegistry _registry;
    private readonly Dictionary<string, FunctionDeclStmt> _declared = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);
    private readonly List<Dictionary<string, ScriptType?>> _scopes = [];
    private readonly IReadOnlyCollection<string> _seeded;

    private int _loopDepth;
    private int _functionDepth;
    private bool _inStage;
    private ScriptType? _returnType;

    /// <summary>Глубина вложенности в аргументы вызова с <c>parallel: true</c>.</summary>
    private int _parallelDepth;

    /// <summary>Обращалась ли проверяемая сейчас стадия к файлам.</summary>
    private bool _stageUsesFiles;

    /// <summary>
    /// Номер области, начиная с которой имена принадлежат самой параллельной лямбде.
    /// </summary>
    /// <remarks>
    /// Ноль — параллельного участка нет. Всё, что связано ниже этой границы, для ветви внешнее:
    /// присваивать туда из нескольких потоков — гонка, и её ловит проверка, а не отладчик.
    /// </remarks>
    private int _parallelFloor;

    /// <summary>Создаёт проверяющий проход.</summary>
    /// <param name="diagnostics">Накопитель диагностик.</param>
    /// <param name="registry">Реестр функций.</param>
    /// <param name="seeded">
    /// Имена данных, поданных хостом: скрипт видит их связанными, и проверка обязана знать о
    /// них, иначе корректный скрипт не пройдёт её из-за имени, которого нет в исходнике.
    /// </param>
    public Checker(DiagnosticBag diagnostics, FunctionRegistry registry, IReadOnlyCollection<string>? seeded = null)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _seeded = seeded ?? [];
    }

    /// <summary>Проверяет разобранный скрипт.</summary>
    public void Check(ScriptUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        CollectDeclarations(unit);

        var roots = new Dictionary<string, ScriptType?>(StringComparer.Ordinal);

        foreach (string constant in ScriptConstants.All.Keys) roots[constant] = ScriptType.Num;
        foreach (string name in _seeded) roots[name] = null;

        _scopes.Add(roots);

        foreach (Stmt statement in unit.Statements) CheckStatement(statement);

        _scopes.Clear();
    }

    private void CollectDeclarations(ScriptUnit unit)
    {
        foreach (Stmt statement in unit.Statements)
        {
            switch (statement)
            {
                case FunctionDeclStmt declaration:
                    if (!_declared.TryAdd(declaration.Name, declaration))
                    {
                        _diagnostics.Error(DiagnosticCodes.DuplicateFunction, declaration.NameSpan,
                            $"функция '{declaration.Name}' уже объявлена в этом файле",
                            "имена функций уникальны: переименуйте одну из них");
                    }

                    CheckAttributes(declaration);
                    break;

                case UseStmt use:
                    if (!_registry.HasNamespace(use.Namespace))
                    {
                        _diagnostics.Error(DiagnosticCodes.UnknownNamespace, use.Span,
                            $"неизвестное пространство имён '{use.Namespace}'",
                            NamespaceHint(use.Namespace));
                        break;
                    }

                    _aliases[use.Alias] = use.Namespace;
                    break;
            }
        }
    }

    /// <summary>
    /// Проверяет атрибуты объявления.
    /// </summary>
    /// <remarks>
    /// Аргументы атрибутов обязаны быть литералами: у атрибута нет области видимости, в которой
    /// можно было бы вычислить выражение, и <c>@retry(n)</c> с переменной <c>n</c> означал бы,
    /// что число попыток зависит от места вызова, — а атрибут стоит при объявлении.
    /// </remarks>
    private void CheckAttributes(FunctionDeclStmt declaration)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool cache = false;
        bool noCache = false;

        foreach (AttributeNode attribute in declaration.Attributes)
        {
            if (!declaration.IsStage)
            {
                _diagnostics.Error(DiagnosticCodes.UnexpectedToken, attribute.Span,
                    $"атрибут '@{attribute.Name}' применим только к стадии",
                    "перезапуск и кэш нечистой функции лишены смысла: объявите её через 'stage'");
                continue;
            }

            if (!seen.Add(attribute.Name))
            {
                _diagnostics.Warning(DiagnosticCodes.DuplicateArgument, attribute.Span,
                    $"атрибут '@{attribute.Name}' указан дважды",
                    "повтор ничего не меняет: уберите лишний");
            }

            switch (attribute.Name)
            {
                case "cache":
                    cache = true;
                    RequireNoArguments(attribute);
                    break;

                case "nocache":
                    noCache = true;
                    RequireNoArguments(attribute);
                    break;

                case "pure":
                    RequireNoArguments(attribute);
                    break;

                case "retry":
                    CheckRetry(attribute);
                    break;

                case "timeout":
                    CheckTimeout(attribute);
                    break;

                case "deprecated":
                    if (LiteralOf(attribute) is not { Type: ScriptType.Str })
                    {
                        _diagnostics.Error(DiagnosticCodes.TypeMismatch, attribute.Span,
                            "'@deprecated' принимает строку с причиной",
                            "например: @deprecated(\"вместо неё используйте features2\")");
                    }

                    break;

                default:
                    _diagnostics.Error(DiagnosticCodes.UnexpectedToken, attribute.Span,
                        $"неизвестный атрибут '@{attribute.Name}'",
                        "известны: @cache, @nocache, @retry(n), @timeout(d), @pure, @deprecated(\"...\")");
                    break;
            }
        }

        if (cache && noCache)
        {
            _diagnostics.Warning(DiagnosticCodes.BadOperandTypes, declaration.NameSpan,
                $"у стадии '{declaration.Name}' указаны и '@cache', и '@nocache'",
                "запрет сильнее разрешения: результат кэшироваться не будет");
        }
    }

    private void RequireNoArguments(AttributeNode attribute)
    {
        if (attribute.Arguments.Count == 0) return;

        _diagnostics.Error(DiagnosticCodes.ExtraPositional, attribute.Span,
            $"'@{attribute.Name}' не принимает аргументов");
    }

    private void CheckRetry(AttributeNode attribute)
    {
        if (LiteralOf(attribute) is not { Type: ScriptType.Num } count)
        {
            _diagnostics.Error(DiagnosticCodes.TypeMismatch, attribute.Span,
                "'@retry' принимает целое число попыток",
                "например: @retry(3) — до трёх попыток");

            return;
        }

        if (count.RawNumber >= 1 && count.RawNumber == Math.Floor(count.RawNumber)) return;

        _diagnostics.Error(DiagnosticCodes.BadOperand, attribute.Span,
            "число попыток в '@retry' — целое, не меньше единицы",
            "@retry(1) означает «без повторов»");
    }

    private void CheckTimeout(AttributeNode attribute)
    {
        if (LiteralOf(attribute) is not { } value || value.Type is not (ScriptType.Dur or ScriptType.Num))
        {
            _diagnostics.Error(DiagnosticCodes.TypeMismatch, attribute.Span,
                "'@timeout' принимает длительность",
                "например: @timeout(90s) либо @timeout(5m)");

            return;
        }

        bool positive = value.Type == ScriptType.Dur
            ? value.AsDuration("@timeout") > TimeSpan.Zero
            : value.RawNumber > 0;

        if (positive) return;

        _diagnostics.Error(DiagnosticCodes.BadOperand, attribute.Span,
            "длительность в '@timeout' должна быть положительной");
    }

    private static ScriptValue? LiteralOf(AttributeNode attribute) =>
        attribute.Arguments.Count == 1 && attribute.Arguments[0] is LiteralExpr literal
            ? literal.Value
            : null;

    // --- инструкции ---

    private void CheckStatement(Stmt statement)
    {
        switch (statement)
        {
            case LetStmt let:
                CheckLet(let);
                break;

            case SetStmt set:
                CheckSet(set);
                break;

            case FunctionDeclStmt declaration:
                CheckFunctionBody(declaration);
                break;

            case ForStmt loop:
                CheckFor(loop);
                break;

            case WhileStmt loop:
                RequireBool(CheckExpression(loop.Condition), loop.Condition, "условие 'while'");
                _loopDepth++;
                _ = CheckBlock(loop.Body, ownScope: true);
                _loopDepth--;
                break;

            case BreakStmt or ContinueStmt:
                if (_loopDepth == 0)
                {
                    _diagnostics.Error(DiagnosticCodes.NotInLoop, statement.Span,
                        statement is BreakStmt ? "'break' вне цикла" : "'continue' вне цикла");
                }

                break;

            case ReturnStmt returnStatement:
                CheckReturn(returnStatement);
                break;

            case TryStmt tryStatement:
                _ = CheckBlock(tryStatement.Body, ownScope: true);
                PushScope();
                DeclareName(tryStatement.ErrorName, tryStatement.Span, ScriptType.Record, warnShadowing: false);
                _ = CheckBlock(tryStatement.Handler, ownScope: false);
                PopScope();
                break;

            case EmitStmt emit:
                _ = CheckExpression(emit.Value);

                if (_inStage)
                {
                    _diagnostics.Error(DiagnosticCodes.UnboundName, emit.Span,
                        "стадия не может делать 'emit'",
                        "стадия должна быть чистой: верните значение и объявите результат снаружи");
                }

                break;

            case ShowStmt show:
                _ = CheckExpression(show.Value);
                break;

            case AssertStmt assert:
                RequireBool(CheckExpression(assert.Condition), assert.Condition, "условие 'assert'");
                if (assert.Message != null) _ = CheckExpression(assert.Message);
                break;

            case ExpressionStmt expression:
                _ = CheckExpression(expression.Expression);
                break;

            case UseStmt or OptionsStmt:
                break;
        }
    }

    private void CheckLet(LetStmt let)
    {
        ScriptType? valueType = CheckExpression(let.Value);

        if (let.DeclaredType is ScriptType declared && valueType is ScriptType actual
            && !TypeRules.Accepts(declared, actual))
        {
            _diagnostics.Error(DiagnosticCodes.DeclaredTypeMismatch, let.Span,
                $"'{let.Name}': объявлен тип {declared.ToName()}, а значение имеет тип {actual.ToName()}");
        }

        DeclareName(let.Name, let.NameSpan, let.DeclaredType ?? valueType);
    }

    /// <summary>
    /// Сверяет обещания стадии с её телом.
    /// </summary>
    /// <remarks>
    /// Файл — это состояние, которого нет в ключе кэша: закэшированная стадия, читающая CSV,
    /// вернёт вчерашнюю таблицу и после того, как файл переписали. Молчать об этом нельзя, а
    /// запрещать — тоже: чтение файла стадией законно, если кэш ей не нужен. Поэтому
    /// <c>@cache</c> с чтением файлов даёт предупреждение, а <c>@pure</c> — ошибку: это уже
    /// прямое обещание автора, что стадия ни от чего снаружи не зависит.
    /// </remarks>
    private void CheckStagePurity(FunctionDeclStmt declaration)
    {
        if (!_stageUsesFiles) return;

        bool pure = false;
        bool cache = false;

        foreach (AttributeNode attribute in declaration.Attributes)
        {
            if (string.Equals(attribute.Name, "pure", StringComparison.Ordinal)) pure = true;
            if (string.Equals(attribute.Name, "cache", StringComparison.Ordinal)) cache = true;
        }

        if (pure)
        {
            _diagnostics.Error(DiagnosticCodes.SandboxDenied, declaration.NameSpan,
                $"стадия '{declaration.Name}' помечена '@pure', но обращается к файлам",
                "уберите '@pure' либо вынесите чтение наружу и передайте данные аргументом");

            return;
        }

        if (!cache) return;

        _diagnostics.Warning(DiagnosticCodes.SandboxDenied, declaration.NameSpan,
            $"стадия '{declaration.Name}' кэшируется, но читает файлы",
            "содержимое файла в ключ кэша не входит: после правки файла вернётся прежний " +
            "результат. Читайте файл снаружи и передавайте данные аргументом либо уберите '@cache'");
    }

    /// <summary>
    /// Предупреждает о вызове устаревшей стадии.
    /// </summary>
    /// <remarks>
    /// Предупреждение стоит на месте вызова, а не объявления: автор скрипта смотрит туда, где
    /// написал вызов, и метка при объявлении, которую он однажды прочитал, его не остановит.
    /// </remarks>
    private void WarnIfDeprecated(FunctionDeclStmt declaration, TextSpan span)
    {
        foreach (AttributeNode attribute in declaration.Attributes)
        {
            if (!string.Equals(attribute.Name, "deprecated", StringComparison.Ordinal)) continue;
            if (LiteralOf(attribute) is not { Type: ScriptType.Str } reason) continue;

            _diagnostics.Warning(DiagnosticCodes.NotImplementedYet, span,
                $"стадия '{declaration.Name}' помечена устаревшей",
                reason.AsString("@deprecated"));

            return;
        }
    }

    /// <summary>Есть ли у вызова аргумент <c>parallel: true</c>.</summary>
    private static bool IsParallelCall(CallExpr call)
    {
        foreach (ArgumentNode argument in call.Arguments)
        {
            if (!string.Equals(argument.Name, "parallel", StringComparison.Ordinal)) continue;

            return argument.Value is LiteralExpr { Value.Type: ScriptType.Bool } literal
                && literal.Value.RawNumber != 0;
        }

        return false;
    }

    /// <summary>
    /// Запрещает параллельной лямбде писать во внешнее имя.
    /// </summary>
    /// <remarks>
    /// Ветви исполняются одновременно, поэтому такое присваивание — гонка, и результат зависел
    /// бы от того, какая ветвь успела последней. Ловится до запуска: гонку, которая проявляется
    /// раз в сто прогонов, ищут неделями.
    /// </remarks>
    private void RequirePureInParallel(NameExpr name)
    {
        if (_parallelFloor == 0) return;

        for (int i = _scopes.Count - 1; i >= _parallelFloor; i--)
        {
            if (_scopes[i].ContainsKey(name.Name)) return;
        }

        _diagnostics.Error(DiagnosticCodes.UnboundSet, name.Span,
            $"параллельная лямбда не может присваивать внешнему имени '{name.Name}'",
            "ветви исполняются одновременно, и такое присваивание — гонка: верните значение " +
            "из лямбды и соберите результат снаружи");
    }

    private void CheckSet(SetStmt set)
    {
        ScriptType? valueType = CheckExpression(set.Value);

        switch (set.Target)
        {
            case NameExpr name:
                if (!TryLookup(name.Name, out ScriptType? current))
                {
                    _diagnostics.Error(DiagnosticCodes.UnboundSet, name.Span,
                        $"имя '{name.Name}' не связано",
                        $"новое имя вводится через 'let {name.Name} = ...'");

                    return;
                }

                RequirePureInParallel(name);

                if (set.Compound is BinaryOperator op && current is ScriptType left && valueType is ScriptType right)
                {
                    if (TypeRules.Binary(op, left, right) == null)
                    {
                        _diagnostics.Error(DiagnosticCodes.BadOperandTypes, set.Span,
                            $"оператор '{OperatorText.Of(op)}=' не определён для типов {left.ToName()} и {right.ToName()}");
                    }

                    return;
                }

                // Тип имени после присваивания больше не известен точно: дальше по тексту
                // он мог стать любым, и утверждать прежний — значит врать проверке.
                Assign(name.Name, set.Compound == null ? valueType : current);
                break;

            case IndexExpr index:
                _ = CheckExpression(index.Target);
                foreach (IndexArgument argument in index.Arguments)
                {
                    if (argument.Value != null) _ = CheckExpression(argument.Value);
                }

                break;

            case MemberExpr member:
                _ = CheckExpression(member.Target);
                break;
        }
    }

    private void CheckFor(ForStmt loop)
    {
        ScriptType? sequence = CheckExpression(loop.Iterable);

        if (loop.By != null) _ = CheckExpression(loop.By);

        if (sequence is ScriptType known && !TypeRules.IsIterable(known))
        {
            _diagnostics.Error(DiagnosticCodes.NotIterable, loop.Iterable.Span,
                $"по значению типа {known.ToName()} нельзя пройти циклом",
                "перебираются список, вектор, диапазон, строка и таблица; для записи используйте core.pairs(x)");
        }

        ScriptType? element = sequence is ScriptType type ? TypeRules.ElementOf(type) : null;

        PushScope();

        foreach (string name in loop.Names)
            DeclareName(name, loop.Span, loop.Names.Count == 1 ? element : null, warnShadowing: false);

        _loopDepth++;
        _ = CheckBlock(loop.Body, ownScope: false);
        _loopDepth--;

        PopScope();
    }

    private void CheckReturn(ReturnStmt statement)
    {
        ScriptType? valueType = statement.Value == null ? ScriptType.None : CheckExpression(statement.Value);

        if (_functionDepth == 0)
        {
            _diagnostics.Error(DiagnosticCodes.ReturnOutsideFunction, statement.Span,
                "'return' вне функции",
                "значение из скрипта отдаётся через 'emit имя = значение'");

            return;
        }

        if (_returnType is ScriptType declared && valueType is ScriptType actual
            && !TypeRules.Accepts(declared, actual))
        {
            _diagnostics.Error(DiagnosticCodes.DeclaredTypeMismatch, statement.Span,
                $"объявлен результат типа {declared.ToName()}, а возвращается {actual.ToName()}");
        }
    }

    private void CheckFunctionBody(FunctionDeclStmt declaration)
    {
        // Стадия обязана быть чистой: она видит только свои параметры и другие объявления
        // файла. Иначе кэшировать её по аргументам было бы неверно — результат зависел бы от
        // того, что лежало снаружи в момент вызова.
        List<Dictionary<string, ScriptType?>> saved = [.. _scopes];

        if (declaration.IsStage)
        {
            _scopes.Clear();
            _scopes.Add(new Dictionary<string, ScriptType?>(StringComparer.Ordinal));
        }

        PushScope();

        foreach (ParameterNode parameter in declaration.Parameters)
        {
            if (parameter.Default != null) _ = CheckExpression(parameter.Default);
            DeclareName(parameter.Name, parameter.Span, parameter.DeclaredType, warnShadowing: false);
        }

        _functionDepth++;
        bool previousStage = _inStage;
        bool previousFiles = _stageUsesFiles;
        ScriptType? previousReturn = _returnType;

        _inStage = declaration.IsStage;
        _stageUsesFiles = false;
        _returnType = declaration.ReturnType;

        ScriptType? bodyType = CheckBlock(declaration.Body, ownScope: false);

        if (declaration.IsStage) CheckStagePurity(declaration);

        _stageUsesFiles = previousFiles;

        if (_returnType is ScriptType expected && bodyType is ScriptType actual
            && !TypeRules.Accepts(expected, actual))
        {
            _diagnostics.Error(DiagnosticCodes.DeclaredTypeMismatch, declaration.Body.Span,
                $"'{declaration.Name}': объявлен результат типа {expected.ToName()}, а тело даёт {actual.ToName()}");
        }

        _returnType = previousReturn;
        _inStage = previousStage;
        _functionDepth--;
        PopScope();

        if (!declaration.IsStage) return;

        _scopes.Clear();
        _scopes.AddRange(saved);
    }

    /// <summary>Проверяет блок и возвращает тип его значения — типа последнего выражения.</summary>
    private ScriptType? CheckBlock(BlockExpr block, bool ownScope)
    {
        if (ownScope) PushScope();

        ScriptType? last = ScriptType.None;

        foreach (Stmt statement in block.Statements)
        {
            CheckStatement(statement);

            last = statement is ExpressionStmt expression ? TypeOfChecked(expression.Expression) : ScriptType.None;
        }

        if (ownScope) PopScope();

        return last;
    }

    /// <summary>
    /// Тип уже проверенного выражения.
    /// </summary>
    /// <remarks>
    /// Значение блока нужно знать после того, как инструкции уже пройдены. Повторный проход
    /// по выражению выдал бы вторую копию каждой диагностики, поэтому тип берётся отдельным
    /// проходом без побочных эффектов.
    /// </remarks>
    private ScriptType? TypeOfChecked(Expr expression) => InferQuietly(expression);

    // --- выражения ---

    private ScriptType? CheckExpression(Expr expression)
    {
        switch (expression)
        {
            case LiteralExpr literal:
                return literal.Value.Type;

            case PlaceholderExpr:
                return null;

            case InterpolationExpr interpolation:
                foreach (InterpolationPart part in interpolation.Parts)
                {
                    if (part.Expression != null) _ = CheckExpression(part.Expression);
                }

                return ScriptType.Str;

            case NameExpr name:
                return CheckName(name);

            case MemberExpr member:
                return CheckMember(member, asCallee: false);

            case IndexExpr index:
                return CheckIndex(index);

            case CallExpr call:
                return CheckCall(call, pipedType: null, piped: false);

            case PipeExpr pipe:
                {
                    ScriptType? left = CheckExpression(pipe.Left);
                    return CheckCall(pipe.Right, left, piped: true);
                }

            case UnaryExpr unary:
                return CheckUnary(unary);

            case BinaryExpr binary:
                return CheckBinary(binary);

            case RangeExpr range:
                _ = CheckExpression(range.From);
                _ = CheckExpression(range.To);
                if (range.By != null) _ = CheckExpression(range.By);
                return ScriptType.Range;

            case LambdaExpr lambda:
                {
                    PushScope();

                    // На параллельном участке телом лямбды становится отдельная ветвь, поэтому
                    // изнутри неё не видно, где именно проходит граница «своего»: пол области
                    // запоминается здесь и проверяется в 'set'.
                    int previousFloor = _parallelFloor;

                    if (_parallelDepth > 0) _parallelFloor = _scopes.Count;

                    foreach (string parameter in lambda.Parameters)
                        DeclareName(parameter, lambda.Span, null, warnShadowing: false);

                    _ = CheckExpression(lambda.Body);

                    _parallelFloor = previousFloor;

                    PopScope();

                    return ScriptType.Fn;
                }

            case IfExpr conditional:
                return CheckIf(conditional);

            case BlockExpr block:
                return CheckBlock(block, ownScope: true);

            case ListExpr list:
                foreach (Expr item in list.Items) _ = CheckExpression(item);
                return ScriptType.List;

            case VectorExpr vector:
                foreach (Expr item in vector.Items) _ = CheckExpression(item);
                return ScriptType.Vec;

            case RecordExpr record:
                foreach (RecordFieldNode field in record.Fields) _ = CheckExpression(field.Value);
                return ScriptType.Record;

            default:
                return null;
        }
    }

    private ScriptType? CheckUnary(UnaryExpr unary)
    {
        ScriptType? operand = CheckExpression(unary.Operand);

        if (operand is not ScriptType known || known == ScriptType.Any) return null;

        ScriptType? result = TypeRules.Unary(unary.Operator, known);

        if (result != null) return result;

        _diagnostics.Error(DiagnosticCodes.BadOperandTypes, unary.Span,
            $"оператор '{OperatorText.Of(unary.Operator)}' не определён для типа {known.ToName()}",
            unary.Operator == UnaryOperator.Not && known == ScriptType.Num
                ? "неявного приведения числа к логическому значению нет: напишите сравнение, например 'x > 0'"
                : null);

        return null;
    }

    private ScriptType? CheckBinary(BinaryExpr binary)
    {
        ScriptType? leftType = CheckExpression(binary.Left);
        ScriptType? rightType = CheckExpression(binary.Right);

        if (leftType is not ScriptType left || rightType is not ScriptType right) return null;
        if (left == ScriptType.Any || right == ScriptType.Any) return null;

        if (binary.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual)
        {
            CheckEquality(binary, left, right);
            return ScriptType.Bool;
        }

        ScriptType? result = TypeRules.Binary(binary.Operator, left, right);

        if (result != null) return result;

        _diagnostics.Error(DiagnosticCodes.BadOperandTypes,
            binary.OperatorSpan.Length > 0 ? binary.OperatorSpan : binary.Span,
            left == right
                ? $"оператор '{OperatorText.Of(binary.Operator)}' не определён для типа {left.ToName()}"
                : $"оператор '{OperatorText.Of(binary.Operator)}' не определён для типов {left.ToName()} и {right.ToName()}",
            OperandHint(binary.Operator, left, right));

        return null;
    }

    /// <summary>
    /// Предупреждения о сравнениях, которые формально верны, но почти наверняка не то, что задумано.
    /// </summary>
    private void CheckEquality(BinaryExpr binary, ScriptType left, ScriptType right)
    {
        if (left != right && left != ScriptType.None && right != ScriptType.None)
        {
            _diagnostics.Warning(DiagnosticCodes.ComparingDifferentTypes, binary.Span,
                $"сравниваются значения разных типов: {left.ToName()} и {right.ToName()}",
                binary.Operator == BinaryOperator.Equal
                    ? "результат всегда ложь; приведите значения явно (core.to_str, core.parse_num)"
                    : "результат всегда истина; приведите значения явно (core.to_str, core.parse_num)");

            return;
        }

        if (left != ScriptType.Num) return;
        if (IsIntegerLiteral(binary.Left) || IsIntegerLiteral(binary.Right)) return;

        _diagnostics.Warning(DiagnosticCodes.ExactFloatComparison, binary.Span,
            "точное сравнение вещественных чисел",
            "результат счёта редко совпадает бит в бит: math.approx(a, b, eps: 1e-9)");
    }

    private static bool IsIntegerLiteral(Expr expression) => expression switch
    {
        LiteralExpr { Value.Type: ScriptType.Num } literal => literal.Value.RawNumber == Math.Floor(literal.Value.RawNumber),
        UnaryExpr { Operator: UnaryOperator.Negate } unary => IsIntegerLiteral(unary.Operand),
        _ => false,
    };

    private ScriptType? CheckIf(IfExpr conditional)
    {
        RequireBool(CheckExpression(conditional.Condition), conditional.Condition, "условие 'if'");

        ScriptType? then = CheckBlock(conditional.Then, ownScope: true);

        if (conditional.Else == null) return null;

        ScriptType? otherwise = CheckExpression(conditional.Else);

        return then != null && then == otherwise ? then : null;
    }

    private ScriptType? CheckIndex(IndexExpr index)
    {
        ScriptType? target = CheckExpression(index.Target);
        var keys = new List<ScriptType?>(index.Arguments.Count);

        foreach (IndexArgument argument in index.Arguments)
            keys.Add(argument.Value == null ? null : CheckExpression(argument.Value));

        if (target is not ScriptType known) return null;

        if (index.Arguments.Count == 2)
        {
            bool bothSingle = keys[0] == ScriptType.Num && keys[1] == ScriptType.Num;
            return known == ScriptType.Mat && bothSingle ? ScriptType.Num : null;
        }

        if (index.Arguments.Count != 1) return null;

        bool slice = keys[0] == ScriptType.Range;

        return known switch
        {
            ScriptType.Vec => slice ? ScriptType.Vec : ScriptType.Num,
            ScriptType.Str => ScriptType.Str,
            ScriptType.Range => slice ? null : ScriptType.Num,
            ScriptType.Mat => slice ? ScriptType.Mat : ScriptType.Vec,
            ScriptType.Table => keys[0] == ScriptType.Str ? null : slice ? ScriptType.Table : ScriptType.Record,
            _ => null,
        };
    }

    private ScriptType? CheckName(NameExpr name)
    {
        if (TryLookup(name.Name, out ScriptType? type)) return type;
        if (_declared.ContainsKey(name.Name)) return ScriptType.Fn;
        if (_registry.Find($"core.{name.Name}") != null) return ScriptType.Fn;
        if (_registry.HasNamespace(name.Name) || _aliases.ContainsKey(name.Name)) return null;

        _diagnostics.Error(DiagnosticCodes.UnboundName, name.Span,
            $"имя '{name.Name}' не связано",
            NameHint(name.Name));

        return null;
    }

    /// <summary>
    /// Проверяет обращение через точку.
    /// </summary>
    /// <remarks>
    /// Различие между <c>ml.kmeans</c> и <c>cfg.temp</c> синтаксически неразрешимо: решает то,
    /// связано ли имя слева с переменной. Если связано — это поле либо метод дескриптора, и
    /// проверить его до запуска нельзя, тип значения статически неизвестен.
    /// </remarks>
    private ScriptType? CheckMember(MemberExpr member, bool asCallee)
    {
        if (member.Target is not NameExpr root)
        {
            _ = CheckExpression(member.Target);
            return null;
        }

        if (TryLookup(root.Name, out _)) return null;

        string? ns = ResolveNamespace(root.Name);

        if (ns == null)
        {
            _diagnostics.Error(DiagnosticCodes.UnboundName, root.Span,
                $"имя '{root.Name}' не связано и не является пространством имён",
                NameHint(root.Name));

            return null;
        }

        string fullName = $"{ns}.{member.Name}";
        ScriptFunction? function = _registry.Find(fullName);

        if (function != null) return ScriptType.Fn;

        _diagnostics.Error(DiagnosticCodes.UnknownFunction, member.Span,
            $"неизвестная функция '{fullName}'",
            FunctionHint(ns, member.Name, asCallee));

        return null;
    }

    private ScriptType? CheckCall(CallExpr call, ScriptType? pipedType, bool piped)
    {
        var argumentTypes = new List<ScriptType?>(call.Arguments.Count);

        // Признак параллельности читается до проверки аргументов: лямбда, которую предстоит
        // проверить, — это и есть тело будущей ветви.
        bool parallel = IsParallelCall(call);

        if (parallel) _parallelDepth++;

        foreach (ArgumentNode argument in call.Arguments)
        {
            argumentTypes.Add(argument.Value == null || argument.Value is PlaceholderExpr
                ? pipedType
                : CheckExpression(argument.Value));
        }

        if (parallel) _parallelDepth--;

        switch (call.Callee)
        {
            case MemberExpr member when member.Target is NameExpr root && !TryLookup(root.Name, out _):
                {
                    string? ns = ResolveNamespace(root.Name);

                    if (ns == null)
                    {
                        _diagnostics.Error(DiagnosticCodes.UnboundName, root.Span,
                            $"имя '{root.Name}' не связано и не является пространством имён",
                            NameHint(root.Name));

                        return null;
                    }

                    // Обращение к файлам внутри стадии отмечается: её результат зависит от
                    // того, чего в ключе кэша нет.
                    if (_inStage && string.Equals(ns, "io", StringComparison.Ordinal)) _stageUsesFiles = true;

                    ScriptFunction? function = _registry.Find($"{ns}.{member.Name}");

                    if (function == null)
                    {
                        _diagnostics.Error(DiagnosticCodes.UnknownFunction, member.Span,
                            $"неизвестная функция '{ns}.{member.Name}'",
                            FunctionHint(ns, member.Name, asCallee: true));

                        return null;
                    }

                    CheckNativeArguments(call, function, argumentTypes, piped, pipedType);
                    return ReturnTypeOf(function);
                }

            case MemberExpr member:
                // Метод дескриптора: тип значения слева статически неизвестен, проверить нечем.
                _ = CheckExpression(member.Target);
                return null;

            case NameExpr name when _declared.TryGetValue(name.Name, out FunctionDeclStmt? declaration)
                && !TryLookup(name.Name, out _):
                WarnIfDeprecated(declaration, name.Span);
                CheckUserArguments(call, declaration, argumentTypes, piped, pipedType);
                return declaration.ReturnType;

            case NameExpr name when TryLookup(name.Name, out ScriptType? bound):
                if (bound is ScriptType type && type is not (ScriptType.Fn or ScriptType.Any))
                {
                    _diagnostics.Error(DiagnosticCodes.NotCallable, name.Span,
                        $"'{name.Name}' имеет тип {type.ToName()} и не является функцией");
                }

                return null;

            case NameExpr name:
                {
                    ScriptFunction? function = _registry.Find($"core.{name.Name}");

                    if (function == null)
                    {
                        _diagnostics.Error(DiagnosticCodes.UnknownFunction, name.Span,
                            $"неизвестная функция '{name.Name}'",
                            NameHint(name.Name));

                        return null;
                    }

                    CheckNativeArguments(call, function, argumentTypes, piped, pipedType);
                    return ReturnTypeOf(function);
                }

            default:
                _ = CheckExpression(call.Callee);
                return null;
        }
    }

    private static ScriptType? ReturnTypeOf(ScriptFunction function)
    {
        if (function.ReturnHandleType != null) return ScriptType.Handle;

        return function.ReturnType == ScriptType.Any ? null : function.ReturnType;
    }

    /// <summary>
    /// Сверяет аргументы вызова с сигнатурой функции модуля.
    /// </summary>
    /// <remarks>
    /// Правило простое и потому запоминаемое: позиционно передаются обязательные параметры в
    /// порядке объявления, всё необязательное — только по имени. По имени можно передать что
    /// угодно. Так порядок аргументов перестаёт быть тем, в чём можно ошибиться молча.
    /// </remarks>
    private void CheckNativeArguments(
        CallExpr call,
        ScriptFunction function,
        IReadOnlyList<ScriptType?> argumentTypes,
        bool piped,
        ScriptType? pipedType)
    {
        var slots = new List<(ScriptParameter Parameter, ScriptType? Type, TextSpan Span)>();
        var filled = new HashSet<string>(StringComparer.Ordinal);

        int positional = 0;
        bool hasPlaceholder = false;

        foreach (ArgumentNode argument in call.Arguments)
        {
            if (argument.IsPlaceholder || argument.Value is PlaceholderExpr) hasPlaceholder = true;
            if (argument.Name == null) positional++;
        }

        if (piped && !hasPlaceholder) positional++;

        int allowed = function.IsVariadic ? int.MaxValue : Math.Max(function.RequiredCount, 1);

        if (positional > allowed)
        {
            _diagnostics.Error(DiagnosticCodes.ExtraPositional, call.ArgumentsSpan,
                $"слишком много позиционных аргументов: {positional}, а обязательных параметров {function.RequiredCount}",
                $"необязательные параметры передаются только по имени\nсигнатура: {function.Signature}");
        }

        int cursor = 0;

        if (piped && !hasPlaceholder && function.Parameters.Count > 0)
        {
            slots.Add((function.Parameters[0], pipedType, call.Span));
            _ = filled.Add(function.Parameters[0].Name);
            cursor = 1;
        }

        for (int i = 0; i < call.Arguments.Count; i++)
        {
            ArgumentNode argument = call.Arguments[i];

            if (argument.Name == null)
            {
                if (cursor < function.Parameters.Count)
                {
                    slots.Add((function.Parameters[cursor], argumentTypes[i], argument.Span));
                    _ = filled.Add(function.Parameters[cursor].Name);
                }

                cursor++;
                continue;
            }

            ScriptParameter? parameter = function.FindParameter(argument.Name);

            if (parameter == null)
            {
                _diagnostics.Error(DiagnosticCodes.UnknownArgument, argument.NameSpan,
                    $"у '{function.FullName}' нет аргумента '{argument.Name}'",
                    ArgumentHint(function, argument.Name));

                continue;
            }

            if (!filled.Add(argument.Name))
            {
                _diagnostics.Error(DiagnosticCodes.DuplicateArgument, argument.NameSpan,
                    $"аргумент '{argument.Name}' передан дважды",
                    $"сигнатура: {function.Signature}");

                continue;
            }

            slots.Add((parameter, argumentTypes[i], argument.Span));
        }

        foreach ((ScriptParameter parameter, ScriptType? type, TextSpan span) in slots)
        {
            if (type is not ScriptType actual) continue;
            if (parameter.IsVariadic || TypeRules.Accepts(parameter.Type, actual)) continue;

            _diagnostics.Error(DiagnosticCodes.TypeMismatch, span,
                $"аргумент '{parameter.Name}' функции '{function.FullName}': ожидался {parameter.Type.ToName()}, передан {actual.ToName()}",
                $"сигнатура: {function.Signature}");
        }

        foreach (ScriptParameter parameter in function.Parameters)
        {
            if (parameter.IsOptional || parameter.IsVariadic) continue;
            if (filled.Contains(parameter.Name)) continue;

            _diagnostics.Error(DiagnosticCodes.MissingArgument, call.ArgumentsSpan,
                $"не передан обязательный аргумент '{parameter.Name}' функции '{function.FullName}'",
                $"{parameter.Description}\nсигнатура: {function.Signature}".Trim());
        }
    }

    /// <summary>Сверяет аргументы вызова с объявлением <c>fn</c> из этого же файла.</summary>
    private void CheckUserArguments(
        CallExpr call,
        FunctionDeclStmt declaration,
        IReadOnlyList<ScriptType?> argumentTypes,
        bool piped,
        ScriptType? pipedType)
    {
        var filled = new HashSet<string>(StringComparer.Ordinal);
        int cursor = 0;

        if (piped)
        {
            if (declaration.Parameters.Count > 0)
            {
                CheckUserArgument(declaration.Parameters[0], pipedType, call.Span, declaration);
                _ = filled.Add(declaration.Parameters[0].Name);
            }

            cursor = 1;
        }

        for (int i = 0; i < call.Arguments.Count; i++)
        {
            ArgumentNode argument = call.Arguments[i];

            if (argument.Name == null)
            {
                if (cursor >= declaration.Parameters.Count)
                {
                    _diagnostics.Error(DiagnosticCodes.ExtraPositional, argument.Span,
                        $"'{declaration.Name}' принимает {declaration.Parameters.Count} аргумент(ов)",
                        UserSignature(declaration));

                    cursor++;
                    continue;
                }

                ParameterNode target = declaration.Parameters[cursor];
                CheckUserArgument(target, argumentTypes[i], argument.Span, declaration);
                _ = filled.Add(target.Name);
                cursor++;
                continue;
            }

            ParameterNode? named = FindParameter(declaration, argument.Name);

            if (named == null)
            {
                _diagnostics.Error(DiagnosticCodes.UnknownArgument, argument.NameSpan,
                    $"у '{declaration.Name}' нет параметра '{argument.Name}'",
                    UserArgumentHint(declaration, argument.Name));

                continue;
            }

            if (!filled.Add(argument.Name))
            {
                _diagnostics.Error(DiagnosticCodes.DuplicateArgument, argument.NameSpan,
                    $"аргумент '{argument.Name}' передан дважды",
                    UserSignature(declaration));

                continue;
            }

            CheckUserArgument(named, argumentTypes[i], argument.Span, declaration);
        }

        foreach (ParameterNode parameter in declaration.Parameters)
        {
            if (parameter.Default != null || filled.Contains(parameter.Name)) continue;

            _diagnostics.Error(DiagnosticCodes.MissingArgument, call.ArgumentsSpan,
                $"не передан обязательный аргумент '{parameter.Name}' функции '{declaration.Name}'",
                UserSignature(declaration));
        }
    }

    private void CheckUserArgument(ParameterNode parameter, ScriptType? argument, TextSpan span, FunctionDeclStmt declaration)
    {
        if (parameter.DeclaredType is not ScriptType expected) return;
        if (argument is not ScriptType actual) return;
        if (TypeRules.Accepts(expected, actual)) return;

        _diagnostics.Error(DiagnosticCodes.TypeMismatch, span,
            $"аргумент '{parameter.Name}' функции '{declaration.Name}': ожидался {expected.ToName()}, передан {actual.ToName()}",
            UserSignature(declaration));
    }

    private static ParameterNode? FindParameter(FunctionDeclStmt declaration, string name)
    {
        foreach (ParameterNode parameter in declaration.Parameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.Ordinal)) return parameter;
        }

        return null;
    }

    private static string UserSignature(FunctionDeclStmt declaration)
    {
        var parts = new List<string>(declaration.Parameters.Count);

        foreach (ParameterNode parameter in declaration.Parameters)
        {
            string text = parameter.DeclaredType is ScriptType type
                ? $"{parameter.Name}: {type.ToName()}"
                : parameter.Name;

            parts.Add(parameter.Default != null ? $"{text} = …" : text);
        }

        string result = declaration.ReturnType is ScriptType returns ? $" -> {returns.ToName()}" : string.Empty;

        return $"сигнатура: {declaration.Name}({string.Join(", ", parts)}){result}";
    }

    private static string UserArgumentHint(FunctionDeclStmt declaration, string name)
    {
        var names = new List<string>(declaration.Parameters.Count);

        foreach (ParameterNode parameter in declaration.Parameters) names.Add(parameter.Name);

        string? closest = Suggestions.Closest(name, names);

        return closest != null
            ? $"возможно, имелось в виду: {closest}\n{UserSignature(declaration)}"
            : UserSignature(declaration);
    }

    private void RequireBool(ScriptType? type, Expr expression, string what)
    {
        if (type is not ScriptType known || known is ScriptType.Bool or ScriptType.Any) return;

        _diagnostics.Error(DiagnosticCodes.ConditionNotBool, expression.Span,
            $"{what} имеет тип {known.ToName()}, а должно быть логическим",
            known == ScriptType.Num
                ? "неявного приведения числа к логическому значению нет: напишите сравнение, например 'x > 0'"
                : "используйте сравнение либо функцию, возвращающую bool");
    }

    private static string? OperandHint(BinaryOperator op, ScriptType left, ScriptType right)
    {
        if (op == BinaryOperator.Multiply && left == ScriptType.Mat && right == ScriptType.Mat)
            return "для поэлементного произведения матриц есть mat.hadamard";

        if (left is ScriptType.Str or ScriptType.Num && right is ScriptType.Str or ScriptType.Num && left != right)
            return "неявных приведений между num и str нет: core.to_str(x) либо core.parse_num(s)";

        return "проверьте типы операндов: type(x) показывает тип значения";
    }

    // --- области видимости ---

    private void PushScope() => _scopes.Add(new Dictionary<string, ScriptType?>(StringComparer.Ordinal));

    private void PopScope() => _scopes.RemoveAt(_scopes.Count - 1);

    private bool TryLookup(string name, out ScriptType? type)
    {
        for (int i = _scopes.Count - 1; i >= 0; i--)
        {
            if (_scopes[i].TryGetValue(name, out type)) return true;
        }

        type = null;
        return false;
    }

    private void Assign(string name, ScriptType? type)
    {
        for (int i = _scopes.Count - 1; i >= 0; i--)
        {
            if (!_scopes[i].ContainsKey(name)) continue;

            _scopes[i][name] = type;
            return;
        }
    }

    private void DeclareName(string name, TextSpan span, ScriptType? type, bool warnShadowing = true)
    {
        if (string.IsNullOrEmpty(name)) return;

        Dictionary<string, ScriptType?> current = _scopes[^1];

        if (current.ContainsKey(name))
        {
            _diagnostics.Error(DiagnosticCodes.DuplicateLet, span,
                $"имя '{name}' уже связано в этой области",
                $"изменить значение можно через 'set {name} = ...'");

            return;
        }

        current[name] = type;

        if (!warnShadowing || _scopes.Count < 2) return;

        for (int i = _scopes.Count - 2; i >= 0; i--)
        {
            if (!_scopes[i].ContainsKey(name)) continue;

            _diagnostics.Warning(DiagnosticCodes.Shadowing, span,
                $"имя '{name}' затеняет объявленное во внешней области",
                "внешнее значение станет недоступно до конца блока; выберите другое имя, если это не задумано");

            return;
        }
    }

    private string? ResolveNamespace(string name)
    {
        if (_aliases.TryGetValue(name, out string? alias)) return alias;

        return _registry.HasNamespace(name) ? name : null;
    }

    // --- вывод типа без диагностик ---

    /// <summary>
    /// Определяет тип выражения, ничего не сообщая.
    /// </summary>
    /// <remarks>
    /// Нужен там, где выражение уже проверено, а тип понадобился повторно (значение блока).
    /// Повторный <see cref="CheckExpression"/> выдал бы вторую копию каждой диагностики.
    /// </remarks>
    private ScriptType? InferQuietly(Expr expression) => expression switch
    {
        LiteralExpr literal => literal.Value.Type,
        InterpolationExpr => ScriptType.Str,
        ListExpr => ScriptType.List,
        VectorExpr => ScriptType.Vec,
        RecordExpr => ScriptType.Record,
        RangeExpr => ScriptType.Range,
        LambdaExpr => ScriptType.Fn,
        NameExpr name => TryLookup(name.Name, out ScriptType? type) ? type : null,
        UnaryExpr unary => InferQuietly(unary.Operand) is ScriptType operand ? TypeRules.Unary(unary.Operator, operand) : null,
        BinaryExpr binary => InferBinaryQuietly(binary),
        CallExpr call => InferCallQuietly(call),
        PipeExpr pipe => InferCallQuietly(pipe.Right),
        _ => null,
    };

    private ScriptType? InferBinaryQuietly(BinaryExpr binary)
    {
        if (InferQuietly(binary.Left) is not ScriptType left) return null;
        if (InferQuietly(binary.Right) is not ScriptType right) return null;

        return TypeRules.Binary(binary.Operator, left, right);
    }

    private ScriptType? InferCallQuietly(CallExpr call)
    {
        switch (call.Callee)
        {
            case MemberExpr member when member.Target is NameExpr root && !TryLookup(root.Name, out _):
                {
                    string? ns = ResolveNamespace(root.Name);
                    ScriptFunction? function = ns == null ? null : _registry.Find($"{ns}.{member.Name}");

                    return function == null ? null : ReturnTypeOf(function);
                }

            case NameExpr name when _declared.TryGetValue(name.Name, out FunctionDeclStmt? declaration):
                return declaration.ReturnType;

            case NameExpr name:
                {
                    ScriptFunction? function = _registry.Find($"core.{name.Name}");
                    return function == null ? null : ReturnTypeOf(function);
                }

            default:
                return null;
        }
    }

    // --- подсказки ---

    private string NameHint(string name)
    {
        var candidates = new List<string>();

        foreach (Dictionary<string, ScriptType?> scope in _scopes) candidates.AddRange(scope.Keys);

        candidates.AddRange(_declared.Keys);

        foreach (ScriptFunction function in _registry.InNamespace("core")) candidates.Add(function.Name);

        string? closest = Suggestions.Closest(name, candidates);

        return closest != null
            ? $"возможно, имелось в виду: {closest}"
            : "имена вводятся через 'let'; функции библиотеки записываются с пространством имён, например 'math.sqrt(x)'";
    }

    private string NamespaceHint(string name)
    {
        string? closest = Suggestions.Closest(name, _registry.Namespaces);

        return closest != null
            ? $"возможно, имелось в виду: {closest}"
            : $"доступные пространства: {string.Join(", ", Sorted(_registry.Namespaces))}";
    }

    private string FunctionHint(string ns, string name, bool asCallee)
    {
        var names = new List<string>();

        foreach (ScriptFunction function in _registry.InNamespace(ns)) names.Add(function.Name);

        string? closest = Suggestions.Closest(name, names);

        if (closest != null)
        {
            ScriptFunction? found = _registry.Find($"{ns}.{closest}");
            string description = found?.Description ?? string.Empty;

            return description.Length > 0
                ? $"возможно, имелось в виду: {ns}.{closest} ({description})"
                : $"возможно, имелось в виду: {ns}.{closest}";
        }

        string suffix = asCallee ? string.Empty : "\nобращение к полю записи требует, чтобы слева было связанное имя";

        return $"все функции пространства: help(\"{ns}\"){suffix}";
    }

    private static string ArgumentHint(ScriptFunction function, string name)
    {
        var names = new List<string>();

        foreach (ScriptParameter known in function.Parameters) names.Add(known.Name);

        string? closest = Suggestions.Closest(name, names);

        if (closest == null) return $"сигнатура: {function.Signature}";

        ScriptParameter? parameter = function.FindParameter(closest);
        string note = parameter == null ? string.Empty
            : parameter.Description.Length > 0
                ? $" ({parameter.Description}{(parameter.IsOptional ? string.Empty : ", обязательный")})"
                : parameter.IsOptional ? string.Empty : " (обязательный)";

        return $"возможно, имелось в виду: {closest}{note}\nсигнатура: {function.Signature}";
    }

    private static IEnumerable<string> Sorted(IEnumerable<string> names)
    {
        var list = new List<string>(names);
        list.Sort(StringComparer.Ordinal);
        return list;
    }
}
