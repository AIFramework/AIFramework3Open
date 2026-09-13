using System.Numerics;
using AI.Microwave.Propagation;
using AI.Statistics;
using Vector3 = AI.Geometry.Primitives.Vector3;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Геометрическая стохастическая модель TR 38.901 проверяется ответами, полученными независимо: крупномасштабные
/// параметры — выборочными средними, СКО и корреляциями против таблиц, вероятность прямой видимости — частотой,
/// потери — свободным пространством на метре и коэффициентами QuaDRiGa, элемент решётки — шириной луча 65°,
/// решётка — набегом фазы плоской волны, движение абонента — триангуляцией последнего рассеивателя по двум
/// моментам и доплеровским сдвигом, карты — корреляцией exp(−r/d).
/// </summary>
public class Tr38901ChannelTests
{
    private const double Carrier = 3.5e9;
    private const double SpeedOfLight = 299792458.0;
    private static readonly Vector3 Base = new(0, 0, 10);

    #region Случайное поле

    [Fact]
    public void SpatialRandomField_HasExponentialCorrelation()
    {
        const double distance = 10;
        const int count = 3000;
        double[] lags = [2, 5, 10, 20];
        var rng = new Random(1);
        var origin = new double[count];
        var shifted = new double[lags.Length][];

        for (int j = 0; j < lags.Length; j++)
            shifted[j] = new double[count];

        for (int i = 0; i < count; i++)
        {
            var field = new SpatialRandomField(distance, rng);
            origin[i] = field.Value(3, -2);

            for (int j = 0; j < lags.Length; j++)
                shifted[j][i] = field.Value(3 + (lags[j] * Math.Cos(0.7)), -2 + (lags[j] * Math.Sin(0.7)));
        }

        Assert.Equal(0, origin.Average(), 0.06);
        Assert.Equal(1, StandardDeviation(origin), 0.05);

        for (int j = 0; j < lags.Length; j++)
            Assert.Equal(SpatialRandomField.Correlation(lags[j], distance), Correlation(origin, shifted[j]), 0.06);
    }

    #endregion

    #region Сценарии: потери, прямая видимость, таблицы

    [Theory]
    [InlineData(Tr38901Environment.UrbanMicro)]
    [InlineData(Tr38901Environment.IndoorMixedOffice)]
    public void LineOfSightPathLoss_InterceptIsFreeSpaceAtOneMetre(Tr38901Environment environment)
    {
        Tr38901Scenario scenario = Tr38901Scenario.All.First(s => s.Environment == environment);

        // Постоянная 32,4 дБ в формулах 38.901 — потери свободного пространства на одном метре
        Assert.Equal(PathLoss.FreeSpaceDb(Carrier, 1), scenario.PathLossDb(true, 1, 1.5, 1.5, Carrier), 0.05);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(100.0)]
    [InlineData(1000.0)]
    public void RuralPathLoss_MatchesQuadrigaCoefficients(double distance2D)
    {
        // Конфигурация QuaDRiGa 3GPP_38.901_RMa_LOS: 20,478·lg d + 31,741 + 20·lg f + 0,0014·d до точки перелома
        double d3 = Math.Sqrt((distance2D * distance2D) + (33.5 * 33.5));
        double expected = (20.478 * Math.Log10(d3)) + 31.741 + (20 * Math.Log10(Carrier / 1e9)) + (0.0014 * d3);

        Assert.Equal(expected, Tr38901Scenario.RuralMacro.PathLossDb(true, distance2D, 35, 1.5, Carrier), 0.02);
    }

