using AI.Geometry.Primitives;
using AI.Solvers.Chem.Database;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Structures;

/// <summary>
/// Атом структуры
/// </summary>
public sealed class AtomSite
{
    /// <summary>Символ элемента</summary>
    public string Element { get; init; } = string.Empty;

    /// <summary>Метка узла (для кристаллических структур)</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Декартовы координаты, ангстремы</summary>
    public Vector3 Position { get; init; }

    /// <summary>Заселённость позиции</summary>
    public double Occupancy { get; init; } = 1.0;

    /// <summary>Изотропный тепловой параметр B, квадратные ангстремы</summary>
    public double ThermalParameter { get; init; }

    /// <summary>Формальный заряд</summary>
    public double Charge { get; init; }

    /// <summary>Копия атома со сдвинутой позицией</summary>
    /// <param name="position">Новые координаты</param>
    public AtomSite WithPosition(Vector3 position) => new()
    {
        Element = Element,
        Label = Label,
        Position = position,
        Occupancy = Occupancy,
        ThermalParameter = ThermalParameter,
        Charge = Charge
    };

    /// <summary>Элемент и координаты</summary>
    public override string ToString() => $"{Element} {Position}";
}

/// <summary>
/// Структура вещества: атомы с координатами и, для кристаллов, элементарная ячейка
/// </summary>
/// <remarks>
/// Один тип обслуживает молекулу, кадр траектории и кристаллическую структуру:
/// разница только в наличии ячейки. Периодические расчёты (расстояния, функции
/// распределения) при заданной ячейке используют ближайший образ.
/// </remarks>
public sealed class MolecularStructure
{
    private readonly List<AtomSite> _atoms = new();

    /// <summary>Название структуры</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Атомы</summary>
    public IReadOnlyList<AtomSite> Atoms => _atoms;

    /// <summary>Элементарная ячейка; null для непериодической системы</summary>
    public UnitCell Cell { get; set; }

    /// <summary>Периодична ли система</summary>
    public bool IsPeriodic => Cell != null;

    /// <summary>Число атомов</summary>
    public int Count => _atoms.Count;

    /// <summary>Создаёт пустую структуру</summary>
    public MolecularStructure()
    {
    }

    /// <summary>Создаёт структуру из атомов</summary>
    /// <param name="atoms">Атомы</param>
    /// <param name="cell">Элементарная ячейка</param>
    public MolecularStructure(IEnumerable<AtomSite> atoms, UnitCell cell = null)
    {
        ArgumentNullException.ThrowIfNull(atoms);
        _atoms.AddRange(atoms);
        Cell = cell;
    }

    /// <summary>Добавляет атом</summary>
    /// <param name="atom">Атом</param>
    public MolecularStructure Add(AtomSite atom)
    {
        ArgumentNullException.ThrowIfNull(atom);
        _atoms.Add(atom);
        return this;
    }

    /// <summary>Добавляет атом по элементу и координатам</summary>
    /// <param name="element">Символ элемента</param>
    /// <param name="x">Координата X</param>
    /// <param name="y">Координата Y</param>
    /// <param name="z">Координата Z</param>
    public MolecularStructure Add(string element, double x, double y, double z)
        => Add(new AtomSite { Element = element, Position = new Vector3(x, y, z) });

    /// <summary>Добавляет атом в дробных координатах ячейки</summary>
    /// <param name="element">Символ элемента</param>
    /// <param name="x">Дробная координата по a</param>
    /// <param name="y">Дробная координата по b</param>
    /// <param name="z">Дробная координата по c</param>
    /// <param name="label">Метка узла</param>
    /// <param name="occupancy">Заселённость</param>
    public MolecularStructure AddFractional(string element, double x, double y, double z,
        string label = "", double occupancy = 1.0)
    {
        if (Cell == null)
            throw new InvalidOperationException("Дробные координаты требуют заданной элементарной ячейки");

        return Add(new AtomSite
        {
            Element = element,
            Label = label,
            Position = Cell.ToCartesian(new Vector3(x, y, z)),
            Occupancy = occupancy
        });
    }

    /// <summary>Дробные координаты атома</summary>
    /// <param name="index">Номер атома</param>
    public Vector3 FractionalPosition(int index)
        => Cell == null
            ? throw new InvalidOperationException("У структуры нет элементарной ячейки")
            : Cell.ToFractional(_atoms[index].Position);

