using AI.LLM.Agents.ReAct.Tools;

namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Всё, что нужно для принятия решения об одном шаге. Собирается движком.
/// </summary>
/// <remarks>
/// Реализация решения не хранит состояние между шагами: состояние — это <see cref="Trace"/>,
/// и владеет им движок. Благодаря этому разные способы получать решение (нативные вызовы
/// инструментов и текстовый протокол) взаимозаменяемы: каждый просто по-своему проецирует
/// один и тот же след.
/// </remarks>
public sealed class ReActPolicyContext
{
    /// <summary>Запрос пользователя.</summary>
    public ReActQuery Query { get; init; }

    /// <summary>Доступные инструменты. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<IReActTool> Tools { get; init; } = [];

    /// <summary>След прогона.</summary>
    public ReActTrace Trace { get; init; }

    /// <summary>След, отрендеренный в текст. Никогда не <c>null</c>; пуст на первом шаге.</summary>
    public string RenderedTrace { get; init; } = string.Empty;

    /// <summary>Системный промпт: база вызывающей стороны + навыки + список инструментов.</summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>Номер шага, начиная с единицы.</summary>
    public int StepNumber { get; init; }

    /// <summary>Предельное число шагов.</summary>
    public int MaxSteps { get; init; }

    /// <summary>
    /// Замечание движка к предыдущему шагу: неизвестный инструмент, повтор действия,
    /// неразобранный ответ. Может быть <c>null</c>.
    /// </summary>
    public string CorrectiveNote { get; init; }
}
