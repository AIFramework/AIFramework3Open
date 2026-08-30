using AI.Insights;
using AI.Physics.Electromagnetism;
using AI.Physics.Fluids;
using AI.Physics.Mechanics;
using AI.Physics.Optics;
using AI.Physics.Thermodynamics;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Физика проверяется задачами со справочным ответом и законами сохранения:
/// импульс сохраняется в любом ударе, энергия — только в упругом, КПД никакой машины
/// не превосходит карнотовского.
/// </summary>
public class PhysicsTests
{
    private static Quantity Si(double value, string unit) => Quantity.Of(value, unit);

    #region Кинематика

    [Fact]
    public void Kinematics_FreeFall_MatchesTextbookValues()
    {
        // Падение с 20 м: t = √(2h/g) ≈ 2.02 с
        Quantity time = Kinematics.FreeFallTime(Si(20, "m"));

        Assert.Equal(2.0193, time.In(Si_.Second), tolerance: 1e-3);
    }

    [Fact]
    public void Kinematics_StoppingDistance_ScalesWithSquareOfSpeed()
    {
        Quantity slow = Kinematics.StoppingDistance(Si(60, "km/h"), Si(6, "m/s^2"));
        Quantity fast = Kinematics.StoppingDistance(Si(120, "km/h"), Si(6, "m/s^2"));

        // Удвоение скорости учетверяет тормозной путь
        Assert.Equal(4.0, fast.SiValue / slow.SiValue, tolerance: 1e-9);
        Assert.Equal(23.15, slow.In(Si_.Metre), tolerance: 0.01);
    }

    [Fact]
    public void Kinematics_RejectsWrongDimension()
    {
        _ = Assert.Throws<DimensionMismatchException>(
            () => Kinematics.FreeFallTime(Si(20, "s")));
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(45.0)]
    [InlineData(60.0)]
    public void Projectile_Range_MatchesClosedForm(double angle)
    {
        Quantity speed = Si(20, "m/s");
        TrajectoryResult result = Projectile.Launch(speed, angle);

        double g = 9.80665;
        double expected = 20.0 * 20.0 * Math.Sin(2 * angle * Math.PI / 180) / g;

        Assert.Equal(expected, result.Range.In(Si_.Metre), tolerance: 1e-9);
    }

    [Fact]
    public void Projectile_FortyFiveDegrees_GivesMaximumRange()
    {
        Quantity speed = Si(30, "m/s");

        double best = Projectile.Launch(speed, Projectile.OptimalAngleDegrees).Range.SiValue;

        foreach (double angle in new[] { 20.0, 35.0, 50.0, 70.0 })
            Assert.True(Projectile.Launch(speed, angle).Range.SiValue <= best + 1e-9);
    }

    [Fact]
    public void Projectile_ComplementaryAngles_GiveEqualRange()
    {
        Quantity speed = Si(25, "m/s");

        Assert.Equal(
            Projectile.Launch(speed, 30).Range.SiValue,
            Projectile.Launch(speed, 60).Range.SiValue,
            tolerance: 1e-9);
    }

    #endregion

    #region Динамика

    [Fact]
    public void Oscillator_Period_MatchesFormula()
    {
        var oscillator = new HarmonicOscillator(Si(0.5, "kg"), Si(200, "N/m"));

        Assert.Equal(2 * Math.PI * Math.Sqrt(0.5 / 200), oscillator.Period.SiValue, tolerance: 1e-12);
        Assert.Equal(DampingRegime.Undamped, oscillator.Regime);
    }

    [Fact]
    public void Oscillator_ClassifiesDampingRegimes()
    {
        Quantity mass = Si(1, "kg");
        Quantity stiffness = Si(100, "N/m");
        double critical = 2 * Math.Sqrt(100 * 1.0);

        Assert.Equal(DampingRegime.Underdamped,
            new HarmonicOscillator(mass, stiffness, Si(critical * 0.3, "N·s/m")).Regime);
        Assert.Equal(DampingRegime.Critical,
            new HarmonicOscillator(mass, stiffness, Si(critical, "N·s/m")).Regime);
        Assert.Equal(DampingRegime.Overdamped,
            new HarmonicOscillator(mass, stiffness, Si(critical * 2, "N·s/m")).Regime);
    }

