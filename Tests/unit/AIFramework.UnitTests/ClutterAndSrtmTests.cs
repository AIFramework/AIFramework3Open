using System.IO.Compression;
using System.Net;
using AI.Earth.Geodesy;
using AI.Microwave.Propagation;
using AI.Statistics;
using Vector3 = AI.Geometry.Primitives.Vector3;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Потери в застройке сверяются с проверочными данными реализации P.2108 от NTIA/ITS (p2108-test-data): поправка
/// на окружение антенны и статистическая модель для наземных трасс, включая отказы вне области определения.
/// Загрузчик SRTM проверяется на подменённом HTTP: адрес плитки, распаковка gzip, кэш, море вместо 404.
/// </summary>
public class ClutterAndSrtmTests
{
    #region P.2108, раздел 3.1

    // f, ГГц; h, м; w_s, м; R, м; класс NTIA (1 — вода … 6 — плотная застройка); A_h, дБ с округлением до 0,1
    [Theory]
    [InlineData(1.5, 2, 27, 10, 1, 16)]
    [InlineData(1.5, 2, 27, 6, 1, 10.9)]
    [InlineData(1.5, 2, 27, 10, 2, 16)]
    [InlineData(1.5, 2, 27, 6, 2, 10.9)]
    [InlineData(1.5, 2, 27, 10, 3, 20.5)]
    [InlineData(1.5, 2, 27, 6, 3, 14.6)]
    [InlineData(1.5, 2, 27, 15, 4, 24.5)]
    [InlineData(1.5, 2, 27, 6, 4, 14.6)]
    [InlineData(1.5, 2, 27, 15, 5, 24.5)]
    [InlineData(1.5, 2, 27, 6, 5, 14.6)]
    [InlineData(1.5, 2, 27, 20, 6, 27.1)]
    [InlineData(1.5, 2, 27, 6, 6, 14.6)]
    [InlineData(3, 3, 15, 15, 6, 29)]
    [InlineData(0.9, 2.3, 30, 10, 2, 13.7)]
    [InlineData(1.5, 2.5, 25, 10, 3, 20.2)]
    [InlineData(0.03, 2.1, 24.5, 9.8, 3, 5.7)]
    [InlineData(1.7, 30, 24.5, 9.8, 3, 0)]
    [InlineData(1.7, 30, 24.5, 30, 3, 0)]
    public void HeightGainCorrection_MatchesNtiaTestData(double fGHz, double h, double streetWidth, double r, int ntiaClass, double expected)
    {
        double actual = ItuP2108.HeightGainCorrectionDb(fGHz * 1e9, h, (ClutterCategory)(ntiaClass - 1), r, streetWidth);

        Assert.Equal(expected, actual, 0.051);
    }

