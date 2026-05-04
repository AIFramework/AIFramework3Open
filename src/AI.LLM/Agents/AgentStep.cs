using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.LLM.Agents;

/// <summary>
/// Один шаг работы агента (итерация цикла ReAct).
/// </summary>
public sealed class AgentStep
{
    /// <summary>
    /// Порядковый номер шага (начиная с 1).
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// Текстовый ответ ассистента на этом шаге (может быть null если были только tool_calls).
    /// </summary>
    public string AssistantMessage { get; init; }

    /// <summary>
    /// Reasoning-вывод модели (если поддерживается).
    /// </summary>
    public string Reasoning { get; init; }

    /// <summary>
    /// Запрошенные моделью вызовы инструментов.
    /// </summary>
    public List<ToolCall> ToolCalls { get; init; }

    /// <summary>
    /// Результаты выполнения инструментов.
    /// </summary>
    public List<ToolExecutionResult> ToolResults { get; init; }

    /// <summary>
    /// Наблюдение среды, полученное на этом шаге (скриншот, камера, датчики).
    /// Null если <see cref="IObservationProvider"/> не подключён или наблюдение не запрашивалось.
    /// </summary>
    public AgentObservation Observation { get; init; }

    /// <summary>
    /// Причина завершения генерации на этом шаге.
    /// </summary>
    public string FinishReason { get; init; }
}
