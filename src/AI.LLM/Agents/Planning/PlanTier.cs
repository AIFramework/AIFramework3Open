namespace AI.LLM.Agents.Planning;

/// <summary>
/// Ярус плана — группа шагов, выполнимых параллельно (без взаимных зависимостей).
/// Ярусы вычисляются алгоритмом Кана (топологическая сортировка).
/// </summary>
public sealed class PlanTier
{
    /// <summary>Уровень яруса (0 — корень, без зависимостей).</summary>
    public int Level { get; }

    /// <summary>Шаги этого яруса (могут выполняться параллельно).</summary>
    public IReadOnlyList<PlanStep> Steps { get; }

    public PlanTier(int level, IReadOnlyList<PlanStep> steps)
    {
        Level = level;
        Steps = steps ?? [];
    }
}
