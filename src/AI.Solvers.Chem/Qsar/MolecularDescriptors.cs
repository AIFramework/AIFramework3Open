using AI.DataStructs.Algebraic;
using NCDK;
using NCDK.Aromaticities;
using NCDK.Graphs;
using NCDK.Smiles;
using NCDK.Tools.Manipulator;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Qsar;

/// <summary>
/// Набор молекулярных дескрипторов
/// </summary>
public sealed class DescriptorSet
{
    /// <summary>Названия дескрипторов</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Значения дескрипторов</summary>
    public Vector Values { get; }

    /// <summary>Исходная структура</summary>
    public string Smiles { get; }

    /// <summary>Создаёт набор</summary>
    /// <param name="names">Названия дескрипторов</param>
    /// <param name="values">Значения дескрипторов</param>
    /// <param name="smiles">Исходная строка SMILES</param>
    public DescriptorSet(IReadOnlyList<string> names, Vector values, string smiles = null)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(values);

        if (names.Count != values.Count)
            throw new ArgumentException("Число названий и число значений должно совпадать");

        Names = names;
        Values = values;
        Smiles = smiles ?? string.Empty;
    }

    /// <summary>Значение дескриптора по названию</summary>
    /// <param name="name">Название дескриптора</param>
    public double this[string name]
    {
        get
        {
            for (int i = 0; i < Names.Count; i++)
            {
                if (string.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase))
                    return Values[i];
            }

            throw new KeyNotFoundException($"Дескриптор {name} не рассчитывается");
        }
    }

    /// <summary>Отчёт по дескрипторам</summary>
    public string Report()
    {
        var text = new StringBuilder();

        text.AppendLine($"Дескрипторы структуры {Smiles}");

        for (int i = 0; i < Names.Count; i++)
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-20} {1,12:F4}", Names[i], Values[i]));

        return text.ToString();
    }
}

/// <summary>
/// Расчёт молекулярных дескрипторов по структуре
/// </summary>
/// <remarks>
/// Считаются только те величины, которые выводятся из структурной формулы точно:
/// состав, циклы, топологические индексы графа тяжёлых атомов. Оценочные схемы
/// вроде аддитивного logP сюда намеренно не входят - в модели свойств их место
/// занимает обучение по реальным измерениям.
/// </remarks>
public static class MolecularDescriptors
{
    private static readonly string[] DescriptorNames =
    {
        "MolarMass",
        "HeavyAtoms",
        "Carbons",
        "Nitrogens",
        "Oxygens",
        "Sulfurs",
        "Halogens",
        "Hydrogens",
        "HeteroatomFraction",
        "FormalCharge",
        "Rings",
        "AromaticRings",
        "AromaticAtoms",
        "RotatableBonds",
        "HBondDonors",
        "HBondAcceptors",
        "DoubleBonds",
        "TripleBonds",
        "Unsaturation",
        "Fsp3",
        "Wiener",
        "Randic",
        "ZagrebM1",
        "ZagrebM2",
        "BalabanJ",
        "Diameter",
        "AverageDegree"
    };

    /// <summary>Названия рассчитываемых дескрипторов</summary>
    public static IReadOnlyList<string> Names => DescriptorNames;

    /// <summary>Считает дескрипторы по строке SMILES</summary>
    /// <param name="smiles">Строка SMILES</param>
    public static DescriptorSet Compute(string smiles)
    {
        if (string.IsNullOrWhiteSpace(smiles))
            throw new ArgumentException("Пустая строка SMILES", nameof(smiles));

        var parser = new SmilesParser(NCDK.Default.ChemObjectBuilder.Instance);
        IAtomContainer molecule = parser.ParseSmiles(smiles);

        AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(molecule);
        CDK.HydrogenAdder.AddImplicitHydrogens(molecule);
        Aromaticity.CDKLegacy.Apply(molecule);

        return Compute(molecule, smiles);
    }

