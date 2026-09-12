using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.MathUtils.ODE;
using AI.Physics.Nuclear;
using AI.Physics.Relativity;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Ядерная физика сверяется с измеренным: энергия связи углерода-12 — из одних констант CODATA,
/// дейтрон — 2.2246 МэВ, синтез D-T — 17.59 МэВ, порог первой искусственной реакции Резерфорда —
/// 1.53 МэВ, грамм радия — около кюри. Цепочки распада сверены с численным интегрированием
/// и с точными формулами частных случаев.
/// </summary>
public class NuclearPhysicsTests
{
    // Массы атомов из таблицы AME2020, а. е. м.
    private const double H1 = 1.00782503223;
    private const double H2 = 2.01410177812;
    private const double H3 = 3.01604928132;
    private const double He3 = 3.01602932197;
    private const double He4 = 4.00260325413;
    private const double N14 = 14.00307400425;
    private const double O17 = 16.99913175650;
    private const double Fe56 = 55.93493633;
    private const double Ni62 = 61.92834537;
    private const double Pb208 = 207.9766525;

    private static Quantity U(double value) => Quantity.Of(value, "u");

    private static double MeV(Quantity energy) => energy.In("MeV");

    // Масса атома по массовому числу и избытку массы, кэВ
    private static Quantity Atom(int massNumber, double massExcessKeV) => U(massNumber + (massExcessKeV / 931_494.10372));

    private static double TwoMemberRatio(double parentHalfLife, double daughterHalfLife, double time, double branching = 1)
    {
        double parent = Math.Log(2) / parentHalfLife;
        double daughter = Math.Log(2) / daughterHalfLife;

        return branching * daughter / (daughter - parent) * (1 - Math.Exp(-(daughter - parent) * time));
    }

    #region Энергия связи

    [Fact]
    public void HydrogenAtomMass_FromCodataMatchesMassTable()
        => Assert.Equal(H1, NuclearBinding.HydrogenAtomMass.In("u"), 1e-9);

    [Fact]
    public void Carbon12_BindingFromConstantsAlone()
    {
        // Масса атома углерода-12 — ровно 12 а. е. м. по определению: нужны только константы CODATA
        NuclearBinding carbon = NuclearBinding.FromAtomicMass(new Nuclide(6, 12, "C-12"), U(12));

        Assert.Equal(92.162, MeV(carbon.Energy), 1e-3);
        Assert.Equal(7.680, MeV(carbon.PerNucleon), 1e-3);
    }

    [Fact]
    public void LightNuclei_MatchMeasuredBindingEnergies()
    {
        Assert.Equal(2.224566, MeV(NuclearBinding.FromAtomicMass(new Nuclide(1, 2, "H-2"), U(H2)).Energy), 2e-5);
        Assert.Equal(28.296, MeV(NuclearBinding.FromAtomicMass(new Nuclide(2, 4, "He-4"), U(He4)).Energy), 1e-3);
    }

    [Fact]
    public void BindingPerNucleon_PeaksAtNickel62NotIron56()
    {
        NuclearBinding iron = NuclearBinding.FromAtomicMass(new Nuclide(26, 56, "Fe-56"), U(Fe56));
        NuclearBinding nickel = NuclearBinding.FromAtomicMass(new Nuclide(28, 62, "Ni-62"), U(Ni62));

        // Расхожее «железо связано сильнее всех» неверно: никель-62 связан чуть сильнее
        Assert.Equal(8.7903, MeV(iron.PerNucleon), 2e-4);
        Assert.Equal(8.7945, MeV(nickel.PerNucleon), 2e-4);
        Assert.True(nickel.PerNucleon.SiValue > iron.PerNucleon.SiValue);
        Assert.Contains(iron.Interpret().Findings, f => f.Contains("вершины", StringComparison.Ordinal));
    }

