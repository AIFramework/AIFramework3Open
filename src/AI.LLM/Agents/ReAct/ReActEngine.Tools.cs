using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// <see cref="ReActEngine"/>: сборка набора инструментов, поиск по имени и исполнение действий
/// шага с ограничением параллелизма и таймаутом.
/// </summary>
public sealed partial class ReActEngine
{
    private List<IReActTool> CollectTools(ReActRunContext context)
    {
        var tools = new List<IReActTool>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IReActToolSource source in _toolSources)
        {
            IEnumerable<IReActTool> produced;
            try
            {
                produced = source.GetTools(context) ?? [];
            }
            catch (Exception ex)
            {
                // Недоступный источник не должен ронять весь ход: остальные инструменты работают.
                Log.Warning(ex, "ReAct: источник инструментов {Source} не отработал", source.GetType().Name);
                continue;
            }

            foreach (IReActTool tool in produced)
            {
                if (tool == null || string.IsNullOrWhiteSpace(tool.Name))
                    continue;

                if (!seen.Add(tool.Name))
                {
                    Log.Warning("ReAct: инструмент {Tool} объявлен дважды, вторая версия пропущена", tool.Name);
                    continue;
                }

                tools.Add(tool);
            }
        }

        return tools;
    }

    private List<IReActSkill> SelectSkills(ReActRunContext context)
    {
        var skills = new List<IReActSkill>();
        foreach (IReActSkill skill in _skills)
        {
            if (skill == null)
                continue;

            bool applicable;
            try
            {
                applicable = skill.IsApplicable(context);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ReAct: навык {Skill} не смог определить применимость", skill.Name);
                applicable = false;
            }

            if (applicable)
                skills.Add(skill);
        }

        return skills;
    }

    /// <summary>
    /// Ищет инструмент по имени, прощая различия в регистре и разделителях: модели устойчиво
    /// путают <c>web_search</c>, <c>web-search</c> и <c>Web Search</c>, и обрывать из-за этого
    /// ход было бы расточительно.
    /// </summary>
    private static IReActTool Resolve(IReadOnlyList<IReActTool> tools, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (IReActTool tool in tools)
        {
            if (string.Equals(tool.Name, name, StringComparison.OrdinalIgnoreCase))
                return tool;
        }

        string normalized = Normalize(name);
        foreach (IReActTool tool in tools)
        {
            if (string.Equals(Normalize(tool.Name), normalized, StringComparison.Ordinal))
                return tool;
        }

        return null;
    }

    private static string Normalize(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Исполняет действия шага и записывает шаг в след. Повторные действия не исполняются —
    /// вместо этого возвращается ранее полученное наблюдение.
    /// </summary>
    private async IAsyncEnumerable<ReActEvent> PerformAsync(
        RunState run,
        int step,
        IReadOnlyList<ReActAction> actions,
        string thought,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var slots = new ReActObservation[actions.Count];
        var pending = new List<PlannedCall>(actions.Count);
        string note = null;

        for (int i = 0; i < actions.Count; i++)
        {
            ReActAction action = actions[i];
            IReActTool tool = Resolve(run.Tools, action.ToolName);
            if (tool == null)
            {
                slots[i] = new ReActObservation
                {
                    Action = action,
                    Ok = false,
                    Text = "инструмент не найден",
                    ErrorCode = "unknown_tool",
                };
                yield return new ReActEvent.Observed(step, slots[i]);
                continue;
            }

            string key = ReActActionKey.Create(action.ToolName, action.Arguments);
            ReActObservation cached = run.Trace.Recall(key);
            if (cached != null)
            {
                // Тот же вызов с тем же аргументом — исполнять заново незачем: результат
                // не изменится, а бюджет шагов уйдёт. Модели возвращаем прежнее наблюдение
                // вместе с замечанием.
                slots[i] = cached;
                note = _template.BuildRepeatedActionNote(tool.Name);
                yield return new ReActEvent.Observed(step, cached);
                continue;
            }

            pending.Add(new PlannedCall(i, action, tool, key));
        }

        if (pending.Count > 0)
        {
            await foreach (ReActEvent evt in ExecuteAsync(run, step, pending, slots, cancellationToken)
                .ConfigureAwait(false))
                yield return evt;
        }

        var observations = new List<ReActObservation>(slots.Length);
        foreach (ReActObservation observation in slots)
        {
            if (observation != null)
                observations.Add(observation);
        }

        run.Trace.Add(new ReActStep
        {
            Number = step,
            Thought = thought,
            Actions = actions,
            Observations = observations,
            Note = note,
        });

        if (note != null)
            yield return new ReActEvent.Note(step, note);
    }

    private async IAsyncEnumerable<ReActEvent> ExecuteAsync(
        RunState run,
        int step,
        IReadOnlyList<PlannedCall> calls,
        ReActObservation[] slots,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ReActEvent>();
        using var terminal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var gate = new SemaphoreSlim(Math.Max(1, _config.MaxParallelTools));

        ReActRunContext toolContext = run.Query.ToRunContext(step);

        Task worker = Task.Run(
            async () =>
            {
                try
                {
                    var tasks = new List<Task>(calls.Count);
                    foreach (PlannedCall call in calls)
                        tasks.Add(RunOneAsync(run, step, call, toolContext, slots, channel.Writer, gate, terminal, cancellationToken));

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            },
            CancellationToken.None);

        try
        {
            await foreach (ReActEvent evt in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return evt;
        }
        finally
        {
            // Потребитель мог прекратить чтение досрочно — тогда работающие инструменты
            // надо остановить, а не бросить выполняться в пустоту.
            terminal.Cancel();
            await worker.ConfigureAwait(false);
        }
    }

    private async Task RunOneAsync(
        RunState run,
        int step,
        PlannedCall call,
        ReActRunContext toolContext,
        ReActObservation[] slots,
        ChannelWriter<ReActEvent> writer,
        SemaphoreSlim gate,
        CancellationTokenSource terminal,
        CancellationToken outer)
    {
        await gate.WaitAsync(outer).ConfigureAwait(false);
        try
        {
            await writer.WriteAsync(new ReActEvent.ToolStarted(step, call.Action), outer).ConfigureAwait(false);

            var stopwatch = Stopwatch.StartNew();
            ReActToolOutcome outcome = null;
            string failure = null;
            string errorCode = null;

            using var timeout = _config.ToolTimeout.HasValue
                ? new CancellationTokenSource(_config.ToolTimeout.Value)
                : null;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                terminal.Token, timeout?.Token ?? CancellationToken.None);

            try
            {
                var invocation = new ReActToolInvocation(call.Action, toolContext);
                await foreach (ReActToolEvent evt in call.Tool
                    .ExecuteAsync(invocation, linked.Token)
                    .WithCancellation(linked.Token)
                    .ConfigureAwait(false))
                {
                    switch (evt)
                    {
                        case ReActToolEvent.Progress progress:
                            await writer
                                .WriteAsync(
                                    new ReActEvent.ToolProgress(step, call.Tool.Name, progress.Message, progress.Payload),
                                    outer)
                                .ConfigureAwait(false);
                            break;

                        case ReActToolEvent.Result result:
                            outcome = result.Value;
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (outer.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeout is { IsCancellationRequested: true })
            {
                failure = "инструмент не уложился в отведённое время";
                errorCode = "timeout";
            }
            catch (OperationCanceledException)
            {
                failure = "вызов прерван: ход завершён другим инструментом";
                errorCode = "superseded";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ReAct: инструмент {Tool} завершился ошибкой", call.Tool.Name);
                failure = "ошибка инструмента: " + ex.Message;
                errorCode = "exception";
            }

            stopwatch.Stop();

            ReActObservation observation = Build(call, outcome, failure, errorCode, stopwatch.Elapsed);

            // Своя ячейка результата у каждого вызова, а вот общий счётчик расходов и словарь
            // повторов — одни на шаг, и писать в них из нескольких вызовов сразу нельзя.
            slots[call.Index] = observation;
            lock (run.Sync)
            {
                run.Trace.Remember(call.Key, observation);
                run.Usage.AddToolResults([
                    new AI.LLM.Agents.Tools.ToolExecutionResult(
                        call.Action.Id, call.Tool.Name, observation.Text, observation.Ok, observation.Elapsed),
                ]);
            }

            await writer.WriteAsync(new ReActEvent.Observed(step, observation), outer).ConfigureAwait(false);

            if (observation.EndsTurn)
                terminal.Cancel();
        }
        finally
        {
            gate.Release();
        }
    }

    private ReActObservation Build(
        PlannedCall call, ReActToolOutcome outcome, string failure, string errorCode, TimeSpan elapsed)
    {
        if (failure != null)
            return new ReActObservation
            {
                Action = call.Action,
                Ok = false,
                Text = failure,
                ErrorCode = errorCode,
                Elapsed = elapsed,
            };

        if (outcome == null)
            return new ReActObservation
            {
                Action = call.Action,
                Ok = false,
                Text = "инструмент не вернул результат",
                ErrorCode = "no_result",
                Elapsed = elapsed,
            };

        string text = outcome.Observation ?? string.Empty;
        if (text.Length > _config.MaxObservationChars)
            text = text[.._config.MaxObservationChars] + "…";

        return new ReActObservation
        {
            Action = call.Action,
            Ok = outcome.Ok,
            Text = text,
            ErrorCode = outcome.ErrorCode,
            Elapsed = elapsed,
            Citations = outcome.Citations,
            Images = outcome.Images,
            EndsTurn = outcome.EndsTurn,
            TerminalAnswer = outcome.TerminalAnswer,
            Payload = outcome.Payload,
        };
    }

    /// <summary>Запланированный вызов: место в шаге, действие, найденный инструмент и ключ повтора.</summary>
    private readonly record struct PlannedCall(int Index, ReActAction Action, IReActTool Tool, string Key);
}
