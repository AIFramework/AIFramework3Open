using AI.Microwave.Heating;
using AI.Microwave.Models;
using AI.Microwave.Physics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>СВЧ-нагрев: проникновение, камера, аппликатор.</summary>
public static partial class MicrowaveDemoRunner
{
    private static DielectricMaterial Load(IReadOnlyDictionary<string, double> p, int fallback = 0)
    {
        var loads = DielectricMaterial.GetStandardLoads();
        return loads[Math.Clamp(I(p, "mat", fallback), 0, loads.Count - 1)];
    }

    private static DemoResult PenetrationUniformity(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        double freqHz = N(p, "f", 2450) * 1e6;
        double lambda = MicrowavePhysics.Wavelength(freqHz);
        var material = Load(p);
        double thickness = N(p, "t", 50) / 1000.0;
        double power = N(p, "power", 900);
        double mass = N(p, "mass", 1);

        double depth = material.PenetrationDepthM(lambda);

        const int n = 200;
        var x = new double[n];
        var oneSided = new double[n];
        var twoSided = new double[n];
        for (int i = 0; i < n; i++)
        {
            double d = thickness * i / (n - 1.0);
            x[i] = d * 1000;
            oneSided[i] = 100.0 * Math.Exp(-d / depth);
            twoSided[i] = 100.0 * (Math.Exp(-d / depth) + Math.Exp(-(thickness - d) / depth))
                        / (1.0 + Math.Exp(-thickness / depth));
        }

        var cv = MakeView(s);
        cv.ChartName = $"{material.Name}, {N(p, "f", 2450):F0} МГц: глубина проникновения {depth * 1000:F1} мм";
        cv.LabelX = "глубина от поверхности, мм";
        cv.LabelY = "тепловыделение, % от поверхностного";

        cv.AddPlot(V(x), V(oneSided), "Одностороннее облучение", Sky, 2);
        cv.AddPlot(V(x), V(twoSided), "Двустороннее", Emerald, 2);

        double volumetric = power / Math.Max(mass / material.DensityKgPerM3, 1e-9);
        double field = DielectricHeating.FieldForVolumetricPower(freqHz, material.LossFactor, volumetric);
        double rate = DielectricHeating.HeatingRateKPerS(volumetric, material);

        var rep = new ReportBuilder()
            .Metric("Глубина проникновения", depth * 1000, "мм",
                "Мощность падает в e раз", MetricTone.Neutral, "F1")
            .Metric("Неравномерность", DielectricHeating.SurfaceToCenterRatio(thickness, depth), "раз",
                "Поверхность против середины при одностороннем облучении",
                DielectricHeating.SurfaceToCenterRatio(thickness, depth) > 2
                    ? MetricTone.Bad : MetricTone.Good, "F2")
            .Metric("Скорость нагрева", rate, "К/с",
                $"При {power:F0} Вт в {mass:F1} кг", MetricTone.Neutral, "F3")
            .Metric("Поле в материале", field, "В/м",
                "Действующее значение", MetricTone.Neutral, "F0");

        var t = rep.Table("Материалы на этой частоте",
            ["Материал", "eps'", "eps''", "tg d", "Глубина, мм", "Разгон"],
            [false, true, true, true, true, false]);

        foreach (var m in DielectricMaterial.GetStandardLoads())
        {
            double d = m.PenetrationDepthM(lambda);
            t.Row(m.Name,
                m.RelativePermittivity.ToString("F1"),
                m.LossFactor.ToString("F3"),
                m.LossTangent.ToString("F4"),
                double.IsInfinity(d) ? "прозрачен" : (d * 1000).ToString("F1"),
                m.IsRunawayProne ? "да" : "нет");
        }

        rep.Table("Режим", ["Величина", "Значение"], [false, true])
            .Row("Толщина слоя, мм", (thickness * 1000).ToString("F0"))
            .Row("Двустороннее, неравномерность",
                DielectricHeating.SurfaceToCenterRatioTwoSided(thickness, depth).ToString("F2"))
            .Row("Толщина при неравномерности 2x, мм",
                (DielectricHeating.MaxThicknessForUniformity(depth, 2.0) * 1000).ToString("F0"))
            .Row("Отражение от поверхности, %", (material.SurfaceReflectance * 100).ToString("F1"))
            .Row("Длина волны в материале, мм",
                (material.WavelengthInMaterialM(lambda) * 1000).ToString("F1"))
            .Row("Время нагрева на 60 К, с",
                DielectricHeating.HeatingTimeS(mass, material, 60, power).ToString("F0"));

        rep.Note("Глубина проникновения - главное ограничение СВЧ-нагрева: она не зависит от " +
                 "мощности, поэтому толстый продукт добавлением киловатт не прогреть, можно " +
                 "только сжечь поверхность. Обход - двустороннее облучение, снижение частоты " +
                 "(на 915 МГц проникновение глубже) или выдержка на теплопроводность.");

        return Png(cv, s, report: rep.Build());
    }

