using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Structures;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Crystallography;

/// <summary>
/// Кристаллическая структура: асимметричная часть, симметрия и элементарная ячейка
/// </summary>
/// <remarks>
/// Полное содержимое ячейки получается размножением асимметричной части операциями
/// симметрии с отбраковкой совпавших позиций: атом на particular position
/// (в центре инверсии, на оси) даёт меньше образов, чем число операций.
/// </remarks>
public sealed class Crystal
{
    private const double PositionTolerance = 1e-4;

    private MolecularStructure _contents;

    /// <summary>Асимметричная часть структуры</summary>
    public MolecularStructure AsymmetricUnit { get; }

    /// <summary>Операции симметрии</summary>
    public IReadOnlyList<SymmetryOperation> Symmetry { get; }

    /// <summary>Обозначение пространственной группы</summary>
    public string SpaceGroup { get; init; } = string.Empty;

    /// <summary>Номер группы по международным таблицам</summary>
    public int SpaceGroupNumber { get; init; }

    /// <summary>Элементарная ячейка</summary>
    public UnitCell Cell => AsymmetricUnit.Cell;

    /// <summary>Создаёт кристалл</summary>
    /// <param name="asymmetricUnit">Асимметричная часть с заданной ячейкой</param>
    /// <param name="symmetry">Операции симметрии; null - только тождественная</param>
    public Crystal(MolecularStructure asymmetricUnit, IReadOnlyList<SymmetryOperation> symmetry = null)
    {
        AsymmetricUnit = asymmetricUnit ?? throw new ArgumentNullException(nameof(asymmetricUnit));

        if (asymmetricUnit.Cell == null)
            throw new ArgumentException("Кристаллическая структура требует элементарной ячейки", nameof(asymmetricUnit));

        Symmetry = symmetry is { Count: > 0 } ? symmetry : new[] { SymmetryOperation.Identity };
    }

    /// <summary>Читает кристалл из CIF-файла</summary>
    /// <param name="text">Содержимое CIF</param>
    public static Crystal FromCif(string text)
    {
        CifContent content = StructureFormats.ReadCif(text);

        if (content.AsymmetricUnit.Cell == null)
            throw new FormatException("В CIF нет параметров элементарной ячейки");

        return new Crystal(content.AsymmetricUnit, content.Symmetry)
        {
            SpaceGroup = content.SpaceGroup,
            SpaceGroupNumber = content.SpaceGroupNumber
        };
    }

    /// <summary>
    /// Полное содержимое ячейки: асимметричная часть, размноженная симметрией
    /// </summary>
    public MolecularStructure Contents => _contents ??= Expand();

    /// <summary>Число атомов в ячейке</summary>
    public int AtomsInCell => Contents.Count;

    /// <summary>
    /// Число формульных единиц в ячейке, определённое по составу
    /// </summary>
    /// <param name="formulaUnitAtoms">Число атомов в формульной единице</param>
    public double FormulaUnits(int formulaUnitAtoms)
        => formulaUnitAtoms > 0 ? (double)AtomsInCell / formulaUnitAtoms : double.NaN;

    /// <summary>Рентгеновская плотность, г/см3</summary>
    /// <param name="database">Справочник элементов</param>
    public double Density(ChemDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        double mass = Contents.MolarMass(database);

        return mass / (0.602214076 * Cell.Volume);
    }

    /// <summary>
    /// Кратчайшие межатомные расстояния в структуре с учётом периодичности
    /// </summary>
    /// <param name="maxDistance">Порог расстояния, ангстремы</param>
    public IEnumerable<(string First, string Second, double Distance)> Contacts(double maxDistance)
    {
        MolecularStructure contents = Contents;

        foreach (var (first, second, distance) in contents.Contacts(maxDistance))
        {
            yield return (
                Describe(contents.Atoms[first]),
                Describe(contents.Atoms[second]),
                distance);
        }
    }

    /// <summary>Отчёт по структуре</summary>
    /// <param name="database">Справочник элементов для расчёта плотности</param>
    public string Report(ChemDatabase database = null)
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"Кристаллическая структура: {(string.IsNullOrEmpty(AsymmetricUnit.Name) ? "без названия" : AsymmetricUnit.Name)}");

        if (!string.IsNullOrEmpty(SpaceGroup))
            text.AppendLine($"  Пространственная группа: {SpaceGroup}" + (SpaceGroupNumber > 0 ? $" (№{SpaceGroupNumber})" : string.Empty));

        text.AppendLine($"  Ячейка: {Cell}");
        text.AppendLine($"  Сингония: {Cell.System}");
        text.AppendLine(string.Format(culture, "  Операций симметрии: {0}", Symmetry.Count));
        text.AppendLine(string.Format(culture, "  Атомов: {0} в асимметричной части, {1} в ячейке",
            AsymmetricUnit.Count, AtomsInCell));
        text.AppendLine($"  Состав ячейки: {Contents.Formula}");

        if (database != null)
            text.AppendLine(string.Format(culture, "  Рентгеновская плотность: {0:F3} г/см3", Density(database)));

        return text.ToString();
    }

    private MolecularStructure Expand()
    {
        var result = new MolecularStructure { Cell = Cell, Name = AsymmetricUnit.Name };
        var placed = new List<Vector3>();

        foreach (AtomSite atom in AsymmetricUnit.Atoms)
        {
            Vector3 fractional = Cell.ToFractional(atom.Position);

            foreach (SymmetryOperation operation in Symmetry)
            {
                Vector3 image = operation.ApplyWrapped(fractional);

                if (placed.Any(p => SamePosition(p, image)))
                    continue;

                placed.Add(image);
                result.Add(atom.WithPosition(Cell.ToCartesian(image)));
            }
        }

        return result;
    }

    // Позиции сравниваются с учётом периодичности: 0.999 и 0.001 - это одно место
    private static bool SamePosition(Vector3 left, Vector3 right)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            double difference = Math.Abs(left[axis] - right[axis]);
            difference = Math.Min(difference, 1 - difference);

            if (difference > PositionTolerance)
                return false;
        }

        return true;
    }

    private static string Describe(AtomSite atom)
        => string.IsNullOrEmpty(atom.Label) ? atom.Element : atom.Label;
}