    [Fact]
    public void Oscillator_UndampedMotion_ReturnsToStartAfterPeriod()
    {
        var oscillator = new HarmonicOscillator(Si(1, "kg"), Si(4, "N/m"));
        Quantity start = Si(0.1, "m");

        Assert.Equal(0.1, oscillator.Displacement(start, oscillator.Period).In(Si_.Metre), tolerance: 1e-9);
        Assert.Equal(-0.1, oscillator.Displacement(start, oscillator.Period / 2).In(Si_.Metre), tolerance: 1e-9);
    }

    [Fact]
    public void Collision_Elastic_ConservesMomentumAndEnergy()
    {
        Quantity m1 = Si(2, "kg"), m2 = Si(3, "kg");
        Quantity u1 = Si(5, "m/s"), u2 = Si(-1, "m/s");

        CollisionResult result = Collisions.Collide(m1, u1, m2, u2, restitution: 1.0);

        double momentumBefore = (2 * 5) + (3 * -1);
        double momentumAfter = (2 * result.FirstSpeed.SiValue) + (3 * result.SecondSpeed.SiValue);

        Assert.Equal(momentumBefore, momentumAfter, tolerance: 1e-9);
        Assert.Equal(0.0, result.EnergyLoss.SiValue, tolerance: 1e-9);
    }

    [Fact]
    public void Collision_PerfectlyInelastic_BodiesMoveTogether()
    {
        CollisionResult result = Collisions.Collide(
            Si(2, "kg"), Si(6, "m/s"), Si(4, "kg"), Si(0, "m/s"), restitution: 0.0);

        Assert.Equal(result.FirstSpeed.SiValue, result.SecondSpeed.SiValue, tolerance: 1e-12);
        Assert.Equal(2.0, result.FirstSpeed.In(Si_.MetrePerSecond), tolerance: 1e-9);

        // Потеря энергии при слипании: 24 Дж из 36
        Assert.Equal(24.0, result.EnergyLoss.In(Si_.Joule), tolerance: 1e-9);
    }

    [Fact]
    public void RigidBody_MomentsOfInertia_MatchReferenceValues()
    {
        Quantity mass = Si(2, "kg");
        Quantity radius = Si(0.5, "m");

        Assert.Equal(0.25, RigidBody.SolidCylinder(mass, radius).SiValue, tolerance: 1e-12);
        Assert.Equal(0.5, RigidBody.Hoop(mass, radius).SiValue, tolerance: 1e-12);
        Assert.Equal(0.2, RigidBody.SolidSphere(mass, radius).SiValue, tolerance: 1e-12);
    }

    [Fact]
    public void RigidBody_ParallelAxis_MatchesRodAboutEnd()
    {
        Quantity mass = Si(3, "kg");
        Quantity length = Si(2, "m");

        Quantity central = RigidBody.RodAboutCentre(mass, length);
        Quantity shifted = RigidBody.ParallelAxis(central, mass, length / 2);

        // Теорема Гюйгенса — Штейнера обязана дать формулу для оси через конец
        Assert.Equal(RigidBody.RodAboutEnd(mass, length).SiValue, shifted.SiValue, tolerance: 1e-12);
    }

    #endregion

    #region Орбиты

    [Fact]
    public void Orbits_LowEarthOrbit_MatchesKnownSpeed()
    {
        Quantity earth = Orbits.GravitationalParameter(Si(5.972e24, "kg"));
        Quantity radius = Si(6771, "km");   // высота 400 км

        Assert.Equal(7672, Orbits.CircularSpeed(earth, radius).In(Si_.MetrePerSecond), tolerance: 5);
    }

