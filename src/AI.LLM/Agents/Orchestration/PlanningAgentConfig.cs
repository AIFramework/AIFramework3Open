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
    /// <para>
    /// ВНИМАНИЕ: при параллельном выполнении все шаги яруса разделяют один экземпляр
    /// <see cref="Agent"/>, одни и те же экземпляры инструментов и валидаторов.
    /// Они должны быть потокобезопасны: инструменты с внутренним состоянием
    /// могут приводить к гонкам данных.
    /// </para>
    /// </summary>
    public bool ExecuteParallelTiers { get; set; } = false;
}
