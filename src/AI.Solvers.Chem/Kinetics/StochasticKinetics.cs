namespace AI.Solvers.Chem.Kinetics;

/// <summary>
/// Траектория стохастической кинетики: числа молекул после каждого события
/// </summary>
/// <remarks>
/// Траектория ступенчатая: между событиями числа молекул постоянны. Поэтому средние по времени
/// считаются точно, интегралом ступенчатой функции, а не по выборке в узлах сетки.
/// </remarks>
public sealed class StochasticTrajectory
{
    private readonly List<double> _times;
    private readonly List<int[]> _counts;
    private readonly Dictionary<string, int> _index;

    internal StochasticTrajectory(
        IReadOnlyList<string> species, List<double> times, List<int[]> counts, double finalTime, bool truncated)
    {
        Species = species;
        _times = times;
        _counts = counts;
        FinalTime = finalTime;
        Truncated = truncated;
        _index = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < species.Count; i++)
            _index[species[i]] = i;
    }

    /// <summary>Вещества в порядке следования в векторе состояния</summary>
    public IReadOnlyList<string> Species { get; }

    /// <summary>Моменты событий; первый — нуль, начальное состояние</summary>
    public IReadOnlyList<double> Times => _times;

    /// <summary>Число произошедших реакций</summary>
    public int Events => _times.Count - 1;

    /// <summary>Конец траектории</summary>
    public double FinalTime { get; }

    /// <summary>Счёт прерван по пределу числа событий, и траектория короче заказанной</summary>
    public bool Truncated { get; }

    /// <summary>Числа молекул после события с заданным номером</summary>
    /// <param name="index">Номер события; нуль — начальное состояние</param>
    public int[] CountsAt(int index) => (int[])_counts[index].Clone();

    /// <summary>Числа молекул в момент времени</summary>
    /// <param name="time">Момент от нуля до конца траектории</param>
    public int[] At(double time)
    {
        if (time < 0 || time > FinalTime)
            throw new ArgumentOutOfRangeException(nameof(time), $"Момент должен лежать на отрезке [0; {FinalTime}]");

        int position = _times.BinarySearch(time);

        if (position < 0)
            position = ~position - 1;

        return CountsAt(Math.Max(0, position));
    }

    /// <summary>Число молекул вещества в момент времени</summary>
    /// <param name="species">Вещество</param>
    /// <param name="time">Момент</param>
    public int CountAt(string species, double time) => At(time)[Index(species)];

    /// <summary>Среднее по времени число молекул на отрезке</summary>
    /// <param name="species">Вещество</param>
    /// <param name="from">Начало отрезка — после выхода на стационарный режим</param>
    /// <param name="to">Конец отрезка; по умолчанию конец траектории</param>
    public double TimeAverage(string species, double from = 0, double? to = null)
        => Moment(Index(species), from, to ?? FinalTime, square: false);

    /// <summary>Дисперсия числа молекул по времени на отрезке</summary>
    /// <param name="species">Вещество</param>
    /// <param name="from">Начало отрезка</param>
    /// <param name="to">Конец отрезка; по умолчанию конец траектории</param>
    public double TimeVariance(string species, double from = 0, double? to = null)
    {
        int index = Index(species);
        double end = to ?? FinalTime;
        double mean = Moment(index, from, end, square: false);

        return Moment(index, from, end, square: true) - (mean * mean);
    }

    private double Moment(int species, double from, double to, bool square)
    {
        if (from < 0 || to > FinalTime || !(to > from))
            throw new ArgumentOutOfRangeException(nameof(to), $"Отрезок должен лежать внутри [0; {FinalTime}] и иметь длину");

        double sum = 0;

        for (int k = 0; k < _times.Count; k++)
        {
            double start = Math.Max(_times[k], from);
            double end = Math.Min(k + 1 < _times.Count ? _times[k + 1] : FinalTime, to);

            if (end <= start)
                continue;

            double value = _counts[k][species];
            sum += (square ? value * value : value) * (end - start);
        }

        return sum / (to - from);
    }

    private int Index(string species)
        => _index.TryGetValue(species, out int index)
            ? index
            : throw new ArgumentException($"Вещества «{species}» в схеме нет", nameof(species));
}