    [Fact]
    public void Orbits_EscapeSpeed_IsRootTwoTimesCircular()
    {
        Quantity earth = Orbits.GravitationalParameter(Si(5.972e24, "kg"));
        Quantity radius = Si(6371, "km");

        double circular = Orbits.CircularSpeed(earth, radius).SiValue;
        double escape = Orbits.EscapeSpeed(earth, radius).SiValue;

        Assert.Equal(Math.Sqrt(2), escape / circular, tolerance: 1e-12);
        Assert.Equal(11186, escape, tolerance: 10);
    }

    [Fact]
    public void Orbits_GeostationaryRadius_MatchesKnownValue()
    {
        Quantity earth = Orbits.GravitationalParameter(Si(5.972e24, "kg"));
        Quantity day = Si(86164, "s");   // звёздные сутки

        Assert.Equal(42164, Orbits.RadiusForPeriod(earth, day).In(UnitRegistry.Parse("km")), tolerance: 10);
    }

    [Fact]
    public void Orbits_VisViva_AgreesWithCircularSpeed()
    {
        Quantity earth = Orbits.GravitationalParameter(Si(5.972e24, "kg"));
        Quantity radius = Si(7000, "km");

        // На круговой орбите большая полуось равна радиусу
        Assert.Equal(
            Orbits.CircularSpeed(earth, radius).SiValue,
            Orbits.VisViva(earth, radius, radius).SiValue,
            tolerance: 1e-9);
    }

    #endregion

    #region Термодинамика

    [Fact]
    public void IdealGas_MolarVolume_MatchesStandardConditions()
    {
        // При 0 °C и 101325 Па моль занимает 22.41 литра
        Quantity volume = IdealGas.Volume(Si(1, "mol"), Quantity.Of(0, Si_.Celsius), Si(101325, "Pa"));

        Assert.Equal(22.414, volume.In(UnitRegistry.Parse("L")), tolerance: 0.01);
    }

    [Fact]
    public void IdealGas_IsothermalWork_MatchesLogarithm()
    {
        Quantity work = IdealGas.IsothermalWork(Si(2, "mol"), Si(300, "K"), Si(1, "L"), Si(2, "L"));

        double expected = 2 * 8.31446261815324 * 300 * Math.Log(2);

        Assert.Equal(expected, work.In(Si_.Joule), tolerance: 1e-6);
    }

    [Fact]
    public void IdealGas_AdiabaticCompression_RaisesTemperature()
    {
        // Сжатие вдвое для двухатомного газа: T₂ = T₁·2^0.4
        Quantity result = IdealGas.AdiabaticTemperature(Si(300, "K"), Si(2, "L"), Si(1, "L"));

        Assert.Equal(300 * Math.Pow(2, 0.4), result.In(Si_.Kelvin), tolerance: 1e-9);
    }

    [Fact]
    public void IdealGas_SpeedOfSound_MatchesAirValue()
    {
        // Воздух при 20 °C: около 343 м/с
        Quantity speed = IdealGas.SpeedOfSound(
            Quantity.Of(20, Si_.Celsius), Si(0.0289644, "kg/mol"));

        Assert.Equal(343, speed.In(Si_.MetrePerSecond), tolerance: 2);
    }

    [Fact]
    public void Cycles_CarnotEfficiency_BoundsOtherCycles()
    {
        // Отто со степенью сжатия 10 работает между теми же температурами хуже Карно
        CycleResult otto = Cycles.Otto(10);
        CycleResult carnot = Cycles.Carnot(Si(1000, "K"), Si(300, "K"));

        Assert.Equal(0.602, otto.Efficiency, tolerance: 1e-3);
        Assert.Equal(0.7, carnot.Efficiency, tolerance: 1e-9);
        Assert.True(otto.Efficiency < carnot.Efficiency);
    }

    [Fact]
    public void Cycles_RejectsColdSourceAboveHot()
    {
        _ = Assert.Throws<ArgumentException>(() => Cycles.Carnot(Si(300, "K"), Si(400, "K")));
    }