    [Fact]
    public void DropModel_WorksForHeavyNucleiButNotForLightOnes()
    {
        NuclearBinding iron = NuclearBinding.FromAtomicMass(new Nuclide(26, 56, "Fe-56"), U(Fe56));
        NuclearBinding lead = NuclearBinding.FromAtomicMass(new Nuclide(82, 208, "Pb-208"), U(Pb208));
        NuclearBinding helium = NuclearBinding.FromAtomicMass(new Nuclide(2, 4, "He-4"), U(He4));

        Assert.Equal(1636.43, MeV(lead.Energy), 0.05);
        Assert.InRange(Math.Abs(iron.PredictionError!.Value), 0, 0.01);
        Assert.InRange(Math.Abs(lead.PredictionError!.Value), 0, 0.01);

        // Дважды магический свинец связан сильнее капли — это оболочки
        Assert.Contains(lead.Interpret().Findings, f => f.Contains("оболочечный", StringComparison.Ordinal));

        // Для гелия капля ошибается почти на пятую часть, и разбор об этом предупреждает
        Assert.True(helium.PredictionError < -0.15);
        Assert.Contains(helium.Interpret().Warnings, w => w.Contains("лёгких ядер", StringComparison.Ordinal));
    }

    [Fact]
    public void ValleyOfStability_BendsTowardNeutronExcess()
    {
        var model = new SemiEmpiricalMassFormula();

        Assert.Equal(8, model.MostStableProtonNumber(16));
        Assert.Equal(26, model.MostStableProtonNumber(56));

        // У тяжёлых ядер кулоновское отталкивание сдвигает устойчивость к избытку нейтронов
        Assert.InRange(model.MostStableProtonNumber(208) / 208.0, 0.38, 0.42);
    }

    #endregion

    #region Энергия реакций

    [Fact]
    public void DeuteriumTritiumFusion_Releases17_6MeV()
    {
        Quantity q = NuclearReactions.QValue([U(H2), U(H3)], [U(He4), PhysicalConstants.NeutronMass]);

        Assert.Equal(17.589, MeV(q), 2e-3);

        // Около 3.4·10¹⁴ Дж на килограмм топлива
        Quantity perKilogram = NuclearReactions.SpecificEnergy(q, [U(H2), U(H3)]);
        Assert.Equal(3.374e14, perKilogram.SiValue, 0.005e14);
    }

    [Fact]
    public void BetaDecays_AccountForElectronMasses()
    {
        Assert.Equal(18.592, NuclearReactions.BetaMinusQ(U(H3), U(He3)).In("keV"), 3e-3);

        // Углерод-11 → бор-11: позитрону достаётся энергия захвата минус две массы электрона
        Quantity capture = NuclearReactions.ElectronCaptureQ(Atom(11, 10_650.34), Atom(11, 8_667.71));
        Quantity positron = NuclearReactions.PositronEmissionQ(Atom(11, 10_650.34), Atom(11, 8_667.71));

        Assert.Equal(0.9606, MeV(positron), 2e-3);
        Assert.Equal(2 * 0.51099895069, MeV(capture) - MeV(positron), 1e-7);

        // Бериллий-7: разность масс атомов меньше 1.022 МэВ — позитрону не хватает энергии,
        // остаётся только захват электрона
        Assert.Equal(0.8618, MeV(NuclearReactions.ElectronCaptureQ(Atom(7, 15_768.9), Atom(7, 14_907.1))), 2e-3);
        Assert.True(NuclearReactions.PositronEmissionQ(Atom(7, 15_768.9), Atom(7, 14_907.1)).SiValue < 0);
    }

    [Fact]
    public void RutherfordTransmutation_ThresholdIs1_53MeV()
    {
        // ¹⁴N(α,p)¹⁷O — первая искусственная ядерная реакция, Резерфорд, 1919
        ReactionThreshold threshold = RelativisticKinematics.Threshold(U(He4), U(N14), U(H1), U(O17));

        Assert.Equal(-1.1919, MeV(threshold.QValue), 1e-3);
        Assert.Equal(1.5326, MeV(threshold.KineticEnergy), 2e-3);

        // Q мало по сравнению с массами — классическая оценка совпадает с точной
        double classical = MeV(threshold.ClassicalEstimate);
        Assert.Equal(MeV(threshold.KineticEnergy), classical, 1e-3 * classical);
        Assert.Contains(threshold.Interpret().Findings, f => f.Contains("совпадает", StringComparison.Ordinal));
    }

