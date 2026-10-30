using AI.Charts.JS;
using AI.Microwave.Coverage;
using AI.Microwave.Propagation;
using AI.Microwave.Safety;
using AiFrameworkDemo.Core;
using SkiaSharp;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>Распространение радиоволн: лучи на улице, канал TR 38.901, покрытие сети.</summary>
public static partial class MicrowaveDemoRunner
{
    private const double SinrMinDb = -10;
    private const double SinrMaxDb = 30;

    private static DemoResult StreetMultipath(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        double width = N(p, "width", 20);
        double distance = N(p, "dist", 200);

        // Дома с запасом за концы трассы: иначе высокие порядки отражений уходят за торец улицы.
        var edge = new Vector3(distance + 100, 0, 0);
        var height = new Vector3(0, 0, 20);

        var scene = new RayTracingScene(N(p, "f", 3500) * 1e6)
            .Add(PlanarReflector.Infinite(new Vector3(0, 0, 0), new Vector3(0, 0, 1), RadioMaterial.MediumDryGround, "асфальт"))
            .Add(new PlanarReflector(new Vector3(-50, -width / 2, 0), edge, height, RadioMaterial.Concrete, "дом слева"))
            .Add(new PlanarReflector(new Vector3(-50, width / 2, 0), edge, height, RadioMaterial.Concrete, "дом справа"));

        MultipathChannel street = scene.Trace(
            new RadioEndpoint(new Vector3(0, 0.15 * width, 10)),
            new RadioEndpoint(new Vector3(distance, -0.2 * width, 1.5)) { Velocity = new Vector3(-N(p, "v", 15), 0, 0) },
            maxReflections: I(p, "order", 3));

        const int n = 801;
        const double bandHz = 100e6;
        var offsets = Enumerable.Range(0, n).Select(i => bandHz * ((i / (n - 1.0)) - 0.5)).ToArray();
        var gainDb = street.FrequencyResponse(offsets).Select(h => 20 * Math.Log10(h.Magnitude));

        var cv = MakeView(s);
        cv.ChartName = $"АЧХ канала: {street.Count} путей, разброс задержек {street.RmsDelaySpreadSeconds * 1e9:F1} нс";
        cv.LabelX = "отстройка от несущей, МГц";
        cv.LabelY = "усиление, дБ";

        double edgeMhz = bandHz / 2e6;
        cv.AddPlot(V(offsets.Select(f => f / 1e6)), V(gainDb), "|H(f)|", Sky, 2);
        cv.AddPlot(V([-edgeMhz, edgeMhz]), V([street.PathGainDb, street.PathGainDb]), "Средний уровень", Amber, 2);

        var rep = new ReportBuilder()
            .Metric("Потери на трассе", -street.PathGainDb, "дБ", "По суммарной мощности путей", MetricTone.Neutral, "F1")
            .Metric("Разброс задержек", street.RmsDelaySpreadSeconds * 1e9, "нс",
                "Среднеквадратичный, по мощности путей", MetricTone.Neutral, "F1")
            .Metric("Полоса когерентности", Fmt(street.CoherenceBandwidthHz() / 1e6), "МГц",
                "Корреляция падает до 0,5; прочерк - сильный луч не даёт ей упасть")
            .Metric("Доплер", street.MaxDopplerHz, "Гц", "Наибольший сдвиг среди путей", MetricTone.Neutral, "F0")
            .Metric("Время когерентности", Fmt(street.CoherenceTimeSeconds() * 1e3), "мс",
                "Сколько канал остаётся похожим на себя; прочерк - приёмник стоит")
            .Metric("K-фактор", 10 * Math.Log10(street.RicianKFactor), "дБ",
                "Прямой луч к сумме остальных", MetricTone.Neutral, "F1");

        var t = rep.Table("Сильнейшие пути",
            ["Отражений", "Избыточная задержка, нс", "Мощность, дБ", "Доплер, Гц", "Азимут прихода, град"],
            [true, true, true, true, true]);

        foreach (var path in street.Paths.OrderByDescending(x => x.Power).Take(10))
        {
            t.Row(path.Order.ToString(),
                ((path.DelaySeconds - street.FirstArrivalSeconds) * 1e9).ToString("F1"),
                path.PowerDb.ToString("F1"),
                path.DopplerHz.ToString("F0"),
                path.AzimuthOfArrivalDeg.ToString("F0"));
        }

        rep.Note("Лучи разной длины складываются то в фазе, то в противофазе, поэтому по полосе идут " +
                 "провалы: сигнал шире полосы когерентности замирает частотно-избирательно. " +
                 "Учитываются только зеркальные отражения - дифракции на рёбрах и рассеяния здесь нет.");

        return Png(cv, s, textOutput: street.Interpret().ToLlmText(), report: rep.Build());
    }

