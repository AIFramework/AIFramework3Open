using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AI.Solvers.Chem.Structures;

/// <summary>
/// Содержимое CIF-файла: асимметричная часть структуры и её симметрия
/// </summary>
/// <param name="AsymmetricUnit">Атомы асимметричной части с ячейкой</param>
/// <param name="Symmetry">Операции симметрии пространственной группы</param>
/// <param name="SpaceGroup">Обозначение пространственной группы</param>
/// <param name="SpaceGroupNumber">Номер группы по международным таблицам; 0 если не указан</param>
public readonly record struct CifContent(
    MolecularStructure AsymmetricUnit,
    IReadOnlyList<SymmetryOperation> Symmetry,
    string SpaceGroup,
    int SpaceGroupNumber);

/// <summary>
/// Чтение и запись структурных форматов: XYZ, PDB, CIF
/// </summary>
/// <remarks>
/// Форматы читаются в объёме, достаточном для расчётов: координаты, элементы,
/// параметры ячейки и симметрия. Экзотические расширения игнорируются, а не
/// вызывают отказ, - файлы приборов и программ полны необязательных полей.
/// </remarks>
public static class StructureFormats
{
    private static readonly char[] Separators = { ' ', '\t' };

    /// <summary>Читает структуру из формата XYZ</summary>
    /// <param name="text">Содержимое файла</param>
    public static MolecularStructure ReadXyz(string text)
    {
        IReadOnlyList<MolecularStructure> frames = ReadXyzTrajectory(text);

        return frames.Count > 0
            ? frames[0]
            : throw new FormatException("Файл XYZ не содержит кадров");
    }

    /// <summary>
    /// Читает траекторию из многокадрового XYZ: кадры идут подряд, каждый со своим заголовком
    /// </summary>
    /// <param name="text">Содержимое файла</param>
    public static IReadOnlyList<MolecularStructure> ReadXyzTrajectory(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        var frames = new List<MolecularStructure>();
        int line = 0;

        while (line < lines.Length)
        {
            while (line < lines.Length && string.IsNullOrWhiteSpace(lines[line]))
                line++;

            if (line >= lines.Length)
                break;

            if (!int.TryParse(lines[line].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                throw new FormatException($"Ожидалось число атомов, встречено '{lines[line].Trim()}'");

            string comment = line + 1 < lines.Length ? lines[line + 1].Trim() : string.Empty;
            var structure = new MolecularStructure { Name = comment, Cell = ParseLatticeComment(comment) };

            for (int i = 0; i < count; i++)
            {
                int index = line + 2 + i;

                if (index >= lines.Length)
                    throw new FormatException("Файл XYZ оборван: атомов меньше заявленного числа");

                string[] parts = lines[index].Split(Separators, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4)
                    throw new FormatException($"Строка атома не разобрана: '{lines[index]}'");

                structure.Add(new AtomSite
                {
                    Element = NormalizeElement(parts[0]),
                    Position = new Vector3(Number(parts[1]), Number(parts[2]), Number(parts[3]))
                });
            }

            frames.Add(structure);
            line += count + 2;
        }

        return frames;
    }

    /// <summary>Записывает структуру в формате XYZ</summary>
    /// <param name="structure">Структура</param>
    public static string WriteXyz(MolecularStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var text = new StringBuilder();
        text.AppendLine(structure.Count.ToString(CultureInfo.InvariantCulture));
        text.AppendLine(structure.Name);

        foreach (AtomSite atom in structure.Atoms)
        {
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-3} {1,14:F6} {2,14:F6} {3,14:F6}",
                atom.Element, atom.Position.X, atom.Position.Y, atom.Position.Z));
        }

