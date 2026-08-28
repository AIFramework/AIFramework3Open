using AI.DataStructs.Algebraic;
using AI.MathUtils.ODE;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Kinetics;

/// <summary>
/// Элементарная стадия схемы: расход реагентов и накопление продуктов
/// со скоростью r = k · П[Ci]^ni
/// </summary>
public sealed class ReactionStep
{
    /// <summary>Название стадии</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Реагенты: имя вещества -> стехиометрический коэффициент
    /// </summary>
    public IReadOnlyDictionary<string, double> Reactants { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Продукты: имя вещества -> стехиометрический коэффициент
    /// </summary>
    public IReadOnlyDictionary<string, double> Products { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Порядки по веществам; если не заданы, берутся стехиометрические коэффициенты реагентов
    /// </summary>
    public IReadOnlyDictionary<string, double> Orders { get; init; }

    /// <summary>Индекс константы скорости в векторе параметров</summary>
    public int RateConstantIndex { get; init; }

    /// <summary>Порядок по веществу</summary>
    public double OrderOf(string species)
    {
        if (Orders != null && Orders.TryGetValue(species, out double order))
            return order;

        return Reactants.TryGetValue(species, out double stoichiometry) ? stoichiometry : 0;
    }

    /// <summary>Запись стадии в виде уравнения</summary>
    public override string ToString()
    {
        static string Side(IReadOnlyDictionary<string, double> species) => species.Count == 0
            ? "0"
            : string.Join(" + ", species.Select(s =>
                Math.Abs(s.Value - 1) < 1e-12
                    ? s.Key
                    : s.Value.ToString("G3", CultureInfo.InvariantCulture) + " " + s.Key));

        return $"{Side(Reactants)} -> {Side(Products)}";
    }
}

/// <summary>
/// Кинетическая схема: набор веществ и элементарных стадий.
/// Интегрирует систему dc/dt и служит моделью для подгонки констант.
/// </summary>
/// <remarks>
/// Скорости считаются по закону действующих масс, система решается методом
/// Рунге-Кутты 4-го порядка из <c>AI.ClassicMath</c>. Для жёстких схем
/// (константы различаются на порядки) увеличивайте <see cref="StepsPerInterval"/>.
/// </remarks>
public sealed class KineticScheme
{
    private readonly List<string> _species;
    private readonly List<ReactionStep> _steps;
    private readonly Dictionary<string, int> _index;

    /// <summary>Вещества схемы в порядке их следования в векторе состояния</summary>
    public IReadOnlyList<string> Species => _species;

    /// <summary>Стадии схемы</summary>
    public IReadOnlyList<ReactionStep> Steps => _steps;

    /// <summary>Число констант скорости</summary>
    public int RateConstantCount => _steps.Count == 0 ? 0 : _steps.Max(s => s.RateConstantIndex) + 1;

    /// <summary>Число шагов интегрирования между точками вывода</summary>
    public int StepsPerInterval { get; set; } = 40;

    /// <summary>Создаёт схему из списка веществ и стадий</summary>
    /// <param name="species">Вещества</param>
    /// <param name="steps">Стадии</param>
    public KineticScheme(IEnumerable<string> species, IEnumerable<ReactionStep> steps)
    {
        _species = species?.ToList() ?? throw new ArgumentNullException(nameof(species));
        _steps = steps?.ToList() ?? throw new ArgumentNullException(nameof(steps));

        if (_species.Count == 0)
            throw new ArgumentException("Scheme must contain at least one species", nameof(species));

        _index = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < _species.Count; i++)
            _index[_species[i]] = i;

        foreach (var step in _steps)
        {
            foreach (string name in step.Reactants.Keys.Concat(step.Products.Keys))
            {
                if (!_index.ContainsKey(name))
                    throw new ArgumentException($"Species '{name}' is used in a step but not declared in the scheme");
            }
        }
    }

    /// <summary>Индекс вещества в векторе состояния</summary>
    public int IndexOf(string species) => _index.TryGetValue(species, out int index) ? index : -1;

    /// <summary>
    /// Правая часть системы: скорости изменения концентраций
    /// </summary>
    /// <param name="concentrations">Текущие концентрации</param>
    /// <param name="rateConstants">Константы скорости</param>
    public Vector Derivatives(Vector concentrations, IReadOnlyList<double> rateConstants)
    {
        var derivatives = new Vector(_species.Count);

        foreach (var step in _steps)
        {
            double rate = rateConstants[step.RateConstantIndex];

            foreach (var reactant in step.Reactants)
            {
                double order = step.OrderOf(reactant.Key);

                if (order == 0)
                    continue;

                double concentration = Math.Max(0, concentrations[_index[reactant.Key]]);
                rate *= Math.Abs(order - 1) < 1e-12 ? concentration : Math.Pow(concentration, order);
            }

            if (rate == 0)
                continue;

            foreach (var reactant in step.Reactants)
                derivatives[_index[reactant.Key]] -= rate * reactant.Value;

            foreach (var product in step.Products)
                derivatives[_index[product.Key]] += rate * product.Value;
        }

        return derivatives;
    }

    /// <summary>
    /// Интегрирует схему и возвращает концентрации в заданные моменты времени
    /// </summary>
    /// <param name="initial">Начальные концентрации в порядке <see cref="Species"/></param>
    /// <param name="rateConstants">Константы скорости</param>
    /// <param name="times">Моменты времени (возрастающие)</param>
    public Vector[] Simulate(IReadOnlyList<double> initial, IReadOnlyList<double> rateConstants, IReadOnlyList<double> times)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(rateConstants);
        ArgumentNullException.ThrowIfNull(times);

        if (initial.Count != _species.Count)
            throw new ArgumentException($"Expected {_species.Count} initial concentrations, got {initial.Count}", nameof(initial));

        if (rateConstants.Count < RateConstantCount)
            throw new ArgumentException($"Scheme needs {RateConstantCount} rate constant(s)", nameof(rateConstants));

        double start = times.Count > 0 ? Math.Min(0, times[0]) : 0;

        return RungeKutta.SolveSystem(
            (_, y) => Derivatives(y, rateConstants),
            start,
            new Vector(initial.ToArray()),
            times,
            StepsPerInterval);
    }