    private static DemoResult Tr38901Demo(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var scenario = Scenario(p);

        var model = new Tr38901ChannelModel(scenario, N(p, "f", 3500) * 1e6)
        {
            BaseStationArray = AntennaArray.Tr38901Panel(4, 4) with { DowntiltDeg = 8 },
            TerminalArray = new AntennaArray(1, 1) { Polarization = ArrayPolarization.Dual },
            LineOfSight = I(p, "los", 0) switch { 1 => true, 2 => false, _ => null },
        };

        Tr38901Channel channel = model.Generate(
            new Vector3(0, 0, scenario.TypicalBaseStationHeightM),
            new Vector3(N(p, "dist", 160), 0, scenario.TypicalTerminalHeightM),
            new Random(I(p, "seed", 1)),
            terminalVelocity: new Vector3(N(p, "v", 30) / 3.6, 0, 0));

        // Одна пара антенн - обычный многолучевой канал со всеми его метриками.
        MultipathChannel siso = channel.ToMultipathChannel();
        double strongest = siso.Paths.Max(x => x.PowerDb);
        double DelayNs(PropagationPath x) => (x.DelaySeconds - siso.FirstArrivalSeconds) * 1e9;
        double PowerDb(PropagationPath x) => x.PowerDb - strongest;

        var cv = MakeView(s);
        string state = channel.IsLineOfSight ? "прямая видимость" : "без прямой видимости";
        cv.ChartName = $"{scenario.Name}: {state}, лучей {channel.RayCount}";
        cv.LabelX = "избыточная задержка, нс";
        cv.LabelY = "мощность луча, дБ от сильнейшего";

        var scattered = siso.Paths.Where(x => x.Kind != PathKind.LineOfSight).ToArray();
        cv.AddScatter(V(scattered.Select(DelayNs)), V(scattered.Select(PowerDb)), "Рассеянные лучи", Sky);

        var direct = siso.Paths.Where(x => x.Kind == PathKind.LineOfSight).ToArray();
        if (direct.Length > 0)
            cv.AddScatter(V(direct.Select(DelayNs)), V(direct.Select(PowerDb)), "Прямой луч", Amber);

        var rep = new ReportBuilder()
            .Metric("Потери на трассе", channel.PathLossDb, "дБ",
                $"Затенение {channel.ShadowFadingDb:F1} дБ", MetricTone.Neutral, "F1")
            .Metric("Кластеров", channel.Clusters.Count, "", $"Лучей {channel.RayCount}")
            .Metric("Ёмкость 2x32", channel.CapacityBitsPerHz(10), "бит/с/Гц",
                "При SNR 10 дБ, панель 4x4 с двумя поляризациями", MetricTone.Good, "F2")
            .Metric("Разброс задержек", siso.RmsDelaySpreadSeconds * 1e9, "нс",
                "Для пары антенн", MetricTone.Neutral, "F0")
            .Metric("Полоса когерентности", Fmt(siso.CoherenceBandwidthHz() / 1e6), "МГц",
                "Прочерк - сильный прямой луч не даёт корреляции упасть до 0,5")
            .Metric("Доплер", siso.MaxDopplerHz, "Гц", "Наибольший сдвиг среди лучей", MetricTone.Neutral, "F0");

        var t = rep.Table("Сильнейшие кластеры",
            ["Задержка, нс", "Доля мощности, %", "Азимут прихода, град", "Зенит прихода, град", "Азимут ухода, град"],
            [true, true, true, true, true]);

        foreach (var cluster in channel.Clusters.OrderByDescending(x => x.Power).Take(8))
        {
            t.Row((cluster.DelaySeconds * 1e9).ToString("F0"),
                (cluster.Power * 100).ToString("F1"),
                cluster.AzimuthOfArrivalDeg.ToString("F0"),
                cluster.ZenithOfArrivalDeg.ToString("F0"),
                cluster.AzimuthOfDepartureDeg.ToString("F0"));
        }

        rep.Note("Канал складывается из кластеров рассеивателей, в каждом двадцать лучей со своими " +
                 "углами. Смените номер реализации: при тех же условиях другой розыгрыш даёт другую " +
                 "обстановку, поэтому системы сравнивают по множеству розыгрышей, а не по одному.");

        return Png(cv, s, textOutput: channel.Interpret().ToLlmText(), report: rep.Build());
    }

