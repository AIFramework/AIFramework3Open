using AI.Microwave.Safety;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>Радиочастотная безопасность: пределы, профиль облучения, санзона.</summary>
public static partial class MicrowaveDemoRunner
{
    private static DemoResult ExposureLimitsDemo(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var category = I(p, "cat", 0) == 0 ? ExposureCategory.General : ExposureCategory.Occupational;

        const int n = 400;
        var freq = new double[n];
        var sanpin = new double[n];
        var icnirp = new double[n];
        var fcc = new double[n];

        for (int i = 0; i < n; i++)
        {
            // Логарифмическая сетка от 10 МГц до 100 ГГц.
            double f = 10e6 * Math.Pow(1e4, i / (n - 1.0));
            freq[i] = f / 1e9;
            sanpin[i] = Limit(ExposureStandard.Sanpin, f, category);
            icnirp[i] = Limit(ExposureStandard.Icnirp2020, f, category);
            fcc[i] = Limit(ExposureStandard.FccOet65, f, category);
        }

        var cv = MakeView(s);
        cv.ChartName = $"ПДУ по плотности потока энергии, {(category == ExposureCategory.General ? "население" : "персонал")}";
        cv.LabelX = "частота, ГГц";
        cv.LabelY = "ПДУ, Вт/м2";

        cv.AddPlot(V(freq), V(sanpin), "СанПиН 1.2.3685-21", Pink, 2);
        cv.AddPlot(V(freq), V(icnirp), "ICNIRP 2020", Sky, 2);
        cv.AddPlot(V(freq), V(fcc), "FCC OET-65", Amber, 2);

        var rep = new ReportBuilder();
        var t = rep.Table("Пределы на характерных частотах",
            ["Частота", "СанПиН, мкВт/см2", "ICNIRP, мкВт/см2", "FCC, мкВт/см2", "E по СанПиН, В/м"],
            [false, true, true, true, true]);

        foreach (var (label, f) in new[]
        {
            ("100 МГц (ЧМ, ТВ)", 100e6), ("450 МГц", 450e6), ("900 МГц (GSM)", 900e6),
            ("1800 МГц (LTE)", 1800e6), ("2450 МГц (ISM, Wi-Fi)", 2450e6),
            ("3500 МГц (5G C-band)", 3500e6), ("26 ГГц (5G mmWave)", 26e9),
        })
        {
            t.Row(label,
                Fmt(Limit(ExposureStandard.Sanpin, f, category) * 100),
                Fmt(Limit(ExposureStandard.Icnirp2020, f, category) * 100),
                Fmt(Limit(ExposureStandard.FccOet65, f, category) * 100),
                Fmt(ExposureLimits.ElectricFieldLimit(ExposureStandard.Sanpin, f, category)));
        }

        double ratio = Limit(ExposureStandard.Icnirp2020, 1800e6, category)
                     / Math.Max(Limit(ExposureStandard.Sanpin, 1800e6, category), 1e-12);

        rep.Metric("Разница на 1800 МГц", ratio, "раз",
                "Во сколько раз российский предел строже международного",
                MetricTone.Warn, "F0")
           .Metric("Усреднение СанПиН", ExposureLimits.AveragingMinutes(ExposureStandard.Sanpin, 1800e6, category) / 60,
                "ч", "Время усреднения воздействия", MetricTone.Neutral, "F1")
           .Metric("Усреднение ICNIRP", ExposureLimits.AveragingMinutes(ExposureStandard.Icnirp2020, 1800e6, category),
                "мин", "Время усреднения воздействия", MetricTone.Neutral, "F0");

        rep.Note("Российский норматив на порядки строже международных и не зависит от частоты " +
                 "выше 300 МГц, тогда как ICNIRP и FCC поднимают предел с ростом частоты. " +
                 "Значения в коде - рабочая заготовка: перед выпуском документа их обязательно " +
                 "сверяют с действующей редакцией.");

        return Png(cv, s, report: rep.Build());
    }

    private static double Limit(ExposureStandard standard, double frequencyHz, ExposureCategory category)
    {
        double v = ExposureLimits.PowerDensityLimit(standard, frequencyHz, category);
        return double.IsNaN(v) ? 0.0 : v;
    }

