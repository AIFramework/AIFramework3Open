namespace AI.Biology.Sequences;

/// <summary>
/// Аминокислоты: однобуквенный и трёхбуквенный коды, гидропатичность.
/// </summary>
/// <remarks>
/// Трёхбуквенные названия нужны для чтения структур: в файлах PDB остатки записаны именно так.
/// Кроме двадцати стандартных распознаются селеноцистеин (SEC, U), пирролизин (PYL, O) и
/// селенометионин (MSE): им заменяют метионин для кристаллографии, и по смыслу это метионин.
/// </remarks>
public static class AminoAcids
{
    /// <summary>Двадцать стандартных аминокислот в однобуквенном коде</summary>
    public const string Standard = "ACDEFGHIKLMNPQRSTVWY";

    private static readonly Dictionary<string, char> ThreeToOne = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALA"] = 'A', ["ARG"] = 'R', ["ASN"] = 'N', ["ASP"] = 'D', ["CYS"] = 'C',
        ["GLN"] = 'Q', ["GLU"] = 'E', ["GLY"] = 'G', ["HIS"] = 'H', ["ILE"] = 'I',
        ["LEU"] = 'L', ["LYS"] = 'K', ["MET"] = 'M', ["PHE"] = 'F', ["PRO"] = 'P',
        ["SER"] = 'S', ["THR"] = 'T', ["TRP"] = 'W', ["TYR"] = 'Y', ["VAL"] = 'V',
        ["SEC"] = 'U', ["PYL"] = 'O', ["MSE"] = 'M'
    };

    private static readonly Dictionary<char, string> OneToThree = ThreeToOne
        .Where(p => p.Key != "MSE")
        .ToDictionary(p => p.Value, p => p.Key.ToUpperInvariant());

    // Шкала Кайта — Дулиттла (1982): положительные значения — гидрофобные остатки
    private static readonly Dictionary<char, double> KyteDoolittle = new()
    {
        ['A'] = 1.8, ['R'] = -4.5, ['N'] = -3.5, ['D'] = -3.5, ['C'] = 2.5,
        ['Q'] = -3.5, ['E'] = -3.5, ['G'] = -0.4, ['H'] = -3.2, ['I'] = 4.5,
        ['L'] = 3.8, ['K'] = -3.9, ['M'] = 1.9, ['F'] = 2.8, ['P'] = -1.6,
        ['S'] = -0.8, ['T'] = -0.7, ['W'] = -0.9, ['Y'] = -1.3, ['V'] = 4.2
    };

    /// <summary>Является ли остаток аминокислотой</summary>
    /// <param name="residueName">Трёхбуквенное название</param>
    public static bool IsAminoAcid(string? residueName)
        => residueName is not null && ThreeToOne.ContainsKey(residueName.Trim());

    /// <summary>Однобуквенный код по трёхбуквенному; X для неизвестного остатка</summary>
    /// <param name="residueName">Трёхбуквенное название</param>
    public static char OneLetter(string residueName)
    {
        ArgumentNullException.ThrowIfNull(residueName);

        return ThreeToOne.TryGetValue(residueName.Trim(), out char code) ? code : 'X';
    }

    /// <summary>Трёхбуквенный код по однобуквенному</summary>
    /// <param name="code">Однобуквенный код</param>
    public static string ThreeLetter(char code)
        => OneToThree.TryGetValue(char.ToUpperInvariant(code), out string? name)
            ? name
            : throw new ArgumentException($"Аминокислоты «{code}» нет", nameof(code));

    /// <summary>Гидропатичность остатка по шкале Кайта — Дулиттла</summary>
    /// <param name="code">Однобуквенный код стандартной аминокислоты</param>
    public static double Hydropathy(char code)
        => KyteDoolittle.TryGetValue(char.ToUpperInvariant(code), out double value)
            ? value
            : throw new ArgumentException($"Для «{code}» гидропатичность не определена", nameof(code));

    /// <summary>
    /// Средняя гидропатичность белка (GRAVY): положительная у мембранных и гидрофобных белков
    /// </summary>
    /// <param name="protein">Последовательность в однобуквенном коде</param>
    public static double Gravy(string protein)
    {
        ArgumentException.ThrowIfNullOrEmpty(protein);

        return protein.Average(Hydropathy);
    }

    /// <summary>
    /// Профиль гидропатичности: среднее в скользящем окне
    /// </summary>
    /// <param name="protein">Последовательность</param>
    /// <param name="window">Ширина окна; для поиска трансмембранных участков берут около 19</param>
    /// <remarks>
    /// Значение с номером i относится к окну, начинающемуся с остатка i. Трансмембранная спираль
    /// выглядит как участок около двадцати остатков со средним выше 1,6 — порог самих авторов шкалы.
    /// </remarks>
    public static double[] HydropathyProfile(string protein, int window = 9)
    {
        ArgumentNullException.ThrowIfNull(protein);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(window);

        if (protein.Length < window)
            return [];

        double[] values = protein.Select(Hydropathy).ToArray();
        var profile = new double[values.Length - window + 1];
        double sum = values.Take(window).Sum();

        for (int i = 0; i < profile.Length; i++)
        {
            profile[i] = sum / window;

            if (i + window < values.Length)
                sum += values[i + window] - values[i];
        }

        return profile;
    }
}
