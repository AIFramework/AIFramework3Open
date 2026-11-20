namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Объект памяти: конкретная вещь (человек, машина, проект, дом), о которой владелец что-то сказал и которую
/// надо помнить дольше одного ответа. Факты — его свойства (<see cref="MemoryFact"/>).
/// </summary>
public sealed record MemoryObject
{
    /// <summary>
    /// Устойчивый идентификатор в памяти владельца. Не выводится из имени: имя можно поменять, а тезки остаются
    /// разными объектами.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Имя для показа и для строки «остальные объекты».</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Вид объекта из графа знаний, ровно один (идентификатор вида); <see langword="null"/>: вид не известен.
    /// </summary>
    public string? Kind { get; init; }
}