    #endregion

    #region Распад

    [Fact]
    public void Year_IsJulianAndTakesPrefixes()
    {
        Assert.Equal(365.25, Quantity.Of(1, "a").In("d"), 12);
        Assert.Equal(1e9, Quantity.Of(1, "Ga").In("a"), 1e-3);
        Assert.Equal(1e6, Quantity.Of(1, "Myr").In("yr"), 1e-6);
    }

    [Fact]
    public void RadiumGram_IsAboutOneCurie()
    {
        Quantity specific = RadioactiveDecay.SpecificActivity(Quantity.Of(1600, "a"), Quantity.Of(226.0254, "g/mol"));
        double perGram = specific.SiValue / 1_000;

        // Кюри определяли как активность грамма радия; уточнённый период даёт на процент меньше
        Assert.Equal(3.66e10, perGram, 0.01e10);
        Assert.InRange(perGram / 3.7e10, 0.98, 1.0);
    }

    [Fact]
    public void RadiocarbonAge_OfQuarterRemaining_IsTwoHalfLives()
    {
        Quantity halfLife = Quantity.Of(5730, "a");

        Assert.Equal(11_460, RadioactiveDecay.ElapsedTime(halfLife, 0.25).In("a"), 1e-9);
        Assert.Equal(0.25, RadioactiveDecay.RemainingFraction(halfLife, Quantity.Of(11_460, "a")), 12);
        Assert.Equal(halfLife.SiValue / Math.Log(2), RadioactiveDecay.MeanLife(halfLife).SiValue, 1e-3);
        Assert.Equal(1e6, RadioactiveDecay.AtomsForActivity(RadioactiveDecay.Activity(halfLife, 1e6), halfLife), 1e-6);
    }

    [Fact]
    public void RadiumRadon_ReachSecularEquilibrium()
    {
        var chain = new DecayChain(
            new ChainMember("Ra-226", Quantity.Of(1600, "a")),
            new ChainMember("Rn-222", Quantity.Of(3.8235, "d")),
            ChainMember.Stable("Po-218 и далее"));

        Quantity time = Quantity.Of(40, "d");
        Quantity[] activities = chain.Activities(time, [1e20, 0, 0]);
        double ratio = activities[1].SiValue / activities[0].SiValue;
        double expected = TwoMemberRatio(Quantity.Of(1600, "a").SiValue, Quantity.Of(3.8235, "d").SiValue, time.SiValue);

        Assert.Equal(expected, ratio, 1e-9 * expected);
        Assert.Equal(1.0, ratio, 1e-3);
        Assert.Equal(DecayEquilibrium.Secular, chain.Equilibrium(0));
        Assert.Contains(chain.State(time, 1e20).Interpret().Findings, f => f.Contains("активности сравнялись", StringComparison.Ordinal));
    }

