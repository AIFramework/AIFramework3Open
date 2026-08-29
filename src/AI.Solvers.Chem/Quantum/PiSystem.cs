using AI.DataStructs.Algebraic;
using NCDK;
using NCDK.Aromaticities;
using NCDK.Smiles;
using NCDK.Tools.Manipulator;

namespace AI.Solvers.Chem.Quantum;

/// <summary>
/// Параметры гетероатома в методе Хюккеля
/// </summary>
/// <param name="AlphaShift">Поправка кулоновского интеграла h: alpha_X = alpha + h·beta</param>
/// <param name="Resonance">Множитель резонансного интеграла k: beta_CX = k·beta</param>
/// <param name="Electrons">Число электронов, отдаваемых атомом в систему</param>
public readonly record struct HeteroatomParameters(double AlphaShift, double Resonance, int Electrons);

/// <summary>
/// Центр сопряжённой системы
/// </summary>
/// <param name="Element">Символ элемента</param>
/// <param name="Electrons">Число отдаваемых в систему электронов</param>
/// <param name="AlphaShift">Поправка кулоновского интеграла h</param>
/// <param name="Label">Метка центра</param>
public readonly record struct PiCenter(string Element, int Electrons, double AlphaShift, string Label)
{
    /// <summary>Строка описания центра</summary>
    public override string ToString() => string.IsNullOrEmpty(Label) ? Element : Label;
}

/// <summary>
/// Сопряжённая система: граф центров и резонансных связей
/// </summary>
/// <remarks>
/// Метод Хюккеля сводит задачу к одной матрице: на диагонали стоит поправка h
/// кулоновского интеграла, вне диагонали - множитель k резонансного интеграла.
/// Всё выражено в единицах beta при alpha = 0, поэтому собственные значения
/// сразу дают энергии орбиталей E = alpha + x·beta.
/// </remarks>
public sealed class PiSystem
{
    private readonly List<PiCenter> _centers = new();
    private readonly Dictionary<(int, int), double> _bonds = new();

    /// <summary>
    /// Параметры гетероатомов по Стрейтвизеру; таблицу принято подстраивать
    /// под конкретный класс соединений
    /// </summary>
    public static Dictionary<string, HeteroatomParameters> Heteroatoms { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = new HeteroatomParameters(0.0, 1.0, 1),
        ["N1"] = new HeteroatomParameters(0.5, 1.0, 1),
        ["N2"] = new HeteroatomParameters(1.5, 0.8, 2),
        ["O1"] = new HeteroatomParameters(1.0, 1.0, 1),
        ["O2"] = new HeteroatomParameters(2.0, 0.8, 2),
        ["S"] = new HeteroatomParameters(1.0, 0.7, 2),
        ["F"] = new HeteroatomParameters(3.0, 0.7, 2),
        ["Cl"] = new HeteroatomParameters(2.0, 0.4, 2),
        ["Br"] = new HeteroatomParameters(1.5, 0.3, 2)
    };

    /// <summary>Центры системы</summary>
    public IReadOnlyList<PiCenter> Centers => _centers;

    /// <summary>Число центров</summary>
    public int Count => _centers.Count;

    /// <summary>Заряд системы: положительный убирает электроны</summary>
    public int Charge { get; set; }

    /// <summary>Название системы</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Полное число электронов в сопряжённой системе</summary>
    public int Electrons => _centers.Sum(c => c.Electrons) - Charge;

    /// <summary>Добавляет центр и возвращает его номер</summary>
    /// <param name="element">Символ элемента</param>
    /// <param name="electrons">Число отдаваемых электронов</param>
    /// <param name="alphaShift">Поправка кулоновского интеграла</param>
    /// <param name="label">Метка центра</param>
    public int AddCenter(string element = "C", int electrons = 1, double alphaShift = 0, string label = null)
    {
        _centers.Add(new PiCenter(element, electrons, alphaShift, label ?? $"{element}{_centers.Count + 1}"));

        return _centers.Count - 1;
    }

    /// <summary>Добавляет резонансную связь между центрами</summary>
    /// <param name="first">Номер первого центра</param>
    /// <param name="second">Номер второго центра</param>
    /// <param name="resonance">Множитель резонансного интеграла</param>
    public void AddBond(int first, int second, double resonance = 1.0)
    {
        if (first == second)
            throw new ArgumentException("Связь центра с самим собой не имеет смысла");

        if (first < 0 || second < 0 || first >= _centers.Count || second >= _centers.Count)
            throw new ArgumentOutOfRangeException(nameof(first), "Номер центра вне системы");

        _bonds[Key(first, second)] = resonance;
    }

    /// <summary>Есть ли связь между центрами</summary>
    /// <param name="first">Номер первого центра</param>
    /// <param name="second">Номер второго центра</param>
    public bool HasBond(int first, int second) => _bonds.ContainsKey(Key(first, second));

    /// <summary>Связи системы</summary>
    public IEnumerable<(int First, int Second, double Resonance)> Bonds
        => _bonds.Select(b => (b.Key.Item1, b.Key.Item2, b.Value));

    /// <summary>Линейный полиен из n атомов углерода</summary>
    /// <param name="count">Число атомов</param>
    public static PiSystem Chain(int count)
    {
        if (count < 2)
            throw new ArgumentException("В цепи должно быть не менее двух центров", nameof(count));

        var system = new PiSystem { Name = $"полиен C{count}" };

        for (int i = 0; i < count; i++)
            system.AddCenter();

        for (int i = 0; i + 1 < count; i++)
            system.AddBond(i, i + 1);

        return system;
    }

