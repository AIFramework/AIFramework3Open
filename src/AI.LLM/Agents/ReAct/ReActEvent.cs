namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Событие потока цикла. Набор вариантов подобран так, чтобы одного потока хватило всем трём
/// способам наблюдать за агентом: вытягивающему (<c>await foreach</c>), проталкивающему
/// (<see cref="IProgress{T}"/>) и «просто дождаться результата».
/// </summary>
/// <remarks>
/// Поток заменяет собой события экземпляра (<c>event EventHandler</c>). У движка нет состояния
/// прогона, поэтому один экземпляр обслуживает параллельные запуски; события экземпляра при
/// этом перемешивали бы разные прогоны между собой.
/// </remarks>
public abstract record ReActEvent
{
    /// <summary>
    /// Именованная фаза работы. Договорённость для интерфейса: событие с тем же
    /// <paramref name="Label"/> обновляет предыдущую строку, а не добавляет новую, — иначе
    /// панель шагов вырастает до сотен записей на одной длинной операции.
    /// </summary>
    /// <param name="Label">Название фазы.</param>
    /// <param name="Detail">Уточнение; может быть <c>null</c>.</param>
    public sealed record Phase(string Label, string Detail = null) : ReActEvent;

    /// <summary>Рассуждение шага.</summary>
    /// <param name="Step">Номер шага, начиная с единицы.</param>
    /// <param name="Text">Текст рассуждения.</param>
    public sealed record Thought(int Step, string Text) : ReActEvent;

    /// <summary>Начало вызова инструмента.</summary>
    /// <param name="Step">Номер шага.</param>
    /// <param name="Action">Выполняемое действие.</param>
    public sealed record ToolStarted(int Step, ReActAction Action) : ReActEvent;

    /// <summary>
    /// Промежуточное сообщение изнутри инструмента. <paramref name="Payload"/> движок не
    /// интерпретирует и передаёт наружу нетронутым: так собственные события вызывающей стороны
    /// (элементы интерфейса, прогресс длинной генерации) проходят через цикл, не затаскивая
    /// продуктовые типы в библиотеку.
    /// </summary>
    /// <param name="Step">Номер шага.</param>
    /// <param name="Tool">Имя инструмента.</param>
    /// <param name="Message">Человекочитаемое сообщение; может быть <c>null</c>.</param>
    /// <param name="Payload">Произвольные данные вызывающей стороны; может быть <c>null</c>.</param>
    public sealed record ToolProgress(int Step, string Tool, string Message, object Payload = null) : ReActEvent;

    /// <summary>Наблюдение — результат инструмента.</summary>
    /// <param name="Step">Номер шага.</param>
    /// <param name="Value">Наблюдение.</param>
    public sealed record Observed(int Step, ReActObservation Value) : ReActEvent;

    /// <summary>
    /// Служебная пометка движка: повтор действия, неизвестный инструмент, неразобранный ответ.
    /// Полезна для диагностики — видно, как цикл поправлял модель.
    /// </summary>
    /// <param name="Step">Номер шага.</param>
    /// <param name="Text">Текст пометки.</param>
    public sealed record Note(int Step, string Text) : ReActEvent;

    /// <summary>Фрагмент итогового ответа (канал, видимый пользователю).</summary>
    /// <param name="Text">Фрагмент текста.</param>
    public sealed record AnswerChunk(string Text) : ReActEvent;

    /// <summary>Фрагмент рассуждений модели (канал размышлений, отдельный от ответа).</summary>
    /// <param name="Text">Фрагмент текста.</param>
    public sealed record ReasoningChunk(string Text) : ReActEvent;

    /// <summary>Терминальное событие: прогон завершён. Всегда последнее в потоке.</summary>
    /// <param name="Result">Итог прогона.</param>
    public sealed record Completed(ReActResult Result) : ReActEvent;
}
