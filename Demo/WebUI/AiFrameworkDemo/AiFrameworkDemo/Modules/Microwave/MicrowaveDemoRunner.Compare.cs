using AI.Microwave.Calculators;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>Сравнение трёх схем под одно ТЗ - ради чего и заведён общий контракт.</summary>
public static partial class MicrowaveDemoRunner
{
    private static DemoResult CompareAll(IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var prm = BuildParams(p);
        IAntennaCalculator[] calcs =
        [
            new HornAntennaCalculator(),
            new ParabolicAntennaCalculator(),
            new HornWithLensCalculator(),
        ];

        var results = calcs.Select(c => (Calc: c, R: c.Calculate(prm))).ToList();

        var cv = MakeView(s);
        cv.ChartName = $"Одно ТЗ ({prm.FrequencyMHz:F0} МГц, {prm.PowerWatts:F0} Вт, " +
                       $"{prm.RequiredBeamwidthDegrees:F1} град) - три конструкции";
        cv.LabelX = "вариант";
        cv.LabelY = "относительно лучшего, %";

        double bestGain = results.Max(x => x.R.GainDbi);
        double minMass = results.Where(x => x.R.WeightKg > 0).Min(x => x.R.WeightKg);
        double minLength = results.Where(x => x.R.TotalLengthM > 0).Min(x => x.R.TotalLengthM);

        var idx = V([1.0, 2.0, 3.0]);
        cv.AddBar(idx, V(results.Select(x => 100.0 * x.R.GainDbi / bestGain)), "Усиление", Sky);
        cv.AddBar(idx, V(results.Select(x => x.R.WeightKg > 0 ? 100.0 * minMass / x.R.WeightKg : 0)),
            "Лёгкость (обратная масса)", Emerald);
        cv.AddBar(idx, V(results.Select(x => x.R.TotalLengthM > 0 ? 100.0 * minLength / x.R.TotalLengthM : 0)),
            "Компактность (обратная длина)", Amber);

        var rep = new ReportBuilder();

        var best = results
            .Where(x => x.R.MeetsAllRequirements)
            .OrderBy(x => x.R.WeightKg)
            .FirstOrDefault();

        rep.Metric("Проходят ТЗ", results.Count(x => x.R.MeetsAllRequirements), "из 3",
            "ШДН, УБЛ и запас по пробою одновременно",
            results.Any(x => x.R.MeetsAllRequirements) ? MetricTone.Good : MetricTone.Bad);

        if (best.Calc is not null)
        {
            rep.Metric("Лучший вариант", best.Calc.AntennaType, null,
                "Самый лёгкий из проходящих ТЗ", MetricTone.Good);
            rep.Metric("Его масса", best.R.WeightKg, "кг", null, MetricTone.Neutral, "F1");
            rep.Metric("Его усиление", best.R.GainDbi, "дБи", null, MetricTone.Neutral, "F2");
        }

        var t = rep.Table("Сравнение",
            ["Тип", "G, дБи", "КИП", "ШДН, град", "УБЛ, дБ", "Масса, кг", "Длина, м", "Запас", "ТЗ"],
            [false, true, true, true, true, true, true, true, false]);

        foreach (var (calc, r) in results)
        {
            t.Row(calc.AntennaType,
                r.GainDbi.ToString("F2"),
                r.Efficiency.ToString("F3"),
                r.BeamwidthEPlane.ToString("F2"),
                r.SideLobeLevel.ToString("F1"),
                r.WeightKg.ToString("F1"),
                r.TotalLengthM.ToString("F2"),
                r.SafetyMargin.ToString("F0"),
                r.MeetsAllRequirements ? "да" : "нет");
        }

        var w = rep.Table("Почему вариант не проходит", ["Тип", "Причина"]);
        foreach (var (calc, r) in results)
        {
            foreach (var warn in r.Warnings)
                w.Row(calc.AntennaType, warn);
        }

        rep.Note("Все три расчёта получают одно и то же задание и возвращают один и тот же тип " +
                 "результата, поэтому сравнимы построчно. Наиболее нагруженная точка тракта у всех " +
                 "трёх обычно одна - горловина питающего волновода, а не раскрыв.");

        var log = string.Join("\n\n", results.Select(x => DumpLog(x.Calc, prm, x.R)));
        return Png(cv, s, textOutput: log, report: rep.Build());
    }
}
