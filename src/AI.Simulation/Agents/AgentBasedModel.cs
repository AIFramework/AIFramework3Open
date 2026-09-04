namespace AI.Simulation.Agents;

/// <summary>
/// Участник агентной модели
/// </summary>
/// <typeparam name="TWorld">Тип среды</typeparam>
public interface IAgent<in TWorld>
{
    /// <summary>
    /// Один шаг поведения агента
    /// </summary>
    /// <param name="world">Среда, в которой он действует</param>
    /// <param name="random">Общий генератор случайных чисел модели</param>
    void Step(TWorld world, Random random);
}

/// <summary>Порядок обхода агентов на шаге</summary>
public enum ActivationOrder
{
    /// <summary>В порядке добавления — воспроизводимо, но вносит систематическое преимущество первых</summary>
    Sequential,

    /// <summary>Случайный порядок на каждом шаге — обычный выбор в агентном моделировании</summary>
    Shuffled
}

/// <summary>
/// Каркас агентного моделирования.
/// </summary>
/// <remarks>
/// <para>
/// Модель задаётся не уравнениями для целого, а правилами поведения отдельных участников;
/// поведение системы получается из их взаимодействия. Так описывается то, что не сводится
/// к среднему: расслоение, заторы, эпидемии на сети знакомств, обвалы рынка.
/// </para>
/// <para>
/// Порядок обхода агентов — не деталь реализации, а часть модели. При последовательном
/// обходе первые в списке систематически получают преимущество, и результат оказывается
/// артефактом нумерации; поэтому по умолчанию порядок перемешивается на каждом шаге.
/// </para>
/// </remarks>
/// <typeparam name="TWorld">Тип среды</typeparam>
public sealed class AgentBasedModel<TWorld>
{
    private readonly List<IAgent<TWorld>> _agents = [];
    private readonly List<int> _order = [];

    /// <summary>Создаёт модель</summary>
    /// <param name="world">Среда</param>
    /// <param name="seed">Зерно генератора; нужно для воспроизводимости</param>
    /// <param name="activation">Порядок обхода агентов</param>
    public AgentBasedModel(TWorld world, int? seed = null, ActivationOrder activation = ActivationOrder.Shuffled)
    {
        World = world;
        Random = seed is null ? new Random() : new Random(seed.Value);
        Activation = activation;
    }

    /// <summary>Среда</summary>
    public TWorld World { get; }

    /// <summary>Генератор случайных чисел модели</summary>
    public Random Random { get; }

    /// <summary>Порядок обхода агентов</summary>
    public ActivationOrder Activation { get; }

    /// <summary>Число выполненных шагов</summary>
    public int Steps { get; private set; }

    /// <summary>Агенты модели</summary>
    public IReadOnlyList<IAgent<TWorld>> Agents => _agents;

    /// <summary>Действие, выполняемое после каждого шага — сбор показателей</summary>
    public Action<AgentBasedModel<TWorld>>? AfterStep { get; set; }

    /// <summary>Условие досрочной остановки</summary>
    public Func<AgentBasedModel<TWorld>, bool>? StopWhen { get; set; }

    /// <summary>Добавляет агента</summary>
    /// <param name="agent">Агент</param>
    public void Add(IAgent<TWorld> agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);
        _order.Add(_order.Count);
    }

    /// <summary>Добавляет несколько агентов</summary>
    /// <param name="agents">Агенты</param>
    public void AddRange(IEnumerable<IAgent<TWorld>> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        foreach (IAgent<TWorld> agent in agents)
            Add(agent);
    }

    /// <summary>Выполняет один шаг модели</summary>
    public void Step()
    {
        if (Activation == ActivationOrder.Shuffled)
            Shuffle();

        foreach (int index in _order)
            _agents[index].Step(World, Random);

        Steps++;
        AfterStep?.Invoke(this);
    }

    /// <summary>
    /// Выполняет шаги, пока не исчерпан предел либо не сработало условие остановки
    /// </summary>
    /// <param name="steps">Предельное число шагов</param>
    /// <returns>Сколько шагов выполнено</returns>
    public int Run(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        int executed = 0;

        for (int i = 0; i < steps; i++)
        {
            if (StopWhen?.Invoke(this) == true)
                break;

            Step();
            executed++;
        }

        return executed;
    }

    private void Shuffle()
    {
        for (int i = _order.Count - 1; i > 0; i--)
        {
            int j = Random.Next(i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
    }
}
