using AI.Biology.Ecology;
using AI.Biology.Genetics;
using AI.Biology.Populations;
using AI.Biology.Sequences;
using AI.Insights;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Биология проверяется тождествами и задачами с известным ответом: комплементарность
/// обратима, генетический код переводит известный ген в известный белок, доли в моделях
/// популяций сохраняются, индексы разнообразия достигают своих границ.
/// </summary>
public class BiologyTests
{
    #region Последовательности

    [Fact]
    public void Sequence_ReverseComplement_IsItsOwnInverse()
    {
        var dna = new NucleotideSequence("ATGCGTACGTTAGC");

        Assert.Equal(dna.Letters, dna.ReverseComplement().ReverseComplement().Letters);
    }

    [Fact]
    public void Sequence_ReverseComplement_MatchesKnownAnswer()
    {
        var dna = new NucleotideSequence("AAAACCCGGT");

        Assert.Equal("ACCGGGTTTT", dna.ReverseComplement().Letters);
    }

    [Fact]
    public void Sequence_GcContent_CountsBothStrandsEqually()
    {
        var dna = new NucleotideSequence("GGCCATAT");

        // Доля G+C одинакова у обеих цепей: это свойство комплементарности
        Assert.Equal(0.5, dna.GcContent, tolerance: 1e-12);
        Assert.Equal(dna.GcContent, dna.ReverseComplement().GcContent, tolerance: 1e-12);
    }

    [Fact]
    public void Sequence_Transcription_ReplacesThymineWithUracil()
    {
        var dna = new NucleotideSequence("ATGGCCATTGTAATG");
        NucleotideSequence rna = dna.Transcribe();

        Assert.Equal("AUGGCCAUUGUAAUG", rna.Letters);
        Assert.Equal(NucleicAcid.Rna, rna.Kind);
        Assert.Equal(dna.Letters, rna.ReverseTranscribe().Letters);
    }

    [Fact]
    public void Sequence_RejectsAmbiguousLetters()
    {
        _ = Assert.Throws<ArgumentException>(() => new NucleotideSequence("ATGCN"));
        _ = Assert.Throws<ArgumentException>(() => new NucleotideSequence("AUGC", NucleicAcid.Dna));
    }

    [Fact]
    public void Sequence_KmerCounts_SumToWindowCount()
    {
        var dna = new NucleotideSequence("AAAAA");

        IReadOnlyDictionary<string, int> counts = dna.KmerCounts(2);

        Assert.Single(counts);
        Assert.Equal(4, counts["AA"]);
    }

    [Fact]
    public void GeneticCode_TranslatesKnownCodons()
    {
        Assert.Equal('M', GeneticCode.Translate("AUG"));
        Assert.Equal('W', GeneticCode.Translate("UGG"));
        Assert.Equal('*', GeneticCode.Translate("UAA"));
        Assert.Equal('F', GeneticCode.Translate("UUU"));

        // Кодон принимается и в записи ДНК
        Assert.Equal('M', GeneticCode.Translate("ATG"));
    }

    [Fact]
    public void GeneticCode_IsDegenerate()
    {
        // Лейцин и аргинин кодируются шестью кодонами, метионин и триптофан — одним
        Assert.Equal(6, GeneticCode.Degeneracy('L'));
        Assert.Equal(6, GeneticCode.Degeneracy('R'));
        Assert.Equal(1, GeneticCode.Degeneracy('M'));
        Assert.Equal(1, GeneticCode.Degeneracy('W'));
        Assert.Equal(3, GeneticCode.Degeneracy('*'));
    }

    [Fact]
    public void GeneticCode_Translation_MatchesKnownPeptide()
    {
        // AUG GCC AUU GUA AUG GGC CGC UGA → MAIVMGR со стопом
        var rna = new NucleotideSequence("AUGGCCAUUGUAAUGGGCCGCUGA", NucleicAcid.Rna);

        Assert.Equal("MAIVMGR", GeneticCode.Translate(rna));
    }

