namespace AI.LLM.Agents.ReAct.Tools;

/// <summary>
/// Событие исполняющегося инструмента. Инструмент — поток, а не функция: длинные операции
/// (поиск, генерация документа, обход страниц) должны показывать прогресс, пока идут,
/// а другого канала для этого нет.
/// </summary>
public abstract record ReActToolEvent
{
    /// <summary>
    /// Промежуточное сообщение. <paramref name="Payload"/> движок передаёт наружу нетронутым —
    /// это канал для собственных событий вызывающей стороны.
    /// </summary>
    /// <param name="Message">Человекочитаемое сообщение; может быть <c>null</c>.</param>
    /// <param name="Payload">Произвольные данные вызывающей стороны; может быть <c>null</c>.</param>
    public sealed record Progress(string Message, object Payload = null) : ReActToolEvent;

    /// <summary>Финальный результат. Должен быть последним событием потока.</summary>
    /// <param name="Value">Результат инструмента.</param>
    public sealed record Result(ReActToolOutcome Value) : ReActToolEvent;
}
