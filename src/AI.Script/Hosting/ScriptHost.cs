using AI.Script.Binding;
using AI.Script.Docs;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;
using System.Diagnostics;

namespace AI.Script.Hosting;

/// <summary>
/// Точка входа для вызывающего кода: регистрация модулей, проверка и запуск скриптов.
/// </summary>
/// <remarks>
/// Проверка отделена от запуска намеренно: <see cref="Check"/> стоит миллисекунды и не имеет
/// побочных эффектов, поэтому агент может проверить сгенерированный скрипт и исправить его до
/// того, как будет потрачена секунда счёта.
/// </remarks>
public sealed class ScriptHost
{
    private readonly FunctionRegistry _registry = new();

    /// <summary>Реестр зарегистрированных функций.</summary>
    public FunctionRegistry Registry => _registry;

    /// <summary>Регистрирует модуль.</summary>
    public ScriptHost Use(IScriptModule module)
    {
        _registry.Add(module);
        return this;
    }

    /// <summary>
    /// Манифест возможностей: что язык умеет и как это вызвать.
    /// </summary>
    /// <remarks>
    /// Выводится из тех же объектов, что и вызов, поэтому разойтись с реальностью не может.
    /// Для системного промпта модели годится <see cref="ManifestOptions.Index"/>: полный
    /// перечень на сотни функций туда не помещается, а точные сигнатуры модель запрашивает
    /// отдельно — <see cref="Describe"/> либо <see cref="Search"/>.
    /// </remarks>
    public string DescribeCapabilities(ManifestOptions? options = null) =>
        ManifestBuilder.Build(_registry.Modules, options);

    /// <summary>Справка по пространству имён либо по конкретной функции.</summary>
    public string Describe(string query) => Manifest.Describe(_registry.Modules, query);

    /// <summary>Ищет функции по имени и описанию.</summary>
    public IReadOnlyList<ManifestMatch> Search(string query, int limit = 10) =>
        ManifestBuilder.Search(_registry.Modules, query, limit);

    /// <summary>Проверяет скрипт, не выполняя его.</summary>
    /// <param name="source">Текст скрипта.</param>
    /// <param name="fileName">Имя файла для диагностики.</param>
    /// <param name="seeded">Имена данных, которые подаст хост при запуске.</param>
    public CheckResult Check(string source, string fileName = "script.ais", IReadOnlyCollection<string>? seeded = null)
    {
        var text = new SourceText(source ?? string.Empty, fileName);
        var diagnostics = new DiagnosticBag(text);

        _ = Analyse(text, diagnostics, seeded);

        return new CheckResult
        {
            Success = !diagnostics.HasErrors,
            Diagnostics = diagnostics.ToList(),
        };
    }

