using AI.Algorithms.EWG;
using AI.Algorithms.GraphStructure;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Simulation.Space;

namespace AI.Simulation.Social;

/// <summary>Строение сети влияния: придёт ли она к согласию, чей голос в нём весит больше и как быстро</summary>
public sealed class InfluenceAnalysis : IInterpretable
{
    internal InfluenceAnalysis(int size, bool stronglyConnected, int period, IReadOnlyList<double>? socialPower, double secondEigenvalue)
    {
        Size = size;
        IsStronglyConnected = stronglyConnected;
        Period = period;
        SocialPower = socialPower;
        SecondEigenvalueModulus = secondEigenvalue;
    }

    /// <summary>Число агентов</summary>
    public int Size { get; }

    /// <summary>Сильно связна ли сеть: влияние от каждого доходит до каждого</summary>
    public bool IsStronglyConnected { get; }

    /// <summary>Период цепи: 1 — мнения сходятся; больше 1 — колеблются по кругу; 0 — сеть не связна</summary>
    public int Period { get; }

    /// <summary>Придут ли все к одному мнению из любых начальных</summary>
    public bool ReachesConsensus => IsStronglyConnected && Period == 1;

    /// <summary>Социальная сила: вес начального мнения каждого агента в итоговом согласии; <c>null</c> — сеть не связна</summary>
    public IReadOnlyList<double>? SocialPower { get; }

    /// <summary>Модуль второго по величине собственного числа матрицы влияния: чем ближе к 1, тем медленнее согласие</summary>
    public double SecondEigenvalueModulus { get; }

    /// <summary>Шагов, за которые разногласия уменьшаются в 100 раз: ln 0,01/ln|λ₂|</summary>
    public double StepsToHundredfold => SecondEigenvalueModulus <= 0
        ? 1
        : SecondEigenvalueModulus >= 1 ? double.PositiveInfinity : Math.Log(0.01) / Math.Log(SecondEigenvalueModulus);

    /// <summary>Число равных голосов, которому равносильно итоговое согласие: 1/Σπ²</summary>
    public double EffectiveVoices => SocialPower is null ? double.NaN : 1 / SocialPower.Sum(p => p * p);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var builder = new InterpretationBuilder("Сеть влияния Де Грота")
            .Metric("Агентов", Size, null, null, MetricQuality.Unknown, 0)
            .Metric("|λ₂|", SecondEigenvalueModulus, null, "второе собственное число: скорость забывания разногласий", MetricQuality.Unknown, 4)
            .Metric("Шагов до сокращения разногласий в 100 раз", StepsToHundredfold, null, null, MetricQuality.Unknown, 1);

        if (!IsStronglyConnected)
        {
            return builder
                .Summary("Сеть влияния распадается на части, до которых не доходит влияние друг друга: общего согласия не будет.")
                .Finding("Каждая замкнутая группа придёт к своему мнению, а те, кто только слушает, окажутся между ними.")
                .Build();
        }

        int leader = Enumerable.Range(0, Size).MaxBy(i => SocialPower![i]);
        double share = SocialPower![leader];

        return builder
            .Summary(ReachesConsensus
                ? $"Все придут к одному мнению — среднему начальных с весами социальной силы; сильнее всех агент {leader + 1}: {Fmt.Pct(share)} итога."
                : $"Влияние доходит до всех, но мнения колеблются с периодом {Period} и к согласию не приходят.")
            .Metric("Равносильно голосов", EffectiveVoices, null, "1/Σπ²: сколько равных участников дали бы такое же согласие", MetricQuality.Unknown, 1)
            .Metric("Наибольшая социальная сила", share, null, $"агент {leader + 1}", MetricQuality.Unknown, 3)
            .FindingIf(share > 2.0 / Size,
                $"Начальное мнение агента {leader + 1} весит в итоге в {Fmt.Num(share * Size, 1)} раза больше равной доли: "
                + "согласие смещено к тем, кого слушают многие и кто сам мало кого слушает.")
            .FindingIf(Period > 1, "Никто не удерживает долю своего мнения, и мнения ходят по кругу. Достаточно любому агенту немного доверять себе, чтобы колебания затухли.")
            .Warning("Модель Де Грота предполагает, что все обновляют мнение одинаково и всегда. Упрямцев в ней нет — для них модель "
                + "Фридкина — Джонсена; никто не перестаёт слушать далёких по взглядам — для этого ограниченное доверие.")
            .Build();
    }
}

