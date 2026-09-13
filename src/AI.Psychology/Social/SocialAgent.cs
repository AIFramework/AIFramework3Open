using AI.Psychology.Emotion;
using AI.Psychology.Traits;
using AI.Simulation.Agents;
using AI.Simulation.Space;

namespace AI.Psychology.Social;

/// <summary>Параметры агента, которые определяют его эмоциональную жизнь в группе</summary>
/// <param name="Baseline">Базовая линия аффекта — привычное состояние</param>
/// <param name="RelaxationTime">Время возврата к базовой линии, в единицах шага мира</param>
/// <param name="Spread">Разброс собственных колебаний аффекта</param>
/// <param name="Expressiveness">Выразительность — насколько его чувства заразительны, больше нуля</param>
/// <param name="Openness">Открытость — насколько он впитывает чужие чувства, больше нуля</param>
public sealed record AgentParameters(Affect Baseline, double RelaxationTime, double Spread, double Expressiveness, double Openness);

/// <summary>
/// Агент с чертами, аффектом и мнением: его чувства возвращаются к фону, заражаются от контактов,
/// а мнение подтягивается к мнениям тех контактов, кому он доверяет.
/// </summary>
/// <remarks>
/// Шаг агента складывается из трёх готовых механизмов: возврата аффекта к базовой линии
/// (<see cref="AffectDynamics"/>), заражения с весами εⱼ·α·δᵢ (<see cref="EmotionalContagion"/>) и
/// ограниченного доверия Хегсельмана — Краузе по контактам. Агенты читают только текущие состояния
/// других и записывают следующие, а мир применяет их все сразу — поэтому итог не зависит от порядка
/// обхода, и при выключенных возврате и шуме динамика совпадает с моделью заражения точно.
/// </remarks>
public sealed class SocialAgent : IAgent<SocialWorld>
{
    private Affect _nextAffect;
    private double _nextOpinion;

    /// <summary>Создаёт агента</summary>
    /// <param name="node">Узел сети контактов</param>
    /// <param name="parameters">Параметры</param>
    /// <param name="affect">Начальный аффект</param>
    /// <param name="opinion">Начальное мнение</param>
    /// <param name="traits">Черты личности, если они известны</param>
    public SocialAgent(int node, AgentParameters parameters, Affect affect, double opinion, Personality? traits = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(node);
        ArgumentNullException.ThrowIfNull(parameters);
        Affect.Require(affect, nameof(affect));

        if (!(parameters.Expressiveness > 0) || double.IsInfinity(parameters.Expressiveness)
            || !(parameters.Openness > 0) || double.IsInfinity(parameters.Openness))
            throw new ArgumentOutOfRangeException(nameof(parameters), "Выразительность и открытость — конечные положительные числа");

        if (!double.IsFinite(opinion))
            throw new ArgumentOutOfRangeException(nameof(opinion), "Мнение — конечное число");

        if (traits is not null && !traits.IsValid)
            throw new ArgumentOutOfRangeException(nameof(traits), "Черты лежат на [−1; 1]");

        Node = node;
        Parameters = parameters;
        Traits = traits;
        Dynamics = new AffectDynamics(parameters.Baseline, parameters.RelaxationTime, parameters.Spread);
        Affect = _nextAffect = affect;
        Opinion = _nextOpinion = opinion;
    }

    /// <summary>
    /// Агент по чертам личности: параметры получаются переводом, который задаёт пользователь
    /// </summary>
    /// <param name="node">Узел сети контактов</param>
    /// <param name="traits">Черты</param>
    /// <param name="mapping">Перевод черт в параметры агента</param>
    /// <param name="opinion">Начальное мнение</param>
    /// <param name="affect">Начальный аффект; по умолчанию — базовая линия</param>
    public static SocialAgent FromPersonality(int node, Personality traits, Func<Personality, AgentParameters> mapping, double opinion = 0, Affect? affect = null)
    {
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(mapping);

        AgentParameters parameters = mapping(traits);

        return new SocialAgent(node, parameters, affect ?? parameters.Baseline, opinion, traits);
    }

    /// <summary>Узел сети контактов</summary>
    public int Node { get; }

    /// <summary>Черты личности; <c>null</c> — неизвестны</summary>
    public Personality? Traits { get; }

    /// <summary>Параметры</summary>
    public AgentParameters Parameters { get; }

    /// <summary>Динамика аффекта вокруг базовой линии</summary>
    public AffectDynamics Dynamics { get; }

    /// <summary>Текущий аффект</summary>
    public Affect Affect { get; private set; }

    /// <summary>Текущее мнение</summary>
    public double Opinion { get; private set; }

    /// <summary>Встряска от события: применяется сразу, до следующего шага мира</summary>
    /// <param name="impulse">Толчок в пространстве PAD</param>
    public void Stimulate(Affect impulse) => Affect = _nextAffect = AffectDynamics.Stimulate(Affect, impulse);

    /// <inheritdoc />
    public void Step(SocialWorld world, Random random)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(random);

        double step = world.TimeStep;
        Affect relaxed = world.Noise ? Dynamics.Next(Affect, step, random) : Dynamics.Expected(Affect, step);
        Affect pull = Affect.Neutral;
        double heard = Opinion;
        int trusted = 1;

