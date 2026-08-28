using AI.Charts;
using AI.Microwave.Calculators;
using AI.Microwave.Geometry;
using AI.Microwave.Models;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>Линзовая антенна: синтез и геометрия профиля.</summary>
public static partial class MicrowaveDemoRunner
{
    private static DemoResult LensDesign(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var prm = BuildParams(p);
        var calc = new HornWithLensCalculator();
        var r = calc.Calculate(prm);

        double focal = r.SpecificParameters.GetValueOrDefault("LensFocalLength_m", 1);
        var lens = new DielectricLens(prm.LensMaterial, r.ApertureWidthM, focal,
            prm.WavelengthM, prm.LensEdgeThicknessM);

        var cv = MakeView(s);
        cv.ChartName = $"Линза {prm.LensMaterial.Name}: D = {lens.DiameterM:F2} м, " +
                       $"толщина {lens.CenterThicknessM * 100:F0} см, {lens.WeightKg:F0} кг";
        cv.LabelX = "по оси, м";
        cv.LabelY = "радиус, м";
        DrawLens(cv, lens);

        var rep = BaseReport(prm, r);
        rep.Table("Линза", ["Величина", "Значение"], [false, true])
            .Row("Показатель преломления", prm.LensMaterial.RefractiveIndex.ToString("F3"))
            .Row("Толщина в центре, см", (lens.CenterThicknessM * 100).ToString("F1"))
            .Row("Кромка видна под углом, град", lens.RimHalfAngleDeg.ToString("F1"))
            .Row("Объём, м3", lens.VolumeM3.ToString("F4"))
            .Row("Масса сплошной, кг", lens.WeightKg.ToString("F1"))
            .Row("Зон Френеля", lens.ZoneCount.ToString())
            .Row("Масса зонированной, кг", lens.ZonedWeightKg.ToString("F1"))
            .Row("Потери в диэлектрике, дБ",
                r.SpecificParameters.GetValueOrDefault("DielectricLoss_dB").ToString("F3"))
            .Row("Потери на отражение, дБ",
                r.SpecificParameters.GetValueOrDefault("ReflectionLoss_dB").ToString("F3"));

        rep.Note("Толщина сплошной линзы растёт вместе с апертурой, а масса - как её куб. " +
                 "Это и есть причина, по которой на дециметровых волнах линзовая схема " +
                 "проигрывает зеркальной, выигрывая при этом по длине.");

        return Png(cv, s, textOutput: DumpLog(calc, prm, r), report: rep.Build());
    }

    private static DemoResult LensProfile(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var dielectrics = DielectricProperties.GetStandardDielectrics();
        var material = dielectrics[Math.Clamp(I(p, "diel", 0), 0, dielectrics.Count - 1)];

        double lambda = AI.Microwave.Physics.MicrowavePhysics.Wavelength(N(p, "f", 2450) * 1e6);
        double diameter = N(p, "d", 1.7);
        var lens = new DielectricLens(material, diameter, N(p, "fd", 1.0) * diameter, lambda, 0.01);

        var cv = MakeView(s);
        cv.ChartName = $"{material.Name}: n = {material.RefractiveIndex:F2}, D = {diameter:F2} м, " +
                       $"{lens.ZoneCount} зон Френеля";
        cv.LabelX = "радиус, м";
        cv.LabelY = "толщина, м";

        const int n = 241;
        var rr = new double[n];
        var solid = new double[n];
        var zoned = new double[n];
        for (int i = 0; i < n; i++)
        {
            double rho = diameter / 2 * i / (n - 1.0);
            rr[i] = rho;
            solid[i] = lens.ThicknessAt(rho);
            zoned[i] = lens.ZonedThicknessAt(rho);
        }

        cv.AddPlot(V(rr), V(solid), "Сплошная линза", Sky, 2);
        cv.AddPlot(V(rr), V(zoned), "Зонированная", Pink, 2);

        double saved = 100.0 * (1.0 - lens.ZonedVolumeM3 / lens.VolumeM3);
        var rep = new ReportBuilder()
            .Metric("Толщина в центре", lens.CenterThicknessM * 100, "см",
                "Из геометрии гиперболического профиля, а не из фокусного расстояния",
                lens.ThicknessToDiameter > 0.25 ? MetricTone.Warn : MetricTone.Neutral, "F1")
            .Metric("Масса сплошной", lens.WeightKg, "кг", $"Объём {lens.VolumeM3:F4} м3",
                lens.WeightKg > 50 ? MetricTone.Bad : MetricTone.Neutral, "F1")
            .Metric("Масса зонированной", lens.ZonedWeightKg, "кг",
                $"Экономия {saved:F0} %", MetricTone.Good, "F1")
            .Metric("Шаг зонирования", lens.ZoneStepM * 1000, "мм",
                "lambda / (n - 1): срез на эту величину не меняет фазу", MetricTone.Neutral, "F0");

        rep.Table("Профиль", ["Величина", "Значение"], [false, true])
            .Row("Фокусное расстояние, м", lens.FocalLengthM.ToString("F3"))
            .Row("Кромка видна под углом, град", lens.RimHalfAngleDeg.ToString("F2"))
            .Row("Толщина на краю, мм", (lens.EdgeThicknessM * 1000).ToString("F0"))
            .Row("Толщина / диаметр", lens.ThicknessToDiameter.ToString("F3"))
            .Row("Средний путь в диэлектрике, м", lens.MeanPathM.ToString("F4"))
            .Row("Затухание в теле, дБ",
                (material.AttenuationNpPerM(lambda) * lens.MeanPathM * 8.686).ToString("F3"));

        rep.Note("Профиль r(theta) = (n-1) f / (n cos(theta) - 1) - гипербола с фокусом в " +
                 "точке облучателя. Корень для каждого радиуса ищется бисекцией, объём берётся " +
                 "адаптивной квадратурой Симпсона: и то и другое - готовые методы AI.Solvers.Math.");

        return Png(cv, s, textOutput: null, report: rep.Build());
    }

    /// <summary>Сечение линзы: гиперболическая грань, плоская грань, зонированный профиль.</summary>
    private static void DrawLens(ChartView cv, DielectricLens lens)
    {
        const int n = 161;
        double radius = lens.DiameterM / 2;
        double back = lens.FocalLengthM + lens.CenterThicknessM;

        var rr = new double[2 * n - 1];
        var front = new double[2 * n - 1];
        var zoned = new double[2 * n - 1];
        for (int i = 0; i < n; i++)
        {
            double rho = radius * i / (n - 1.0);
            double f = back - lens.ThicknessAt(rho);
            double z = back - lens.ZonedThicknessAt(rho);
            rr[n - 1 + i] = rho;
            rr[n - 1 - i] = -rho;
            front[n - 1 + i] = f;
            front[n - 1 - i] = f;
            zoned[n - 1 + i] = z;
            zoned[n - 1 - i] = z;
        }

        cv.AddPlot(V(front), V(rr), "Гиперболическая грань", Sky, 2);
        cv.AddPlot(V([back, back]), V([-radius, radius]), "Плоская грань", Emerald, 2);
        if (lens.ZoneCount > 1)
            cv.AddPlot(V(zoned), V(rr), $"Зонированная ({lens.ZoneCount} зон)", Pink, 1);
        cv.AddScatter(V([0.0]), V([0.0]), "Фокус (облучатель)", Amber);
    }
}
