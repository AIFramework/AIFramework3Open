namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Движок агента: решает, как думать и когда звать инструмент. Набор инструментов, бюджет, пауза, приемка и
/// сохранение запуска лежат снаружи, у хоста, и одинаковы для любого движка.
/// </summary>
public interface IEngine
{
    /// <summary>Ключ движка, по которому его выбирает запись агента (например, <c>react</c>, <c>direct</c>).</summary>
    string Key { get; }

    /// <summary>
    /// Прогон. Возвращает только поток событий; второго канала возврата нет.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Итог один: последнее событие потока всегда <see cref="EngineEvent.Finished"/> с <see cref="EngineResult"/>,
    /// и после него событий нет. Сбой, исчерпание бюджета и вопрос человеку тоже приходят итогом с
    /// соответствующим <see cref="EngineStatus"/>, а не исключением; отмена токеном может прервать поток
    /// исключением <see cref="OperationCanceledException"/>, как принято в .NET.
    /// </para>
    /// <para>
    /// Непустой <see cref="EngineInput.State"/> на входе — продолжить с места, где прогон остановился.
    /// </para>
    /// </remarks>
    /// <param name="input">Задача, контекст и порты прогона.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Поток событий, последним — <see cref="EngineEvent.Finished"/>.</returns>
    IAsyncEnumerable<EngineEvent> RunAsync(EngineInput input, CancellationToken cancellationToken = default);
}