    /// <summary>Проверяет и выполняет скрипт.</summary>
    public async Task<RunResult> RunAsync(string source, RunOptions? options = null, CancellationToken cancellationToken = default)
    {
        RunOptions effective = (options ?? new RunOptions()).Clone();

        var text = new SourceText(source ?? string.Empty, effective.FileName);
        var diagnostics = new DiagnosticBag(text);
        var stopwatch = Stopwatch.StartNew();

        ScriptUnit unit = Analyse(text, diagnostics, SeededNames(effective));

        if (diagnostics.HasErrors)
        {
            return new RunResult
            {
                Success = false,
                Diagnostics = diagnostics.ToList(),
                Stats = new RunStats { Elapsed = stopwatch.Elapsed },
            };
        }

        ApplyOptions(unit.Options, effective, diagnostics);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (effective.Limits.Timeout is TimeSpan timeout && timeout > TimeSpan.Zero)
            timeoutSource.CancelAfter(timeout);

        var context = new RunContext(effective, _registry, diagnostics, timeoutSource.Token);
        var interpreter = new Interpreter(context, _registry, diagnostics, text);

        bool success = false;

        try
        {
            await interpreter.RunAsync(unit).ConfigureAwait(false);
            success = true;
        }
        catch (ScriptError error)
        {
            // Секрет маскируется и здесь: ключ попадает в вывод не из 'print', а из сообщения
            // библиотеки — из адреса запроса, заголовка, текста отказа службы.
            diagnostics.Error(
                error.Code,
                error.Span ?? default,
                context.Secrets.Apply(error.Message),
                error.Hint == null ? null : context.Secrets.Apply(error.Hint));
        }
        catch (OperationCanceledException)
        {
            bool byUser = cancellationToken.IsCancellationRequested;

            diagnostics.Error(
                byUser ? DiagnosticCodes.Cancelled : DiagnosticCodes.Timeout,
                default,
                byUser ? "прогон отменён" : $"превышен таймаут прогона ({effective.Limits.Timeout})",
                byUser ? null : "поднимите options.timeout либо уменьшите объём работы");
        }
#pragma warning disable CA1031 // Скрипт не должен ронять хост: любой отказ становится диагностикой.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            diagnostics.Error(
                DiagnosticCodes.FunctionFailed, default,
                context.Secrets.Apply($"внутренний отказ: {exception.GetType().Name} — {exception.Message}"));
        }

        stopwatch.Stop();

        return new RunResult
        {
            Success = success && !diagnostics.HasErrors,
            Diagnostics = diagnostics.ToList(),
            Transcript = context.Transcript,
            Emitted = context.Emitted,
            Artifacts = context.Artifacts,
            Graph = context.Graph,
            Stats = new RunStats
            {
                Steps = context.Counters.Steps,
                Allocations = context.Counters.Allocations,
                Calls = context.Calls,
                Stages = context.Graph.Nodes.Count,
                CachedStages = context.Graph.CachedCount,
                ExternalCalls = context.Counters.ExternalCalls,
                ExternalTokens = context.Counters.ExternalTokens,
                ExternalCost = context.Counters.ExternalCost,
                Elapsed = stopwatch.Elapsed,
            },
        };
    }

    private ScriptUnit Analyse(SourceText text, DiagnosticBag diagnostics, IReadOnlyCollection<string>? seeded)
    {
        var parser = new Parser(text, diagnostics);
        ScriptUnit unit = parser.ParseUnit();

        if (!diagnostics.HasErrors) new Checker(diagnostics, _registry, seeded).Check(unit);

        return unit;
    }

    /// <summary>
    /// Переносит рабочую папку скрипта внутрь песочницы хоста.
    /// </summary>
    /// <remarks>
    /// Не «сменить корень», а «углубиться»: путь из скрипта проходит через уже настроенную
    /// песочницу, поэтому <c>workdir: "../.."</c> отклоняется тем же кодом, что и любой другой
    /// выход наружу.
    /// </remarks>
    private static void ApplyWorkdir(RunOptions target, OptionFieldNode field, DiagnosticBag diagnostics)
    {
        if (!target.Sandbox.Enabled)
        {
            diagnostics.Warning(
                DiagnosticCodes.SandboxDenied, field.Span,
                "работа с файлами запрещена настройками прогона: 'workdir' не применён");

            return;
        }

        try
        {
            string full = target.Sandbox.Resolve(field.Value.AsString("options.workdir"), forWriting: true);
            bool readOnly = target.Sandbox is WorkspaceSandbox { IsReadOnly: true };

            target.Sandbox = new WorkspaceSandbox(full, readOnly);
        }
        catch (ScriptError error)
        {
            diagnostics.Error(error.Code, field.Span, error.Message, error.Hint);
        }
    }

    /// <summary>
    /// Применяет <c>options.cache</c>.
    /// </summary>
    /// <remarks>
    /// Скрипт вправе только отказаться от кэша (<c>"off"</c>) либо попросить кэш в памяти
    /// прогона. Назначить папку он не может: где лежат файлы — это решение того, кто запускает,
    /// и передаётся оно через <see cref="RunOptions.Cache"/>. Иначе скрипт из чужих рук
    /// выбирал бы, куда писать на диск хоста.
    /// </remarks>
    private static void ApplyCache(RunOptions target, OptionFieldNode field, DiagnosticBag diagnostics)
    {
        string mode = field.Value.Type == ScriptType.Str
            ? field.Value.AsString("options.cache")
            : field.Value.Type == ScriptType.Bool && field.Value.RawNumber != 0 ? "memory" : "off";

        switch (mode)
        {
            case "off":
                target.Cache = DisabledStageCache.Instance;
                break;

            case "memory":
                target.Cache ??= new MemoryStageCache();
                break;

            default:
                diagnostics.Warning(
                    DiagnosticCodes.UnknownArgument, field.Span,
                    $"неизвестный режим кэша '{mode}'",
                    "известны: \"memory\" — держать в памяти прогона, \"off\" — не кэшировать; " +
                    "папку на диске назначает хост, а не скрипт");
                break;
        }
    }

    private static IReadOnlyCollection<string> SeededNames(RunOptions options)
    {
        if (options.Seeded == null) return [];

        var names = new List<string>(options.Seeded.Count);

        foreach (var pair in options.Seeded) names.Add(pair.Key);

        return names;
    }

    /// <summary>
    /// Переносит блок <c>options</c> в настройки прогона.
    /// </summary>
    /// <remarks>
    /// Значение, закреплённое хостом, не подменяется молча: автор скрипта получает
    /// предупреждение. Молчание означало бы, что политика прогона принадлежит скрипту, а она
    /// принадлежит тому, кто его запускает.
    /// </remarks>
    private static void ApplyOptions(OptionsStmt? options, RunOptions target, DiagnosticBag diagnostics)
    {
        if (options == null) return;

        foreach (OptionFieldNode field in options.Fields)
        {
            if (target.LockedOptions.Contains(field.Name))
            {
                diagnostics.Warning(
                    DiagnosticCodes.NotImplementedYet, field.Span,
                    $"опция '{field.Name}' закреплена хостом: значение из скрипта не применено");

                continue;
            }

            switch (field.Name)
            {
                case "seed":
                    target.Seed = (int)field.Value.AsNumber("options.seed");
                    break;

                case "steps":
                    target.Limits.Steps = (int)field.Value.AsNumber("options.steps");
                    break;

                case "timeout":
                    target.Limits.Timeout = field.Value.Type == ScriptType.Dur
                        ? field.Value.AsDuration("options.timeout")
                        : TimeSpan.FromSeconds(field.Value.AsNumber("options.timeout"));
                    break;

                case "workdir":
                    ApplyWorkdir(target, field, diagnostics);
                    break;

                case "parallel":
                    target.Parallelism = Math.Max(1, (int)field.Value.AsNumber("options.parallel"));
                    break;

                case "cache":
                    ApplyCache(target, field, diagnostics);
                    break;

                case "on_nan":
                    diagnostics.Info(
                        DiagnosticCodes.NotImplementedYet, field.Span,
                        $"опция '{field.Name}' пока не влияет на прогон",
                        "она появится вместе с соответствующим этапом");
                    break;

                default:
                    diagnostics.Warning(
                        DiagnosticCodes.UnknownArgument, field.Span,
                        $"неизвестная опция '{field.Name}'",
                        "известны: seed, steps, timeout, parallel, workdir, cache, on_nan");
                    break;
            }
        }
    }
}
