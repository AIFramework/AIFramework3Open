using AI.Agents.Contracts.Models;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Итог <see cref="ModelClassifier.ClassifyAsync"/>: статус, класс и весь ответ порта.
/// </summary>
public sealed record Classification
{
    /// <summary>Чем кончилась классификация.</summary>
    public required ClassificationStatus Status { get; init; }

    /// <summary>Класс в том виде, в каком он объявлен, при <see cref="ClassificationStatus.Ok"/>; иначе <see langword="null"/>.</summary>
    public string? Class { get; init; }

    /// <summary>Ответ порта как есть: текст модели, код и текст сбоя, модель, расход.</summary>
    public required LlmAnswer Answer { get; init; }
}

/// <summary>
/// Чем кончилась классификация. Сбой, пустой ответ и ответ без класса различаются: ни один из них не выглядит как
/// класс.
/// </summary>
public enum ClassificationStatus
{
    /// <summary>Не задано. Успехом не считается.</summary>
    Unknown = 0,

    /// <summary>Класс найден в ответе.</summary>
    Ok,

    /// <summary>Модель ответила, но ни одного слова из списка классов в ответе нет.</summary>
    NoClass,

    /// <summary>Модель не написала ответа.</summary>
    Empty,

    /// <summary>Вызов модели не состоялся; код и текст сбоя в <see cref="Classification.Answer"/>.</summary>
    Failed,
}