    [Fact]
    public void GeneticCode_FindsOpenReadingFrame()
    {
        var dna = new NucleotideSequence("TTATGGCCATTGTAATGGGCCGCTGATT");

        IReadOnlyList<OpenReadingFrame> frames = GeneticCode.FindOpenReadingFrames(dna, minimumLength: 5);

        Assert.NotEmpty(frames);
        Assert.Contains(frames, f => f.Protein == "MAIVMGR");
    }

    #endregion

    #region Выравнивание

    [Fact]
    public void Alignment_IdenticalSequences_GiveFullIdentity()
    {
        AlignmentResult result = Alignment.Global("ACGTACGT", "ACGTACGT");

        Assert.Equal(1.0, result.Identity, tolerance: 1e-12);
        Assert.Equal(8.0, result.Score, tolerance: 1e-9);
        Assert.Equal(0, result.Gaps);
    }

    [Fact]
    public void Alignment_Global_InsertsGapForDeletion()
    {
        AlignmentResult result = Alignment.Global("ACGTACGT", "ACGTCGT");

        Assert.Equal(8, result.First.Length);
        Assert.Equal(8, result.Second.Length);
        Assert.Equal(1, result.Gaps);
        Assert.Equal(7, result.Matches);
    }

    [Fact]
    public void Alignment_Local_FindsCommonSubsequence()
    {
        // Общий участок GATTACA окружён разным контекстом
        AlignmentResult result = Alignment.Local("TTTTGATTACAGGGG", "CCCGATTACAAAAA");

        Assert.Contains("GATTACA", result.First.Replace("-", string.Empty), StringComparison.Ordinal);
        Assert.Equal(7.0, result.Score, tolerance: 1e-9);
        Assert.Equal(1.0, result.Identity, tolerance: 1e-12);
    }

    [Fact]
    public void Alignment_Local_ScoreNeverBelowZero()
    {
        AlignmentResult result = Alignment.Local("AAAA", "TTTT");

        Assert.True(result.Score >= 0);
    }

    [Fact]
    public void Alignment_HammingDistance_CountsMismatches()
    {
        Assert.Equal(7, Alignment.HammingDistance("GAGCCTACTAACGGGAT", "CATCGTAATGACGGCCT"));
        _ = Assert.Throws<ArgumentException>(() => Alignment.HammingDistance("AAA", "AA"));
    }

    #endregion

    #region Генетика

    [Fact]
    public void HardyWeinberg_BalancedPopulation_IsInEquilibrium()
    {
        // Идеально равновесная выборка: p = q = 0.5, генотипы 250 : 500 : 250
        HardyWeinbergResult result = HardyWeinberg.Test(250, 500, 250);

        Assert.Equal(0.5, result.AlleleFrequency, tolerance: 1e-12);
        Assert.Equal(0.0, result.ChiSquare, tolerance: 1e-9);
        Assert.True(result.InEquilibrium);
    }

    [Fact]
    public void HardyWeinberg_ExcessOfHomozygotes_BreaksEquilibrium()
    {
        // Гетерозигот вдвое меньше ожидаемого — признак близкородственного скрещивания
        HardyWeinbergResult result = HardyWeinberg.Test(400, 200, 400);

        Assert.False(result.InEquilibrium);
        Assert.True(result.ChiSquare > 3.84);
    }

    [Fact]
    public void HardyWeinberg_RareDisease_HasManyCarriers()
    {
        // Один больной на десять тысяч: носителем оказывается каждый пятидесятый
        double carriers = HardyWeinberg.CarrierFrequency(1.0 / 10000);

        Assert.Equal(0.0198, carriers, tolerance: 1e-4);
        Assert.True(carriers > 100 * (1.0 / 10000));
    }

