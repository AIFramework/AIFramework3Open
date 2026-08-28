using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Semantics;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;
using System.Text;

namespace AI.Script.Runtime;

public sealed partial class Interpreter
{
    /// <summary>Вычисляет выражение.</summary>
    public async ValueTask<ScriptValue> EvaluateAsync(Expr expression, Scope scope)
    {
        _context.CountStep();

        switch (expression)
        {
            case LiteralExpr literal:
                return literal.Value;

            case NameExpr name:
                return EvaluateName(name, scope);

            case InterpolationExpr interpolation:
                return await EvaluateInterpolationAsync(interpolation, scope).ConfigureAwait(false);

            case UnaryExpr unary:
                return await EvaluateUnaryAsync(unary, scope).ConfigureAwait(false);

            case BinaryExpr binary:
                return await EvaluateBinaryAsync(binary, scope).ConfigureAwait(false);

            case RangeExpr range:
                return await EvaluateRangeAsync(range, scope).ConfigureAwait(false);

            case CallExpr call:
                return await EvaluateCallAsync(call, scope, null).ConfigureAwait(false);

            case PipeExpr pipe:
                {
                    ScriptValue piped = await EvaluateAsync(pipe.Left, scope).ConfigureAwait(false);
                    return await EvaluateCallAsync(pipe.Right, scope, piped).ConfigureAwait(false);
                }

            case MemberExpr member:
                return await EvaluateMemberAsync(member, scope).ConfigureAwait(false);

            case IndexExpr index:
                return await EvaluateIndexAsync(index, scope).ConfigureAwait(false);

            case LambdaExpr lambda:
                return ScriptValue.Fn(new ScriptClosure("лямбда", ToParameters(lambda.Parameters), lambda.Body, scope));

            case IfExpr conditional:
                return await EvaluateIfValueAsync(conditional, scope).ConfigureAwait(false);

            case BlockExpr block:
                {
                    Completion completion = await ExecuteBlockAsync(block, new Scope(scope)).ConfigureAwait(false);
                    return RequireValue(completion, block.Span);
                }

            case ListExpr list:
                return await EvaluateListAsync(list, scope).ConfigureAwait(false);

            case VectorExpr vector:
                return await EvaluateVectorAsync(vector, scope).ConfigureAwait(false);

            case RecordExpr record:
                return await EvaluateRecordAsync(record, scope).ConfigureAwait(false);

            case PlaceholderExpr:
                throw new ScriptError(
                    DiagnosticCodes.UnexpectedToken,
                    "'_' допустим только как аргумент звена конвейера",
                    "например: weights |> stat.weighted_mean(values, w: _)")
                {
                    Span = expression.Span,
                };

            default:
                return ScriptValue.None;
        }
    }

    private ScriptValue RequireValue(Completion completion, TextSpan span)
    {
        if (completion.Flow == Flow.Normal) return completion.Value;

        throw new ScriptError(
            DiagnosticCodes.NotInLoop,
            "управляющая инструкция внутри выражения",
            "'break', 'continue' и 'return' допустимы в блоке-инструкции, но не в блоке, значение которого используется")
        {
            Span = span,
        };
    }

    private static List<ParameterNode> ToParameters(IReadOnlyList<string> names)
    {
        var parameters = new List<ParameterNode>(names.Count);

        foreach (string name in names) parameters.Add(new ParameterNode { Name = name });

        return parameters;
    }

    private ScriptValue EvaluateName(NameExpr name, Scope scope)
    {
        if (scope.TryGet(name.Name, out ScriptValue value)) return value;

        ScriptFunction? function = _registry.Find($"core.{name.Name}");
        if (function != null) return ScriptValue.Fn(new NativeFunction(function));

        throw new ScriptError(
            DiagnosticCodes.UnboundName,
            $"имя '{name.Name}' не связано",
            $"новое имя вводится через 'let {name.Name} = ...'")
        {
            Span = name.Span,
        };
    }

    private async ValueTask<ScriptValue> EvaluateInterpolationAsync(InterpolationExpr interpolation, Scope scope)
    {
        var builder = new StringBuilder();

        foreach (InterpolationPart part in interpolation.Parts)
        {
            if (part.Expression == null)
            {
                _ = builder.Append(part.Text);
                continue;
            }

            ScriptValue value = await EvaluateAsync(part.Expression, scope).ConfigureAwait(false);
            _ = builder.Append(ScriptFormatter.Format(value));
        }

        return ScriptValue.Str(builder.ToString());
    }

    private async ValueTask<ScriptValue> EvaluateUnaryAsync(UnaryExpr unary, Scope scope)
    {
        ScriptValue operand = await EvaluateAsync(unary.Operand, scope).ConfigureAwait(false);

        try
        {
            return Operations.Unary(unary.Operator, operand);
        }
        catch (ScriptError error) when (error.Span == null)
        {
            error.Span = unary.Span;
            throw;
        }
    }