    /// <summary>Циклический полиен (аннулен) из n атомов углерода</summary>
    /// <param name="count">Число атомов</param>
    /// <param name="charge">Заряд цикла</param>
    public static PiSystem Ring(int count, int charge = 0)
    {
        if (count < 3)
            throw new ArgumentException("В цикле должно быть не менее трёх центров", nameof(count));

        PiSystem system = Chain(count);
        system.AddBond(count - 1, 0);
        system.Charge = charge;
        system.Name = $"цикл C{count}" + (charge == 0 ? string.Empty : $" заряд {charge:+0;-0}");

        return system;
    }

    /// <summary>
    /// Выделяет сопряжённую систему из структуры, заданной SMILES
    /// </summary>
    /// <param name="smiles">Строка SMILES</param>
    /// <param name="charge">Заряд системы</param>
    public static PiSystem FromSmiles(string smiles, int charge = 0)
    {
        if (string.IsNullOrWhiteSpace(smiles))
            throw new ArgumentException("Пустая строка SMILES", nameof(smiles));

        var parser = new SmilesParser(NCDK.Default.ChemObjectBuilder.Instance);
        IAtomContainer molecule = parser.ParseSmiles(smiles);

        AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(molecule);
        Aromaticity.CDKLegacy.Apply(molecule);

        var selected = new List<IAtom>();

        foreach (IAtom atom in molecule.Atoms)
        {
            if (atom.Symbol == "H")
                continue;

            if (atom.IsAromatic || HasMultipleBond(molecule, atom) || IsConjugatedSubstituent(molecule, atom))
                selected.Add(atom);
        }

        var system = new PiSystem { Name = smiles, Charge = charge };
        var indices = new Dictionary<IAtom, int>();

        foreach (IAtom atom in selected)
        {
            HeteroatomParameters parameters = Describe(molecule, atom);
            indices[atom] = system.AddCenter(atom.Symbol, parameters.Electrons, parameters.AlphaShift,
                $"{atom.Symbol}{molecule.Atoms.IndexOf(atom) + 1}");
        }

        foreach (IBond bond in molecule.Bonds)
        {
            if (bond.Atoms.Count != 2)
                continue;

            IAtom left = bond.Atoms[0], right = bond.Atoms[1];

            if (!indices.TryGetValue(left, out int first) || !indices.TryGetValue(right, out int second))
                continue;

            // Резонансный интеграл связи ослабляется тем из двух атомов,
            // который сильнее отличается от углерода
            double resonance = Math.Min(Describe(molecule, left).Resonance, Describe(molecule, right).Resonance);

            system.AddBond(first, second, resonance);
        }

        if (system.Count == 0)
            throw new ArgumentException("В структуре нет сопряжённой системы", nameof(smiles));

        return system;
    }

    /// <summary>
    /// Матрица Хюккеля в единицах beta при alpha = 0
    /// </summary>
    public Matrix TopologicalMatrix()
    {
        if (_centers.Count == 0)
            throw new InvalidOperationException("Сопряжённая система пуста");

        var matrix = new Matrix(_centers.Count, _centers.Count);

        for (int i = 0; i < _centers.Count; i++)
            matrix[i, i] = _centers[i].AlphaShift;

        foreach (var ((first, second), resonance) in _bonds)
        {
            matrix[first, second] = resonance;
            matrix[second, first] = resonance;
        }

        return matrix;
    }

    private static (int, int) Key(int first, int second)
        => first < second ? (first, second) : (second, first);

    private static bool HasMultipleBond(IAtomContainer molecule, IAtom atom)
    {
        foreach (IBond bond in molecule.GetConnectedBonds(atom))
        {
            if (bond.Order is BondOrder.Double or BondOrder.Triple && bond.Atoms.All(a => a.Symbol != "H"))
                return true;
        }

        return false;
    }

    // Гетероатом с неподелённой парой у кратной связи или ароматического кольца
    // тоже входит в сопряжение: так ведут себя азот анилина и кислород фенола
    private static bool IsConjugatedSubstituent(IAtomContainer molecule, IAtom atom)
    {
        if (!Heteroatoms.ContainsKey(atom.Symbol) || atom.Symbol == "C")
            return false;

        foreach (IBond bond in molecule.GetConnectedBonds(atom))
        {
            IAtom other = bond.GetOther(atom);

            if (other.IsAromatic || HasMultipleBond(molecule, other))
                return true;
        }

        return false;
    }

    private static HeteroatomParameters Describe(IAtomContainer molecule, IAtom atom)
    {
        string symbol = atom.Symbol;

        if (symbol == "C")
            return Heteroatoms["C"];

        if (symbol is "N" or "O")
        {
            // Один электрон отдаёт атом, уже занятый кратной связью (пиридин, карбонил);
            // два - атом, который вводит в систему неподелённую пару (пиррол, фуран)
            bool donatesPair = !HasMultipleBond(molecule, atom);
            string key = symbol + (donatesPair ? "2" : "1");

            return Heteroatoms[key];
        }

        return Heteroatoms.TryGetValue(symbol, out HeteroatomParameters parameters)
            ? parameters
            : new HeteroatomParameters(0, 1, 1);
    }
}
