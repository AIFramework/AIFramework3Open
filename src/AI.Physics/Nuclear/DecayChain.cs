using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>
/// Цепочка радиоактивного распада: точное решение уравнений Бейтмана.
/// </summary>
/// <remarks>
/// <para>
/// Каждый член цепочки распадается в следующий с постоянной <c>λᵢ</c> и долей ветвления <c>bᵢ</c>;
/// последний может быть стабильным. Число ядер n-го члена —
/// <c>Nₙ(t) = Σₖ Nₖ(0)·Π(bⱼλⱼt)·e[−λₖt, …, −λₙt]</c>, где <c>e[…]</c> — разделённая разность
/// экспоненты. В такой записи формула не делит на разности постоянных распада и остаётся точной
/// при совпадающих и близких периодах, в первые мгновения и в жёстких цепочках вроде урановой,
/// где периоды различаются от минут до миллиардов лет.
/// </para>
/// <para>
/// Матричная экспонента системы дала бы тот же ответ, но на жёстких цепочках она теряет точность
/// в медленных компонентах; готовая в репозитории к тому же внутренняя деталь систем управления.
/// </para>
/// </remarks>
public sealed class DecayChain
{
    /// <summary>
    /// Во сколько раз родительский нуклид должен жить дольше дочернего, чтобы равновесие
    /// считалось вековым
    /// </summary>
    public const double SecularRatio = 1_000;

    private readonly ChainMember[] _members;
    private readonly double[] _lambda;

    /// <summary>Создаёт цепочку</summary>
    /// <param name="members">Члены цепочки от родительского к последнему</param>
    public DecayChain(IEnumerable<ChainMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        _members = members.ToArray();

        if (_members.Length == 0)
            throw new ArgumentException("Цепочка должна содержать хотя бы один нуклид", nameof(members));

        _lambda = new double[_members.Length];

        for (int i = 0; i < _members.Length; i++)
        {
            ChainMember member = _members[i] ?? throw new ArgumentException("Член цепочки не задан", nameof(members));

            if (member.HalfLife is { } halfLife)
                _lambda[i] = Math.Log(2) / RadioactiveDecay.Seconds(halfLife, nameof(members));
            else if (i < _members.Length - 1)
                throw new ArgumentException(
                    $"Стабильный нуклид «{member.Name}» может стоять только в конце цепочки", nameof(members));
        }
    }

    /// <summary>Создаёт цепочку</summary>
    /// <param name="members">Члены цепочки от родительского к последнему</param>
    public DecayChain(params ChainMember[] members)
        : this((IEnumerable<ChainMember>)members)
    {
    }

    /// <summary>Члены цепочки</summary>
    public IReadOnlyList<ChainMember> Members => _members;

    /// <summary>Число членов</summary>
    public int Count => _members.Length;

    /// <summary>Постоянная распада члена цепочки; ноль у стабильного</summary>
    /// <param name="member">Номер члена</param>
    public Quantity DecayConstant(int member) => new(_lambda[RequireMember(member)], Dimension.Frequency);

    /// <summary>Число ядер каждого члена цепочки в момент <paramref name="time"/></summary>
    /// <param name="time">Время от начала</param>
    /// <param name="initialAtoms">Начальное число ядер каждого члена</param>
    public double[] Amounts(Quantity time, IReadOnlyList<double> initialAtoms)
    {
        double t = Seconds(time);
        double[] initial = Initial(initialAtoms);
        var result = new double[Count];

        for (int n = 0; n < Count; n++)
            result[n] = AmountOf(n, t, initial);

        return result;
    }

    /// <summary>Число ядер каждого члена, если в начале был только родительский нуклид</summary>
    /// <param name="time">Время от начала</param>
    /// <param name="parentAtoms">Начальное число ядер родительского нуклида</param>
    public double[] Amounts(Quantity time, double parentAtoms) => Amounts(time, ParentOnly(parentAtoms));

