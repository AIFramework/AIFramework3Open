using System.Collections.Concurrent;

namespace AI.Script.Hosting;

/// <summary>
/// Запись журнала опытов: одно испытание со всем, что нужно, чтобы его повторить.
/// </summary>
/// <param name="Id">Номер испытания, по которому его находят позже.</param>
/// <param name="Script">Отпечаток текста скрипта: та же задача, но другой скрипт — другой опыт.</param>
/// <param name="Seed">Зерно ветви: без него повтор даёт другие случайные числа.</param>
/// <param name="Parameters">Точка плана: что именно проверяли.</param>
/// <param name="Metrics">Что получилось.</param>
/// <param name="Started">Когда начали.</param>
/// <param name="ElapsedMs">Сколько заняло, в миллисекундах.</param>
/// <param name="Tokens">Токенов израсходовано за испытание.</param>
/// <param name="Cost">Стоимость испытания.</param>
public sealed record ExperimentEntry(
    string Id,
    string Script,
    int Seed,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyDictionary<string, object?> Metrics,
    DateTimeOffset Started,
    long ElapsedMs,
    long Tokens,
    decimal Cost);

/// <summary>
/// Журнал опытов.
/// </summary>
/// <remarks>
/// Опыт без журнала не повторить: через неделю от него остаётся вывод в переписке, а чем он
/// получен — уже нет. Поэтому каждое испытание записывается само, без просьбы со стороны
/// скрипта.
/// <para>
/// Где журнал лежит, решает хост: прогону достаточно интерфейса. В открытом фреймворке есть
/// журнал в памяти; продукт кладёт его в хранилище владельца, и тогда опыт, начатый в одной
/// сессии, виден и продолжается в другой.
/// </para>
/// </remarks>
public interface IExperimentJournal
{
    /// <summary>Записывает испытание.</summary>
    Task AppendAsync(ExperimentEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Читает записанное, в порядке добавления.</summary>
    Task<IReadOnlyList<ExperimentEntry>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет действие в очереди с другими такими действиями над этим журналом.
    /// </summary>
    /// <remarks>
    /// Опыт берет номер испытания из журнала и тут же занимает его записью: два опыта одновременно
    /// прочли бы одно и то же наибольшее значение и получили одинаковые номера. По умолчанию очередь
    /// одна на объект журнала. Хост, у которого разные объекты пишут в одно хранилище (две сессии
    /// одного владельца), дает очередь на хранилище. <see cref="AppendAsync"/> внутри действия
    /// вызывается свободно: очередь записи у журнала своя.
    /// </remarks>
    async Task ExclusiveAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = ExperimentJournalQueues.For(this);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _ = gate.Release();
        }
    }
}

/// <summary>Очереди журналов по умолчанию: одна на объект, живет столько же, сколько он.</summary>
internal static class ExperimentJournalQueues
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IExperimentJournal, SemaphoreSlim> s_queues = new();

    public static SemaphoreSlim For(IExperimentJournal journal) => s_queues.GetValue(journal, _ => new SemaphoreSlim(1, 1));
}

/// <summary>Журнал в памяти: живёт столько же, сколько объект, которому его отдали.</summary>
/// <remarks>
/// Потокобезопасен намеренно: испытания идут ветвями одновременно, и журнал — то место, куда
/// они пишут все сразу.
/// </remarks>
public sealed class MemoryExperimentJournal : IExperimentJournal
{
    private readonly ConcurrentQueue<ExperimentEntry> _entries = new();

    /// <summary>Сколько испытаний записано.</summary>
    public int Count => _entries.Count;

    /// <inheritdoc/>
    public Task AppendAsync(ExperimentEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entries.Enqueue(entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExperimentEntry>> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExperimentEntry>>([.. _entries]);
}
