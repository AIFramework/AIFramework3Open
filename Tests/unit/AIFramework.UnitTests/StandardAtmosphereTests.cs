using AI.Physics.Fluids;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Международная стандартная атмосфера: сверка с опубликованными таблицами ISO 2533 и перевод высот.
/// </summary>
public class StandardAtmosphereTests
{
    private static Quantity Meters(double value) => Quantity.Of(value, "m");

    [Theory]
    [InlineData(0, 288.15, 101325.0, 1.225)]
    [InlineData(11000, 216.65, 22632.06, 0.3639176)]
    [InlineData(20000, 216.65, 5474.889, 0.08803489)]
    [InlineData(32000, 228.65, 868.0187, 0.01322500)]
    [InlineData(47000, 270.65, 110.9063, 0.001427541)]
    [InlineData(71000, 214.65, 3.956420, 6.42110e-5)]
    public void StandardAtmosphere_TableAltitudes_MatchPublishedValues(
        double altitude, double temperature, double pressure, double density)
    {
        var h = Meters(altitude);

        Assert.Equal(temperature, StandardAtmosphere.Temperature(h).SiValue, 1e-9);
        Assert.Equal(1.0, StandardAtmosphere.Pressure(h).SiValue / pressure, 1e-6);
        Assert.Equal(1.0, StandardAtmosphere.Density(h).SiValue / density, 2e-5);
    }

    [Fact]
    public void StandardAtmosphere_SeaLevel_SoundAndViscosityMatchTables()
    {
        var zero = Meters(0);
        var tropopause = Meters(11000);

        Assert.Equal(340.294, StandardAtmosphere.SpeedOfSound(zero).SiValue, 0.001);
        Assert.Equal(295.070, StandardAtmosphere.SpeedOfSound(tropopause).SiValue, 0.001);
        Assert.Equal(1.0, StandardAtmosphere.DynamicViscosity(zero).SiValue / 1.7894e-5, 1e-4);
        Assert.Equal(1.0, StandardAtmosphere.DynamicViscosity(tropopause).SiValue / 1.4216e-5, 1e-4);
        Assert.Equal(1.0, StandardAtmosphere.KinematicViscosity(zero).SiValue / 1.4607e-5, 1e-3);
    }

    [Fact]
    public void StandardAtmosphere_LayerBoundaries_AreContinuous()
    {
        foreach (double boundary in new[] { 11000.0, 20000, 32000, 47000, 51000, 71000 })
        {
            double below = StandardAtmosphere.Pressure(Meters(boundary - 1e-6)).SiValue;
            double above = StandardAtmosphere.Pressure(Meters(boundary + 1e-6)).SiValue;

            Assert.Equal(1.0, below / above, 1e-9);
        }
    }

    [Fact]
    public void StandardAtmosphere_GeopotentialAltitude_ConvertsBothWays()
    {
        double h = StandardAtmosphere.GeopotentialAltitude(Meters(11019.068)).SiValue;
        double top = StandardAtmosphere.GeometricAltitude(StandardAtmosphere.MaxAltitude).SiValue;
        double back = StandardAtmosphere.GeometricAltitude(StandardAtmosphere.GeopotentialAltitude(Meters(30000))).SiValue;

        Assert.Equal(11000.0, h, 0.01);
        Assert.Equal(86000.0, top, 0.5);
        Assert.Equal(30000.0, back, 1e-6);
    }

    [Fact]
    public void StandardAtmosphere_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StandardAtmosphere.Pressure(Meters(90000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => StandardAtmosphere.Density(Meters(-6000)));
        Assert.ThrowsAny<Exception>(() => StandardAtmosphere.Temperature(Quantity.Of(1, "s")));
    }
}