    [Fact]
    public void Mendel_MonohybridRatio_FitsThreeToOne()
    {
        // Классические данные Менделя: 705 пурпурных и 224 белых цветка
        (double chi, double pValue, bool fits) = Mendel.TestRatio([705, 224], Mendel.MonohybridRatio);

        Assert.True(fits);
        Assert.True(pValue > 0.05);
        Assert.True(chi < 3.84);
    }

    [Fact]
    public void Mendel_DihybridRatio_FitsNineToThreeToThreeToOne()
    {
        // Опыт Менделя по двум признакам: 315 : 108 : 101 : 32
        (_, double pValue, bool fits) = Mendel.TestRatio([315, 108, 101, 32], Mendel.DihybridRatio);

        Assert.True(fits);
        Assert.True(pValue > 0.05);
    }

    [Fact]
    public void Mendel_DistortedRatio_IsRejected()
    {
        (_, _, bool fits) = Mendel.TestRatio([500, 500], Mendel.MonohybridRatio);

        Assert.False(fits);
    }

    [Fact]
    public void Mendel_MapDistance_MatchesRecombinationPercent()
    {
        double frequency = Mendel.RecombinationFrequency(recombinants: 18, total: 200);

        Assert.Equal(0.09, frequency, tolerance: 1e-12);
        Assert.Equal(9.0, Mendel.MapDistance(frequency), tolerance: 1e-12);
    }

    #endregion

    #region Популяции

    [Fact]
    public void Growth_Exponential_DoublesAfterDoublingTime()
    {
        double rate = 0.35;
        double doubling = PopulationGrowth.DoublingTime(rate);

        Assert.Equal(200, PopulationGrowth.Exponential(100, rate, doubling), tolerance: 1e-9);
    }

    [Fact]
    public void Growth_Logistic_ApproachesCapacity()
    {
        double capacity = 1000;

        Assert.Equal(capacity, PopulationGrowth.Logistic(10, 0.5, capacity, 100), tolerance: 1e-6);
        Assert.True(PopulationGrowth.Logistic(10, 0.5, capacity, 5) < capacity);
    }

    [Fact]
    public void Growth_Logistic_InflectionAtHalfCapacity()
    {
        double capacity = 1000;
        double inflection = PopulationGrowth.InflectionTime(10, 0.5, capacity);

        // В точке перегиба численность равна половине ёмкости среды
        Assert.Equal(capacity / 2, PopulationGrowth.Logistic(10, 0.5, capacity, inflection), tolerance: 1e-6);
    }

    [Fact]
    public void LotkaVolterra_StartingAtEquilibrium_StaysThere()
    {
        (double prey, double predator) = LotkaVolterra.Equilibrium(1.0, 0.02, 0.5, 0.01);

        IReadOnlyList<PredatorPreyState> states = LotkaVolterra.Simulate(
            1.0, 0.02, 0.5, 0.01, prey, predator, finalTime: 50);

        Assert.Equal(prey, states[^1].Prey, tolerance: 1e-3);
        Assert.Equal(predator, states[^1].Predator, tolerance: 1e-3);
    }

    [Fact]
    public void LotkaVolterra_Oscillates()
    {
        IReadOnlyList<PredatorPreyState> states = LotkaVolterra.Simulate(
            1.0, 0.02, 0.5, 0.01, initialPrey: 80, initialPredator: 30, finalTime: 40, points: 400);

        double minimum = states.Min(s => s.Prey);
        double maximum = states.Max(s => s.Prey);

        // Численности колеблются, а не сходятся к точке
        Assert.True(maximum > 1.5 * minimum);
        Assert.All(states, s => Assert.True(s.Prey > 0 && s.Predator > 0));
    }

    #endregion

    #region Эпидемии

    [Fact]
    public void Sir_ConservesTotalPopulation()
    {
        EpidemicResult result = EpidemicModels.Sir(transmissionRate: 0.4, recoveryRate: 0.1);

        foreach (EpidemicState state in result.States)
            Assert.Equal(1.0, state.Susceptible + state.Infected + state.Recovered, tolerance: 1e-6);
    }

