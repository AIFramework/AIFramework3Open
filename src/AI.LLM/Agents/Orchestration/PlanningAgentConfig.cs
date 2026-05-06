namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Настройки поведения <see cref="PlanningAgent"/>.
/// </summary>
public sealed class PlanningAgentConfig
{
    /// <summary>
    /// Максимальное число повторных попыток для одного шага.
    /// При исчерпании инициируется перепланирование.
    /// </summary>
    public int MaxStepRetries { get; set; } = 2;

    /// <summary>
    /// Максимальное число перепланирований за одну задачу.
    /// </summary>
    public int MaxReplanAttempts { get; set; } = 3;

    /// <summary>
    /// Выполнять шаги одного яруса параллельно (Task.WhenAll).
    /// Если false — шаги каждого яруса выполняются последовательно.
    /// </summary>
    public bool ExecuteParallelTiers { get; set; } = false;
}
