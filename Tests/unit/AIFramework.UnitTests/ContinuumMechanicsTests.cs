using AI.Geometry.Primitives;
using AI.Physics.Continuum;
using AI.Physics.Fluids;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Механика сплошных сред проверяется тождествами, которые не зависят от реализации: инварианты
/// тензора не меняются при повороте и удовлетворяют характеристическому уравнению, закон Гука
/// обратим, решения Ламе удовлетворяют уравнению равновесия и совместности, вязкоупругие функции —
/// интегральному тождеству, скачок уплотнения — законам сохранения массы, импульса и энергии.
/// </summary>
public class ContinuumMechanicsTests
{
    private static Quantity Q(double value, string unit) => Quantity.Of(value, unit);

    private static readonly ElasticMaterial Steel = ElasticMaterial.FromYoungAndPoisson(Q(200, "GPa"), 0.3);

    #region Тензор напряжений

    [Fact]
    public void Invariants_DoNotDependOnAxes_AndSatisfyCharacteristicEquation()
    {
        var rng = new Random(1);

        for (int trial = 0; trial < 20; trial++)
        {
            SymmetricTensor s = RandomStress(rng);
            (Vector3 e1, Vector3 e2, Vector3 e3) = RandomBasis(rng);
            SymmetricTensor rotated = s.InBasis(e1, e2, e3);

            Assert.Equal(s.Trace.SiValue, rotated.Trace.SiValue, 1e-6);
            Assert.Equal(1, rotated.SecondInvariant.SiValue / s.SecondInvariant.SiValue, 1e-9);
            Assert.Equal(1, rotated.ThirdInvariant.SiValue / s.ThirdInvariant.SiValue, 1e-9);
            Assert.Equal(s.VonMises.SiValue, rotated.VonMises.SiValue, 1e-6);

            PrincipalValues p = s.Principal();
            double i1 = s.Trace.SiValue, i2 = s.SecondInvariant.SiValue, i3 = s.ThirdInvariant.SiValue;

            foreach (double lambda in new[] { p.First.SiValue, p.Second.SiValue, p.Third.SiValue })
            {
                double residual = (lambda * lambda * lambda) - (i1 * lambda * lambda) + (i2 * lambda) - i3;
                Assert.True(Math.Abs(residual) < 1e-9 * Math.Pow(Math.Abs(p.First.SiValue) + Math.Abs(p.Third.SiValue), 3));
            }

            // В главных осях касательных компонент нет
            SymmetricTensor diagonal = s.InBasis(p.FirstDirection, p.SecondDirection, p.ThirdDirection);
            Assert.True(Math.Abs(diagonal.Xy.SiValue) + Math.Abs(diagonal.Yz.SiValue) + Math.Abs(diagonal.Zx.SiValue) < 1e-6);
            Assert.Equal(p.First.SiValue, diagonal.Xx.SiValue, 1e-6);
        }
    }

    [Fact]
    public void ElementaryStates_HaveTextbookEquivalentsAndLodeAngles()
    {
        SymmetricTensor tension = SymmetricTensor.Uniaxial(Q(100, "MPa"));
        SymmetricTensor compression = SymmetricTensor.Uniaxial(Q(-100, "MPa"));
        SymmetricTensor shear = SymmetricTensor.PureShear(Q(100, "MPa"));
        SymmetricTensor pressure = SymmetricTensor.Hydrostatic(Q(50, "MPa"));

        Assert.Equal(100, tension.VonMises.In("MPa"), 9);
        Assert.Equal(0, tension.LodeAngleDegrees, 6);
        Assert.Equal(60, compression.LodeAngleDegrees, 6);
        Assert.Equal(30, shear.LodeAngleDegrees, 6);
        Assert.Equal(100 * Math.Sqrt(3), shear.VonMises.In("MPa"), 9);
        Assert.Equal(100, shear.MaxShear.In("MPa"), 6);
        Assert.Equal(0, pressure.VonMises.SiValue, 9);
        Assert.True(double.IsNaN(pressure.LodeAngleDegrees));
        Assert.Equal(-50, pressure.Mean.In("MPa"), 9);

        // На площадке под 45° чистый сдвиг — чистое растяжение
        PlaneTraction onDiagonal = shear.Traction(new Vector3(1, 1, 0));
        Assert.Equal(100, onDiagonal.Normal.In("MPa"), 6);
        Assert.Equal(0, onDiagonal.Shear.SiValue, 3);
    }