    /// <summary>Считает дескрипторы по подготовленной структуре</summary>
    /// <param name="molecule">Структура с расставленными типами атомов и ароматичностью</param>
    /// <param name="smiles">Исходная строка SMILES для отчёта</param>
    public static DescriptorSet Compute(IAtomContainer molecule, string smiles = null)
    {
        ArgumentNullException.ThrowIfNull(molecule);

        var heavy = molecule.Atoms.Where(a => a.Symbol != "H").ToList();

        if (heavy.Count == 0)
            throw new ArgumentException("В структуре нет тяжёлых атомов", nameof(molecule));

        var index = new Dictionary<IAtom, int>();

        for (int i = 0; i < heavy.Count; i++)
            index[heavy[i]] = i;

        int carbons = heavy.Count(a => a.Symbol == "C");
        int nitrogens = heavy.Count(a => a.Symbol == "N");
        int oxygens = heavy.Count(a => a.Symbol == "O");
        int sulfurs = heavy.Count(a => a.Symbol == "S");
        int halogens = heavy.Count(a => a.Symbol is "F" or "Cl" or "Br" or "I");

        int hydrogens = molecule.Atoms.Count(a => a.Symbol == "H")
            + molecule.Atoms.Sum(a => a.ImplicitHydrogenCount ?? 0);

        int charge = molecule.Atoms.Sum(a => a.FormalCharge ?? 0);

        var formula = MolecularFormulaManipulator.GetMolecularFormula(molecule);
        double mass = MolecularFormulaManipulator.GetMass(formula);

        int[][] rings = Cycles.FindSSSR(molecule).GetPaths();
        int aromaticRings = rings.Count(path => IsAromaticRing(molecule, path));
        int aromaticAtoms = heavy.Count(a => a.IsAromatic);

        int doubleBonds = molecule.Bonds.Count(b => b.Order == BondOrder.Double && !b.IsAromatic);
        int tripleBonds = molecule.Bonds.Count(b => b.Order == BondOrder.Triple);

        double unsaturation = ((2.0 * carbons) + 2 + nitrogens - hydrogens - halogens) / 2.0;

        int sp3 = heavy.Count(a => a.Symbol == "C" && IsSp3(molecule, a));
        double fsp3 = carbons > 0 ? (double)sp3 / carbons : 0;

        int donors = heavy.Count(a => a.Symbol is "N" or "O" && HydrogensOn(molecule, a) > 0);
        int acceptors = heavy.Count(a => a.Symbol is "N" or "O");
        int rotatable = CountRotatable(molecule);

        var degrees = new int[heavy.Count];

        foreach (IAtom atom in heavy)
            degrees[index[atom]] = molecule.GetConnectedAtoms(atom).Count(a => a.Symbol != "H");

        int[,] distance = Distances(molecule, heavy, index);
        var (wiener, diameter, distanceSums) = DistanceStatistics(distance, heavy.Count);
        var (randic, zagrebM2) = EdgeIndices(molecule, index, degrees);

        double zagrebM1 = degrees.Sum(d => (double)d * d);
        double balaban = Balaban(molecule, index, distanceSums, heavy.Count);
        double averageDegree = degrees.Average();

        var values = new Vector(
            mass,
            heavy.Count,
            carbons,
            nitrogens,
            oxygens,
            sulfurs,
            halogens,
            hydrogens,
            (double)(heavy.Count - carbons) / heavy.Count,
            charge,
            rings.Length,
            aromaticRings,
            aromaticAtoms,
            rotatable,
            donors,
            acceptors,
            doubleBonds,
            tripleBonds,
            unsaturation,
            fsp3,
            wiener,
            randic,
            zagrebM1,
            zagrebM2,
            balaban,
            diameter,
            averageDegree);

        return new DescriptorSet(DescriptorNames, values, smiles);
    }

    private static bool IsAromaticRing(IAtomContainer molecule, int[] path)
    {
        foreach (int atom in path.Distinct())
        {
            if (!molecule.Atoms[atom].IsAromatic)
                return false;
        }

        return true;
    }

