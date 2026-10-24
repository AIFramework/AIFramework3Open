using System.Diagnostics;
using AI.LLM.Agents.Planning;
using AI.LLM.Agents.Tools;
using Serilog;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Оркестратор высокого уровня: Планирование → Выполнение → Повтор → Перепланирование.
/// <para>
/// Цикл работы:
/// <list type="number">
/// <item>Генерирует план через <see cref="PlanGenerator"/>.</item>
/// <item>Поярусно выполняет каждый шаг через внутренний <see cref="Agent"/>.</item>
/// <item>При провале шага — повторяет до <see cref="PlanningAgentConfig.MaxStepRetries"/> раз.</item>
/// <item>При исчерпании попыток — перегенерирует план с контекстом провала.</item>
/// </list>
/// </para>
/// <para>
/// Вся история выполненных шагов накапливается в <see cref="StepMemory"/> и
/// автоматически передаётся агенту при каждом следующем шаге.
/// </para>
/// </summary>
public sealed class PlanningAgent
{
    private readonly Agent _agent;
    private readonly Func<ToolRegistry, StepMemory, Agent> _agentFactory;
    private readonly PlanGenerator _planner;
    private readonly StepMemory _memory;
    private readonly IStepValidator _validator;
    private readonly IAsyncStepValidator _asyncValidator;
    private readonly PlanningAgentConfig _config;
    private readonly Func<PlanStep, string, CancellationToken, Task<AgentResult>> _stepExecutor;
    private readonly Func<PlanTree, CancellationToken, Task<PlanTree>> _planApproval;

    /// <summary>Сколько знаков итога принятого шага попадает в запрос следующего шага своему исполнителю.</summary>
    private const int MaxPriorResultChars = 600;

    /// <summary>
    /// Контекст одной задачи: исполняющий агент, реестр инструментов для планировщика и память шагов.
    /// </summary>
    /// <remarks>
    /// Введён ради <see cref="RunAsync(string, IEnumerable{object}, CancellationToken)"/>: инструменты
    /// задачи должны попасть И планировщику, И исполнителю. Дать их только планировщику — значит
    /// получить план, ссылающийся на инструменты, которых у исполняющего агента нет.
    /// </remarks>
    private sealed record RunScope(Agent Agent, ToolRegistry PlannerTools, StepMemory Memory);

    /// <summary>Вызывается после генерации нового плана.</summary>
    public event EventHandler<PlanTree> OnPlanGenerated;

    /// <summary>Вызывается перед началом выполнения шага.</summary>
    public event EventHandler<PlanStep> OnStepStarted;

    /// <summary>Вызывается после завершения попытки выполнения шага.</summary>
    public event EventHandler<StepExecutionResult> OnStepCompleted;

    /// <summary>Вызывается при перепланировании (после исчерпания попыток шага).</summary>
    public event EventHandler<PlanTree> OnReplanned;

    /// <summary>Попытка шага не принята, и он пойдёт снова: в результате — отклонённый ответ и номер попытки.</summary>
    public event EventHandler<StepExecutionResult> OnStepRetry;

    internal PlanningAgent(
        Agent agent, Func<ToolRegistry, StepMemory, Agent> agentFactory,
        PlanGenerator planner, StepMemory memory,
        IStepValidator validator, IAsyncStepValidator asyncValidator,
        PlanningAgentConfig config,
        Func<PlanStep, string, CancellationToken, Task<AgentResult>> stepExecutor = null,
        Func<PlanTree, CancellationToken, Task<PlanTree>> planApproval = null)
    {
        _agent          = agent;
        _agentFactory   = agentFactory;
        _planner        = planner;
        _memory         = memory;
        _validator      = validator;
        _asyncValidator = asyncValidator;
        _config         = config;
        _stepExecutor   = stepExecutor;
        _planApproval   = planApproval;
    }

    /// <summary>
    /// Принимает результат шага. Асинхронная приёмка (например по <see cref="PlanStep.DoneWhen"/>
    /// через модель) имеет приоритет — синхронная остаётся для реализаций без ввода-вывода.
    /// </summary>
    private Task<bool> ValidateAsync(PlanStep step, AgentResult result, CancellationToken ct) =>
        _asyncValidator is not null
            ? _asyncValidator.IsSuccessAsync(step, result, ct)
            : Task.FromResult(_validator.IsSuccess(step, result));

    /// <summary>
    /// Выполняет задачу инструментами, заданными при сборке оркестратора.
    /// </summary>
    public Task<PlanningAgentResult> RunAsync(string goal, CancellationToken ct = default)
        => RunCoreAsync(goal, new RunScope(_agent, null, _memory), ct);

