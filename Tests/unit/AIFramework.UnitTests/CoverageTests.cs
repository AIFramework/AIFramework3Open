using AI.Microwave.Coverage;
using AI.Microwave.Propagation;
using AI.Microwave.Safety;
using AI.Statistics;
using Vector3 = AI.Geometry.Primitives.Vector3;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Покрытие сети проверяется ответами, полученными независимо: мощность в точке — формулой Фрииса с диаграммой,
/// шум — k·T·B, SINR на середине между двумя сайтами — симметрией, доля площади с уверенным приёмом — формулой
/// Джейкса, межсайтовая корреляция затенения — средним |X| разности двух нормальных величин, моделирование —
/// точной аналитической вероятностью покрытия.
/// </summary>
public class CoverageTests
{
    private const double SpeedOfLight = 299792458.0;

    #region Шум, проникновение, скорость

    [Fact]
    public void ThermalNoise_IsKTB()
    {
        double kTB = 10 * Math.Log10(1.380649e-23 * 290 * 1e6 * 1000);

        Assert.Equal(kTB, LinkBudget.ThermalNoiseDbm(1e6), 12);
        Assert.Equal(-114, LinkBudget.ThermalNoiseDbm(1e6), 0.03);
        Assert.Equal(kTB + (10 * Math.Log10(20)) + 7, LinkBudget.ThermalNoiseDbm(20e6, 7), 12);
    }

    [Fact]
    public void PenetrationLoss_FollowsTr38901()
    {
        const double f = 3.5e9;
        double glass = 2 + (0.2 * 3.5), irr = 23 + (0.3 * 3.5), concrete = 5 + (4 * 3.5);
        double low = 5 - (10 * Math.Log10((0.3 * Math.Pow(10, -glass / 10)) + (0.7 * Math.Pow(10, -concrete / 10))));
        double high = 5 - (10 * Math.Log10((0.7 * Math.Pow(10, -irr / 10)) + (0.3 * Math.Pow(10, -concrete / 10))));

        Assert.Equal(low + 5, PenetrationLoss.LossDb(TerminalEnvironment.IndoorLowLoss, f, 10), 12);
        Assert.Equal(high + 5, PenetrationLoss.LossDb(TerminalEnvironment.IndoorHighLoss, f, 10), 12);
        Assert.Equal(9, PenetrationLoss.LossDb(TerminalEnvironment.InCar, f));
        Assert.Equal(0, PenetrationLoss.LossDb(TerminalEnvironment.Outdoor, f));
        Assert.Equal(4.4, PenetrationLoss.SigmaDb(TerminalEnvironment.IndoorLowLoss));
        Assert.Equal(6.5, PenetrationLoss.SigmaDb(TerminalEnvironment.IndoorHighLoss));
        Assert.True(PenetrationLoss.LossDb(TerminalEnvironment.IndoorLowLoss, 28e9) > PenetrationLoss.LossDb(TerminalEnvironment.IndoorLowLoss, 2e9));
    }

    [Fact]
    public void ThroughputMapping_FollowsAttenuatedShannon()
    {
        Assert.Equal(0.6 * Math.Log2(11), ThroughputMapping.Lte36942Downlink.SpectralEfficiency(10), 12);
        Assert.Equal(4.4, ThroughputMapping.Lte36942Downlink.SpectralEfficiency(30));
        Assert.Equal(0, ThroughputMapping.Lte36942Downlink.SpectralEfficiency(-10.5));
        Assert.Equal(2.0, ThroughputMapping.Lte36942Uplink.SpectralEfficiency(20));
        Assert.Equal(1, ThroughputMapping.Shannon.SpectralEfficiency(0), 12);
    }

