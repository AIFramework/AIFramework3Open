using System.Runtime.CompilerServices;
using System.Text;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.Agents.Interop.Models;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Abstractions;
using Serilog;
using AgentImage = AI.LLM.Agents.Multimodal.AgentImage;

namespace AI.Agents.Interop.Engines;

/// <summary>Что осталось после прогона ReAct: итог старого движка, отказ бюджета и расход.</summary>
internal sealed class ReActOutcome
{
    private readonly object _sync = new();
    private long _input;
    private long _output;
    private decimal? _cost;
    private bool _spent;

    /// <summary>Итог старого движка; <see langword="null"/>: прогон остановлен бюджетом до итога.</summary>
    public ReActResult? Result { get; set; }

    /// <summary>Код предела, на котором бюджет не дал следующий шаг; <see langword="null"/>: бюджет не останавливал.</summary>
    public StopCode? BudgetStop { get; set; }

    /// <summary>Сумма расхода; <see langword="null"/>: расхода не было.</summary>
    public ModelUsage? Usage
    {
        get
        {
            lock (_sync)
                return _spent ? new ModelUsage { InputTokens = _input, OutputTokens = _output, Cost = _cost } : null;
        }
    }

    /// <summary>Прибавляет расход одного вызова модели.</summary>
    public void Add(ModelUsage usage)
    {
        lock (_sync)
        {
            _spent = true;
            _input += usage.InputTokens;
            _output += usage.OutputTokens;
            if (usage.Cost is decimal cost)
                _cost = (_cost ?? 0m) + cost;
        }
    }
}

/// <summary>
/// Общая часть адаптеров движков: проверки входа, сборка <see cref="ReActEngine"/> через
/// <see cref="ReActAgentBuilder"/>, запрос и перевод потока событий.
/// </summary>
internal static class ReActRun
{
    /// <summary>
    /// Отказ до старта: продолжение с места, неразобранные параметры, истекший срок. <see langword="null"/>: можно идти.
    /// </summary>
    public static EngineResult? Refusal(EngineInput input, out EngineOptions options)
    {
        if (!EngineOptions.TryParse(input.Options, out options, out string? error))
        {
            Log.Warning("Адаптер движка: параметры не разобраны: {Error}", error);
            return new EngineResult
            {
                Status = EngineStatus.Failed,
                StopCode = new StopCode(EngineInteropCodes.InvalidOptions),
                Unfinished = [error!],
            };
        }

        if (input.State != null || input.Task.Answer != null)
        {
            return new EngineResult
            {
                Status = EngineStatus.Failed,
                StopCode = new StopCode(EngineInteropCodes.ContinuationNotSupported),
                Unfinished = ["продолжение с места (State, ответ человека) старый движок не принимает; начните запуск заново"],
            };
        }

        if (input.Budget.Deadline is DateTimeOffset deadline && deadline <= DateTimeOffset.UtcNow)
        {
            return new EngineResult
            {
                Status = EngineStatus.Incomplete,
                StopCode = StopCode.TimeLimit,
                Unfinished = ["срок запуска истек до старта"],
            };
        }

        return null;
    }

    /// <summary>Набор, который всегда идет через гейт эффектов: уже обернутый хостом не оборачивается второй раз.</summary>
    public static IToolSet Gated(IToolSet tools, ILogger? log) =>
        EffectGate.IsGated(tools) ? tools : new EffectGate(tools, log);

    /// <summary>Собирает движок: модель через порт (роль decide), инструменты через набор, срок из бюджета.</summary>
    /// <param name="input">Вход движка.</param>
    /// <param name="tools">Набор инструментов (уже через гейт).</param>
    /// <param name="options">Разобранные параметры движка.</param>
    /// <param name="log">Журнал.</param>
    /// <param name="defaultMaxIterations">
    /// Предел итераций, когда параметр <c>maxIterations</c> не задан; <see langword="null"/> — предел ReActEngine по
    /// умолчанию.
    /// </param>
    public static ReActEngine Build(
        EngineInput input, IToolSet tools, EngineOptions options, ILogger? log, int? defaultMaxIterations = null)
    {
        ILLMClient llm = input.Model.AsLlmClient(ModelRole.Decide, log);
        bool native = options.Decisions switch
        {
            EngineOptions.Native => true,
            EngineOptions.Json => false,
            _ => input.Model.Describe(ModelRole.Decide).ToolCalling,
        };

        ReActAgentBuilder builder = ReActAgentBuilder.Create();
        builder = native ? builder.WithNativeToolCalling(llm) : builder.WithStructuredJson(llm);
        builder.WithToolSource(tools.AsToolSource(log));

        if (input.Context?.Instructions is { } instructions)
            builder.WithSystemPrompt(instructions);

        if ((options.MaxIterations ?? defaultMaxIterations) is int count)
            builder.WithMaxIterations(count);

        if (input.Budget.Deadline is DateTimeOffset deadline)
            builder.WithMaxDuration(deadline - DateTimeOffset.UtcNow);

        return builder.Build();
    }