    [Fact]
    public void PathLoss_HasTabulatedFarSlopes()
    {
        static double Slope(Tr38901Scenario scenario, bool lineOfSight, double baseHeight)
        {
            double far = scenario.PathLossDb(lineOfSight, 3000, baseHeight, 1.5, Carrier);
            double near = scenario.PathLossDb(lineOfSight, 1000, baseHeight, 1.5, Carrier);
            double dh = baseHeight - 1.5;

            return (far - near) / Math.Log10(Math.Sqrt((3000 * 3000) + (dh * dh)) / Math.Sqrt((1000 * 1000) + (dh * dh)));
        }

        // За точкой перелома прямая видимость даёт 40 дБ на декаду; без неё у UMi — 35,3
        Assert.Equal(40, Slope(Tr38901Scenario.UrbanMacro, true, 25), 9);
        Assert.Equal(35.3, Slope(Tr38901Scenario.UrbanMicro, false, 10), 9);

        // RMa непрерывна в точке перелома
        double breakpoint = Tr38901Scenario.RuralMacro.BreakpointDistanceM(35, 1.5, Carrier);
        Assert.Equal(
            Tr38901Scenario.RuralMacro.PathLossDb(true, breakpoint - 1e-6, 35, 1.5, Carrier),
            Tr38901Scenario.RuralMacro.PathLossDb(true, breakpoint + 1e-6, 35, 1.5, Carrier),
            0.01);
    }

    [Fact]
    public void LineOfSightProbability_FollowsTable()
    {
        Assert.Equal(1, Tr38901Scenario.UrbanMicro.LineOfSightProbability(18));
        Assert.Equal(18 / 5000.0, Tr38901Scenario.UrbanMicro.LineOfSightProbability(5000), 9);
        Assert.Equal(Math.Exp(-0.5), Tr38901Scenario.RuralMacro.LineOfSightProbability(510), 12);
        Assert.True(Tr38901Scenario.UrbanMacro.LineOfSightProbability(100, 22.5) > Tr38901Scenario.UrbanMacro.LineOfSightProbability(100, 1.5));
        Assert.Equal(-Math.Pow(10, 0.3), Tr38901Scenario.UrbanMicro.ZenithOffsetDepartureDeg(false, 100, 1.5, Carrier), 12);
    }

