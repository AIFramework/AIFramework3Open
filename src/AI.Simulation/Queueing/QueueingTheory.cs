using AI.Insights;

namespace AI.Simulation.Queueing;

/// <summary>Показатели системы массового обслуживания в установившемся режиме</summary>
/// <param name="Utilisation">Загрузка приборов ρ</param>
/// <param name="QueueLength">Средняя длина очереди L_q</param>
/// <param name="SystemLength">Среднее число заявок в системе L</param>
/// <param name="WaitTime">Среднее время ожидания W_q</param>
/// <param name="SystemTime">Среднее время пребывания W</param>
/// <param name="IdleProbability">Вероятность простоя системы</param>
public readonly record struct QueueMetrics(
    double Utilisation, double QueueLength, double SystemLength,
    double WaitTime, double SystemTime, double IdleProbability) : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Система массового обслуживания")
            .Summary($"Загрузка {Fmt.Pct(Utilisation)}. В очереди в среднем {Fmt.Num(QueueLength, 3)} заявки, "
                + $"в системе {Fmt.Num(SystemLength, 3)}. Ожидание {Fmt.Num(WaitTime, 4)}, "
                + $"полное время {Fmt.Num(SystemTime, 4)}.")
            .Metric("ρ", Fmt.Pct(Utilisation), null, "доля времени занятости приборов",
                Utilisation >= 0.9 ? MetricQuality.Critical : Utilisation > 0.7 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("L_q", Fmt.Num(QueueLength, 4), null, "среднее число ожидающих")
            .Metric("W_q", Fmt.Num(WaitTime, 5), null, "среднее время ожидания")
            .Metric("W", Fmt.Num(SystemTime, 5), null, "ожидание плюс обслуживание")
            .Metric("P₀", Fmt.Pct(IdleProbability), null, "вероятность застать систему пустой")
            .Finding("Формула Литтла связывает всё воедино: среднее число заявок равно интенсивности "
                + "потока, умноженной на среднее время пребывания. Она верна для любой дисциплины "
                + "обслуживания и любых распределений.")
            .FindingIf(Utilisation >= 0.8,
                "Загрузка близка к единице: длина очереди растёт как 1/(1−ρ). Здесь каждая доля процента "
                + "нагрузки стоит дороже предыдущей, и запас мощности перестаёт быть роскошью.")
            .Warning("Формулы верны для простейшего потока и показательного обслуживания. Реальные потоки "
                + "часто неоднородны по времени суток, а обслуживание менее изменчиво, чем показательное, "
                + "и тогда действительная очередь короче расчётной.")
            .Build();
}

/// <summary>
/// Аналитические формулы теории массового обслуживания.
/// </summary>
/// <remarks>
/// <para>
/// Дают точный ответ для простейшего потока и показательного времени обслуживания —
/// и служат проверкой для имитационной модели: если моделирование системы M/M/1
/// не сходится к этим числам, ошибка в модели, а не в теории.
/// </para>
/// <para>
/// Главный вывод формул важнее самих чисел: зависимость от загрузки нелинейна. При ρ = 0.5
/// в очереди одна заявка, при 0.9 — девять, при 0.99 — девяносто девять. Планировать
/// мощность «под завязку» нельзя не из осторожности, а из-за вида этой формулы.
/// </para>
/// </remarks>
public static class QueueingTheory
{
    /// <summary>
    /// Одноканальная система с неограниченной очередью (M/M/1)
    /// </summary>
    /// <param name="arrivalRate">Интенсивность потока заявок λ</param>
    /// <param name="serviceRate">Интенсивность обслуживания μ</param>
    /// <exception cref="ArgumentException">Загрузка не меньше единицы: очередь растёт неограниченно</exception>
    public static QueueMetrics SingleServer(double arrivalRate, double serviceRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arrivalRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serviceRate);

        double rho = arrivalRate / serviceRate;

        if (rho >= 1)
            throw new ArgumentException(
                $"Загрузка ρ = {rho:F3} не меньше единицы: установившегося режима нет, очередь растёт без предела",
                nameof(arrivalRate));

        double systemLength = rho / (1 - rho);
        double queueLength = rho * rho / (1 - rho);
        double systemTime = 1 / (serviceRate - arrivalRate);
        double waitTime = rho / (serviceRate - arrivalRate);