    [Fact]
    public void SpatialRandomField_GridSampleEqualsPointValues()
    {
        var field = new SpatialRandomField(30, new Random(1), 128);
        double[] xs = [-40, 0, 12.5, 90];
        double[] ys = [5, 60, 200];
        double[,] grid = field.Sample(xs, ys);

        for (int r = 0; r < ys.Length; r++)
        {
            for (int c = 0; c < xs.Length; c++)
                Assert.Equal(field.Value(xs[c], ys[r]), grid[r, c], 12);
        }
    }

    #endregion

    #region Мощность и SINR

    [Theory]
    [InlineData(300.0, 200.0)]
    [InlineData(-100.0, 400.0)]
    [InlineData(500.0, -50.0)]
    public void ReceivedPower_MatchesFriisWithPattern(double x, double y)
    {
        var sector = new RadiationSource
        {
            Position = new SitePoint(0, 0, 30),
            AzimuthDeg = 45,
            DowntiltDeg = 3,
            FrequencyHz = 2e9,
            TransmitPowerW = 20,
            FeederLossDb = 2,
            GainDbi = 17,
            Pattern = new GaussianPattern { AzimuthBeamwidthDeg = 65, ElevationBeamwidthDeg = 8, FrontToBackDb = 25 },
        };
        var scene = new CoverageScene(new FreeSpaceModel()) { Receiver = new CoverageReceiver { HeightM = 1.5 } };
        scene.Add(sector);

        // Направление на точку: азимут от севера, угол места — независимо от кода сектора
        double dz = 1.5 - 30, ground = Math.Sqrt((x * x) + (y * y)), d = Math.Sqrt((ground * ground) + (dz * dz));
        double relativeAzimuth = Wrap180((Math.Atan2(x, y) * 180 / Math.PI) - 45);
        double relativeElevation = (Math.Atan2(dz, ground) * 180 / Math.PI) + 3;
        double attenuation = Math.Min((12 * Math.Pow(relativeAzimuth / 65, 2)) + (12 * Math.Pow(relativeElevation / 8, 2)), 25);
        double friis = 20 * Math.Log10(4 * Math.PI * d * 2e9 / SpeedOfLight);
        double expected = (10 * Math.Log10(20 * 1000)) - 2 + 17 - attenuation - friis;

        PointCoverage point = scene.Evaluate(x, y);

        Assert.Equal(expected, point.ReceivedPowerDbm, 9);
        Assert.Equal(expected - scene.Receiver.NoiseDbm, point.SinrDb, 9);
        Assert.Equal(double.NegativeInfinity, point.InterferenceDbm);
    }

    [Fact]
    public void TwoSymmetricSites_GiveZeroSinrAtMidline()
    {
        static CoverageScene Scene(double load, double secondFrequency)
        {
            var scene = new CoverageScene(new FreeSpaceModel()) { NeighbourLoad = load };
            scene.Add(Omni(-500, 0, 2e9, 1000));
            scene.Add(Omni(500, 0, secondFrequency, 1000));
            return scene;
        }

        // На серединном перпендикуляре сигналы равны: SINR = 0 дБ, при половинной нагрузке соседа — 3 дБ
        Assert.Equal(0, Scene(1, 2e9).Evaluate(0, 300).SinrDb, 3);
        Assert.Equal(10 * Math.Log10(2), Scene(0.5, 2e9).Evaluate(0, 300).SinrDb, 3);

        // На разных несущих помехи нет вовсе
        PointCoverage separated = Scene(1, 2.1e9).Evaluate(0, 300);
        Assert.Equal(double.NegativeInfinity, separated.InterferenceDbm);
        Assert.True(separated.SinrDb > 50);
    }

    #endregion