    [Fact]
    public void LineOfSightState_OccursWithTabulatedFrequency()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMacro, Carrier);
        Tr38901Drop drop = model.CreateDrop(new Vector3(0, 0, 25), new Random(2), spatiallyConsistent: false);
        var terminal = new Vector3(60, 80, 1.5);
        const int count = 6000;

        int visible = Enumerable.Range(0, count).Count(_ => drop.IsLineOfSight(terminal));
        double expected = (18 / 100.0) + (Math.Exp(-100 / 63.0) * (1 - (18 / 100.0)));

        Assert.Equal(expected, visible / (double)count, 0.025);
    }

    [Fact]
    public void CorrelationTables_AreFactorizedWithoutRegularization()
    {
        // Все таблицы v16.1 положительно определены: L·Lᵀ воспроизводит их до округления
        foreach (Tr38901Scenario scenario in Tr38901Scenario.All)
        {
            foreach (bool lineOfSight in new[] { true, false })
            {
                double deviation = scenario.Parameters(lineOfSight).CorrelationRegularization;

                Assert.True(deviation < 1e-9, $"{scenario.Name}, {(lineOfSight ? "LOS" : "NLOS")}: отклонение {deviation}");
            }
        }
    }

    [Theory]
    [InlineData(Tr38901Environment.UrbanMacro, true)]
    [InlineData(Tr38901Environment.UrbanMacro, false)]
    [InlineData(Tr38901Environment.UrbanMicro, false)]
    public void RealizedDelaySpread_MatchesLargeScaleParameter(Tr38901Environment environment, bool lineOfSight)
    {
        Tr38901Scenario scenario = Tr38901Scenario.All.First(s => s.Environment == environment);
        var model = new Tr38901ChannelModel(scenario, Carrier) { LineOfSight = lineOfSight };
        var baseStation = new Vector3(0, 0, scenario.TypicalBaseStationHeightM);
        var rng = new Random(12);
        var target = new List<double>();
        var realized = new List<double>();

        for (int i = 0; i < 400; i++)
        {
            Tr38901Channel channel = model.Generate(baseStation, new Vector3(150, 40, 1.5), rng);
            target.Add(Math.Log10(channel.LargeScale.DelaySpreadSeconds));
            realized.Add(Math.Log10(channel.ToMultipathChannel().RmsDelaySpreadSeconds));
        }

        // Калибровка: разброс задержек собранного из лучей канала в среднем равен разыгранному DS —
        // так работают масштаб C_τ при прямой видимости и деление сильнейших кластеров
        Assert.Equal(target.Average(), realized.Average(), 0.06);
    }

    [Fact]
    public void LargeScaleParameters_MatchTableStatistics()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier);
        Tr38901Drop drop = model.CreateDrop(Base, new Random(3), spatiallyConsistent: false);
        var terminal = new Vector3(120, 40, 1.5);
        var lgDs = new List<double>();
        var sf = new List<double>();
        var losDs = new List<double>();
        var k = new List<double>();

        for (int i = 0; i < 5000; i++)
        {
            LargeScaleParameters nlos = drop.LargeScale(terminal, lineOfSight: false);
            lgDs.Add(Math.Log10(nlos.DelaySpreadSeconds));
            sf.Add(nlos.ShadowFadingDb);

            LargeScaleParameters los = drop.LargeScale(terminal, lineOfSight: true);
            losDs.Add(Math.Log10(los.DelaySpreadSeconds));
            k.Add(los.KFactorDb);
        }

        double lg = Math.Log10(1 + 3.5);

        // UMi без прямой видимости: lgDS ~ N(−0,24·lg(1 + f) − 6,83; 0,16·lg(1 + f) + 0,28), SF 7,82 дБ, ρ(DS, SF) = −0,7
        Assert.Equal((-0.24 * lg) - 6.83, lgDs.Average(), 0.02);
        Assert.Equal((0.16 * lg) + 0.28, StandardDeviation(lgDs), 0.02);
        Assert.Equal(7.82, StandardDeviation(sf), 0.3);
        Assert.Equal(-0.7, Correlation(lgDs, sf), 0.03);

        // С прямой видимостью: K ~ N(9; 5) дБ, ρ(DS, K) = −0,7
        Assert.Equal(9, k.Average(), 0.25);
        Assert.Equal(5, StandardDeviation(k), 0.2);
        Assert.Equal(-0.7, Correlation(losDs, k), 0.03);
    }

    #endregion

    #region Антенны

    [Fact]
    public void ElementPattern_Matches3gppTable()
    {
        static double Db((double Theta, double Phi) field) => 10 * Math.Log10((field.Theta * field.Theta) + (field.Phi * field.Phi));

        AntennaArray panel = AntennaArray.Tr38901Panel(1, 1, dualPolarized: false);

        // 8 дБи в максимуме, −3 дБ на 32,5° в каждой плоскости, полка 30 дБ
        Assert.Equal(8, Db(panel.FieldPattern(0, 90, 0)), 9);
        Assert.Equal(0, panel.FieldPattern(0, 90, 0).Phi, 12);
        Assert.Equal(5, Db(panel.FieldPattern(0, 90, 32.5)), 9);
        Assert.Equal(5, Db(panel.FieldPattern(0, 122.5, 0)), 9);
        Assert.Equal(2, Db(panel.FieldPattern(0, 122.5, 32.5)), 9);
        Assert.Equal(-22, Db(panel.FieldPattern(0, 90, 180)), 9);

        // Две поляризации ±45°: поровну по θ и φ и ортогональны
        AntennaArray cross = AntennaArray.Tr38901Panel(1, 1);
        (double t0, double p0) = cross.FieldPattern(0, 90, 0);
        (double t1, double p1) = cross.FieldPattern(1, 90, 0);
        Assert.Equal(Math.Abs(t0), Math.Abs(p0), 12);
        Assert.Equal(0, (t0 * t1) + (p0 * p1), 12);
        Assert.Equal(8, Db((t1, p1)), 9);

        // Ориентация: азимут, наклон вниз и поворот вокруг главного направления
        Assert.Equal(8, Db((panel with { BearingDeg = 90 }).FieldPattern(0, 90, 90)), 9);
        Assert.Equal(8, Db((panel with { DowntiltDeg = 10 }).FieldPattern(0, 100, 0)), 9);

        (double theta, double phi) = (panel with { MechanicalSlantDeg = 90 }).FieldPattern(0, 90, 0);
        Assert.Equal(0, theta, 12);
        Assert.Equal(Math.Pow(10, 8 / 20.0), Math.Abs(phi), 9);
    }

    [Fact]
    public void ArrayResponse_IsPlaneWaveSteering()
    {
        var array = new AntennaArray(1, 4);
        double wavelength = SpeedOfLight / Carrier;

        for (int s = 0; s < 3; s++)
        {
            Vector3 step = array.ElementOffset(s + 1, wavelength) - array.ElementOffset(s, wavelength);
            Assert.Equal(wavelength / 2, step.Y, 12);
            Assert.Equal(0, step.X, 12);
            Assert.Equal(0, step.Z, 12);
        }

        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { BaseStationArray = array };
        Tr38901Channel channel = model.Generate(Base, new Vector3(-40, 90, 1.5), new Random(4));

        // Соседние элементы на λ/2 по Y: отношение коэффициентов — e^(jπ·r̂_y) по направлению ухода
        foreach (MimoPath path in channel.Paths())
        {
            Complex expected = Complex.FromPolarCoordinates(1, Math.PI * Direction(path.ZenithOfDepartureDeg, path.AzimuthOfDepartureDeg).Y);

            for (int s = 0; s < 3; s++)
            {
                Complex ratio = path.Coefficients[0, s + 1] / path.Coefficients[0, s];
                Assert.Equal(expected.Real, ratio.Real, 9);
                Assert.Equal(expected.Imaginary, ratio.Imaginary, 9);
            }
        }
    }

    #endregion

    #region Реализация канала

    [Fact]
    public void PathGain_AveragesToMinusPathLoss()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = true };
        var terminal = new Vector3(60, 0, 1.5);
        var rng = new Random(5);
        double[] gains = Enumerable.Range(0, 2000).Select(_ => model.Generate(Base, terminal, rng).PathGainDb).ToArray();

        Assert.Equal(-Tr38901Scenario.UrbanMicro.PathLossDb(true, 60, 10, 1.5, Carrier), gains.Average(), 0.3);
        Assert.Equal(4, StandardDeviation(gains), 0.25);
    }

    [Fact]
    public void Powers_SplitBetweenLineOfSightAndClusters()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = true };
        Tr38901Channel channel = model.Generate(Base, new Vector3(90, -20, 1.5), new Random(6));
        MultipathChannel siso = channel.ToMultipathChannel();

        double gain = Math.Pow(10, channel.PathGainDb / 10);
        double k = Math.Pow(10, channel.LargeScale.KFactorDb / 10);
        double clusters = channel.Clusters.Sum(c => c.Power);
        double direct = siso.Paths.Where(p => p.Kind == PathKind.LineOfSight).Sum(p => p.Power);

        // Отброшенные кластеры слабее сильнейшего на 25 дБ: вместе не больше 11·10^(−2,5)
        Assert.InRange(clusters, 1 - (11 * Math.Pow(10, -2.5)), 1 + 1e-12);
        Assert.Equal(k / (k + 1), direct / gain, 12);
        Assert.Equal(clusters / (k + 1), (siso.TotalPower - direct) / gain, 12);
        Assert.Equal(k / clusters, siso.RicianKFactor, 9);
    }

    [Fact]
    public void Clusters_FollowStandardStructure()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = true };
        var terminal = new Vector3(70, 40, 1.5);
        Tr38901Channel channel = model.Generate(Base, terminal, new Random(7));
        IReadOnlyList<Tr38901Cluster> clusters = channel.Clusters;

        Assert.Equal(0, clusters[0].DelaySeconds);
        Assert.True(clusters.Zip(clusters.Skip(1)).All(pair => pair.First.DelaySeconds <= pair.Second.DelaySeconds));
        Assert.InRange(clusters.Count, 2, 12);
        Assert.Equal(2, clusters.Count(c => c.IsSplit));

        // С прямой видимостью первый кластер стоит точно на прямом пути
        Vector3 toBase = Base - terminal;
        Assert.Equal(Math.Atan2(toBase.Y, toBase.X) * 180 / Math.PI, clusters[0].AzimuthOfArrivalDeg, 9);
        Assert.Equal(Math.Acos(toBase.Z / toBase.Length) * 180 / Math.PI, clusters[0].ZenithOfArrivalDeg, 9);
        Assert.Equal(Math.Atan2(-toBase.Y, -toBase.X) * 180 / Math.PI, clusters[0].AzimuthOfDepartureDeg, 9);
        Assert.Equal(Math.Acos(-toBase.Z / toBase.Length) * 180 / Math.PI, clusters[0].ZenithOfDepartureDeg, 9);

        // Два сильнейших кластера — три подкластера: +0, +1,28·c_DS, +2,56·c_DS по 10, 6 и 4 луча; c_DS = 5 нс
        const double clusterSpread = 5e-9;
        double direct = channel.Distance3DM / SpeedOfLight;
        List<MimoPath> scattered = channel.Paths().Where(p => p.Kind == PathKind.Scattered).ToList();

        for (int index = 0; index < clusters.Count; index++)
        {
            List<MimoPath> rays = scattered.Where(p => p.Cluster == index).ToList();
            Assert.Equal(20, rays.Count);

            var groups = rays
                .GroupBy(p => Math.Round((p.DelaySeconds - direct - clusters[index].DelaySeconds) / clusterSpread, 6))
                .OrderBy(g => g.Key)
                .ToList();

            if (!clusters[index].IsSplit)
            {
                Assert.Single(groups);
                Assert.Equal(0, groups[0].Key);
                continue;
            }

            Assert.Equal(new[] { 0.0, 1.28, 2.56 }, groups.Select(g => g.Key));
            Assert.Equal(new[] { 10, 6, 4 }, groups.Select(g => g.Count()));
        }
    }

    [Fact]
    public void Motion_PhaseAdvanceMatchesDoppler()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = true };
        Tr38901Channel channel = model.Generate(Base, new Vector3(60, -25, 1.5), new Random(8), new Vector3(-8, 6, 0));
        const double step = 1e-4;
        IReadOnlyList<MimoPath> now = channel.Paths(0), next = channel.Paths(step);

        // За 1 мм пути фаза каждого луча поворачивается на 2π·f_D·Δt с f_D = r̂_rx·v/λ
        for (int i = 0; i < now.Count; i++)
        {
            double advance = (next[i].Coefficients[0, 0] / now[i].Coefficients[0, 0]).Phase;
            Assert.Equal(2 * Math.PI * now[i].DopplerHz * step, advance, 4);
        }

        // Прямой путь пересчитывается по геометрии точно
        MimoPath direct = channel.Paths(2.5).Single(p => p.Kind == PathKind.LineOfSight);
        Assert.Equal(Base.DistanceTo(channel.TerminalAt(2.5)) / SpeedOfLight, direct.DelaySeconds, 15);
    }

    [Fact]
    public void Drift_KeepsLastBounceScattererFixed()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = false };
        var terminal = new Vector3(80, 30, 1.5);
        var velocity = new Vector3(5, -3, 0);
        const double later = 4;
        int triangulated = 0, onEllipse = 0;

        for (int seed = 0; seed < 5; seed++)
        {
            Tr38901Channel channel = model.Generate(Base, terminal, new Random(seed), velocity);
            IReadOnlyList<MimoPath> before = channel.Paths(0), after = channel.Paths(later);
            Vector3 p1 = channel.TerminalAt(0), p2 = channel.TerminalAt(later);

            for (int i = 0; i < before.Count; i++)
            {
                Vector3 u1 = Direction(before[i].ZenithOfArrivalDeg, before[i].AzimuthOfArrivalDeg);
                Vector3 u2 = Direction(after[i].ZenithOfArrivalDeg, after[i].AzimuthOfArrivalDeg);
                double cosine = u1.Dot(u2);

                // Луч почти не повернулся — точка пересечения плохо обусловлена
                if (1 - (cosine * cosine) < 1e-4)
                    continue;

                // Ближайшие точки прямых p1 + s·u1 и p2 + t·u2
                Vector3 w = p1 - p2;
                double d = u1.Dot(w), e = u2.Dot(w), denominator = 1 - (cosine * cosine);
                double s = ((cosine * e) - d) / denominator;
                double t = (e - (cosine * d)) / denominator;
                Vector3 scatterer = p1 + (s * u1);

                // Лучи в оба момента сходятся в одной точке, и изменение длины пути — изменение расстояния до неё
                Assert.True(scatterer.DistanceTo(p2 + (t * u2)) < 1e-6, $"луч {i}: прямые не пересекаются");
                Assert.Equal(after[i].LengthMetres - before[i].LengthMetres, scatterer.DistanceTo(p2) - s, 6);
                Assert.Equal(before[i].LengthMetres, before[i].DelaySeconds * SpeedOfLight, 6);
                triangulated++;

                // Не зажатый рассеиватель лежит на эллипсоиде: база — рассеиватель — абонент равно длине пути
                if (s > 11 && s < before[i].LengthMetres - 1)
                {
                    Assert.Equal(before[i].LengthMetres, Base.DistanceTo(scatterer) + s, 6);
                    onEllipse++;
                }
            }
        }

        Assert.True(triangulated > 200, $"проверено лучей: {triangulated}");
        Assert.True(onEllipse > 50, $"лучей на эллипсоиде: {onEllipse}");
    }

    [Fact]
    public void Drop_LargeScaleMapsAreSpatiallyCorrelated()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier);
        var rng = new Random(9);
        var here = new Vector3(50, 20, 1.5);
        var near = new Vector3(50.5, 20, 1.5);
        var far = new Vector3(60, 20, 1.5);
        var ds = new List<double>();
        var dsNear = new List<double>();
        var dsFar = new List<double>();

        for (int i = 0; i < 1500; i++)
        {
            Tr38901Drop drop = model.CreateDrop(Base, rng);
            ds.Add(Math.Log10(drop.LargeScale(here, false).DelaySpreadSeconds));
            dsNear.Add(Math.Log10(drop.LargeScale(near, false).DelaySpreadSeconds));
            dsFar.Add(Math.Log10(drop.LargeScale(far, false).DelaySpreadSeconds));
        }

        // Расстояние корреляции DS у UMi без прямой видимости — 10 м
        Assert.Equal(Math.Exp(-0.05), Correlation(ds, dsNear), 0.02);
        Assert.Equal(Math.Exp(-1), Correlation(ds, dsFar), 0.08);
    }

    [Fact]
    public void Drop_LineOfSightStateIsSpatiallyConsistent()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMacro, Carrier);
        var rng = new Random(10);
        var baseStation = new Vector3(0, 0, 25);
        var here = new Vector3(100, 0, 1.5);
        var near = new Vector3(100, 1, 1.5);
        var opposite = new Vector3(-100, 0, 1.5);
        const int count = 800;
        int visible = 0, agreeNear = 0, agreeFar = 0;

        for (int i = 0; i < count; i++)
        {
            Tr38901Drop drop = model.CreateDrop(baseStation, rng);
            bool state = drop.IsLineOfSight(here);

            visible += state ? 1 : 0;
            agreeNear += state == drop.IsLineOfSight(near) ? 1 : 0;
            agreeFar += state == drop.IsLineOfSight(opposite) ? 1 : 0;
        }

        double p = Tr38901Scenario.UrbanMacro.LineOfSightProbability(100);
        double independent = (p * p) + ((1 - p) * (1 - p));

        Assert.Equal(p, visible / (double)count, 0.06);
        Assert.True(agreeNear / (double)count > 0.9, $"совпадений в метре: {agreeNear}");
        Assert.Equal(independent, agreeFar / (double)count, 0.07);
    }

    [Fact]
    public void ChannelMatrix_AgreesWithSisoViewAndCapacity()
    {
        var model = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier)
        {
            BaseStationArray = AntennaArray.Tr38901Panel(2, 2) with { DowntiltDeg = 6 },
            TerminalArray = new AntennaArray(1, 2) { Polarization = ArrayPolarization.Dual },
        };
        Tr38901Channel channel = model.Generate(Base, new Vector3(100, 35, 1.5), new Random(11), new Vector3(3, 0, 0));
        Complex[,] h = channel.ChannelMatrix(4e6, 0.02);
        double scale = Math.Pow(10, channel.PathGainDb / 20);

        Assert.Equal(4, h.GetLength(0));
        Assert.Equal(8, h.GetLength(1));

        for (int u = 0; u < 4; u++)
        {
            for (int s = 0; s < 8; s++)
            {
                Complex siso = channel.ToMultipathChannel(0.02, u, s).FrequencyResponse(4e6);
                Assert.True((siso - h[u, s]).Magnitude < 1e-9 * scale, $"элемент [{u}, {s}]");
            }
        }

        // Одна антенна: ёмкость — формула Шеннона log₂(1 + ρ·|h|²/G)
        var single = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier);
        Tr38901Channel one = single.Generate(Base, new Vector3(100, 35, 1.5), new Random(12));
        double magnitude = one.ChannelMatrix()[0, 0].Magnitude;
        double shannon = Math.Log2(1 + (100 * magnitude * magnitude / Math.Pow(10, one.PathGainDb / 10)));

        Assert.Equal(shannon, one.CapacityBitsPerHz(20), 9);
        Assert.NotNull(channel.Interpret());
    }

    [Fact]
    public void Mimo_MultipliesCapacityInRichScattering()
    {
        var array = new AntennaArray(1, 2) { Polarization = ArrayPolarization.Dual, PolarizationSlantDeg = 45 };
        var siso = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = false };
        var mimo = new Tr38901ChannelModel(Tr38901Scenario.UrbanMicro, Carrier) { LineOfSight = false, BaseStationArray = array, TerminalArray = array };
        var terminal = new Vector3(120, 50, 1.5);

        double single = Enumerable.Range(0, 30).Average(seed => siso.Generate(Base, terminal, new Random(seed)).CapacityBitsPerHz(20));
        double multiple = Enumerable.Range(0, 30).Average(seed => mimo.Generate(Base, terminal, new Random(seed)).CapacityBitsPerHz(20));

        // Четыре на четыре с богатым рассеянием держат несколько потоков
        Assert.True(multiple > 2 * single, $"MIMO {multiple:F2} против {single:F2} бит/с/Гц");
    }

    #endregion

    private static Vector3 Direction(double zenithDeg, double azimuthDeg)
    {
        double theta = zenithDeg * Math.PI / 180, phi = azimuthDeg * Math.PI / 180;

        return new Vector3(Math.Sin(theta) * Math.Cos(phi), Math.Sin(theta) * Math.Sin(phi), Math.Cos(theta));
    }

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        double mean = values.Average();

        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }

    private static double Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;

        for (int i = 0; i < x.Count; i++)
        {
            sxy += (x[i] - mx) * (y[i] - my);
            sxx += (x[i] - mx) * (x[i] - mx);
            syy += (y[i] - my) * (y[i] - my);
        }

        return sxy / Math.Sqrt(sxx * syy);
    }
}
