using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Сообщение разговора с моделью: части с происхождением, вызовы инструментов со стороны модели и
/// результат инструмента с идентификатором вызова.
/// </summary>
public sealed record ModelMessage
{
    /// <summary>Автор сообщения.</summary>
    public required MessageRole Role { get; init; }

    /// <summary>Части: текст и картинки; <see langword="null"/>: частей нет (например, у сообщения только с вызовами).</summary>
    public IReadOnlyList<MessagePart>? Parts { get; init; }

    /// <summary>Вызовы инструментов, которые сделала модель (<see cref="MessageRole.Assistant"/>); <see langword="null"/>: вызовов нет.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>На какой вызов это результат (<see cref="MessageRole.Tool"/>, <see cref="ToolCall.Id"/>); иначе <see langword="null"/>.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Метка кэша: разговор до этого сообщения включительно провайдер может закэшировать;
    /// <see langword="false"/>: метки нет.
    /// </summary>
    public bool Cache { get; init; }
}
