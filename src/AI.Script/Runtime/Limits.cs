using AI.Script.Semantics;

namespace AI.Script.Runtime;

/// <summary>
/// Потолки прогона.
/// </summary>
/// <remarks>
/// Три разных потолка, потому что три разных способа положить хост: незавершающийся цикл
/// (шаги), затянувшийся счёт (время), одна большая аллокация (память). Ни один не заменяет
/// остальные: <c>vec.zeros(1e12)</c> укладывает процесс за один шаг и за одну миллисекунду.
/// </remarks>
public sealed class ScriptLimits
{
    /// <summary>Потолок шагов интерпретатора по умолчанию.</summary>
    public const int DefaultSteps = 5_000_000;

    /// <summary>Потолок вложенности вызовов по умолчанию.</summary>
    /// <remarks>
    /// Не удобство, а защита процесса: тело функции исполняется рекурсией по стеку CLR, а
    /// <c>StackOverflowException</c> в .NET не перехватывается — незавершающаяся рекурсия в
    /// скрипте убила бы весь хост. Потолок шагов здесь не спасает: до него дело не дойдёт.
    /// </remarks>
    public const int DefaultCallDepth = 64;

    /// <summary>Потолок суммарного числа элементов в создаваемых данных по умолчанию.</summary>
    public const long DefaultAllocations = 200_000_000;

    /// <summary>Потолок шагов; ноль и меньше — без потолка.</summary>
    public int Steps { get; set; } = DefaultSteps;

    /// <summary>Потолок вложенности вызовов.</summary>
    public int CallDepth { get; set; } = DefaultCallDepth;

    /// <summary>Потолок суммарного числа элементов данных; ноль и меньше — без потолка.</summary>
    public long Allocations { get; set; } = DefaultAllocations;

    /// <summary>Общий таймаут прогона; <c>null</c> — без таймаута.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Потолок числа платных внешних вызовов за прогон; ноль и меньше — без потолка.
    /// </summary>
    /// <remarks>
    /// Внешний вызов — обращение к оплачиваемой службе (языковая модель, эмбеддер,
    /// переранжировщик). Три потолка вместо одного по той же причине, что и у остальных: цикл,
    /// делающий тысячу дешёвых запросов, и один запрос на миллион токенов разоряют по-разному,
    /// и потолок на одно из этого не ловит другое.
    /// </remarks>
    public int ExternalCalls { get; set; }

    /// <summary>Потолок суммарного числа токенов за прогон; ноль и меньше — без потолка.</summary>
    public long ExternalTokens { get; set; }

    /// <summary>Потолок стоимости прогона в единицах биллинга; ноль и меньше — без потолка.</summary>
    public decimal ExternalCost { get; set; }

    /// <summary>Копия текущих значений.</summary>
    public ScriptLimits Clone() => new()
    {
        Steps = Steps,
        CallDepth = CallDepth,
        Allocations = Allocations,
        Timeout = Timeout,
        ExternalCalls = ExternalCalls,
        ExternalTokens = ExternalTokens,
        ExternalCost = ExternalCost,
    };
}

/// <summary>
/// Счётчики прогона, сверяемые с потолками.
/// </summary>
/// <remarks>
/// Считают через <see cref="Interlocked"/>: на параллельном участке
/// (<c>core.map(parallel: true)</c>) шаги и выделения приходят из нескольких потоков сразу, и
/// несинхронизированный инкремент превратил бы потолок в пожелание.
/// </remarks>
public sealed class LimitCounters
{
    private readonly ScriptLimits _limits;

    private readonly object _costSync = new();

    private int _steps;
    private long _allocations;
    private int _externalCalls;
    private long _externalTokens;
    private decimal _externalCost;

    /// <summary>Создаёт счётчики.</summary>
    public LimitCounters(ScriptLimits limits) => _limits = limits;

    /// <summary>Сделано шагов.</summary>
    public int Steps => Volatile.Read(ref _steps);

    /// <summary>Выделено элементов данных.</summary>
    public long Allocations => Interlocked.Read(ref _allocations);

