using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Runs;

/// <summary>
/// Запись журнала эффектов запуска: какой вызов с каким ключом начат и чем кончился.
/// </summary>
/// <remarks>
/// Запись создается до вызова (итог со статусом <see cref="ToolStatus.Unknown"/>: исход еще не известен) и
/// заменяется итогом после. Поэтому запись со статусом <see cref="ToolStatus.Unknown"/> у потерянного запуска
/// означает «действие могло состояться». Повтор вызова с ключом, у которого итог <see cref="ToolStatus.Ok"/>,
/// инструмент не исполняет: возвращается сохраненный итог.
/// </remarks>
public sealed record EffectRecord
{
    /// <summary>Ключ эффекта (<see cref="ToolCall.EffectKey"/>).</summary>
    public required string Key { get; init; }

    /// <summary>Имя инструмента (<see cref="ToolSpec.Name"/>).</summary>
    public required string Tool { get; init; }

    /// <summary>Итог вызова; <see cref="ToolStatus.Unknown"/>: вызов начат, исход неизвестен.</summary>
    public required ToolResult Result { get; init; }
}
