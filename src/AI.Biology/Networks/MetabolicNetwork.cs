using System.Globalization;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Solvers.Optimization;

namespace AI.Biology.Networks;

/// <summary>Реакция метаболической сети</summary>
/// <param name="Id">Имя</param>
/// <param name="Stoichiometry">Метаболит → коэффициент: отрицательный у расходуемых, положительный у образуемых</param>
/// <param name="LowerBound">Нижняя граница потока; отрицательная у обратимой реакции</param>
/// <param name="UpperBound">Верхняя граница потока</param>
public sealed record MetabolicReaction(
    string Id, IReadOnlyDictionary<string, double> Stoichiometry, double LowerBound, double UpperBound)
{
    /// <summary>Обратима ли реакция</summary>
    public bool IsReversible => LowerBound < 0;

    /// <summary>Запись реакции уравнением</summary>
    public override string ToString()
    {
        static string Side(IEnumerable<KeyValuePair<string, double>> terms) => string.Join(" + ", terms.Select(t =>
            Math.Abs(Math.Abs(t.Value) - 1) < 1e-12
                ? t.Key
                : Math.Abs(t.Value).ToString("G4", CultureInfo.InvariantCulture) + " " + t.Key));

        return $"{Id}: {Side(Stoichiometry.Where(s => s.Value < 0))} {(IsReversible ? "<=>" : "->")} "
            + Side(Stoichiometry.Where(s => s.Value > 0));
    }
}

/// <summary>Результат балансового анализа потоков</summary>
public sealed class FluxBalanceResult : IInterpretable
{
    private readonly IReadOnlyList<MetabolicReaction> _reactions;

    internal FluxBalanceResult(
        string objective, ObjectiveSense sense, SolverStatus status, double objectiveValue,
        IReadOnlyDictionary<string, double> fluxes, IReadOnlyList<MetabolicReaction> reactions,
        IReadOnlyCollection<string> knockouts)
    {
        Objective = objective;
        Sense = sense;
        Status = status;
        ObjectiveValue = objectiveValue;
        Fluxes = fluxes;
        Knockouts = knockouts;
        _reactions = reactions;
    }

    /// <summary>Реакция-цель</summary>
    public string Objective { get; }

    /// <summary>Направление оптимизации</summary>
    public ObjectiveSense Sense { get; }

    /// <summary>Исход решения</summary>
    public SolverStatus Status { get; }

    /// <summary>Найден ли оптимум</summary>
    public bool IsOptimal => Status == SolverStatus.Optimal;

    /// <summary>Поток через реакцию-цель</summary>
    public double ObjectiveValue { get; }

    /// <summary>Потоки всех реакций; пусто, если оптимум не найден</summary>
    public IReadOnlyDictionary<string, double> Fluxes { get; }

    /// <summary>Выключенные реакции</summary>
    public IReadOnlyCollection<string> Knockouts { get; }

    /// <summary>Поток реакции</summary>
    /// <param name="reaction">Имя реакции</param>
    public double this[string reaction] => Fluxes[reaction];

    /// <summary>Реакции с ненулевым потоком, упёршиеся в свою границу, — то, что ограничивает цель</summary>
    public IReadOnlyList<string> Limiting => !IsOptimal
        ? []
        : _reactions
            .Where(r => !Knockouts.Contains(r.Id))
            .Where(r =>
            {
                double flux = Fluxes[r.Id];

                return Math.Abs(flux) > 1e-9
                    && ((!double.IsInfinity(r.UpperBound) && Math.Abs(flux - r.UpperBound) <= 1e-7 * Math.Max(1, Math.Abs(r.UpperBound)))
                        || (!double.IsInfinity(r.LowerBound) && Math.Abs(flux - r.LowerBound) <= 1e-7 * Math.Max(1, Math.Abs(r.LowerBound))));
            })
            .Select(r => r.Id)
            .ToList();

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int active = IsOptimal ? Fluxes.Count(f => Math.Abs(f.Value) > 1e-9) : 0;
        IReadOnlyList<string> limiting = Limiting;

