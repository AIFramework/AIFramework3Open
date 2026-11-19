namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Поле вопроса: что спросить и какие ответы предложить.
/// </summary>
public sealed record InputField
{
    /// <summary>Устойчивый ключ поля: под ним ответ приходит в <see cref="Answer.Values"/>.</summary>
    public required string Key { get; init; }

    /// <summary>Формулировка для человека.</summary>
    public required string Text { get; init; }

    /// <summary>Готовые варианты ответа; <see langword="null"/>: ответ свободный.</summary>
    public IReadOnlyList<string>? Choices { get; init; }
}
