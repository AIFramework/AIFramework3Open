using AI.Biology.Sequences;
using AI.Biology.Structures;
using AI.Geometry.Primitives;
using AI.Solvers.Chem.Dynamics;
using AI.Solvers.Chem.Structures;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Структурная биология проверяется обратимостью и геометрией из учебника: цепь, построенная по
/// углам, даёт те же углы при измерении; идеальная α-спираль имеет расстояние Cα–Cα 3,8 Å и связь
/// O(i)–N(i+4) около 3 Å; DSSP на известных наборах водородных связей даёт известные коды;
/// укладка РНК сверяется с перебором всех допустимых структур.
/// </summary>
public class StructuralBiologyTests
{
    #region Основная цепь

    [Fact]
    public void Backbone_BuiltFromAngles_MeasuresTheSameAngles()
    {
        var rng = new Random(1);
        int n = 25;
        double[] phi = Enumerable.Range(0, n).Select(_ => -180 + (rng.NextDouble() * 360)).ToArray();
        double[] psi = Enumerable.Range(0, n).Select(_ => -180 + (rng.NextDouble() * 360)).ToArray();
        double[] omega = Enumerable.Range(0, n).Select(i => i == 7 ? 2.0 : 175 + (rng.NextDouble() * 10)).ToArray();

        ProteinStructure protein = BackboneBuilder.Build(phi, psi, omega);

        Assert.Equal(n, protein.Count);
        Assert.True(double.IsNaN(protein.Phi(0)));
        Assert.True(double.IsNaN(protein.Psi(n - 1)));

        for (int i = 0; i < n; i++)
        {
            if (i > 0)
                Assert.True(AngleGap(phi[i], protein.Phi(i)) < 1e-6, $"φ{i}: {phi[i]} против {protein.Phi(i)}");

            if (i + 1 < n)
            {
                Assert.True(AngleGap(psi[i], protein.Psi(i)) < 1e-6, $"ψ{i}");
                Assert.True(AngleGap(omega[i], protein.Omega(i)) < 1e-6, $"ω{i}");
            }

            ProteinResidue r = protein.Residues[i];
            Assert.Equal(1.458, protein.Position(r.N).DistanceTo(protein.Position(r.CAlpha)), 9);
            Assert.Equal(1.525, protein.Position(r.CAlpha).DistanceTo(protein.Position(r.C)), 9);
            Assert.Equal(1.231, protein.Position(r.C).DistanceTo(protein.Position(r.O)), 9);
        }
    }

    [Fact]
    public void AlphaHelix_HasTextbookGeometry_AndIsAssignedH()
    {
        (double phi, double psi) = BackboneBuilder.AlphaHelix;
        ProteinStructure helix = BackboneBuilder.Build(20, phi, psi);
        MolecularStructure trace = helix.CAlphaTrace();

        for (int i = 0; i + 1 < 20; i++)
            Assert.Equal(3.80, trace.Atoms[i].Position.DistanceTo(trace.Atoms[i + 1].Position), 0.02);

        // Водородная связь i → i+4 и подъём 1,5 Å на остаток
        for (int i = 0; i + 4 < 20; i++)
        {
            double hydrogenBond = helix.Position(helix.Residues[i].O).DistanceTo(helix.Position(helix.Residues[i + 4].N));
            Assert.InRange(hydrogenBond, 2.7, 3.3);
        }

        Assert.InRange(trace.Atoms[0].Position.DistanceTo(trace.Atoms[19].Position), 27.5, 29.8);

        SecondaryStructureAssignment dssp = SecondaryStructure.Assign(helix);

        Assert.All(Enumerable.Range(2, 16), i => Assert.Equal('H', dssp.Codes[i]));
        Assert.All(dssp.HydrogenBonds.Where(b => b.Donor - b.Acceptor == 4), b => Assert.True(b.Energy < -1.0));
    }

