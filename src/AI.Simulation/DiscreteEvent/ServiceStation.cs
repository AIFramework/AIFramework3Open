using AI.Insights;

namespace AI.Simulation.DiscreteEvent;

/// <summary>Показатели работы обслуживающей системы</summary>
/// <param name="Arrivals">Число поступивших заявок</param>
/// <param name="Served">Число обслуженных заявок</param>
/// <param name="Rejected">Число отказов из-за переполнения</param>
/// <param name="AverageWait">Среднее время ожидания в очереди</param>
/// <param name="AverageSystemTime">Среднее время пребывания в системе</param>
/// <param name="AverageQueueLength">Средняя длина очереди по времени</param>
/// <param name="Utilisation">Загрузка приборов</param>
/// <param name="MaxQueueLength">Наибольшая длина очереди</param>
public readonly record struct ServiceStatistics(
    int Arrivals, int Served, int Rejected,
    double AverageWait, double AverageSystemTime,
    double AverageQueueLength, double Utilisation, int MaxQueueLength) : IInterpretable
{
    /// <summary>Доля отказов</summary>
    public double RejectionRate => Arrivals == 0 ? 0 : (double)Rejected / Arrivals;

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Обслуживающая система")
            .Summary($"Поступило заявок {Arrivals}, обслужено {Served}"
                + (Rejected > 0 ? $", отказано {Rejected} ({Fmt.Pct(RejectionRate)})" : string.Empty)
                + $". Загрузка приборов {Fmt.Pct(Utilisation)}, средняя очередь "
                + $"{Fmt.Num(AverageQueueLength, 2)} заявки, среднее ожидание {Fmt.Num(AverageWait, 3)}.")
            .Metric("Загрузка", Fmt.Pct(Utilisation), null, "доля времени, когда приборы заняты",
                Utilisation > 0.9 ? MetricQuality.Warning : Utilisation > 0.3 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Средняя очередь", Fmt.Num(AverageQueueLength, 3), null, "среднее по времени, а не по заявкам")
            .Metric("Наибольшая очередь", MaxQueueLength, null, "пиковое значение за прогон", MetricQuality.Unknown, 0)
            .Metric("Среднее ожидание", Fmt.Num(AverageWait, 4), null, "в очереди, без обслуживания")
            .Metric("Среднее время в системе", Fmt.Num(AverageSystemTime, 4), null, "ожидание плюс обслуживание")
            .FindingIf(Utilisation > 0.85,
                "Загрузка выше 85 %: очередь растёт нелинейно. Прибавка десяти процентов нагрузки здесь "
                + "удлиняет ожидание в разы, а не на десятую часть.")
            .FindingIf(Rejected > 0,
                $"Отказано {Fmt.Pct(RejectionRate)} заявок: накопитель переполнялся. Увеличение места "
                + "в очереди снизит отказы, но удлинит ожидание — это обмен, а не улучшение.")
            .FindingIf(Utilisation < 0.3,
                "Приборы простаивают большую часть времени: мощность избыточна для такого потока.")
            .Warning("Показатели получены за один прогон и содержат случайную погрешность. Для оценки "
                + "доверительного интервала нужно несколько прогонов с разными зёрнами генератора.")
            .Warning("Начальный участок прогона искажает средние: система стартует пустой и приходит "
                + "к установившемуся режиму не сразу. При коротких прогонах его отбрасывают.")
            .Build();
}

/// <summary>
/// Многоканальная обслуживающая система с очередью.
/// </summary>
/// <remarks>
/// Заявки поступают событиями, занимают свободный прибор либо встают в очередь.
/// Дисциплина обслуживания — в порядке поступления; накопитель может быть ограничен,
/// и тогда заявка сверх ёмкости получает отказ.
/// </remarks>
public sealed class ServiceStation
{
    private readonly SimulationEngine _engine;
    private readonly int _servers;
    private readonly int _capacity;
    private readonly Queue<double> _waiting = new();
    private readonly Tally _wait = new();
    private readonly Tally _system = new();
    private readonly TimeWeightedAccumulator _queueLength = new();
    private readonly TimeWeightedAccumulator _busyServers = new();

    private int _busy;
    private int _arrivals;
    private int _served;
    private int _rejected;

    /// <summary>Создаёт систему</summary>
    /// <param name="engine">Модель, в которой она работает</param>
    /// <param name="servers">Число приборов обслуживания</param>
    /// <param name="capacity">Ёмкость накопителя; по умолчанию не ограничена</param>
    public ServiceStation(SimulationEngine engine, int servers = 1, int capacity = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(servers);

        _engine = engine;
        _servers = servers;
        _capacity = capacity;
    }

    /// <summary>Длительность обслуживания одной заявки</summary>
    public required Func<double> ServiceTime { get; init; }

    /// <summary>Число заявок в очереди</summary>
    public int QueueLength => _waiting.Count;

    /// <summary>
    /// Принимает заявку: занимает прибор либо ставит в очередь
    /// </summary>
    /// <returns><c>false</c>, если заявка получила отказ из-за переполнения</returns>
    public bool Arrive()
    {
        _arrivals++;

        if (_busy >= _servers && _waiting.Count >= _capacity)
        {
            _rejected++;
            return false;
        }

        if (_busy < _servers)
        {
            _busy++;
            _busyServers.Update(_engine.Now, _busy);
            BeginService(_engine.Now);

            return true;
        }

        _waiting.Enqueue(_engine.Now);
        _queueLength.Update(_engine.Now, _waiting.Count);

        return true;
    }

    /// <summary>Показатели работы на текущий момент</summary>
    public ServiceStatistics Statistics()
        => new(
            _arrivals,
            _served,
            _rejected,
            _wait.Mean,
            _system.Mean,
            _queueLength.Average(_engine.Now),
            _servers == 0 ? 0 : _busyServers.Average(_engine.Now) / _servers,
            (int)_queueLength.Maximum);

    private void BeginService(double arrivedAt)
    {
        double duration = ServiceTime();

        _engine.Schedule(duration, () => CompleteService(arrivedAt, duration));
    }

    private void CompleteService(double arrivedAt, double duration)
    {
        _served++;
        _wait.Observe(_engine.Now - duration - arrivedAt);
        _system.Observe(_engine.Now - arrivedAt);

        if (_waiting.Count > 0)
        {
            double queuedAt = _waiting.Dequeue();
            _queueLength.Update(_engine.Now, _waiting.Count);
            BeginService(queuedAt);

            return;
        }

        _busy--;
        _busyServers.Update(_engine.Now, _busy);
    }
}