    [Theory]
    [InlineData(0.02, 2, 27, 10)]
    [InlineData(4, 2, 27, 10)]
    [InlineData(1, 0, 10, 9)]
    [InlineData(2, 1, 0, 9)]
    [InlineData(2, 1, 27, 0)]
    public void HeightGainCorrection_RejectsInputsOutsideRecommendation(double fGHz, double h, double streetWidth, double r)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ItuP2108.HeightGainCorrectionDb(fGHz * 1e9, h, ClutterCategory.Suburban, r, streetWidth));

    [Theory]
    [InlineData(-0.77, -5.96059383728336638563)]
    [InlineData(0.0, 0.00285220856360571492)]
    [InlineData(1.5, 10.75438646420360661479)]
    public void NotchDiffraction_MatchesNtiaEquation2a(double nu, double expected)
        => Assert.Equal(expected, KnifeEdgeDiffraction.ItuApproximationLossDb(nu) - 6.03, 12);

    [Fact]
    public void RepresentativeHeights_FollowTable()
    {
        Assert.Equal(10, ItuP2108.RepresentativeHeightM(ClutterCategory.OpenRural));
        Assert.Equal(10, ItuP2108.RepresentativeHeightM(ClutterCategory.Suburban));
        Assert.Equal(15, ItuP2108.RepresentativeHeightM(ClutterCategory.Urban));
        Assert.Equal(15, ItuP2108.RepresentativeHeightM(ClutterCategory.TreesForest));
        Assert.Equal(20, ItuP2108.RepresentativeHeightM(ClutterCategory.DenseUrban));
        Assert.Equal(0, ItuP2108.ProfileClutterHeightM(ClutterCategory.WaterSea));
    }

    #endregion

    #region P.2108, раздел 3.2

    // f, ГГц; d, км; p, %; L_ctt, дБ с округлением до 0,1
    [Theory]
    [InlineData(0.5, 0.25, 50, 17.4)]
    [InlineData(0.5, 0.25, 1, 3.9)]
    [InlineData(0.5, 0.25, 99, 30.9)]
    [InlineData(26.6, 15.8, 45, 32.5)]
    [InlineData(67, 5.4, 30.5, 30.9)]
    [InlineData(3.5, 1, 0.1, 16.8)]
    [InlineData(3.5, 1, 99.9, 42.8)]
    public void TerrestrialStatistical_MatchesNtiaTestData(double fGHz, double dKm, double p, double expected)
        => Assert.Equal(expected, ItuP2108.TerrestrialStatisticalLossDb(fGHz * 1e9, dKm * 1000, p), 0.051);

    [Theory]
    [InlineData(0.24, 2, 50)]
    [InlineData(67.1, 5, 50)]
    [InlineData(10, 0.24, 50)]
    [InlineData(6, 3, 0)]
    [InlineData(6, 3, 100)]
    public void TerrestrialStatistical_RejectsInputsOutsideRecommendation(double fGHz, double dKm, double p)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ItuP2108.TerrestrialStatisticalLossDb(fGHz * 1e9, dKm * 1000, p));

    [Fact]
    public void ClutterLossModel_AddsMedianAndSpread()
    {
        const double f = 2e9;
        var inner = new FreeSpaceModel { ShadowSigmaDb = 3 };
        var model = new ClutterLossModel(inner);
        var bothEnds = new ClutterLossModel(inner) { BothEnds = true };
        var tx = new Vector3(0, 0, 30);
        var rx = new Vector3(1500, 0, 1.5);

        LinkLoss free = inner.Loss(tx, rx, f), cluttered = model.Loss(tx, rx, f);
        double median = ItuP2108.TerrestrialStatisticalLossDb(f, 1500);

        Assert.Equal(free.LineOfSightDb + median, cluttered.LineOfSightDb, 6);
        Assert.Equal(free.LineOfSightDb + (2 * median), bothEnds.Loss(tx, rx, f).LineOfSightDb, 6);

        // Квантиль на уровне Φ(1) отстоит от медианы ровно на σ. На 500 м он заведомо меньше значения на 2 км;
        // ближе к 2 км правило «не больше, чем на 2 км» выбирает между двумя квантилями, и σ из них не выделить
        var near = new Vector3(500, 0, 1.5);
        double nearMedian = ItuP2108.TerrestrialStatisticalLossDb(f, 500);
        double sigma = ItuP2108.TerrestrialStatisticalLossDb(f, 500, 100 * StatInference.NormalCdf(1)) - nearMedian;

        Assert.Equal(Math.Sqrt((3 * 3) + (sigma * sigma)), model.Loss(tx, near, f).LineOfSightSigmaDb, 4);

        // Дальше 2 км потери застройки не растут
        double far = model.Loss(tx, new Vector3(8000, 0, 1.5), f).LineOfSightDb - inner.Loss(tx, new Vector3(8000, 0, 1.5), f).LineOfSightDb;
        Assert.Equal(ItuP2108.TerrestrialStatisticalLossDb(f, 2000), far, 6);
    }

    #endregion

    #region Застройка в рельефе

    [Fact]
    public void GridTerrain_WithCategories_DerivesClutterAndSea()
    {
        var heights = new double[3, 3];
        var clutter = new ClutterCategory[3, 3];

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                clutter[i, j] = j == 0 ? ClutterCategory.WaterSea : j == 1 ? ClutterCategory.Urban : ClutterCategory.DenseUrban;
        }

        var terrain = new GridTerrain(0, 0, 100, heights, clutter);

        Assert.True(terrain.IsSeaAt(0, 100));
        Assert.Equal(0, terrain.ClutterHeightAt(0, 100), 12);
        Assert.Equal(15, terrain.ClutterHeightAt(100, 100), 12);
        Assert.Equal(17.5, terrain.ClutterHeightAt(150, 100), 12);
        Assert.Equal(ClutterCategory.DenseUrban, terrain.ClutterAt(190, 40));
        Assert.Equal(ClutterCategory.WaterSea, terrain.ClutterAt(-500, 0));
    }

    [Fact]
    public void TerrainModel_AddsTerminalClutterFromCategory()
    {
        // Плотная застройка у абонента на высоте 2 м, 1,5 ГГц: 27,1 дБ по данным NTIA; базовая станция выше крыш
        var terrain = new FlatTerrain { HeightM = 100, Clutter = ClutterCategory.DenseUrban };
        var plain = new TerrainPropagationModel(terrain);
        var cluttered = new TerrainPropagationModel(terrain) { TerminalClutter = true };
        var tx = new Vector3(0, 0, 30);
        var rx = new Vector3(1200, 300, 2);

        P1812Loss withClutter = cluttered.Analyze(tx, rx, 1.5e9);

        Assert.Equal(27.1, withClutter.TerminalClutterDb, 0.051);
        Assert.Equal(plain.Analyze(tx, rx, 1.5e9).TotalDb + withClutter.TerminalClutterDb, withClutter.TotalDb, 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => cluttered.Analyze(tx, rx, 3.5e9));
    }

    #endregion

    #region Загрузка SRTM

    [Fact]
    public async Task Downloader_FetchesDecompressesAndCaches()
    {
        short[] heights = [100, 110, 120, 130, 140, 150, 160, 170, 180];
        var handler = new StubHandler();
        handler.Files["https://s3.amazonaws.com/elevation-tiles-prod/skadi/N10/N10E020.hgt.gz"] = Gzip(ToBigEndian(heights));
        string cache = TemporaryDirectory();

        try
        {
            var downloader = new SrtmDownloader(cache, new HttpClient(handler));
            SrtmTile tile = await downloader.GetTileAsync(10, 20);

            Assert.Equal(100, tile.HeightAt(11, 20), 9);
            Assert.Equal(140, tile.HeightAt(10.5, 20.5), 9);
            Assert.True(File.Exists(Path.Combine(cache, "N10E020.hgt")));

            // Повторно плитка берётся из кэша
            await downloader.GetTileAsync(10, 20);
            Assert.Single(handler.Requests);

            // Над морем плиток нет: 404 даёт уровень моря, а если так не велено — ошибку
            Assert.Equal(0, (await downloader.GetTileAsync(-5, -30)).HeightAt(-4.5, -29.5), 12);
            Assert.Equal("https://s3.amazonaws.com/elevation-tiles-prod/skadi/S05/S05W030.hgt.gz", handler.Requests[^1]);

            var strict = new SrtmDownloader(cache, new HttpClient(handler)) { MissingTileIsSea = false };
            await Assert.ThrowsAsync<HttpRequestException>(() => strict.GetTileAsync(-6, -30));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task Downloader_ReadsUncompressedTemplateAndBuildsLocalTerrain()
    {
        // Высота линейна по широте и долготе, как в проверке ToLocalGrid
        const int samples = 101;
        var heights = new short[samples * samples];

        for (int i = 0; i < samples; i++)
        {
            for (int j = 0; j < samples; j++)
                heights[(i * samples) + j] = (short)Math.Round((1000 * (1 - (i / 100.0))) + (500 * (j / 100.0)));
        }

        var handler = new StubHandler();
        handler.Files["http://tiles.test/N10E020.hgt"] = ToBigEndian(heights);
        string cache = TemporaryDirectory();

        try
        {
            var downloader = new SrtmDownloader(cache, new HttpClient(handler)) { UrlTemplate = "http://tiles.test/{1}.hgt" };
            GridTerrain terrain = await downloader.LoadTerrainAsync(new GeoPoint(10.5, 20.5), 2000, 500);

            Assert.Equal(750, terrain.GroundHeightAt(0, 0), 0.5);
            Assert.Single(handler.Requests);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void TilesAround_CoversTheSquare()
    {
        var single = SrtmDownloader.TilesAround(new GeoPoint(55.5, 37.5), 1000);
        Assert.Equal(new[] { (55, 37) }, single);

        var corner = SrtmDownloader.TilesAround(new GeoPoint(55.99, 37.99), 5000);
        Assert.Equal(4, corner.Count);
        Assert.Contains((56, 38), corner);

        // Через антимеридиан долгота переходит на −180
        var dateLine = SrtmDownloader.TilesAround(new GeoPoint(10.5, 179.99), 3000);
        Assert.Contains((10, -180), dateLine);
        Assert.Contains((10, 179), dateLine);
    }

    #endregion

    private static byte[] ToBigEndian(short[] heights) => heights.SelectMany(h => new[] { (byte)(h >> 8), (byte)(h & 0xFF) }).ToArray();

    private static byte[] Gzip(byte[] data)
    {
        using var buffer = new MemoryStream();

        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(data);

        return buffer.ToArray();
    }

    private static string TemporaryDirectory() => Path.Combine(Path.GetTempPath(), "srtm-cache-" + Guid.NewGuid().ToString("N"));

    private sealed class StubHandler : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            Requests.Add(url);

            return Task.FromResult(Files.TryGetValue(url, out byte[]? body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