    /// <summary>
    /// Выполняет задачу ЯВНЫМ списком инструментов — только для этого запуска.
    /// </summary>
    /// <param name="goal">Задача.</param>
    /// <param name="tools">
    /// Экземпляры с методами <c>[AgentTool]</c>. Список ЗАМЕЩАЕТ инструменты сборки, а не дополняет:
    /// нужен базовый набор плюс задачные — передайте оба. Пустой список — осознанное «инструментов
    /// нет»: агент ответит текстом.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Экземпляр не объявляет ни одного метода с <c>[AgentTool]</c>.
    /// </exception>
    /// <remarks>
    /// Набор уходит И планировщику, И исполняющему агенту: иначе план ссылался бы на инструменты,
    /// которых у исполнителя нет. Запуск полностью изолирован — свой агент, свой реестр и своя
    /// память шагов, — поэтому один экземпляр <see cref="PlanningAgent"/> можно звать параллельно
    /// с разными наборами, не мешая соседним задачам.
    /// </remarks>
    public Task<PlanningAgentResult> RunAsync(
        string goal, IEnumerable<object> tools, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var instances = tools as object[] ?? [.. tools];
        EnsureDeclaresTools(instances, nameof(tools));

        var registry = ToolRegistry.FromObjects(instances);
        var memory   = new StepMemory();
        return RunCoreAsync(goal, new RunScope(_agentFactory(registry, memory), registry, memory), ct);
    }

    /// <summary>
    /// Выполняет задачу готовым реестром инструментов — только для этого запуска.
    /// </summary>
    /// <remarks>
    /// Путь для инструментов, зарегистрированных с именами из рантайма — например агентов каталога,
    /// отданных как инструменты через
    /// <see cref="ToolRegistry.Register(string, string, Delegate, string)"/>. Атрибутный путь
    /// (<see cref="RunAsync(string, IEnumerable{object}, CancellationToken)"/>) такой набор выразить
    /// не может: имя там статично на тип, и однотипные носители затирали бы друг друга.
    /// <para>
    /// Один и тот же реестр уходит планировщику и исполнителю, поэтому план не может сослаться
    /// на инструмент, которого нет у исполняющего агента.
    /// </para>
    /// </remarks>
    public Task<PlanningAgentResult> RunAsync(
        string goal, ToolRegistry registry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var memory = new StepMemory();
        return RunCoreAsync(goal, new RunScope(_agentFactory(registry, memory), registry, memory), ct);
    }

    /// <summary>
    /// Проверяет, что каждый переданный экземпляр действительно объявляет инструменты.
    /// </summary>
    /// <remarks>
    /// Набор принимается как <see cref="object"/> — контракта у инструментов нет, только атрибут
    /// <c>[AgentTool]</c>. Чужой экземпляр (опечатка, забытый атрибут, не тот объект) молча даёт
    /// пустой реестр: планировщик не увидит инструментов, исполнитель их не получит, и задача тихо
    /// выродится в текстовый ответ без единого предупреждения. Поэтому падаем здесь и называем
    /// виноватый тип. Пустой СПИСОК при этом остаётся законным — это явное «инструментов нет».
    /// </remarks>
    private static void EnsureDeclaresTools(IReadOnlyList<object> instances, string paramName)
    {
        var bad = instances
            .Where(i => i is null || !ToolRegistry.DeclaresTools(i))
            .Select(i => i?.GetType().Name ?? "null")
            .ToList();

        if (bad.Count == 0) return;

        throw new ArgumentException(
            $"Не объявляют ни одного метода с [AgentTool]: {string.Join(", ", bad)}. "
            + "Чтобы запустить задачу вовсе без инструментов, передайте пустой список.",
            paramName);
    }

    private async Task<PlanningAgentResult> RunCoreAsync(
        string goal, RunScope scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(goal))
            throw new ArgumentException("Goal cannot be empty.", nameof(goal));

        // Новая задача — чистая память шагов. Без этого повторный вызов на том же экземпляре
        // тащил бы в контекст шаги предыдущей задачи (и ссылки на инструменты, которых
        // в текущем наборе уже нет).
        await scope.Memory.ClearAsync().ConfigureAwait(false);

        var sw          = Stopwatch.StartNew();
        var allSteps    = new List<StepExecutionResult>();
        var replanCount = 0;
        var success     = false;

        var plan = await _planner.GenerateAsync(goal, null, scope.PlannerTools, ct).ConfigureAwait(false);
        Log.Information("[PlanningAgent] Plan generated: {Count} steps", plan.Steps.Count);
        OnPlanGenerated?.Invoke(this, plan);
        PrintPlan(plan, replanCount);
        (plan, var rejected) = await ApproveAsync(plan, ct).ConfigureAwait(false);

