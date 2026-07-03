using System.Diagnostics;
using AI.LLM.Agents.Planning;
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
    private readonly PlanGenerator _planner;
    private readonly StepMemory _memory;
    private readonly IStepValidator _validator;
    private readonly PlanningAgentConfig _config;

    /// <summary>Вызывается после генерации нового плана.</summary>
    public event EventHandler<PlanTree> OnPlanGenerated;

    /// <summary>Вызывается перед началом выполнения шага.</summary>
    public event EventHandler<PlanStep> OnStepStarted;

    /// <summary>Вызывается после завершения попытки выполнения шага.</summary>
    public event EventHandler<StepExecutionResult> OnStepCompleted;

    /// <summary>Вызывается при перепланировании (после исчерпания попыток шага).</summary>
    public event EventHandler<PlanTree> OnReplanned;

    internal PlanningAgent(
        Agent agent, PlanGenerator planner, StepMemory memory,
        IStepValidator validator, PlanningAgentConfig config)
    {
        _agent     = agent;
        _planner   = planner;
        _memory    = memory;
        _validator = validator;
        _config    = config;
    }

    /// <summary>
    /// Выполняет задачу: генерирует план и поярусно исполняет шаги с retry/replan.
    /// </summary>
    public async Task<PlanningAgentResult> RunAsync(string goal, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
            throw new ArgumentException("Goal cannot be empty.", nameof(goal));

        var sw          = Stopwatch.StartNew();
        var allSteps    = new List<StepExecutionResult>();
        var replanCount = 0;
        var success     = false;

        var plan = await _planner.GenerateAsync(goal, null, ct).ConfigureAwait(false);
        Log.Information("[PlanningAgent] Plan generated: {Count} steps", plan.Steps.Count);
        OnPlanGenerated?.Invoke(this, plan);
        PrintPlan(plan, replanCount);

        while (replanCount <= _config.MaxReplanAttempts)
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
                var (tierSteps, executionFailure) = await ExecutePlanAsync(plan, goal, ct).ConfigureAwait(false);
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

            plan = await _planner.GenerateAsync(goal, [failureContext], ct).ConfigureAwait(false);
            await _memory.ClearAsync().ConfigureAwait(false);

            OnReplanned?.Invoke(this, plan);
            PrintPlan(plan, replanCount);
        }

        sw.Stop();
        return new PlanningAgentResult(
            goal, allSteps, plan, replanCount, success, sw.Elapsed, _memory.Cells);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task<(List<StepExecutionResult> Steps, string FailureReason)> ExecutePlanAsync(
        PlanTree plan, string goal, CancellationToken ct)
    {
        var results = new List<StepExecutionResult>();

        foreach (var tier in plan.Tiers)
        {
            List<(StepExecutionResult Result, string LastError)> tierResults;

            if (_config.ExecuteParallelTiers)
            {
                var tasks = tier.Steps.Select(s => ExecuteStepWithRetriesAsync(s, goal, ct));
                tierResults = [.. await Task.WhenAll(tasks).ConfigureAwait(false)];
            }
            else
            {
                tierResults = [];
                foreach (var step in tier.Steps)
                    tierResults.Add(await ExecuteStepWithRetriesAsync(step, goal, ct).ConfigureAwait(false));
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
        PlanStep step, string goal, CancellationToken ct)
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
                var query = BuildStepQuery(step, goal, attempt);
                lastResult = await _agent.RunAsync(query, ct).ConfigureAwait(false);
                lastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "[PlanningAgent] Exception in step {StepId} attempt {Attempt}", step.Id, attempt);
                lastResult = null;
                lastError = ex.Message;
            }

            var ok = lastResult is not null && _validator.IsSuccess(step, lastResult);

            if (ok)
            {
                var cell = new StepMemoryEntry(step, lastResult!.Answer, true, attempt);
                await _memory.AddCellAsync(cell).ConfigureAwait(false);

                var stepResult = new StepExecutionResult(step, lastResult, true, false, attempt);
                OnStepCompleted?.Invoke(this, stepResult);
                Console.WriteLine($"  [{step.Id}] ✓ done ({attempt} attempt(s))");
                return (stepResult, null);
            }

            Log.Warning("[PlanningAgent] Step {StepId} attempt {Attempt}/{Max} failed",
                step.Id, attempt, maxAttempts);
        }

        var failedCell = new StepMemoryEntry(
            step, lastResult?.Answer ?? (lastError is not null ? $"FAILED: {lastError}" : "FAILED"),
            false, maxAttempts);
        await _memory.AddCellAsync(failedCell).ConfigureAwait(false);

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