    /// <summary>
    /// Концентрация одного вещества во времени
    /// </summary>
    /// <param name="species">Вещество</param>
    /// <param name="initial">Начальные концентрации</param>
    /// <param name="rateConstants">Константы скорости</param>
    /// <param name="times">Моменты времени</param>
    public double[] SimulateSpecies(string species, IReadOnlyList<double> initial,
        IReadOnlyList<double> rateConstants, IReadOnlyList<double> times)
    {
        int index = IndexOf(species);

        if (index < 0)
            throw new ArgumentException($"Species '{species}' is not part of the scheme", nameof(species));

        return Simulate(initial, rateConstants, times).Select(state => state[index]).ToArray();
    }

    #region Готовые схемы

    /// <summary>
    /// Необратимая реакция A -> B заданного порядка
    /// </summary>
    /// <param name="order">Порядок по A</param>
    public static KineticScheme Simple(double order = 1)
        => new(new[] { "A", "B" }, new[]
        {
            new ReactionStep
            {
                Name = "A -> B",
                Reactants = new Dictionary<string, double> { ["A"] = 1 },
                Products = new Dictionary<string, double> { ["B"] = 1 },
                Orders = new Dictionary<string, double> { ["A"] = order },
                RateConstantIndex = 0
            }
        });

    /// <summary>Последовательные реакции A -> B -> C</summary>
    public static KineticScheme Consecutive()
        => new(new[] { "A", "B", "C" }, new[]
        {
            new ReactionStep
            {
                Name = "A -> B",
                Reactants = new Dictionary<string, double> { ["A"] = 1 },
                Products = new Dictionary<string, double> { ["B"] = 1 },
                RateConstantIndex = 0
            },
            new ReactionStep
            {
                Name = "B -> C",
                Reactants = new Dictionary<string, double> { ["B"] = 1 },
                Products = new Dictionary<string, double> { ["C"] = 1 },
                RateConstantIndex = 1
            }
        });

    /// <summary>Обратимая реакция A = B</summary>
    public static KineticScheme Reversible()
        => new(new[] { "A", "B" }, new[]
        {
            new ReactionStep
            {
                Name = "A -> B",
                Reactants = new Dictionary<string, double> { ["A"] = 1 },
                Products = new Dictionary<string, double> { ["B"] = 1 },
                RateConstantIndex = 0
            },
            new ReactionStep
            {
                Name = "B -> A",
                Reactants = new Dictionary<string, double> { ["B"] = 1 },
                Products = new Dictionary<string, double> { ["A"] = 1 },
                RateConstantIndex = 1
            }
        });

    /// <summary>Реакция второго порядка A + B -> C</summary>
    public static KineticScheme Bimolecular()
        => new(new[] { "A", "B", "C" }, new[]
        {
            new ReactionStep
            {
                Name = "A + B -> C",
                Reactants = new Dictionary<string, double> { ["A"] = 1, ["B"] = 1 },
                Products = new Dictionary<string, double> { ["C"] = 1 },
                RateConstantIndex = 0
            }
        });

    /// <summary>Параллельные реакции A -> B и A -> C</summary>
    public static KineticScheme Parallel()
        => new(new[] { "A", "B", "C" }, new[]
        {
            new ReactionStep
            {
                Name = "A -> B",
                Reactants = new Dictionary<string, double> { ["A"] = 1 },
                Products = new Dictionary<string, double> { ["B"] = 1 },
                RateConstantIndex = 0
            },
            new ReactionStep
            {
                Name = "A -> C",
                Reactants = new Dictionary<string, double> { ["A"] = 1 },
                Products = new Dictionary<string, double> { ["C"] = 1 },
                RateConstantIndex = 1
            }
        });

    #endregion

    /// <summary>Схема в текстовом виде</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.AppendLine($"Вещества: {string.Join(", ", _species)}");

        foreach (var step in _steps)
            text.AppendLine($"  k{step.RateConstantIndex + 1}: {step}");

        return text.ToString();
    }
}
