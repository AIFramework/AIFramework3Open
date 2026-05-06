using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Результат выполнения одного шага плана агентом.
/// </summary>
public sealed class StepExecutionResult
{
    /// <summary>Шаг плана, который выполнялся.</summary>
    public PlanStep Step { get; }

    /// <summary>Результат работы агента по этому шагу. Null при исключении.</summary>
    public AgentResult AgentResult { get; }

    /// <summary>Шаг выполнен успешно.</summary>
    public bool Success { get; }

    /// <summary>Исчерпаны все попытки и требуется перепланирование.</summary>
    public bool Exhausted { get; }

    /// <summary>Число фактических попыток выполнения.</summary>
    public int Attempts { get; }

    public StepExecutionResult(
        PlanStep step, AgentResult agentResult,
        bool success, bool exhausted, int attempts)
    {
        Step        = step;
        AgentResult = agentResult;
        Success     = success;
        Exhausted   = exhausted;
        Attempts    = attempts;
    }
}
