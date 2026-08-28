using AI.Microwave.Physics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>
/// Апертурная теория: почему зеркало и линза считаются одним и тем же кодом.
/// </summary>
public static partial class MicrowaveDemoRunner
{
    private static DemoResult EdgeTaper(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        double working = N(p, "taper", 10);

        const int n = 251;
        var taper = new double[n];
        var taperEff = new double[n];
        var spillEff = new double[n];
        var total = new double[n];
        var sidelobe = new double[n];
        var beamFactor = new double[n];

        for (int i = 0; i < n; i++)
        {
            double edge = 25.0 * i / (n - 1.0);
            taper[i] = edge;
            taperEff[i] = 100.0 * ApertureIllumination.TaperEfficiency(edge);
            spillEff[i] = 100.0 * ApertureIllumination.SpilloverEfficiency(edge);
            total[i] = taperEff[i] * spillEff[i] / 100.0;
            sidelobe[i] = -ApertureIllumination.SidelobeLevelDb(edge);
            beamFactor[i] = ApertureIllumination.BeamwidthFactor(edge);
        }

        double bestTaper = taper[Array.IndexOf(total, total.Max())];

        var cv = MakeView(s);
        cv.ChartName = $"Спад к краю апертуры: КИП максимален при {bestTaper:F1} дБ";
        cv.LabelX = "спад поля на краю апертуры, дБ";
        cv.LabelY = "проценты  |  -УБЛ, дБ  |  коэффициент k";

        cv.AddPlot(V(taper), V(taperEff), "КИП по спаданию, %", Sky, 2);
        cv.AddPlot(V(taper), V(spillEff), "Перехват, %", Emerald, 2);
        cv.AddPlot(V(taper), V(total), "Произведение, %", Amber, 3);
        cv.AddPlot(V(taper), V(sidelobe), "УБЛ со знаком минус, дБ", Pink, 2);
        cv.AddPlot(V(taper), V(beamFactor), "k в theta = k lambda / D", Violet, 1);
        cv.AddScatter(V([working]),
            V([100.0 * ApertureIllumination.TaperEfficiency(working)
                     * ApertureIllumination.SpilloverEfficiency(working)]),
            "Рабочая точка", Slate);

        var rep = new ReportBuilder()
            .Metric("Рабочий спад", working, "дБ", "Задан ползунком", MetricTone.Neutral, "F1")
            .Metric("КИП по спаданию", ApertureIllumination.TaperEfficiency(working), "",
                "Чем ровнее освещена апертура, тем выше", MetricTone.Neutral, "F3")
            .Metric("Перехват", ApertureIllumination.SpilloverEfficiency(working), "",
                "Доля мощности облучателя, попавшая на апертуру", MetricTone.Neutral, "F3")
            .Metric("УБЛ", ApertureIllumination.SidelobeLevelDb(working), "дБ",
                "Первый боковой лепесток круглой апертуры",
                ApertureIllumination.SidelobeLevelDb(working) < -20 ? MetricTone.Good : MetricTone.Warn,
                "F1")
            .Metric("Оптимум произведения", bestTaper, "дБ",
                "Максимум КИП по спаданию, умноженного на перехват", MetricTone.Good, "F1");

        var t = rep.Table("Классические рабочие точки",
            ["Спад, дБ", "КИП спад.", "Перехват", "Произв.", "УБЛ, дБ", "k"],
            [true, true, true, true, true, true]);

        foreach (double v in new[] { 0.0, 5.0, 8.0, 10.0, 12.0, 15.0, 20.0, 25.0 })
        {
            double te = ApertureIllumination.TaperEfficiency(v);
            double se = ApertureIllumination.SpilloverEfficiency(v);
            t.Row(v.ToString("F0"), te.ToString("F3"), se.ToString("F3"),
                (te * se).ToString("F3"),
                ApertureIllumination.SidelobeLevelDb(v).ToString("F1"),
                ApertureIllumination.BeamwidthFactor(v).ToString("F1"));
        }

        rep.Note("Спад поля на краю - единственный параметр, от которого зависят и КИП, " +
                 "и перехват, и УБЛ, и коэффициент в формуле ширины луча. Поэтому зеркало " +
                 "и линза в AI.Microwave считаются одним и тем же кодом: различается только " +
                 "то, как задан угол на кромку. Классические -10 дБ дают k = 70, то есть " +
                 "знакомую формулу theta = 70 lambda / D.");

        return Png(cv, s, report: rep.Build());
    }
}