    [Fact]
    public void TechnetiumGenerator_PeaksAfter23Hours()
    {
        double molybdenum = Quantity.Of(65.94, "h").SiValue;
        double technetium = Quantity.Of(6.0067, "h").SiValue;

        var chain = new DecayChain(
            new ChainMember("Mo-99", Quantity.Of(65.94, "h"), 0.876),
            new ChainMember("Tc-99m", Quantity.Of(6.0067, "h")),
            ChainMember.Stable("Tc-99"));

        double parent = Math.Log(2) / molybdenum;
        double daughter = Math.Log(2) / technetium;
        double analytic = Math.Log(daughter / parent) / (daughter - parent);

        // Поиск максимума по сетке обязан совпасть с формулой ln(λ₂/λ₁)/(λ₂ − λ₁)
        Assert.Equal(analytic, chain.PeakActivityTime(1, 1e15).SiValue, 1e-6 * analytic);
        Assert.Equal(22.84, analytic / 3_600, 0.01);

        Quantity fiveDays = Quantity.Of(5, "d");
        Quantity[] activities = chain.Activities(fiveDays, [1e15, 0, 0]);
        double expected = TwoMemberRatio(molybdenum, technetium, fiveDays.SiValue, 0.876);

        Assert.Equal(expected, activities[1].SiValue / activities[0].SiValue, 1e-9 * expected);
        Assert.Equal(DecayEquilibrium.Transient, chain.Equilibrium(0));
        Assert.Contains(chain.State(fiveDays, 1e15).Interpret().Findings, f => f.Contains("подвижное равновесие", StringComparison.Ordinal));
    }

    [Fact]
    public void Bateman_MatchesNumericalIntegration()
    {
        double[] halfLivesHours = [5, 2, 7];
        double[] branching = [1.0, 0.7, 1.0];
        double[] lambda = halfLivesHours.Select(h => Math.Log(2) / h).ToArray();

        var chain = new DecayChain(
            new ChainMember("A", Quantity.Of(5, "h")),
            new ChainMember("B", Quantity.Of(2, "h"), 0.7),
            new ChainMember("C", Quantity.Of(7, "h")),
            ChainMember.Stable("D"));

        // Все члены присутствуют с самого начала — проверяется общий случай, а не только чистый родитель
        double[] initial = [1e6, 0, 5e4, 1e3];

        Vector Rates(double t, Vector n) => new(
            -lambda[0] * n[0],
            (branching[0] * lambda[0] * n[0]) - (lambda[1] * n[1]),
            (branching[1] * lambda[1] * n[1]) - (lambda[2] * n[2]),
            branching[2] * lambda[2] * n[2]);

        double[] hours = [1, 5, 20, 60];
        Vector[] numeric = RungeKutta.SolveSystem(Rates, 0, new Vector(initial), hours, stepsPerInterval: 4_000);

        for (int i = 0; i < hours.Length; i++)
        {
            double[] exact = chain.Amounts(Quantity.Of(hours[i], "h"), initial);

            for (int member = 0; member < 4; member++)
                Assert.Equal(numeric[i][member], exact[member], 1e-7 * Math.Max(1, Math.Abs(numeric[i][member])));
        }
    }

    [Fact]
    public void UraniumSeries_StiffChainConservesNucleiAndReachesEquilibrium()
    {
        // Периоды от минуты до миллиардов лет: отношение постоянных распада около 10¹⁵
        var chain = new DecayChain(
            new ChainMember("U-238", Quantity.Of(4.468e9, "a")),
            new ChainMember("Th-234", Quantity.Of(24.10, "d")),
            new ChainMember("Pa-234m", Quantity.Of(1.159, "min")),
            new ChainMember("U-234", Quantity.Of(2.455e5, "a")),
            ChainMember.Stable("Th-230 и далее"));

        const double Atoms = 1e20;

        foreach (double years in new[] { 0.01, 1, 1e3, 1e6, 1e9 })
            Assert.Equal(Atoms, chain.Amounts(Quantity.Of(years, "a"), Atoms).Sum(), 1e-12 * Atoms);

        Quantity oneYear = Quantity.Of(1, "a");
        Quantity[] activities = chain.Activities(oneYear, [Atoms, 0, 0, 0, 0]);
        double thorium = activities[1].SiValue / activities[0].SiValue;
        double protactinium = activities[2].SiValue / activities[0].SiValue;
        double expected = TwoMemberRatio(Quantity.Of(4.468e9, "a").SiValue, Quantity.Of(24.10, "d").SiValue, oneYear.SiValue);

        Assert.Equal(expected, thorium, 1e-9);
        Assert.Equal(thorium, protactinium, 1e-4);
    }