    /// <summary>Брутто-формула структуры</summary>
    public string Formula
    {
        get
        {
            var counts = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (AtomSite atom in _atoms)
            {
                counts.TryGetValue(atom.Element, out double current);
                counts[atom.Element] = current + atom.Occupancy;
            }

            var text = new StringBuilder();

            foreach (var entry in counts.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                text.Append(entry.Key);

                if (Math.Abs(entry.Value - 1) > 1e-9)
                    text.Append(entry.Value.ToString("G4", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }
    }

    /// <summary>Молярная масса структуры, г/моль</summary>
    /// <param name="database">Справочник элементов</param>
    public double MolarMass(ChemDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        double mass = 0;

        foreach (AtomSite atom in _atoms)
        {
            var element = database.GetElement(atom.Element)
                ?? throw new InvalidOperationException($"Элемент '{atom.Element}' отсутствует в справочнике");

            mass += element.AtomicMass * atom.Occupancy;
        }

        return mass;
    }

    /// <summary>Расстояние между атомами, ангстремы</summary>
    /// <param name="first">Номер первого атома</param>
    /// <param name="second">Номер второго атома</param>
    public double Distance(int first, int second)
    {
        Vector3 from = _atoms[first].Position;
        Vector3 to = _atoms[second].Position;

        return Cell == null ? from.DistanceTo(to) : Cell.MinimumImage(from, to).Length;
    }

    /// <summary>Валентный угол, градусы</summary>
    /// <param name="first">Номер первого атома</param>
    /// <param name="vertex">Номер атома в вершине угла</param>
    /// <param name="second">Номер второго атома</param>
    public double Angle(int first, int vertex, int second)
    {
        Vector3 left = _atoms[first].Position - _atoms[vertex].Position;
        Vector3 right = _atoms[second].Position - _atoms[vertex].Position;

        return left.AngleTo(right);
    }

    /// <summary>Двугранный угол, градусы</summary>
    /// <param name="first">Номер первого атома</param>
    /// <param name="second">Номер второго атома</param>
    /// <param name="third">Номер третьего атома</param>
    /// <param name="fourth">Номер четвёртого атома</param>
    public double Torsion(int first, int second, int third, int fourth)
    {
        Vector3 b1 = _atoms[second].Position - _atoms[first].Position;
        Vector3 b2 = _atoms[third].Position - _atoms[second].Position;
        Vector3 b3 = _atoms[fourth].Position - _atoms[third].Position;

        Vector3 n1 = b1.Cross(b2);
        Vector3 n2 = b2.Cross(b3);

        double y = n1.Cross(n2).Dot(b2.Normalized);
        double x = n1.Dot(n2);

        return Math.Atan2(y, x) * 180 / Math.PI;
    }

    /// <summary>Геометрический центр</summary>
    public Vector3 Centroid
    {
        get
        {
            if (_atoms.Count == 0)
                return Vector3.Zero;

            Vector3 sum = Vector3.Zero;

            foreach (AtomSite atom in _atoms)
                sum += atom.Position;

            return sum / _atoms.Count;
        }
    }

    /// <summary>Центр масс</summary>
    /// <param name="database">Справочник элементов</param>
    public Vector3 CenterOfMass(ChemDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        Vector3 sum = Vector3.Zero;
        double total = 0;

        foreach (AtomSite atom in _atoms)
        {
            var element = database.GetElement(atom.Element);
            double mass = element?.AtomicMass ?? 0;

            sum += atom.Position * mass;
            total += mass;
        }

        return total > 0 ? sum / total : Centroid;
    }

    /// <summary>Радиус инерции относительно геометрического центра, ангстремы</summary>
    public double RadiusOfGyration()
    {
        if (_atoms.Count == 0)
            return 0;

        Vector3 center = Centroid;
        double sum = 0;

        foreach (AtomSite atom in _atoms)
            sum += (atom.Position - center).LengthSquared;

        return Math.Sqrt(sum / _atoms.Count);
    }

    /// <summary>Копия структуры со сдвигом всех атомов</summary>
    /// <param name="shift">Вектор сдвига</param>
    public MolecularStructure Translate(Vector3 shift)
        => new(_atoms.Select(a => a.WithPosition(a.Position + shift)), Cell) { Name = Name };

    /// <summary>Копия структуры с центром в начале координат</summary>
    public MolecularStructure Centered() => Translate(-Centroid);

    /// <summary>Атомы заданного элемента</summary>
    /// <param name="element">Символ элемента</param>
    public IEnumerable<AtomSite> OfElement(string element)
        => _atoms.Where(a => string.Equals(a.Element, element, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Пары атомов, находящихся ближе заданного расстояния
    /// </summary>
    /// <param name="maxDistance">Порог расстояния, ангстремы</param>
    public IEnumerable<(int First, int Second, double Distance)> Contacts(double maxDistance)
    {
        for (int i = 0; i < _atoms.Count; i++)
        {
            for (int j = i + 1; j < _atoms.Count; j++)
            {
                double distance = Distance(i, j);

                if (distance <= maxDistance)
                    yield return (i, j, distance);
            }
        }
    }

    /// <summary>Краткое описание структуры</summary>
    public override string ToString()
        => $"{(string.IsNullOrEmpty(Name) ? "структура" : Name)}: {Count} атомов, {Formula}"
        + (IsPeriodic ? $", ячейка {Cell}" : string.Empty);
}