    /// <summary>Активность каждого члена цепочки</summary>
    /// <param name="time">Время от начала</param>
    /// <param name="initialAtoms">Начальное число ядер каждого члена</param>
    public Quantity[] Activities(Quantity time, IReadOnlyList<double> initialAtoms)
    {
        double[] amounts = Amounts(time, initialAtoms);
        var result = new Quantity[Count];

        for (int i = 0; i < Count; i++)
            result[i] = new Quantity(_lambda[i] * amounts[i], Dimension.Frequency);

        return result;
    }

    /// <summary>Состояние цепочки в момент времени — с разбором равновесий</summary>
    /// <param name="time">Время от начала</param>
    /// <param name="initialAtoms">Начальное число ядер каждого члена</param>
    public DecayChainState State(Quantity time, IReadOnlyList<double> initialAtoms)
    {
        double t = Seconds(time);
        double[] amounts = Amounts(time, initialAtoms);
        var activities = new Quantity[Count];

        for (int i = 0; i < Count; i++)
            activities[i] = new Quantity(_lambda[i] * amounts[i], Dimension.Frequency);

        var equilibria = new DecayEquilibrium[Count - 1];

        for (int i = 0; i < Count - 1; i++)
            equilibria[i] = Equilibrium(i);

        return new DecayChainState(new Quantity(t, Dimension.TimeDim), _members, amounts, activities, equilibria);
    }

    /// <summary>Состояние цепочки, если в начале был только родительский нуклид</summary>
    /// <param name="time">Время от начала</param>
    /// <param name="parentAtoms">Начальное число ядер родительского нуклида</param>
    public DecayChainState State(Quantity time, double parentAtoms) => State(time, ParentOnly(parentAtoms));

    /// <summary>Вид равновесия между членом цепочки и следующим за ним</summary>
    /// <param name="parent">Номер родительского члена</param>
    public DecayEquilibrium Equilibrium(int parent)
    {
        RequireMember(parent);

        if (parent == Count - 1)
            throw new ArgumentOutOfRangeException(nameof(parent), "У последнего члена цепочки нет дочернего");

        double parentLambda = _lambda[parent];
        double daughterLambda = _lambda[parent + 1];

        if (daughterLambda == 0 || daughterLambda <= parentLambda)
            return DecayEquilibrium.None;

        return daughterLambda / parentLambda >= SecularRatio ? DecayEquilibrium.Secular : DecayEquilibrium.Transient;
    }

