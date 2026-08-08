using AI.LLM.Agents.Multimodal;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Результат одного действия — то, что модель увидит на следующем шаге.
/// </summary>
/// <remarks>
/// Запись, а не класс: при повторе действия прежнее наблюдение переиспользуется, но привязывается
/// к новому вызову (<c>observation with { Action = … }</c>) — идентификатор вызова обязан быть тем,
/// что модель прислала сейчас.
/// </remarks>
public sealed record ReActObservation
{
    /// <summary>Действие, породившее наблюдение.</summary>
    public ReActAction Action { get; init; }

    /// <summary>Инструмент отработал успешно.</summary>
    public bool Ok { get; init; }

    /// <summary>
    /// Текст наблюдения. Никогда не <c>null</c>. Ошибка тоже описывается текстом:
    /// модель должна узнать, что именно не получилось, и выбрать другой путь.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Код ошибки для программной обработки. Может быть <c>null</c>.</summary>
    public string ErrorCode { get; init; }

    /// <summary>Длительность вызова.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Источники, на которые опирается наблюдение. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<ReActCitation> Citations { get; init; } = [];

    /// <summary>Изображения, приложенные инструментом. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<AgentImage> Images { get; init; } = [];

    /// <summary>Инструмент потребовал завершить ход собственным результатом.</summary>
    public bool EndsTurn { get; init; }

    /// <summary>Готовый ответ терминального инструмента. Может быть <c>null</c>.</summary>
    public string TerminalAnswer { get; init; }

    /// <summary>Результат терминального инструмента в типе вызывающей стороны. Может быть <c>null</c>.</summary>
    public object Payload { get; init; }
}
