using AI.Microwave.Physics;
using AI.Solvers.Chem.Crystallography;
using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Structures;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Тесты типизированных границ API: домены принимают <see cref="Quantity"/>,
/// проверяют размерность и возвращают величину, а не безымянное число.
/// </summary>
public class UnitsBoundaryTests
{
    private static readonly Quantity CopperConductivity = Quantity.Of(5.8e7, "S/m");

    #region СВЧ

    [Fact]
    public void Microwave_Wavelength_MatchesUntypedCalculation()
    {
        Quantity lambda = MicrowaveQuantities.Wavelength(Quantity.Of(2.45, "GHz"));

        Assert.Equal(Dimension.LengthDim, lambda.Dimension);
        Assert.Equal(122.364, lambda.In("mm"), 3);
        Assert.Equal(MicrowavePhysics.Wavelength(2.45e9), lambda.In(Si.Metre), 12);
    }

    [Fact]
    public void Microwave_Wavelength_RejectsWrongDimension()
    {
        DimensionMismatchException error = Assert.Throws<DimensionMismatchException>(
            () => MicrowaveQuantities.Wavelength(Quantity.Of(2.45, Si.Metre)));

        Assert.Equal(Dimension.Frequency, error.Expected);
        Assert.Equal(Dimension.LengthDim, error.Actual);
    }

    [Fact]
    public void Microwave_SkinDepth_ForCopper_IsMicrometreScale()
    {
        Quantity depth = MicrowaveQuantities.SkinDepth(Quantity.Of(2.45, "GHz"), CopperConductivity);

        Assert.Equal(Dimension.LengthDim, depth.Dimension);
        Assert.Equal(1.335, depth.In("µm"), 3);
    }

    [Fact]
    public void Microwave_SurfaceResistance_HasResistanceDimension()
    {
        Quantity resistance = MicrowaveQuantities.SurfaceResistance(Quantity.Of(2.45, "GHz"), CopperConductivity);

        Assert.Equal(Dimension.Resistance, resistance.Dimension);
        Assert.Equal(MicrowavePhysics.SurfaceResistance(2.45e9, 5.8e7), resistance.In(Si.Ohm), 12);
    }

    [Fact]
    public void Microwave_FarFieldDistance_ScalesWithApertureSquared()
    {
        Quantity lambda = MicrowaveQuantities.Wavelength(Quantity.Of(10.0, "GHz"));
        Quantity near = MicrowaveQuantities.FarFieldDistance(Quantity.Of(0.5, Si.Metre), lambda);
        Quantity far = MicrowaveQuantities.FarFieldDistance(Quantity.Of(1.0, Si.Metre), lambda);

        Assert.Equal(4.0, far.In(Si.Metre) / near.In(Si.Metre), 9);
    }

    [Fact]
    public void Microwave_PeakField_UsesFreeSpaceImpedanceByDefault()
    {
        Quantity field = MicrowaveQuantities.PeakFieldFromPowerDensity(Quantity.Of(10.0, "W/m^2"));

        Assert.Equal(MicrowaveQuantities.ElectricField, field.Dimension);
        Assert.Equal(MicrowavePhysics.PeakFieldFromPowerDensity(10.0), field.In("V/m"), 9);
    }

    [Fact]
    public void Microwave_ApertureGain_IsDimensionlessRatio()
    {
        Quantity lambda = MicrowaveQuantities.Wavelength(Quantity.Of(2.45, "GHz"));
        double gain = MicrowaveQuantities.ApertureGain(Quantity.Of(1.0, Si.SquareMetre), 0.65, lambda);

        Assert.Equal(MicrowavePhysics.ApertureGain(1.0, 0.65, lambda.In(Si.Metre)), gain, 9);
        Assert.True(gain > 1.0);
    }

    #endregion

    #region Порошковая дифракция

    [Fact]
    public void Powder_SpacingFromAngle_MatchesUntypedCalculation()
    {
        Quantity wavelength = Quantity.Of(1.5406, Si.Angstrom);
        Quantity spacing = PowderAnalysis.SpacingFromAngle(Quantity.Of(44.5, Si.Degree), wavelength);

        Assert.Equal(Dimension.LengthDim, spacing.Dimension);
        Assert.Equal(PowderAnalysis.SpacingFromAngle(44.5, 1.5406), spacing.In(Si.Angstrom), 9);
    }

    [Fact]
    public void Powder_SpacingFromAngle_IsIndifferentToInputUnit()
    {
        Quantity inAngstrom = PowderAnalysis.SpacingFromAngle(
            Quantity.Of(44.5, Si.Degree), Quantity.Of(1.5406, Si.Angstrom));

        Quantity inNanometre = PowderAnalysis.SpacingFromAngle(
            Quantity.Of(44.5, Si.Degree), Quantity.Of(0.15406, "nm"));

        Assert.True(inAngstrom.AlmostEquals(inNanometre, 1e-12));
    }

