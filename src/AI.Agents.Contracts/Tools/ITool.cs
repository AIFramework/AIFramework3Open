namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Инструмент: описание и исполнение. Одна форма для прямого вызова, нативного вызова модели и MCP.
/// </summary>
public interface ITool
{
    /// <summary>Описание инструмента.</summary>
    ToolSpec Spec { get; }

    /// <summary>
    /// Вид действия этого вызова до исполнения. Инструмент, у которого вид зависит от аргументов, отвечает
    /// точнее объявленного (например, «нажатие на кнопку отправки» — <see cref="ToolAction.Irreversible"/>).
    /// </summary>
    /// <remarks>
    /// По умолчанию возвращается <see cref="ToolSpec.Action"/>. Гейт трактует ответ через
    /// <see cref="ToolActions.Effective(ToolAction)"/>, поэтому <see cref="ToolAction.Unknown"/> означает
    /// «необратимое». Если вид проявился только при исполнении, инструмент сообщает его в
    /// <see cref="ToolResult.Raised"/>.
    /// </remarks>
    /// <param name="call">Предстоящий вызов.</param>
    /// <returns>Вид действия вызова.</returns>
    ToolAction Classify(ToolCall call) => Spec.Action;

    /// <summary>
    /// Исполнить вызов. Поток: кадры прогресса, последним — итог (правила потока — <see cref="ToolUpdate"/>).
    /// </summary>
    /// <param name="call">Вызов.</param>
    /// <param name="context">Запуск и шаг, в которых идет вызов.</param>
    /// <param name="cancellationToken">Отмена вызова.</param>
    /// <returns>Поток кадров вызова.</returns>
    IAsyncEnumerable<ToolUpdate> CallAsync(ToolCall call, ToolContext context, CancellationToken cancellationToken = default);
}