    [Fact]
    public void Sir_BelowThreshold_EpidemicFadesOut()
    {
        EpidemicResult result = EpidemicModels.Sir(transmissionRate: 0.08, recoveryRate: 0.1);

        Assert.True(result.BasicReproductionNumber < 1);
        Assert.True(result.FinalSize < 0.01);
        Assert.Equal(0.0, result.HerdImmunityThreshold, tolerance: 1e-12);
    }

    [Fact]
    public void Sir_AboveThreshold_ProducesOutbreak()
    {
        EpidemicResult result = EpidemicModels.Sir(transmissionRate: 0.4, recoveryRate: 0.1);

        Assert.Equal(4.0, result.BasicReproductionNumber, tolerance: 1e-12);
        Assert.Equal(0.75, result.HerdImmunityThreshold, tolerance: 1e-12);
        Assert.True(result.PeakInfected > 0.3);
        Assert.True(result.FinalSize > result.HerdImmunityThreshold);
    }

    [Fact]
    public void Sir_FinalSize_MatchesImplicitEquation()
    {
        EpidemicResult result = EpidemicModels.Sir(transmissionRate: 0.3, recoveryRate: 0.1, finalTime: 400, points: 801);
        double analytic = EpidemicModels.FinalEpidemicSize(3.0);

        // Численное решение обязано сойтись к корню уравнения итогового размера
        Assert.Equal(analytic, result.FinalSize, tolerance: 0.01);
        Assert.Equal(0.9405, analytic, tolerance: 1e-3);
    }

    [Fact]
    public void Interpret_Epidemic_ExplainsOvershoot()
    {
        Interpretation interpretation = EpidemicModels.Sir(0.4, 0.1).Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "R₀");
        Assert.Contains(interpretation.Findings, f => f.Contains("восприимчивые", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("однородной", StringComparison.Ordinal));
    }

    #endregion

    #region Разнообразие

    [Fact]
    public void Diversity_UniformCommunity_ReachesMaximumShannon()
    {
        int[] uniform = [25, 25, 25, 25];

        // При равном обилии индекс Шеннона равен натуральному логарифму числа видов
        Assert.Equal(Math.Log(4), Diversity.Shannon(uniform), tolerance: 1e-12);
        Assert.Equal(1.0, Diversity.Evenness(uniform), tolerance: 1e-12);
    }

    [Fact]
    public void Diversity_SingleSpecies_HasZeroDiversity()
    {
        int[] single = [100, 0, 0];

        Assert.Equal(0.0, Diversity.Shannon(single), tolerance: 1e-12);
        Assert.Equal(0.0, Diversity.Simpson(single), tolerance: 1e-12);
        Assert.Equal(1, Diversity.Richness(single));
    }

    [Fact]
    public void Diversity_DominatedCommunity_HasLowEvenness()
    {
        int[] dominated = [97, 1, 1, 1];
        int[] uniform = [25, 25, 25, 25];

        Assert.True(Diversity.Evenness(dominated) < 0.3);
        Assert.True(Diversity.Shannon(dominated) < Diversity.Shannon(uniform));

        // Число видов одинаково — различие видно только по индексам
        Assert.Equal(Diversity.Richness(uniform), Diversity.Richness(dominated));
    }

    [Fact]
    public void Diversity_Chao1_ExceedsObservedWhenSingletonsPresent()
    {
        int[] withRareSpecies = [50, 20, 10, 1, 1, 1, 2];

        Assert.True(Diversity.Chao1(withRareSpecies) > Diversity.Richness(withRareSpecies));
    }

    [Fact]
    public void Diversity_Jaccard_MeasuresSharedSpecies()
    {
        var first = new HashSet<string> { "дуб", "клён", "берёза" };
        var second = new HashSet<string> { "клён", "берёза", "осина" };

        Assert.Equal(0.5, Diversity.Jaccard(first, second), tolerance: 1e-12);
        Assert.Equal(1.0, Diversity.Jaccard(first, first), tolerance: 1e-12);
    }

