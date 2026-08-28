namespace AI.Script.Hosting;

/// <summary>
/// Куда прогон сообщает о ходе работы.
/// </summary>
/// <remarks>
/// Отдельный интерфейс, а не запись в транскрипт: транскрипт — это то, что напечатал скрипт, и
/// подмешивать в него служебные сообщения значило бы портить результат ради удобства
/// наблюдателя. Хост, которому прогресс не нужен, ничего не передаёт и ничего не платит.
/// <para>
/// Реализация обязана быть потокобезопасной: при <c>core.map(parallel: true)</c> уведомления
/// приходят из нескольких потоков.
/// </para>
/// </remarks>
public interface IProgressSink
{
    /// <summary>Стадия начата.</summary>
    void StageStarted(StageNode stage);

    /// <summary>Стадия завершена — посчитана, взята из кэша либо сорвалась.</summary>
    void StageFinished(StageNode stage);
}

/// <summary>Прогресс, выводимый вызываемым делегатом: обёртка для простых хостов.</summary>
public sealed class DelegateProgressSink : IProgressSink
{
    private readonly Action<StageNode, bool> _report;

    /// <summary>Создаёт приёмник; второй аргумент делегата — признак завершения.</summary>
    public DelegateProgressSink(Action<StageNode, bool> report) =>
        _report = report ?? throw new ArgumentNullException(nameof(report));

    /// <inheritdoc/>
    public void StageStarted(StageNode stage) => _report(stage, false);

    /// <inheritdoc/>
    public void StageFinished(StageNode stage) => _report(stage, true);
}