        return new QueueMetrics(rho, queueLength, systemLength, waitTime, systemTime, 1 - rho);
    }

    /// <summary>
    /// Многоканальная система с неограниченной очередью (M/M/c)
    /// </summary>
    /// <param name="arrivalRate">Интенсивность потока заявок</param>
    /// <param name="serviceRate">Интенсивность обслуживания одним прибором</param>
    /// <param name="servers">Число приборов</param>
    public static QueueMetrics MultiServer(double arrivalRate, double serviceRate, int servers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arrivalRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serviceRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(servers);

        double offered = arrivalRate / serviceRate;
        double rho = offered / servers;

        if (rho >= 1)
            throw new ArgumentException(
                $"Загрузка ρ = {rho:F3} не меньше единицы: установившегося режима нет", nameof(servers));

        double sum = 0;
        double term = 1;

        for (int n = 0; n < servers; n++)
        {
            sum += term;
            term *= offered / (n + 1);
        }

        double last = term * servers / (servers - offered);
        double idle = 1.0 / (sum + last);

        double waitingProbability = last * idle;
        double queueLength = waitingProbability * rho / (1 - rho);
        double waitTime = queueLength / arrivalRate;
        double systemTime = waitTime + (1 / serviceRate);

        return new QueueMetrics(rho, queueLength, arrivalRate * systemTime, waitTime, systemTime, idle);
    }

    /// <summary>
    /// Вероятность ожидания в многоканальной системе — формула Эрланга C
    /// </summary>
    /// <param name="arrivalRate">Интенсивность потока</param>
    /// <param name="serviceRate">Интенсивность обслуживания</param>
    /// <param name="servers">Число приборов</param>
    public static double ErlangC(double arrivalRate, double serviceRate, int servers)
    {
        QueueMetrics metrics = MultiServer(arrivalRate, serviceRate, servers);

        return metrics.QueueLength <= 0 ? 0 : metrics.QueueLength * (1 - metrics.Utilisation) / metrics.Utilisation;
    }

    /// <summary>
    /// Вероятность отказа в системе без очереди — формула Эрланга B
    /// </summary>
    /// <param name="offeredLoad">Предложенная нагрузка в эрлангах</param>
    /// <param name="servers">Число приборов</param>
    /// <remarks>
    /// Расчёт ведётся рекуррентно, а не по факториалам: при сотне приборов прямая формула
    /// переполняет числа с плавающей точкой задолго до ответа.
    /// </remarks>
    public static double ErlangB(double offeredLoad, int servers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offeredLoad);
        ArgumentOutOfRangeException.ThrowIfNegative(servers);

        double blocking = 1.0;

        for (int n = 1; n <= servers; n++)
            blocking = offeredLoad * blocking / (n + (offeredLoad * blocking));

        return blocking;
    }

    /// <summary>
    /// Одноканальная система с ограниченным накопителем (M/M/1/K)
    /// </summary>
    /// <param name="arrivalRate">Интенсивность потока</param>
    /// <param name="serviceRate">Интенсивность обслуживания</param>
    /// <param name="capacity">Наибольшее число заявок в системе</param>
    /// <returns>Показатели и вероятность отказа</returns>
    public static (QueueMetrics Metrics, double BlockingProbability) LimitedQueue(
        double arrivalRate, double serviceRate, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        double rho = arrivalRate / serviceRate;

        double idle = Math.Abs(rho - 1) < 1e-12
            ? 1.0 / (capacity + 1)
            : (1 - rho) / (1 - Math.Pow(rho, capacity + 1));

        double blocking = idle * Math.Pow(rho, capacity);
        double effectiveRate = arrivalRate * (1 - blocking);

        double systemLength = 0;

        for (int n = 1; n <= capacity; n++)
            systemLength += n * idle * Math.Pow(rho, n);

        double systemTime = effectiveRate <= 0 ? 0 : systemLength / effectiveRate;
        double serviceTime = 1 / serviceRate;
        double waitTime = Math.Max(0, systemTime - serviceTime);
        double queueLength = effectiveRate * waitTime;

        return (
            new QueueMetrics(1 - idle, queueLength, systemLength, waitTime, systemTime, idle),
            blocking);
    }

    /// <summary>
    /// Формула Литтла: среднее число заявок равно интенсивности, умноженной на среднее время
    /// </summary>
    /// <param name="arrivalRate">Интенсивность потока</param>
    /// <param name="averageTime">Среднее время пребывания</param>
    public static double LittleLaw(double arrivalRate, double averageTime) => arrivalRate * averageTime;
}
