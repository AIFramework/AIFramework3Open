namespace AI.Simulation.DiscreteEvent;

/// <summary>
/// Накопитель среднего по наблюдениям
/// </summary>
/// <remarks>
/// Считает среднее по выборке значений — время ожидания, длину очереди в момент прихода
/// и прочие величины, наблюдаемые в отдельные моменты.
/// </remarks>
public sealed class Tally
{
    private double _sum;
    private double _sumOfSquares;
    private double _minimum = double.PositiveInfinity;
    private double _maximum = double.NegativeInfinity;

    /// <summary>Число наблюдений</summary>
    public int Count { get; private set; }

    /// <summary>Среднее значение</summary>
    public double Mean => Count == 0 ? 0 : _sum / Count;

    /// <summary>Наименьшее значение</summary>
    public double Minimum => Count == 0 ? 0 : _minimum;

    /// <summary>Наибольшее значение</summary>
    public double Maximum => Count == 0 ? 0 : _maximum;

    /// <summary>Выборочное стандартное отклонение</summary>
    public double StandardDeviation
    {
        get
        {
            if (Count < 2)
                return 0;

            double variance = (_sumOfSquares - (_sum * _sum / Count)) / (Count - 1);

            return variance <= 0 ? 0 : Math.Sqrt(variance);
        }
    }

    /// <summary>Добавляет наблюдение</summary>
    /// <param name="value">Значение</param>
    public void Observe(double value)
    {
        Count++;
        _sum += value;
        _sumOfSquares += value * value;
        _minimum = Math.Min(_minimum, value);
        _maximum = Math.Max(_maximum, value);
    }
}

/// <summary>
/// Накопитель среднего по времени
/// </summary>
/// <remarks>
/// Средняя длина очереди — это не среднее по клиентам, а среднее по времени: очередь
/// длиной десять, простоявшая секунду, весит меньше очереди длиной два, простоявшей час.
/// Путаница этих двух средних — обычная ошибка в отчётах по моделированию.
/// </remarks>
public sealed class TimeWeightedAccumulator
{
    private double _area;
    private double _lastTime;
    private double _lastValue;

    /// <summary>Текущее значение</summary>
    public double Current => _lastValue;

    /// <summary>Наибольшее достигнутое значение</summary>
    public double Maximum { get; private set; }

    /// <summary>Средневзвешенное по времени значение</summary>
    /// <param name="now">Текущее модельное время</param>
    public double Average(double now)
    {
        double total = _area + (_lastValue * (now - _lastTime));

        return now <= 0 ? 0 : total / now;
    }

    /// <summary>Обновляет значение в заданный момент</summary>
    /// <param name="now">Модельное время</param>
    /// <param name="value">Новое значение</param>
    public void Update(double now, double value)
    {
        _area += _lastValue * (now - _lastTime);
        _lastTime = now;
        _lastValue = value;
        Maximum = Math.Max(Maximum, value);
    }
}

/// <summary>
/// Ядро дискретно-событийного моделирования.
/// </summary>
/// <remarks>
/// <para>
/// Модельное время движется скачками от события к событию, а не равномерными шагами:
/// между приходом клиента и окончанием обслуживания в системе ничего не происходит,
/// и считать эти промежутки незачем. Поэтому час работы банка моделируется за доли секунды,
/// а не за час.
/// </para>
/// <para>
/// Календарь событий построен на <c>PriorityQueue</c> стандартной библиотеки: очередь
/// с приоритетом в <c>AI.Algorithms</c> создаётся с фиксированной ёмкостью, а число
/// событий в модели заранее неизвестно.
/// </para>
/// <para>
/// Порядок одновременных событий определяется номером постановки в очередь: при равном
/// времени первым выполняется запланированное раньше. Без этого правила результат зависел бы
/// от внутреннего устройства очереди и не воспроизводился.
/// </para>
/// </remarks>
public sealed class SimulationEngine
{
    private readonly PriorityQueue<Action, (double Time, long Order)> _calendar =
        new(Comparer<(double Time, long Order)>.Create((left, right) =>
        {
            int byTime = left.Time.CompareTo(right.Time);

            return byTime != 0 ? byTime : left.Order.CompareTo(right.Order);
        }));

    private long _order;

    /// <summary>Создаёт модель</summary>
    /// <param name="seed">Зерно генератора случайных чисел; нужно для воспроизводимости</param>
    public SimulationEngine(int? seed = null) => Random = seed is null ? new Random() : new Random(seed.Value);

    /// <summary>Текущее модельное время</summary>
    public double Now { get; private set; }

    /// <summary>Генератор случайных чисел модели</summary>
    public Random Random { get; }

    /// <summary>Число выполненных событий</summary>
    public long ProcessedEvents { get; private set; }

    /// <summary>Есть ли ещё запланированные события</summary>
    public bool HasEvents => _calendar.Count > 0;

    /// <summary>
    /// Планирует событие через заданное время
    /// </summary>
    /// <param name="delay">Задержка от текущего момента</param>
    /// <param name="action">Что произойдёт</param>
    public void Schedule(double delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (delay < 0)
            throw new ArgumentOutOfRangeException(nameof(delay), "Событие нельзя запланировать в прошлое");

        _calendar.Enqueue(action, (Now + delay, _order++));
    }

    /// <summary>
    /// Планирует событие на заданный момент модельного времени
    /// </summary>
    /// <param name="time">Момент</param>
    /// <param name="action">Что произойдёт</param>
    public void ScheduleAt(double time, Action action)
    {
        if (time < Now)
            throw new ArgumentOutOfRangeException(nameof(time), "Событие нельзя запланировать в прошлое");

        Schedule(time - Now, action);
    }

    /// <summary>
    /// Выполняет события до заданного момента
    /// </summary>
    /// <param name="until">Момент остановки</param>
    /// <returns>Достигнут ли указанный момент; <c>false</c>, если события кончились раньше</returns>
    public bool Run(double until)
    {
        while (_calendar.TryPeek(out _, out (double Time, long Order) key) && key.Time <= until)
        {
            Action action = _calendar.Dequeue();
            Now = key.Time;
            ProcessedEvents++;
            action();
        }

        Now = Math.Max(Now, until);

        return true;
    }

    /// <summary>Выполняет все запланированные события</summary>
    /// <param name="eventLimit">Предел числа событий</param>
    public void RunToCompletion(long eventLimit = long.MaxValue)
    {
        while (_calendar.TryDequeue(out Action? action, out (double Time, long Order) key)
            && ProcessedEvents < eventLimit)
        {
            Now = key.Time;
            ProcessedEvents++;
            action();
        }
    }

    /// <summary>Экспоненциально распределённая задержка с заданной интенсивностью</summary>
    /// <param name="rate">Интенсивность потока</param>
    public double Exponential(double rate) => AI.Statistics.RandomEngine.NextExponential(Random, rate);

    /// <summary>Равномерная задержка на отрезке</summary>
    /// <param name="low">Наименьшее значение</param>
    /// <param name="high">Наибольшее значение</param>
    public double Uniform(double low, double high) => low + (Random.NextDouble() * (high - low));
}