    [Fact]
    public void BetaStrand_IsExtendedAndHasNoHelix()
    {
        (double phi, double psi) = BackboneBuilder.BetaStrand;
        ProteinStructure strand = BackboneBuilder.Build(12, phi, psi);
        MolecularStructure trace = strand.CAlphaTrace();

        for (int i = 0; i + 2 < 12; i++)
            Assert.InRange(trace.Atoms[i].Position.DistanceTo(trace.Atoms[i + 2].Position), 6.0, 7.2);

        string codes = SecondaryStructure.Assign(strand).Codes;

        Assert.DoesNotContain('H', codes);
        Assert.DoesNotContain('E', codes);

        (double hPhi, double hPsi) = BackboneBuilder.AlphaHelix;
        Assert.True(strand.RadiusOfGyration() > BackboneBuilder.Build(12, hPhi, hPsi).RadiusOfGyration() * 1.5);
    }

    #endregion

    #region DSSP на известных связях

    [Theory]
    [InlineData("helix", "-HHHHHHHHHH-")]
    [InlineData("helix310", "-GGGGGGGG-")]
    [InlineData("turn", "---TTT----")]
    [InlineData("antiparallel", "--EEE------EEE--")]
    [InlineData("parallel", "--EEE-----EEE--")]
    [InlineData("bridge", "---B------B---")]
    public void Dssp_KnownHydrogenBondPatterns(string pattern, string expected)
    {
        (int Acceptor, int Donor)[] bonds = pattern switch
        {
            "helix" => Enumerable.Range(0, 8).Select(k => (k, k + 4)).ToArray(),
            "helix310" => Enumerable.Range(0, 7).Select(k => (k, k + 3)).ToArray(),
            "turn" => [(2, 6)],
            // Антипараллельные цепи 2–4 и 11–13: пары 2–13 и 4–11 связаны в обе стороны
            "antiparallel" => [(2, 13), (13, 2), (4, 11), (11, 4)],
            // Параллельные цепи 2–4 и 10–12: связи идут «лесенкой» 1→10→3→12→5
            "parallel" => [(1, 10), (10, 3), (3, 12), (12, 5)],
            "bridge" => [(3, 10), (10, 3)],
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

        Assert.Equal(expected, SecondaryStructure.Assign(expected.Length, bonds).Codes);
    }

    [Fact]
    public void Dssp_ChainBreakCutsTheHelix()
    {
        (int, int)[] bonds = Enumerable.Range(0, 8).Select(k => (k, k + 4)).ToArray();
        bool[] linked = Enumerable.Repeat(true, 12).ToArray();
        linked[5] = false;
        linked[11] = false;

        // Через разрыв между 5-м и 6-м остатками поворотов нет: остаются повороты в 0, 1, 6 и 7,
        // и спираль распадается на два участка по четыре остатка
        Assert.Equal("-HHHH--HHHH-", SecondaryStructure.Assign(12, bonds, linked).Codes);
    }

    #endregion

    #region PDB

    [Fact]
    public void Pdb_IsReadIntoResidues_FirstModelAndFirstAlternateOnly()
    {
        const string sequence = "MKTAYIAKQRQI";
        (double phi, double psi) = BackboneBuilder.AlphaHelix;
        ProteinStructure built = BackboneBuilder.Build(
            Enumerable.Repeat(phi, sequence.Length).ToArray(),
            Enumerable.Repeat(psi, sequence.Length).ToArray(),
            sequence: sequence);

        var lines = new List<string> { "HEADER    TEST", "MODEL        1" };
        int serial = 1;

        foreach (AtomSite atom in built.Atoms.Atoms)
        {
            bool alternate = atom.ResidueNumber == 5 && atom.Label == "CA";
            lines.Add(PdbLine("ATOM", serial++, atom.Label, alternate ? 'A' : ' ', atom.ResidueName, 'A', atom.ResidueNumber, atom.Position, atom.Element));

            if (alternate)
                lines.Add(PdbLine("ATOM", serial++, atom.Label, 'B', atom.ResidueName, 'A', atom.ResidueNumber, atom.Position + new Vector3(5, 0, 0), atom.Element));
        }

        lines.Add(PdbLine("HETATM", serial++, "O", ' ', "HOH", 'A', 101, new Vector3(20, 20, 20), "O"));
        lines.Add("ENDMDL");
        lines.Add("MODEL        2");
        lines.Add(PdbLine("ATOM", serial, "N", ' ', "MET", 'A', 1, new Vector3(50, 50, 50), "N"));
        lines.Add("ENDMDL");

        ProteinStructure read = ProteinStructure.ReadPdb(string.Join("\n", lines));

        Assert.Equal(sequence.Length * 4 + 1, read.Atoms.Count);
        Assert.Equal(sequence, read.Sequence());
        Assert.Equal(['A'], read.Chains);
        Assert.True(read.Atoms.Atoms[^1].IsHetero);

        for (int i = 1; i + 1 < sequence.Length; i++)
        {
            Assert.True(AngleGap(phi, read.Phi(i)) < 0.5);
            Assert.True(AngleGap(psi, read.Psi(i)) < 0.5);
        }

        Assert.Equal('H', SecondaryStructure.Assign(read).Codes[5]);
    }

    #endregion

    #region Сравнение структур

    [Fact]
    public void TmScore_OfRigidCopy_IsOne()
    {
        (double phi, double psi) = BackboneBuilder.AlphaHelix;
        ProteinStructure helix = BackboneBuilder.Build(30, phi, psi);
        ProteinStructure moved = Moved(helix, p => Rotate(p, 0.7, 1.9) + new Vector3(12, -4, 30));

        TmScoreResult tm = StructureComparison.TmScore(moved, helix);

        Assert.Equal(1.0, tm.Score, 9);
        Assert.Equal(0.0, tm.Rmsd, 6);
        Assert.Equal(0.0, StructureComparison.Rmsd(moved, helix), 6);
        Assert.Equal(30, tm.ResiduesWithinD0);
    }

    [Fact]
    public void TmScore_FindsTheConservedHalf_WhereFullSuperpositionFails()
    {
        (double phi, double psi) = BackboneBuilder.AlphaHelix;
        ProteinStructure reference = BackboneBuilder.Build(40, phi, psi);

        // Вторая половина сдвинута как жёсткое тело: общая укладка у первой половины
        ProteinStructure model = Moved(reference, p => Rotate(p, 1.1, 0.4) + new Vector3(15, 8, -6), number => number > 20);

        TmScoreResult tm = StructureComparison.TmScore(model, reference);
        double baseline = TmAtFullSuperposition(model, reference, tm.D0);

        Assert.True(tm.Score >= 0.49, $"TM {tm.Score}");
        Assert.True(tm.Score > baseline + 0.05, $"поиск {tm.Score} не лучше совмещения по всем {baseline}");
        Assert.True(StructureComparison.Rmsd(model, reference) > 3);
        Assert.InRange(tm.ResiduesWithinD0, 18, 24);
    }

    [Fact]
    public void ContactMap_IsSymmetric_AndHelixContactsAreLocal()
    {
        (double phi, double psi) = BackboneBuilder.AlphaHelix;
        ProteinStructure helix = BackboneBuilder.Build(24, phi, psi);
        bool[,] map = helix.ContactMap();

        for (int i = 0; i < 24; i++)
            for (int j = 0; j < 24; j++)
            {
                Assert.Equal(map[i, j], map[j, i]);

                // В спирали на 8 Å дотягиваются только соседи по цепи в пределах витка с небольшим
                if (Math.Abs(i - j) > 6)
                    Assert.False(map[i, j]);
            }

        Assert.InRange(helix.RelativeContactOrder(), 0.0, 0.25);
    }

    #endregion

    #region РНК

    [Fact]
    public void Nussinov_MatchesExhaustiveEnumeration()
    {
        var rng = new Random(71);

        for (int trial = 0; trial < 60; trial++)
        {
            string rna = new(Enumerable.Range(0, 4 + rng.Next(9)).Select(_ => "ACGU"[rng.Next(4)]).ToArray());
            bool wobble = trial % 2 == 0;
            RnaSecondaryStructure folded = RnaFolding.Nussinov(rna, 3, wobble);

            Assert.True(RnaFolding.IsValid(rna, folded.Pairs, 3, wobble), $"{rna}: {folded.DotBracket}");
            Assert.Equal(BruteMaximumPairs(rna, 0, rna.Length - 1, wobble, new Dictionary<(int, int), int>()), folded.PairCount);
        }
    }

    [Fact]
    public void Nussinov_FoldsKnownHairpin()
    {
        Assert.Equal("(((...)))", RnaFolding.Nussinov("GGGAAAUCC").DotBracket);
        Assert.Equal(2, RnaFolding.Nussinov("GGGAAAUCC", allowWobble: false).PairCount);
        Assert.Equal(3, RnaFolding.Nussinov("gggaaatcc").PairCount);
    }

    [Fact]
    public void DotBracket_RoundTripsAndRejectsPseudoknots()
    {
        const string structure = "((..((...))..))...";
        IReadOnlyList<(int Open, int Close)> pairs = RnaFolding.ParseDotBracket(structure);

        Assert.Equal(structure, RnaFolding.ToDotBracket(structure.Length, pairs));
        Assert.Equal(4, pairs.Count);
        Assert.Equal(1, RnaFolding.BasePairDistance("((...))", "(.....)"));
        Assert.Equal(3, RnaFolding.BasePairDistance("((...))", "..(.)..")); // общих пар нет: 2 + 1

        _ = Assert.Throws<FormatException>(() => RnaFolding.ParseDotBracket("(()"));
        _ = Assert.Throws<FormatException>(() => RnaFolding.ParseDotBracket("([)]"));
        _ = Assert.Throws<ArgumentException>(() => RnaFolding.ToDotBracket(8, [(0, 4), (2, 6)]));
    }

    #endregion

    #region Инструменты

    private static double AngleGap(double a, double b)
    {
        double d = (a - b) % 360;

        if (d > 180)
            d -= 360;

        if (d < -180)
            d += 360;

        return Math.Abs(d);
    }

    private static Vector3 Rotate(Vector3 p, double aboutZ, double aboutX)
    {
        var z = new Vector3((p.X * Math.Cos(aboutZ)) - (p.Y * Math.Sin(aboutZ)), (p.X * Math.Sin(aboutZ)) + (p.Y * Math.Cos(aboutZ)), p.Z);

        return new Vector3(z.X, (z.Y * Math.Cos(aboutX)) - (z.Z * Math.Sin(aboutX)), (z.Y * Math.Sin(aboutX)) + (z.Z * Math.Cos(aboutX)));
    }

    private static ProteinStructure Moved(ProteinStructure protein, Func<Vector3, Vector3> transform, Func<int, bool>? which = null)
    {
        var atoms = new MolecularStructure();

        foreach (AtomSite atom in protein.Atoms.Atoms)
            atoms.Add(which is null || which(atom.ResidueNumber) ? atom.WithPosition(transform(atom.Position)) : atom);

        return ProteinStructure.FromAtoms(atoms);
    }

    private static double TmAtFullSuperposition(ProteinStructure model, ProteinStructure reference, double d0)
    {
        MolecularStructure target = reference.CAlphaTrace();
        MolecularStructure aligned = StructureAlignment.Align(model.CAlphaTrace(), target).Aligned;

        return Enumerable.Range(0, target.Count)
            .Select(k => aligned.Atoms[k].Position.DistanceTo(target.Atoms[k].Position))
            .Sum(d => 1 / (1 + ((d / d0) * (d / d0)))) / target.Count;
    }

    private static string PdbLine(string record, int serial, string atom, char alternate, string residue, char chain, int number, Vector3 p, string element)
        => FormattableString.Invariant(
            $"{record,-6}{serial,5} {(" " + atom).PadRight(4)}{alternate}{residue,3} {chain}{number,4}    {p.X,8:F3}{p.Y,8:F3}{p.Z,8:F3}{1.0,6:F2}{20.0,6:F2}          {element,2}");

    /// <summary>Перебор: основание i либо свободно, либо спарено с каким-то k — без общих формул Нуссинова</summary>
    private static int BruteMaximumPairs(string rna, int i, int j, bool wobble, Dictionary<(int, int), int> memo)
    {
        if (j - i <= 3)
            return 0;

        if (memo.TryGetValue((i, j), out int known))
            return known;

        int best = BruteMaximumPairs(rna, i + 1, j, wobble, memo);

        for (int k = i + 4; k <= j; k++)
        {
            if (RnaFolding.CanPair(rna[i], rna[k], wobble))
                best = Math.Max(best, 1 + BruteMaximumPairs(rna, i + 1, k - 1, wobble, memo) + BruteMaximumPairs(rna, k + 1, j, wobble, memo));
        }

        memo[(i, j)] = best;

        return best;
    }

    #endregion
}
