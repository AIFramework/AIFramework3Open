namespace AI.Agents.Contracts.Memory;

/// <summary>Запрос среза памяти под задачу.</summary>
public sealed record MemoryAsk
{
    /// <summary>Область чтения; ставит хост.</summary>
    public required MemoryScope Scope { get; init; }

    /// <summary>Текст задачи или запроса, под который отбирается фокус.</summary>
    public required string Task { get; init; }

    /// <summary>
    /// Бюджет в токенах на всю память хода: профиль, фокус и имена остальных. Постоянный: не растет вместе с
    /// числом фактов.
    /// </summary>
    public required int Budget { get; init; }
}
