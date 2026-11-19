using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Что видит агент кроме задачи: инструкции, история разговора и файлы. Минимальная форма; новые поля
/// добавляются необязательными.
/// </summary>
public sealed record EngineContext
{
    /// <summary>Инструкции агента (системная часть); <see langword="null"/>: инструкций нет.</summary>
    public string? Instructions { get; init; }

    /// <summary>История разговора до задачи; <see langword="null"/>: истории нет.</summary>
    public IReadOnlyList<ModelMessage>? History { get; init; }

    /// <summary>Файлы, приложенные к задаче; <see langword="null"/>: файлов нет.</summary>
    public IReadOnlyList<ToolFile>? Files { get; init; }
}
