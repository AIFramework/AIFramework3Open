namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Срез памяти под задачу в пределах бюджета: профиль, фокус, имена остальных объектов и след отбора.
/// </summary>
/// <remarks>
/// Сборщик контекста кладет срез в ход как недоверенные данные. След отбора (<see cref="Trace"/>) идет в журнал
/// хода: ход знает, что вспомнил, и человек это видит.
/// </remarks>
public sealed record MemoryView
{
    /// <summary>Статус чтения; при <see cref="MemoryStatus.Partial"/> и <see cref="MemoryStatus.Failed"/> причины в <see cref="Problems"/>.</summary>
    public required MemoryStatus Status { get; init; }

    /// <summary>Профиль владельца: попадает всегда, независимо от задачи.</summary>
    public required IReadOnlyList<MemoryFact> Profile { get; init; }

    /// <summary>Факты по задаче, с выводами и их посылками.</summary>
    public required IReadOnlyList<MemoryFact> Focus { get; init; }

    /// <summary>Остальные объекты памяти — только именами, чтобы модель знала, о чем можно спросить.</summary>
    public required IReadOnlyList<MemoryObject> Others { get; init; }

    /// <summary>Почему выбран каждый факт из <see cref="Profile"/> и <see cref="Focus"/>.</summary>
    public required IReadOnlyList<MemoryPick> Trace { get; init; }

    /// <summary>Причины неполного или неудачного чтения; <see langword="null"/>: проблем нет.</summary>
    public IReadOnlyList<string>? Problems { get; init; }
}
