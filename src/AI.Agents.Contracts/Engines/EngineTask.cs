namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Задача движку на один прогон: задание, что сдать, замечания к прошлой попытке и ответ человека.
/// </summary>
/// <remarks>
/// Приемка живет снаружи движка. Движок вернул <see cref="EngineStatus.Done"/>, хост проверил итог и при
/// замечаниях зовет движок снова с тем же <see cref="EngineInput.State"/> и замечаниями в
/// <see cref="Remarks"/>. Так проверки одинаково работают на любом движке.
/// </remarks>
public sealed record EngineTask
{
    /// <summary>Задание.</summary>
    public required string Text { get; init; }

    /// <summary>Что сдать: вид и форма итога; <see langword="null"/>: форма не задана, итог — ответ текстом.</summary>
    public string? Deliverable { get; init; }

    /// <summary>Замечания приемки к прошлой попытке; <see langword="null"/>: попытка первая или замечаний нет.</summary>
    public IReadOnlyList<string>? Remarks { get; init; }

    /// <summary>Ответ человека на вопрос, на котором стоял запуск; <see langword="null"/>: не продолжение после вопроса.</summary>
    public Answer? Answer { get; init; }
}
