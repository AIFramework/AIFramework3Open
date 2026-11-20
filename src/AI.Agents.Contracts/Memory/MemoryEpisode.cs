using AI.Agents.Contracts.Citations;

namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Неизменяемый первоисточник записи: ход разговора, правка человеком, решение по предложению. Факты держатся
/// на эпизодах; снятие эпизода (<see cref="MemoryWrite.Withdraw"/>) снимает его опоры.
/// </summary>
public sealed record MemoryEpisode
{
    /// <summary>Идентификатор эпизода (непрозрачная строка хоста), ключ журнала записи.</summary>
    public required string Id { get; init; }

    /// <summary>Кто произвел эпизод.</summary>
    public required MemoryActor Actor { get; init; }

    /// <summary>Когда эпизод произошел.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>
    /// Текст эпизода: реплика и ответ, из которых извлекаются факты; <see langword="null"/>: текста нет, эпизод
    /// несет только явные изменения (<see cref="MemoryWrite.Changes"/>) или снятие эпизодов.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Что ход прочитал: файлы, страницы, вывод инструментов. По этим опорам реализация отличает сказанное
    /// человеком от содержимого источника, а содержимое источника — это RAG, не память;
    /// <see langword="null"/>: ход ничего не читал.
    /// </summary>
    public IReadOnlyList<Citation>? Read { get; init; }
}