    [Fact]
    public void Diversity_BrayCurtis_IsZeroForIdenticalCommunities()
    {
        int[] community = [10, 20, 30];

        Assert.Equal(0.0, Diversity.BrayCurtis(community, community), tolerance: 1e-12);
        Assert.Equal(1.0, Diversity.BrayCurtis([10, 0], [0, 10]), tolerance: 1e-12);
    }

    #endregion

    #region Регрессии: выравнивание и размер вспышки

    [Fact]
    public void Alignment_Global_MatchesExhaustiveSearch()
    {
        // Прежде обратный ход шёл по максимумам клеток, а переход «пропуск — пропуск в другой строке» был запрещён
        var rng = new Random(5);
        ScoringScheme[] schemes = [ScoringScheme.Nucleotide, new(2, -3, -5, -1), new(1, -4, -1, -1), ScoringScheme.Linear(1, -1, -1)];

        for (int trial = 0; trial < 240; trial++)
        {
            string a = RandomText(rng, "ACGT", rng.Next(0, 6));
            string b = RandomText(rng, "ACGT", rng.Next(0, 6));
            ScoringScheme s = schemes[trial % schemes.Length];
            double Similarity(char x, char y) => x == y ? s.Match : s.Mismatch;

            AlignmentResult result = Alignment.Global(a, b, s);

            Assert.Equal(BestGlobal(a, b, Similarity, s.GapOpen, s.GapExtend), result.Score, 9);
            Assert.Equal(a, result.First.Replace("-", string.Empty));
            Assert.Equal(b, result.Second.Replace("-", string.Empty));
            Assert.Equal(result.Score, Rescore(result.First, result.Second, Similarity, s.GapOpen, s.GapExtend), 9);
        }
    }

    [Fact]
    public void Alignment_Local_MatchesExhaustiveSearch()
    {
        var rng = new Random(6);

        for (int trial = 0; trial < 80; trial++)
        {
            string a = RandomText(rng, "ACGT", 1 + rng.Next(4));
            string b = RandomText(rng, "ACGT", 1 + rng.Next(4));
            ScoringScheme s = trial % 2 == 0 ? ScoringScheme.Nucleotide : new ScoringScheme(2, -1, -1.5, -0.5);
            double Similarity(char x, char y) => x == y ? s.Match : s.Mismatch;

            double best = 0;

            for (int i = 0; i <= a.Length; i++)
                for (int j = i + 1; j <= a.Length; j++)
                    for (int k = 0; k <= b.Length; k++)
                        for (int l = k + 1; l <= b.Length; l++)
                            best = Math.Max(best, BestGlobal(a[i..j], b[k..l], Similarity, s.GapOpen, s.GapExtend));

            AlignmentResult result = Alignment.Local(a, b, s);

            Assert.Equal(best, result.Score, 9);

            if (result.First.Length > 0)
            {
                Assert.Contains(result.First.Replace("-", string.Empty), a, StringComparison.Ordinal);
                Assert.Contains(result.Second.Replace("-", string.Empty), b, StringComparison.Ordinal);
                Assert.Equal(result.Score, Rescore(result.First, result.Second, Similarity, s.GapOpen, s.GapExtend), 9);
            }
        }
    }

    [Fact]
    public void Blosum62_KnownEntries_AndProteinAlignmentIsOptimal()
    {
        SubstitutionMatrix blosum = SubstitutionMatrix.Blosum62;

        Assert.Equal(11, blosum['W', 'W']);
        Assert.Equal(9, blosum['C', 'C']);
        Assert.Equal(4, blosum['A', 'A']);
        Assert.Equal(3, blosum['I', 'V']);
        Assert.Equal(3, blosum['F', 'Y']);
        Assert.Equal(-4, blosum['W', 'N']);
        Assert.Equal(1, blosum['s', 'a']);
        _ = Assert.Throws<ArgumentException>(() => blosum['B', 'A']);

        var rng = new Random(7);

        for (int trial = 0; trial < 60; trial++)
        {
            string a = RandomText(rng, AminoAcids.Standard, rng.Next(0, 5));
            string b = RandomText(rng, AminoAcids.Standard, rng.Next(0, 5));

            AlignmentResult result = Alignment.Global(a, b, blosum, -4, -1);

            Assert.Equal(BestGlobal(a, b, (x, y) => blosum[x, y], -4, -1), result.Score, 9);
        }
    }