        return new InterpretationBuilder("Балансовый анализ потоков")
            .Summary(Status switch
            {
                SolverStatus.Optimal =>
                    $"{(Sense == ObjectiveSense.Maximize ? "Наибольший" : "Наименьший")} поток через «{Objective}» — "
                    + $"{Fmt.Num(ObjectiveValue, 4)}; работают {active} реакций из {_reactions.Count}."
                    + (Knockouts.Count > 0 ? $" Выключено реакций: {Knockouts.Count}." : string.Empty),
                SolverStatus.Infeasible =>
                    "Стационарного распределения потоков нет: границы реакций несовместны с балансом метаболитов.",
                SolverStatus.Unbounded =>
                    $"Поток через «{Objective}» не ограничен: у какого-то пути нет верхней границы — обычно забыт "
                    + "предел поглощения субстрата.",
                _ => "Решатель исчерпал предел итераций."
            })
            .Metric("Цель", Objective, null, Sense == ObjectiveSense.Maximize ? "максимизируется" : "минимизируется")
            .Metric("Значение цели", IsOptimal ? Fmt.Num(ObjectiveValue, 6) : "—", null, null,
                IsOptimal ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Активных реакций", active, null, "с ненулевым потоком", MetricQuality.Unknown, 0)
            .Metric("Метаболитов в балансе", _reactions.SelectMany(r => r.Stoichiometry.Keys).Distinct().Count(), null,
                "у каждого образование равно расходу", MetricQuality.Unknown, 0)
            .FindingIf(limiting.Count > 0,
                $"Цель ограничивают реакции на пределе: {string.Join(", ", limiting.Take(8))}"
                + (limiting.Count > 8 ? " и другие" : string.Empty)
                + ". Расширение именно их границ может увеличить цель.")
            .Warning("Оптимум по цели единствен, а распределение потоков — обычно нет: другие пути могут дать "
                + "ту же цель. Какие потоки действительно определены, показывает анализ вариабельности "
                + "(FluxVariability).")
            .Warning("Метод предполагает стационарность и оптимальность клетки по выбранной цели. "
                + "Кинетики, регуляции и концентраций в нём нет — только стехиометрия и границы.")
            .Build();
    }
}

/// <summary>
/// Метаболическая сеть и балансовый анализ потоков (FBA).
/// </summary>
/// <remarks>
/// <para>
/// В стационаре каждый метаболит образуется так же быстро, как расходуется: <c>S·v = 0</c>, где
/// S — стехиометрическая матрица, v — потоки реакций. Вместе с границами потоков это
/// многогранник допустимых состояний, и на нём ищется распределение, максимизирующее цель —
/// обычно реакцию биомассы. Задача линейная и решается симплексом из
/// <c>AI.Solvers.Optimization</c>: своего решателя здесь нет.
/// </para>
/// <para>
/// Обмен со средой задаётся реакциями с одним метаболитом: <c>-> glc</c> — поглощение глюкозы,
/// <c>co2 -></c> — выделение углекислоты. Их границы и есть условия среды.
/// </para>
/// </remarks>
public sealed class MetabolicNetwork
{
    private readonly List<MetabolicReaction> _reactions = [];
    private readonly Dictionary<string, int> _reactionIndex = new(StringComparer.Ordinal);
    private readonly List<string> _metabolites = [];
    private readonly Dictionary<string, int> _metaboliteIndex = new(StringComparer.Ordinal);

    /// <summary>Реакции в порядке добавления</summary>
    public IReadOnlyList<MetabolicReaction> Reactions => _reactions;

    /// <summary>Метаболиты в порядке появления</summary>
    public IReadOnlyList<string> Metabolites => _metabolites;

    /// <summary>Добавляет реакцию</summary>
    /// <param name="id">Имя</param>
    /// <param name="stoichiometry">Метаболит → коэффициент</param>
    /// <param name="lowerBound">Нижняя граница потока</param>
    /// <param name="upperBound">Верхняя граница потока</param>
    public MetabolicReaction AddReaction(
        string id, IReadOnlyDictionary<string, double> stoichiometry, double lowerBound = 0, double upperBound = 1000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(stoichiometry);

        if (_reactionIndex.ContainsKey(id))
            throw new ArgumentException($"Реакция «{id}» уже есть", nameof(id));

        if (double.IsNaN(lowerBound) || double.IsNaN(upperBound) || lowerBound > upperBound)
            throw new ArgumentException($"У реакции «{id}» нижняя граница больше верхней", nameof(lowerBound));

        var copy = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach ((string metabolite, double coefficient) in stoichiometry)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metabolite);

            if (coefficient == 0 || double.IsNaN(coefficient) || double.IsInfinity(coefficient))
                throw new ArgumentException($"Коэффициент «{metabolite}» в реакции «{id}» должен быть конечным и ненулевым", nameof(stoichiometry));

