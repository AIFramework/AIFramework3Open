namespace AI.Agents.Contracts.Memory;

/// <summary>Итог записи: статус и исход каждого изменения.</summary>
public sealed record MemoryWriteResult
{
    /// <summary>Статус записи.</summary>
    public required MemoryStatus Status { get; init; }

    /// <summary>
    /// Исходы: по одному на каждое явное или извлеченное изменение и на каждый факт, закрытый снятием эпизода.
    /// Пустой список при <see cref="MemoryStatus.Ok"/> значит «в эпизоде нечего запомнить».
    /// </summary>
    public required IReadOnlyList<MemoryOutcome> Outcomes { get; init; }

    /// <summary>
    /// Причины при <see cref="MemoryStatus.Partial"/> и <see cref="MemoryStatus.Failed"/>;
    /// <see langword="null"/>: проблем нет.
    /// </summary>
    public IReadOnlyList<string>? Problems { get; init; }
}
