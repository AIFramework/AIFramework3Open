namespace AI.LLM.Agents.Tools;

/// <summary>
/// Как источник инструментов понимает строку, которую вернул метод <see cref="AgentToolAttribute"/>.
/// </summary>
/// <remarks>
/// Строка не говорит, удалось ли действие: «файл не найден» и «файл записан» для вызывающего одинаковы. Поэтому
/// выбор делается явно при создании источника, а не молча. Инструмент сообщает отказ без исключения через
/// <see cref="Multimodal.ToolResult.Failure(string, IReadOnlyList{Multimodal.AgentImage})"/>; исключение метода
/// всегда сбой.
/// </remarks>
public enum ToolTextResult
{
    /// <summary>Не выбрано: источник с таким значением не создается.</summary>
    Unspecified = 0,

    /// <summary>
    /// Строка — удача, как было до появления выбора. Годится для инструментов, которые отказывают только
    /// исключением или через <see cref="Multimodal.ToolResult.Failure(string, IReadOnlyList{Multimodal.AgentImage})"/>.
    /// </summary>
    Success = 1,

    /// <summary>
    /// Строка не принимается: метод-инструмент обязан вернуть итог с признаком успеха
    /// (<see cref="Multimodal.ToolResult"/> или <c>Task</c> с ним) либо ничего (<c>void</c>, <c>Task</c>). Метод с
    /// другим типом результата — ошибка при создании источника, а не удача при вызове.
    /// </summary>
    Refused = 2,
}
