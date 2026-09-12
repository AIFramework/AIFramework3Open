using AI.Geometry.Primitives;
using AI.Insights;
using AI.Physics.Relativity;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Релятивистская кинематика проверяется инвариантами и числами, известными из опыта: масса
/// не меняется при бусте, мюон из распада пиона летит с импульсом 29.79 МэВ/c, порог рождения
/// антипротона на неподвижной мишени — шесть масс протона.
/// </summary>
public class RelativityTests
{
    private static Quantity MeV(double value) => Quantity.Of(value, "MeV");

    private static double InMeV(Quantity energy) => energy.In("MeV");

    private static Quantity Speed(double beta) => new(beta * PhysicalConstants.SpeedOfLight.SiValue, Dimension.Velocity);

    #region Преобразования Лоренца

    [Fact]
    public void Boost_KeepsInvariantMass()
    {
        FourVector proton = RelativisticKinematics.FromMomentum(MeV(938.272), MeV(2_000), new Vector3(1, 2, 3));
        double invariant = proton.Square;

        Vector3[] frames = [new(0.3, -0.2, 0.5), new(0.99, 0, 0), new(0, 0, -0.9), new(-0.5, 0.5, 0.5)];

        foreach (Vector3 beta in frames)
        {
            FourVector moved = proton.Boost(beta);
            FourVector back = moved.Boost(-beta);

            // Энергия и импульс меняются, масса — нет; обратный буст возвращает исходный вектор
            Assert.Equal(1.0, moved.Square / invariant, 10);
            Assert.Equal(proton.T, back.T, 1e-9 * proton.T);
            Assert.Equal(0, (back.Space - proton.Space).Length, 1e-9 * proton.T);
        }
    }

    [Fact]
    public void Boosts_AlongOneAxis_ComposeByVelocityAddition()
    {
        FourVector muon = RelativisticKinematics.FromMomentum(MeV(105.66), MeV(300), new Vector3(1, 1, 0));
        double combined = SpecialRelativity.Beta(SpecialRelativity.AddVelocities(Speed(0.6), Speed(0.7)));

        FourVector twice = muon.Boost(new Vector3(0.6, 0, 0)).Boost(new Vector3(0.7, 0, 0));
        FourVector once = muon.Boost(new Vector3(combined, 0, 0));

        // Два буста подряд — один буст со скоростью по релятивистскому сложению, а быстроты складываются
        Assert.Equal(once.T, twice.T, 1e-9 * muon.T);
        Assert.Equal(0, (once.Space - twice.Space).Length, 1e-9 * muon.T);
        Assert.Equal(
            SpecialRelativity.Rapidity(0.6) + SpecialRelativity.Rapidity(0.7),
            SpecialRelativity.Rapidity(combined),
            12);

        // Сумма досветовых скоростей остаётся досветовой
        Assert.Equal(1.8 / 1.81, SpecialRelativity.Beta(SpecialRelativity.AddVelocities(Speed(0.9), Speed(0.9))), 12);
    }

    [Fact]
    public void BoostToOwnVelocity_GivesRestFrame()
    {
        FourVector pion = RelativisticKinematics.FromKineticEnergy(MeV(139.57), MeV(500), new Vector3(0, 1, -2));
        FourVector rest = pion.Boost(pion.Velocity);

        Assert.Equal(InMeV(MassEnergy.RestEnergy(MeV(139.57))), InMeV(new Quantity(rest.T, Dimension.Energy)), 1e-9);
        Assert.True(rest.Space.Length < 1e-9 * pion.T);
    }

    [Fact]
    public void BoostedPhoton_ReproducesRelativisticDoppler()
    {
        FourVector photon = RelativisticKinematics.Photon(Quantity.Of(1, "eV"), new Vector3(1, 0, 0));
        double ratio = photon.Boost(new Vector3(0.6, 0, 0)).T / photon.T;

        // Наблюдатель уходит от источника со скоростью 0.6c: частота падает ровно вдвое
        Assert.Equal(SpecialRelativity.DopplerFactor(0.6), ratio, 12);
        Assert.Equal(0.5, ratio, 12);
    }

    [Fact]
    public void MuonDecayLength_AgreesBetweenDilationAndFourMomentum()
    {
        double c = PhysicalConstants.SpeedOfLight.SiValue;
        Quantity lifetime = Quantity.Of(2.1969811e-6, "s");
        Quantity speed = Speed(0.998);

        double byDilation = 0.998 * c * SpecialRelativity.DilatedTime(lifetime, speed).SiValue;

        double gamma = SpecialRelativity.LorentzFactor(speed);
        FourVector muon = RelativisticKinematics.FromMomentum(
            MeV(105.6583755), MeV(105.6583755 * gamma * 0.998), new Vector3(0, 0, -1));
        double byMomentum = muon.Space.Length / Math.Sqrt(muon.Square) * c * lifetime.SiValue;

        Assert.Equal(byDilation, byMomentum, 1e-6 * byDilation);

        // Без замедления времени мюон космических лучей пролетал бы 657 м и не доходил до земли
        Assert.Equal(10_398, byDilation, tolerance: 1);
        Assert.Equal(657.3, 0.998 * c * lifetime.SiValue, 0.1);
    }

    #endregion

    #region Распады и реакции

    [Fact]
    public void NeutralPionDecay_SplitsMassBetweenTwoPhotons()
    {
        TwoBodyDecay decay = RelativisticKinematics.Decay(MeV(134.9768), MeV(0), MeV(0));

        Assert.Equal(67.4884, InMeV(decay.FirstEnergy), 1e-4);
        Assert.Equal(1.0, decay.FirstBeta, 12);
    }

