using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Итог вызова модели: ответ, рассуждение и вызовы инструментов раздельно, статус, код сбоя, фактическая
/// модель и расход.
/// </summary>
/// <remarks>
/// Сбой — это статус и код, а не текст в ответе: потребитель не угадывает, получил он ответ или сообщение
/// об ошибке. Рассуждение в <see cref="Text"/> не входит.
/// </remarks>
public sealed record LlmAnswer
{
    /// <summary>Чем кончился вызов.</summary>
    public required LlmStatus Status { get; init; }

    /// <summary>Ответ модели; <see langword="null"/>: текста нет (сбой, пустой ответ, только вызовы инструментов).</summary>
    public string? Text { get; init; }

    /// <summary>Рассуждение модели; <see langword="null"/>: его нет.</summary>
    public string? Reasoning { get; init; }

    /// <summary>Вызовы инструментов; <see langword="null"/>: вызовов нет.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Код сбоя при <see cref="LlmStatus.Failed"/>; иначе <see langword="null"/>.</summary>
    public LlmFailure? Failure { get; init; }

    /// <summary>Модель, которая фактически ответила (после запасных); <see langword="null"/>: неизвестно.</summary>
    public string? Model { get; init; }

    /// <summary>Расход вызова по всем попыткам; <see langword="null"/>: провайдер расход не сообщил.</summary>
    public ModelUsage? Usage { get; init; }
}
