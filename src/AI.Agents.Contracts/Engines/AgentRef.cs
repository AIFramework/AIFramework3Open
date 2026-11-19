namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Ссылка на агента: постоянный идентификатор записи и, при необходимости, ее версия.
/// </summary>
/// <remarks>
/// Агент — это данные, а не класс; ссылка не знает, кто и как его исполнит. Запуск фиксирует версию, поэтому
/// продолжение запуска идет на той же версии, даже если запись с тех пор изменилась.
/// </remarks>
public sealed record AgentRef
{
    /// <summary>Постоянный идентификатор агента (непрозрачная строка хоста).</summary>
    public required string Id { get; init; }

    /// <summary>Версия записи; <see langword="null"/>: действующая версия на момент запуска.</summary>
    public int? Version { get; init; }
}