    [Fact]
    public void Cycles_HeatPump_IsBetterThanDirectHeating()
    {
        double coefficient = Cycles.HeatPumpCoefficient(Si(293, "K"), Si(273, "K"));

        // Тепловой насос между 0 и 20 °C даёт больше десяти киловатт тепла на киловатт работы
        Assert.True(coefficient > 10);
        Assert.Equal(14.65, coefficient, tolerance: 0.01);
    }

    [Fact]
    public void HeatTransfer_Radiation_FollowsFourthPower()
    {
        Quantity cool = HeatTransfer.Radiation(Si(300, "K"));
        Quantity hot = HeatTransfer.Radiation(Si(600, "K"));

        // Удвоение температуры увеличивает поток в шестнадцать раз
        Assert.Equal(16.0, hot.SiValue / cool.SiValue, tolerance: 1e-9);
        Assert.Equal(459.3, cool.SiValue, tolerance: 0.1);
    }

    [Fact]
    public void HeatTransfer_Conduction_MatchesFourierLaw()
    {
        // Кирпичная стена: λ = 0.6, толщина 0.5 м, перепад 25 К
        Quantity flux = HeatTransfer.Conduction(Si(0.6, "W/m/K"), Si(25, "K"), Si(0.5, "m"));

        Assert.Equal(30.0, flux.SiValue, tolerance: 1e-9);
    }

    #endregion

    #region Электричество

    [Fact]
    public void Coulomb_ForceBetweenElementaryCharges_MatchesReference()
    {
        Quantity force = Electrostatics.CoulombForce(
            PhysicalConstants.ElementaryCharge, PhysicalConstants.ElementaryCharge, Si(1, "nm"));

        // Два элементарных заряда на нанометре отталкиваются с силой около 0.23 нН
        Assert.Equal(2.307e-10, force.SiValue, tolerance: 1e-13);
    }

    [Fact]
    public void Capacitor_Energy_MatchesFormula()
    {
        Quantity capacitance = Si(100, "µF");
        Quantity energy = Electrostatics.CapacitorEnergy(capacitance, Si(12, "V"));

        Assert.Equal(0.5 * 100e-6 * 144, energy.In(Si_.Joule), tolerance: 1e-12);
    }

    [Fact]
    public void Circuit_ResonanceFrequency_MatchesThomsonFormula()
    {
        Quantity frequency = Circuits.ResonanceFrequency(Si(1, "mH"), Si(1, "µF"));

        Assert.Equal(5032.9, frequency.In(Si_.Hertz), tolerance: 0.1);
    }

    [Fact]
    public void Circuit_ImpedanceAtResonance_EqualsResistance()
    {
        Quantity inductance = Si(1, "mH");
        Quantity capacitance = Si(1, "µF");
        Quantity resistance = Si(10, "Ω");

        Quantity resonance = Circuits.ResonanceFrequency(inductance, capacitance);
        Quantity impedance = Circuits.Impedance(resistance, inductance, capacitance, resonance);

        // На резонансе реактивные сопротивления компенсируются
        Assert.Equal(10.0, impedance.In(Si_.Ohm), tolerance: 1e-6);
    }

    [Fact]
    public void Circuit_ChargingVoltage_ReachesSixtyThreePercentAfterTau()
    {
        Quantity tau = Circuits.TimeConstantRC(Si(1, "kΩ"), Si(1, "µF"));
        Quantity voltage = Circuits.ChargingVoltage(Si(10, "V"), tau, tau);

        Assert.Equal(1e-3, tau.In(Si_.Second), tolerance: 1e-12);
        Assert.Equal(10 * (1 - Math.Exp(-1)), voltage.In(Si_.Volt), tolerance: 1e-9);
    }

    #endregion

    #region Оптика

    [Fact]
    public void Optics_Snell_MatchesWaterAir()
    {
        // Из воздуха в воду под 30°
        double angle = GeometricOptics.RefractionAngleDegrees(30, 1.0, 1.333);

        Assert.Equal(22.03, angle, tolerance: 0.01);
    }