    /// <summary>
    /// Момент наибольшей активности члена цепочки; ноль, если активность наибольшая в самом начале
    /// </summary>
    /// <remarks>
    /// Глобальный максимум ищется на логарифмической сетке от десятитысячной доли самого короткого
    /// периода до тысячи самых длинных и уточняется золотым сечением. Для пары «родительский —
    /// дочерний» ответ обязан совпасть с формулой <c>ln(λ₂/λ₁)/(λ₂ − λ₁)</c>.
    /// </remarks>
    /// <param name="member">Номер члена цепочки</param>
    /// <param name="initialAtoms">Начальное число ядер каждого члена</param>
    public Quantity PeakActivityTime(int member, IReadOnlyList<double> initialAtoms)
    {
        RequireMember(member);

        if (_lambda[member] == 0)
            throw new InvalidOperationException($"У стабильного нуклида «{_members[member].Name}» нет активности");

        double[] initial = Initial(initialAtoms);
        double shortest = double.PositiveInfinity;
        double longest = 0;

        for (int i = 0; i <= member; i++)
        {
            if (_lambda[i] == 0)
                continue;

            double halfLife = Math.Log(2) / _lambda[i];
            shortest = Math.Min(shortest, halfLife);
            longest = Math.Max(longest, halfLife);
        }

        double Activity(double t) => _lambda[member] * AmountOf(member, t, initial);

        const int Points = 800;
        double low = 1e-4 * shortest;
        double step = Math.Pow(1e3 * longest / low, 1.0 / (Points - 1));
        var grid = new double[Points];
        double best = Activity(0);
        int bestIndex = -1;

        for (int i = 0; i < Points; i++)
        {
            grid[i] = low * Math.Pow(step, i);
            double value = Activity(grid[i]);

            if (value > best)
            {
                best = value;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return Quantity.Zero(Dimension.TimeDim);

        double left = bestIndex == 0 ? 0 : grid[bestIndex - 1];
        double right = bestIndex == Points - 1 ? grid[bestIndex] : grid[bestIndex + 1];

        const double Golden = 0.6180339887498949;
        double x1 = right - (Golden * (right - left));
        double x2 = left + (Golden * (right - left));
        double f1 = Activity(x1);
        double f2 = Activity(x2);

        for (int iteration = 0; iteration < 200 && right - left > 1e-12 * right; iteration++)
        {
            if (f1 < f2)
            {
                left = x1;
                x1 = x2;
                f1 = f2;
                x2 = left + (Golden * (right - left));
                f2 = Activity(x2);
            }
            else
            {
                right = x2;
                x2 = x1;
                f2 = f1;
                x1 = right - (Golden * (right - left));
                f1 = Activity(x1);
            }
        }

        return new Quantity(0.5 * (left + right), Dimension.TimeDim);
    }

    /// <summary>Момент наибольшей активности, если в начале был только родительский нуклид</summary>
    /// <param name="member">Номер члена цепочки</param>
    /// <param name="parentAtoms">Начальное число ядер родительского нуклида</param>
    public Quantity PeakActivityTime(int member, double parentAtoms) => PeakActivityTime(member, ParentOnly(parentAtoms));

    private double AmountOf(int n, double t, double[] initial)
    {
        double total = 0;
        var nodes = new double[n + 1];

        for (int k = 0; k <= n; k++)
        {
            double start = initial[k];

            if (start == 0)
                continue;

            if (k == n)
            {
                total += start * Math.Exp(-_lambda[n] * t);
                continue;
            }

            // Множители считаются в логарифмах: произведение bλt по длинной цепочке и сама
            // разделённая разность по отдельности могут выйти за пределы double
            double logFactor = Math.Log(start);
            bool vanishes = false;

            for (int j = k; j < n; j++)
            {
                double factor = _members[j].BranchingToNext * _lambda[j] * t;

                if (factor == 0)
                {
                    vanishes = true;
                    break;
                }

                logFactor += Math.Log(factor);
            }

            if (vanishes)
                continue;

            int count = n - k + 1;

            for (int i = 0; i < count; i++)
                nodes[i] = -_lambda[k + i] * t;

            double value = ExponentialDividedDifference.Compute(nodes.AsSpan(0, count), out double shift);

            if (value > 0)
                total += Math.Exp(logFactor + shift + Math.Log(value));
        }

        return total;
    }

    private double[] Initial(IReadOnlyList<double> initialAtoms)
    {
        ArgumentNullException.ThrowIfNull(initialAtoms);

        if (initialAtoms.Count != Count)
            throw new ArgumentException(
                $"Начальных количеств {initialAtoms.Count}, а членов цепочки {Count}", nameof(initialAtoms));

        var initial = new double[Count];

        for (int i = 0; i < Count; i++)
        {
            if (!(initialAtoms[i] >= 0) || double.IsInfinity(initialAtoms[i]))
                throw new ArgumentOutOfRangeException(nameof(initialAtoms), "Число ядер — конечное неотрицательное число");

            initial[i] = initialAtoms[i];
        }

        return initial;
    }

    private double[] ParentOnly(double parentAtoms)
    {
        var initial = new double[Count];
        initial[0] = parentAtoms;

        return initial;
    }

    private int RequireMember(int member)
    {
        if (member < 0 || member >= Count)
            throw new ArgumentOutOfRangeException(nameof(member), $"Члена {member} в цепочке из {Count} нет");

        return member;
    }

    private static double Seconds(Quantity time)
    {
        double t = time.RequireSi(Dimension.TimeDim, nameof(time));

        if (!(t >= 0) || double.IsInfinity(t))
            throw new ArgumentOutOfRangeException(nameof(time), "Время — конечное неотрицательное число");

        return t;
    }
}
