namespace AI.Agents.Contracts.Memory;

/// <summary>Одно явное изменение памяти.</summary>
/// <remarks>
/// Какие поля нужны, задает операция: <see cref="MemoryOp.Add"/> — <see cref="Fact"/>;
/// <see cref="MemoryOp.Revise"/> — <see cref="Target"/> и <see cref="Fact"/> с новым значением;
/// <see cref="MemoryOp.Confirm"/> и <see cref="MemoryOp.Retire"/> — <see cref="Target"/>. Изменение без нужных
/// полей реализация отклоняет исходом <see cref="MemoryVerdict.Rejected"/> с причиной, а не исключением.
/// </remarks>
public sealed record MemoryChange
{
    /// <summary>Операция.</summary>
    public required MemoryOp Op { get; init; }

    /// <summary>Факт: новый (Add) или новое значение (Revise); <see langword="null"/>: операции факт не нужен.</summary>
    public MemoryFact? Fact { get; init; }

    /// <summary>
    /// Идентификатор существующего факта (<see cref="MemoryFact.Id"/>) для Revise, Confirm и Retire;
    /// <see langword="null"/>: операция не ссылается на существующий факт (Add).
    /// </summary>
    public string? Target { get; init; }

    /// <summary>Причина изменения словами (по смыслу нужна для Retire); <see langword="null"/>: не указана.</summary>
    public string? Reason { get; init; }
}
