using AI.Biology.Sequences;
using AI.Geometry.Primitives;
using AI.Solvers.Chem.Structures;

namespace AI.Biology.Structures;

/// <summary>Остаток белковой цепи и номера атомов его основной цепи</summary>
/// <param name="Chain">Идентификатор цепи</param>
/// <param name="Number">Номер остатка по файлу</param>
/// <param name="InsertionCode">Код вставки</param>
/// <param name="Name">Трёхбуквенное название</param>
/// <param name="N">Номер атома азота основной цепи; −1, если его нет</param>
/// <param name="CAlpha">Номер атома Cα; −1, если его нет</param>
/// <param name="C">Номер карбонильного углерода; −1, если его нет</param>
/// <param name="O">Номер карбонильного кислорода; −1, если его нет</param>
public sealed record ProteinResidue(char Chain, int Number, char InsertionCode, string Name, int N, int CAlpha, int C, int O)
{
    /// <summary>Однобуквенный код остатка</summary>
    public char Code => AminoAcids.OneLetter(Name);

    /// <summary>Есть ли все четыре атома основной цепи</summary>
    public bool HasBackbone => N >= 0 && CAlpha >= 0 && C >= 0 && O >= 0;

    /// <summary>Название, цепь и номер</summary>
    public override string ToString()
        => $"{Name} {Chain}{Number}{(InsertionCode == ' ' ? string.Empty : InsertionCode.ToString())}";
}

/// <summary>
/// Пространственная структура белка: атомы, разобранные на остатки основной цепи.
/// </summary>
/// <remarks>
/// <para>
/// Атомы хранятся в <see cref="MolecularStructure"/> из химического модуля и читаются его же
/// разбором PDB: второго парсера формата здесь нет. Остатки собираются по цепи, номеру и коду
/// вставки; учитываются только аминокислоты, вода и лиганды пропускаются.
/// </para>
/// <para>
/// Соседние остатки считаются связанными пептидной связью, если расстояние C(i)–N(i+1) не больше
/// 2 Å при длине связи 1,33 Å. Иначе в цепи разрыв — в кристаллических структурах так выглядят
/// неразрешённые петли, — и двугранные углы на разрыве не определены.
/// </para>
/// </remarks>
public sealed class ProteinStructure
{
    /// <summary>Наибольшее расстояние C–N, при котором остатки считаются связанными, ангстремы</summary>
    public const double PeptideBondLimit = 2.0;

    private readonly List<ProteinResidue> _residues;

    private ProteinStructure(MolecularStructure atoms, List<ProteinResidue> residues)
    {
        Atoms = atoms;
        _residues = residues;
    }

    /// <summary>Все атомы структуры</summary>
    public MolecularStructure Atoms { get; }

    /// <summary>Аминокислотные остатки в порядке файла</summary>
    public IReadOnlyList<ProteinResidue> Residues => _residues;

    /// <summary>Число остатков</summary>
    public int Count => _residues.Count;

    /// <summary>Цепи в порядке появления</summary>
    public IReadOnlyList<char> Chains => _residues.Select(r => r.Chain).Distinct().ToList();

    /// <summary>Читает белок из PDB</summary>
    /// <param name="text">Содержимое файла</param>
    public static ProteinStructure ReadPdb(string text) => FromAtoms(StructureFormats.ReadPdb(text));

    /// <summary>Разбирает атомы с разметкой остатков на остатки белка</summary>
    /// <param name="atoms">Атомы с заполненными названием, номером и цепью остатка</param>
    public static ProteinStructure FromAtoms(MolecularStructure atoms)
    {
        ArgumentNullException.ThrowIfNull(atoms);

        var order = new List<(char Chain, int Number, char Insertion, string Name)>();
        var slots = new Dictionary<(char, int, char), int[]>();

        for (int i = 0; i < atoms.Count; i++)
        {
            AtomSite atom = atoms.Atoms[i];

            if (!AminoAcids.IsAminoAcid(atom.ResidueName))
                continue;

            var key = (atom.ChainId, atom.ResidueNumber, atom.InsertionCode);

            if (!slots.TryGetValue(key, out int[]? backbone))
            {
                backbone = [-1, -1, -1, -1];
                slots[key] = backbone;
                order.Add((atom.ChainId, atom.ResidueNumber, atom.InsertionCode, atom.ResidueName.Trim().ToUpperInvariant()));
            }

            int slot = atom.Label switch
            {
                "N" => 0,
                "CA" => 1,
                "C" => 2,
                "O" => 3,
                _ => -1
            };

            if (slot >= 0 && backbone[slot] < 0)
                backbone[slot] = i;
        }

        var residues = order
            .Select(r =>
            {
                int[] b = slots[(r.Chain, r.Number, r.Insertion)];
                return new ProteinResidue(r.Chain, r.Number, r.Insertion, r.Name, b[0], b[1], b[2], b[3]);
            })
            .ToList();

        return new ProteinStructure(atoms, residues);
    }

    /// <summary>Аминокислотная последовательность всех цепей подряд</summary>
    public string Sequence() => new(_residues.Select(r => r.Code).ToArray());

    /// <summary>Аминокислотная последовательность одной цепи</summary>
    /// <param name="chain">Идентификатор цепи</param>
    public string Sequence(char chain) => new(_residues.Where(r => r.Chain == chain).Select(r => r.Code).ToArray());

    /// <summary>Координаты атома</summary>
    /// <param name="atom">Номер атома</param>
    public Vector3 Position(int atom) => Atoms.Atoms[atom].Position;

