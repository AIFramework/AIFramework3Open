using AI.DataStructs.Algebraic;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Тесты подсистемы физических величин: размерности, единицы, перенос неопределённости.
/// </summary>
public class UnitsTests
{
    private const double Tolerance = 1e-9;

    #region Размерности

    [Fact]
    public void Dimension_ForceEqualsMassTimesAcceleration()
    {
        Assert.Equal(Dimension.Force, Dimension.MassDim * Dimension.Acceleration);
    }

    [Fact]
    public void Dimension_EnergyDividedByTime_IsPower()
    {
        Assert.Equal(Dimension.Power, Dimension.Energy / Dimension.TimeDim);
    }

    [Fact]
    public void Dimension_Sqrt_RoundTripsThroughPow()
    {
        Assert.Equal(Dimension.LengthDim, Dimension.Area.Sqrt());
        Assert.Equal(Dimension.Area, Dimension.LengthDim.Pow(2));
    }

    [Fact]
    public void Dimension_HalfExponent_SurvivesMultiplication()
    {
        Dimension noiseDensity = Dimension.Voltage / Dimension.Frequency.Sqrt();
        Assert.Equal(0.5, noiseDensity.Time - Dimension.Voltage.Time, 12);
        Assert.Equal(Dimension.Voltage.Pow(2) / Dimension.Frequency, noiseDensity.Pow(2));
    }

    [Fact]
    public void Dimension_Sqrt_BeyondHalfExponent_Throws()
    {
        Dimension root = Dimension.Volume.Sqrt();
        Assert.Equal(1.5, root.Length, 12);

        _ = Assert.Throws<InvalidOperationException>(() => root.Sqrt());
    }

    [Fact]
    public void Dimension_ToString_UsesBaseSiSymbols()
    {
        Assert.Equal("kg·m²·s⁻³", Dimension.Power.ToString());
        Assert.Equal("1", Dimension.None.ToString());
    }

    #endregion

    #region Единицы и перевод

    [Fact]
    public void Unit_KilometrePerHour_ConvertsToMetrePerSecond()
    {
        Quantity speed = Quantity.Of(90.0, "km/h");
        Assert.Equal(25.0, speed.In(Si.MetrePerSecond), 12);
    }

    [Fact]
    public void Unit_DegreeCelsius_IsAffine()
    {
        Quantity temperature = Quantity.Of(25.0, Si.DegreeCelsius);
        Assert.Equal(298.15, temperature.In(Si.Kelvin), 12);
        Assert.Equal(77.0, temperature.In(Si.DegreeFahrenheit), 10);
    }

    [Fact]
    public void Unit_AffineUnit_CannotBeComposed()
    {
        _ = Assert.Throws<InvalidOperationException>(() => Si.DegreeCelsius * Si.Metre);
    }

    [Theory]
    [InlineData("kW·h", 3.6e6)]
    [InlineData("W·h", 3600.0)]
    [InlineData("hPa", 100.0)]
    [InlineData("mg", 1e-6)]
    [InlineData("km", 1000.0)]
    [InlineData("µs", 1e-6)]
    public void UnitRegistry_Parse_ResolvesPrefixesAndProducts(string symbol, double expectedFactor)
    {
        Unit unit = UnitRegistry.Parse(symbol);
        Assert.Equal(expectedFactor, unit.Factor, 12);
    }

    [Fact]
    public void UnitRegistry_Parse_ExactSymbolWinsOverPrefix()
    {
        Assert.Equal(Dimension.MagneticFluxDensity, UnitRegistry.Parse("T").Dimension);
        Assert.Equal(60.0, UnitRegistry.Parse("min").Factor, 12);
        Assert.Equal(3600.0, UnitRegistry.Parse("h").Factor, 12);
    }

    [Fact]
    public void UnitRegistry_Parse_HandlesExponentsAndDivision()
    {
        Assert.Equal(Dimension.Acceleration, UnitRegistry.Parse("m/s^2").Dimension);
        Assert.Equal(Dimension.Acceleration, UnitRegistry.Parse("m/s²").Dimension);
        Assert.Equal(Dimension.Density, UnitRegistry.Parse("kg/m^3").Dimension);
        Assert.Equal(Dimension.Volume / Dimension.MassDim / Dimension.TimeDim.Pow(2),
            UnitRegistry.Parse("m^3/kg/s^2").Dimension);
    }

    [Fact]
    public void UnitRegistry_Parse_UnknownSymbol_Throws()
    {
        _ = Assert.Throws<FormatException>(() => UnitRegistry.Parse("wat"));
    }

    #endregion

    #region Величины

    [Fact]
    public void Quantity_Parse_ReadsValueAndUnit()
    {
        Quantity g = Quantity.Parse("9.81 m/s^2");
        Assert.Equal(Dimension.Acceleration, g.Dimension);
        Assert.Equal(9.81, g.SiValue, 12);
    }

    [Fact]
    public void Quantity_Parse_ScientificNotation()
    {
        Quantity q = Quantity.Parse("1.5e-3 kg");
        Assert.Equal(1.5e-3, q.SiValue, 15);
    }

