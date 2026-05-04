namespace AI.LLM.Agents;

/// <summary>
/// Результат работы агента.
/// </summary>
public sealed class AgentResult
{
    /// <summary>Финальный текстовый ответ агента.</summary>
    public string Answer { get; }

    /// <summary>Все шаги, выполненные агентом.</summary>
    public IReadOnlyList<AgentStep> Steps { get; }

    /// <summary>Суммарное количество итераций.</summary>
    public int TotalSteps => Steps.Count;

    /// <summary>Общее время выполнения.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Полная статистика использования: LLM-токены + вызовы инструментов.
    /// Аккумулирует данные по всем итерациям цикла ReAct.
    /// </summary>
    public AgentUsage Usage { get; }

    public AgentResult(string answer, IReadOnlyList<AgentStep> steps, TimeSpan elapsed, AgentUsage usage)
    {
        Answer = answer;
        Steps = steps;
        Elapsed = elapsed;
        Usage = usage;
    }
}