            copy[metabolite] = coefficient;
        }

        if (copy.Count == 0)
            throw new ArgumentException($"Реакция «{id}» не затрагивает ни одного метаболита", nameof(stoichiometry));

        foreach (string metabolite in copy.Keys)
        {
            if (_metaboliteIndex.TryAdd(metabolite, _metabolites.Count))
                _metabolites.Add(metabolite);
        }

        var reaction = new MetabolicReaction(id, copy, lowerBound, upperBound);
        _reactionIndex[id] = _reactions.Count;
        _reactions.Add(reaction);

        return reaction;
    }

    /// <summary>Добавляет реакцию уравнением</summary>
    /// <param name="id">Имя</param>
    /// <param name="equation">Уравнение вроде <c>2 A + B -> C</c>; пустая сторона — обмен со средой</param>
    /// <param name="lowerBound">Нижняя граница потока</param>
    /// <param name="upperBound">Верхняя граница потока</param>
    public MetabolicReaction AddReaction(string id, string equation, double lowerBound = 0, double upperBound = 1000)
    {
        ArgumentNullException.ThrowIfNull(equation);

        string[] sides = equation.Split("->");

        if (sides.Length != 2)
            throw new FormatException($"В уравнении «{equation}» должна быть ровно одна стрелка «->»");

        var stoichiometry = new Dictionary<string, double>(StringComparer.Ordinal);

        void Collect(string side, double sign)
        {
            foreach (string raw in side.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                double coefficient = 1;
                string name = raw;

                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    coefficient = value;
                    name = parts[1];
                }
                else if (parts.Length != 1)
                {
                    throw new FormatException($"Слагаемое «{raw}» в уравнении «{equation}» не разобрано");
                }

                stoichiometry[name] = stoichiometry.GetValueOrDefault(name) + (sign * coefficient);
            }
        }

        Collect(sides[0], -1);
        Collect(sides[1], +1);

        foreach (string cancelled in stoichiometry.Where(s => s.Value == 0).Select(s => s.Key).ToList())
            _ = stoichiometry.Remove(cancelled);

        return AddReaction(id, stoichiometry, lowerBound, upperBound);
    }

    /// <summary>Стехиометрическая матрица: метаболиты по строкам, реакции по столбцам</summary>
    public double[,] StoichiometricMatrix()
    {
        var matrix = new double[_metabolites.Count, _reactions.Count];

        for (int r = 0; r < _reactions.Count; r++)
        {
            foreach ((string metabolite, double coefficient) in _reactions[r].Stoichiometry)
                matrix[_metaboliteIndex[metabolite], r] = coefficient;
        }

        return matrix;
    }

    /// <summary>
    /// Балансовый анализ: распределение потоков, оптимальное по реакции-цели
    /// </summary>
    /// <param name="objective">Реакция-цель</param>
    /// <param name="sense">Максимизировать или минимизировать</param>
    /// <param name="knockouts">Выключаемые реакции — их поток приравнивается нулю</param>
    public FluxBalanceResult Optimize(
        string objective, ObjectiveSense sense = ObjectiveSense.Maximize, IEnumerable<string>? knockouts = null)
    {
        int target = ReactionIndex(objective);
        var knocked = new HashSet<string>(knockouts ?? [], StringComparer.Ordinal);

        foreach (string id in knocked)
            _ = ReactionIndex(id);

        (LinearProgram program, Variable[] variables) = Build(sense, knocked);
        program.SetObjective(variables[target], 1);

        LpSolution solution = LpSolver.Solve(program);

        var fluxes = new Dictionary<string, double>(StringComparer.Ordinal);

        if (solution.IsOptimal)
        {
            for (int r = 0; r < _reactions.Count; r++)
                fluxes[_reactions[r].Id] = Clean(solution[variables[r]]);
        }

        return new FluxBalanceResult(
            objective, sense, solution.Status,
            solution.IsOptimal ? Clean(solution.Objective) : double.NaN,
            fluxes, _reactions, knocked);
    }

    /// <summary>
    /// Анализ вариабельности потоков: пределы каждого потока при цели не хуже заданной доли оптимума
    /// </summary>
    /// <param name="objective">Реакция-цель, максимизируемая</param>
    /// <param name="optimumFraction">Доля оптимума, которую нужно сохранить: 1 — только оптимальные решения</param>
    /// <remarks>
    /// Показывает, какие потоки оптимум действительно определяет, а какие могут идти обходными путями.
    /// Стоит 2·R + 1 задач линейного программирования по числу реакций R.
    /// </remarks>
    public IReadOnlyDictionary<string, (double Minimum, double Maximum)> FluxVariability(
        string objective, double optimumFraction = 1.0)
    {
        if (optimumFraction is < 0 or > 1 || double.IsNaN(optimumFraction))
            throw new ArgumentOutOfRangeException(nameof(optimumFraction), "Доля оптимума лежит на отрезке [0, 1]");

        int target = ReactionIndex(objective);
        FluxBalanceResult best = Optimize(objective);

        if (!best.IsOptimal)
            throw new InvalidOperationException($"Оптимум по «{objective}» не найден: {best.Status}");

        double optimum = best.ObjectiveValue;
        double floor = optimum - ((1 - optimumFraction) * Math.Abs(optimum)) - (1e-9 * Math.Max(1, Math.Abs(optimum)));
        var result = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
        var empty = new HashSet<string>(StringComparer.Ordinal);

        for (int r = 0; r < _reactions.Count; r++)
        {
            double Extreme(ObjectiveSense sense)
            {
                (LinearProgram program, Variable[] variables) = Build(sense, empty);
                var unit = new double[variables.Length];
                unit[target] = 1;
                _ = program.AddConstraint(new Vector(unit), ConstraintSign.GreaterOrEqual, floor, "цель");
                program.SetObjective(variables[r], 1);

                LpSolution solution = LpSolver.Solve(program);

                return solution.Status switch
                {
                    SolverStatus.Optimal => Clean(solution.Objective),
                    SolverStatus.Unbounded => sense == ObjectiveSense.Maximize ? double.PositiveInfinity : double.NegativeInfinity,
                    _ => double.NaN
                };
            }

            result[_reactions[r].Id] = (Extreme(ObjectiveSense.Minimize), Extreme(ObjectiveSense.Maximize));
        }

        return result;
    }

    /// <summary>
    /// Незаменимые реакции: после выключения любой из них цель падает ниже заданной доли оптимума
    /// </summary>
    /// <param name="objective">Реакция-цель, максимизируемая</param>
    /// <param name="viabilityFraction">Доля оптимума, ниже которой организм считается нежизнеспособным</param>
    public IReadOnlyList<string> EssentialReactions(string objective, double viabilityFraction = 0.01)
    {
        FluxBalanceResult wild = Optimize(objective);

        if (!wild.IsOptimal)
            throw new InvalidOperationException($"Оптимум по «{objective}» не найден: {wild.Status}");

        double threshold = viabilityFraction * wild.ObjectiveValue;
        var essential = new List<string>();

        foreach (MetabolicReaction reaction in _reactions)
        {
            if (reaction.Id == objective)
                continue;

            FluxBalanceResult knocked = Optimize(objective, knockouts: [reaction.Id]);

            if (!knocked.IsOptimal || knocked.ObjectiveValue < threshold)
                essential.Add(reaction.Id);
        }

        return essential;
    }

    private (LinearProgram Program, Variable[] Variables) Build(ObjectiveSense sense, ISet<string> knocked)
    {
        var program = new LinearProgram(sense, "Балансовый анализ потоков");
        var variables = new Variable[_reactions.Count];

        for (int r = 0; r < _reactions.Count; r++)
        {
            MetabolicReaction reaction = _reactions[r];
            bool off = knocked.Contains(reaction.Id);

            variables[r] = program.AddVariable(reaction.Id, off ? 0 : reaction.LowerBound, off ? 0 : reaction.UpperBound);
        }

        double[,] matrix = StoichiometricMatrix();

        for (int m = 0; m < _metabolites.Count; m++)
        {
            var row = new double[_reactions.Count];

            for (int r = 0; r < _reactions.Count; r++)
                row[r] = matrix[m, r];

            _ = program.AddConstraint(new Vector(row), ConstraintSign.Equal, 0, _metabolites[m]);
        }

        return (program, variables);
    }

    private int ReactionIndex(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _reactionIndex.TryGetValue(id, out int index) ? index : throw new KeyNotFoundException($"Реакции «{id}» в сети нет");
    }

    private static double Clean(double value) => Math.Abs(value) < 1e-9 ? 0 : value;
}
