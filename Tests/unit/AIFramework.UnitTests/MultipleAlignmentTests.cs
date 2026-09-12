using AI.Biology.Phylogeny;
using AI.Biology.Sequences;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Множественное выравнивание проверяется тремя независимыми способами: для двух
/// последовательностей оно обязано совпасть с попарным оптимумом, для трёх — не превзойти
/// точный оптимум трёхмерного программирования, а на данных, смоделированных со вставками и
/// делециями, — восстановить истинное выравнивание, известное по построению.
/// </summary>
public class MultipleAlignmentTests
{
    private const string FamilyTree =
        "(((A:0.05,B:0.08):0.04,(C:0.06,D:0.05):0.05):0.03,((E:0.07,F:0.04):0.05,(G:0.06,H:0.08):0.04):0.03);";

    #region Точные случаи

    [Fact]
    public void TwoSequences_MatchPairwiseOptimum()
    {
        var rng = new Random(1);
        ScoringScheme[] schemes = [ScoringScheme.Nucleotide, new(2, -3, -5, -1), new(1, -4, -1, -1)];

        for (int trial = 0; trial < 60; trial++)
        {
            string a = RandomText(rng, "ACGT", 1 + rng.Next(12));
            string b = RandomText(rng, "ACGT", 1 + rng.Next(12));
            ScoringScheme scheme = schemes[trial % schemes.Length];

            MultipleAlignment msa = ProgressiveAlignment.Align(["a", "b"], [a, b], scheme);

            AssertConsistent(msa, [a, b]);
            Assert.Equal(Alignment.Global(a, b, scheme).Score, msa.SumOfPairsScore!.Value, 9);
        }
    }

    [Fact]
    public void ThreeSequences_NeverBeatTheExactOptimum_AndReachItWhenObvious()
    {
        ScoringScheme linear = ScoringScheme.Linear(2, -1, -2);
        var rng = new Random(3);

        for (int trial = 0; trial < 40; trial++)
        {
            string[] triple = Enumerable.Range(0, 3).Select(_ => RandomText(rng, "ACGT", 1 + rng.Next(6))).ToArray();
            double score = ProgressiveAlignment.Align(["a", "b", "c"], triple, linear).SumOfPairsScore!.Value;

            Assert.True(score <= ExactThree(triple[0], triple[1], triple[2], linear) + 1e-9);
        }

        // Одна последовательность и две с разными одиночными делециями
        string[] easy = ["ACGTTGCA", "ACGTGCA", "ACTTGCA"];

        Assert.Equal(ExactThree(easy[0], easy[1], easy[2], linear),
            ProgressiveAlignment.Align(["a", "b", "c"], easy, linear).SumOfPairsScore!.Value, 9);
    }

    [Fact]
    public void Rows_ReproduceInputs_AndScoreIsRecomputedIndependently()
    {
        var rng = new Random(2);

        for (int trial = 0; trial < 25; trial++)
        {
            string root = RandomText(rng, "ACGT", 10 + rng.Next(20));
            string[] sequences = Enumerable.Range(0, 2 + rng.Next(6)).Select(_ => Mutate(root, rng)).ToArray();
            string[] labels = sequences.Select((_, i) => $"s{i}").ToArray();

            MultipleAlignment msa = ProgressiveAlignment.Align(labels, sequences);

            AssertConsistent(msa, sequences);
            Assert.Equal(IndependentSumOfPairs(msa.Rows, ScoringScheme.Nucleotide), msa.SumOfPairsScore!.Value, 9);
            Assert.Equal(msa.SumOfPairs(), msa.SumOfPairsScore.Value, 9);
            Assert.True(msa.SumOfPairsScore >= msa.ProgressiveScore - 1e-9);
        }
    }

