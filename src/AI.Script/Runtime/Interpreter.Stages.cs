using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Semantics;
using AI.Script.Syntax;
using System.Diagnostics;

namespace AI.Script.Runtime;

/// <summary>
/// Исполнение стадий конвейера: кэш, повторы, собственный таймаут, узел графа прогона.
/// </summary>
/// <remarks>
/// Стадия отличается от функции ровно тем, что за ней наблюдают и её результат можно не
/// считать заново. Всё остальное — то же исполнение тела, поэтому здесь только обвязка, а сам
/// вызов уходит в общий путь.
/// </remarks>
public sealed partial class Interpreter
{
    private int? _currentStage;

    /// <summary>
    /// Выполняет стадию: кэш → повторы → таймаут, с записью в граф прогона.
    /// </summary>
    private async ValueTask<ScriptValue> RunStageAsync(
        ScriptClosure closure,
        Scope scope,
        TextSpan span)
    {
        StageOptions options = closure.Stage ?? StageOptions.Default;
        StageNode node = _context.Graph.Add(closure.Name, _currentStage);

        string? key = CacheKey(closure, scope, out string? notCacheable);

        node.Key = key;
        node.NotCacheable = notCacheable;

        _context.Progress?.StageStarted(node);

        if (key != null && _context.Cache.TryGet(key, out ScriptValue cached))
        {
            node.Outcome = StageOutcome.Cached;
            node.Result = ScriptFormatter.Summary(cached);

            _context.Progress?.StageFinished(node);

            return cached;
        }

        var stopwatch = Stopwatch.StartNew();
        int? previousStage = _currentStage;

        _currentStage = node.Id;

        try
        {
            ScriptValue value = await AttemptStageAsync(closure, scope, span, options, node).ConfigureAwait(false);

            stopwatch.Stop();

            node.Elapsed = stopwatch.Elapsed;
            node.Outcome = StageOutcome.Computed;
            node.Result = ScriptFormatter.Summary(value);

            if (key != null) _context.Cache.Put(key, value);

            _context.Progress?.StageFinished(node);

            return value;
        }
        catch (ScriptError error)
        {
            stopwatch.Stop();

            node.Elapsed = stopwatch.Elapsed;
            node.Outcome = StageOutcome.Failed;
            node.Error = error.Message;

            _context.Progress?.StageFinished(node);

            throw;
        }
        finally
        {
            _currentStage = previousStage;
        }
    }

    /// <summary>
    /// Выполняет тело стадии, повторяя при отказе и соблюдая собственный таймаут.
    /// </summary>
    /// <remarks>
    /// Повторяются только отказы самой стадии. Прерывание прогона (<see cref="ScriptAbort"/>) и
    /// отмена не повторяются никогда: превышенный лимит от повтора не исчезнет, а попытка
    /// продолжить после отмены — это отказ подчиняться отмене.
    /// </remarks>
    private async ValueTask<ScriptValue> AttemptStageAsync(
        ScriptClosure closure,
        Scope scope,
        TextSpan span,
        StageOptions options,
        StageNode node)
    {
        ScriptError? last = null;

        for (int attempt = 1; attempt <= Math.Max(1, options.Attempts); attempt++)
        {
            node.Attempts = attempt;

            try
            {
                // Каждая попытка получает свою область поверх связанных аргументов: имена,
                // введённые сорвавшейся попыткой, не должны мешать следующей.
                return await ExecuteStageBodyAsync(closure, new Scope(scope), span, options).ConfigureAwait(false);
            }
            catch (ScriptAbort)
            {
                throw;
            }
            catch (ScriptError error)
            {
                last = error;

                if (attempt >= options.Attempts) throw;
            }
        }

        throw last ?? new ScriptError(DiagnosticCodes.FunctionFailed, $"стадия '{closure.Name}' не выполнилась");
    }

    /// <summary>
    /// Исполняет тело стадии, при <c>@timeout</c> — под отдельной отменой.
    /// </summary>
    /// <remarks>
    /// Таймаут стадии не подменяет общий таймаут прогона, а добавляется к нему: срабатывает
    /// тот, который наступит раньше. Собственная отмена связана с общей, поэтому отмена
    /// прогона по-прежнему останавливает и стадию.
    /// </remarks>
    private async ValueTask<ScriptValue> ExecuteStageBodyAsync(
        ScriptClosure closure,
        Scope scope,
        TextSpan span,
        StageOptions options)
    {
        if (options.Timeout is not TimeSpan timeout || timeout <= TimeSpan.Zero)
            return await ExecuteClosureBodyAsync(closure, scope, span).ConfigureAwait(false);

        using var source = CancellationTokenSource.CreateLinkedTokenSource(_context.Cancellation);

        source.CancelAfter(timeout);

        CancellationToken previous = _context.PushCancellation(source.Token);

        try
        {
            return await ExecuteClosureBodyAsync(closure, scope, span).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested && !previous.IsCancellationRequested)
        {
            throw new ScriptError(
                DiagnosticCodes.Timeout,
                $"стадия '{closure.Name}' не уложилась в отведённое ей время ({Describe(timeout)})",
                "поднимите '@timeout' либо разбейте стадию на части")
            {
                Span = span,
            };
        }
        finally
        {
            _context.PopCancellation(previous);
        }
    }

    /// <summary>
    /// Ключ кэша стадии.
    /// </summary>
    /// <remarks>
    /// В ключ входят текст стадии, версии модулей и значения аргументов. Текст — потому что
    /// правка тела обязана обесценить прежний результат; версии — потому что тот же текст на
    /// новой библиотеке может считать иначе.
    /// </remarks>
    private string? CacheKey(ScriptClosure closure, Scope scope, out string? notCacheable)
    {
        notCacheable = null;

        StageOptions options = closure.Stage ?? StageOptions.Default;

        if (!options.Cache) return null;

        if (_context.Cache is DisabledStageCache)
        {
            notCacheable = "кэш выключен настройками прогона";

            return null;
        }

        var values = new List<ScriptValue>(closure.Parameters.Count);

        foreach (Syntax.Ast.ParameterNode parameter in closure.Parameters)
        {
            if (scope.TryGet(parameter.Name, out ScriptValue value)) values.Add(value);
        }

        if (ValueDigest.TryBuild(KeyParts(closure), values, out string key, out string? reason)) return key;

        notCacheable = reason;

        return null;
    }

    private IEnumerable<string> KeyParts(ScriptClosure closure)
    {
        yield return closure.Name;
        yield return closure.SourceDigest;

        foreach (IScriptModule module in _registry.Modules) yield return module.Name + "@" + module.Version;
    }

    private static string Describe(TimeSpan value) =>
        value.TotalSeconds < 60 ? $"{value.TotalSeconds:0.###}s" : $"{value.TotalMinutes:0.###}m";
}
