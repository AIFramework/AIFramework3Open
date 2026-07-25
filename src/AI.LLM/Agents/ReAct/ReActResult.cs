namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Итог прогона. <see cref="StopReason"/> позволяет отличить полноценный ответ от исчерпания
/// бюджета или сбоя, не разбирая текст.
/// </summary>
public sealed class ReActResult
{
    /// <summary>Итоговый ответ. Никогда не <c>null</c>, но может быть пустым.</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>Почему цикл остановился.</summary>
    public ReActStopReason StopReason { get; init; }

    /// <summary>Шаги цикла в порядке выполнения. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<ReActStep> Steps { get; init; } = [];

    /// <summary>Источники, накопленные инструментами, без повторов по адресу.</summary>
    public IReadOnlyList<ReActCitation> Citations { get; init; } = [];

    /// <summary>
    /// Результат терминального инструмента в типе вызывающей стороны. Заполняется при
    /// <see cref="ReActStopReason.TerminalTool"/>: бывают инструменты, чей результат и есть
    /// ответ хода (готовая форма, изображение, изменённый документ), и сведение его к строке
    /// потеряло бы суть. Может быть <c>null</c>.
    /// </summary>
    public object Payload { get; init; }

    /// <summary>Учёт токенов и вызовов инструментов за прогон.</summary>
    public AgentUsage Usage { get; init; } = new();

    /// <summary>Полная длительность прогона.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Описание сбоя при <see cref="ReActStopReason.EngineFailure"/>. Может быть <c>null</c>.
    /// </summary>
    public string Error { get; init; }

    /// <summary>Прогон завершился сбоем.</summary>
    public bool Failed => StopReason == ReActStopReason.EngineFailure;

    /// <summary>Число выполненных шагов.</summary>
    public int TotalSteps => Steps.Count;

    /// <summary>Ответ непустой.</summary>
    public bool HasAnswer => !string.IsNullOrWhiteSpace(Answer);

    /// <summary>Результат терминального инструмента с приведением типа; <c>null</c>, если тип не тот.</summary>
    /// <typeparam name="T">Ожидаемый тип результата.</typeparam>
    public T PayloadAs<T>() where T : class => Payload as T;
}
