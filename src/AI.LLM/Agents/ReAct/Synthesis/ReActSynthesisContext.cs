namespace AI.LLM.Agents.ReAct.Synthesis;

/// <summary>Данные для итогового обращения к модели.</summary>
public sealed class ReActSynthesisContext
{
    /// <summary>Исходный запрос.</summary>
    public ReActQuery Query { get; init; }

    /// <summary>След прогона.</summary>
    public ReActTrace Trace { get; init; }

    /// <summary>След, отрендеренный в текст. Никогда не <c>null</c>.</summary>
    public string RenderedTrace { get; init; } = string.Empty;

    /// <summary>
    /// Черновик от модели, если при завершении она прислала текст. Может быть <c>null</c>.
    /// </summary>
    public string Draft { get; init; }

    /// <summary>Источники, накопленные за прогон. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<ReActCitation> Citations { get; init; } = [];

    /// <summary>
    /// Почему цикл остановился. Синтезу это важно: при исчерпании бюджета честнее оговорить
    /// неполноту, чем выдавать частичный результат за исчерпывающий.
    /// </summary>
    public ReActStopReason StopReason { get; init; }
}