        while (!rejected && replanCount <= _config.MaxReplanAttempts)
        {
            ct.ThrowIfCancellationRequested();

            // Валидация плана перед выполнением: циклический или пустой план
            // не выполняет ни одного шага и не должен считаться успехом.
            string failureReason;
            if (plan.HasCycle)
            {
                failureReason = "План содержит цикл зависимостей между шагами и не может быть выполнен.";
            }
            else if (plan.Steps.Count == 0 || plan.Tiers.Count == 0)
            {
                failureReason = "План пуст: LLM не вернул шагов или ответ не удалось распарсить.";
            }
            else
            {
                var (tierSteps, executionFailure) = await ExecutePlanAsync(plan, goal, scope, ct).ConfigureAwait(false);
                allSteps.AddRange(tierSteps);
                failureReason = executionFailure;
            }

            if (failureReason is null)
            {
                success = true;
                break;
            }

            replanCount++;
            if (replanCount > _config.MaxReplanAttempts)
            {
                Log.Warning("[PlanningAgent] Replan limit reached ({Count})", _config.MaxReplanAttempts);
                break;
            }

            Log.Warning("[PlanningAgent] Replan #{Count}: {Reason}", replanCount, failureReason);
            Console.WriteLine($"\n[Replan #{replanCount}] {failureReason}\n");

            var failureContext = new Skill(
                "previous_failure",
                $"Attempt {replanCount} failed: {failureReason}. Build a different approach.");

            plan = await _planner.GenerateAsync(goal, [failureContext], scope.PlannerTools, ct)
                .ConfigureAwait(false);
            await scope.Memory.ClearAsync().ConfigureAwait(false);

            OnReplanned?.Invoke(this, plan);
            PrintPlan(plan, replanCount);
            (plan, rejected) = await ApproveAsync(plan, ct).ConfigureAwait(false);
        }

