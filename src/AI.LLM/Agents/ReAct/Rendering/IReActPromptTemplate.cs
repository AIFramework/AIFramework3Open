using AI.LLM.Agents.ReAct.Tools;

namespace AI.LLM.Agents.ReAct.Rendering;

/// <summary>
/// Тексты, которые цикл говорит модели: системный промпт и служебные подсказки при ошибках.
/// <para>
/// Вынесено в интерфейс, чтобы в библиотеке не оказалось ни одного текста, привязанного к
/// конкретному продукту. Правила уровня механики («одно действие за шаг», «не повторяй
/// сделанное») — в реализации по умолчанию; всё остальное задаёт вызывающая сторона.
/// </para>
/// </summary>
public interface IReActPromptTemplate
{
    /// <summary>Собирает системный промпт шага.</summary>
    /// <param name="basePrompt">Промпт вызывающей стороны; может быть <c>null</c>.</param>
    /// <param name="tools">Доступные инструменты.</param>
    /// <param name="skills">Применимые навыки.</param>
    /// <param name="context">Контекст прогона.</param>
    string BuildSystemPrompt(
        string basePrompt,
        IReadOnlyList<IReActTool> tools,
        IReadOnlyList<IReActSkill> skills,
        ReActRunContext context);

    /// <summary>Подсказка при обращении к несуществующему инструменту.</summary>
    /// <param name="requestedName">Что запросила модель.</param>
    /// <param name="availableNames">Имена доступных инструментов.</param>
    string BuildUnknownToolNote(string requestedName, IReadOnlyList<string> availableNames);

    /// <summary>Подсказка при повторе уже выполненного действия.</summary>
    /// <param name="toolName">Имя инструмента.</param>
    string BuildRepeatedActionNote(string toolName);

    /// <summary>Подсказка при череде падений одного инструмента.</summary>
    /// <param name="toolName">Имя инструмента.</param>
    string BuildRepeatedFailureNote(string toolName);

    /// <summary>Подсказка, когда ответ модели не удалось разобрать.</summary>
    string BuildMalformedDecisionNote();
}