    [Fact]
    public void Quantity_MassTimesAcceleration_GivesForceInNewtons()
    {
        Quantity mass = Quantity.Of(2.0, Si.Kilogram);
        Quantity acceleration = Quantity.Of(3.0, Si.MetrePerSecondSquared);
        Quantity force = mass * acceleration;

        Assert.Equal(Dimension.Force, force.Dimension);
        Assert.Equal(6.0, force.In(Si.Newton), 12);
    }

    [Fact]
    public void Quantity_AddingDifferentDimensions_Throws()
    {
        Quantity length = Quantity.Of(1.0, Si.Metre);
        Quantity time = Quantity.Of(1.0, Si.Second);

        _ = Assert.Throws<DimensionMismatchException>(() => length + time);
    }

    [Fact]
    public void Quantity_In_WrongDimension_Throws()
    {
        Quantity energy = Quantity.Of(1.0, Si.Joule);
        _ = Assert.Throws<DimensionMismatchException>(() => energy.In(Si.Watt));
    }

    [Fact]
    public void Quantity_RequireSi_ChecksApiBoundary()
    {
        Quantity frequency = Quantity.Of(2.4, "GHz");
        Assert.Equal(2.4e9, frequency.RequireSi(Dimension.Frequency, "frequency"), 6);

        DimensionMismatchException error = Assert.Throws<DimensionMismatchException>(
            () => frequency.RequireSi(Dimension.LengthDim, "wavelength"));

        Assert.Equal(Dimension.LengthDim, error.Expected);
        Assert.Equal(Dimension.Frequency, error.Actual);
        Assert.Contains("wavelength", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Quantity_Comparison_WorksWithinDimension()
    {
        Assert.True(Quantity.Of(1.0, "km") > Quantity.Of(900.0, Si.Metre));
        Assert.True(Quantity.Of(1.0, "h") == Quantity.Of(3600.0, Si.Second));
    }

    [Fact]
    public void Quantity_ToString_UsesDisplayUnit()
    {
        Assert.Equal("9.81 m/s²", Quantity.Of(9.81, Si.MetrePerSecondSquared).ToString());
        Assert.Equal("1500 W", Quantity.Of(1.5, "kW").ToString());
        Assert.Equal("25 °C", Quantity.Of(25.0, Si.DegreeCelsius).ToString(Si.DegreeCelsius));
    }

    [Fact]
    public void Quantity_WavelengthFromFrequency_MatchesPhysics()
    {
        Quantity frequency = Quantity.Of(2.45, "GHz");
        Quantity wavelength = PhysicalConstants.SpeedOfLight / frequency;

        Assert.Equal(Dimension.LengthDim, wavelength.Dimension);
        Assert.Equal(122.36, wavelength.In("mm"), 2);
    }

    #endregion

    #region Неопределённость

    [Fact]
    public void Measurement_Sum_AddsUncertaintiesInQuadrature()
    {
        var a = Measurement.Of(10.0, 0.1, Si.Metre);
        var b = Measurement.Of(20.0, 0.2, Si.Metre);
        Measurement sum = a + b;

        Assert.Equal(30.0, sum.Value.In(Si.Metre), 12);
        Assert.Equal(Math.Sqrt(0.05), sum.SiUncertainty, 12);
    }

    [Fact]
    public void Measurement_Product_AddsRelativeUncertaintiesInQuadrature()
    {
        var a = Measurement.Relative(Quantity.Of(4.0, Si.Metre), 0.01);
        var b = Measurement.Relative(Quantity.Of(5.0, Si.Metre), 0.02);
        Measurement product = a * b;

        Assert.Equal(20.0, product.Value.In(Si.SquareMetre), 12);
        Assert.Equal(Math.Sqrt(0.0001 + 0.0004), product.RelativeUncertainty, 12);
    }

    [Fact]
    public void Measurement_Pow_ScalesRelativeUncertainty()
    {
        var side = Measurement.Relative(Quantity.Of(2.0, Si.Metre), 0.01);
        Measurement volume = side.Pow(3);

        Assert.Equal(8.0, volume.Value.In(Si.CubicMetre), 12);
        Assert.Equal(0.03, volume.RelativeUncertainty, 12);
        Assert.Equal("8 ± 0.12 m³", Measurement.Of(2.00, 0.01, Si.Metre).Pow(3).ToString());
    }

    [Fact]
    public void Measurement_UnitConversion_ScalesUncertainty()
    {
        var length = Measurement.Of(1.0, 0.005, Si.Metre);
        Assert.Equal(5.0, length.UncertaintyIn(UnitRegistry.Parse("mm")), 9);
    }

    [Fact]
    public void Measurement_Interval_CoversValue()
    {
        var m = Measurement.Of(9.80, 0.02, Si.MetrePerSecondSquared);
        (Quantity low, Quantity high) = m.Interval(2.0);

        Assert.Equal(9.76, low.SiValue, 12);
        Assert.Equal(9.84, high.SiValue, 12);
    }

    [Fact]
    public void Measurement_IsConsistentWith_ComparesWithinUncertainty()
    {
        var first = Measurement.Of(9.79, 0.03, Si.MetrePerSecondSquared);
        var second = Measurement.Of(9.82, 0.03, Si.MetrePerSecondSquared);
        var distant = Measurement.Of(10.50, 0.03, Si.MetrePerSecondSquared);

        Assert.True(first.IsConsistentWith(second));
        Assert.False(first.IsConsistentWith(distant));
    }

    [Fact]
    public void Measurement_ToString_ShowsUncertainty()
    {
        var m = Measurement.Of(9.81, 0.02, Si.MetrePerSecondSquared);
        Assert.Equal("9.81 ± 0.02 m/s²", m.ToString("0.##", null));
    }

    #endregion

    #region Константы

    [Fact]
    public void PhysicalConstants_GasConstant_EqualsAvogadroTimesBoltzmann()
    {
        Quantity product = PhysicalConstants.AvogadroConstant * PhysicalConstants.BoltzmannConstant;
        Assert.True(product.AlmostEquals(PhysicalConstants.GasConstant, 1e-12));
    }

    [Fact]
    public void PhysicalConstants_FaradayConstant_EqualsAvogadroTimesElementaryCharge()
    {
        Quantity product = PhysicalConstants.AvogadroConstant * PhysicalConstants.ElementaryCharge;
        Assert.True(product.AlmostEquals(PhysicalConstants.FaradayConstant, 1e-12));
    }

    [Fact]
    public void PhysicalConstants_VacuumRelation_HoldsToMeasuredPrecision()
    {
        Quantity product = PhysicalConstants.VacuumPermittivity
            * PhysicalConstants.VacuumPermeability
            * PhysicalConstants.SpeedOfLight.Pow(2);

        Assert.Equal(1.0, product.Value, 8);
    }

    [Fact]
    public void PhysicalConstants_Dimensions_AreCorrect()
    {
        Assert.Equal(Dimension.Velocity, PhysicalConstants.SpeedOfLight.Dimension);
        Assert.Equal(Dimension.Energy * Dimension.TimeDim, PhysicalConstants.PlanckConstant.Dimension);
        Assert.Equal(Dimension.Acceleration, PhysicalConstants.StandardGravity.Dimension);
        Assert.True(PhysicalConstants.FineStructureConstant.Dimension.IsDimensionless);
    }

    [Fact]
    public void PhysicalConstants_GravitationalConstant_CarriesCodataUncertainty()
    {
        Measurement g = PhysicalConstants.WithUncertainty.GravitationalConstant;
        Assert.Equal(6.67430e-11, g.Value.SiValue, 15);
        Assert.Equal(0.00015e-11, g.SiUncertainty, 15);
        Assert.Equal(2.2e-5, g.RelativeUncertainty, 6);
    }

    [Fact]
    public void PhysicalConstants_PhotonEnergy_MatchesElectronVolt()
    {
        Quantity frequency = Quantity.Of(1.0, "PHz");
        Quantity energy = PhysicalConstants.PlanckConstant * frequency;

        Assert.Equal(Dimension.Energy, energy.Dimension);
        Assert.Equal(4.1357, energy.In(Si.ElectronVolt), 3);
    }

    #endregion

    #region Ряды величин

    [Fact]
    public void QuantityVector_ConvertsWholeSeriesAtApiBoundary()
    {
        var speeds = QuantityVector.Of(new Vector(90.0, 120.0, 60.0), "km/h");
        Vector si = speeds.ToVector(Si.MetrePerSecond);

        Assert.Equal(3, speeds.Count);
        Assert.Equal(25.0, si[0], 12);
        Assert.Equal(Dimension.Velocity, speeds.Dimension);
    }

    [Fact]
    public void QuantityVector_Mean_KeepsDimension()
    {
        var speeds = QuantityVector.Of(new Vector(10.0, 20.0, 30.0), Si.MetrePerSecond);
        Quantity mean = speeds.Mean();

        Assert.Equal(20.0, mean.In(Si.MetrePerSecond), 12);
        Assert.Equal(Dimension.Velocity, mean.Dimension);
    }

    [Fact]
    public void QuantityVector_RequireSi_RejectsWrongDimension()
    {
        var series = QuantityVector.Of(new Vector(1.0, 2.0), Si.Second);
        _ = Assert.Throws<DimensionMismatchException>(() => series.RequireSi(Dimension.LengthDim, "distances"));
    }

    [Fact]
    public void QuantityVector_MultiplyByQuantity_CombinesDimensions()
    {
        var times = QuantityVector.Of(new Vector(1.0, 2.0), Si.Second);
        QuantityVector distances = times * Quantity.Of(3.0, Si.MetrePerSecond);

        Assert.Equal(Dimension.LengthDim, distances.Dimension);
        Assert.Equal(6.0, distances[1].In(Si.Metre), Tolerance);
    }

    #endregion
}
