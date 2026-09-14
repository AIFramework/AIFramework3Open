using AI.Physics.Mechanics.Terminal;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Пробитие листа выбиванием пробки: предел пробития из баланса энергии, скорость за листом по Рехту и Ипсону.
/// </summary>
public class PlugPenetrationTests
{
    private static Quantity Mass(double kilograms) => new(kilograms, Dimension.MassDim);

    private static Quantity Meters(double meters) => new(meters, Dimension.LengthDim);

    private static Quantity Pascals(double value) => new(value, Dimension.Pressure);

    private static Quantity Speed(double value) => new(value, Dimension.Velocity);

    [Fact]
    public void PlugPenetration_BallisticLimit_BalancesKineticEnergyAndShearWork()
    {
        Quantity limit = PlugPenetration.BallisticLimit(Mass(0.008), Meters(0.009), Meters(0.003), Pascals(3e8), deformationFactor: 1.5);
        double work = PlugPenetration.ShearWork(Meters(0.009), Meters(0.003), Pascals(3e8)).SiValue;

        Assert.Equal(3e8 * Math.PI * 0.009 * 0.003 * 0.003 / 2, work, 1e-9);
        Assert.Equal(1.5 * work, 0.5 * 0.008 * limit.SiValue * limit.SiValue, 1e-9);
    }

    [Fact]
    public void PlugPenetration_BallisticLimit_ThickerSheetNeedsMoreSpeedAndResidualFollowsEnergy()
    {
        double thin = PlugPenetration.BallisticLimit(Mass(0.0036), Meters(0.0085), Meters(0.0008), Pascals(3e8)).SiValue;
        double thick = PlugPenetration.BallisticLimit(Mass(0.0036), Meters(0.0085), Meters(0.002), Pascals(3e8)).SiValue;
        double softer = PlugPenetration.BallisticLimit(Mass(0.0036), Meters(0.0085), Meters(0.0008), Pascals(3e8), deformationFactor: 3).SiValue;

        // Предел пропорционален толщине и корню из множителя деформации
        Assert.Equal(2.5, thick / thin, 1e-12);
        Assert.Equal(Math.Sqrt(3), softer / thin, 1e-12);
        Assert.Equal(0, PlugPenetration.ResidualVelocity(Speed(thin * 0.9), Speed(thin)).SiValue);
        Assert.Equal(Math.Sqrt((400 * 400) - (thin * thin)), PlugPenetration.ResidualVelocity(Speed(400), Speed(thin)).SiValue, 1e-9);
    }

    [Fact]
    public void PlugPenetration_ResidualVelocity_CarriesPlugAwayByRechtIpson()
    {
        // Пуля 9 мм, 8 г, в стальной лист 3 мм: скорость пары снаряд и пробка меньше в m/(m + m_пробки) раз
        Quantity limit = PlugPenetration.BallisticLimit(Mass(0.008), Meters(0.009), Meters(0.003), Pascals(3e8));
        Quantity plug = PlugPenetration.PlugMass(Meters(0.009), Meters(0.003), new Quantity(7850, Dimension.Density));

        double residual = PlugPenetration.ResidualVelocity(Speed(360), limit, Mass(0.008), plug).SiValue;

        Assert.Equal(7850 * Math.PI * 0.009 * 0.009 * 0.003 / 4, plug.SiValue, 1e-12);
        Assert.Equal(0.008 / (0.008 + plug.SiValue) * Math.Sqrt((360 * 360) - (limit.SiValue * limit.SiValue)), residual, 1e-9);
        Assert.True(residual < PlugPenetration.ResidualVelocity(Speed(360), limit).SiValue * 0.9);
    }

    [Fact]
    public void PlugPenetration_BallisticLimit_RejectsWrongUnits()
    {
        Assert.Throws<DimensionMismatchException>(() =>
            PlugPenetration.BallisticLimit(Meters(0.008), Meters(0.009), Meters(0.003), Pascals(3e8)));
    }
}
