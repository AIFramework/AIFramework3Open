using AI.LLM.Agents.Multimodal;

namespace AI.LLM.Agents.ReAct.Tools;

/// <summary>
/// Результат работы инструмента: наблюдение для следующего шага и, при необходимости,
/// сигнал завершить ход собственным ответом.
/// </summary>
public sealed class ReActToolOutcome
{
    /// <summary>Инструмент отработал успешно.</summary>
    public bool Ok { get; init; }

    /// <summary>
    /// Наблюдение — текст, который увидит модель на следующем шаге. Никогда не <c>null</c>.
    /// Ошибку тоже полезно возвращать наблюдением: модель может выбрать другой путь вместо
    /// повторной попытки.
    /// </summary>
    public string Observation { get; init; } = string.Empty;

    /// <summary>Источники, на которые опирается наблюдение. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<ReActCitation> Citations { get; init; } = [];

    /// <summary>
    /// Изображения, возвращённые инструментом (снимок экрана, отрисованная схема).
    /// Никогда не <c>null</c>. Модели без поддержки изображений их не увидят — наблюдение
    /// должно оставаться понятным и без них.
    /// </summary>
    public IReadOnlyList<AgentImage> Images { get; init; } = [];

    /// <summary>
    /// Завершить ход немедленно, минуя синтез. Нужно инструментам, чей результат и есть ответ:
    /// готовая форма уточнения, сгенерированное изображение, применённая правка. Пропускать
    /// такой результат через синтез вредно — он его пересказывает и теряет.
    /// </summary>
    public bool EndsTurn { get; init; }

    /// <summary>Готовый ответ терминального инструмента. Может быть <c>null</c>.</summary>
    public string TerminalAnswer { get; init; }

    /// <summary>Результат в типе вызывающей стороны. Может быть <c>null</c>.</summary>
    public object Payload { get; init; }

    /// <summary>Код ошибки для программной обработки. Может быть <c>null</c>.</summary>
    public string ErrorCode { get; init; }

    /// <summary>Инструмент завершает ход.</summary>
    public bool IsTerminal => EndsTurn;

    /// <summary>Успешное наблюдение.</summary>
    /// <param name="observation">Текст наблюдения.</param>
    /// <param name="citations">Источники; допускается <c>null</c>.</param>
    public static ReActToolOutcome Success(string observation, IReadOnlyList<ReActCitation> citations = null) =>
        new() { Ok = true, Observation = observation ?? string.Empty, Citations = citations ?? [] };

    /// <summary>Неудача: наблюдение объясняет модели, что именно не получилось.</summary>
    /// <param name="observation">Описание ошибки для модели.</param>
    /// <param name="errorCode">Код ошибки; необязателен.</param>
    public static ReActToolOutcome Failure(string observation, string errorCode = null) =>
        new() { Ok = false, Observation = observation ?? string.Empty, ErrorCode = errorCode };

    /// <summary>Завершение хода результатом инструмента, без синтеза.</summary>
    /// <param name="terminalAnswer">Готовый ответ; допускается <c>null</c>, если ответ несёт <paramref name="payload"/>.</param>
    /// <param name="observation">Текстовое представление результата для следа.</param>
    /// <param name="payload">Результат в типе вызывающей стороны.</param>
    /// <param name="ok">Считать ли результат успешным.</param>
    /// <param name="citations">Источники; допускается <c>null</c>.</param>
    public static ReActToolOutcome Terminal(
        string terminalAnswer,
        string observation = null,
        object payload = null,
        bool ok = true,
        IReadOnlyList<ReActCitation> citations = null) =>
        new()
        {
            Ok = ok,
            Observation = observation ?? terminalAnswer ?? string.Empty,
            EndsTurn = true,
            TerminalAnswer = terminalAnswer,
            Payload = payload,
            Citations = citations ?? [],
        };
}
