using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Чем кончился прогон движка. Причина остановки — отдельно, кодом <see cref="EngineResult.StopCode"/>.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<EngineStatus>))]
public enum EngineStatus
{
    /// <summary>Статус не задан. Итогом не считается: ни успехом, ни частичным материалом.</summary>
    Unknown = 0,

    /// <summary>Задача сделана.</summary>
    Done,

    /// <summary>
    /// Не доделано: кончились шаги, токены, деньги или срок. Есть частичный материал и список
    /// <see cref="EngineResult.Unfinished"/>. Это не успех и не сбой.
    /// </summary>
    Incomplete,

    /// <summary>Нужен ответ человека; вопрос в <see cref="EngineResult.Question"/>.</summary>
    AwaitsUser,

    /// <summary>Сбой; причина в <see cref="EngineResult.StopCode"/>.</summary>
    Failed,

    /// <summary>Прогон отменен.</summary>
    Cancelled,
}
