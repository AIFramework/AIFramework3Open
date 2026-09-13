using AI.Earth.Geodesy;
using AI.Microwave.Coverage;
using AI.Microwave.Propagation;
using AI.Microwave.Safety;
using Vector3 = AI.Geometry.Primitives.Vector3;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Потери по рельефу сверяются с эталонной программой исследовательской комиссии 3 МСЭ-R Py1812: её проверочные
/// трассы b2iseac_rural_land_1km и _10km и промежуточные величины трассы rburg_rural_noclutter из журналов расчёта.
/// Буллингтон на одиночном препятствии сверяется с дифракцией на крае, плоская Земля — со свободным
/// пространством, плитка SRTM — с высотами, записанными в неё.
/// </summary>
public class TerrainPropagationTests
{
    // Трасса b2iseac_rural_land_1km: 95,3 МГц, антенны 60 и 7 м, помехи 10 м
    private static readonly double[] Km1Distances = [0, 0.2, 0.4, 0.6, 0.8, 1];
    private static readonly double[] Km1Heights = [754.4, 754.4, 729.9, 685.3, 634.3, 610.3];
    private static readonly double[] Km1Clutter = [10, 10, 10, 10, 10, 10];

    // Трасса b2iseac_rural_land_10km: те же частота и антенны, неравномерный шаг, помехи 0–15 м
    private static readonly double[] Km10Distances =
        [0, 0.2, 0.4, 0.6, 0.8, 1, 1.2, 1.4, 1.6, 1.8, 2, 2.5, 3, 3.5, 4, 4.5, 5, 5.5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10];

    private static readonly double[] Km10Heights =
    [
        754.4, 754.4, 729.9, 685.3, 634.3, 610.3, 601, 591.7, 530.4, 455.9, 385.1, 373.4, 358.5, 309,
        316.6, 335.3, 408.1, 532.7, 556.3, 556.3, 488.2, 367.7, 304.8, 292.6, 238.3, 265.1, 250.3,
    ];

    private static readonly double[] Km10Clutter = [10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 0, 0, 0, 15, 10, 10, 10, 10, 10, 10, 10, 0, 0, 0, 0, 0];

    #region Проверочные трассы P.1812

    [Fact]
    public void ValidationPath1km_MatchesReferenceImplementation()
    {
        var profile = new TerrainProfile(Km1Distances.Select(d => d * 1000).ToArray(), Km1Heights, Km1Clutter);

        P1812Loss median = ItuP1812.MedianLoss(profile, 60, 7, 95.3e6, Polarization.Horizontal);
        P1812Loss beta = ItuP1812.MedianLoss(profile, 60, 7, 95.3e6, Polarization.Horizontal, effectiveRadiusKm: ItuP1812.BetaEffectiveEarthRadiusKm);
        (double hst, double hsr, double hstd, double hsrd) = ItuP1812.SmoothEarthHeights(Km1Distances, Km1Heights, 814.4, 617.3);

        Assert.Equal(8930.776786, ItuP1812.MedianEffectiveEarthRadiusKm(45), 5);
        Assert.Equal(783.304, hst, 6);
        Assert.Equal(611.196, hsr, 6);
        Assert.Equal(754.4, hstd, 9);
        Assert.Equal(610.3, hsrd, 9);
        Assert.Equal(72.14737981, median.FreeSpaceDb, 6);
        Assert.Equal(15.34252882, median.DiffractionDb, 6);
        Assert.Equal(15.33794877, beta.DiffractionDb, 6);
        Assert.Equal(87.48990862, median.TotalDb, 6);
    }