    [Fact]
    public void PlaneStress_MohrCircle_MatchesTextbookExample()
    {
        // σx = 80, σy = −40, τxy = 30 МПа: σ₁,₂ = 20 ± √(60² + 30²)
        SymmetricTensor s = SymmetricTensor.Stress(Q(80, "MPa"), Q(-40, "MPa"), default, Q(30, "MPa"));
        double radius = Math.Sqrt((60 * 60) + (30 * 30));

        PrincipalValues p = s.Principal();
        Assert.Equal(20 + radius, p.First.In("MPa"), 6);
        Assert.Equal(0, p.Second.In("MPa"), 6);
        Assert.Equal(20 - radius, p.Third.In("MPa"), 6);

        // Поворот осей на угол главных площадок обнуляет касательное
        double angle = 0.5 * Math.Atan2(2 * 30, 80 - (-40)) * 180 / Math.PI;
        SymmetricTensor principal = s.RotatedAboutZ(angle);

        Assert.Equal(0, principal.Xy.In("MPa"), 6);
        Assert.Equal(20 + radius, principal.Xx.In("MPa"), 6);
        Assert.Equal((20 + radius - (20 - radius)) / 2, s.MohrCircles()[2].Radius.In("MPa"), 6);
    }

    #endregion

    #region Упругость