/// <summary>
/// Стохастическая кинетика: точное моделирование реакций между отдельными молекулами
/// методом Гиллеспи.
/// </summary>
/// <remarks>
/// <para>
/// Детерминированная кинетика <see cref="KineticScheme.Simulate"/> описывает концентрации и верна,
/// когда молекул много. В клетке их бывает единицы: десяток молекул мРНК, один-два промотора.
/// Тогда случайность отдельных событий определяет поведение системы, и среднее уже не отвечает
/// на главный вопрос — насколько клетки различаются между собой. Прямой метод Гиллеспи (1977)
/// разыгрывает, какая реакция произойдёт следующей и когда, и даёт точную реализацию основного
/// кинетического уравнения, без шага по времени.
/// </para>
/// <para>
/// Константы здесь стохастические, c: вероятность в единицу времени того, что одна заданная
/// комбинация молекул прореагирует. Для реакций нулевого и первого порядка c совпадает с
/// детерминированной константой k. Для A + B → … при концентрациях, выраженных числом молекул
/// на объём Ω, c = k/Ω, для 2A → … c = 2k/Ω. Пропенсивность — c, умноженная на число
/// различных комбинаций реагентов: для 2A это x(x − 1)/2, а не x².
/// </para>
/// <para>
/// Стадии должны быть элементарными: целые стехиометрические коэффициенты и порядки, равные им.
/// Дробный порядок — свойство суммарной скорости многих событий, и разыграть его как одно событие
/// нельзя, поэтому такая схема отвергается. Время счёта пропорционально числу событий: для схем
/// с быстрыми и медленными реакциями нужны приближённые методы (τ-скачки), которых здесь нет.
/// </para>
/// </remarks>
public static class StochasticKinetics
{
    /// <summary>
    /// Разыгрывает одну реализацию схемы прямым методом Гиллеспи
    /// </summary>
    /// <param name="scheme">Схема из элементарных стадий</param>
    /// <param name="initialCounts">Начальные числа молекул в порядке <see cref="KineticScheme.Species"/></param>
    /// <param name="rateConstants">Стохастические константы c по индексам стадий</param>
    /// <param name="finalTime">Конец моделирования</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="maxEvents">Предел числа событий</param>
    public static StochasticTrajectory Simulate(
        KineticScheme scheme,
        IReadOnlyList<int> initialCounts,
        IReadOnlyList<double> rateConstants,
        double finalTime,
        Random random,
        int maxEvents = 10_000_000)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(initialCounts);
        ArgumentNullException.ThrowIfNull(rateConstants);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEvents);

        if (!(finalTime > 0))
            throw new ArgumentOutOfRangeException(nameof(finalTime), "Время моделирования должно быть положительным");

        if (initialCounts.Count != scheme.Species.Count)
            throw new ArgumentException($"Ожидается {scheme.Species.Count} начальных чисел молекул", nameof(initialCounts));

        if (initialCounts.Any(c => c < 0))
            throw new ArgumentException("Число молекул не может быть отрицательным", nameof(initialCounts));

        if (rateConstants.Count < scheme.RateConstantCount)
            throw new ArgumentException($"Схеме нужно {scheme.RateConstantCount} констант", nameof(rateConstants));

        Channel[] channels = scheme.Steps.Select(step => Compile(scheme, step, rateConstants)).ToArray();

        int[] state = initialCounts.ToArray();
        var times = new List<double> { 0 };
        var counts = new List<int[]> { (int[])state.Clone() };
        var propensities = new double[channels.Length];
        double time = 0;
        bool truncated = false;

        while (true)
        {
            double total = 0;

            for (int r = 0; r < channels.Length; r++)
            {
                propensities[r] = channels[r].Propensity(state);
                total += propensities[r];
            }

            // Все реакции невозможны: система пришла в поглощающее состояние
            if (total <= 0)
                break;

            double wait = -Math.Log(1 - random.NextDouble()) / total;

            if (time + wait > finalTime)
                break;

            time += wait;

            double pick = random.NextDouble() * total;
            int chosen = channels.Length - 1;

            for (int r = 0; r < channels.Length; r++)
            {
                pick -= propensities[r];

                if (pick < 0 && propensities[r] > 0)
                {
                    chosen = r;
                    break;
                }
            }

            while (propensities[chosen] <= 0)
                chosen--;

            channels[chosen].Fire(state);
            times.Add(time);
            counts.Add((int[])state.Clone());

            if (times.Count - 1 >= maxEvents)
            {
                truncated = true;
                break;
            }
        }

        return new StochasticTrajectory(scheme.Species, times, counts, truncated ? time : finalTime, truncated);
    }

    private static Channel Compile(KineticScheme scheme, ReactionStep step, IReadOnlyList<double> rateConstants)
    {
        double constant = rateConstants[step.RateConstantIndex];

        if (constant < 0 || double.IsNaN(constant))
            throw new ArgumentException($"Константа стадии «{step}» должна быть неотрицательной", nameof(rateConstants));

        var reactants = new List<(int Species, int Count)>();
        var change = new Dictionary<int, int>();

        foreach (var reactant in step.Reactants)
        {
            int count = WholeCoefficient(reactant.Value, step);

            if (Math.Abs(step.OrderOf(reactant.Key) - reactant.Value) > 1e-12)
                throw new ArgumentException(
                    $"Стадия «{step}» не элементарна: порядок по {reactant.Key} не равен стехиометрии. "
                    + "Стохастически разыгрываются только элементарные стадии");

            int index = scheme.IndexOf(reactant.Key);
            reactants.Add((index, count));
            change[index] = change.GetValueOrDefault(index) - count;
        }

        if (step.Orders != null && step.Orders.Any(o => !step.Reactants.ContainsKey(o.Key) && Math.Abs(o.Value) > 1e-12))
            throw new ArgumentException($"Стадия «{step}» имеет порядок по веществу, которое в ней не расходуется");

        foreach (var product in step.Products)
        {
            int index = scheme.IndexOf(product.Key);
            change[index] = change.GetValueOrDefault(index) + WholeCoefficient(product.Value, step);
        }

        return new Channel(constant, reactants.ToArray(), change.Where(c => c.Value != 0).Select(c => (c.Key, c.Value)).ToArray());
    }

    private static int WholeCoefficient(double value, ReactionStep step)
    {
        double rounded = Math.Round(value);

        return value > 0 && Math.Abs(value - rounded) < 1e-12
            ? (int)rounded
            : throw new ArgumentException($"Стадия «{step}»: стехиометрический коэффициент {value} не целый положительный");
    }

    private sealed class Channel
    {
        private readonly double _constant;
        private readonly (int Species, int Count)[] _reactants;
        private readonly (int Species, int Delta)[] _change;

        public Channel(double constant, (int Species, int Count)[] reactants, (int Species, int Delta)[] change)
        {
            _constant = constant;
            _reactants = reactants;
            _change = change;
        }

        // Число различных наборов молекул, способных прореагировать: произведение сочетаний C(x, ν)
        public double Propensity(int[] state)
        {
            double value = _constant;

            foreach ((int species, int count) in _reactants)
            {
                int available = state[species];

                if (available < count)
                    return 0;

                for (int k = 0; k < count; k++)
                    value *= (double)(available - k) / (k + 1);
            }

            return value;
        }

        public void Fire(int[] state)
        {
            foreach ((int species, int delta) in _change)
                state[species] += delta;
        }
    }
}