    [Fact]
    public void Refinement_NeverLowersSumOfPairs()
    {
        for (int seed = 0; seed < 6; seed++)
        {
            MultipleAlignment truth = SequenceEvolution.SimulateAlignment(
                PhylogeneticTree.ParseNewick(FamilyTree), 80, new Random(20 + seed), indelRate: 0.2);
            string[] sequences = Ungapped(truth);

            MultipleAlignment plain = ProgressiveAlignment.Align(truth.Labels, sequences, options: new() { RefinementPasses = 0 });
            MultipleAlignment refined = ProgressiveAlignment.Align(truth.Labels, sequences, options: new() { RefinementPasses = 4 });

            Assert.Equal(plain.SumOfPairsScore!.Value, refined.ProgressiveScore!.Value, 9);
            Assert.True(refined.SumOfPairsScore >= plain.SumOfPairsScore - 1e-9);
            AssertConsistent(refined, sequences);
        }
    }

    #endregion

    #region Смоделированные семейства

    [Fact]
    public void SimulatedAlignment_IsATrueAlignment()
    {
        PhylogeneticTree tree = PhylogeneticTree.ParseNewick(FamilyTree);
        MultipleAlignment truth = SequenceEvolution.SimulateAlignment(tree, 120, new Random(10), indelRate: 0.1);

        Assert.Equal(tree.LeafNames, truth.Labels);
        Assert.All(Enumerable.Range(0, truth.Length), c => Assert.Contains(truth.Rows, r => r[c] != '-'));
        Assert.True(truth.GapFraction > 0);

        MultipleAlignment noIndels = SequenceEvolution.SimulateAlignment(tree, 120, new Random(10), indelRate: 0);

        Assert.Equal(120, noIndels.Length);
        Assert.Equal(0, noIndels.GapFraction);
    }

    [Fact]
    public void SimulatedFamily_IsRecoveredAccurately()
    {
        MultipleAlignment truth = SequenceEvolution.SimulateAlignment(
            PhylogeneticTree.ParseNewick(FamilyTree), 150, new Random(11), indelRate: 0.05);
        string[] sequences = Ungapped(truth);

        AlignmentAccuracy accuracy = ProgressiveAlignment.Align(truth.Labels, sequences).CompareWith(truth);
        AlignmentAccuracy baseline = FlushLeft(truth.Labels, sequences).CompareWith(truth);

        Assert.True(accuracy.PairScore >= 0.95, $"пары {accuracy.PairScore}, столбцы {accuracy.ColumnScore}");
        Assert.True(accuracy.ColumnScore >= 0.85, $"столбцы {accuracy.ColumnScore}");
        Assert.True(accuracy.PairScore > baseline.PairScore + 0.3, $"без выравнивания {baseline.PairScore}");
    }

