using AI.LLM.Agents.Multimodal;

namespace AI.LLM.Agents.Tools;

/// <summary>
/// Результат выполнения инструмента (текст + опциональные изображения).
/// </summary>
public sealed class ToolExecutionResult
{
    /// <summary>Идентификатор вызова (tool_call_id).</summary>
    public string ToolCallId { get; }

    /// <summary>Имя вызванного инструмента.</summary>
    public string ToolName { get; }

    /// <summary>Текстовый результат выполнения.</summary>
    public string Content { get; }

    /// <summary>Изображения, возвращённые инструментом.</summary>
    public IReadOnlyList<AgentImage> Images { get; }

    /// <summary>Есть ли изображения в результате.</summary>
    public bool HasImages => Images is { Count: > 0 };

    /// <summary>Успешно ли выполнен инструмент.</summary>
    public bool IsSuccess { get; }

    /// <summary>Время выполнения.</summary>
    public TimeSpan Elapsed { get; }

    public ToolExecutionResult(string toolCallId, string toolName, string content, bool isSuccess, TimeSpan elapsed,
        IReadOnlyList<AgentImage> images = null)
    {
        ToolCallId = toolCallId;
        ToolName = toolName;
        Content = content;
        IsSuccess = isSuccess;
        Elapsed = elapsed;
        Images = images ?? [];
    }
}
