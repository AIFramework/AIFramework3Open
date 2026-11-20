namespace AI.Agents.Contracts.Runs;

/// <summary>
/// Хранилище запусков. Порт: реализацию (файл, база, память процесса) дает продукт, фреймворк ее не содержит.
/// </summary>
/// <remarks>
/// Минимальная форма: сохранить, прочитать по идентификатору, перечислить по состоянию. Совместная запись
/// одного запуска двумя исполнителями (версия записи, блокировка) — забота реализации.
/// </remarks>
public interface IRunStore
{
    /// <summary>Сохранить запуск: создать или заменить запись с тем же <see cref="RunState.Id"/>.</summary>
    /// <param name="run">Запуск.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Задача сохранения.</returns>
    Task SaveAsync(RunState run, CancellationToken cancellationToken = default);

    /// <summary>Прочитать запуск.</summary>
    /// <param name="runId">Идентификатор запуска.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Запуск; <see langword="null"/>: такого запуска нет.</returns>
    Task<RunState?> GetAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Перечислить запуски в состоянии: ждущие человека (<see cref="RunStatus.AwaitsUser"/>), исполняемые
    /// (<see cref="RunStatus.Running"/>) — чтобы найти потерянные, и так далее.
    /// </summary>
    /// <param name="status">Состояние.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Запуски в этом состоянии, порядок задает реализация.</returns>
    IAsyncEnumerable<RunState> ListAsync(RunStatus status, CancellationToken cancellationToken = default);
}
