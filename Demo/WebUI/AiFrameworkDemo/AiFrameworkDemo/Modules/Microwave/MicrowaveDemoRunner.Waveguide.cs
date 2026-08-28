using AI.Microwave.Models;
using AI.Microwave.Physics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>Волноводный тракт: дисперсия, затухание, допустимая мощность.</summary>
public static partial class MicrowaveDemoRunner
{
    private static DemoResult WaveguideTe10(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var standards = RectangularWaveguide.GetStandards();
        var wg = standards[Math.Clamp(I(p, "wg", 3), 0, standards.Count - 1)];
        var metals = MaterialProperties.GetStandardMaterials();
        var metal = metals[Math.Clamp(I(p, "mat", 0), 0, metals.Count - 1)];

        double fc = wg.CutoffTE10Hz;
        double fMin = 1.02 * fc;
        double fMax = 1.15 * wg.CutoffNextModeHz;

        const int n = 400;
        var fs = new double[n];
        var impedance = new double[n];
        var attenuation = new double[n];
        var guideLambda = new double[n];

        for (int i = 0; i < n; i++)
        {
            double f = fMin + (fMax - fMin) * i / (n - 1.0);
            fs[i] = f / 1e9;
            impedance[i] = wg.WaveImpedanceTE10(f);
            attenuation[i] = wg.AttenuationDbPerM(f, metal.Conductivity) * 1000.0;
            guideLambda[i] = wg.GuideWavelength(f) * 1000.0;
        }

        var cv = MakeView(s);
        cv.ChartName = $"{wg.Standard} ({wg.WidthMm:F1} x {wg.HeightMm:F1} мм), {metal.Name}: " +
                       $"fc = {fc / 1e9:F3} ГГц";
        cv.LabelX = "частота, ГГц";
        cv.LabelY = "Z, Ом  |  затухание, дБ/км  |  lambda_g, мм";

        cv.AddPlot(V(fs), V(impedance), "Z волновое TE10, Ом", Sky, 2);
        cv.AddPlot(V(fs), V(attenuation), "Затухание, дБ/км", Amber, 2);
        cv.AddPlot(V(fs), V(guideLambda), "lambda в волноводе, мм", Violet, 1);
        cv.AddPlot(V([wg.BandLowHz / 1e9, wg.BandLowHz / 1e9]), V([0.0, 1200.0]),
            "Рабочая полоса", Slate, 1);
        cv.AddPlot(V([wg.BandHighHz / 1e9, wg.BandHighHz / 1e9]), V([0.0, 1200.0]), "", Slate, 1);

        double fMid = 0.5 * (wg.BandLowHz + wg.BandHighHz);
        var rep = new ReportBuilder()
            .Metric("Критическая частота", fc / 1e9, "ГГц", "TE10: ниже неё волновод заперт",
                MetricTone.Neutral, "F3")
            .Metric("Высшая мода", wg.CutoffNextModeHz / 1e9, "ГГц",
                "TE20 или TE01: выше режим перестаёт быть одномодовым", MetricTone.Warn, "F3")
            .Metric("Рабочая полоса",
                $"{wg.BandLowHz / 1e9:F2} - {wg.BandHighHz / 1e9:F2}", "ГГц",
                "От 1.25 fc до 0.95 от высшей моды", MetricTone.Good)
            .Metric("Затухание в середине полосы",
                wg.AttenuationDbPerM(fMid, metal.Conductivity) * 1000.0, "дБ/км",
                $"{metal.Name}, скин-слой " +
                $"{MicrowavePhysics.SkinDepth(fMid, metal.Conductivity) * 1e6:F2} мкм",
                MetricTone.Neutral, "F1");

        var t = rep.Table("Стандартный ряд EIA",
            ["Волновод", "a, мм", "b, мм", "fc, ГГц", "Полоса, ГГц", "дБ/км"],
            [false, true, true, true, false, true]);

        foreach (var w in standards)
        {
            double mid = 0.5 * (w.BandLowHz + w.BandHighHz);
            t.Row(w.Standard,
                w.WidthMm.ToString("F2"),
                w.HeightMm.ToString("F2"),
                (w.CutoffTE10Hz / 1e9).ToString("F3"),
                $"{w.BandLowHz / 1e9:F2} - {w.BandHighHz / 1e9:F2}",
                (w.AttenuationDbPerM(mid, metal.Conductivity) * 1000.0).ToString("F1"));
        }

        rep.Note("Волновое сопротивление TE10 равно eta0 / sqrt(1 - (fc/f)^2): у критической " +
                 "частоты оно уходит в бесконечность, с ростом частоты стремится к 377 Ом. " +
                 "Затухание у отсечки тоже растёт неограниченно, поэтому рабочая полоса " +
                 "начинается не от fc, а от 1.25 fc.");

        return Png(cv, s, report: rep.Build());
    }