    /// <summary>Связан ли остаток со следующим пептидной связью</summary>
    /// <param name="index">Номер остатка</param>
    public bool IsLinked(int index)
    {
        if (index < 0 || index + 1 >= Count)
            return false;

        ProteinResidue current = _residues[index];
        ProteinResidue next = _residues[index + 1];

        return current.Chain == next.Chain && current.C >= 0 && next.N >= 0
            && Position(current.C).DistanceTo(Position(next.N)) <= PeptideBondLimit;
    }

    /// <summary>Двугранный угол φ = C(i−1)–N–Cα–C, градусы; NaN на начале цепи и на разрыве</summary>
    /// <param name="index">Номер остатка</param>
    public double Phi(int index)
    {
        if (index <= 0 || index >= Count || !IsLinked(index - 1))
            return double.NaN;

        ProteinResidue previous = _residues[index - 1];
        ProteinResidue current = _residues[index];

        return current.CAlpha < 0 || current.C < 0
            ? double.NaN
            : Atoms.Torsion(previous.C, current.N, current.CAlpha, current.C);
    }

    /// <summary>Двугранный угол ψ = N–Cα–C–N(i+1), градусы; NaN на конце цепи и на разрыве</summary>
    /// <param name="index">Номер остатка</param>
    public double Psi(int index)
    {
        if (!IsLinked(index))
            return double.NaN;

        ProteinResidue current = _residues[index];
        ProteinResidue next = _residues[index + 1];

        return current.N < 0 || current.CAlpha < 0
            ? double.NaN
            : Atoms.Torsion(current.N, current.CAlpha, current.C, next.N);
    }

    /// <summary>
    /// Двугранный угол ω = Cα–C–N(i+1)–Cα(i+1), градусы: около 180° у транс-пептида, около 0° у цис
    /// </summary>
    /// <param name="index">Номер остатка</param>
    public double Omega(int index)
    {
        if (!IsLinked(index))
            return double.NaN;

        ProteinResidue current = _residues[index];
        ProteinResidue next = _residues[index + 1];

        return current.CAlpha < 0 || next.CAlpha < 0
            ? double.NaN
            : Atoms.Torsion(current.CAlpha, current.C, next.N, next.CAlpha);
    }

    /// <summary>
    /// Атомы Cα по порядку остатков — для совмещения и мер формы из химического модуля
    /// </summary>
    /// <remarks>Остатки без атома Cα пропускаются.</remarks>
    public MolecularStructure CAlphaTrace()
    {
        var trace = new MolecularStructure { Name = Atoms.Name };

        foreach (ProteinResidue residue in _residues.Where(r => r.CAlpha >= 0))
            trace.Add(Atoms.Atoms[residue.CAlpha]);

        return trace;
    }

    /// <summary>Радиус инерции по атомам Cα, ангстремы</summary>
    /// <remarks>
    /// У компактных глобулярных белков он растёт как N^0,38 от числа остатков; заметно больший
    /// радиус означает вытянутый или развёрнутый белок.
    /// </remarks>
    public double RadiusOfGyration() => CAlphaTrace().RadiusOfGyration();

    /// <summary>Карта контактов: пары остатков, чьи атомы Cα ближе порога</summary>
    /// <param name="cutoff">Порог расстояния, ангстремы; принято 8 Å</param>
    public bool[,] ContactMap(double cutoff = 8.0)
    {
        MolecularStructure trace = CAlphaTrace();
        int n = trace.Count;
        var map = new bool[n, n];

        for (int i = 0; i < n; i++)
        {
            map[i, i] = true;

            for (int j = i + 1; j < n; j++)
            {
                bool contact = trace.Atoms[i].Position.DistanceTo(trace.Atoms[j].Position) <= cutoff;
                map[i, j] = contact;
                map[j, i] = contact;
            }
        }

        return map;
    }

    /// <summary>
    /// Относительный контактный порядок: средняя удалённость контактирующих остатков по цепи,
    /// делённая на длину цепи
    /// </summary>
    /// <param name="cutoff">Порог контакта по Cα, ангстремы</param>
    /// <param name="minimumSeparation">Наименьшее расстояние по цепи, начиная с которого пара считается контактом</param>
    /// <remarks>
    /// Плэкско, Саймонс и Бейкер (1998) нашли, что скорость сворачивания небольших белков падает
    /// с ростом контактного порядка: белку с дальними по цепи контактами дольше искать свою форму.
    /// В оригинале контакты считаются по всем тяжёлым атомам на 6 Å; здесь — по Cα на 8 Å, и
    /// абсолютные значения с опубликованными напрямую не сравниваются.
    /// </remarks>
    public double RelativeContactOrder(double cutoff = 8.0, int minimumSeparation = 3)
    {
        MolecularStructure trace = CAlphaTrace();
        int n = trace.Count;
        long separationSum = 0;
        int contacts = 0;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + minimumSeparation; j < n; j++)
            {
                if (trace.Atoms[i].Position.DistanceTo(trace.Atoms[j].Position) > cutoff)
                    continue;

                separationSum += j - i;
                contacts++;
            }
        }

        return contacts == 0 ? 0 : (double)separationSum / ((double)n * contacts);
    }

    /// <summary>Краткое описание</summary>
    public override string ToString()
        => $"{(string.IsNullOrEmpty(Atoms.Name) ? "белок" : Atoms.Name)}: остатков {Count}, цепей {Chains.Count}";
}
