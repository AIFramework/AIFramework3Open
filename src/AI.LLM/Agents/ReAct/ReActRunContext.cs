using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Контекст прогона, который видят источники инструментов и скиллы, когда решают,
/// что предложить модели.
/// </summary>
public sealed class ReActRunContext
{
    /// <summary>Запрос пользователя. Никогда не <c>null</c>.</summary>
    public string Query { get; }

    /// <summary>История диалога. Никогда не <c>null</c>; может быть пустой.</summary>
    public IReadOnlyList<LLMMessage> History { get; }

    /// <summary>Номер текущего шага, начиная с единицы. Ноль — сборка инструментов до первого шага.</summary>
    public int StepNumber { get; }

    /// <summary>
    /// Произвольная метка вызывающей стороны (идентификатор сессии, владелец, редактируемый
    /// документ). Библиотека её не интерпретирует и передаёт инструментам и скиллам как есть —
    /// так продуктовые сущности не попадают в открытый контракт. Может быть <c>null</c>.
    /// </summary>
    public object Tag { get; }

    /// <summary>Создаёт контекст прогона.</summary>
    /// <param name="query">Запрос пользователя.</param>
    /// <param name="history">История диалога; допускается <c>null</c>.</param>
    /// <param name="stepNumber">Номер шага.</param>
    /// <param name="tag">Метка вызывающей стороны; допускается <c>null</c>.</param>
    public ReActRunContext(string query, IReadOnlyList<LLMMessage> history = null, int stepNumber = 0, object tag = null)
    {
        Query = query ?? string.Empty;
        History = history ?? [];
        StepNumber = stepNumber;
        Tag = tag;
    }

    /// <summary>Метка вызывающей стороны с приведением типа; <c>null</c>, если тип не тот.</summary>
    /// <typeparam name="T">Ожидаемый тип метки.</typeparam>
    public T TagAs<T>() where T : class => Tag as T;

    /// <summary>Копия контекста с другим номером шага.</summary>
    /// <param name="stepNumber">Новый номер шага.</param>
    public ReActRunContext AtStep(int stepNumber) => new(Query, History, stepNumber, Tag);
}