    private static DemoResult PowerHandling(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        double altitude = N(p, "alt", 0);
        double temperature = N(p, "temp", 20);
        double humidity = N(p, "hum", 50);
        double requiredMargin = N(p, "margin", 3);

        var env = new EnvironmentalConditions
        {
            Altitude = altitude,
            Temperature = temperature,
            Humidity = humidity,
        };

        var standards = RectangularWaveguide.GetStandards();

        // Предельная мощность: поле в горловине, делённое на требуемый запас,
        // не должно превышать порог пробоя. E ~ sqrt(P), поэтому P ~ 1/margin^2.
        double MaxPower(RectangularWaveguide wg, EnvironmentalConditions e)
        {
            double f = 0.5 * (wg.BandLowHz + wg.BandHighHz);
            double allowed = e.GetBreakdownFieldStrength() / requiredMargin;
            return allowed * allowed * wg.CrossSectionAreaM2 / (4.0 * wg.WaveImpedanceTE10(f));
        }

        const int n = 200;
        var alts = new double[n];
        var thresholds = new double[n];
        var powers = new double[n];
        var wgRef = standards[3];

        for (int i = 0; i < n; i++)
        {
            double h = 12000.0 * i / (n - 1.0);
            var e = new EnvironmentalConditions
            {
                Altitude = h,
                Temperature = temperature,
                Humidity = humidity,
            };
            alts[i] = h / 1000.0;
            thresholds[i] = e.GetBreakdownFieldStrength() / 1e6;
            powers[i] = MaxPower(wgRef, e) / 1000.0;
        }

        var cv = MakeView(s);
        cv.ChartName = $"Порог пробоя и допустимая мощность {wgRef.Standard} по высоте " +
                       $"({temperature:F0} C, {humidity:F0} %, запас {requiredMargin:F1}x)";
        cv.LabelX = "высота, км";
        cv.LabelY = "порог, МВ/м  |  мощность, кВт";

        cv.AddPlot(V(alts), V(thresholds), "Порог пробоя, МВ/м", Pink, 2);
        cv.AddPlot(V(alts), V(powers), $"Допустимая мощность {wgRef.Standard}, кВт", Sky, 2);
        cv.AddScatter(V([altitude / 1000.0]), V([env.GetBreakdownFieldStrength() / 1e6]),
            "Рабочая точка", Amber);

        var rep = new ReportBuilder()
            .Metric("Порог пробоя", env.GetBreakdownFieldStrength() / 1e6, "МВ/м",
                "Пропорционален плотности воздуха, то есть p/T",
                env.GetBreakdownFieldStrength() < 2e6 ? MetricTone.Warn : MetricTone.Good, "F3")
            .Metric("Абсолютное давление", env.GetAbsolutePressureAtm(), "атм",
                "Барометрическая поправка на высоту", MetricTone.Neutral, "F3")
            .Metric("Точка росы", env.GetDewPoint(), "C",
                "Ниже неё на диэлектрике выпадает конденсат",
                env.GetDewPoint() > temperature - 5 ? MetricTone.Warn : MetricTone.Neutral, "F1")
            .Metric($"Мощность {wgRef.Standard}", MaxPower(wgRef, env) / 1000.0, "кВт",
                $"При запасе {requiredMargin:F1}x в середине рабочей полосы",
                MetricTone.Neutral, "F1");

        var t = rep.Table("Допустимая мощность по ряду EIA",
            ["Волновод", "Сечение, см2", "Z TE10, Ом", "P макс, кВт"],
            [false, true, true, true]);

        foreach (var wg in standards)
        {
            double f = 0.5 * (wg.BandLowHz + wg.BandHighHz);
            t.Row(wg.Standard,
                (wg.CrossSectionAreaM2 * 1e4).ToString("F2"),
                wg.WaveImpedanceTE10(f).ToString("F0"),
                (MaxPower(wg, env) / 1000.0).ToString("F1"));
        }

        rep.Note("Поле в горловине волновода максимально по всему тракту: сечение там " +
                 "минимально. Проверять электрическую прочность по раскрыву рупора значит " +
                 "проверять самое безопасное место - в типовом случае запас там на два " +
                 "порядка больше.");

        return Png(cv, s, report: rep.Build());
    }
}