    #region Площадь покрытия

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(-4.0)]
    public void AreaCoverage_MatchesJakesFormula(double edgeMarginDb)
    {
        const double n = 3.5, sigma = 8, radius = 1000, threshold = -100, f = 900e6;
        var model = new LogDistanceModel(n, sigma);

        // Мощность подобрана так, чтобы медиана на краю круга была на edgeMarginDb выше порога
        double edgeLoss = PathLoss.FreeSpaceDb(f, 1) + (10 * n * Math.Log10(radius));
        double eirpDbm = threshold + edgeMarginDb + edgeLoss;
        var scene = new CoverageScene(model) { SignalThresholdDbm = threshold, Receiver = new CoverageReceiver { HeightM = 1.5 } };
        scene.Add(Omni(0, 0, f, Math.Pow(10, eirpDbm / 10) / 1000, heightM: 1.5));

        CoverageMap map = scene.Compute(CoverageGrid.Square(0, 0, radius, 10));
        double area = map.Cells.Where(c => Math.Sqrt((c.X * c.X) + (c.Y * c.Y)) <= radius).Average(c => c.CoverageProbability);

        // Джейкс: доля площади круга с сигналом выше порога при логнормальном затенении
        double a = -edgeMarginDb / (sigma * Math.Sqrt(2));
        double b = 10 * n * Math.Log10(Math.E) / (sigma * Math.Sqrt(2));
        double jakes = 0.5 * (1 - Erf(a) + (Math.Exp((1 - (2 * a * b)) / (b * b)) * (1 - Erf((1 - (a * b)) / b))));

        Assert.Equal(jakes, area, 0.005);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Simulation_ConvergesToAnalyticCoverage(bool tr38901)
    {
        IPropagationModel model = tr38901 ? Tr38901Scenario.UrbanMacro : new LogDistanceModel(3.5, 8);
        double height = tr38901 ? 25 : 1.5;
        var scene = new CoverageScene(model) { SignalThresholdDbm = tr38901 ? -95 : -100 };
        scene.Add(Omni(0, 0, 2e9, tr38901 ? 10 : 5, heightM: height));

        CoverageGrid grid = CoverageGrid.Square(0, 0, 1000, 40);
        double analytic = scene.Compute(grid).AreaCoverage;
        double simulated = scene.Simulate(grid, 30, new Random(2)).AreaSignalCoverage;

        Assert.InRange(analytic, 0.2, 0.98);
        Assert.Equal(analytic, simulated, 0.02);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.0)]
    public void InterSiteShadowCorrelation_SetsMidlineSinrSpread(double correlation)
    {
        const double sigma = 8;
        var scene = new CoverageScene(new FreeSpaceModel { ShadowSigmaDb = sigma, ShadowCorrelationDistanceM = 20 })
        {
            InterSiteShadowCorrelation = correlation,
        };
        scene.Add(Omni(-500, 0, 2e9, 1000));
        scene.Add(Omni(500, 0, 2e9, 1000));

        // Столбец точек на серединном перпендикуляре через 100 м — много дальше расстояния корреляции
        var grid = new CoverageGrid(-50, 100, 100, 1, 12);
        CoverageSimulation simulation = scene.Simulate(grid, 300, new Random(3));

        // Средние мощности равны, поэтому SINR = |X|, X ~ N(0, 2σ²(1 − ρ)), и E|X| = σ_X·√(2/π)
        double expected = Math.Sqrt(2 * sigma * sigma * (1 - correlation)) * Math.Sqrt(2 / Math.PI);

        Assert.Equal(expected, simulation.SinrSamplesDb.Average(), 0.35);
    }

    #endregion

    #region Модели и сеть

    [Fact]
    public void Tr38901Scenario_ServesAsPropagationModel()
    {
        IPropagationModel model = Tr38901Scenario.UrbanMicro;
        LinkLoss loss = model.Loss(new Vector3(0, 0, 10), new Vector3(120, 50, 1.5), 3.5e9);
        double d2 = Math.Sqrt((120 * 120) + (50 * 50));

        Assert.Equal(Tr38901Scenario.UrbanMicro.LineOfSightProbability(d2, 1.5), loss.LineOfSightProbability, 12);
        Assert.Equal(Tr38901Scenario.UrbanMicro.PathLossDb(true, d2, 10, 1.5, 3.5e9), loss.LineOfSightDb, 12);
        Assert.Equal(Tr38901Scenario.UrbanMicro.PathLossDb(false, d2, 10, 1.5, 3.5e9), loss.NonLineOfSightDb, 12);
        Assert.Equal(7.82, loss.NonLineOfSightSigmaDb);
        Assert.InRange(loss.MeanDb, loss.LineOfSightDb, loss.NonLineOfSightDb);

        // Смесь по состояниям: на медиане прямой видимости её доля даёт половину своей вероятности; допуск —
        // точность нормального распределения ядра, около 10⁻⁷
        double atLos = loss.ProbabilityWithin(loss.LineOfSightDb);
        double mixture = (loss.LineOfSightProbability * 0.5)
            + ((1 - loss.LineOfSightProbability) * StatInference.NormalCdf((loss.LineOfSightDb - loss.NonLineOfSightDb) / 7.82));
        Assert.Equal(mixture, atLos, 1e-7);
    }

    [Fact]
    public void IndoorReceiver_LosesExactlyThePenetrationLoss()
    {
        RadiationSource sector = Omni(0, 0, 3.5e9, 20);
        var outdoor = new CoverageScene(new FreeSpaceModel());
        var indoor = new CoverageScene(new FreeSpaceModel()) { Receiver = new CoverageReceiver { Environment = TerminalEnvironment.IndoorHighLoss, IndoorDistanceM = 4 } };
        outdoor.Add(sector);
        indoor.Add(sector);

        double difference = outdoor.Evaluate(200, 100).ReceivedPowerDbm - indoor.Evaluate(200, 100).ReceivedPowerDbm;

        Assert.Equal(PenetrationLoss.LossDb(TerminalEnvironment.IndoorHighLoss, 3.5e9, 4), difference, 9);
    }

    [Fact]
    public void HexagonalNetwork_FollowsTr38901Layout()
    {
        var scene = new CoverageScene(Tr38901Scenario.UrbanMacro);
        var template = new RadiationSource { Name = "UMa", Position = new SitePoint(0, 0, 25), FrequencyHz = 3.5e9, TransmitPowerW = 40, GainDbi = 16 };
        IReadOnlyList<RadiationSource> sectors = scene.AddHexagonalNetwork(1, 500, template);

        Assert.Equal(21, sectors.Count);

        SitePoint[] sites = sectors.Select(s => s.Position).Distinct().ToArray();
        Assert.Equal(7, sites.Length);
        Assert.All(sites.Where(s => s.X != 0 || s.Y != 0), s => Assert.Equal(500, Math.Sqrt((s.X * s.X) + (s.Y * s.Y)), 9));
        Assert.Equal(new[] { 60.0, 180.0, 300.0 }, sectors.Take(3).Select(s => s.AzimuthDeg));
        Assert.Equal(19 * 3, new CoverageScene(Tr38901Scenario.UrbanMacro).AddHexagonalNetwork(2, 500, template).Count);

        CoverageMap map = scene.Compute(CoverageGrid.Square(0, 0, 400, 50));
        Assert.Equal(1, Enumerable.Range(0, 21).Sum(map.ServingShare), 9);
        Assert.NotNull(map.Interpret());
        Assert.NotNull(scene.Simulate(CoverageGrid.Square(0, 0, 400, 100), 2, new Random(4)).Interpret());
    }

    #endregion

    private static RadiationSource Omni(double x, double y, double frequencyHz, double powerW, double heightM = 30) => new()
    {
        Position = new SitePoint(x, y, heightM),
        FrequencyHz = frequencyHz,
        TransmitPowerW = powerW,
        FeederLossDb = 0,
        GainDbi = 0,
        Pattern = new IsotropicPattern(),
    };

    private static double Wrap180(double angle)
    {
        double a = (angle + 180) % 360;

        return (a < 0 ? a + 360 : a) - 180;
    }

    private static double Erf(double x) => (2 * StatInference.NormalCdf(x * Math.Sqrt(2))) - 1;
}
