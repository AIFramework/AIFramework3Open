namespace AI.LLM.Agents.Planning;

/// <summary>
/// Дерево плана: все шаги + ярусная декомпозиция по алгоритму Кана.
/// </summary>
public sealed class PlanTree
{
    /// <summary>Исходная задача (цель).</summary>
    public string Goal { get; }

    /// <summary>Все шаги плана в порядке топологической сортировки.</summary>
    public IReadOnlyList<PlanStep> Steps { get; }

    /// <summary>Обнаружен цикл в зависимостях (план невалиден).</summary>
    public bool HasCycle { get; }

    /// <summary>Ярусы: группы параллельно выполнимых шагов.</summary>
    public IReadOnlyList<PlanTier> Tiers { get; }

    /// <summary>Количество ярусов (глубина плана).</summary>
    public int Depth => Tiers?.Count ?? 0;

    /// <summary>Статистика использования LLM при генерации.</summary>
    public AgentUsage Usage { get; }

    public PlanTree(string goal, IReadOnlyList<PlanStep> steps,
        IReadOnlyList<PlanTier> tiers, bool hasCycle, AgentUsage usage)
    {
        Goal = goal;
        Steps = steps;
        Tiers = tiers ?? [];
        HasCycle = hasCycle;
        Usage = usage;
    }
}