    [Fact]
    public void Powder_AngleFromSpacing_RoundTrips()
    {
        Quantity wavelength = Quantity.Of(1.5406, Si.Angstrom);
        Quantity spacing = Quantity.Of(2.0343, Si.Angstrom);

        Quantity angle = PowderAnalysis.AngleFromSpacing(spacing, wavelength);
        Quantity back = PowderAnalysis.SpacingFromAngle(angle, wavelength);

        Assert.True(back.AlmostEquals(spacing, 1e-9));
    }

    [Fact]
    public void Powder_AngleFromSpacing_RejectsWrongDimension()
    {
        _ = Assert.Throws<DimensionMismatchException>(() => PowderAnalysis.AngleFromSpacing(
            Quantity.Of(2.0, Si.Second), Quantity.Of(1.5406, Si.Angstrom)));
    }

    [Fact]
    public void Powder_ScherrerSize_MatchesUntypedCalculation()
    {
        Quantity size = PowderAnalysis.ScherrerSize(
            Quantity.Of(44.5, Si.Degree),
            Quantity.Of(0.3, Si.Degree),
            Quantity.Of(1.5406, Si.Angstrom));

        Assert.Equal(PowderAnalysis.ScherrerSize(44.5, 0.3, 1.5406), size.In(Si.Angstrom), 9);
        Assert.True(size.In("nm") is > 1 and < 100);
    }

    #endregion

    #region Элементарная ячейка

    [Fact]
    public void UnitCell_TypedParameters_CarryLengthDimension()
    {
        var cell = UnitCell.Cubic(4.0);

        Assert.Equal(Dimension.LengthDim, cell.LengthA.Dimension);
        Assert.Equal(4.0, cell.LengthA.In(Si.Angstrom), 12);
        Assert.Equal(0.4, cell.LengthA.In("nm"), 12);
    }

    [Fact]
    public void UnitCell_CellVolume_IsVolumeInSi()
    {
        var cell = UnitCell.Cubic(4.0);

        Assert.Equal(Dimension.Volume, cell.CellVolume.Dimension);
        Assert.Equal(64.0, cell.CellVolume.In(Si.Angstrom.Pow(3)), 9);
        Assert.True(cell.CellVolume.AlmostEquals(Quantity.Of(6.4e-29, Si.CubicMetre), 1e-12));
    }

    #endregion

    #region Бюджет неопределённости

    [Fact]
    public void UncertaintyBudget_ToMeasurement_CarriesCombinedStandardUncertainty()
    {
        var budget = new UncertaintyBudget("масса навески", 1.0000, Si.Gram)
            .Add("весы", 0.0002)
            .Add("калибровка", 0.0001);

        Measurement result = budget.ToMeasurement();

        Assert.Equal(Dimension.MassDim, result.Dimension);
        Assert.Equal(1.0, result.Value.In(Si.Gram), 12);
        Assert.Equal(budget.CombinedStandardUncertainty, result.UncertaintyIn(Si.Gram), 12);
    }

    [Fact]
    public void UncertaintyBudget_ToMeasurement_ConvertsUncertaintyWithUnit()
    {
        var budget = new UncertaintyBudget("масса навески", 1.0000, Si.Gram).Add("весы", 0.0002);

        Measurement result = budget.ToMeasurement();

        Assert.Equal(result.UncertaintyIn(Si.Gram) * 1000.0, result.UncertaintyIn(UnitRegistry.Parse("mg")), 9);
    }

    [Fact]
    public void UncertaintyBudget_ToQuantity_ReturnsTypedValue()
    {
        var budget = new UncertaintyBudget("объём", 250.0, UnitRegistry.Parse("mL"));

        Assert.Equal(Dimension.Volume, budget.ToQuantity().Dimension);
        Assert.Equal(2.5e-4, budget.ToQuantity().In(Si.CubicMetre), 12);
    }

    [Fact]
    public void UncertaintyBudget_WithoutTypedUnit_RefusesToConvert()
    {
        var budget = new UncertaintyBudget("отклик", 1.0, "усл. ед.").Add("шум", 0.01);

        _ = Assert.Throws<InvalidOperationException>(() => budget.ToMeasurement());

        // Add(name, halfWidth) — оценка типа B из границ допуска: u = halfWidth / √3
        Assert.Equal(0.01 / Math.Sqrt(3.0), budget.CombinedStandardUncertainty, 12);
    }

    #endregion
}
