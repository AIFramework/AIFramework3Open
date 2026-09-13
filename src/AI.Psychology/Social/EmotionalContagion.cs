using AI.DataStructs.Algebraic;
using AI.Simulation.Social;
using AI.Simulation.Space;
using AI.Simulation.SystemDynamics;

namespace AI.Psychology.Social;

/// <summary>
/// Эмоциональное заражение: люди подстраивают свои чувства под чувства тех, с кем общаются.
/// </summary>
/// <remarks>
/// <para>
/// Модель ASCRIBE Боссе, Трёра и ван дер Вала (2009): dqᵢ/dt = Σⱼ wᵢⱼ·(qⱼ − qᵢ), где сила канала
/// wᵢⱼ = εⱼ·α·δᵢ складывается из выразительности отправителя εⱼ, силы связи α и открытости получателя δᵢ.
/// Выразительный человек заражает других сильнее, открытый сильнее заражается сам.
/// </para>
/// <para>
/// Это непрерывная модель Де Грота с лапласианом вместо матрицы влияния, и её согласие считает
/// готовая <see cref="DeGrootModel"/>: шаг Эйлера I − h·L стохастичен по строкам и при любом
/// допустимом h имеет тот же левый собственный вектор. Для симметричных связей ответ известен
/// в замкнутом виде: вес чувства человека в общем итоге пропорционален εᵢ/δᵢ — громко выражать и
/// мало впитывать значит задавать настроение группы. Решение уравнений моделью запасов из
/// <c>AI.Simulation</c> — независимая проверка обоих ответов.
/// </para>
/// </remarks>
public sealed class EmotionalContagion
{
    private readonly double[,] _weights;
    private readonly double[] _strength;

    /// <summary>Создаёт модель на сети контактов</summary>
    /// <param name="network">Сеть контактов</param>
    /// <param name="expressiveness">Выразительность каждого — насколько его чувства видны другим, больше нуля</param>
    /// <param name="openness">Открытость каждого — насколько он впитывает чужие чувства, больше нуля</param>
    /// <param name="channel">Сила связи α, одинаковая для всех контактов</param>
    public EmotionalContagion(ContactNetwork network, IReadOnlyList<double> expressiveness, IReadOnlyList<double> openness, double channel = 1)
    {
        ArgumentNullException.ThrowIfNull(network);
        int n = network.NodeCount;
        RequirePositive(expressiveness, n, nameof(expressiveness));
        RequirePositive(openness, n, nameof(openness));

        if (!(channel > 0) || double.IsInfinity(channel))
            throw new ArgumentOutOfRangeException(nameof(channel), "Сила связи — конечное положительное число");

        Network = network;
        Expressiveness = [.. expressiveness];
        Openness = [.. openness];
        Channel = channel;
        _weights = new double[n, n];
        _strength = new double[n];

        for (int i = 0; i < n; i++)
        {
            foreach (int j in network.Contacts(i))
            {
                _weights[i, j] = expressiveness[j] * channel * openness[i];
                _strength[i] += _weights[i, j];
            }
        }
    }

    /// <summary>Сеть контактов</summary>
    public ContactNetwork Network { get; }

    /// <summary>Выразительность ε</summary>
    public IReadOnlyList<double> Expressiveness { get; }

    /// <summary>Открытость δ</summary>
    public IReadOnlyList<double> Openness { get; }

    /// <summary>Сила связи α</summary>
    public double Channel { get; }

    /// <summary>Число людей</summary>
    public int Size => _strength.Length;

    /// <summary>Наибольший шаг, при котором шаг Эйлера I − h·L остаётся стохастическим: 1/max Σⱼ wᵢⱼ</summary>
    public double MaxStableStep => _strength.Max() > 0 ? 1 / _strength.Max() : double.PositiveInfinity;

    /// <summary>Сила канала от отправителя к получателю wᵢⱼ = εⱼ·α·δᵢ</summary>
    /// <param name="receiver">Получатель</param>
    /// <param name="sender">Отправитель</param>
    public double Weight(int receiver, int sender) => _weights[receiver, sender];

    /// <summary>Скорости изменения чувств: dqᵢ/dt = Σⱼ wᵢⱼ·(qⱼ − qᵢ)</summary>
    /// <param name="emotions">Текущие чувства</param>
    public double[] Rates(IReadOnlyList<double> emotions)
    {
        InfluenceMatrixCheck(emotions);

        return [.. Enumerable.Range(0, Size).Select(i => Rate(i, emotions))];
    }

    /// <summary>Решение уравнений заражения моделью запасов (Рунге — Кутта)</summary>
    /// <param name="initial">Начальные чувства</param>
    /// <param name="time">Время расчёта</param>
    /// <param name="points">Число точек вывода</param>
    /// <param name="stepsPerInterval">Шагов интегрирования между точками</param>
    public IReadOnlyList<SystemState> Simulate(IReadOnlyList<double> initial, double time, int points = 100, int stepsPerInterval = 20)
    {
        InfluenceMatrixCheck(initial);

        var model = new StockFlowModel("Эмоциональное заражение");

        for (int i = 0; i < Size; i++)
        {
            int receiver = i;
            model.AddStock($"q{i}", initial[i], (_, levels) => Rate(receiver, levels));
        }

        return model.Run(time, points, stepsPerInterval);
    }

    /// <summary>Шаг Эйлера как модель Де Грота: W = I − h·L</summary>
    /// <param name="step">Шаг h, не больше <see cref="MaxStableStep"/></param>
    public DeGrootModel Discretize(double step)
    {
        if (!(step > 0 && step <= MaxStableStep))
            throw new ArgumentOutOfRangeException(nameof(step), $"Шаг лежит на (0; {MaxStableStep}]: иначе веса станут отрицательными");

        var influence = new Matrix(Size, Size);

        for (int i = 0; i < Size; i++)
        {
            influence[i, i] = 1 - (step * _strength[i]);

            for (int j = 0; j < Size; j++)
            {
                if (j != i)
                    influence[i, j] = step * _weights[i, j];
            }
        }

        return new DeGrootModel(influence);
    }

    /// <summary>Вес чувства каждого в итоговом общем чувстве</summary>
    public IReadOnlyList<double> SocialPower() => Discretize(MaxStableStep / 2).SocialPower();

    /// <summary>Итоговое общее чувство, к которому придёт связная группа</summary>
    /// <param name="initial">Начальные чувства</param>
    public double Consensus(IReadOnlyList<double> initial) => Discretize(MaxStableStep / 2).Consensus(initial);

    private double Rate(int receiver, IReadOnlyList<double> emotions)
    {
        double rate = 0;

        foreach (int sender in Network.Contacts(receiver))
            rate += _weights[receiver, sender] * (emotions[sender] - emotions[receiver]);

        return rate;
    }

    private void InfluenceMatrixCheck(IReadOnlyList<double> emotions)
    {
        ArgumentNullException.ThrowIfNull(emotions);

        if (emotions.Count != Size)
            throw new ArgumentException($"Чувств {emotions.Count}, а людей {Size}", nameof(emotions));

        if (emotions.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Чувства — конечные числа", nameof(emotions));
    }

    private static void RequirePositive(IReadOnlyList<double> values, int count, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);

        if (values.Count != count)
            throw new ArgumentException($"Значений {values.Count}, а людей {count}", name);

        if (values.Any(value => !(value > 0) || double.IsInfinity(value)))
            throw new ArgumentOutOfRangeException(name, "Значения — конечные положительные числа");
    }
}
