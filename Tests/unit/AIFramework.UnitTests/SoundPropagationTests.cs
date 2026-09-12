using AI.DataStructs.Algebraic;
using AI.Physics.Acoustics;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Распространение звука сверяется со справочными таблицами ISO 9613 и законами расхождения:
/// точечный источник теряет 6 дБ на удвоение расстояния, линейный — 3.
/// </summary>
public class SoundPropagationTests
{
    [Theory]
    // ISO 9613-2, табл. 2: поглощение воздухом, дБ/км, на средних частотах октав
    [InlineData(10.0, 70.0, 1000.0, 3.7)]
    [InlineData(10.0, 70.0, 8000.0, 117.0)]
    [InlineData(20.0, 70.0, 1000.0, 5.0)]
    [InlineData(20.0, 70.0, 4000.0, 22.9)]
    public void SoundPropagation_AirAbsorption_MatchesIsoTable(double celsius, double humidity, double nominalHz, double expectedDbPerKm)
    {
        double exactHz = OctaveBands.MidbandFrequencies[BandOf(nominalHz)];

        double perKm = SoundPropagation.AirAbsorptionDbPerMetre(Of(exactHz, "Hz"), Of(celsius, "°C"), humidity) * 1000;

        Assert.InRange(perKm, expectedDbPerKm * 0.95, expectedDbPerKm * 1.05);
    }

    [Fact]
    public void SoundPropagation_Divergence_PointLosesSixDecibelsPerDoublingLineThree()
    {
        Assert.Equal(11.0, SoundPropagation.DivergenceDb(Of(1, "m"), SourceGeometry.Point), 1);
        Assert.Equal(6.02, Doubling(SourceGeometry.Point), 2);
        Assert.Equal(3.01, Doubling(SourceGeometry.Line), 2);
    }

    [Fact]
    public void SoundPropagation_Foliage_FollowsIsoTableAndCapsAt200Metres()
    {
        Assert.All(SoundPropagation.FoliageAttenuationDb(Of(5, "m")), attenuation => Assert.Equal(0.0, attenuation));
        Assert.Equal(new[] { 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 2.0, 3.0 }, SoundPropagation.FoliageAttenuationDb(Of(15, "m")).ToArray());

        // 1 кГц: 0.06 дБ/м; дальше 200 м не растёт
        Assert.Equal(6.0, SoundPropagation.FoliageAttenuationDb(Of(100, "m"))[4], 9);
        Assert.Equal(12.0, SoundPropagation.FoliageAttenuationDb(Of(1, "km"))[4], 9);
    }

    [Fact]
    public void SoundPropagation_PorousGround_MatchesIsoSimplifiedFormula()
    {
        // 4.8 − (2·1.5/100)·(17 + 300/100) = 4.2 дБ
        Assert.Equal(4.2, SoundPropagation.PorousGroundAttenuationDb(Of(100, "m"), Of(1.5, "m")), 9);
        Assert.Equal(0.0, SoundPropagation.PorousGroundAttenuationDb(Of(10, "m"), Of(5, "m")), 12);
    }

    [Fact]
    public void SoundPropagation_ReceivedLevels_ForestFiltersHighFrequenciesFirst()
    {
        // Одинаковый шум во всех полосах через 1 км, из них 200 м густого леса
        var flat = new Vector(Enumerable.Repeat(100.0, OctaveBands.Count));

        Vector received = SoundPropagation.ReceivedLevels(flat, Of(1, "km"), SourceGeometry.Point, Of(200, "m"), Of(15, "°C"), 70);

        for (int band = 1; band < OctaveBands.Count; band++)
            Assert.True(received[band] < received[band - 1]);
    }

    [Fact]
    public void OctaveBands_Levels_AddEnergetically()
    {
        var oneKilohertz = new Vector(-200.0, -200.0, -200.0, -200.0, 60.0, -200.0, -200.0, -200.0);

        Assert.Equal(63.01, OctaveBands.Sum(new Vector(60.0, 60.0)), 2);
        Assert.Equal(60.0, OctaveBands.AWeightedLevel(oneKilohertz), 6);
    }

    [Fact]
    public void OctaveBands_AudibilityMargin_ComparesWithMaskingAndThreshold()
    {
        var signal = new Vector(-200.0, -200.0, -200.0, -200.0, 40.0, -200.0, -200.0, -200.0);
        var background = new Vector(Enumerable.Repeat(30.0, OctaveBands.Count));
        var silence = new Vector(Enumerable.Repeat(-200.0, OctaveBands.Count));

        Assert.Equal(10.0, OctaveBands.AudibilityMarginDb(signal, background), 1);

        // В тишине сигнал сравнивается только с порогом слышимости: 2.4 дБ на 1 кГц
        Assert.Equal(40.0 - 2.4, OctaveBands.AudibilityMarginDb(signal, silence), 6);
    }

    private static Quantity Of(double value, string unit) => Quantity.Of(value, unit);

    private static double Doubling(SourceGeometry geometry) =>
        SoundPropagation.DivergenceDb(Of(200, "m"), geometry) - SoundPropagation.DivergenceDb(Of(100, "m"), geometry);

    private static int BandOf(double nominalHz) => (int)Math.Round(Math.Log2(nominalHz / 1000.0)) + 4;
}