    /// <summary>Считает шаг; бросает при выходе за потолок.</summary>
    public void CountStep()
    {
        if (_limits.Steps <= 0) return;

        if (Interlocked.Increment(ref _steps) > _limits.Steps)
        {
            throw new ScriptAbort(
                DiagnosticCodes.StepLimit,
                $"превышен потолок в {_limits.Steps} шагов интерпретатора",
                "цикл не завершается либо задача слишком велика для прототипа: поднимите options.steps или перенесите счёт в функцию библиотеки");
        }
    }

    /// <summary>Сделано платных внешних вызовов.</summary>
    public int ExternalCalls => Volatile.Read(ref _externalCalls);

    /// <summary>Израсходовано токенов.</summary>
    public long ExternalTokens => Interlocked.Read(ref _externalTokens);

    /// <summary>Потрачено в единицах биллинга.</summary>
    public decimal ExternalCost
    {
        get
        {
            lock (_costSync) return _externalCost;
        }
    }

    /// <summary>
    /// Заявляет о намерении сделать платный вызов; бросает, если вызовов уже было довольно.
    /// </summary>
    /// <remarks>
    /// Проверяется до запроса, а не после: потолок «не больше двух вызовов», срабатывающий по
    /// итогам третьего, разрешает ровно тот вызов, который должен был запретить, — и он уже
    /// оплачен. Число вызовов, в отличие от числа токенов, известно заранее, поэтому здесь
    /// откладывать проверку незачем.
    /// </remarks>
    public void BeginExternalCall()
    {
        if (_limits.ExternalCalls > 0 && Volatile.Read(ref _externalCalls) >= _limits.ExternalCalls)
        {
            throw new ScriptAbort(
                DiagnosticCodes.CostLimit,
                $"превышен потолок в {_limits.ExternalCalls} внешних вызовов за прогон",
                "уменьшите число обращений к модели либо поднимите потолок в настройках хоста");
        }

        _ = Interlocked.Increment(ref _externalCalls);
    }

    /// <summary>
    /// Учитывает расход состоявшегося вызова; бросает при выходе за потолок токенов или стоимости.
    /// </summary>
    /// <param name="tokens">Израсходовано токенов; ноль, если служба их не сообщает.</param>
    /// <param name="cost">Стоимость вызова; ноль, если служба её не сообщает.</param>
    /// <remarks>
    /// Здесь отказ приходит уже после того, как ответ получен и оплачен, и иначе быть не может:
    /// сколько токенов вернёт модель, до запроса не знает никто. Потолок токенов поэтому
    /// останавливает следующий вызов, а не текущий.
    /// </remarks>
    public void CountExternal(long tokens, decimal cost)
    {
        long total = Interlocked.Add(ref _externalTokens, Math.Max(0, tokens));

        if (_limits.ExternalTokens > 0 && total > _limits.ExternalTokens)
        {
            throw new ScriptAbort(
                DiagnosticCodes.CostLimit,
                $"превышен потолок в {_limits.ExternalTokens} токенов за прогон",
                "укоротите промпты либо поднимите потолок в настройках хоста");
        }

        decimal spent;

        lock (_costSync) spent = _externalCost += Math.Max(0, cost);

        if (_limits.ExternalCost > 0 && spent > _limits.ExternalCost)
        {
            throw new ScriptAbort(
                DiagnosticCodes.CostLimit,
                $"превышен потолок стоимости прогона ({_limits.ExternalCost})",
                "возьмите модель дешевле либо поднимите потолок в настройках хоста");
        }
    }

    /// <summary>Учитывает выделение элементов; бросает при выходе за потолок.</summary>
    public void CountAllocation(long elements)
    {
        if (_limits.Allocations <= 0 || elements <= 0) return;

        if (Interlocked.Add(ref _allocations, elements) > _limits.Allocations)
        {
            throw new ScriptAbort(
                DiagnosticCodes.MemoryLimit,
                $"превышен потолок в {_limits.Allocations} элементов данных за прогон",
                "уменьшите размеры данных либо поднимите потолок в настройках хоста");
        }
    }
}