/// <summary>
/// Модель Де Грота: каждый агент на каждом шаге заменяет своё мнение средним мнений тех, кого слушает.
/// </summary>
/// <remarks>
/// <para>
/// x(t + 1) = W·x(t), где W стохастична по строкам. Если влияние от каждого доходит до каждого и
/// цепь непериодична, все приходят к одному мнению c = πᵀ·x(0). Вектор π — левый собственный вектор W
/// для числа 1, социальная сила агентов: вес начального мнения каждого в итоговом согласии. Он же —
/// стационарное распределение марковской цепи с переходами W.
/// </para>
/// <para>
/// Здесь π находится решением линейной системы (Wᵀ − I)·π = 0 с условием Σπ = 1 готовым
/// LU-разложением, а итерация мнений служит независимой проверкой. Скорость согласия задаёт модуль
/// второго собственного числа из <see cref="Eigen.General"/>: разногласия убывают как |λ₂|ᵗ. Сильную
/// связность проверяет алгоритм Тарьяна из <c>AI.Algorithms</c>.
/// </para>
/// </remarks>
public sealed class DeGrootModel
{
    private readonly Matrix _influence;
    private readonly Lazy<double[]?> _power;
    private readonly Lazy<double> _secondEigenvalue;

    /// <summary>Создаёт модель по матрице влияния</summary>
    /// <param name="influence">Матрица влияния: w_ij — насколько агент i слушает агента j; строки дают 1</param>
    public DeGrootModel(Matrix influence)
    {
        _influence = InfluenceMatrix.Require(influence, nameof(influence));
        Size = _influence.Height;
        (IsStronglyConnected, Period) = Structure(_influence);
        _power = new Lazy<double[]?>(() => IsStronglyConnected ? SolveSocialPower(_influence) : null);
        _secondEigenvalue = new Lazy<double>(SecondModulus);
    }

    /// <summary>Модель по сети контактов: доля своего мнения и поровну между контактами</summary>
    /// <param name="network">Сеть контактов</param>
    /// <param name="selfWeight">Доля своего мнения, от 0 до 1</param>
    public static DeGrootModel FromNetwork(ContactNetwork network, double selfWeight = 0.5) =>
        new(InfluenceMatrix.FromNetwork(network, selfWeight));

    /// <summary>Число агентов</summary>
    public int Size { get; }

    /// <summary>Сильно связна ли сеть влияния</summary>
    public bool IsStronglyConnected { get; }

    /// <summary>Период цепи; 0 — сеть не связна</summary>
    public int Period { get; }

    /// <summary>Придут ли все к одному мнению из любых начальных</summary>
    public bool ReachesConsensus => IsStronglyConnected && Period == 1;

    /// <summary>Модуль второго по величине собственного числа матрицы влияния</summary>
    public double SecondEigenvalueModulus => _secondEigenvalue.Value;

    /// <summary>Вес, с которым агент listener слушает агента speaker</summary>
    /// <param name="listener">Кто слушает</param>
    /// <param name="speaker">Кого слушают</param>
    public double Influence(int listener, int speaker) => _influence[listener, speaker];

    /// <summary>Один шаг: x′ = W·x</summary>
    /// <param name="opinions">Мнения</param>
    public double[] Step(IReadOnlyList<double> opinions)
    {
        InfluenceMatrix.RequireOpinions(opinions, Size, nameof(opinions));

        return InfluenceMatrix.Apply(_influence, opinions);
    }

    /// <summary>Траектория мнений: начальные и ещё steps шагов</summary>
    /// <param name="initial">Начальные мнения</param>
    /// <param name="steps">Число шагов</param>
    public double[][] Run(IReadOnlyList<double> initial, int steps)
    {
        InfluenceMatrix.RequireOpinions(initial, Size, nameof(initial));
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        var trajectory = new double[steps + 1][];
        trajectory[0] = [.. initial];

        for (int t = 1; t <= steps; t++)
            trajectory[t] = InfluenceMatrix.Apply(_influence, trajectory[t - 1]);

        return trajectory;
    }