    [Theory]
    [InlineData(GuideTreeMethod.Upgma)]
    [InlineData(GuideTreeMethod.NeighborJoining)]
    public void GuideTree_JoinsCloseRelativesFirst(GuideTreeMethod method)
    {
        PhylogeneticTree truth = PhylogeneticTree.ParseNewick("((A:0.02,B:0.03):0.3,(C:0.03,D:0.02):0.3);");
        MultipleAlignment simulated = SequenceEvolution.SimulateAlignment(truth, 200, new Random(12), indelRate: 0.02);

        MultipleAlignment msa = ProgressiveAlignment.Align(simulated.Labels, Ungapped(simulated), options: new() { GuideTree = method });

        Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(truth, msa.GuideTree!));
    }

    [Fact]
    public void AlignmentThenTree_RecoversSimulatedPhylogeny()
    {
        PhylogeneticTree truth = PhylogeneticTree.ParseNewick(FamilyTree);
        MultipleAlignment simulated = SequenceEvolution.SimulateAlignment(truth, 800, new Random(13), indelRate: 0.03);

        MultipleAlignment msa = ProgressiveAlignment.Align(simulated.Labels, Ungapped(simulated));
        PhylogeneticTree tree = TreeBuilder.NeighborJoining(msa.Distances(DistanceModel.JukesCantor));

        Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(truth, tree));
    }

    #endregion

    #region Белки, меры и форматы

    [Fact]
    public void ProteinFamily_KeepsTheConservedTryptophanInOneColumn()
    {
        string[] proteins =
        [
            "MKTAYWIAKQRQISFVKSHFSRQ",
            "MKTAYWIAKQRQISFVKSHFSRQLEE",
            "MKAYWIAKQRQLSFVKSHFSR",
            "MSKTAFWVAKQRQISFVKSHFSRQ"
        ];

        MultipleAlignment msa = ProgressiveAlignment.Align(["p1", "p2", "p3", "p4"], proteins, SubstitutionMatrix.Blosum62);

        AssertConsistent(msa, proteins);
        Assert.Contains(Enumerable.Range(0, msa.Length), c => msa.Rows.All(r => r[c] == 'W'));
        Assert.True(msa.ConservedColumns >= 15);

        _ = Assert.Throws<ArgumentException>(() => ProgressiveAlignment.Align(["a", "b"], ["MKB", "MK"], SubstitutionMatrix.Blosum62));
    }

    [Fact]
    public void Accuracy_IsOneAgainstItself_AndCountsMisalignedPairs()
    {
        var reference = new MultipleAlignment(["x", "y"], ["ACGT-", "AC-TT"]);
        AlignmentAccuracy self = reference.CompareWith(reference);

        Assert.Equal(1, self.PairScore);
        Assert.Equal(1, self.ColumnScore);
        Assert.Equal(3, self.ReferencePairs);

        // Порядок строк другой, T последовательности y стоит против G: из трёх пар эталона верны две
        var other = new MultipleAlignment(["y", "x"], ["ACTT", "ACGT"]);
        AlignmentAccuracy accuracy = other.CompareWith(reference);

        Assert.Equal(2.0 / 3, accuracy.PairScore, 12);
        Assert.Equal(2.0 / 3, accuracy.ColumnScore, 12);

        _ = Assert.Throws<ArgumentException>(() => new MultipleAlignment(["x", "y"], ["ACGA", "ACTT"]).CompareWith(reference));
    }

    [Fact]
    public void Fasta_RoundTrips()
    {
        var msa = new MultipleAlignment(["s1", "вид 2"], ["AC-GTAAC", "ACTGT-AC"]);
        MultipleAlignment parsed = MultipleAlignment.ParseFasta(msa.ToFasta(lineWidth: 3));

        Assert.Equal(msa.Labels, parsed.Labels);
        Assert.Equal(msa.Rows, parsed.Rows);

        _ = Assert.Throws<FormatException>(() => MultipleAlignment.ParseFasta("ACGT"));
        _ = Assert.Throws<FormatException>(() => MultipleAlignment.ParseFasta(">a\nACGT\n>b\nACG"));
    }

    [Fact]
    public void Input_IsValidated_AndSingleSequenceIsTrivial()
    {
        _ = Assert.Throws<ArgumentException>(() => ProgressiveAlignment.Align(["a", "b"], ["AC-G", "ACG"]));
        _ = Assert.Throws<ArgumentException>(() => ProgressiveAlignment.Align(["a", "a"], ["ACG", "ACG"]));
        _ = Assert.Throws<ArgumentException>(() => ProgressiveAlignment.Align(["a"], ["ACG", "ACG"]));

        MultipleAlignment single = ProgressiveAlignment.Align(["a"], ["acgt"]);

        Assert.Equal(["ACGT"], single.Rows);
        Assert.Contains(ProgressiveAlignment.Align(["a", "b", "c"], ["ACGT", "ACT", "AGT"]).Interpret().Warnings,
            w => w.Contains("прогрессивное", StringComparison.Ordinal));
    }

    #endregion

    #region Инструменты

    private static string RandomText(Random rng, string alphabet, int length)
        => new(Enumerable.Range(0, length).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());

    private static string Mutate(string root, Random rng)
    {
        var letters = new List<char>(root);

        for (int k = 0; k < 3; k++)
        {
            int position = rng.Next(letters.Count);

            switch (rng.Next(3))
            {
                case 0:
                    letters[position] = "ACGT"[rng.Next(4)];
                    break;
                case 1 when letters.Count > 1:
                    letters.RemoveAt(position);
                    break;
                default:
                    letters.Insert(position, "ACGT"[rng.Next(4)]);
                    break;
            }
        }

        return new string(letters.ToArray());
    }

    private static string[] Ungapped(MultipleAlignment alignment)
        => Enumerable.Range(0, alignment.Count).Select(alignment.Sequence).ToArray();

    private static MultipleAlignment FlushLeft(IReadOnlyList<string> labels, string[] sequences)
    {
        int length = sequences.Max(s => s.Length);

        return new MultipleAlignment(labels, sequences.Select(s => s.PadRight(length, '-')).ToArray());
    }

    private static void AssertConsistent(MultipleAlignment msa, IReadOnlyList<string> inputs)
    {
        Assert.Equal(inputs.Count, msa.Count);
        Assert.All(msa.Rows, r => Assert.Equal(msa.Length, r.Length));

        for (int i = 0; i < inputs.Count; i++)
            Assert.Equal(inputs[i].ToUpperInvariant(), msa.Sequence(i));

        Assert.All(Enumerable.Range(0, msa.Length), c => Assert.Contains(msa.Rows, r => r[c] != '-'));
    }

    /// <summary>Сумма по парам: каждая пара проецируется, пропуски считаются сплошными участками</summary>
    private static double IndependentSumOfPairs(IReadOnlyList<string> rows, ScoringScheme s)
    {
        double total = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            for (int j = i + 1; j < rows.Count; j++)
            {
                var keep = Enumerable.Range(0, rows[i].Length).Where(c => rows[i][c] != '-' || rows[j][c] != '-').ToArray();
                string top = new(keep.Select(c => rows[i][c]).ToArray());
                string bottom = new(keep.Select(c => rows[j][c]).ToArray());

                for (int c = 0; c < top.Length; c++)
                {
                    if (top[c] != '-' && bottom[c] != '-')
                        total += top[c] == bottom[c] ? s.Match : s.Mismatch;
                }

                total += RunCost(top, s) + RunCost(bottom, s);
            }
        }

        return total;
    }

    private static double RunCost(string row, ScoringScheme s)
        => System.Text.RegularExpressions.Regex.Matches(row, "-+").Sum(m => s.GapOpen + ((m.Length - 1) * s.GapExtend));

    /// <summary>Точный оптимум суммы по парам для трёх строк при линейном штрафе — трёхмерная таблица</summary>
    private static double ExactThree(string a, string b, string c, ScoringScheme s)
    {
        var best = new double[a.Length + 1, b.Length + 1, c.Length + 1];

        double Pair(char x, char y) => x == '-' && y == '-' ? 0 : x == '-' || y == '-' ? s.GapOpen : x == y ? s.Match : s.Mismatch;

        for (int i = 0; i <= a.Length; i++)
            for (int j = 0; j <= b.Length; j++)
                for (int k = 0; k <= c.Length; k++)
                {
                    if (i + j + k == 0)
                        continue;

                    double value = double.NegativeInfinity;

                    for (int mask = 1; mask < 8; mask++)
                    {
                        int di = mask & 1, dj = (mask >> 1) & 1, dk = (mask >> 2) & 1;

                        if (i < di || j < dj || k < dk)
                            continue;

                        char x = di == 1 ? a[i - 1] : '-';
                        char y = dj == 1 ? b[j - 1] : '-';
                        char z = dk == 1 ? c[k - 1] : '-';

                        value = Math.Max(value, best[i - di, j - dj, k - dk] + Pair(x, y) + Pair(x, z) + Pair(y, z));
                    }

                    best[i, j, k] = value;
                }

        return best[a.Length, b.Length, c.Length];
    }

    #endregion
}
