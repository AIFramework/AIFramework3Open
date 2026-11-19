namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Набор инструментов запуска: что доступно и как вызвать. Единственный путь движка к инструментам.
/// </summary>
/// <remarks>
/// Гейт действий живет здесь, в одном месте: до вызова набор берет вид через
/// <see cref="ITool.Classify(ToolCall)"/> и <see cref="ToolActions.Effective(ToolAction)"/> и, если действие
/// не разрешено, отвечает итогом <see cref="ToolStatus.Denied"/> или <see cref="ToolStatus.AwaitsUser"/>
/// вместо вызова. Неизвестное имя дает итог <see cref="ToolStatus.Failed"/>, а не исключение.
/// </remarks>
public interface IToolSet
{
    /// <summary>Описания доступных инструментов; набор может меняться между шагами.</summary>
    IReadOnlyList<ToolSpec> Available { get; }

    /// <summary>
    /// Исполнить вызов. Поток по тем же правилам, что у <see cref="ITool.CallAsync(ToolCall, ToolContext, CancellationToken)"/>.
    /// </summary>
    /// <param name="call">Вызов; имя сравнивается через <see cref="ToolSpec.NameComparer"/>.</param>
    /// <param name="context">Запуск и шаг, в которых идет вызов.</param>
    /// <param name="cancellationToken">Отмена вызова.</param>
    /// <returns>Поток кадров вызова.</returns>
    IAsyncEnumerable<ToolUpdate> CallAsync(ToolCall call, ToolContext context, CancellationToken cancellationToken = default);
}