    [Fact]
    public void ValidationPath10km_MatchesReferenceImplementation()
    {
        var profile = new TerrainProfile(Km10Distances.Select(d => d * 1000).ToArray(), Km10Heights, Km10Clutter);

        P1812Loss median = ItuP1812.MedianLoss(profile, 60, 7, 95.3e6, Polarization.Horizontal);
        P1812Loss beta = ItuP1812.MedianLoss(profile, 60, 7, 95.3e6, Polarization.Horizontal, effectiveRadiusKm: ItuP1812.BetaEffectiveEarthRadiusKm);
        (double hst, double hsr, double hstd, double hsrd) = ItuP1812.SmoothEarthHeights(Km10Distances, Km10Heights, 814.4, 257.3);

        Assert.Equal(574.05538, hst, 5);
        Assert.Equal(274.52262, hsr, 5);
        Assert.Equal(537.65013, hstd, 5);
        Assert.Equal(206.91287, hsrd, 5);
        Assert.Equal(91.99531592, median.FreeSpaceDb, 6);
        Assert.Equal(28.49553647, median.DiffractionDb, 6);
        Assert.Equal(28.44456493, beta.DiffractionDb, 6);
        Assert.Equal(120.4908524, median.TotalDb, 6);
        Assert.False(median.IsLineOfSight);
    }

    [Fact]
    public void ValidationPath96km_SphericalEarthAndSmoothBullington()
    {
        // rburg_rural_noclutter, 98,2 МГц, радиус β₀: высоты над гладкой поверхностью из журнала, шаг профиля 0,1 км
        const double hte = 44.46182993, hre = 19.07975011, frequency = 98.2e6;
        double[] distances = Enumerable.Range(0, 963).Select(i => i / 10.0).ToArray();

        Assert.Equal(111.9057367, ItuP1812.FreeSpaceDb(96.2, 407, 515, frequency), 6);
        Assert.Equal(37.42847713, ItuP1812.SphericalEarthDb(96.2, hte, hre, ItuP1812.BetaEffectiveEarthRadiusKm, frequency).Horizontal, 6);
        Assert.Equal(16.1773341, ItuP1812.BullingtonDb(distances, new double[963], hte, hre, ItuP1812.BetaEffectiveEarthRadiusKm, frequency), 6);
    }

    #endregion

    #region Независимые проверки

    [Theory]
    [InlineData(40.0)]
    [InlineData(6.0)]
    public void Bullington_OnSingleEdge_IsKnifeEdgeWithPathCorrection(double edgeHeightM)
    {
        const double frequency = 1e9;
        double bullington = ItuP1812.BullingtonDb([0, 1, 2], [0, edgeHeightM, 0], 10, 10, 1e12, frequency);

        // Край на середине двухкилометровой трассы над линией антенн на высоте 10 м
        double nu = KnifeEdgeDiffraction.FresnelParameter(edgeHeightM - 10, 1000, 1000, frequency);
        double approximate = KnifeEdgeDiffraction.ItuApproximationLossDb(nu);
        double correction = (1 - Math.Exp(-approximate / 6)) * (10 + (0.02 * 2));

        // λ в P.1812 взята с c = 2,998·10⁸ м/с — отсюда допуск
        Assert.Equal(approximate + correction, bullington, 0.01);

        // Приближение P.526 близко к точному интегралу Френеля
        Assert.Equal(KnifeEdgeDiffraction.LossDb(nu), approximate, 0.6);
    }

    [Fact]
    public void FlatTerrain_WithHighAntennas_IsFreeSpace()
    {
        var model = new TerrainPropagationModel(new FlatTerrain { HeightM = 120 });
        P1812Loss loss = model.Analyze(new Vector3(0, 0, 30), new Vector3(2000, 0, 30), 900e6);

        Assert.True(loss.IsLineOfSight);
        Assert.Equal(0, loss.DiffractionDb, 9);
        Assert.Equal(PathLoss.FreeSpaceDb(900e6, 2000), loss.TotalDb, 0.06);
    }

