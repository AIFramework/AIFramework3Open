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
            .Warning("Показатели получены за один прогон и содержат случайную погрешность. Доверительный "
                + "интервал даёт серия независимых прогонов с разными зёрнами — Experiments.Replications.")
            .Warning("Начальный участок прогона искажает средние: система стартует пустой и приходит "
                + "к установившемуся режиму не сразу. Его отбрасывают вызовом ResetStatistics после разгона.")
            .Build();
}

/// <summary>
/// Многоканальная обслуживающая система с очередью и классами заявок.
/// </summary>
/// <remarks>
/// <para>
/// Заявки поступают событиями, занимают свободный прибор либо встают в очередь. Накопитель
/// может быть ограничен, и тогда заявка сверх ёмкости получает отказ.
/// </para>
/// <para>
/// Дисциплина задаёт, кого прибор берёт следующим: в порядке поступления либо по приоритету
/// класса (класс 0 — старший), с прерыванием или без. Прерванная заявка возвращается в голову
/// своей очереди и потом дообслуживается остаток — работа не теряется. Она возвращается туда,
/// даже если накопитель полон: заявка уже принята системой, и отказать ей задним числом нельзя.
/// </para>
/// <para>
/// Календарь событий не умеет отменять запланированное, поэтому прерывание устроено через
/// номер попытки обслуживания: у прерванной заявки он растёт, и её прежнее событие окончания,
/// сработав, видит чужой номер и ничего не делает.
/// </para>
/// </remarks>
public sealed class ServiceStation
{
    private readonly SimulationEngine _engine;
    private readonly int _servers;
    private readonly int _capacity;
    private readonly LinkedList<Job>[] _queues;
    private readonly List<Job> _inService = [];
    private readonly ClassRecord _total = new();
    private readonly ClassRecord[] _classes;

    /// <summary>Создаёт систему</summary>
    /// <param name="engine">Модель, в которой она работает</param>
    /// <param name="servers">Число приборов обслуживания</param>
    /// <param name="capacity">Ёмкость накопителя; по умолчанию не ограничена</param>
    /// <param name="discipline">Дисциплина обслуживания</param>
    /// <param name="classes">Число классов заявок</param>
    public ServiceStation(
        SimulationEngine engine,
        int servers = 1,
        int capacity = int.MaxValue,
        QueueDiscipline discipline = QueueDiscipline.FirstComeFirstServed,
        int classes = 1)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(servers);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(classes);

        _engine = engine;
        _servers = servers;
        _capacity = capacity;
        Discipline = discipline;
        ClassCount = classes;

        // В порядке поступления классы стоят в одной общей очереди
        _queues = new LinkedList<Job>[discipline == QueueDiscipline.FirstComeFirstServed ? 1 : classes];

        for (int i = 0; i < _queues.Length; i++)
            _queues[i] = new LinkedList<Job>();

        _classes = new ClassRecord[classes];