    private static bool IsSp3(IAtomContainer molecule, IAtom atom)
    {
        if (atom.IsAromatic)
            return false;

        foreach (IBond bond in molecule.GetConnectedBonds(atom))
        {
            if (bond.Order != BondOrder.Single)
                return false;
        }

        return true;
    }

    private static int HydrogensOn(IAtomContainer molecule, IAtom atom)
        => (atom.ImplicitHydrogenCount ?? 0) + molecule.GetConnectedAtoms(atom).Count(a => a.Symbol == "H");

    private static int CountRotatable(IAtomContainer molecule)
    {
        int count = 0;

        foreach (IBond bond in molecule.Bonds)
        {
            if (bond.Order != BondOrder.Single || bond.IsAromatic || bond.IsInRing)
                continue;

            IAtom left = bond.Begin, right = bond.End;

            if (left.Symbol == "H" || right.Symbol == "H")
                continue;

            int leftNeighbours = molecule.GetConnectedAtoms(left).Count(a => a.Symbol != "H");
            int rightNeighbours = molecule.GetConnectedAtoms(right).Count(a => a.Symbol != "H");

            // Связь с концевой группой вращается, но конформацию молекулы не меняет
            if (leftNeighbours <= 1 || rightNeighbours <= 1)
                continue;

            count++;
        }

        return count;
    }

    // Матрица топологических расстояний графа тяжёлых атомов, обход в ширину
    private static int[,] Distances(IAtomContainer molecule, IReadOnlyList<IAtom> heavy, Dictionary<IAtom, int> index)
    {
        int n = heavy.Count;
        var distance = new int[n, n];
        var queue = new Queue<int>();
        var neighbours = new List<int>[n];

        for (int i = 0; i < n; i++)
        {
            neighbours[i] = molecule.GetConnectedAtoms(heavy[i])
                .Where(a => index.ContainsKey(a))
                .Select(a => index[a])
                .ToList();
        }

        for (int start = 0; start < n; start++)
        {
            for (int i = 0; i < n; i++)
                distance[start, i] = -1;

            distance[start, start] = 0;
            queue.Clear();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                foreach (int next in neighbours[current])
                {
                    if (distance[start, next] < 0)
                    {
                        distance[start, next] = distance[start, current] + 1;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return distance;
    }

    private static (double Wiener, int Diameter, double[] Sums) DistanceStatistics(int[,] distance, int n)
    {
        double wiener = 0;
        int diameter = 0;
        var sums = new double[n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int value = distance[i, j];

                // Несвязные части (соль, сольват) в сумму не входят
                if (value < 0)
                    continue;

                sums[i] += value;

                if (j > i)
                {
                    wiener += value;
                    diameter = Math.Max(diameter, value);
                }
            }
        }

        return (wiener, diameter, sums);
    }

    private static (double Randic, double ZagrebM2) EdgeIndices(
        IAtomContainer molecule, Dictionary<IAtom, int> index, int[] degrees)
    {
        double randic = 0, zagreb = 0;

        foreach (IBond bond in molecule.Bonds)
        {
            if (!index.TryGetValue(bond.Begin, out int left) || !index.TryGetValue(bond.End, out int right))
                continue;

            int product = degrees[left] * degrees[right];

            if (product <= 0)
                continue;

            randic += 1 / Math.Sqrt(product);
            zagreb += product;
        }

        return (randic, zagreb);
    }

    private static double Balaban(IAtomContainer molecule, Dictionary<IAtom, int> index, double[] sums, int n)
    {
        int edges = molecule.Bonds.Count(b => index.ContainsKey(b.Begin) && index.ContainsKey(b.End));

        if (edges == 0)
            return 0;

        // Цикломатическое число графа
        int cyclomatic = edges - n + 1;
        double sum = 0;

        foreach (IBond bond in molecule.Bonds)
        {
            if (!index.TryGetValue(bond.Begin, out int left) || !index.TryGetValue(bond.End, out int right))
                continue;

            double product = sums[left] * sums[right];

            if (product > 0)
                sum += 1 / Math.Sqrt(product);
        }

        return (double)edges / (cyclomatic + 1) * sum;
    }
}
