namespace AI.Agents.Contracts.Memory;

/// <summary>Исход одного изменения: что стало с фактом и почему.</summary>
public sealed record MemoryOutcome
{
    /// <summary>Факт, к которому относится исход: записанный, предложенный, закрытый или отклоненный.</summary>
    public required MemoryFact Fact { get; init; }

    /// <summary>Исход.</summary>
    public required MemoryVerdict Verdict { get; init; }

    /// <summary>
    /// Причина словами — для журнала хода и экрана; <see langword="null"/>: причина очевидна из исхода. Для
    /// <see cref="MemoryVerdict.Rejected"/> и <see cref="MemoryVerdict.Archived"/> реализация ее дает.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Существующий факт, к которому отнесена запись: с ним склеен повтор (<see cref="MemoryVerdict.Confirmed"/>)
    /// или его закрыло новое значение (<see cref="MemoryVerdict.Revised"/>); <see langword="null"/>: такого нет.
    /// </summary>
    public string? Existing { get; init; }
}
