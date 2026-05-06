using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Ячейка памяти выполненного шага плана.
/// Хранит идентификатор, описание, результат выполнения и число попыток.
/// </summary>
public sealed class StepMemoryEntry
{
    /// <summary>Идентификатор шага из плана (step_0, step_1, ...).</summary>
    public string StepId { get; }

    /// <summary>Описание шага.</summary>
    public string Description { get; }

    /// <summary>Имя инструмента, использованного для шага (null если не задан).</summary>
    public string ToolName { get; }

    /// <summary>Текстовый результат выполнения.</summary>
    public string Result { get; }

    /// <summary>Успешно ли выполнен шаг.</summary>
    public bool Success { get; }

    /// <summary>Число попыток до завершения (1 = с первого раза).</summary>
    public int Attempts { get; }

    /// <summary>Время завершения шага.</summary>
    public DateTimeOffset Timestamp { get; }

    public StepMemoryEntry(PlanStep step, string result, bool success, int attempts)
    {
        ArgumentNullException.ThrowIfNull(step);
        StepId      = step.Id ?? "";
        Description = step.Description ?? "";
        ToolName    = step.ToolName;
        Result      = result ?? "";
        Success     = success;
        Attempts    = attempts;
        Timestamp   = DateTimeOffset.UtcNow;
    }
}