    [Fact]
    public void Optics_CriticalAngle_ForWaterMatchesReference()
    {
        Assert.Equal(48.6, GeometricOptics.CriticalAngleDegrees(1.333, 1.0), tolerance: 0.05);
        Assert.True(double.IsNaN(GeometricOptics.CriticalAngleDegrees(1.0, 1.333)));
    }

    [Fact]
    public void Optics_ThinLens_MatchesTextbookProblem()
    {
        // Предмет на 30 см от собирающей линзы с фокусом 10 см: изображение на 15 см
        Quantity image = GeometricOptics.ImageDistance(Si(10, "cm"), Si(30, "cm"));

        Assert.Equal(15.0, image.In(UnitRegistry.Parse("cm")), tolerance: 1e-9);
        Assert.Equal(-0.5, GeometricOptics.Magnification(Si(30, "cm"), image), tolerance: 1e-9);
    }

    [Fact]
    public void Optics_ObjectInFocalPlane_GivesImageAtInfinity()
    {
        Quantity image = GeometricOptics.ImageDistance(Si(10, "cm"), Si(10, "cm"));

        Assert.True(double.IsInfinity(image.SiValue));
    }

    [Fact]
    public void Optics_DoubleSlit_FringeSpacingMatchesFormula()
    {
        Quantity spacing = WaveOptics.FringeSpacing(Si(550, "nm"), Si(0.5, "mm"), Si(2, "m"));

        Assert.Equal(2.2, spacing.In(UnitRegistry.Parse("mm")), tolerance: 1e-6);
    }

    [Fact]
    public void Optics_PhotonEnergy_MatchesElectronVolts()
    {
        // Зелёный свет 550 нм: около 2.25 эВ
        Quantity energy = WaveOptics.PhotonEnergy(Si(550, "nm"));

        Assert.Equal(2.254, energy.In(Si_.ElectronVolt), tolerance: 1e-3);
    }

    [Fact]
    public void Optics_RayleighResolution_ImprovesWithAperture()
    {
        double small = WaveOptics.RayleighResolution(Si(550, "nm"), Si(50, "mm"));
        double large = WaveOptics.RayleighResolution(Si(550, "nm"), Si(200, "mm"));

        Assert.Equal(4.0, small / large, tolerance: 1e-12);
    }

    #endregion

    #region Гидродинамика

    [Fact]
    public void Hydrostatics_PressureAtDepth_MatchesReference()
    {
        // Десять метров воды дают примерно одну атмосферу
        Quantity pressure = Hydrostatics.Pressure(Si(1000, "kg/m^3"), Si(10, "m"));

        Assert.Equal(98066.5, pressure.In(Si_.Pascal), tolerance: 1);

        // Ровно одну атмосферу даёт столб 10.33 м, а не 10: расхожее «десять метров — атмосфера»
        // округляет на три процента
        Assert.Equal(0.9679, pressure.In(Si_.Atmosphere), tolerance: 1e-3);
    }

    [Fact]
    public void Hydrostatics_Ice_FloatsWithNineTenthsSubmerged()
    {
        double fraction = Hydrostatics.SubmergedFraction(Si(917, "kg/m^3"), Si(1000, "kg/m^3"));

        Assert.Equal(0.917, fraction, tolerance: 1e-9);
    }

    [Fact]
    public void Fluids_Torricelli_MatchesFreeFallSpeed()
    {
        Quantity speed = FlowDynamics.TorricelliSpeed(Si(5, "m"));

        // Скорость истечения равна скорости свободного падения с той же высоты
        Assert.Equal(Math.Sqrt(2 * 9.80665 * 5), speed.In(Si_.MetrePerSecond), tolerance: 1e-12);
    }