        foreach (int contact in world.Network.Contacts(Node))
        {
            SocialAgent other = world.AgentAt(contact);
            double weight = other.Parameters.Expressiveness * world.Channel * Parameters.Openness;
            pull += weight * (other.Affect - Affect);

            if (Math.Abs(other.Opinion - Opinion) <= world.Confidence)
            {
                heard += other.Opinion;
                trusted++;
            }
        }

        _nextAffect = (relaxed + (step * pull)).Clamp();
        _nextOpinion = heard / trusted;
    }

    internal void Commit()
    {
        Affect = _nextAffect;
        Opinion = _nextOpinion;
    }
}

/// <summary>Мир социальных агентов: сеть контактов, шаг времени и общие правила общения</summary>
public sealed class SocialWorld
{
    private readonly SocialAgent?[] _agents;

    /// <summary>Создаёт мир</summary>
    /// <param name="network">Сеть контактов</param>
    /// <param name="timeStep">Шаг времени; для устойчивости заражения — не больше 1/max Σⱼ wᵢⱼ</param>
    /// <param name="confidence">Порог доверия к чужому мнению; +∞ — слушают всех контактов</param>
    /// <param name="channel">Сила эмоциональной связи α</param>
    /// <param name="noise">Колеблется ли аффект сам по себе</param>
    public SocialWorld(ContactNetwork network, double timeStep, double confidence = double.PositiveInfinity, double channel = 1, bool noise = true)
    {
        ArgumentNullException.ThrowIfNull(network);

        if (!(timeStep > 0) || double.IsInfinity(timeStep))
            throw new ArgumentOutOfRangeException(nameof(timeStep), "Шаг — конечное положительное число");

        if (!(confidence > 0))
            throw new ArgumentOutOfRangeException(nameof(confidence), "Порог доверия — положительное число");

        if (!(channel >= 0) || double.IsInfinity(channel))
            throw new ArgumentOutOfRangeException(nameof(channel), "Сила связи — конечное неотрицательное число");

        Network = network;
        TimeStep = timeStep;
        Confidence = confidence;
        Channel = channel;
        Noise = noise;
        _agents = new SocialAgent?[network.NodeCount];
    }

    /// <summary>Сеть контактов</summary>
    public ContactNetwork Network { get; }

    /// <summary>Шаг времени</summary>
    public double TimeStep { get; }

    /// <summary>Порог доверия к чужому мнению</summary>
    public double Confidence { get; }

    /// <summary>Сила эмоциональной связи α</summary>
    public double Channel { get; }

    /// <summary>Колеблется ли аффект сам по себе</summary>
    public bool Noise { get; }

    /// <summary>Агенты по узлам сети</summary>
    public IReadOnlyList<SocialAgent> Agents => [.. _agents.Select((agent, node) => agent ?? throw new InvalidOperationException($"Узел {node} без агента"))];

    /// <summary>Средний аффект группы</summary>
    public Affect MeanAffect
    {
        get
        {
            IReadOnlyList<SocialAgent> agents = Agents;

            return new Affect(
                agents.Average(agent => agent.Affect.Pleasure),
                agents.Average(agent => agent.Affect.Arousal),
                agents.Average(agent => agent.Affect.Dominance));
        }
    }

    /// <summary>Агент в узле сети</summary>
    /// <param name="node">Узел</param>
    public SocialAgent AgentAt(int node) =>
        _agents[node] ?? throw new InvalidOperationException($"Узел {node} без агента");

    /// <summary>Ставит агента в его узел</summary>
    /// <param name="agent">Агент</param>
    public void Register(SocialAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (agent.Node >= _agents.Length)
            throw new ArgumentOutOfRangeException(nameof(agent), $"Узла {agent.Node} в сети нет");

        if (_agents[agent.Node] is not null)
            throw new InvalidOperationException($"Узел {agent.Node} уже занят");

        _agents[agent.Node] = agent;
    }

    /// <summary>Модель заражения с параметрами агентов этого мира: для анализа итогового общего чувства</summary>
    public EmotionalContagion Contagion()
    {
        IReadOnlyList<SocialAgent> agents = Agents;

        return new EmotionalContagion(Network,
            [.. agents.Select(agent => agent.Parameters.Expressiveness)],
            [.. agents.Select(agent => agent.Parameters.Openness)],
            Channel > 0 ? Channel : double.Epsilon);
    }

    /// <summary>Применяет следующие состояния всех агентов разом</summary>
    public void Commit()
    {
        foreach (SocialAgent agent in Agents)
            agent.Commit();
    }
}

/// <summary>Сборка агентной модели социального мира</summary>
public static class SocialSimulation
{
    /// <summary>
    /// Ставит агентов в мир и собирает модель, которая после каждого шага применяет состояния разом
    /// </summary>
    /// <param name="world">Мир</param>
    /// <param name="agents">Агенты — по одному на каждый узел сети</param>
    /// <param name="seed">Зерно генератора</param>
    public static AgentBasedModel<SocialWorld> Create(SocialWorld world, IEnumerable<SocialAgent> agents, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agents);

        foreach (SocialAgent agent in agents)
            world.Register(agent);

        var model = new AgentBasedModel<SocialWorld>(world, seed);
        model.AddRange(world.Agents);

        // Без этого шага агенты видели бы уже обновлённых соседей, и итог зависел бы от порядка обхода
        model.AfterStep = m => m.World.Commit();

        return model;
    }
}
