using AI.LLM.Agents.ReAct.Synthesis;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Настройки цикла: бюджеты и пороги защит. Все значения задаются здесь и только здесь —
/// в билдере не должно быть зашитых чисел.
/// </summary>
public sealed class ReActConfig
{
    /// <summary>Максимальное число шагов.</summary>
    public int MaxIterations { get; set; } = 8;

    /// <summary>
    /// Предел времени на весь прогон. <c>null</c> — без предела. Одного счётчика шагов мало:
    /// шаг может идти минутами, а запрос пользователя ждать столько не может.
    /// </summary>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>Предел времени на один вызов инструмента. <c>null</c> — без предела.</summary>
    public TimeSpan? ToolTimeout { get; set; }

    /// <summary>
    /// Сколько инструментов исполнять одновременно, когда модель запросила несколько действий.
    /// По умолчанию один: инструменты не обязаны быть потокобезопасными, и параллелизм —
    /// осознанное решение вызывающей стороны, а не поведение по умолчанию.
    /// </summary>
    public int MaxParallelTools { get; set; } = 1;

    /// <summary>Предел длины одного наблюдения в следе.</summary>
    public int MaxObservationChars { get; set; } = 4000;

    /// <summary>Предел длины всего следа, отдаваемого модели.</summary>
    public int MaxScratchpadChars { get; set; } = 12000;

    /// <summary>
    /// Сколько раз допускается повторить то же действие с тем же аргументом. При превышении
    /// цикл останавливается с <see cref="ReActStopReason.NoProgress"/>.
    /// </summary>
    public int MaxRepeatedActions { get; set; } = 1;

    /// <summary>Сколько падений одного инструмента подряд допускается.</summary>
    public int MaxConsecutiveFailures { get; set; } = 2;

    /// <summary>Сколько раз можно подсказать модели правильное имя инструмента.</summary>
    public int UnknownToolBudget { get; set; } = 2;

    /// <summary>Сколько раз можно попросить модель переформулировать неразобранный ответ.</summary>
    public int MalformedDecisionBudget { get; set; } = 1;

    /// <summary>Когда запускать синтез итогового ответа.</summary>
    public ReActSynthesisMode SynthesisMode { get; set; } = ReActSynthesisMode.WhenNoAnswer;

    /// <summary>Отдавать ли рассуждения шагов в поток событий.</summary>
    public bool EmitThoughts { get; set; } = true;
}
