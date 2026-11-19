using AI.Agents.Contracts.Engines;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Что инструмент знает о запуске, в котором его вызвали: идентификатор запуска и путь шага.
/// </summary>
/// <remarks>
/// Прогресс инструмент отдает кадрами <see cref="ToolUpdate.Progress"/> своего потока, отмена приходит
/// токеном <see cref="ITool.CallAsync(ToolCall, ToolContext, CancellationToken)"/>; второго канала для них в
/// контексте нет, чтобы два пути не расходились. Все прочее, что нужно продукту (сессия, пользователь,
/// память), хост передает наследником этой записи: она не запечатана.
/// </remarks>
public record ToolContext
{
    /// <summary>Идентификатор запуска (непрозрачная строка хоста).</summary>
    public required string RunId { get; init; }

    /// <summary>Путь шага, на котором идет вызов.</summary>
    public required StepPath Path { get; init; }
}
