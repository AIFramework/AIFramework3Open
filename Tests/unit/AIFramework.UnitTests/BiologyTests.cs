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
}