        return text.ToString();
    }

    /// <summary>Читает структуру из формата PDB</summary>
    /// <param name="text">Содержимое файла</param>
    public static MolecularStructure ReadPdb(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var structure = new MolecularStructure();

        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.StartsWith("CRYST1", StringComparison.Ordinal) && raw.Length >= 54)
            {
                structure.Cell = new UnitCell(
                    Number(raw.Substring(6, 9)), Number(raw.Substring(15, 9)), Number(raw.Substring(24, 9)),
                    Number(raw.Substring(33, 7)), Number(raw.Substring(40, 7)), Number(raw.Substring(47, 7)));

                continue;
            }

            if (raw.StartsWith("TITLE", StringComparison.Ordinal) && raw.Length > 10)
            {
                structure.Name = raw[10..].Trim();
                continue;
            }

            bool isAtom = raw.StartsWith("ATOM", StringComparison.Ordinal)
                || raw.StartsWith("HETATM", StringComparison.Ordinal);

            if (!isAtom || raw.Length < 54)
                continue;

            // Символ элемента стоит в колонках 77-78, но во многих файлах он пуст:
            // тогда его выводят из имени атома
            string element = raw.Length >= 78 ? raw.Substring(76, 2).Trim() : string.Empty;

            if (element.Length == 0)
                element = new string(raw.Substring(12, 4).Trim().TakeWhile(char.IsLetter).ToArray());

            structure.Add(new AtomSite
            {
                Element = NormalizeElement(element),
                Label = raw.Substring(12, 4).Trim(),
                Position = new Vector3(
                    Number(raw.Substring(30, 8)),
                    Number(raw.Substring(38, 8)),
                    Number(raw.Substring(46, 8))),
                Occupancy = raw.Length >= 60 ? NumberOrDefault(raw.Substring(54, 6), 1.0) : 1.0,
                ThermalParameter = raw.Length >= 66 ? NumberOrDefault(raw.Substring(60, 6), 0) : 0
            });
        }

        return structure;
    }

    /// <summary>Читает CIF-файл: ячейку, асимметричную часть и симметрию</summary>
    /// <param name="text">Содержимое файла</param>
    public static CifContent ReadCif(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var symmetry = new List<SymmetryOperation>();
        var atoms = new List<(string Element, string Label, Vector3 Fractional, double Occupancy, double Thermal)>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("loop_", StringComparison.OrdinalIgnoreCase))
            {
                var headers = new List<string>();
                int cursor = i + 1;

                while (cursor < lines.Length && lines[cursor].Trim().StartsWith('_'))
                {
                    headers.Add(lines[cursor].Trim().ToLowerInvariant());
                    cursor++;
                }

                var rows = new List<string[]>();

                while (cursor < lines.Length)
                {
                    string row = lines[cursor].Trim();

                    if (row.Length == 0 || row.StartsWith('_') || row.StartsWith("loop_", StringComparison.OrdinalIgnoreCase)
                        || row.StartsWith("data_", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    rows.Add(SplitCifRow(row));
                    cursor++;
                }

                CollectLoop(headers, rows, symmetry, atoms);
                i = cursor - 1;
                continue;
            }

            if (line.StartsWith('_'))
            {
                string[] parts = SplitCifRow(line);

                if (parts.Length >= 2)
                    values[parts[0].ToLowerInvariant()] = string.Join(" ", parts.Skip(1));
            }
        }

        UnitCell cell = null;

        if (values.ContainsKey("_cell_length_a"))
        {
            cell = new UnitCell(
                CifNumber(values, "_cell_length_a"),
                CifNumber(values, "_cell_length_b"),
                CifNumber(values, "_cell_length_c"),
                CifNumber(values, "_cell_angle_alpha", 90),
                CifNumber(values, "_cell_angle_beta", 90),
                CifNumber(values, "_cell_angle_gamma", 90));
        }

        var structure = new MolecularStructure { Cell = cell };

        if (values.TryGetValue("_chemical_name_common", out string name)
            || values.TryGetValue("_chemical_formula_sum", out name))
        {
            structure.Name = name.Trim('\'', '"');
        }

        foreach (var atom in atoms)
        {
            structure.Add(new AtomSite
            {
                Element = atom.Element,
                Label = atom.Label,
                Position = cell?.ToCartesian(atom.Fractional) ?? atom.Fractional,
                Occupancy = atom.Occupancy,
                ThermalParameter = atom.Thermal
            });
        }

        if (symmetry.Count == 0)
            symmetry.Add(SymmetryOperation.Identity);

        string group = values.TryGetValue("_symmetry_space_group_name_h-m", out string groupName)
            || values.TryGetValue("_space_group_name_h-m_alt", out groupName)
            ? groupName.Trim('\'', '"')
            : string.Empty;

        int number = (int)CifNumber(values, "_symmetry_int_tables_number", 0);

        if (number == 0)
            number = (int)CifNumber(values, "_space_group_it_number", 0);

        return new CifContent(structure, symmetry, group, number);
    }

    private static void CollectLoop(
        List<string> headers,
        List<string[]> rows,
        List<SymmetryOperation> symmetry,
        List<(string, string, Vector3, double, double)> atoms)
    {
        int symmetryColumn = headers.FindIndex(h =>
            h.Contains("symmetry_equiv_pos_as_xyz", StringComparison.Ordinal)
            || h.Contains("space_group_symop_operation_xyz", StringComparison.Ordinal));

        if (symmetryColumn >= 0)
        {
            foreach (string[] row in rows)
            {
                if (symmetryColumn >= row.Length)
                    continue;

                if (SymmetryOperation.TryParse(row[symmetryColumn], out SymmetryOperation operation))
                    symmetry.Add(operation);
            }

            return;
        }

        int typeColumn = headers.IndexOf("_atom_site_type_symbol");
        int labelColumn = headers.IndexOf("_atom_site_label");
        int xColumn = headers.IndexOf("_atom_site_fract_x");
        int yColumn = headers.IndexOf("_atom_site_fract_y");
        int zColumn = headers.IndexOf("_atom_site_fract_z");
        int occupancyColumn = headers.IndexOf("_atom_site_occupancy");
        int thermalColumn = headers.IndexOf("_atom_site_u_iso_or_equiv");

        if (xColumn < 0 || yColumn < 0 || zColumn < 0)
            return;

        foreach (string[] row in rows)
        {
            if (row.Length <= Math.Max(xColumn, Math.Max(yColumn, zColumn)))
                continue;

            string label = labelColumn >= 0 && labelColumn < row.Length ? row[labelColumn] : string.Empty;
            string element = typeColumn >= 0 && typeColumn < row.Length ? row[typeColumn] : label;

            atoms.Add((
                NormalizeElement(element),
                label,
                new Vector3(CifValue(row[xColumn]), CifValue(row[yColumn]), CifValue(row[zColumn])),
                occupancyColumn >= 0 && occupancyColumn < row.Length ? CifValueOrDefault(row[occupancyColumn], 1.0) : 1.0,
                thermalColumn >= 0 && thermalColumn < row.Length ? CifValueOrDefault(row[thermalColumn], 0) : 0));
        }
    }

    // Строка CIF: значения через пробелы, части в кавычках склеиваются
    private static string[] SplitCifRow(string line)
        => Regex.Matches(line, @"'[^']*'|""[^""]*""|\S+")
            .Select(m => m.Value.Trim('\'', '"'))
            .ToArray();

    // Значение CIF может нести погрешность в скобках: 5.6402(2)
    private static double CifValue(string text)
        => CifValueOrDefault(text, double.NaN) is var value && double.IsNaN(value)
            ? throw new FormatException($"Не разобрано числовое значение CIF '{text}'")
            : value;

    private static double CifValueOrDefault(string text, double fallback)
    {
        if (string.IsNullOrWhiteSpace(text) || text is "." or "?")
            return fallback;

        int bracket = text.IndexOf('(');
        string body = bracket >= 0 ? text[..bracket] : text;

        return double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;
    }

    private static double CifNumber(Dictionary<string, string> values, string key, double fallback = double.NaN)
    {
        if (!values.TryGetValue(key, out string text))
        {
            return double.IsNaN(fallback)
                ? throw new FormatException($"В CIF отсутствует обязательное поле {key}")
                : fallback;
        }

        return CifValueOrDefault(text, fallback);
    }

    // Ячейка в комментарии XYZ расширенного формата: Lattice="ax ay az bx by bz cx cy cz"
    private static UnitCell ParseLatticeComment(string comment)
    {
        Match match = Regex.Match(comment, "Lattice=\"([^\"]+)\"", RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        double[] numbers = match.Groups[1].Value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(Number)
            .ToArray();

        if (numbers.Length != 9)
            return null;

        var a = new Vector3(numbers[0], numbers[1], numbers[2]);
        var b = new Vector3(numbers[3], numbers[4], numbers[5]);
        var c = new Vector3(numbers[6], numbers[7], numbers[8]);

        return new UnitCell(a.Length, b.Length, c.Length, b.AngleTo(c), a.AngleTo(c), a.AngleTo(b));
    }

    private static string NormalizeElement(string symbol)
    {
        string letters = new(symbol.Trim().TakeWhile(char.IsLetter).ToArray());

        if (letters.Length == 0)
            return symbol.Trim();

        return char.ToUpperInvariant(letters[0]) + (letters.Length > 1 ? letters[1..].ToLowerInvariant() : string.Empty);
    }

    private static double Number(string text)
        => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new FormatException($"Не разобрано число '{text.Trim()}'");

    private static double NumberOrDefault(string text, double fallback)
        => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;
}
