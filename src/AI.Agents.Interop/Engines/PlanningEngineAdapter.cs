using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Models;
using AI.LLM.Agents;
using AI.LLM.Agents.Orchestration;
using AI.LLM.Agents.Planning;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.Tools;
using Serilog;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Движок контракта (<see cref="IEngine"/>) над старым <c>PlanningAgent</c>, ключ <see cref="EngineKey"/>:
/// план → исполнение шагов → повтор → перепланирование.
/// </summary>
/// <remarks>
/// <para>
/// <c>PlanningAgent</c> не меняется и собирается публичным <c>PlanningAgentBuilder</c>. Свой исполнитель шагов
/// (<c>Agent</c>) вызывает инструменты через <c>ToolRegistry</c> напрямую, мимо <see cref="IToolSet"/>, и провести
/// его вызовы через гейт эффектов без потерь нельзя. Поэтому шаг плана исполняет цикл ReAct тем же образцом,
/// что и адаптер <c>react</c> (публичный <c>WithStepExecutor</c>): модель через порт, инструменты через гейт.
/// Планировщик видит инструменты по описаниям из <see cref="IToolSet.Available"/>.
/// </para>
/// <para>
/// Настройки шага повторяют старый исполнитель там, где они выражаются: тот же системный промпт
/// (<see cref="EngineContext.Instructions"/>, у старого — <c>WithSystemPrompt</c> планировщика) и тот же предел
/// 30 итераций на попытку шага. Не выражаются: температура 0,15 (<c>ModelAsk.Temperature</c> есть, но цикл ReAct
/// шага ее не задает, и она приходит из настроек порта), шаблон системного промпта и запрос шага (цикл ReAct оборачивает промпт своим
/// шаблоном, а с <c>WithStepExecutor</c> <c>PlanningAgent</c> сам строит запрос с итогами принятых шагов вместо
/// памяти шагов). Список расхождений — в README сборки.
/// </para>
/// <para>
/// Гейт один на весь прогон: повтор шага (<c>MaxStepRetries</c>) и перепланирование не исполняют второй раз уже
/// совершенный эффект. События: начало шага плана — <see cref="EngineEvent.Step"/> с ярусом плана и
/// идентификатором шага в <see cref="StepPath.Branch"/>; события цикла внутри шага идут с тем же путем.
/// </para>
/// </remarks>
public sealed class PlanningEngineAdapter : IEngine
{
    /// <summary>Ключ движка.</summary>
    public const string EngineKey = "plan";

    /// <summary>
    /// Предел итераций цикла одного шага, как у исполнителя шагов старого <c>PlanningAgent</c>
    /// (<c>PlanningAgentBuilder</c>: <c>WithMaxIterations(30)</c>); параметр <c>maxIterations</c> его заменяет.
    /// </summary>
    private const int StepMaxIterations = 30;

    private readonly ILogger? _log;

    /// <summary>Создает адаптер.</summary>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    public PlanningEngineAdapter(ILogger? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public string Key => EngineKey;

    private ILogger Logger => _log ?? Log.Logger;

    /// <inheritdoc />
    public IAsyncEnumerable<EngineEvent> RunAsync(EngineInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return RunCoreAsync(input, cancellationToken);
    }

    private async IAsyncEnumerable<EngineEvent> RunCoreAsync(
        EngineInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (ReActRun.Refusal(input, out EngineOptions options) is { } refusal)
        {
            yield return new EngineEvent.Finished { Result = refusal };
            yield break;
        }

        if (input.Context is { History.Count: > 0 } or { Files.Count: > 0 })
            Logger.Warning("План → движок контракта: история и файлы контекста не передаются: PlanningAgent их не принимает");

        var recorder = new CallRecorder(ReActRun.Gated(input.Tools, _log));
        var run = new PlanRun(input, recorder, options, _log);

        Task<EngineResult> finishing = run.StartAsync(cancellationToken);

        await foreach (EngineEvent evt in run.Events.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
            yield return evt;

        EngineResult result = await finishing.ConfigureAwait(false);
        yield return new EngineEvent.Finished { Result = result };
    }

    /// <summary>Один прогон плана: события идут через канал, пока <c>PlanningAgent.RunAsync</c> работает.</summary>
    private sealed class PlanRun(EngineInput input, CallRecorder tools, EngineOptions options, ILogger? log)
    {
        private readonly Channel<EngineEvent> _events = Channel.CreateUnbounded<EngineEvent>();
        private readonly ReActOutcome _usage = new();
        private StopCode? _budgetStop;

        public ChannelReader<EngineEvent> Events => _events.Reader;

        private ILogger Logger => log ?? Log.Logger;

        public async Task<EngineResult> StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await RunCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _events.Writer.TryComplete();
            }
        }

        private async Task<EngineResult> RunCoreAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (input.Budget.Deadline is DateTimeOffset deadline)
                linked.CancelAfter(deadline - DateTimeOffset.UtcNow);