    /// <summary>Социальная сила агентов: левый собственный вектор W для числа 1, в сумме 1</summary>
    /// <exception cref="InvalidOperationException">Сеть не сильно связна, и вектор не единствен</exception>
    public IReadOnlyList<double> SocialPower() =>
        _power.Value ?? throw new InvalidOperationException(
            "Сеть влияния не сильно связна: у каждой замкнутой группы своё согласие, единой социальной силы нет");

    /// <summary>Итоговое согласие: πᵀ·x(0)</summary>
    /// <param name="initial">Начальные мнения</param>
    /// <exception cref="InvalidOperationException">Сеть не придёт к согласию</exception>
    public double Consensus(IReadOnlyList<double> initial)
    {
        InfluenceMatrix.RequireOpinions(initial, Size, nameof(initial));

        if (!ReachesConsensus)
            throw new InvalidOperationException(IsStronglyConnected
                ? $"Цепь периодична с периодом {Period}: мнения колеблются и к согласию не приходят"
                : "Сеть влияния не сильно связна: общего согласия нет");

        IReadOnlyList<double> power = SocialPower();

        return Enumerable.Range(0, Size).Sum(i => power[i] * initial[i]);
    }

    /// <summary>Строение сети: связность, период, социальная сила и скорость согласия</summary>
    public InfluenceAnalysis Analyze() => new(Size, IsStronglyConnected, Period, _power.Value, SecondEigenvalueModulus);

    /// <summary>
    /// Сильная связность по Тарьяну и период цепи. Период — НОД разностей уровней обхода в ширину по всем
    /// дугам: в <c>AI.Algorithms</c> такого расчёта нет, а обход сам по себе короче вызова
    /// </summary>
    private static (bool StronglyConnected, int Period) Structure(Matrix influence)
    {
        int n = influence.Height;
        var graph = new Graph(n);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i != j && influence[i, j] > 0)
                    graph.AddArc(i, j);
            }
        }

        if (new TarjanSCC(graph).Count != 1)
            return (false, 0);

        var level = Enumerable.Repeat(-1, n).ToArray();
        var queue = new Queue<int>();
        level[0] = 0;
        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();

            for (int v = 0; v < n; v++)
            {
                if (influence[u, v] > 0 && level[v] < 0)
                {
                    level[v] = level[u] + 1;
                    queue.Enqueue(v);
                }
            }
        }

        int period = 0;

        for (int u = 0; u < n; u++)
        {
            for (int v = 0; v < n; v++)
            {
                if (influence[u, v] > 0)
                    period = Gcd(period, Math.Abs(level[u] + 1 - level[v]));
            }
        }

        // Один агент без самовлияния невозможен: строка стохастична, значит w_00 = 1 и период 1
        return (true, Math.Max(period, 1));
    }

    /// <summary>
    /// (Wᵀ − I)·π = 0 и Σπ = 1: строки Wᵀ − I в сумме дают нуль, поэтому одну из них можно заменить
    /// условием нормировки, и система становится невырожденной
    /// </summary>
    private static double[] SolveSocialPower(Matrix influence)
    {
        int n = influence.Height;
        var system = new Matrix(n, n);
        var right = new Vector(n);

        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < n; j++)
                system[i, j] = influence[j, i] - (i == j ? 1 : 0);
        }

        for (int j = 0; j < n; j++)
            system[n - 1, j] = 1;

        right[n - 1] = 1;
        Vector power = LU.Solve(system, right);

        return [.. power.Select(p => Math.Max(p, 0))];
    }

    private double SecondModulus()
    {
        if (Size == 1)
            return 0;

        double[] moduli = [.. Eigen.General(_influence).Select(value => value.Magnitude).OrderDescending()];

        return moduli[1];
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
            (a, b) = (b, a % b);

        return a;
    }
}
