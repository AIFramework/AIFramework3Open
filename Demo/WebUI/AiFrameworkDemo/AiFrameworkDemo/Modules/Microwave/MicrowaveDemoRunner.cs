using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Microwave.Calculators;
using AI.Microwave.Geometry;
using AI.Microwave.Models;
using AI.Microwave.Physics;
using AiFrameworkDemo.Core;
using SkiaSharp;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>
/// Демонстратор AI.Microwave: синтез антенн под ТЗ, волноводный тракт
/// и апертурная теория, на которой держится синтез.
/// </summary>
public static partial class MicrowaveDemoRunner
{
    private static SKColor Hex(string hex) => SKColor.Parse(hex);

    private static readonly SKColor Sky = Hex("#38bdf8");
    private static readonly SKColor Emerald = Hex("#34d399");
    private static readonly SKColor Amber = Hex("#fbbf24");
    private static readonly SKColor Pink = Hex("#f472b6");
    private static readonly SKColor Slate = Hex("#94a3b8");
    private static readonly SKColor Violet = Hex("#a78bfa");

    private static Vector V(IEnumerable<double> values) => new(values.ToArray());

    public static DemoResult Run(
        string algoKey,
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        DemoSettings s) => algoKey switch
        {
            "horn_design" => HornDesign(p, s),
            "parabolic_design" => ParabolicDesign(p, s),
            "lens_design" => LensDesign(p, s),
            "compare_all" => CompareAll(p, s),
            "waveguide_te10" => WaveguideTe10(p, s),
            "power_handling" => PowerHandling(p, s),
            "edge_taper" => EdgeTaper(p, s),
            "lens_profile" => LensProfile(p, s),
            "exposure_limits" => ExposureLimitsDemo(p, s),
            "exposure_profile" => ExposureProfile(p, s),
            "sanitary_zone" => SanitaryZone(p, s),
            "penetration_uniformity" => PenetrationUniformity(p, s),
            "cavity_modes" => CavityModes(p, s),
            "applicator_balance" => ApplicatorBalance(p, s),
            _ => new DemoResult { Error = $"Неизвестный ключ алгоритма: {algoKey}" },
        };

    /// <summary>Собирает техническое задание из значений ползунков.</summary>
    private static AntennaParameters BuildParams(IReadOnlyDictionary<string, double> p)
    {
        var metals = MaterialProperties.GetStandardMaterials();
        var dielectrics = DielectricProperties.GetStandardDielectrics();

        double freqMhz = N(p, "f", 2450);
        var prm = new AntennaParameters
        {
            FrequencyMHz = freqMhz,
            PowerWatts = N(p, "p", 900),
            RequiredBeamwidthDegrees = N(p, "bw", 5),
            RequiredSidelobeLevelDb = N(p, "sll", -20),
            Material = metals[Math.Clamp(I(p, "mat", 0), 0, metals.Count - 1)],
            LensMaterial = dielectrics[Math.Clamp(I(p, "diel", 0), 0, dielectrics.Count - 1)],
            EdgeTaperDb = N(p, "taper", 10),
            ReflectorFocalToDiameterRatio = N(p, "fd", 0.40),
            LensFocalToDiameterRatio = N(p, "fd", 1.0),
            SurfaceToleranceMm = N(p, "tol", 0.5),
        };

        // Волновод подбирается под частоту: иначе на 10 ГГц остался бы
        // дециметровый WR-340, который там давно многомодовый.
        prm.Waveguide = RectangularWaveguide.SelectForFrequency(prm.FrequencyHz);
        return prm;
    }

    /// <summary>Общая часть отчёта: ключевые числа любой рассчитанной антенны.</summary>
    private static ReportBuilder BaseReport(AntennaParameters prm, AntennaDesignResult r)
    {
        var rep = new ReportBuilder()
            .Metric("Усиление", r.GainDbi, "дБи", "С учётом КИП, потерь и рассогласования",
                r.GainDbi > 25 ? MetricTone.Good : MetricTone.Neutral, "F2")
            .Metric("Ширина луча", r.BeamwidthEPlane, "град",
                $"Требуется {prm.RequiredBeamwidthDegrees:F2} град",
                r.MeetsBeamwidthRequirement ? MetricTone.Good : MetricTone.Bad, "F2")
            .Metric("УБЛ", r.SideLobeLevel, "дБ",
                $"Требуется {prm.RequiredSidelobeLevelDb:F0} дБ",
                r.MeetsSidelobeRequirement ? MetricTone.Good : MetricTone.Bad, "F1")
            .Metric("Масса", r.WeightKg, "кг", "Конструкция целиком",
                r.WeightKg > 300 ? MetricTone.Warn : MetricTone.Neutral, "F1")
            .Metric("Запас по пробою", r.SafetyMargin, "раз",
                $"Худшая точка тракта: {r.HotSpot}",
                r.IsSafe ? MetricTone.Good : MetricTone.Bad, "F1");

        rep.Table("Итог", ["Величина", "Значение"], [false, true])
            .Row("КНД, дБи", r.DirectivityDbi.ToString("F2"))
            .Row("Полный КИП", r.Efficiency.ToString("F3"))
            .Row("КСВ", r.VSWR.ToString("F2"))
            .Row("Возвратные потери, дБ", r.ReturnLossDb.ToString("F1"))
            .Row("Габарит по оси, м", r.TotalLengthM.ToString("F3"))
            .Row("Потери, Вт", r.OhmicLossesW.ToString("F2"))
            .Row("Перегрев, К", r.MaxTemperatureRise.ToString("F2"))
            .Row("Дальняя зона, м", r.FarFieldDistanceM.ToString("F1"))
            .Row("Отн. стоимость", r.CostRelative.ToString("F1"));

        if (r.Warnings.Count > 0)
        {
            var t = rep.Table("Предупреждения", ["Что не так"]);
            foreach (var w in r.Warnings) t.Row(w);
        }

        if (r.Recommendations.Count > 0)
        {
            var t = rep.Table("Рекомендации", ["Что сделать"]);
            foreach (var w in r.Recommendations) t.Row(w);
        }

        return rep;
    }

    /// <summary>Текстовый лог: всё, что калькулятор положил в результат.</summary>
    private static string DumpLog(IAntennaCalculator calc, AntennaParameters prm, AntennaDesignResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($">> new {calc.GetType().Name}().Calculate(parameters)");
        sb.AppendLine($"   f = {prm.FrequencyMHz:F0} МГц, P = {prm.PowerWatts:F0} Вт, " +
                      $"ТЗ по ШДН = {prm.RequiredBeamwidthDegrees:F2} град");
        sb.AppendLine($"   волновод {prm.Waveguide.Standard} " +
                      $"({prm.Waveguide.WidthMm:F2} x {prm.Waveguide.HeightMm:F2} мм), " +
                      $"fc = {prm.Waveguide.CutoffTE10Hz / 1e6:F0} МГц");
        sb.AppendLine($"   материал: {prm.Material.Name}");
        sb.AppendLine();
        foreach (var kv in r.SpecificParameters)
            sb.AppendLine($"   {kv.Key,-32} = {kv.Value:G6}");
        return sb.ToString();
    }
}