    private async ValueTask<ScriptValue> EvaluateBinaryAsync(BinaryExpr binary, Scope scope)
    {
        if (binary.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            bool left = (await EvaluateAsync(binary.Left, scope).ConfigureAwait(false))
                .AsBool($"левый операнд '{OperatorText.Of(binary.Operator)}'");

            if (binary.Operator == BinaryOperator.And && !left) return ScriptValue.False;
            if (binary.Operator == BinaryOperator.Or && left) return ScriptValue.True;

            bool right = (await EvaluateAsync(binary.Right, scope).ConfigureAwait(false))
                .AsBool($"правый операнд '{OperatorText.Of(binary.Operator)}'");

            return ScriptValue.Bool(right);
        }

        ScriptValue leftValue = await EvaluateAsync(binary.Left, scope).ConfigureAwait(false);
        ScriptValue rightValue = await EvaluateAsync(binary.Right, scope).ConfigureAwait(false);

        try
        {
            return Operations.Binary(binary.Operator, leftValue, rightValue);
        }
        catch (ScriptError error) when (error.Span == null)
        {
            error.Span = binary.OperatorSpan.Length > 0 ? binary.OperatorSpan : binary.Span;
            throw;
        }
    }

    private async ValueTask<ScriptValue> EvaluateRangeAsync(RangeExpr range, Scope scope)
    {
        double from = (await EvaluateAsync(range.From, scope).ConfigureAwait(false)).AsNumber("начало диапазона");
        double to = (await EvaluateAsync(range.To, scope).ConfigureAwait(false)).AsNumber("конец диапазона");
        double step = range.By == null
            ? 1
            : (await EvaluateAsync(range.By, scope).ConfigureAwait(false)).AsNumber("шаг диапазона");

        if (step == 0)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, "шаг диапазона равен нулю")
            {
                Span = range.Span,
            };
        }

        return ScriptValue.Range(new ScriptRange(from, to, step));
    }

    private async ValueTask<ScriptValue> EvaluateIfValueAsync(IfExpr conditional, Scope scope)
    {
        if (conditional.Else == null)
        {
            throw new ScriptError(
                DiagnosticCodes.UnexpectedToken,
                "'if' в позиции выражения обязан иметь ветвь 'else'",
                "иначе у выражения не было бы значения при ложном условии")
            {
                Span = conditional.Span,
            };
        }

        Completion completion = await ExecuteIfAsync(conditional, scope).ConfigureAwait(false);

        return RequireValue(completion, conditional.Span);
    }

    private async ValueTask<ScriptValue> EvaluateListAsync(ListExpr list, Scope scope)
    {
        var items = new ScriptValue[list.Items.Count];

        for (int i = 0; i < list.Items.Count; i++)
            items[i] = await EvaluateAsync(list.Items[i], scope).ConfigureAwait(false);

        _context.CountAllocation(items.Length);

        return ScriptValue.List(ScriptList.Own(items));
    }

    private async ValueTask<ScriptValue> EvaluateVectorAsync(VectorExpr expression, Scope scope)
    {
        var vector = new Vector(expression.Items.Count);

        for (int i = 0; i < expression.Items.Count; i++)
        {
            ScriptValue item = await EvaluateAsync(expression.Items[i], scope).ConfigureAwait(false);
            vector[i] = item.AsNumber($"элемент {i} литерала вектора");
        }

        _context.CountAllocation(vector.Count);

        return ScriptValue.Vec(vector);
    }

    private async ValueTask<ScriptValue> EvaluateRecordAsync(RecordExpr record, Scope scope)
    {
        var fields = new List<KeyValuePair<string, ScriptValue>>(record.Fields.Count);

        foreach (RecordFieldNode field in record.Fields)
        {
            ScriptValue value = await EvaluateAsync(field.Value, scope).ConfigureAwait(false);

            if (!field.IsSpread)
            {
                fields.Add(new KeyValuePair<string, ScriptValue>(field.Name!, value));
                continue;
            }

            if (value.Type != ScriptType.Record)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"распаковать можно только запись, а здесь {value.Type.ToName()}")
                {
                    Span = field.Span,
                };
            }

            foreach (var pair in value.AsRecord().Pairs()) fields.Add(pair);
        }

        return ScriptValue.Record(ScriptRecord.From(fields));
    }

    // --- обращение через точку ---

    private async ValueTask<ScriptValue> EvaluateMemberAsync(MemberExpr member, Scope scope)
    {
        if (member.Target is NameExpr root && !scope.TryGet(root.Name, out _))
        {
            string? ns = ResolveNamespace(root.Name);

            if (ns != null)
            {
                ScriptFunction? function = _registry.Find($"{ns}.{member.Name}");

                if (function != null) return ScriptValue.Fn(new NativeFunction(function));

                throw new ScriptError(
                    DiagnosticCodes.UnknownFunction,
                    $"неизвестная функция '{ns}.{member.Name}'",
                    $"все функции пространства: help(\"{ns}\")")
                {
                    Span = member.Span,
                };
            }
        }

        ScriptValue target = await EvaluateAsync(member.Target, scope).ConfigureAwait(false);

        if (target.Type == ScriptType.Record)
        {
            ScriptRecord record = target.AsRecord();

            if (record.TryGet(member.Name, out ScriptValue value)) return value;

            string? closest = Suggestions.Closest(member.Name, record.Keys);

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"в записи нет поля '{member.Name}'",
                closest != null
                    ? $"возможно, имелось в виду: {closest}"
                    : $"поля записи: {string.Join(", ", record.Keys)}")
            {
                Span = member.NameSpan,
            };
        }

        if (target.Type == ScriptType.Handle)
        {
            ScriptHandle handle = target.AsHandle();

            if (_registry.TryGetMethod(handle.TypeName, member.Name, out ScriptFunction method))
                return ScriptValue.Fn(new NativeFunction(method));

            throw new ScriptError(
                DiagnosticCodes.UnknownFunction,
                $"у дескриптора '{handle.TypeName}' нет метода '{member.Name}'")
            {
                Span = member.NameSpan,
            };
        }

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"у значения типа {target.Type.ToName()} нет полей",
            "длина берётся функцией len(x), а не полем")
        {
            Span = member.Span,
        };
    }

    private string? ResolveNamespace(string name)
    {
        if (_aliases.TryGetValue(name, out string? alias)) return alias;

        return _registry.HasNamespace(name) ? name : null;
    }

    // --- вызовы ---

    private async ValueTask<ScriptValue> EvaluateCallAsync(CallExpr call, Scope scope, ScriptValue? piped)
    {
        ScriptFunction? native = null;
        ScriptCallable? callable = null;
        ScriptValue? receiver = null;

        switch (call.Callee)
        {
            case MemberExpr member when member.Target is NameExpr root && !scope.TryGet(root.Name, out _):
                {
                    string? ns = ResolveNamespace(root.Name);

                    if (ns == null)
                    {
                        throw new ScriptError(
                            DiagnosticCodes.UnboundName,
                            $"имя '{root.Name}' не связано и не является пространством имён")
                        {
                            Span = root.Span,
                        };
                    }

                    native = _registry.Find($"{ns}.{member.Name}");

                    if (native == null)
                    {
                        throw new ScriptError(
                            DiagnosticCodes.UnknownFunction,
                            $"неизвестная функция '{ns}.{member.Name}'",
                            $"все функции пространства: help(\"{ns}\")")
                        {
                            Span = member.Span,
                        };
                    }

                    break;
                }

            case MemberExpr member:
                {
                    ScriptValue target = await EvaluateAsync(member.Target, scope).ConfigureAwait(false);

                    if (target.Type == ScriptType.Handle)
                    {
                        ScriptHandle handle = target.AsHandle();

                        if (!_registry.TryGetMethod(handle.TypeName, member.Name, out ScriptFunction method))
                        {
                            throw new ScriptError(
                                DiagnosticCodes.UnknownFunction,
                                $"у дескриптора '{handle.TypeName}' нет метода '{member.Name}'")
                            {
                                Span = member.NameSpan,
                            };
                        }

                        native = method;
                        receiver = target;
                        break;
                    }

                    if (target.Type == ScriptType.Record)
                    {
                        throw new ScriptError(
                            DiagnosticCodes.BadOperand,
                            $"поле записи '{member.Name}' нельзя вызвать через точку",
                            "если в поле лежит функция, вызовите её так: (запись.поле)(аргументы)")
                        {
                            Span = member.Span,
                        };
                    }

                    ScriptValue value = await EvaluateMemberAsync(member, scope).ConfigureAwait(false);
                    callable = value.AsCallable($"'{member.Name}'");
                    break;
                }

            case NameExpr name when scope.TryGet(name.Name, out ScriptValue bound):
                callable = bound.AsCallable($"'{name.Name}'");
                break;

            case NameExpr name:
                native = _registry.Find($"core.{name.Name}");

                if (native == null)
                {
                    throw new ScriptError(
                        DiagnosticCodes.UnknownFunction,
                        $"неизвестная функция '{name.Name}'")
                    {
                        Span = name.Span,
                    };
                }

                break;

            default:
                {
                    ScriptValue value = await EvaluateAsync(call.Callee, scope).ConfigureAwait(false);
                    callable = value.AsCallable("вызываемое значение");
                    break;
                }
        }

        if (callable is NativeFunction wrapper)
        {
            native = wrapper.Function;
            callable = null;
        }

        (List<ScriptValue> positional, List<KeyValuePair<string, ScriptValue>> named) =
            await EvaluateArgumentsAsync(call, scope, piped, receiver).ConfigureAwait(false);

        _context.CountCall();

        if (native != null) return await InvokeNativeAsync(native, positional, named, call.Span).ConfigureAwait(false);

        return await InvokeClosureAsync((ScriptClosure)callable!, positional, named, call.Span).ConfigureAwait(false);
    }

    private async ValueTask<(List<ScriptValue> Positional, List<KeyValuePair<string, ScriptValue>> Named)>
        EvaluateArgumentsAsync(CallExpr call, Scope scope, ScriptValue? piped, ScriptValue? receiver)
    {
        bool hasPlaceholder = false;

        foreach (ArgumentNode argument in call.Arguments)
        {
            if (argument.IsPlaceholder || argument.Value is PlaceholderExpr) { hasPlaceholder = true; break; }
        }

        var positional = new List<ScriptValue>();
        var named = new List<KeyValuePair<string, ScriptValue>>();

        if (receiver != null) positional.Add(receiver.Value);
        if (piped != null && !hasPlaceholder) positional.Add(piped.Value);

        foreach (ArgumentNode argument in call.Arguments)
        {
            bool placeholder = argument.IsPlaceholder || argument.Value is PlaceholderExpr;

            if (placeholder && piped == null)
            {
                throw new ScriptError(
                    DiagnosticCodes.UnexpectedToken,
                    "'_' допустим только в звене конвейера")
                {
                    Span = argument.Span,
                };
            }

            ScriptValue value = placeholder
                ? piped!.Value
                : await EvaluateAsync(argument.Value!, scope).ConfigureAwait(false);

            if (argument.Name == null) positional.Add(value);
            else named.Add(new KeyValuePair<string, ScriptValue>(argument.Name, value));
        }

        return (positional, named);
    }

    private async ValueTask<ScriptValue> InvokeNativeAsync(
        ScriptFunction function,
        List<ScriptValue> positional,
        List<KeyValuePair<string, ScriptValue>> named,
        TextSpan span)
    {
        ScriptValue[] slots = BindNative(function, positional, named, span);

        try
        {
            return await function.Invoke(slots, _context).ConfigureAwait(false);
        }
        catch (ScriptError error)
        {
            error.Span ??= span;
            throw;
        }
    }

    private static ScriptValue[] BindNative(
        ScriptFunction function,
        List<ScriptValue> positional,
        List<KeyValuePair<string, ScriptValue>> named,
        TextSpan span)
    {
        int count = function.Parameters.Count;
        var slots = new ScriptValue[count];
        var filled = new bool[count];

        int variadic = -1;
        for (int i = 0; i < count; i++)
        {
            if (function.Parameters[i].IsVariadic) { variadic = i; break; }
        }

        var rest = new List<ScriptValue>();
        int cursor = 0;

        foreach (ScriptValue value in positional)
        {
            if (variadic >= 0 && cursor >= variadic)
            {
                rest.Add(value);
                continue;
            }

            if (cursor >= count)
            {
                throw new ScriptError(
                    DiagnosticCodes.ExtraPositional,
                    $"'{function.FullName}' принимает {count} аргумент(ов), передано больше",
                    $"сигнатура: {function.Signature}")
                {
                    Span = span,
                };
            }

            slots[cursor] = value;
            filled[cursor] = true;
            cursor++;
        }

        if (variadic >= 0)
        {
            slots[variadic] = ScriptValue.List(ScriptList.From(rest));
            filled[variadic] = true;
        }

        foreach (var pair in named)
        {
            int index = IndexOfParameter(function, pair.Key);

            if (index < 0)
            {
                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"у '{function.FullName}' нет аргумента '{pair.Key}'",
                    $"сигнатура: {function.Signature}")
                {
                    Span = span,
                };
            }

            if (filled[index])
            {
                throw new ScriptError(
                    DiagnosticCodes.DuplicateArgument,
                    $"аргумент '{pair.Key}' передан дважды",
                    $"сигнатура: {function.Signature}")
                {
                    Span = span,
                };
            }

            slots[index] = pair.Value;
            filled[index] = true;
        }

        for (int i = 0; i < count; i++)
        {
            if (filled[i]) continue;

            ScriptParameter parameter = function.Parameters[i];

            if (parameter.IsVariadic) { slots[i] = ScriptValue.List(ScriptList.Empty); continue; }
            if (parameter.IsOptional) { slots[i] = parameter.Default; continue; }

            throw new ScriptError(
                DiagnosticCodes.MissingArgument,
                $"не передан обязательный аргумент '{parameter.Name}' функции '{function.FullName}'",
                $"сигнатура: {function.Signature}")
            {
                Span = span,
            };
        }

        return slots;
    }

    private static int IndexOfParameter(ScriptFunction function, string name)
    {
        for (int i = 0; i < function.Parameters.Count; i++)
        {
            if (string.Equals(function.Parameters[i].Name, name, StringComparison.Ordinal)) return i;
        }

        return -1;
    }

    private async ValueTask<ScriptValue> InvokeClosureAsync(
        ScriptClosure closure,
        List<ScriptValue> positional,
        List<KeyValuePair<string, ScriptValue>> named,
        TextSpan span)
    {
        if (_depth >= _context.Options.Limits.CallDepth)
        {
            throw new ScriptAbort(
                DiagnosticCodes.CallDepthLimit,
                $"превышена глубина вложенности вызовов ({_context.Options.Limits.CallDepth})",
                "вероятно, рекурсия не завершается");
        }

        var scope = new Scope(closure.Captured);
        int count = closure.Parameters.Count;
        var filled = new bool[count];

        for (int i = 0; i < positional.Count; i++)
        {
            if (i >= count)
            {
                throw new ScriptError(
                    DiagnosticCodes.ExtraPositional,
                    $"'{closure.Name}' принимает {count} аргумент(ов), передано {positional.Count}")
                {
                    Span = span,
                };
            }

            scope.Declare(closure.Parameters[i].Name, positional[i]);
            filled[i] = true;
        }

        foreach (var pair in named)
        {
            int index = -1;

            for (int i = 0; i < count; i++)
            {
                if (!string.Equals(closure.Parameters[i].Name, pair.Key, StringComparison.Ordinal)) continue;
                index = i;
                break;
            }

            if (index < 0)
            {
                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"у '{closure.Name}' нет параметра '{pair.Key}'",
                    $"параметры: {string.Join(", ", closure.ParameterNames)}")
                {
                    Span = span,
                };
            }

            if (filled[index])
            {
                throw new ScriptError(
                    DiagnosticCodes.DuplicateArgument,
                    $"аргумент '{pair.Key}' передан дважды")
                {
                    Span = span,
                };
            }

            scope.Declare(pair.Key, pair.Value);
            filled[index] = true;
        }

        for (int i = 0; i < count; i++)
        {
            if (filled[i]) continue;

            ParameterNode parameter = closure.Parameters[i];

            if (parameter.Default == null)
            {
                throw new ScriptError(
                    DiagnosticCodes.MissingArgument,
                    $"не передан обязательный аргумент '{parameter.Name}' функции '{closure.Name}'",
                    $"параметры: {string.Join(", ", closure.ParameterNames)}")
                {
                    Span = span,
                };
            }

            scope.Declare(parameter.Name, await EvaluateAsync(parameter.Default, closure.Captured).ConfigureAwait(false));
        }

        // Стадия — та же функция, но за ней наблюдают и её результат можно не считать заново.
        // Развилка стоит после связывания аргументов: ключ кэша строится по их значениям.
        return closure.IsStage
            ? await RunStageAsync(closure, scope, span).ConfigureAwait(false)
            : await ExecuteClosureBodyAsync(closure, scope, span).ConfigureAwait(false);
    }

    /// <summary>Исполняет тело замыкания в уже подготовленной области.</summary>
    private async ValueTask<ScriptValue> ExecuteClosureBodyAsync(ScriptClosure closure, Scope scope, TextSpan span)
    {
        _depth++;

        try
        {
            if (closure.Body is BlockExpr block)
            {
                Completion completion = await ExecuteBlockAsync(block, scope).ConfigureAwait(false);

                if (completion.Flow is Flow.Break or Flow.Continue)
                {
                    throw new ScriptError(
                        DiagnosticCodes.NotInLoop,
                        "'break'/'continue' вне цикла в теле функции")
                    {
                        Span = span,
                    };
                }

                return completion.Value;
            }

            return await EvaluateAsync(closure.Body, scope).ConfigureAwait(false);
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>Вызывает значение-функцию: точка входа для функций высшего порядка модулей.</summary>
    public async ValueTask<ScriptValue> CallAsync(ScriptValue callable, params ScriptValue[] arguments)
    {
        ScriptCallable target = callable.AsCallable("вызываемое значение");
        var positional = new List<ScriptValue>(arguments);
        var named = new List<KeyValuePair<string, ScriptValue>>();

        _context.CountCall();

        if (target is NativeFunction native)
            return await InvokeNativeAsync(native.Function, positional, named, default).ConfigureAwait(false);

        return await InvokeClosureAsync((ScriptClosure)target, positional, named, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Вызывает функцию для каждого элемента, при <paramref name="parallelism"/> больше единицы —
    /// на нескольких потоках; порядок результата сохраняется всегда.
    /// </summary>
    /// <remarks>
    /// Каждая ветвь получает собственный интерпретатор: глубина вызовов и текущая стадия — его
    /// личное состояние, и одно на всех превратило бы вложенность в счётчик гонок. Общими
    /// остаются состояние прогона (счётчики, транскрипт, кэш), и всё общее синхронизировано.
    /// <para>
    /// Поток случайных чисел у ветви свой, выведенный из зерна прогона и номера элемента:
    /// иначе результат зависел бы от того, какая ветвь успела первой, то есть перестал бы
    /// воспроизводиться.
    /// </para>
    /// </remarks>
    public async ValueTask<ScriptValue[]> CallEachAsync(
        ScriptValue callable,
        IReadOnlyList<ScriptValue> items,
        int parallelism)
    {
        ArgumentNullException.ThrowIfNull(items);

        var results = new ScriptValue[items.Count];
        int degree = Math.Max(1, parallelism);

        if (degree == 1 || items.Count <= 1)
        {
            for (int i = 0; i < items.Count; i++)
            {
                _context.Cancellation.ThrowIfCancellationRequested();
                results[i] = await CallAsync(callable, items[i]).ConfigureAwait(false);
            }

            return results;
        }

        var scheduling = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree,
            CancellationToken = _context.Cancellation,
        };

        await Parallel.ForAsync(0, items.Count, scheduling, async (index, cancellation) =>
        {
            Interpreter branch = Branch();

            using (_context.UseBranchRandom(index))
                results[index] = await branch.CallAsync(callable, items[index]).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return results;
    }

    // --- индексация ---

    private async ValueTask<ScriptValue> EvaluateIndexAsync(IndexExpr index, Scope scope)
    {
        ScriptValue target = await EvaluateAsync(index.Target, scope).ConfigureAwait(false);
        var arguments = new ScriptValue?[index.Arguments.Count];

        for (int i = 0; i < index.Arguments.Count; i++)
        {
            IndexArgument argument = index.Arguments[i];
            arguments[i] = argument.IsAll ? null : await EvaluateAsync(argument.Value!, scope).ConfigureAwait(false);
        }

        try
        {
            return Index(target, arguments);
        }
        catch (ScriptError error) when (error.Span == null)
        {
            error.Span = index.Span;
            throw;
        }
    }

    private static ScriptValue Index(ScriptValue target, ScriptValue?[] arguments)
    {
        if (arguments.Length == 2) return IndexMatrix(target, arguments);

        if (arguments.Length != 1)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"индекс из {arguments.Length} частей не поддерживается");
        }

        ScriptValue? argument = arguments[0];

        if (argument == null)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                "':' допустим только в индексе матрицы");
        }

        ScriptValue key = argument.Value;

        switch (target.Type)
        {
            case ScriptType.Vec:
                {
                    Vector vector = target.AsVector();

                    if (key.Type == ScriptType.Range) return ScriptValue.Vec(SliceVector(vector, key.AsRange()));

                    int position = Normalize(key.AsNumber("индекс"), vector.Count, "вектор");
                    return ScriptValue.Num(vector[position]);
                }

            case ScriptType.List:
                {
                    ScriptList list = target.AsList();

                    if (key.Type == ScriptType.Range)
                    {
                        (int start, int end) = SliceBounds(key.AsRange(), list.Count, "список");
                        return ScriptValue.List(list.Slice(start, end));
                    }

                    int position = Normalize(key.AsNumber("индекс"), list.Count, "список");
                    return list[position];
                }

            case ScriptType.Str:
                {
                    string text = target.AsString();

                    if (key.Type == ScriptType.Range)
                    {
                        (int start, int end) = SliceBounds(key.AsRange(), text.Length, "строка");
                        return ScriptValue.Str(text[start..end]);
                    }

                    int position = Normalize(key.AsNumber("индекс"), text.Length, "строка");
                    return ScriptValue.Str(text[position].ToString());
                }

            case ScriptType.Record:
                {
                    ScriptRecord record = target.AsRecord();
                    string name = key.AsString("имя поля");

                    if (record.TryGet(name, out ScriptValue value)) return value;

                    throw new ScriptError(
                        DiagnosticCodes.UnknownArgument,
                        $"в записи нет поля '{name}'",
                        $"поля: {string.Join(", ", record.Keys)}");
                }

            case ScriptType.Range:
                {
                    ScriptRange range = target.AsRange();
                    int position = Normalize(key.AsNumber("индекс"), range.Count, "диапазон");
                    return ScriptValue.Num(range[position]);
                }

            case ScriptType.Mat:
                {
                    Matrix matrix = target.AsMatrix();

                    if (key.Type == ScriptType.Range)
                    {
                        (int start, int end) = SliceBounds(key.AsRange(), matrix.Height, "матрица");
                        return ScriptValue.Mat(SubMatrix(matrix, start, end, 0, matrix.Width));
                    }

                    int row = Normalize(key.AsNumber("индекс строки"), matrix.Height, "матрица");
                    var result = new Vector(matrix.Width);

                    for (int j = 0; j < matrix.Width; j++) result[j] = matrix[row, j];

                    return ScriptValue.Vec(result);
                }

            case ScriptType.Table:
                {
                    ScriptTable table = target.AsTable();

                    if (key.Type == ScriptType.Str) return table.Column(key.AsString()).AsValue();

                    if (key.Type == ScriptType.Range)
                    {
                        (int start, int end) = SliceBounds(key.AsRange(), table.RowCount, "таблица");
                        var rows = new int[end - start];

                        for (int i = start; i < end; i++) rows[i - start] = i;

                        return ScriptValue.Table(table.Take(rows));
                    }

                    int index = Normalize(key.AsNumber("номер строки"), table.RowCount, "таблица");
                    return ScriptValue.Record(table.Row(index));
                }

            default:
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"значение типа {target.Type.ToName()} не индексируется");
        }
    }

    private static ScriptValue IndexMatrix(ScriptValue target, ScriptValue?[] arguments)
    {
        if (target.Type != ScriptType.Mat)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"двойной индекс применим к матрице, а здесь {target.Type.ToName()}");
        }

        Matrix matrix = target.AsMatrix();

        Selector rows = ResolveSelector(arguments[0], matrix.Height, "индекс строки", "матрица");
        Selector columns = ResolveSelector(arguments[1], matrix.Width, "индекс столбца", "матрица");

        // Одиночный индекс сужает измерение, ':' и срез — сохраняют его. Отсюда и тип
        // результата: два одиночных дают число, один — вектор, ни одного — матрицу.
        if (rows.IsSingle && columns.IsSingle) return ScriptValue.Num(matrix[rows.Start, columns.Start]);

        if (rows.IsSingle)
        {
            var row = new Vector(columns.Length);

            for (int j = columns.Start; j < columns.End; j++) row[j - columns.Start] = matrix[rows.Start, j];

            return ScriptValue.Vec(row);
        }

        if (columns.IsSingle)
        {
            var column = new Vector(rows.Length);

            for (int i = rows.Start; i < rows.End; i++) column[i - rows.Start] = matrix[i, columns.Start];

            return ScriptValue.Vec(column);
        }

        return ScriptValue.Mat(SubMatrix(matrix, rows.Start, rows.End, columns.Start, columns.End));
    }

    /// <summary>Разобранный элемент индекса: одиночная позиция либо полуинтервал.</summary>
    private readonly struct Selector
    {
        public int Start { get; }

        public int End { get; }

        public bool IsSingle { get; }

        public int Length => End - Start;

        public Selector(int start, int end, bool isSingle)
        {
            Start = start;
            End = end;
            IsSingle = isSingle;
        }
    }

    private static Selector ResolveSelector(ScriptValue? key, int length, string what, string container)
    {
        if (key == null) return new Selector(0, length, isSingle: false);

        if (key.Value.Type == ScriptType.Range)
        {
            (int start, int end) = SliceBounds(key.Value.AsRange(), length, container);
            return new Selector(start, end, isSingle: false);
        }

        int index = Normalize(key.Value.AsNumber(what), length, container);
        return new Selector(index, index + 1, isSingle: true);
    }

    private static Matrix SubMatrix(Matrix matrix, int rowStart, int rowEnd, int columnStart, int columnEnd)
    {
        var result = new Matrix(rowEnd - rowStart, columnEnd - columnStart);

        for (int i = rowStart; i < rowEnd; i++)
        {
            for (int j = columnStart; j < columnEnd; j++) result[i - rowStart, j - columnStart] = matrix[i, j];
        }

        return result;
    }

    private static Vector SliceVector(Vector vector, ScriptRange range)
    {
        (int start, int end) = SliceBounds(range, vector.Count, "вектор");
        var result = new Vector(end - start);

        for (int i = start; i < end; i++) result[i - start] = vector[i];

        return result;
    }

    private static (int Start, int End) SliceBounds(ScriptRange range, int length, string what)
    {
        if (range.Step != 1)
        {
            throw new ScriptError(
                DiagnosticCodes.NotImplementedYet,
                "срез с шагом пока не поддерживается",
                "используйте цикл: for i in a..b by k");
        }

        int start = Normalize(range.Start, length, what, allowEnd: true);
        int end = Normalize(range.End, length, what, allowEnd: true);

        if (end < start)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"срез {start}..{end} задан наоборот");
        }

        return (start, end);
    }

    private static int Normalize(double raw, int length, string what, bool allowEnd = false)
    {
        double rounded = Math.Round(raw);

        if (Math.Abs(raw - rounded) > 1e-9)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"индекс должен быть целым, получено {ScriptFormatter.Number(raw)}");
        }

        int index = (int)rounded;
        if (index < 0) index += length;

        int limit = allowEnd ? length : length - 1;

        if (index < 0 || index > limit)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"индекс {ScriptFormatter.Number(raw)} вне границ: {what} длиной {length}",
                "отрицательный индекс отсчитывается с конца: xs[-1] — последний элемент");
        }

        return index;
    }

    // --- присваивание ---

    private async ValueTask ExecuteSetAsync(SetStmt set, Scope scope)
    {
        ScriptValue value = await EvaluateAsync(set.Value, scope).ConfigureAwait(false);

        switch (set.Target)
        {
            case NameExpr name:
                {
                    if (!scope.TryGet(name.Name, out ScriptValue current))
                    {
                        throw new ScriptError(
                            DiagnosticCodes.UnboundSet,
                            $"имя '{name.Name}' не связано",
                            $"новое имя вводится через 'let {name.Name} = ...'")
                        {
                            Span = name.Span,
                        };
                    }

                    if (set.Compound != null) value = Operations.Binary(set.Compound.Value, current, value);

                    _ = scope.TryAssign(name.Name, value);
                    return;
                }

            case IndexExpr { Target: NameExpr container } index:
                {
                    await AssignIndexedAsync(container, index, set, value, scope).ConfigureAwait(false);
                    return;
                }

            case MemberExpr { Target: NameExpr container } member:
                {
                    if (!scope.TryGet(container.Name, out ScriptValue current))
                    {
                        throw new ScriptError(DiagnosticCodes.UnboundSet, $"имя '{container.Name}' не связано")
                        {
                            Span = container.Span,
                        };
                    }

                    ScriptRecord record = current.AsRecord($"'{container.Name}'");

                    if (set.Compound != null && record.TryGet(member.Name, out ScriptValue previous))
                        value = Operations.Binary(set.Compound.Value, previous, value);

                    _ = scope.TryAssign(container.Name, ScriptValue.Record(record.With(member.Name, value)));
                    return;
                }

            default:
                throw new ScriptError(
                    DiagnosticCodes.NotImplementedYet,
                    "присваивание в глубину пока не поддерживается",
                    "поддержаны 'set x = ...', 'set x[i] = ...' и 'set x.поле = ...'")
                {
                    Span = set.Target.Span,
                };
        }
    }

    /// <summary>
    /// Присваивание по индексу: значения языка неизменяемы, поэтому создаётся копия.
    /// </summary>
    private async ValueTask AssignIndexedAsync(
        NameExpr container,
        IndexExpr index,
        SetStmt set,
        ScriptValue value,
        Scope scope)
    {
        if (!scope.TryGet(container.Name, out ScriptValue current))
        {
            throw new ScriptError(DiagnosticCodes.UnboundSet, $"имя '{container.Name}' не связано")
            {
                Span = container.Span,
            };
        }

        if (index.Arguments.Count != 1 || index.Arguments[0].IsAll)
        {
            throw new ScriptError(
                DiagnosticCodes.NotImplementedYet,
                "присваивание по составному индексу пока не поддерживается")
            {
                Span = index.Span,
            };
        }

        ScriptValue key = await EvaluateAsync(index.Arguments[0].Value!, scope).ConfigureAwait(false);

        switch (current.Type)
        {
            case ScriptType.Vec:
                {
                    Vector vector = current.AsVector();
                    int position = Normalize(key.AsNumber("индекс"), vector.Count, "вектор");
                    double assigned = value.AsNumber("присваиваемое значение");

                    if (set.Compound != null)
                    {
                        assigned = Operations
                            .Binary(set.Compound.Value, ScriptValue.Num(vector[position]), ScriptValue.Num(assigned))
                            .AsNumber("результат составного присваивания");
                    }

                    var copy = new Vector(vector.ToArray());
                    copy[position] = assigned;

                    _ = scope.TryAssign(container.Name, ScriptValue.Vec(copy));
                    return;
                }

            case ScriptType.List:
                {
                    ScriptList list = current.AsList();
                    int position = Normalize(key.AsNumber("индекс"), list.Count, "список");
                    ScriptValue assigned = set.Compound == null
                        ? value
                        : Operations.Binary(set.Compound.Value, list[position], value);

                    _ = scope.TryAssign(container.Name, ScriptValue.List(list.SetItem(position, assigned)));
                    return;
                }

            case ScriptType.Record:
                {
                    ScriptRecord record = current.AsRecord();
                    string name = key.AsString("имя поля");
                    ScriptValue assigned = value;

                    if (set.Compound != null && record.TryGet(name, out ScriptValue previous))
                        assigned = Operations.Binary(set.Compound.Value, previous, value);

                    _ = scope.TryAssign(container.Name, ScriptValue.Record(record.With(name, assigned)));
                    return;
                }

            default:
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"значению типа {current.Type.ToName()} нельзя присвоить по индексу",
                    current.Type == ScriptType.Table
                        ? "таблица неизменяема: колонка задаётся через table.with(t, name: \"...\", values: ...)"
                        : null)
                {
                    Span = index.Span,
                };
        }
    }
}