    [Fact]
    public void ChargedPionDecay_GivesTextbookMuonMomentum()
    {
        TwoBodyDecay decay = RelativisticKinematics.Decay(MeV(139.57039), MeV(105.6583755), MeV(0));

        Assert.Equal(29.792, InMeV(decay.Momentum), 1e-3);
        Assert.Equal(4.120, InMeV(decay.FirstKineticEnergy), 1e-3);

        // Нейтрино безмассовое: его энергия равна импульсу
        Assert.Equal(InMeV(decay.Momentum), InMeV(decay.SecondEnergy), 1e-9);

        Interpretation text = decay.Interpret();
        Assert.Contains(text.Findings, f => f.Contains("нейтрино", StringComparison.Ordinal));
    }

    [Fact]
    public void InvariantMass_OfDecayProducts_RecoversParentInAnyFrame()
    {
        TwoBodyDecay decay = RelativisticKinematics.Decay(MeV(139.57039), MeV(105.6583755), MeV(0));

        FourVector muon = RelativisticKinematics.FromMomentum(MeV(105.6583755), decay.Momentum, new Vector3(0.3, 0.4, 0.5));
        FourVector neutrino = RelativisticKinematics.Photon(decay.Momentum, new Vector3(-0.3, -0.4, -0.5));
        var lab = new Vector3(-0.6, 0.2, 0.3);

        // Так находят частицы: по продуктам в лаборатории восстанавливают массу распавшейся
        Quantity mass = RelativisticKinematics.CentreOfMassEnergy(muon.Boost(lab), neutrino.Boost(lab));

        Assert.Equal(139.57039, InMeV(mass), 1e-6);
    }

    [Fact]
    public void PhotonSystems_HaveMassOnlyWhenNotParallel()
    {
        FourVector right = RelativisticKinematics.Photon(MeV(1), new Vector3(1, 0, 0));
        FourVector left = RelativisticKinematics.Photon(MeV(1), new Vector3(-1, 0, 0));

        Assert.Equal(0, InMeV(RelativisticKinematics.CentreOfMassEnergy(right)), 1e-6);
        Assert.Equal(0, InMeV(RelativisticKinematics.CentreOfMassEnergy(right, right)), 1e-6);
        Assert.Equal(2, InMeV(RelativisticKinematics.CentreOfMassEnergy(right, left)), 1e-9);
    }

    [Fact]
    public void AntiprotonThreshold_IsSixProtonMasses()
    {
        Quantity p = PhysicalConstants.ProtonMass;
        ReactionThreshold threshold = RelativisticKinematics.Threshold(p, p, p, p, p, p);
        double restEnergy = InMeV(MassEnergy.RestEnergy(p));

        // p + p → p + p + p + p̄: ради этого числа в 1955 году строили Беватрон
        Assert.Equal(6 * restEnergy, InMeV(threshold.KineticEnergy), 1e-6);
        Assert.Equal(5.6296, InMeV(threshold.KineticEnergy) / 1_000, 1e-4);
        Assert.Equal(1.0 / 3, threshold.MassCreationShare, 12);

        Interpretation text = threshold.Interpret();
        Assert.Contains(text.Findings, f => f.Contains("встречных пучках", StringComparison.Ordinal));
        Assert.Contains(text.Findings, f => f.Contains("ошибается", StringComparison.Ordinal));
    }

    #endregion

    #region Единицы и точность

    [Fact]
    public void Mass_AndMomentum_AcceptEitherUnits()
    {
        FourVector byMass = RelativisticKinematics.AtRest(PhysicalConstants.ProtonMass);
        FourVector byEnergy = RelativisticKinematics.AtRest(MeV(938.27208943));

        Assert.Equal(byMass.T, byEnergy.T, 1e-8 * byMass.T);

        double c = PhysicalConstants.SpeedOfLight.SiValue;
        double pc = MeV(500).SiValue;
        var inSi = new Quantity(pc / c, MassEnergy.Momentum);

        FourVector a = RelativisticKinematics.FromMomentum(PhysicalConstants.ProtonMass, MeV(500), new Vector3(1, 0, 0));
        FourVector b = RelativisticKinematics.FromMomentum(PhysicalConstants.ProtonMass, inSi, new Vector3(1, 0, 0));

        Assert.Equal(a.T, b.T, 1e-12 * a.T);
    }

    [Fact]
    public void KineticEnergy_OfSlowParticle_KeepsAllDigits()
    {
        // У протона с энергией 1 эВ разность E − mc² дала бы точность лишь около 10⁻⁷
        FourVector slow = RelativisticKinematics.FromKineticEnergy(
            PhysicalConstants.ProtonMass, Quantity.Of(1, "eV"), new Vector3(1, 0, 0));

        Assert.Equal(1.0, RelativisticKinematics.KineticEnergy(slow).In("eV"), 1e-9);
    }

    [Fact]
    public void ForbiddenAndSuperluminal_AreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() => RelativisticKinematics.Decay(MeV(100), MeV(60), MeV(50)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SpecialRelativity.LorentzFactor(Speed(1.0)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FourVector(1, Vector3.Zero).Boost(new Vector3(1, 0, 0)));
        _ = Assert.Throws<DimensionMismatchException>(() => RelativisticKinematics.AtRest(Quantity.Of(1, "m")));
    }

    #endregion
}
