using AI.Biology.Sequences;
using AI.Geometry.Primitives;
using AI.Solvers.Chem.Structures;

namespace AI.Biology.Structures;

/// <summary>
/// Построение основной цепи белка по двугранным углам с идеальной геометрией.
/// </summary>
/// <remarks>
/// <para>
/// Длины связей и валентные углы взяты по Энгху и Хуберу (1991), атомы ставятся методом NeRF
/// (Парсонс и др., 2005): каждый следующий атом задаётся связью, углом и двугранным углом
/// относительно трёх предыдущих. Это обратная задача к измерению углов в
/// <see cref="ProteinStructure"/>, и на ней проверяется, что оба направления согласованы.
/// </para>
/// <para>
/// Боковые цепи не строятся: только N, Cα, C и O. Для анализа вторичной структуры этого
/// достаточно, для расчёта энергии или контактов боковых цепей — нет.
/// </para>
/// </remarks>
public static class BackboneBuilder
{
    /// <summary>Длина связи N–Cα, ангстремы</summary>
    public const double NitrogenAlpha = 1.458;

    /// <summary>Длина связи Cα–C, ангстремы</summary>
    public const double AlphaCarbon = 1.525;

    /// <summary>Длина пептидной связи C–N, ангстремы</summary>
    public const double Peptide = 1.329;

    /// <summary>Длина связи C=O, ангстремы</summary>
    public const double CarbonOxygen = 1.231;

    /// <summary>Валентный угол N–Cα–C, градусы</summary>
    public const double AngleNitrogenAlphaCarbon = 111.2;

    /// <summary>Валентный угол Cα–C–N, градусы</summary>
    public const double AngleAlphaCarbonNitrogen = 116.2;

    /// <summary>Валентный угол C–N–Cα, градусы</summary>
    public const double AngleCarbonNitrogenAlpha = 121.7;

    /// <summary>Валентный угол Cα–C–O, градусы</summary>
    public const double AngleAlphaCarbonOxygen = 120.1;

    /// <summary>Углы правой α-спирали (φ, ψ), градусы</summary>
    public static (double Phi, double Psi) AlphaHelix => (-57.8, -47.0);

    /// <summary>Углы вытянутой β-цепи (φ, ψ), градусы</summary>
    public static (double Phi, double Psi) BetaStrand => (-120.0, 130.0);

    /// <summary>
    /// Строит основную цепь по двугранным углам каждого остатка
    /// </summary>
    /// <param name="phi">Углы φ; первый не используется — у первого остатка φ не определён</param>
    /// <param name="psi">Углы ψ; последний задаёт положение кислорода последнего остатка</param>
    /// <param name="omega">Углы ω между остатком и следующим; по умолчанию 180° (транс)</param>
    /// <param name="sequence">Последовательность в однобуквенном коде; по умолчанию аланины</param>
    public static ProteinStructure Build(
        IReadOnlyList<double> phi, IReadOnlyList<double> psi,
        IReadOnlyList<double>? omega = null, string? sequence = null)
    {
        ArgumentNullException.ThrowIfNull(phi);
        ArgumentNullException.ThrowIfNull(psi);

        int n = phi.Count;

        if (n == 0 || psi.Count != n)
            throw new ArgumentException("Углов φ и ψ должно быть поровну, не меньше одного", nameof(psi));

        if (omega is not null && omega.Count != n)
            throw new ArgumentException("Углов ω должно быть столько же, сколько остатков", nameof(omega));

        if (sequence is not null && sequence.Length != n)
            throw new ArgumentException("Длина последовательности должна совпадать с числом остатков", nameof(sequence));

        var nitrogen = new Vector3[n];
        var alpha = new Vector3[n];
        var carbon = new Vector3[n];
        var oxygen = new Vector3[n];

        nitrogen[0] = Vector3.Zero;
        alpha[0] = new Vector3(NitrogenAlpha, 0, 0);

        double inner = (180 - AngleNitrogenAlphaCarbon) * Math.PI / 180;
        carbon[0] = alpha[0] + (new Vector3(Math.Cos(inner), Math.Sin(inner), 0) * AlphaCarbon);

        for (int i = 0; i + 1 < n; i++)
        {
            nitrogen[i + 1] = Place(nitrogen[i], alpha[i], carbon[i], Peptide, AngleAlphaCarbonNitrogen, psi[i]);
            alpha[i + 1] = Place(alpha[i], carbon[i], nitrogen[i + 1], NitrogenAlpha, AngleCarbonNitrogenAlpha, omega?[i] ?? 180);
            carbon[i + 1] = Place(carbon[i], nitrogen[i + 1], alpha[i + 1], AlphaCarbon, AngleNitrogenAlphaCarbon, phi[i + 1]);
        }

        // Кислород лежит в плоскости пептида напротив следующего азота: N–Cα–C–O = ψ + 180°
        for (int i = 0; i < n; i++)
            oxygen[i] = Place(nitrogen[i], alpha[i], carbon[i], CarbonOxygen, AngleAlphaCarbonOxygen, psi[i] + 180);

        var atoms = new MolecularStructure { Name = "идеальная основная цепь" };

        for (int i = 0; i < n; i++)
        {
            string residue = sequence is null ? "ALA" : AminoAcids.ThreeLetter(sequence[i]);

            atoms.Add(Atom("N", "N", nitrogen[i], residue, i));
            atoms.Add(Atom("C", "CA", alpha[i], residue, i));
            atoms.Add(Atom("C", "C", carbon[i], residue, i));
            atoms.Add(Atom("O", "O", oxygen[i], residue, i));
        }

        return ProteinStructure.FromAtoms(atoms);
    }

    /// <summary>Строит цепь с одинаковыми углами у всех остатков</summary>
    /// <param name="residues">Число остатков</param>
    /// <param name="phi">Угол φ, градусы</param>
    /// <param name="psi">Угол ψ, градусы</param>
    /// <param name="omega">Угол ω, градусы</param>
    public static ProteinStructure Build(int residues, double phi, double psi, double omega = 180)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(residues);

        return Build(
            Enumerable.Repeat(phi, residues).ToArray(),
            Enumerable.Repeat(psi, residues).ToArray(),
            Enumerable.Repeat(omega, residues).ToArray());
    }

    /// <summary>
    /// Ставит атом D по трём предыдущим A, B, C: длина связи C–D, угол B–C–D и двугранный угол A–B–C–D
    /// </summary>
    internal static Vector3 Place(Vector3 a, Vector3 b, Vector3 c, double bond, double angle, double torsion)
    {
        Vector3 bc = (c - b).Normalized;
        Vector3 normal = (b - a).Cross(bc).Normalized;
        Vector3 inPlane = normal.Cross(bc);

        double theta = angle * Math.PI / 180;
        double tau = torsion * Math.PI / 180;

        return c
            + (bc * (-bond * Math.Cos(theta)))
            + (inPlane * (bond * Math.Sin(theta) * Math.Cos(tau)))
            + (normal * (bond * Math.Sin(theta) * Math.Sin(tau)));
    }

    private static AtomSite Atom(string element, string label, Vector3 position, string residue, int index) => new()
    {
        Element = element,
        Label = label,
        Position = position,
        ResidueName = residue,
        ChainId = 'A',
        ResidueNumber = index + 1
    };
}