    /// <summary>
    /// Текст задачи для старого движка: задание, что сдать и замечания приемки к прошлой попытке. Форма
    /// фиксирована: «Что сдать: …» и список «Замечания к прошлой попытке:».
    /// </summary>
    public static string TaskText(EngineTask task)
    {
        var sb = new StringBuilder(task.Text);
        if (!string.IsNullOrWhiteSpace(task.Deliverable))
            sb.Append("\n\nЧто сдать: ").Append(task.Deliverable);

        if (task.Remarks is { Count: > 0 } remarks)
        {
            sb.Append("\n\nЗамечания к прошлой попытке:");
            foreach (string remark in remarks)
                sb.Append("\n- ").Append(remark);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Запрос старого движка: текст, история и картинки из контекста. В метке едет контекст вызова инструмента:
    /// так инструмент и гейт получают идентификатор запуска.
    /// </summary>
    public static ReActQuery Query(string text, EngineContext? context, ToolContext tag, ILogger? log)
    {
        var images = new List<AgentImage>();
        foreach (ToolFile file in context?.Files ?? [])
        {
            if (file.ToImage() is { } image)
                images.Add(image);
            else
                (log ?? Log.Logger).Warning("Адаптер движка: файл {Name} не картинка с байтами, в запрос не передан", file.Name);
        }

        return new ReActQuery(
            text,
            context?.History is { Count: > 0 } history ? ModelTranslation.ToLlmMessages(history) : null,
            images,
            forcedFirstAction: null,
            tag: tag);
    }

    /// <summary>
    /// Прогон старого движка с переводом событий. Итог старого движка кладется в <paramref name="outcome"/>, а не
    /// в поток: у адаптера плана итог свой. Бюджет спрашивается в начале каждого шага (<see cref="IRunBudget.TryStep"/>);
    /// отказ останавливает чтение потока, и следующий шаг не начинается.
    /// </summary>
    public static async IAsyncEnumerable<EngineEvent> StreamAsync(
        ReActEngine engine,
        ReActQuery query,
        CallRecorder recorder,
        IRunBudget budget,
        Func<int, StepPath> pathOf,
        ReActOutcome outcome,
        ILogger? log,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ILogger logger = log ?? Log.Logger;
        var started = new Dictionary<(int Step, string Tool), ToolCall>();
        int step = 0;

        await foreach (ReActEvent evt in engine.StreamAsync(query, cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ReActEvent.Phase { Label: ReActEngine.PhaseStep }:
                    if (!budget.TryStep(out StopCode stop))
                    {
                        outcome.BudgetStop = stop;
                        yield break;
                    }

                    step++;
                    yield return new EngineEvent.Step { Path = pathOf(step) };
                    break;

                case ReActEvent.Thought thought:
                    yield return new EngineEvent.Step { Path = pathOf(thought.Step), Thought = thought.Text };
                    break;

                case ReActEvent.ToolStarted toolStarted:
                {
                    ToolCall call = ToCall(toolStarted.Action);
                    started[(toolStarted.Step, call.Name)] = call;
                    yield return new EngineEvent.ToolStarted { Path = pathOf(toolStarted.Step), Call = call };
                    break;
                }

                case ReActEvent.ToolProgress progress:
                {
                    ToolCall call = started.TryGetValue((progress.Step, progress.Tool), out ToolCall? known)
                        ? known
                        : ToCall(new ReActAction(progress.Tool, string.Empty));
                    yield return new EngineEvent.ToolProgress
                    {
                        Path = pathOf(progress.Step),
                        Call = call,
                        Update = new ToolUpdate.Progress { Message = progress.Message ?? string.Empty, Data = TryJson(progress.Payload, call.Name, logger) },
                    };
                    break;
                }

                case ReActEvent.Observed observed:
                {
                    ReActObservation value = observed.Value;
                    (ToolCall call, ToolResult result) = recorder.TryTake(value.Action.Id, out ToolCall recordedCall, out ToolResult recorded)
                        ? (recordedCall, recorded)
                        : (ToCall(value.Action), ToResult(value, logger));
                    yield return new EngineEvent.ToolFinished { Path = pathOf(observed.Step), Call = call, Result = result };
                    break;
                }

                case ReActEvent.Note note:
                    logger.Debug("ReAct → движок контракта: пометка шага {Step} не переводится: {Text}", note.Step, note.Text);
                    break;

                case ReActEvent.AnswerChunk answer:
                    yield return new EngineEvent.Text { Chunk = answer.Text };
                    break;

                case ReActEvent.ReasoningChunk reasoning:
                    yield return new EngineEvent.Reasoning { Chunk = reasoning.Text };
                    break;

                case ReActEvent.Spent spent:
                    if (spent.Usage.ToUsage() is { } usage)
                    {
                        budget.Spend(usage);
                        outcome.Add(usage);
                        yield return new EngineEvent.Spent { Usage = usage };
                    }

                    break;

                case ReActEvent.Completed completed:
                    outcome.Result = completed.Result;
                    break;
            }
        }
    }

    /// <summary>Итог старого движка → итог контракта по <see cref="ReActEngineTranslationTable.StopReasons"/>.</summary>
    public static EngineResult ToEngineResult(ReActResult result, ModelUsage? usage, ILogger? log)
    {
        ILogger logger = log ?? Log.Logger;
        (EngineStatus status, StopCode code) = ReActEngineTranslationTable.StopReasons[result.StopReason];

        if (result.ModelFailed)
            return ModelFailed(result.Failure!, usage, logger);

        if (result.StopReason == ReActStopReason.EngineFailure)
            logger.Error("ReAct → движок контракта: сбой движка: {Error}", result.Error);

        return new EngineResult
        {
            Status = status,
            StopCode = code,
            Text = result.HasAnswer ? result.Answer : null,
            Payload = TryJson(result.Payload, "итог", logger),
            Evidence = result.Citations.Count > 0 ? [.. result.Citations.Select(c => c.ToCitation())] : null,
            Unfinished = status == EngineStatus.Incomplete
                ? [$"прогон остановлен: {code} после {result.TotalSteps} шагов"]
                : null,
            Usage = usage,
        };
    }

    /// <summary>
    /// Итог при сбое хоста модели — одно место для всех движков: код <see cref="StopCode.ModelFailure"/>, а в
    /// <see cref="EngineResult.Unfinished"/> — код сбоя модели (<see cref="ModelTranslation.FailureOf(Exception)"/>) и
    /// причина. Сбой не выглядит ни успехом, ни поломкой движка.
    /// </summary>
    public static EngineResult ModelFailed(Exception error, ModelUsage? usage, ILogger logger)
    {
        LlmFailure failure = ModelTranslation.FailureOf(error);
        logger.Error(error, "Движок контракта: сбой модели {Failure}: {Message}", failure, error.Message);
        return new EngineResult
        {
            Status = EngineStatus.Failed,
            StopCode = StopCode.ModelFailure,
            Unfinished = [$"{failure}: {error.Message}"],
            Usage = usage,
        };
    }

    /// <summary>Итог, когда бюджет не дал следующий шаг.</summary>
    public static EngineResult BudgetStopped(StopCode code, ModelUsage? usage) => new()
    {
        Status = EngineStatus.Incomplete,
        StopCode = code,
        Unfinished = [$"бюджет запуска не разрешил следующий шаг: {code}"],
        Usage = usage,
    };

    private static ToolCall ToCall(ReActAction action) => new()
    {
        Id = action.Id,
        Name = action.ToolName,
        Arguments = ModelTranslation.ParseArguments(action.Arguments),
    };

    private static ToolResult ToResult(ReActObservation observation, ILogger logger)
    {
        try
        {
            return Outcome(observation, observation.Payload).ToResult(logger);
        }
        catch (InvalidOperationException ex)
        {
            logger.Warning(ex, "ReAct → движок контракта: полезная нагрузка наблюдения не переводится в JSON и опущена");
            return Outcome(observation, payload: null).ToResult(logger);
        }
    }

    private static ReActToolOutcome Outcome(ReActObservation observation, object? payload) => new()
    {
        Ok = observation.Ok,
        Observation = observation.Text,
        Citations = observation.Citations,
        Images = observation.Images,
        EndsTurn = observation.EndsTurn,
        TerminalAnswer = observation.TerminalAnswer,
        Payload = payload,
        ErrorCode = observation.ErrorCode,
    };

    private static System.Text.Json.JsonElement? TryJson(object? value, string what, ILogger logger)
    {
        try
        {
            return ReActTranslation.ToJson(value, what, "движок");
        }
        catch (InvalidOperationException ex)
        {
            logger.Warning(ex, "ReAct → движок контракта: значение не переводится в JSON и опущено");
            return null;
        }
    }
}
