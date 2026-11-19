namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Запустить другого агента шагом. Единственный путь движка к другим агентам: движок не знает, кто и
/// каким движком исполнит шаг.
/// </summary>
public interface IStepRunner
{
    /// <summary>
    /// Прогнать агента на задаче. Поток по правилам <see cref="IEngine.RunAsync(EngineInput, CancellationToken)"/>:
    /// последним идет <see cref="EngineEvent.Finished"/>.
    /// </summary>
    /// <param name="agent">Кого запустить.</param>
    /// <param name="task">Задача шагу.</param>
    /// <param name="path">Путь шага, на котором идет дочерний запуск.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Поток событий дочернего запуска.</returns>
    IAsyncEnumerable<EngineEvent> RunAsync(AgentRef agent, EngineTask task, StepPath path, CancellationToken cancellationToken = default);
}