    [Fact]
    public void Ridge_ShadowsTheFarSide()
    {
        // Хребет высотой 80 м вдоль x = 2 км на ровной местности
        const double spacing = 50;
        int nodes = 201;
        var heights = new double[nodes, nodes];

        for (int i = 0; i < nodes; i++)
        {
            for (int j = 0; j < nodes; j++)
            {
                double x = -5000 + (j * spacing);
                heights[i, j] = 80 * Math.Exp(-Math.Pow((x - 2000) / 100, 2));
            }
        }

        var terrain = new GridTerrain(-5000, -5000, spacing, heights);
        var model = new TerrainPropagationModel(terrain) { ProfileStepM = 25 };
        P1812Loss behind = model.Analyze(new Vector3(0, 0, 30), new Vector3(4000, 0, 1.5), 900e6);
        P1812Loss front = model.Analyze(new Vector3(0, 0, 30), new Vector3(-4000, 0, 1.5), 900e6);

        Assert.False(behind.IsLineOfSight);
        Assert.True(front.IsLineOfSight);
        Assert.True(behind.TotalDb - front.TotalDb > 15, $"тень {behind.TotalDb - front.TotalDb:F1} дБ");

        // Карта покрытия видит ту же тень
        var scene = new CoverageScene(model) { SignalThresholdDbm = -95 };
        scene.Add(new RadiationSource { Position = new SitePoint(0, 0, 30), FrequencyHz = 900e6, TransmitPowerW = 20, GainDbi = 0, FeederLossDb = 0, Pattern = new IsotropicPattern() });
        CoverageMap map = scene.Compute(new CoverageGrid(-4500, -250, 500, 18, 1));

        double shadowed = map.Cells.Where(c => c.X > 2500).Average(c => c.ReceivedPowerDbm);
        double open = map.Cells.Where(c => c.X < -2500).Average(c => c.ReceivedPowerDbm);
        Assert.True(open - shadowed > 15);
        Assert.NotNull(behind.Interpret());
    }

    [Fact]
    public void LocationVariability_FollowsP1812()
        => Assert.Equal((0.52 + (0.024 * 3.5)) * Math.Pow(100, 0.28), ItuP1812.LocationVariabilityDb(3.5e9), 12);

    #endregion

    #region SRTM

    [Fact]
    public void SrtmTile_ReadsBigEndianRowsFromNorth()
    {
        short[] heights = [100, 110, 120, 130, 140, 150, 160, 170, SrtmTile.Void];
        string directory = Path.Combine(Path.GetTempPath(), "srtm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "N10E020.hgt");

        try
        {
            File.WriteAllBytes(path, heights.SelectMany(h => new[] { (byte)(h >> 8), (byte)(h & 0xFF) }).ToArray());
            SrtmTile tile = SrtmTile.Read(path);

            Assert.Equal(10, tile.Latitude);
            Assert.Equal(20, tile.Longitude);
            Assert.Equal(3, tile.Samples);
            Assert.Equal(100, tile.HeightAt(11, 20), 9);
            Assert.Equal(160, tile.HeightAt(10, 20), 9);
            Assert.Equal(140, tile.HeightAt(10.5, 20.5), 9);
            Assert.Equal(120, tile.HeightAt(10.75, 20.25), 9);

            // Пропуск в юго-восточном узле: среднее трёх известных
            Assert.Equal((140 + 150 + 170) / 3.0, tile.HeightAt(10.25, 20.75), 9);
            Assert.Equal("S05W070", SrtmTile.TileName(-5, -70));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SrtmTile_ToLocalGrid_FollowsGeography()
    {
        // Высота линейна по широте и долготе: 1000 м на градус широты и 500 м на градус долготы
        const int samples = 101;
        var heights = new short[samples * samples];

        for (int i = 0; i < samples; i++)
        {
            for (int j = 0; j < samples; j++)
            {
                double latitude = 11 - (i / 100.0), longitude = 20 + (j / 100.0);
                heights[(i * samples) + j] = (short)Math.Round((1000 * (latitude - 10)) + (500 * (longitude - 20)));
            }
        }

        var tile = new SrtmTile(10, 20, heights);
        GridTerrain local = SrtmTile.ToLocalGrid([tile], new GeoPoint(10.5, 20.5), 2000, 500);

        Assert.Equal(750, local.GroundHeightAt(0, 0), 0.5);

        // На север 2 км — около 0,0181°, на восток — около 0,0183°
        Assert.Equal(750 + (1000 * 2000 / 110_600.0), local.GroundHeightAt(0, 2000), 0.6);
        Assert.Equal(750 + (500 * 2000 / (111_320.0 * Math.Cos(10.5 * Math.PI / 180))), local.GroundHeightAt(2000, 0), 0.6);
    }

    #endregion
}
