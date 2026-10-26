namespace AI.LLM.Clients.Base;

/// <summary>
/// Модель закончила ответ, ничего не сказав: ни текста, ни рассуждения, ни вызова инструмента. Связь при этом есть —
/// так модель иногда «молчит» после результата инструмента.
/// </summary>
/// <remarks>
/// Отдельный тип нужен циклу агента: пустой ответ лечится подсказкой модели и новым шагом, а не повтором того же
/// запроса. Наследник <see cref="InvalidOperationException"/> с прежним текстом: для остальных вызывающих ничего не меняется.
/// </remarks>
public sealed class EmptyModelAnswerException : InvalidOperationException
{
    public EmptyModelAnswerException()
        : base("Потоковый ответ пуст - не получено ни текста, ни reasoning, ни изображений, ни tool_calls.")
    {
    }
}