    [Theory]
    [InlineData(1.01)]
    [InlineData(1.05)]
    [InlineData(1.3)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void FinalEpidemicSize_SolvesTheEquationNearThreshold(double r0)
    {
        // Прежняя простая итерация при R₀ = 1,01 ошибалась больше, чем на сам ответ
        double size = EpidemicModels.FinalEpidemicSize(r0);
        double low = 1e-12, high = 1;

        for (int i = 0; i < 200; i++)
        {
            double middle = (low + high) / 2;

            if (1 - middle - Math.Exp(-r0 * middle) > 0)
                low = middle;
            else
                high = middle;
        }

        Assert.Equal((low + high) / 2, size, 12);
        Assert.True(Math.Abs(1 - size - Math.Exp(-r0 * size)) < 1e-13);
    }

    [Fact]
    public void Simulations_RequireAtLeastTwoOutputPoints()
    {
        // Прежде одна точка давала шаг 0/0 и молча возвращала NaN
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => EpidemicModels.Sir(0.3, 0.1, points: 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => LotkaVolterra.Simulate(1, 0.02, 0.5, 0.01, 10, 5, 10, points: 1));
    }

    private static string RandomText(Random rng, string alphabet, int length)
        => new(Enumerable.Range(0, length).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());

    /// <summary>Перебор всех выравниваний с независимым подсчётом счёта</summary>
    private static double BestGlobal(string a, string b, Func<char, char, double> similarity, double open, double extend)
    {
        double best = double.NegativeInfinity;
        var top = new System.Text.StringBuilder();
        var bottom = new System.Text.StringBuilder();

        void Walk(int i, int j)
        {
            if (i == a.Length && j == b.Length)
            {
                best = Math.Max(best, Rescore(top.ToString(), bottom.ToString(), similarity, open, extend));
                return;
            }

            if (i < a.Length && j < b.Length)
            {
                top.Append(a[i]);
                bottom.Append(b[j]);
                Walk(i + 1, j + 1);
                top.Length--;
                bottom.Length--;
            }

            if (i < a.Length)
            {
                top.Append(a[i]);
                bottom.Append('-');
                Walk(i + 1, j);
                top.Length--;
                bottom.Length--;
            }

            if (j < b.Length)
            {
                top.Append('-');
                bottom.Append(b[j]);
                Walk(i, j + 1);
                top.Length--;
                bottom.Length--;
            }
        }

        Walk(0, 0);

        return best;
    }

    /// <summary>Счёт готового выравнивания: пары по схеме, каждый сплошной пропуск — открытие и продления</summary>
    private static double Rescore(string top, string bottom, Func<char, char, double> similarity, double open, double extend)
    {
        Assert.Equal(top.Length, bottom.Length);

        double score = 0;

        for (int k = 0; k < top.Length; k++)
        {
            Assert.False(top[k] == '-' && bottom[k] == '-', "столбец из двух пропусков");

            if (top[k] != '-' && bottom[k] != '-')
                score += similarity(top[k], bottom[k]);
        }

        return score + GapCost(top, open, extend) + GapCost(bottom, open, extend);
    }

    private static double GapCost(string row, double open, double extend)
    {
        double cost = 0;
        int run = 0;

        for (int k = 0; k <= row.Length; k++)
        {
            if (k < row.Length && row[k] == '-')
            {
                run++;
                continue;
            }

            if (run > 0)
                cost += open + ((run - 1) * extend);

            run = 0;
        }

        return cost;
    }

    #endregion
}
