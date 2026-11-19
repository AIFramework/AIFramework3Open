namespace AI.Agents.Contracts.Models;

/// <summary>
/// Расход на модель: токены и деньги.
/// </summary>
/// <remarks>
/// Деньги — в той единице, в которой считает реализация <see cref="IModelPort"/>; в пределах одного запуска
/// единица одна. Плательщик в контракте не виден: учет ведет реализация порта.
/// </remarks>
public sealed record ModelUsage
{
    /// <summary>Токены запроса.</summary>
    public required long InputTokens { get; init; }

    /// <summary>Токены ответа, включая рассуждение.</summary>
    public required long OutputTokens { get; init; }

    /// <summary>Стоимость; <see langword="null"/>: провайдер ее не сообщил.</summary>
    public decimal? Cost { get; init; }
}
