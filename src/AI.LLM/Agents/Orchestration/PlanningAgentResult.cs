using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Итоговый результат работы <see cref="PlanningAgent"/>.
/// </summary>
public sealed class PlanningAgentResult
{
    /// <summary>Исходная цель задачи.</summary>
    public string Goal { get; }

    /// <summary>Все выполненные (и проваленные) шаги во всех попытках.</summary>
    public IReadOnlyList<StepExecutionResult> Steps { get; }

    /// <summary>Финальный план (после последнего перепланирования).</summary>
    public PlanTree FinalPlan { get; }

    /// <summary>Число перепланирований (0 — первый план был успешным).</summary>
    public int ReplanCount { get; }

    /// <summary>Задача завершена успешно.</summary>
    public bool Success { get; }

    /// <summary>Полное время выполнения.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>Ячейки памяти — все выполненные шаги с результатами.</summary>
    public IReadOnlyList<StepMemoryEntry> MemoryCells { get; }

    public PlanningAgentResult(
        string goal,
        IReadOnlyList<StepExecutionResult> steps,
        PlanTree finalPlan,
        int replanCount,
        bool success,
        TimeSpan elapsed,
        IReadOnlyList<StepMemoryEntry> memoryCells)
    {
        Goal        = goal;
        Steps       = steps;
        FinalPlan   = finalPlan;
        ReplanCount = replanCount;
        Success     = success;
        Elapsed     = elapsed;
        MemoryCells = memoryCells;
    }
}