    private static string Fmt(double value)
        => value <= 0 ? "-" : value.ToString(value < 10 ? "F2" : "F0",
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Собирает источник из значений ползунков.</summary>
    private static RadiationSource BuildSource(IReadOnlyDictionary<string, double> p,
        double azimuthDeg = 0)
        => new()
        {
            Name = $"сектор {azimuthDeg:F0} град",
            Position = new SitePoint(0, 0, N(p, "h", 30)),
            AzimuthDeg = azimuthDeg,
            DowntiltDeg = N(p, "tilt", 4),
            FrequencyHz = N(p, "f", 1800) * 1e6,
            TransmitPowerW = N(p, "p", 40),
            FeederLossDb = 2,
            GainDbi = N(p, "g", 18),
            DutyCycle = N(p, "duty", 1.0),
            Pattern = new GaussianPattern { AzimuthBeamwidthDeg = 65, ElevationBeamwidthDeg = 7 },
        };

    private static DemoResult ExposureProfile(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var standard = I(p, "std", 0) switch
        {
            1 => ExposureStandard.Icnirp2020,
            2 => ExposureStandard.FccOet65,
            _ => ExposureStandard.Sanpin,
        };

        var scene = new ExposureScene { Standard = standard };
        var source = scene.Add(BuildSource(p));
        double observer = N(p, "obs", 2);

        var profile = scene.Profile(0, observer, 0.5, 600, 400);
        var distance = profile.Select(x => x.DistanceM).ToArray();
        var density = profile.Select(x => x.PowerDensityWPerM2 * 100).ToArray();
        var ratio = profile.Select(x => x.Ratio).ToArray();

        double limit = ExposureLimits.PowerDensityLimit(standard, source.FrequencyHz) * 100;

        var cv = MakeView(s);
        cv.ChartName = $"ППЭ вдоль оси: ЭИИМ {source.EirpW / 1000:F2} кВт, подвес {source.Position.Z:F0} м, " +
                       $"наклон {source.DowntiltDeg:F1} град";
        cv.LabelX = "расстояние от мачты, м";
        cv.LabelY = "ППЭ, мкВт/см2";

        cv.AddPlot(V(distance), V(density), "ППЭ на высоте наблюдателя", Sky, 2);
        cv.AddPlot(V([distance[0], distance[^1]]), V([limit, limit]), "ПДУ", Pink, 2);

        int peak = 0;
        for (int i = 1; i < density.Length; i++)
            if (density[i] > density[peak]) peak = i;

        double boundary = scene.BoundaryDistance(0, observer, maxRangeM: 3000);

        var rep = new ReportBuilder()
            .Metric("ЭИИМ", source.EirpW / 1000, "кВт", $"{source.EirpDbm:F1} дБм", MetricTone.Neutral, "F2")
            .Metric("Максимум ППЭ", density[peak], "мкВт/см2",
                $"на расстоянии {distance[peak]:F0} м от мачты",
                density[peak] > limit ? MetricTone.Bad : MetricTone.Good, "F3")
            .Metric("Расстояние максимума", distance[peak], "м",
                "Наклонённый луч кладёт пятно на землю не у подножия мачты",
                MetricTone.Neutral, "F0")
            .Metric("Граница зоны", double.IsNaN(boundary) ? "> 3000" : $"{boundary:F0}", "м",
                "Дальше норматив выполняется",
                boundary == 0 ? MetricTone.Good : MetricTone.Warn);

        rep.Table("Зоны излучателя", ["Величина", "Значение"], [false, true])
            .Row("Эквивалентный диаметр раскрыва, м", source.EquivalentApertureDiameterM.ToString("F3"))
            .Row("Ближняя зона до, м", source.NearFieldBoundaryM.ToString("F2"))
            .Row("Дальняя зона с, м", source.FarFieldBoundaryM.ToString("F2"))
            .Row("Полка ближней зоны, Вт/м2", source.NearFieldPowerDensityWPerM2.ToString("F1"))
            .Row("ПДУ, мкВт/см2", limit.ToString("F2"))
            .Row("Мощность на антенне, Вт", source.RadiatedPowerW.ToString("F1"));

        rep.Note("Максимум облучения на земле лежит там, куда наклонённый луч кладёт своё пятно, " +
                 "а не у подножия мачты: под антенной наблюдатель находится глубоко в провале " +
                 "вертикальной диаграммы. Расчёт только по расстоянию, без учёта высоты и наклона, " +
                 "этот максимум пропускает.");

        return Png(cv, s, report: rep.Build());
    }

    private static DemoResult SanitaryZone(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var scene = new ExposureScene { Standard = ExposureStandard.Sanpin };
        for (int i = 0; i < 3; i++) scene.Add(BuildSource(p, i * 120));

        double observer = N(p, "obs", 2);
        double antennaLevel = scene.Sources[0].Position.Z;

        var cv = MakeView(s);
        var first = scene.Sources[0];
        cv.ChartName = $"Границы зоны: 3 сектора по {first.EirpW / 1000:F2} кВт ЭИИМ, подвес {antennaLevel:F0} м";
        cv.LabelX = "восток, м";
        cv.LabelY = "север, м";

        var contour = DrawContour(cv, scene, observer,
            $"На высоте {observer:F1} м", Sky);
        DrawContour(cv, scene, antennaLevel,
            $"На уровне антенн ({antennaLevel:F0} м)", Pink);
        cv.AddScatter(V([0.0]), V([0.0]), "Мачта", Amber);

        double worst = 0, worstAzimuth = 0;
        foreach (var (azimuth, distance) in contour)
        {
            double r = double.IsNaN(distance) ? 3000 : distance;
            if (r > worst) { worst = r; worstAzimuth = azimuth; }
        }

        var atMast = scene.AssessAt(new SitePoint(0, 1, observer));
        var atRoof = scene.AssessAt(new SitePoint(0, 5, first.Position.Z));

        var rep = new ReportBuilder()
            .Metric("Максимум границы", worst, "м", $"по азимуту {worstAzimuth:F0} град",
                worst > 0 ? MetricTone.Warn : MetricTone.Good, "F1")
            .Metric("У подножия мачты", atMast.PowerDensityMicroWattPerCm2, "мкВт/см2",
                $"доля от ПДУ {atMast.Ratio:F3}",
                atMast.IsCompliant ? MetricTone.Good : MetricTone.Bad, "F3")
            .Metric("На уровне антенн, 5 м", atRoof.PowerDensityMicroWattPerCm2, "мкВт/см2",
                $"доля от ПДУ {atRoof.Ratio:F1} - зона работ обслуживающего персонала",
                atRoof.IsCompliant ? MetricTone.Good : MetricTone.Bad, "F1")
            .Metric("Суммарная ЭИИМ", 3 * first.EirpW / 1000, "кВт",
                "Три сектора", MetricTone.Neutral, "F2");

        var t = rep.Table("Граница по азимутам", ["Азимут, град", "Дальность, м"], [true, true]);
        foreach (var (azimuth, distance) in contour)
        {
            if (azimuth % 15 != 0) continue;
            t.Row(azimuth.ToString("F0"),
                double.IsNaN(distance) ? "> 3000" : distance.ToString("F1"));
        }

        rep.Note("Источники суммируются не по ваттам, а по долям от ПДУ: у каждой частоты свой " +
                 "предел, поэтому складывать мощности между диапазонами нельзя. Норма выполнена, " +
                 "когда сумма долей не превышает единицы. Границы ищутся бисекцией из " +
                 "AI.Solvers.Math - доля монотонно убывает с расстоянием.");

        return Png(cv, s, report: rep.Build());
    }

    /// <summary>
    /// Строит контур границы зоны на заданной высоте и возвращает его
    /// в полярном виде для дальнейшего разбора.
    /// </summary>
    private static IReadOnlyList<(double AzimuthDeg, double DistanceM)> DrawContour(
        AI.Charts.ChartView cv, ExposureScene scene, double heightM,
        string label, SkiaSharp.SKColor color)
    {
        var contour = scene.BoundaryContour(heightM, 5, 3000);

        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var (azimuth, distance) in contour)
        {
            double r = double.IsNaN(distance) ? 3000 : distance;
            double rad = azimuth * Math.PI / 180.0;
            xs.Add(r * Math.Sin(rad));
            ys.Add(r * Math.Cos(rad));
        }

        if (xs.Count > 0)
        {
            xs.Add(xs[0]);
            ys.Add(ys[0]);
            cv.AddPlot(V(xs), V(ys), label, color, 2);
        }

        return contour;
    }
}
