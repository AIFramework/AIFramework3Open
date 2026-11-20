using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Interop.Humans;

/// <summary>
/// Контекст повторного вызова инструмента после ответа человека через <c>IHumanChannel</c>: тот же запуск и шаг
/// плюс ответ на вопрос, который инструмент задал итогом <see cref="ToolStatus.AwaitsUser"/> или
/// <see cref="ToolStatus.NeedsInput"/>.
/// </summary>
/// <remarks>
/// Наследник <see cref="ToolContext"/> — способ, который контракт оставил хосту для своих данных; новых полей у
/// <see cref="ToolContext"/> нет. Инструмент читает ответ так: <c>context is AnsweredToolContext answered</c>.
/// </remarks>
public sealed record AnsweredToolContext : ToolContext
{
    /// <summary>Ответ человека на вопрос инструмента.</summary>
    public required Answer Answer { get; init; }
}