    [Fact]
    public void Fluids_Bernoulli_LowersPressureWhereFlowIsFaster()
    {
        Quantity fast = FlowDynamics.ContinuitySpeed(Si(2, "m/s"), Si(0.01, "m^2"), Si(0.005, "m^2"));
        Quantity pressure = FlowDynamics.BernoulliPressure(
            Si(200, "kPa"), Si(1000, "kg/m^3"), Si(2, "m/s"), fast);

        Assert.Equal(4.0, fast.In(Si_.MetrePerSecond), tolerance: 1e-12);
        Assert.Equal(194000, pressure.In(Si_.Pascal), tolerance: 1e-6);
    }

    [Fact]
    public void Fluids_LaminarPipe_UsesSixtyFourOverReynolds()
    {
        PipeFlowResult result = FlowDynamics.PipeFlow(
            Si(1000, "kg/m^3"), Si(0.01, "m/s"), Si(0.01, "m"), Si(1e-3, "Pa·s"), Si(10, "m"));

        Assert.Equal(FlowRegime.Laminar, result.Regime);
        Assert.Equal(100, result.Reynolds, tolerance: 1e-9);
        Assert.Equal(0.64, result.FrictionFactor, tolerance: 1e-9);
    }

    [Fact]
    public void Fluids_TurbulentPipe_IsRecognized()
    {
        PipeFlowResult result = FlowDynamics.PipeFlow(
            Si(1000, "kg/m^3"), Si(2, "m/s"), Si(0.05, "m"), Si(1e-3, "Pa·s"), Si(100, "m"),
            roughness: Si(0.05, "mm"));

        Assert.Equal(FlowRegime.Turbulent, result.Regime);
        Assert.Equal(100000, result.Reynolds, tolerance: 1);
        Assert.True(result.FrictionFactor is > 0.015 and < 0.03);

        Interpretation interpretation = result.Interpret();
        Assert.Contains(interpretation.Findings, f => f.Contains("шероховатости", StringComparison.Ordinal));
    }

    [Fact]
    public void Fluids_TerminalSpeed_MatchesSkydiverEstimate()
    {
        // Парашютист 80 кг, площадь 0.7 м², коэффициент 1.0: около 55 м/с
        Quantity speed = FlowDynamics.TerminalSpeed(
            Si(80, "kg"), 1.0, Si(1.225, "kg/m^3"), Si(0.7, "m^2"));

        Assert.Equal(42.8, speed.In(Si_.MetrePerSecond), tolerance: 0.5);
    }

    #endregion

    #region Объяснимость

    [Fact]
    public void Interpret_Oscillator_ExplainsRegimeAndLimits()
    {
        var oscillator = new HarmonicOscillator(Si(1, "kg"), Si(100, "N/m"), Si(4, "N·s/m"));
        Interpretation interpretation = oscillator.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Добротность");
        Assert.Contains(interpretation.Findings, f => f.Contains("колеблется", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("линейная", StringComparison.Ordinal));
    }

    [Fact]
    public void Interpret_Cycle_WarnsAboutIdealisation()
    {
        Interpretation interpretation = Cycles.Carnot(Si(800, "K"), Si(300, "K")).Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "КПД");
        Assert.Contains(interpretation.Warnings, w => w.Contains("идеализирован", StringComparison.Ordinal));
    }

    #endregion

    /// <summary>Часто используемые единицы — чтобы не разбирать строку в каждом утверждении</summary>
    private static class Si_
    {
        internal static Unit Metre => AI.Units.Si.Metre;
        internal static Unit Second => AI.Units.Si.Second;
        internal static Unit Kelvin => AI.Units.Si.Kelvin;
        internal static Unit Celsius => AI.Units.Si.DegreeCelsius;
        internal static Unit Joule => AI.Units.Si.Joule;
        internal static Unit Volt => AI.Units.Si.Volt;
        internal static Unit Ohm => AI.Units.Si.Ohm;
        internal static Unit Hertz => AI.Units.Si.Hertz;
        internal static Unit Pascal => AI.Units.Si.Pascal;
        internal static Unit Atmosphere => AI.Units.Si.Atmosphere;
        internal static Unit ElectronVolt => AI.Units.Si.ElectronVolt;
        internal static Unit MetrePerSecond => AI.Units.Si.MetrePerSecond;
    }
}
