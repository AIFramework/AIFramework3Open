using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Runs;

/// <summary>
/// Состояние запуска агента. Запуск живет дольше одного прогона движка: он стоит в очереди, ждет человека,
/// продолжается и может потеряться, поэтому набор шире, чем у <see cref="Engines.EngineStatus"/>.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<RunStatus>))]
public enum RunStatus
{
    /// <summary>Состояние не задано. Ни ожиданием, ни итогом не считается.</summary>
    Unknown = 0,

    /// <summary>Запуск создан и ждет исполнителя.</summary>
    Queued,

    /// <summary>Движок исполняет запуск.</summary>
    Running,

    /// <summary>Запуск стоит на вопросе человеку; вопрос в <see cref="RunState.Question"/>.</summary>
    AwaitsUser,

    /// <summary>Задача сделана.</summary>
    Done,

    /// <summary>Не доделано: кончился бюджет или нет продвижения. Это не успех и не сбой.</summary>
    Incomplete,

    /// <summary>Сбой.</summary>
    Failed,

    /// <summary>Запуск отменен.</summary>
    Cancelled,

    /// <summary>
    /// Исполнитель пропал, не записав итог (процесс упал, машина перезапущена). Исход эффектов, начатых в этом
    /// запуске, неизвестен: журнал <see cref="RunState.Effects"/> показывает, какие вызовы начаты.
    /// </summary>
    Lost,
}