    private static DemoResult CavityModes(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var cavity = new MultimodeCavity
        {
            WidthM = N(p, "a", 0.33),
            HeightM = N(p, "b", 0.23),
            DepthM = N(p, "d", 0.35),
        };

        double loadVolume = N(p, "load", 1) / 1000.0;
        double sourceBandwidth = N(p, "bw", 15) * 1e6;
        var steel = MaterialProperties.GetStandardMaterials()[5];
        var water = DielectricMaterial.GetStandardLoads()[0];

        const double f0 = 2.45e9;
        double qEmpty = cavity.WallQualityFactor(f0, steel.Conductivity);
        double qLoaded = loadVolume > 0
            ? cavity.LoadedQualityFactor(f0, steel.Conductivity, loadVolume, water)
            : qEmpty;

        var modes = cavity.Modes(2.3e9, 2.6e9);
        var freqs = modes.Select(m => m.FrequencyHz / 1e9).ToArray();
        var marks = modes.Select(_ => 1.0).ToArray();

        const int n = 300;
        var grid = new double[n];
        var response = new double[n];
        for (int i = 0; i < n; i++)
        {
            double f = 2.3e9 + 0.3e9 * i / (n - 1.0);
            grid[i] = f / 1e9;

            // Сумма лоренцевых контуров всех мод: так выглядит отклик камеры.
            double sum = 0;
            foreach (var mode in modes)
            {
                double detune = 2.0 * qLoaded * (f - mode.FrequencyHz) / mode.FrequencyHz;
                sum += 1.0 / (1.0 + detune * detune);
            }

            response[i] = sum;
        }

        var cv = MakeView(s);
        cv.ChartName = $"Камера {cavity.WidthM:F2} x {cavity.HeightM:F2} x {cavity.DepthM:F2} м " +
                       $"({cavity.VolumeM3 * 1000:F0} л), Q = {qLoaded:F0}";
        cv.LabelX = "частота, ГГц";
        cv.LabelY = "отклик камеры (сумма мод)";

        cv.AddPlot(V(grid), V(response), "Суммарный отклик", Sky, 2);
        if (freqs.Length > 0) cv.AddScatter(V(freqs), V(marks), "Резонансы", Amber);

        int emptyMagnetron = cavity.EffectiveModeCount(f0, 15e6, qEmpty);
        int loadedMagnetron = cavity.EffectiveModeCount(f0, 15e6, qLoaded);
        int solidState = cavity.EffectiveModeCount(f0, sourceBandwidth, qLoaded);
        double efficiency = loadVolume > 0
            ? cavity.HeatingEfficiency(f0, steel.Conductivity, loadVolume, water)
            : 0;

        var rep = new ReportBuilder()
            .Metric("Мод в полосе 2.3...2.6 ГГц", modes.Count, "", "Точный перебор TE и TM",
                MetricTone.Neutral)
            .Metric("Магнетрон, пустая камера", emptyMagnetron, "",
                "Узкие моды, поле стоячее и пятнистое",
                emptyMagnetron < 10 ? MetricTone.Bad : MetricTone.Good)
            .Metric("Магнетрон, с загрузкой", loadedMagnetron, "",
                "Загрузка сбивает добротность, моды перекрываются",
                loadedMagnetron > emptyMagnetron ? MetricTone.Good : MetricTone.Warn)
            .Metric("Источник, полоса " + (sourceBandwidth / 1e6).ToString("F0") + " МГц", solidState, "",
                "Качание частоты добивает равномерность независимо от загрузки",
                MetricTone.Good)
            .Metric("КПД по загрузке", efficiency * 100, "%",
                "Доля мощности в продукт, а не в стенки",
                efficiency > 0.9 ? MetricTone.Good : MetricTone.Warn, "F2");

        rep.Table("Добротность и полосы", ["Величина", "Значение"], [false, true])
            .Row("Q пустой камеры (сталь)", qEmpty.ToString("F0"))
            .Row("Q с загрузкой", qLoaded.ToString("F1"))
            .Row("Ширина моды пустой, кГц", (cavity.ModeBandwidthHz(f0, qEmpty) / 1e3).ToString("F0"))
            .Row("Ширина моды с загрузкой, МГц", (cavity.ModeBandwidthHz(f0, qLoaded) / 1e6).ToString("F1"))
            .Row("Плотность мод (Вейль), 1/МГц", (cavity.ModeDensityPerHz(f0) * 1e6).ToString("F3"))
            .Row("Объём камеры, л", (cavity.VolumeM3 * 1000).ToString("F1"))
            .Row("Площадь стенок, м2", cavity.SurfaceAreaM2.ToString("F3"));

        rep.Note("Равномерность многомодовой камеры определяется числом одновременно " +
                 "возбуждённых мод. Пустая камера имеет добротность в тысячи: моды узкие, " +
                 "магнетрон попадает в единицы из них, поле стоячее и пятнистое. Загрузка " +
                 "сбивает добротность до единиц, моды расплываются и перекрываются - поле " +
                 "выравнивается само. Твердотельный источник добивается того же качанием " +
                 "частоты, но управляемо и на любой загрузке.");

        return Png(cv, s, report: rep.Build());
    }