        sw.Stop();
        return new PlanningAgentResult(
            goal, allSteps, plan, replanCount, success, sw.Elapsed, scope.Memory.Cells, rejected);
    }

    /// <summary>
    /// План на утверждение (<see cref="PlanningAgentBuilder.WithPlanApproval"/>): утверждённый — возможно
    /// поправленный, Rejected — не утверждён. Пустой и циклический план не показывается: он не выполнится и
    /// уйдёт на перепланирование сам.
    /// </summary>
    private async Task<(PlanTree Plan, bool Rejected)> ApproveAsync(PlanTree plan, CancellationToken ct)
    {
        if (_planApproval is null || plan.HasCycle || plan.Steps.Count == 0)
            return (plan, false);

        var approved = await _planApproval(plan, ct).ConfigureAwait(false);
        return approved is null ? (plan, true) : (approved, false);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task<(List<StepExecutionResult> Steps, string FailureReason)> ExecutePlanAsync(
        PlanTree plan, string goal, RunScope scope, CancellationToken ct)
    {
        var results = new List<StepExecutionResult>();

        foreach (var tier in plan.Tiers)
        {
            List<(StepExecutionResult Result, string LastError)> tierResults;

            if (_config.ExecuteParallelTiers)
            {
                var tasks = tier.Steps.Select(s => ExecuteStepWithRetriesAsync(s, goal, scope, ct));
                tierResults = [.. await Task.WhenAll(tasks).ConfigureAwait(false)];
            }
            else
            {
                tierResults = [];
                foreach (var step in tier.Steps)
                    tierResults.Add(await ExecuteStepWithRetriesAsync(step, goal, scope, ct).ConfigureAwait(false));
            }

            results.AddRange(tierResults.Select(r => r.Result));

            var exhausted = tierResults.FirstOrDefault(r => r.Result.Exhausted);
            if (exhausted.Result is not null)
            {
                var reason = $"Step '{exhausted.Result.Step.Description}' failed after {exhausted.Result.Attempts} attempts";
                if (!string.IsNullOrWhiteSpace(exhausted.LastError))
                    reason += $": {exhausted.LastError}";
                return (results, reason);
            }
        }

        return (results, null);
    }

    private async Task<(StepExecutionResult Result, string LastError)> ExecuteStepWithRetriesAsync(
        PlanStep step, string goal, RunScope scope, CancellationToken ct)
    {
        OnStepStarted?.Invoke(this, step);

        AgentResult lastResult = null;
        string lastError = null;
        var maxAttempts = _config.MaxStepRetries + 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var retryNote = attempt > 1 ? $" (retry {attempt - 1}/{_config.MaxStepRetries})" : "";
            Console.WriteLine($"  [{step.Id}]{retryNote} {step.Description}");

            try
            {
                lastResult = _stepExecutor is null
                    ? await scope.Agent.RunAsync(BuildStepQuery(step, goal, attempt), ct).ConfigureAwait(false)
                    : await _stepExecutor(step, BuildExecutorQuery(step, goal, attempt, scope.Memory.Cells, lastResult), ct).ConfigureAwait(false);
                lastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "[PlanningAgent] Exception in step {StepId} attempt {Attempt}", step.Id, attempt);
                lastResult = null;
                lastError = ex.Message;
            }

            var ok = lastResult is not null
                && await ValidateAsync(step, lastResult, ct).ConfigureAwait(false);

            if (ok)
            {
                var cell = new StepMemoryEntry(step, lastResult!.Answer, true, attempt);
                await scope.Memory.AddCellAsync(cell).ConfigureAwait(false);

                var stepResult = new StepExecutionResult(step, lastResult, true, false, attempt);
                OnStepCompleted?.Invoke(this, stepResult);
                Console.WriteLine($"  [{step.Id}] ✓ done ({attempt} attempt(s))");
                return (stepResult, null);
            }

            Log.Warning("[PlanningAgent] Step {StepId} attempt {Attempt}/{Max} failed",
                step.Id, attempt, maxAttempts);
            if (attempt < maxAttempts)
                OnStepRetry?.Invoke(this, new StepExecutionResult(step, lastResult, false, false, attempt));
        }

        var failedCell = new StepMemoryEntry(
            step, lastResult?.Answer ?? (lastError is not null ? $"FAILED: {lastError}" : "FAILED"),
            false, maxAttempts);
        await scope.Memory.AddCellAsync(failedCell).ConfigureAwait(false);

        var failedResult = new StepExecutionResult(step, lastResult, false, true, maxAttempts);
        OnStepCompleted?.Invoke(this, failedResult);
        Console.WriteLine($"  [{step.Id}] ✗ exhausted");
        return (failedResult, lastError);
    }

    private static string BuildStepQuery(PlanStep step, string goal, int attempt)
    {
        var retry = attempt > 1 ? $" — retry {attempt - 1}" : "";
        return $"Overall goal: {goal}\n\nExecute this step{retry}: {step.Description}";
    }

    /// <summary>
    /// Запрос своему исполнителю (<see cref="PlanningAgentBuilder.WithStepExecutor"/>): памяти шагов у него нет,
    /// поэтому итоги принятых шагов идут в запрос. Критерий готовности — чтобы исполнитель знал, по чему шаг
    /// примут; на повторе — отклонённый ответ прошлой попытки.
    /// </summary>
    private static string BuildExecutorQuery(
        PlanStep step, string goal, int attempt, IReadOnlyList<StepMemoryEntry> done, AgentResult rejected)
    {
        var query = new System.Text.StringBuilder();
        query.Append("Общая задача: ").Append(goal).Append("\n\n");
        var accepted = done.Where(c => c.Success).ToList();
        if (accepted.Count > 0)
        {
            query.Append("Уже сделано:\n");
            foreach (var cell in accepted)
            {
                var result = cell.Result.Length > MaxPriorResultChars ? cell.Result[..MaxPriorResultChars] + "…" : cell.Result;
                query.Append("- ").Append(cell.Description).Append(" → ").Append(result.Replace("\n", " ")).Append('\n');
            }
            query.Append('\n');
        }
        query.Append("Сделай этот шаг: ").Append(step.Description).Append('\n');
        if (!string.IsNullOrWhiteSpace(step.DoneWhen))
            query.Append("Шаг примут, когда: ").Append(step.DoneWhen.Trim()).Append('\n');
        if (attempt > 1 && !string.IsNullOrWhiteSpace(rejected?.Answer))
        {
            var answer = rejected.Answer.Length > MaxPriorResultChars ? rejected.Answer[..MaxPriorResultChars] + "…" : rejected.Answer;
            query.Append("\nПрошлая попытка не принята (критерий не выполнен). Её ответ: ").Append(answer).Append('\n');
        }
        return query.ToString();
    }

    private static void PrintPlan(PlanTree plan, int replanIndex)
    {
        var header = replanIndex == 0 ? "Generated plan" : $"Replanned plan #{replanIndex}";
        Console.WriteLine($"\n=== {header} ({plan.Steps.Count} steps) ===");
        foreach (var tier in plan.Tiers)
        {
            foreach (var step in tier.Steps)
            {
                var tool = step.ToolName is not null ? $" [{step.ToolName}]" : "";
                Console.WriteLine($"  {step.Id}{tool}: {step.Description}");
            }
        }
        Console.WriteLine();
    }
}
