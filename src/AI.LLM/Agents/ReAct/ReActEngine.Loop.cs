using System.Diagnostics;
using System.Runtime.CompilerServices;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Tools;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// <see cref="ReActEngine"/>: сама итерация — решение, проверки, исполнение, наблюдение.
/// </summary>
public sealed partial class ReActEngine
{
    private async IAsyncEnumerable<ReActEvent> RunLoopAsync(
        RunState run, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        int step = 0;
        string note = null;
        int unknownBudget = _config.UnknownToolBudget;
        int malformedBudget = _config.MalformedDecisionBudget;
        int repeatCount = 0;
        bool stopped = false;

        if (run.Query.ForcedFirstAction is { } forced)
        {
            step++;
            await foreach (ReActEvent evt in PerformAsync(run, step, [forced], null, cancellationToken)
                .ConfigureAwait(false))
                yield return evt;

            if (TryFinishOnTerminal(run))
                stopped = true;
        }

        while (!stopped && step < _config.MaxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_config.MaxDuration.HasValue && elapsed.Elapsed >= _config.MaxDuration.Value)
            {
                run.StopReason = ReActStopReason.TimeLimit;
                break;
            }

            step++;
            yield return new ReActEvent.Phase(PhaseStep, step + "/" + _config.MaxIterations);

            var context = new ReActPolicyContext
            {
                Query = run.Query,
                Tools = run.Tools,
                Trace = run.Trace,
                RenderedTrace = _renderer.Render(run.Trace),
                SystemPrompt = run.SystemPrompt,
                StepNumber = step,
                MaxSteps = _config.MaxIterations,
                CorrectiveNote = note,
            };
            note = null;

            ReActDecision decision = await _policy.DecideAsync(context, cancellationToken).ConfigureAwait(false);
            decision ??= ReActDecision.Malformed(null);
            run.Usage.AddLlmUsage(decision.Usage);

            if (_config.EmitThoughts && !string.IsNullOrWhiteSpace(decision.Thought))
                yield return new ReActEvent.Thought(step, decision.Thought);

            if (decision.IsMalformed || (!decision.IsFinal && !decision.HasActions))
            {
                if (malformedBudget-- > 0)
                {
                    note = _template.BuildMalformedDecisionNote();
                    yield return new ReActEvent.Note(step, note);
                    step--;   // подсказка формата не должна съедать бюджет шагов
                    continue;
                }

                run.StopReason = ReActStopReason.PolicyFailure;
                break;
            }

            if (decision.IsFinal)
            {
                run.Draft = decision.FinalText;
                run.StopReason = ReActStopReason.FinalAnswer;
                break;
            }

            string unknown = FindUnknownTool(run.Tools, decision.Actions);
            if (unknown != null)
            {
                if (unknownBudget-- > 0)
                {
                    note = _template.BuildUnknownToolNote(unknown, ToolNames(run.Tools));
                    yield return new ReActEvent.Note(step, note);
                    continue;
                }

                run.StopReason = ReActStopReason.NoProgress;
                break;
            }

            int repeatsInStep = CountRepeats(run.Tools, run.Trace, decision.Actions);
            if (repeatsInStep > 0)
            {
                repeatCount += repeatsInStep;
                if (repeatCount > _config.MaxRepeatedActions)
                {
                    run.StopReason = ReActStopReason.NoProgress;
                    break;
                }
            }

            await foreach (ReActEvent evt in PerformAsync(run, step, decision.Actions, decision.Thought, cancellationToken)
                .ConfigureAwait(false))
                yield return evt;

            if (TryFinishOnTerminal(run))
                break;

            string exhausted = FindExhaustedTool(run.Tools, run.Trace, decision.Actions);
            if (exhausted != null)
            {
                run.StopReason = ReActStopReason.NoProgress;
                yield return new ReActEvent.Note(step, _template.BuildRepeatedFailureNote(exhausted));
                break;
            }
        }

    }

    /// <summary>Ход завершён терминальным инструментом?</summary>
    private static bool TryFinishOnTerminal(RunState run)
    {
        if (run.Trace.Count == 0)
            return false;

        ReActStep last = run.Trace.Steps[^1];
        foreach (ReActObservation observation in last.Observations)
        {
            if (!observation.EndsTurn)
                continue;

            run.StopReason = ReActStopReason.TerminalTool;
            run.Answer = observation.TerminalAnswer;
            run.Payload = observation.Payload;
            return true;
        }

        return false;
    }

    /// <summary>Первое имя инструмента, которого нет в наборе; <c>null</c>, если все известны.</summary>
    private static string FindUnknownTool(IReadOnlyList<IReActTool> tools, IReadOnlyList<ReActAction> actions)
    {
        foreach (ReActAction action in actions)
        {
            if (Resolve(tools, action.ToolName) == null)
                return action.ToolName;
        }

        return null;
    }

    /// <remarks>
    /// Имя берётся у найденного инструмента, а не у действия: в след оно попадает каноническим,
    /// и сравнение с тем, как инструмент назвала модель в этот раз, промахивалось бы мимо
    /// собственных прежних вызовов.
    /// </remarks>
    private static int CountRepeats(
        IReadOnlyList<IReActTool> tools, ReActTrace trace, IReadOnlyList<ReActAction> actions)
    {
        int repeats = 0;
        foreach (ReActAction action in actions)
        {
            string name = Resolve(tools, action.ToolName)?.Name ?? action.ToolName;
            if (trace.Recall(ReActActionKey.Create(name, action.Arguments)) != null)
                repeats++;
        }

        return repeats;
    }

    /// <summary>Инструмент, исчерпавший лимит падений подряд; <c>null</c>, если таких нет.</summary>
    private string FindExhaustedTool(
        IReadOnlyList<IReActTool> tools, ReActTrace trace, IReadOnlyList<ReActAction> actions)
    {
        foreach (ReActAction action in actions)
        {
            string name = Resolve(tools, action.ToolName)?.Name ?? action.ToolName;
            if (trace.TrailingFailures(name) >= _config.MaxConsecutiveFailures)
                return name;
        }

        return null;
    }

    private static List<string> ToolNames(IReadOnlyList<IReActTool> tools)
    {
        var names = new List<string>(tools.Count);
        foreach (IReActTool tool in tools)
            names.Add(tool.Name);

        return names;
    }
}