    private static DemoResult ApplicatorBalance(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        double freqHz = N(p, "f", 2450) * 1e6;
        double lambda = MicrowavePhysics.Wavelength(freqHz);
        var material = Load(p, 4);
        double power = N(p, "power", 6000);
        double deltaT = N(p, "dt", 60);
        bool twoSided = I(p, "sides", 0) == 1;

        var applicator = new TravelingWaveApplicator
        {
            Waveguide = RectangularWaveguide.SelectForFrequency(freqHz),
            Load = material,
            ThicknessM = N(p, "t", 30) / 1000.0,
            TwoSided = twoSided,
        };

        const int n = 200;
        var thickness = new double[n];
        var absorbed = new double[n];
        var reflected = new double[n];
        var transmitted = new double[n];
        var uniformity = new double[n];

        for (int i = 0; i < n; i++)
        {
            double t = 0.002 + 0.198 * i / (n - 1.0);
            var probe = new TravelingWaveApplicator
            {
                Waveguide = applicator.Waveguide,
                Load = material,
                ThicknessM = t,
                TwoSided = twoSided,
            };

            thickness[i] = t * 1000;
            absorbed[i] = probe.AbsorbedFraction(lambda) * 100;
            reflected[i] = probe.ReflectedFraction * 100;
            transmitted[i] = probe.TransmittedFraction(lambda) * 100;
            uniformity[i] = probe.UniformityRatio(lambda) * 10;
        }

        var cv = MakeView(s);
        cv.ChartName = $"{material.Name}, {N(p, "f", 2450):F0} МГц, " +
                       $"{(twoSided ? "двустороннее" : "одностороннее")} облучение";
        cv.LabelX = "толщина слоя, мм";
        cv.LabelY = "доля мощности, %  |  неравномерность x10";

        cv.AddPlot(V(thickness), V(absorbed), "Поглощено продуктом", Emerald, 3);
        cv.AddPlot(V(thickness), V(transmitted), "В балластную нагрузку", Amber, 2);
        cv.AddPlot(V(thickness), V(reflected), "Отражено от поверхности", Pink, 2);
        cv.AddPlot(V(thickness), V(uniformity), "Неравномерность x10", Violet, 1);

        double fraction = applicator.AbsorbedFraction(lambda);
        double throughput = applicator.ThroughputKgPerHour(power, lambda, deltaT);
        double ratio = applicator.UniformityRatio(lambda);

        var rep = new ReportBuilder()
            .Metric("Поглощено за проход", fraction * 100, "%",
                "Остальное отражается либо уходит в балласт",
                fraction > 0.5 ? MetricTone.Good : MetricTone.Warn, "F1")
            .Metric("Производительность", throughput, "кг/ч",
                $"При {power / 1000:F1} кВт и нагреве на {deltaT:F0} К",
                MetricTone.Neutral, "F1")
            .Metric("Неравномерность", ratio, "раз",
                "Поверхность против середины слоя",
                ratio > 2 ? MetricTone.Bad : MetricTone.Good, "F2")
            .Metric("Глубина проникновения", applicator.PenetrationDepthM(lambda) * 1000, "мм",
                "Определяет предельную толщину", MetricTone.Neutral, "F1");

        rep.Table("Баланс мощности", ["Статья", "Доля, %"], [false, true])
            .Row("Поглощено продуктом", (fraction * 100).ToString("F1"))
            .Row("Отражено от поверхности", (applicator.ReflectedFraction * 100).ToString("F1"))
            .Row("В балластную нагрузку", (applicator.TransmittedFraction(lambda) * 100).ToString("F1"));

        rep.Table("Линия", ["Величина", "Значение"], [false, true])
            .Row("Волновод", applicator.Waveguide.Standard)
            .Row("Толщина слоя, мм", (applicator.ThicknessM * 1000).ToString("F0"))
            .Row("Предельная толщина при 2x, мм", (applicator.MaxThicknessM(lambda) * 1000).ToString("F0"))
            .Row("Мощность на 100 кг/ч, кВт",
                (applicator.RequiredPowerW(100, lambda, deltaT) / 1000).ToString("F1"))
            .Row("Поле в материале, В/м",
                applicator.FieldInLoadVPerM(power, lambda, freqHz).ToString("F0"))
            .Row("Запас по тепловому разгону",
                FormatMargin(DielectricHeating.ThermalRunawayMargin(
                    freqHz, material, applicator.FieldInLoadVPerM(power, lambda, freqHz), 15, 100)));

        rep.Note("В аппликаторе бегущей волны непоглощённая мощность не исчезает: её сбрасывают " +
                 "в балластную нагрузку, и это прямой убыток КПД. Толстый слой поглощает больше, " +
                 "но прогревается неравномернее - оптимум лежит там, где кривые поглощения и " +
                 "неравномерности расходятся.");

        return Png(cv, s, report: rep.Build());
    }

    private static string FormatMargin(double margin)
        => double.IsInfinity(margin) ? "устойчиво" : margin.ToString("F2");
}
