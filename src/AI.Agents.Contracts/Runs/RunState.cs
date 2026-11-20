using System.Text.Json.Nodes;
using AI.Agents.Contracts.Engines;

namespace AI.Agents.Contracts.Runs;

/// <summary>
/// Запуск агента как сущность: что исполняется, где остановилось и какие эффекты уже совершены. Хранит его
/// продукт через <see cref="IRunStore"/>; контракт дает только форму.
/// </summary>
/// <remarks>
/// Запуск переживает прогон движка: после <see cref="RunStatus.AwaitsUser"/> хост сохраняет состояние, а когда
/// пришел ответ, зовет движок с <see cref="EngineState"/> и ответом в <see cref="EngineTask.Answer"/>. Журнал
/// <see cref="Effects"/> едет вместе с состоянием, чтобы продолжение не исполнило уже совершенное действие
/// второй раз.
/// </remarks>
public sealed record RunState
{
    /// <summary>Идентификатор запуска (непрозрачная строка хоста); тот же, что <see cref="EngineInput.RunId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Агент и версия его записи: продолжение идет на той же версии.</summary>
    public required AgentRef Agent { get; init; }

    /// <summary>Задача запуска: без нее продолжение не знает, что делать.</summary>
    public required EngineTask Task { get; init; }

    /// <summary>Состояние запуска.</summary>
    public required RunStatus Status { get; init; }

    /// <summary>Когда запись изменилась последний раз; по нему хост находит потерянные запуски.</summary>
    public required DateTimeOffset Updated { get; init; }

    /// <summary>
    /// Состояние движка для продолжения (<see cref="EngineResult.State"/> или <see cref="EngineEvent.Saved"/>);
    /// <see langword="null"/>: продолжать с места нечего, только начать заново.
    /// </summary>
    public JsonObject? EngineState { get; init; }

    /// <summary>Вопрос, на котором стоит запуск, при <see cref="RunStatus.AwaitsUser"/>; иначе <see langword="null"/>.</summary>
    public Question? Question { get; init; }

    /// <summary>
    /// Журнал эффектов запуска в порядке начала вызовов; <see langword="null"/>: ни одного эффекта не начато.
    /// </summary>
    public IReadOnlyList<EffectRecord>? Effects { get; init; }
}