    [Fact]
    public void EqualHalfLives_DoNotBreakTheSolution()
    {
        // Классическая формула Бейтмана делит здесь на ноль; точный ответ — N₀·λt·e^(−λt)
        var chain = new DecayChain(
            new ChainMember("A", Quantity.Of(1, "h")),
            new ChainMember("B", Quantity.Of(1, "h")),
            ChainMember.Stable("C"));

        double lambda = Math.Log(2) / 3_600;

        foreach (double hours in new[] { 0.5, 3.0 })
        {
            double t = hours * 3_600;
            double expected = 1e10 * lambda * t * Math.Exp(-lambda * t);

            Assert.Equal(expected, chain.Amounts(Quantity.Of(hours, "h"), 1e10)[1], 1e-12 * expected);
        }

        // Почти равные периоды дают почти тот же ответ — без провала точности
        var near = new DecayChain(
            new ChainMember("A", Quantity.Of(1, "h")),
            new ChainMember("B", Quantity.Of(1.000001, "h")),
            ChainMember.Stable("C"));

        double equal = chain.Amounts(Quantity.Of(2, "h"), 1e10)[1];
        Assert.Equal(equal, near.Amounts(Quantity.Of(2, "h"), 1e10)[1], 1e-5 * equal);
    }

    [Fact]
    public void EarlyTimes_KeepDeepMembersAccurate()
    {
        // Через миллисекунду все экспоненты почти равны единице: классическая сумма
        // потеряла бы все знаки, а верный ответ — первый член ряда N₀·Πλ·tⁿ/n!
        var chain = new DecayChain(
            new ChainMember("A", Quantity.Of(1.0, "h")),
            new ChainMember("B", Quantity.Of(1.1, "h")),
            new ChainMember("C", Quantity.Of(1.2, "h")),
            ChainMember.Stable("D"));

        double[] lambda = [Math.Log(2) / 3_600, Math.Log(2) / 3_960, Math.Log(2) / 4_320];
        const double Atoms = 1e20;
        const double T = 1e-3;

        double[] amounts = chain.Amounts(Quantity.Of(T, "s"), Atoms);
        double third = Atoms * lambda[0] * lambda[1] * T * T / 2;
        double fourth = Atoms * lambda[0] * lambda[1] * lambda[2] * T * T * T / 6;

        Assert.Equal(third, amounts[2], 1e-5 * third);
        Assert.Equal(fourth, amounts[3], 1e-5 * fourth);
    }

    [Fact]
    public void LongerLivedDaughter_Accumulates()
    {
        var chain = new DecayChain(
            new ChainMember("A", Quantity.Of(1, "h")),
            new ChainMember("B", Quantity.Of(10, "h")),
            ChainMember.Stable("C"));

        Assert.Equal(DecayEquilibrium.None, chain.Equilibrium(0));
        Assert.Contains(chain.State(Quantity.Of(5, "h"), 1e6).Interpret().Findings, f => f.Contains("накапливается", StringComparison.Ordinal));
    }

    [Fact]
    public void Chain_RejectsInconsistentInput()
    {
        _ = Assert.Throws<ArgumentException>(() => new DecayChain(ChainMember.Stable("X"), new ChainMember("Y", Quantity.Of(1, "h"))));
        _ = Assert.Throws<DimensionMismatchException>(() => new ChainMember("A", Quantity.Of(1, "m")));

        var chain = new DecayChain(new ChainMember("A", Quantity.Of(1, "h")), ChainMember.Stable("B"));

        _ = Assert.Throws<ArgumentException>(() => chain.Amounts(Quantity.Of(1, "h"), [1.0]));
        _ = Assert.Throws<InvalidOperationException>(() => chain.PeakActivityTime(1, 1e6));

        // В нулевой момент — ровно начальные количества
        double[] initial = [5.0, 2.0];
        Assert.Equal(initial, chain.Amounts(Quantity.Zero(Dimension.TimeDim), initial));
    }

    #endregion
}
