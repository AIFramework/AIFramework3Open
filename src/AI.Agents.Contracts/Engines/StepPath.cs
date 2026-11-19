namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Где в запуске произошло событие: ярус, ветка внутри яруса и дочерний запуск.
/// </summary>
/// <remarks>
/// Для цикла вида «подумал — вызвал инструмент» ярус — номер шага, ветки нет. Для плана ярус — номер яруса,
/// ветка — узел или параллельный вызов. Событие дочернего запуска поднимается родителю с путем родителя и
/// идентификатором ребенка в <see cref="Child"/>.
/// </remarks>
public sealed record StepPath
{
    /// <summary>Номер яруса (шага цикла или яруса плана), с 1.</summary>
    public required int Tier { get; init; }

    /// <summary>Ветка внутри яруса: узел плана, параллельный вызов; <see langword="null"/>: ветки нет.</summary>
    public string? Branch { get; init; }

    /// <summary>Идентификатор дочернего запуска, из которого пришло событие; <see langword="null"/>: свой запуск.</summary>
    public string? Child { get; init; }
}