    private static DemoResult NetworkCoverage(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var scenario = Scenario(p);

        var scene = new CoverageScene(scenario)
        {
            Receiver = new CoverageReceiver
            {
                BandwidthHz = 100e6,
                NoiseFigureDb = 9,
                Environment = (TerminalEnvironment)I(p, "env", 0),
            },
            SignalThresholdDbm = -110,
            SinrThresholdDb = -6,
            NeighbourLoad = N(p, "load", 1),
            Throughput = ThroughputMapping.Nr256Qam,
        };

        double distance = N(p, "isd", 500);
        scene.AddHexagonalNetwork(rings: 1, interSiteDistanceM: distance, template: new RadiationSource
        {
            Position = new SitePoint(0, 0, scenario.TypicalBaseStationHeightM),
            FrequencyHz = N(p, "f", 3500) * 1e6,
            TransmitPowerW = N(p, "p", 80),
            FeederLossDb = 0,
            GainDbi = 17,
            DowntiltDeg = N(p, "tilt", 8),
            Pattern = new GaussianPattern { AzimuthBeamwidthDeg = 65, ElevationBeamwidthDeg = 10, FrontToBackDb = 30 },
        });

        double half = 1.2 * distance;
        CoverageGrid area = CoverageGrid.Square(0, 0, half, half / 40);
        CoverageMap map = scene.Compute(area);
        CoverageSimulation simulation = scene.Simulate(area, drops: 20, new Random(1));

        var xs = Enumerable.Range(0, area.Columns).Select(area.X).ToArray();
        var ys = Enumerable.Range(0, area.Rows).Select(area.Y).ToArray();
        var sinr = Enumerable.Range(0, area.Rows)
            .Select(row => Enumerable.Range(0, area.Columns).Select(column => map[row, column].SinrDb).ToArray())
            .ToArray();

        var sites = scene.Sectors.Select(x => x.Position).Distinct().ToArray();
        var siteX = sites.Select(x => x.X).ToArray();
        var siteY = sites.Select(x => x.Y).ToArray();

        string title = $"SINR по средним потерям, шкала от {SinrMinDb:F0} до {SinrMaxDb:F0} дБ";
        const string axisX = "восток, м", axisY = "север, м";

        var cv = MakeView(s);
        cv.ChartName = title;
        cv.LabelX = axisX;
        cv.LabelY = axisY;
        cv.SetBackgroundImage(RenderSinrMap(sinr));
        cv.AddScatter(V(siteX), V(siteY), "Сайты", SKColors.White);
        cv.SetAxisRange(xs[0], xs[^1], ys[0], ys[^1]);

        var plotly = new PlotlyBuilder { Title = title, AxisX = axisX, AxisY = axisY };
        plotly.AddHeatmap(xs, ys, sinr, "Viridis", opacity: 1, showScale: true, zMin: SinrMinDb, zMax: SinrMaxDb);
        plotly.AddScatter2D(siteX, siteY, "Сайты", "#ffffff", markerSize: 8);

        var rep = new ReportBuilder()
            .Metric("Покрытие по сигналу", map.AreaCoverage * 100, "%",
                $"Сигнал выше {scene.SignalThresholdDbm:F0} дБм с учётом затенения",
                map.AreaCoverage >= 0.95 ? MetricTone.Good : MetricTone.Warn, "F1")
            .Metric("Покрытие по SINR", simulation.AreaSinrCoverage * 100, "%",
                $"SINR выше {scene.SinrThresholdDb:F0} дБ, {simulation.Drops} розыгрышей",
                simulation.AreaSinrCoverage >= 0.95 ? MetricTone.Good : MetricTone.Warn, "F1")
            .Metric("Медиана SINR", simulation.SinrPercentileDb(0.5), "дБ", "С затенением", MetricTone.Neutral, "F1")
            .Metric("Край соты", simulation.SinrPercentileDb(0.05), "дБ", "5-й процентиль SINR", MetricTone.Neutral, "F1")
            .Metric("Скорость на краю соты", simulation.CellEdgeThroughputBps / 1e6, "Мбит/с",
                "Вся полоса 100 МГц у одного абонента", MetricTone.Neutral, "F1")
            .Metric("Средняя скорость", simulation.AreaThroughputBps / 1e6, "Мбит/с",
                "По площади и розыгрышам", MetricTone.Neutral, "F0");

        var t = rep.Table("Распределение SINR по площади, дБ",
            ["Процентиль", "По средним потерям", "С затенением"], [true, true, true]);

        foreach (double q in new[] { 0.05, 0.25, 0.5, 0.75, 0.95 })
        {
            t.Row((q * 100).ToString("F0"),
                map.SinrPercentileDb(q).ToString("F1"),
                simulation.SinrPercentileDb(q).ToString("F1"));
        }

        rep.Note("Карта показывает SINR по средним потерям, цифры по краю соты считаны моделированием: " +
                 "оно разыгрывает затенение как коррелированные поля. Прибавка мощности в сети, " +
                 "ограниченной помехами, поднимает помеху вместе с сигналом - попробуйте изменить " +
                 "наклон антенн и нагрузку соседей.");

        return Png(cv, s, plotlyJson: plotly.Build(),
            textOutput: map.Interpret().ToLlmText() + "\n\n" + simulation.Interpret().ToLlmText(),
            report: rep.Build());
    }

    private static Tr38901Scenario Scenario(IReadOnlyDictionary<string, double> p)
        => Tr38901Scenario.All[Math.Clamp(I(p, "scen", 0), 0, 2)];

    /// <summary>Рисует карту SINR: строка 0 - южный край сетки, поэтому она внизу картинки.</summary>
    private static SKImage RenderSinrMap(double[][] sinrDb)
    {
        int rows = sinrDb.Length, columns = sinrDb[0].Length;
        using var bitmap = new SKBitmap(columns, rows);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double t = (sinrDb[row][column] - SinrMinDb) / (SinrMaxDb - SinrMinDb);
                bitmap.SetPixel(column, rows - 1 - row, ViridisColor(Math.Clamp(t, 0, 1)));
            }
        }

        return SKImage.FromBitmap(bitmap);
    }
}