    [Fact]
    public void ElasticConstants_ConvertBothWays()
    {
        Assert.Equal(76.923, Steel.ShearModulus.In("GPa"), 3);
        Assert.Equal(166.667, Steel.BulkModulus.In("GPa"), 3);
        Assert.Equal(115.385, Steel.LameLambda.In("GPa"), 3);

        ElasticMaterial fromBulk = ElasticMaterial.FromBulkAndShear(Steel.BulkModulus, Steel.ShearModulus);
        ElasticMaterial fromLame = ElasticMaterial.FromLame(Steel.LameLambda, Steel.ShearModulus);
        ElasticWaveSpeeds speeds = Steel.WaveSpeeds(Q(7850, "kg/m^3"));
        ElasticMaterial fromWaves = ElasticMaterial.FromWaveSpeeds(speeds.Longitudinal, speeds.Shear, Q(7850, "kg/m^3"));

        foreach (ElasticMaterial m in new[] { fromBulk, fromLame, fromWaves })
        {
            Assert.Equal(200, m.YoungModulus.In("GPa"), 6);
            Assert.Equal(0.3, m.PoissonRatio, 9);
        }

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ElasticMaterial.FromYoungAndPoisson(Q(1, "GPa"), 0.5));
    }

    [Fact]
    public void Hooke_IsInvertible_AndReproducesUniaxialAndVolumetricResponse()
    {
        var rng = new Random(2);

        for (int trial = 0; trial < 10; trial++)
        {
            SymmetricTensor stress = RandomStress(rng);
            SymmetricTensor back = Steel.StressFromStrain(Steel.StrainFromStress(stress));

            Assert.True((back - stress).VonMises.SiValue + Math.Abs((back - stress).Trace.SiValue) < 1e-3);
        }

        SymmetricTensor strain = Steel.StrainFromStress(SymmetricTensor.Uniaxial(Q(200, "MPa")));
        Assert.Equal(1e-3, strain.Xx.Value, 12);
        Assert.Equal(-0.3e-3, strain.Yy.Value, 12);

        // Всестороннее давление: объёмная деформация −p/K
        SymmetricTensor squeezed = Steel.StrainFromStress(SymmetricTensor.Hydrostatic(Q(100, "MPa")));
        Assert.Equal(-100e6 / Steel.BulkModulus.SiValue, squeezed.Trace.Value, 15);

        // Чистый сдвиг γ: энергия ½Gγ², тензорная компонента — половина γ
        const double Gamma = 1e-3;
        Assert.Equal(0.5 * Steel.ShearModulus.SiValue * Gamma * Gamma,
            Steel.StrainEnergyDensity(SymmetricTensor.Strain(0, 0, 0, Gamma / 2)).SiValue, 6);
    }

    [Fact]
    public void ThermalStress_AgreesWithHookeForEachConstraint()
    {
        const double Alpha = 12e-6;
        Quantity heating = Q(100, "K");
        double free = Alpha * 100;

        // Обойма: деформация нулевая, напряжение −3K·αΔT
        Assert.Equal(-3 * Steel.BulkModulus.SiValue * free,
            Steel.ConstrainedThermalStress(Alpha, heating, ThermalConstraint.Triaxial).SiValue, 3);

        // Плёнка: εxx = εyy = −αΔT, εzz — такое, что σzz = 0
        double nu = Steel.PoissonRatio;
        SymmetricTensor film = Steel.StressFromStrain(SymmetricTensor.Strain(-free, -free, 2 * nu / (1 - nu) * free));

        Assert.Equal(0, film.Zz.SiValue, 3);
        Assert.Equal(film.Xx.SiValue, Steel.ConstrainedThermalStress(Alpha, heating, ThermalConstraint.Biaxial).SiValue, 3);
        Assert.Equal(-240, Steel.ConstrainedThermalStress(Alpha, heating, ThermalConstraint.Uniaxial).In("MPa"), 9);
    }

    [Fact]
    public void RayleighWave_MatchesExactRootAndViktorovApproximation()
    {
        ElasticMaterial quarter = ElasticMaterial.FromYoungAndPoisson(Q(100, "GPa"), 0.25);
        ElasticWaveSpeeds speeds = quarter.WaveSpeeds(Q(2500, "kg/m^3"));

        Assert.Equal(Math.Sqrt(2 - (2 / Math.Sqrt(3))), speeds.Rayleigh.SiValue / speeds.Shear.SiValue, 9);
        Assert.Equal(Math.Sqrt(3), speeds.Longitudinal.SiValue / speeds.Shear.SiValue, 9);

        // При ν = 0 корень уравнения Рэлея тоже точный: η = 3 − √5
        ElasticWaveSpeeds zero = ElasticMaterial.FromYoungAndPoisson(Q(100, "GPa"), 0).WaveSpeeds(Q(2500, "kg/m^3"));
        Assert.Equal(Math.Sqrt(3 - Math.Sqrt(5)), zero.Rayleigh.SiValue / zero.Shear.SiValue, 9);

        // Приближение Викторова точно лишь до полутора процентов — на нём проверяется только ход кривой
        foreach (double nu in new[] { 0.0, 0.2, 0.35, 0.45 })
        {
            ElasticWaveSpeeds w = ElasticMaterial.FromYoungAndPoisson(Q(100, "GPa"), nu).WaveSpeeds(Q(2500, "kg/m^3"));
            Assert.Equal((0.862 + (1.14 * nu)) / (1 + nu), w.Rayleigh.SiValue / w.Shear.SiValue, 0.015);
            // При ν = 0 поперечного сжатия нет, и волна в стержне идёт так же быстро, как в объёме
            Assert.True(w.Rayleigh < w.Shear && w.Shear < w.Bar && w.Bar <= w.Longitudinal);
        }
    }

    #endregion

    #region Критерии текучести и трещины

    [Fact]
    public void YieldCriteria_DifferByAtMost15PercentAtPureShear()
    {
        StressAssessment shear = YieldCriteria.Assess(SymmetricTensor.PureShear(Q(100, "MPa")), Q(250, "MPa"));
        StressAssessment tension = YieldCriteria.Assess(SymmetricTensor.Uniaxial(Q(125, "MPa")), Q(250, "MPa"));
        StressAssessment pressure = YieldCriteria.Assess(SymmetricTensor.Hydrostatic(Q(1000, "MPa")), Q(250, "MPa"));

        Assert.Equal(Math.Sqrt(3) / 2, shear.TrescaSafetyFactor / shear.VonMisesSafetyFactor, 9);
        Assert.Equal(2, tension.VonMisesSafetyFactor, 9);
        Assert.Equal(2, tension.TrescaSafetyFactor, 9);
        Assert.True(double.IsPositiveInfinity(pressure.VonMisesSafetyFactor));
        Assert.Contains(shear.Interpret().Findings, f => f.Contains("Треска", StringComparison.Ordinal));
    }

    [Fact]
    public void MohrCoulomb_ReproducesUniaxialStrengths()
    {
        const double Phi = 30;
        Quantity cohesion = Q(1, "MPa");
        double c = 1e6, phi = Phi * Math.PI / 180;

        double compressive = 2 * c * Math.Cos(phi) / (1 - Math.Sin(phi));
        double tensile = 2 * c * Math.Cos(phi) / (1 + Math.Sin(phi));

        Assert.Equal(1, YieldCriteria.MohrCoulombSafetyFactor(SymmetricTensor.Uniaxial(new Quantity(-compressive, Dimension.Pressure)), cohesion, Phi), 9);
        Assert.Equal(1, YieldCriteria.MohrCoulombSafetyFactor(SymmetricTensor.Uniaxial(new Quantity(tensile, Dimension.Pressure)), cohesion, Phi), 9);
        Assert.True(double.IsPositiveInfinity(YieldCriteria.MohrCoulombSafetyFactor(SymmetricTensor.Hydrostatic(Q(10, "MPa")), cohesion, Phi)));
    }

    [Fact]
    public void Fracture_IsConsistentWithGriffith()
    {
        Quantity toughness = new(50e6, FractureMechanics.StressIntensityDimension);
        Quantity critical = FractureMechanics.CriticalCrackLength(Q(200, "MPa"), toughness);

        Assert.Equal(toughness.SiValue, FractureMechanics.StressIntensity(Q(200, "MPa"), critical).SiValue, 3);

        // Гриффит: K_IC = √(E·G_c) при плоском напряжённом состоянии
        Quantity release = FractureMechanics.EnergyReleaseRate(toughness, Steel, planeStrain: false);
        Assert.Equal(toughness.SiValue * toughness.SiValue / 200e9, release.SiValue, 6);

        CrackAssessment assessment = FractureMechanics.Assess(Q(100, "MPa"), Q(10, "mm"), toughness, Q(500, "MPa"));
        Assert.Equal(50 / (100 * Math.Sqrt(Math.PI * 0.01)), assessment.SafetyFactor, 9);
        Assert.False(assessment.Fractures);
    }

    #endregion

    #region Классические решения

    [Fact]
    public void Lame_SatisfiesBoundaryConditionsEquilibriumAndCompatibility()
    {
        Quantity a = Q(0.1, "m"), b = Q(0.2, "m"), pi = Q(100, "MPa"), po = Q(20, "MPa");

        Assert.Equal(-100, ThickWalledVessels.Cylinder(a, b, pi, po, a).Radial.In("MPa"), 9);
        Assert.Equal(-20, ThickWalledVessels.Cylinder(a, b, pi, po, b).Radial.In("MPa"), 9);
        Assert.Equal(-100, ThickWalledVessels.Sphere(a, b, pi, po, a).Radial.In("MPa"), 9);

        const double H = 1e-6;

        foreach (double r in new[] { 0.11, 0.15, 0.19 })
        {
            double Radial(double radius) => ThickWalledVessels.Cylinder(a, b, pi, po, Q(radius, "m"), AxialCondition.PlaneStrain).Radial.SiValue;
            double Displacement(double radius) => ThickWalledVessels.CylinderRadialDisplacement(a, b, pi, po, Q(radius, "m"), Steel, AxialCondition.PlaneStrain).SiValue;

            CylinderStress s = ThickWalledVessels.Cylinder(a, b, pi, po, Q(r, "m"), AxialCondition.PlaneStrain);

            // Равновесие dσr/dr + (σr − σθ)/r = 0
            double derivative = (Radial(r + H) - Radial(r - H)) / (2 * H);
            Assert.True(Math.Abs(derivative + ((s.Radial.SiValue - s.Hoop.SiValue) / r)) < 1e-3 * 100e6);

            // Совместность: du/dr — это радиальная деформация по закону Гука
            double radialStrain = (Displacement(r + H) - Displacement(r - H)) / (2 * H);
            double hooke = (s.Radial.SiValue - (0.3 * (s.Hoop.SiValue + s.Axial.SiValue))) / 200e9;
            Assert.Equal(hooke, radialStrain, 9);
        }

        // Тонкая стенка: pr/t для цилиндра и pr/(2t) для сферы
        Quantity r0 = Q(1, "m"), r1 = Q(1.002, "m"), p = Q(1, "MPa");
        Assert.Equal(1 / 0.002, ThickWalledVessels.Cylinder(r0, r1, p, default, Q(1.001, "m")).Hoop.In("MPa"), 0.5);
        Assert.Equal(1 / 0.004, ThickWalledVessels.Sphere(r0, r1, p, default, Q(1.001, "m")).Hoop.In("MPa"), 0.5);
    }

    [Fact]
    public void Kirsch_ConcentratesThreefoldAndLeavesHoleFree()
    {
        Quantity s = Q(100, "MPa"), a = Q(1, "cm");

        Assert.Equal(300, PlateWithHole.Stress(s, a, a, 90).Hoop.In("MPa"), 9);
        Assert.Equal(-100, PlateWithHole.Stress(s, a, a, 0).Hoop.In("MPa"), 9);

        (Quantity radial, _, Quantity shear) = PlateWithHole.Stress(s, a, a, 37);
        Assert.Equal(0, radial.SiValue, 6);
        Assert.Equal(0, shear.SiValue, 6);

        // Вдали от отверстия — однородное растяжение
        (Quantity far, Quantity hoop, _) = PlateWithHole.Stress(s, a, Q(10, "m"), 0);
        Assert.Equal(100, far.In("MPa"), 3);
        Assert.Equal(0, hoop.In("MPa"), 3);
    }

    [Fact]
    public void Hertz_ObeysForceDisplacementLaw()
    {
        HertzContactResult contact = HertzContact.SphereOnPlane(Q(1000, "N"), Q(10, "mm"), Steel, Steel);
        double e = contact.EffectiveModulus.SiValue, r = contact.EffectiveRadius.SiValue, delta = contact.Approach.SiValue;

        Assert.Equal(1000, 4.0 / 3 * e * Math.Sqrt(r) * Math.Pow(delta, 1.5), 6);
        Assert.Equal(1000 / (Math.PI * contact.ContactRadius.SiValue * contact.ContactRadius.SiValue), contact.MeanPressure.SiValue, 3);
        Assert.Equal(1.5, contact.MaxPressure.SiValue / contact.MeanPressure.SiValue, 12);

        // Два одинаковых шара — как шар вдвое меньшего радиуса на плоскости
        HertzContactResult pair = HertzContact.Spheres(Q(1000, "N"), Q(20, "mm"), Steel, Q(20, "mm"), Steel);
        Assert.Equal(contact.ContactRadius.SiValue, pair.ContactRadius.SiValue, 12);
    }

    [Fact]
    public void Beams_AgreeWithDoubleIntegrationOfCurvature()
    {
        Quantity length = Q(2, "m"), force = Q(1, "kN");
        Quantity inertia = Beams.RectangleSecondMoment(Q(40, "mm"), Q(80, "mm"));
        double ei = 200e9 * inertia.SiValue;

        // Консоль: w'' = M/EI, M(x) = F(L − x); интегрируем дважды от заделки
        const int Steps = 20000;
        double h = 2.0 / Steps, slope = 0, deflection = 0;

        for (int k = 0; k < Steps; k++)
        {
            double x = (k + 0.5) * h;
            double curvature = 1000 * (2 - x) / ei;
            deflection += (slope * h) + (0.5 * curvature * h * h);
            slope += curvature * h;
        }

        Assert.Equal(deflection, Beams.CantileverTipDeflection(force, length, Steel, inertia).SiValue, 9);

        Quantity pinned = Beams.EulerBucklingLoad(Steel, inertia, length, ColumnEnds.PinnedPinned);
        Assert.Equal(0.25, Beams.EulerBucklingLoad(Steel, inertia, length, ColumnEnds.FixedFree).SiValue / pinned.SiValue, 12);
        Assert.Equal(4, Beams.EulerBucklingLoad(Steel, inertia, length, ColumnEnds.FixedFixed).SiValue / pinned.SiValue, 12);
        Assert.Equal(2.0457, Beams.EulerBucklingLoad(Steel, inertia, length, ColumnEnds.FixedPinned).SiValue / pinned.SiValue, 4);

        // Полый вал тоньше по массе, но напряжение в нём выше ровно на отношение моментов
        double solid = Torsion.ShearStress(Q(1, "kN*m"), Q(50, "mm")).SiValue;
        double hollow = Torsion.ShearStress(Q(1, "kN*m"), Q(50, "mm"), Q(30, "mm")).SiValue;
        Assert.Equal(1 / (1 - Math.Pow(0.6, 4)), hollow / solid, 12);
    }

    #endregion

    #region Вязкоупругость

    [Fact]
    public void Viscoelastic_RelaxationAndCreepSatisfyConvolutionIdentity()
    {
        ViscoelasticModel[] models =
        [
            ViscoelasticModel.Maxwell(Q(1, "GPa"), Q(1e9, "Pa·s")),
            ViscoelasticModel.StandardLinearSolid(Q(0.5, "GPa"), Q(2, "GPa"), Q(4e9, "Pa·s"))
        ];

        foreach (ViscoelasticModel model in models)
        {
            foreach (double t in new[] { 0.3, 1.0, 5.0 })
            {
                // ∫₀ᵗ E(t − s)·J(s) ds = t — по формуле Симпсона
                const int Intervals = 2000;
                double h = t / Intervals, sum = 0;

                for (int k = 0; k <= Intervals; k++)
                {
                    double s = k * h;
                    double weight = k == 0 || k == Intervals ? 1 : k % 2 == 1 ? 4 : 2;
                    sum += weight * model.RelaxationModulus(Q(t - s, "s")).SiValue * model.CreepCompliance(Q(s, "s")).SiValue;
                }

                Assert.Equal(t, sum * h / 3, 1e-6 * Math.Max(1, t));
            }
        }

        ViscoelasticModel solid = models[1];
        Assert.Equal(2.5, solid.RelaxationModulus(Q(0, "s")).In("GPa"), 9);
        Assert.Equal(0.5, solid.RelaxationModulus(Q(1000, "s")).In("GPa"), 6);
        Assert.Equal(2.5, solid.Dynamic(Q(1e6, "Hz")).Storage.In("GPa"), 4);
        Assert.Equal(0.5, solid.Dynamic(Q(1e-6, "Hz")).Storage.In("GPa"), 6);

        // Максвелл: потери наибольшие при ωτ = 1 и равны E/2
        ViscoelasticModel maxwell = models[0];
        Assert.Equal(0.5, maxwell.Dynamic(Q(1, "Hz")).Loss.In("GPa"), 12);
        Assert.True(maxwell.Dynamic(Q(2, "Hz")).Loss < maxwell.Dynamic(Q(1, "Hz")).Loss);

        // Кельвин — Фойгт: E·∫J + η·J(t) = t
        ViscoelasticModel kelvin = ViscoelasticModel.KelvinVoigt(Q(1, "GPa"), Q(2e9, "Pa·s"));
        double tau = 2, time = 3;
        double integral = (time - (tau * (1 - Math.Exp(-time / tau)))) / 1e9;
        Assert.Equal(time, (1e9 * integral) + (2e9 * kelvin.CreepCompliance(Q(time, "s")).SiValue), 9);
    }

    #endregion

    #region Вязкая жидкость

    [Fact]
    public void Poiseuille_ProfileIntegratesToFlowRate_AndMatchesDarcyWeisbach()
    {
        Quantity dp = Q(100, "Pa"), r = Q(1, "mm"), l = Q(1, "m"), mu = Q(1e-3, "Pa·s");
        double flow = ViscousFlow.PoiseuilleFlowRate(dp, r, l, mu).SiValue;

        const int Intervals = 1000;
        double h = 1e-3 / Intervals, integral = 0;

        for (int k = 0; k <= Intervals; k++)
        {
            double y = k * h;
            double weight = k == 0 || k == Intervals ? 1 : k % 2 == 1 ? 4 : 2;
            integral += weight * ViscousFlow.PoiseuilleVelocity(dp, r, l, mu, Q(y, "m")).SiValue * 2 * Math.PI * y;
        }

        Assert.Equal(flow, integral * h / 3, 1e-12);

        // При средней скорости Q/(πR²) закон Дарси — Вейсбаха с f = 64/Re даёт тот же перепад
        double speed = flow / (Math.PI * 1e-6);
        PipeFlowResult pipe = FlowDynamics.PipeFlow(Q(1000, "kg/m^3"), Q(speed, "m/s"), Q(2, "mm"), mu, l);

        Assert.Equal(FlowRegime.Laminar, pipe.Regime);
        Assert.Equal(100, pipe.PressureLoss.SiValue, 9);

        // Касательное на стенке — μ·|du/dr|
        double wall = 1e-3 * (ViscousFlow.PoiseuilleVelocity(dp, r, l, mu, Q(0.999e-3, "m")).SiValue / 1e-6);
        Assert.Equal(ViscousFlow.PoiseuilleWallShear(dp, r, l).SiValue, wall, 1e-3);
    }

    [Fact]
    public void Stokes_SettlingBalancesWeight_AndSuddenPlateSolvesDiffusion()
    {
        Quantity mu = Q(1e-3, "Pa·s"), radius = Q(20, "um");
        Quantity speed = ViscousFlow.StokesSettlingSpeed(Q(2650, "kg/m^3"), Q(1000, "kg/m^3"), radius, mu);
        double weight = 4.0 / 3 * Math.PI * Math.Pow(20e-6, 3) * 1650 * 9.80665;

        Assert.Equal(weight, ViscousFlow.StokesDrag(mu, radius, speed).SiValue, 1e-15);

        // u(y, t) = U·erfc(y/2√(νt)) удовлетворяет u_t = ν·u_yy
        Quantity nu = Q(1e-6, "m^2/s"), plate = Q(1, "m/s");
        double U(double y, double t) => ViscousFlow.SuddenlyStartedPlate(plate, nu, Q(y, "m"), Q(t, "s")).SiValue;

        double y0 = 1e-3, t0 = 1, dy = 1e-5, dt = 1e-3;
        double timeDerivative = (U(y0, t0 + dt) - U(y0, t0 - dt)) / (2 * dt);
        double curvature = (U(y0 + dy, t0) - (2 * U(y0, t0)) + U(y0 - dy, t0)) / (dy * dy);

        Assert.Equal(timeDerivative, 1e-6 * curvature, 2e-3);
        Assert.Equal(1, U(0, 1), 6);
    }

    [Fact]
    public void Blasius_SatisfiesMomentumIntegral_AndShapeFactor()
    {
        Quantity u = Q(5, "m/s"), nu = Q(1.5e-5, "m^2/s"), rho = Q(1.2, "kg/m^3");
        BoundaryLayer at = ViscousFlow.Blasius(u, Q(0.5, "m"), nu, rho);
        BoundaryLayer ahead = ViscousFlow.Blasius(u, Q(0.5001, "m"), nu, rho);
        BoundaryLayer behind = ViscousFlow.Blasius(u, Q(0.4999, "m"), nu, rho);

        // Интеграл импульса Кармана: dθ/dx = c_f/2
        double growth = (ahead.MomentumThickness.SiValue - behind.MomentumThickness.SiValue) / 2e-4;
        Assert.Equal(at.LocalSkinFriction / 2, growth, 1e-8);
        Assert.Equal(2.59, at.DisplacementThickness.SiValue / at.MomentumThickness.SiValue, 2);
        Assert.True(at.Laminar);
    }

    [Fact]
    public void Colebrook_IsSolvedExactly_AndSwameeJainIsWithinThreePercent()
    {
        foreach (double re in new[] { 5e3, 1e5, 1e7 })
        {
            foreach (double roughness in new[] { 0.0, 1e-4, 1e-2 })
            {
                double f = FlowDynamics.ColebrookFrictionFactor(re, roughness);
                double residual = (1 / Math.Sqrt(f)) + (2 * Math.Log10((roughness / 3.7) + (2.51 / (re * Math.Sqrt(f)))));

                Assert.True(Math.Abs(residual) < 1e-10);

                double approximate = FlowDynamics.PipeFlow(Q(1000, "kg/m^3"), Q(re * 1e-6 / 0.1, "m/s"), Q(0.1, "m"), Q(1e-3, "Pa·s"),
                    Q(1, "m"), Q(roughness * 0.1, "m")).FrictionFactor;
                // Прежде в коде стояло «не более процента»; на краю диапазона, Re = 5000 и ε/d = 0,01, — 2,8 %
                Assert.Equal(f, approximate, 0.03 * f);
            }
        }

        double corner = FlowDynamics.ColebrookFrictionFactor(5e3, 1e-2);
        double swameeJain = FlowDynamics.PipeFlow(Q(1000, "kg/m^3"), Q(0.05, "m/s"), Q(0.1, "m"), Q(1e-3, "Pa·s"), Q(1, "m"), Q(1, "mm")).FrictionFactor;
        Assert.True(Math.Abs(swameeJain - corner) / corner > 0.02);

        // Гладкая труба при Re = 10⁵ — 0,018 по диаграмме Муди
        Assert.Equal(0.018, FlowDynamics.ColebrookFrictionFactor(1e5, 0), 0.0003);
    }

    #endregion

    #region Газовая динамика

    [Fact]
    public void Isentropic_And_NormalShock_MatchGasTables()
    {
        IsentropicRatios m2 = GasDynamics.Isentropic(2);
        Assert.Equal(0.5556, m2.Temperature, 4);
        Assert.Equal(0.1278, m2.Pressure, 4);
        Assert.Equal(0.2300, m2.Density, 4);
        Assert.Equal(1.6875, m2.Area, 4);
        Assert.Equal(1, GasDynamics.Isentropic(1).Area, 12);

        NormalShock shock = GasDynamics.Shock(2);
        Assert.Equal(0.5774, shock.DownstreamMach, 4);
        Assert.Equal(4.5, shock.PressureRatio, 12);
        Assert.Equal(2.6667, shock.DensityRatio, 4);
        Assert.Equal(1.6875, shock.TemperatureRatio, 4);
        Assert.Equal(0.7209, shock.StagnationPressureRatio, 4);

        Assert.Equal(26.38, GasDynamics.PrandtlMeyerAngle(2), 2);
        Assert.Equal(0.5283, GasDynamics.CriticalPressureRatio(), 4);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GasDynamics.Shock(0.8));
    }

    [Theory]
    [InlineData(1.2, 1.4)]
    [InlineData(2.0, 1.4)]
    [InlineData(5.0, 1.4)]
    [InlineData(3.0, 5.0 / 3)]
    public void NormalShock_ConservesMassMomentumAndEnergy(double mach, double gamma)
    {
        NormalShock shock = GasDynamics.Shock(mach, gamma);

        // Перед скачком p = ρ = 1, тогда a² = γ
        double u1 = mach * Math.Sqrt(gamma);
        double p2 = shock.PressureRatio, rho2 = shock.DensityRatio;
        double u2 = shock.DownstreamMach * Math.Sqrt(gamma * p2 / rho2);

        Assert.Equal(u1, rho2 * u2, 9);
        Assert.Equal(1 + (u1 * u1), p2 + (rho2 * u2 * u2), 9);
        Assert.Equal((gamma / (gamma - 1)) + (u1 * u1 / 2), (gamma / (gamma - 1) * p2 / rho2) + (u2 * u2 / 2), 9);
        Assert.True(shock.EntropyRise > 0);
    }

    [Fact]
    public void Nozzle_InversesRoundTrip_AndChokedFlowIsSonicMassFlux()
    {
        foreach (double mach in new[] { 0.2, 0.7, 1.5, 3.0 })
        {
            double ratio = GasDynamics.Isentropic(mach).Area;
            Assert.Equal(mach, GasDynamics.MachFromAreaRatio(ratio, supersonic: mach > 1), 9);
        }

        Assert.Equal(2.5, GasDynamics.MachFromPrandtlMeyer(GasDynamics.PrandtlMeyerAngle(2.5)), 9);

        // ρ*·a*·A* из изоэнтропических отношений при M = 1
        double p0 = 1e6, t0 = 500, molar = 0.02897, gasConstant = 8.314462618 / molar;
        IsentropicRatios sonic = GasDynamics.Isentropic(1);
        double density = p0 / (gasConstant * t0) * sonic.Density;
        double speed = Math.Sqrt(1.4 * gasConstant * t0 * sonic.Temperature);

        Quantity flow = GasDynamics.ChokedMassFlow(Q(1, "cm^2"), Q(p0, "Pa"), Q(t0, "K"), Q(molar, "kg/mol"));
        Assert.Equal(density * speed * 1e-4, flow.SiValue, 9);
    }

    #endregion

    #region Инструменты

    private static SymmetricTensor RandomStress(Random rng)
    {
        double R() => (rng.NextDouble() - 0.5) * 400;

        return SymmetricTensor.Stress(Q(R(), "MPa"), Q(R(), "MPa"), Q(R(), "MPa"), Q(R(), "MPa"), Q(R(), "MPa"), Q(R(), "MPa"));
    }

    private static (Vector3, Vector3, Vector3) RandomBasis(Random rng)
    {
        Vector3 R() => new(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);

        Vector3 e1 = R().Normalized;
        Vector3 second = R();
        Vector3 e2 = (second - (e1 * second.Dot(e1))).Normalized;

        return (e1, e2, e1.Cross(e2));
    }

    #endregion
}