        for (int i = 0; i < classes; i++)
            _classes[i] = new ClassRecord();
    }

    /// <summary>Длительность обслуживания одной заявки, общая для всех классов</summary>
    public Func<double>? ServiceTime { get; init; }

    /// <summary>
    /// Длительность обслуживания заявки заданного класса; если задана, заменяет общую
    /// </summary>
    public Func<int, double>? ServiceTimeOfClass { get; init; }

    /// <summary>Дисциплина обслуживания</summary>
    public QueueDiscipline Discipline { get; }

    /// <summary>Число классов заявок</summary>
    public int ClassCount { get; }

    /// <summary>Число заявок в очереди</summary>
    public int QueueLength => _total.Waiting;

    /// <summary>Сколько раз обслуживание прерывалось заявкой старшего класса</summary>
    public int Preemptions { get; private set; }

    /// <summary>
    /// Принимает заявку: занимает прибор, вытесняет младшую либо ставит в очередь
    /// </summary>
    /// <param name="priorityClass">Класс заявки; 0 — старший</param>
    /// <returns><c>false</c>, если заявка получила отказ из-за переполнения</returns>
    public bool Arrive(int priorityClass = 0)
    {
        if (priorityClass < 0 || priorityClass >= ClassCount)
            throw new ArgumentOutOfRangeException(nameof(priorityClass),
                $"Класс {priorityClass} вне диапазона 0…{ClassCount - 1}");

        if (ServiceTime is null && ServiceTimeOfClass is null)
            throw new InvalidOperationException("Не задана длительность обслуживания: ServiceTime или ServiceTimeOfClass");

        _total.Arrivals++;
        _classes[priorityClass].Arrivals++;

        var job = new Job(priorityClass, _engine.Now);

        if (_inService.Count < _servers)
        {
            StartService(job);
            return true;
        }

        if (Discipline == QueueDiscipline.PreemptiveResumePriority
            && LowestPriorityInService() is { } victim
            && victim.Class > priorityClass)
        {
            Preempt(victim);
            StartService(job);
            return true;
        }

        if (_total.Waiting >= _capacity)
        {
            _total.Rejected++;
            _classes[priorityClass].Rejected++;
            return false;
        }

        Enqueue(job, atHead: false);

        return true;
    }

    /// <summary>Показатели работы по всем классам на текущий момент</summary>
    public ServiceStatistics Statistics() => Snapshot(_total);

    /// <summary>Показатели работы для одного класса заявок</summary>
    /// <remarks>Загрузка здесь — доля мощности приборов, ушедшая на этот класс.</remarks>
    /// <param name="priorityClass">Класс заявок</param>
    public ServiceStatistics Statistics(int priorityClass)
    {
        if (priorityClass < 0 || priorityClass >= ClassCount)
            throw new ArgumentOutOfRangeException(nameof(priorityClass),
                $"Класс {priorityClass} вне диапазона 0…{ClassCount - 1}");

        return Snapshot(_classes[priorityClass]);
    }

    /// <summary>
    /// Забывает накопленную статистику и начинает наблюдение с текущего момента
    /// </summary>
    /// <remarks>
    /// Вызывается после разгонного участка. Заявки в очереди и на приборах остаются: система
    /// продолжает работать с того состояния, к которому пришла, а в средние идёт только то,
    /// что случится дальше.
    /// </remarks>
    public void ResetStatistics()
    {
        _total.Reset(_engine.Now);

        foreach (ClassRecord record in _classes)
            record.Reset(_engine.Now);

        Preemptions = 0;
    }

    private ServiceStatistics Snapshot(ClassRecord record)
        => new(
            record.Arrivals,
            record.Served,
            record.Rejected,
            record.Wait.Mean,
            record.System.Mean,
            record.Queue.Average(_engine.Now),
            record.Busy.Average(_engine.Now) / _servers,
            (int)record.Queue.Maximum);

    private void StartService(Job job)
    {
        if (!job.Started)
        {
            job.Remaining = ServiceTimeOfClass?.Invoke(job.Class) ?? ServiceTime!();

            if (!double.IsFinite(job.Remaining) || job.Remaining < 0)
                throw new InvalidOperationException(
                    $"Длительность обслуживания должна быть конечным неотрицательным числом, получено {job.Remaining}");

            job.TotalService = job.Remaining;
            job.Started = true;
        }

        job.SegmentStart = _engine.Now;
        _inService.Add(job);
        ChangeBusy(job.Class, +1);

        int attempt = job.Attempt;
        _engine.Schedule(job.Remaining, () => CompleteService(job, attempt));
    }

    private void CompleteService(Job job, int attempt)
    {
        // Обслуживание было прервано: это событие устарело
        if (attempt != job.Attempt)
            return;

        _ = _inService.Remove(job);
        ChangeBusy(job.Class, -1);

        double inSystem = _engine.Now - job.ArrivedAt;
        double wait = inSystem - job.TotalService;

        _total.Record(wait, inSystem);
        _classes[job.Class].Record(wait, inSystem);

        if (Dequeue() is { } next)
            StartService(next);
    }

    private void Preempt(Job victim)
    {
        victim.Remaining -= _engine.Now - victim.SegmentStart;
        victim.Attempt++;

        _ = _inService.Remove(victim);
        ChangeBusy(victim.Class, -1);
        Preemptions++;

        Enqueue(victim, atHead: true);
    }

    private Job? LowestPriorityInService()
    {
        Job? lowest = null;

        // При равенстве вытесняется начатая позже: так порядок воспроизводим
        foreach (Job job in _inService)
        {
            if (lowest is null || job.Class >= lowest.Class)
                lowest = job;
        }

        return lowest;
    }

    private void Enqueue(Job job, bool atHead)
    {
        LinkedList<Job> queue = _queues[_queues.Length == 1 ? 0 : job.Class];

        if (atHead)
            _ = queue.AddFirst(job);
        else
            _ = queue.AddLast(job);

        ChangeWaiting(job.Class, +1);
    }

    private Job? Dequeue()
    {
        foreach (LinkedList<Job> queue in _queues)
        {
            if (queue.First is not { } node)
                continue;

            queue.RemoveFirst();
            ChangeWaiting(node.Value.Class, -1);

            return node.Value;
        }

        return null;
    }

    private void ChangeBusy(int priorityClass, int delta)
    {
        _total.InService += delta;
        _total.Busy.Update(_engine.Now, _total.InService);

        ClassRecord record = _classes[priorityClass];
        record.InService += delta;
        record.Busy.Update(_engine.Now, record.InService);
    }

    private void ChangeWaiting(int priorityClass, int delta)
    {
        _total.Waiting += delta;
        _total.Queue.Update(_engine.Now, _total.Waiting);

        ClassRecord record = _classes[priorityClass];
        record.Waiting += delta;
        record.Queue.Update(_engine.Now, record.Waiting);
    }

    /// <summary>Заявка в системе</summary>
    private sealed class Job(int priorityClass, double arrivedAt)
    {
        public int Class { get; } = priorityClass;

        public double ArrivedAt { get; } = arrivedAt;

        public bool Started { get; set; }

        public double TotalService { get; set; }

        public double Remaining { get; set; }

        public double SegmentStart { get; set; }

        public int Attempt { get; set; }
    }

    /// <summary>Счётчики и накопители по классу заявок либо по системе в целом</summary>
    private sealed class ClassRecord
    {
        public Tally Wait { get; } = new();

        public Tally System { get; } = new();

        public TimeWeightedAccumulator Queue { get; } = new();

        public TimeWeightedAccumulator Busy { get; } = new();

        public int Arrivals { get; set; }

        public int Served { get; set; }

        public int Rejected { get; set; }

        public int Waiting { get; set; }

        public int InService { get; set; }

        public void Record(double wait, double inSystem)
        {
            Served++;
            Wait.Observe(wait);
            System.Observe(inSystem);
        }

        public void Reset(double now)
        {
            Arrivals = 0;
            Served = 0;
            Rejected = 0;
            Wait.Reset();
            System.Reset();
            Queue.Reset(now);
            Busy.Reset(now);
        }
    }
}
