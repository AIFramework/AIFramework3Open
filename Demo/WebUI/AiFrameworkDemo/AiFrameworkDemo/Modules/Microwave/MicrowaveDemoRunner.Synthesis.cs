using AI.Charts;
using AI.Microwave.Calculators;
using AI.Microwave.Models;
using AiFrameworkDemo.Core;
using SkiaSharp;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>Синтез трёх типов антенн под одно техническое задание.</summary>
public static partial class MicrowaveDemoRunner
{
    private static DemoResult HornDesign(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var prm = BuildParams(p);
        var calc = new HornAntennaCalculator();
        var r = calc.Calculate(prm);

        var cv = MakeView(s);
        cv.ChartName = $"Пирамидальный рупор: {r.ApertureWidthM * 1000:F0} x " +
                       $"{r.ApertureHeightM * 1000:F0} мм, длина {r.AxialLengthM:F2} м, {r.GainDbi:F1} дБи";
        cv.LabelX = "по оси, м";
        cv.LabelY = "поперечный размер, м";

        DrawFlare(cv, r.AxialLengthM, prm.Waveguide.WidthM, r.ApertureWidthM,
            "H-плоскость (широкая стенка)", Sky);
        DrawFlare(cv, r.AxialLengthM, prm.Waveguide.HeightM, r.ApertureHeightM,
            "E-плоскость (узкая стенка)", Amber);

        var rep = BaseReport(prm, r);
        rep.Note("Апертура синтезирована из требуемой ШДН теми же коэффициентами 56 и 67, " +
                 "по которым потом проверяется луч, поэтому ШДН совпадает с ТЗ точно. " +
                 $"Фазовая ошибка в раскрыве: E {r.SpecificParameters.GetValueOrDefault("PhaseErrorE"):F3}, " +
                 $"H {r.SpecificParameters.GetValueOrDefault("PhaseErrorH"):F3} (оптимум 0.25 и 0.375).");

        return Png(cv, s, textOutput: DumpLog(calc, prm, r), report: rep.Build());
    }

    /// <summary>Контур раскрыва рупора в одной плоскости.</summary>
    private static void DrawFlare(ChartView cv, double length, double throat, double aperture,
        string name, SKColor color)
    {
        double[] z = [0, length, length, 0, 0];
        double[] y = [throat / 2, aperture / 2, -aperture / 2, -throat / 2, throat / 2];
        cv.AddPlot(V(z), V(y), name, color, 2);
    }

    private static DemoResult ParabolicDesign(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var prm = BuildParams(p);
        var calc = new ParabolicAntennaCalculator();
        var r = calc.Calculate(prm);

        double diameter = r.SpecificParameters.GetValueOrDefault("Diameter_m", 1);
        double focal = r.SpecificParameters.GetValueOrDefault("FocalLength_m", 1);

        var cv = MakeView(s);
        cv.ChartName = $"Параболоид D = {diameter:F2} м, f/D = {prm.ReflectorFocalToDiameterRatio:F2}, " +
                       $"{r.GainDbi:F1} дБи, {r.WeightKg:F0} кг";
        cv.LabelX = "по оси, м";
        cv.LabelY = "радиус, м";

        const int n = 201;
        var zs = new double[n];
        var rs = new double[n];
        for (int i = 0; i < n; i++)
        {
            double radius = -diameter / 2 + diameter * i / (n - 1.0);
            rs[i] = radius;
            zs[i] = radius * radius / (4 * focal);
        }

        double edge = diameter / 2;
        double edgeZ = edge * edge / (4 * focal);

        cv.AddPlot(V(zs), V(rs), "Профиль зеркала", Sky, 2);
        cv.AddPlot(V([focal, edgeZ]), V([0.0, edge]), "Крайний луч", Slate, 1);
        cv.AddPlot(V([focal, edgeZ]), V([0.0, -edge]), "", Slate, 1);
        cv.AddScatter(V([focal]), V([0.0]), "Фокус (облучатель)", Amber);

        var rep = BaseReport(prm, r);
        rep.Table("Бюджет КИП", ["Составляющая", "Значение"], [false, true])
            .Row("Спадание к краю", r.SpecificParameters.GetValueOrDefault("TaperEfficiency").ToString("F3"))
            .Row("Перехват", r.SpecificParameters.GetValueOrDefault("SpilloverEfficiency").ToString("F3"))
            .Row("Затенение", r.SpecificParameters.GetValueOrDefault("BlockageEfficiency").ToString("F3"))
            .Row("Профиль (Рузе)", r.SpecificParameters.GetValueOrDefault("RuzeEfficiency").ToString("F4"))
            .Row("Прочие потери", prm.MiscellaneousEfficiency.ToString("F2"))
            .Row("Итого", r.Efficiency.ToString("F3"));

        rep.Note($"Край зеркала виден из фокуса под углом " +
                 $"{r.SpecificParameters.GetValueOrDefault("RimHalfAngle_deg"):F1} град, поэтому ДН " +
                 $"облучателя выбрана {r.SpecificParameters.GetValueOrDefault("FeedBeamwidth_deg"):F1} " +
                 "град - ровно так, чтобы на кромке получился заданный спад.");

        return Png(cv, s, textOutput: DumpLog(calc, prm, r), report: rep.Build());
    }
}