            PlanningAgent agent = PlanningAgentBuilder.Create()
                .WithLLM(input.Model.AsLlmClient(ModelRole.Decide, log))
                .WithSystemPrompt(input.Context?.Instructions)
                .WithStepExecutor((step, query, token) => ExecuteStepAsync(step, query, linked, token))
                .Build();

            agent.OnStepStarted += (_, step) =>
                _events.Writer.TryWrite(new EngineEvent.Step { Path = PathOf(step), Thought = null });
            agent.OnPlanGenerated += (_, plan) => Spend(plan.Usage);
            agent.OnReplanned += (_, plan) => Spend(plan.Usage);

            try
            {
                PlanningAgentResult result = await agent
                    .RunAsync(ReActRun.TaskText(input.Task), PlannerRegistry(), linked.Token)
                    .ConfigureAwait(false);
                return ToEngineResult(result);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Отмену изнутри сделал бюджет или срок: это недоделанный прогон, а не отмена вызывающим.
                return ReActRun.BudgetStopped(_budgetStop ?? StopCode.TimeLimit, _usage.Usage);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, "План → движок контракта: сбой PlanningAgent");
                return new EngineResult
                {
                    Status = EngineStatus.Failed,
                    StopCode = StopCode.EngineFailure,
                    Unfinished = [ex.Message],
                    Usage = _usage.Usage,
                };
            }
        }

        /// <summary>Шаг плана — цикл ReAct над тем же гейтом; события цикла идут в общий канал.</summary>
        private async Task<AgentResult> ExecuteStepAsync(
            PlanStep step, string query, CancellationTokenSource linked, CancellationToken token)
        {
            StepPath path = PathOf(step);
            var tag = new ToolContext { RunId = input.RunId, Path = path };
            var outcome = new ReActOutcome();

            await foreach (EngineEvent evt in ReActRun.StreamAsync(
                               ReActRun.Build(input, tools, options, log, StepMaxIterations),
                               ReActRun.Query(query, context: null, tag, log),
                               tools,
                               input.Budget,
                               _ => path,
                               outcome,
                               log,
                               token).ConfigureAwait(false))
            {
                if (evt is EngineEvent.Spent spent)
                    _usage.Add(spent.Usage);

                _events.Writer.TryWrite(evt);
            }

            if (outcome.BudgetStop is StopCode stop)
            {
                _budgetStop = stop;
                await linked.CancelAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                linked.Token.ThrowIfCancellationRequested();
            }

            ReActResult? result = outcome.Result;
            return new AgentResult(
                result?.Answer ?? string.Empty, [], result?.Elapsed ?? TimeSpan.Zero, result?.Usage ?? new AgentUsage());
        }

        /// <summary>
        /// Реестр только для планировщика: имена, описания и схемы из набора. Исполнитель шагов свой, поэтому
        /// обработчик реестра не вызывается; если вызван — честный отказ, а не исполнение мимо гейта.
        /// </summary>
        private ToolRegistry PlannerRegistry()
        {
            var registry = new ToolRegistry();
            foreach (ToolSpec spec in tools.Available ?? [])
            {
                registry.Register(spec.Name, spec.Description, PlannerOnly, spec.Parameters.GetRawText());
            }

            return registry;
        }

        private static string PlannerOnly() =>
            "Ошибка: инструмент исполняется только через набор контракта (гейт эффектов), а не через реестр планировщика.";

        private void Spend(AgentUsage? usage)
        {
            if (usage == null || (usage.PromptTokens == 0 && usage.CompletionTokens == 0 && usage.TotalCost == null))
                return;

            var spent = new ModelUsage { InputTokens = usage.PromptTokens, OutputTokens = usage.CompletionTokens, Cost = usage.TotalCost };
            input.Budget.Spend(spent);
            _usage.Add(spent);
            _events.Writer.TryWrite(new EngineEvent.Spent { Usage = spent });
        }

        private static StepPath PathOf(PlanStep step) => new() { Tier = Math.Max(1, step.Tier + 1), Branch = step.Id };

        private EngineResult ToEngineResult(PlanningAgentResult result)
        {
            string? answer = result.Steps.LastOrDefault(s => s.Success)?.AgentResult?.Answer;
            string? text = string.IsNullOrWhiteSpace(answer) ? null : answer;

            if (result.Success)
            {
                return new EngineResult
                {
                    Status = EngineStatus.Done, StopCode = StopCode.FinalAnswer, Text = text, Usage = _usage.Usage,
                };
            }

            if (result.Rejected)
            {
                return new EngineResult
                {
                    Status = EngineStatus.Failed,
                    StopCode = new StopCode(EngineInteropCodes.PlanRejected),
                    Usage = _usage.Usage,
                };
            }

            return new EngineResult
            {
                Status = EngineStatus.Incomplete,
                StopCode = StopCode.NoProgress,
                Text = text,
                Unfinished = result.Steps.Any(s => !s.Success)
                    ? [.. result.Steps.Where(s => !s.Success).Select(s => s.Step.Description)]
                    : ["план не выполнен после перепланирований"],
                Usage = _usage.Usage,
            };
        }
    }
}
