using AI.Microwave.Geometry;
using AI.Microwave.Models;
using AI.Microwave.Physics;

namespace AI.Microwave.Calculators;

/// <summary>
/// Осесимметричная параболическая антенна с рупорным облучателем в фокусе.
/// </summary>
public class ParabolicAntennaCalculator : AntennaCalculatorBase
{
    /// <summary>Отношение затеняющего диаметра к апертуре облучателя (облучатель плюс крепление).</summary>
    private const double FeedMountingFactor = 1.5;

    /// <summary>Надбавка к стоимости за формовку зеркала.</summary>
    private const double FormingCostFactor = 1.5;

    /// <summary>Отношение пиковой плотности потока к средней в раскрыве облучателя.</summary>
    private const double FeedPeakToAverage = 1.0 / 0.81;

    /// <inheritdoc/>
    public override string AntennaType => "Параболическая антенна";

    /// <inheritdoc/>
    public override string GetDescription()
    {
        return "Параболическая антенна использует отражатель в форме параболоида вращения " +
               "и облучатель (рупор) в фокусе. Обеспечивает высокое усиление (25-50 дБи), " +
               "узкую диаграмму направленности и отличную направленность.";
    }

    /// <inheritdoc/>
    public override string GetAdvantages()
    {
        return "Очень высокое усиление при малой глубине конструкции\n" +
               "Узкая диаграмма направленности\n" +
               "Низкий уровень боковых лепестков\n" +
               "Широкая полоса частот\n" +
               "Наименьшая масса на децибел усиления";
    }

    /// <inheritdoc/>
    public override string GetDisadvantages()
    {
        return "Большой поперечный габарит\n" +
               "Требуется точное изготовление поверхности\n" +
               "Чувствительность к ветровым нагрузкам\n" +
               "Затенение апертуры облучателем и стойками\n" +
               "Заметный перелив мощности мимо зеркала";
    }

    /// <inheritdoc/>
    protected override void CalculateCore(AntennaParameters param, AntennaDesignResult result)
    {
        double lambda = param.WavelengthM;
        double freqHz = param.FrequencyHz;
        var wg = param.Waveguide;
        double taperDb = param.EdgeTaperDb;

        // -- Геометрия зеркала --------------------------------------------------
        // Коэффициент в theta = k lambda / D берётся из того же спада к краю,
        // которым дальше считаются КИП и УБЛ: при -10 дБ это классические 70.
        double beamFactor = ApertureIllumination.BeamwidthFactor(taperDb);
        double diameter = beamFactor * lambda / param.RequiredBeamwidthDegrees;
        var dish = new Paraboloid(diameter, param.ReflectorFocalToDiameterRatio * diameter);

        result.ApertureWidthM = diameter;
        result.ApertureHeightM = diameter;
        result.AxialLengthM = dish.DepthM;

        result.SpecificParameters["Diameter_m"] = diameter;
        result.SpecificParameters["FocalLength_m"] = dish.FocalLengthM;
        result.SpecificParameters["F_over_D"] = dish.FocalToDiameterRatio;
        result.SpecificParameters["Depth_m"] = dish.DepthM;
        result.SpecificParameters["RimHalfAngle_deg"] = dish.RimHalfAngleDeg;
        result.SpecificParameters["ReflectorSurface_m2"] = dish.SurfaceAreaM2;
        result.SpecificParameters["EdgeTaper_dB"] = taperDb;

        // -- Облучатель ---------------------------------------------------------
        // ДН облучателя подбирается так, чтобы на краю зеркала, видимом из
        // фокуса под углом RimHalfAngle, поле спадало ровно на taperDb.
        double feedHpbw = ApertureIllumination.FeedBeamwidthForTaper(dish.RimHalfAngleDeg, taperDb);
        var feed = PyramidalHorn.ForBeamwidth(feedHpbw, wg, lambda);
        double feedEquivalentDiameter = Math.Sqrt(4.0 * feed.ApertureAreaM2 / Math.PI);

        result.SpecificParameters["FeedBeamwidth_deg"] = feedHpbw;
        result.SpecificParameters["FeedApertureE_m"] = feed.ApertureHeightM;
        result.SpecificParameters["FeedApertureH_m"] = feed.ApertureWidthM;
        result.SpecificParameters["FeedLength_m"] = feed.AxialLengthM;

        // Габарит по оси: облучатель стоит в фокусе и выступает вперёд от него.
        result.TotalLengthM = Math.Max(dish.DepthM, dish.FocalLengthM + feed.AxialLengthM);

        // -- КИП ----------------------------------------------------------------
        // Затенение облучателем и его креплением. При широкой требуемой ДН
        // зеркало становится соизмеримо с облучателем: схема вырождается,
        // поэтому доля ограничивается, а пользователь предупреждается.
        double blockageDiameter = FeedMountingFactor * feedEquivalentDiameter;
        double rawBlockage = Math.Pow(blockageDiameter / diameter, 2);
        double blockageRatio = Math.Min(rawBlockage, 0.5);
        double blockageEfficiency = Math.Pow(1.0 - blockageRatio, 2);

        if (rawBlockage > 0.25)
        {
            result.Warnings.Add(
                $"Облучатель перекрывает {rawBlockage * 100.0:F0} % зеркала: при такой широкой " +
                "ДН зеркальная схема неприменима, расчёт ниже носит оценочный характер.");
        }

        double taperEfficiency = ApertureIllumination.TaperEfficiency(taperDb);
        double spilloverEfficiency = ApertureIllumination.SpilloverEfficiency(taperDb);
        double ruzeEfficiency = MicrowavePhysics.RuzeEfficiency(param.SurfaceToleranceM, lambda);

        double vswr = FeedVswr(feed);
        double mismatchEfficiency = MicrowavePhysics.MismatchEfficiency(vswr);
        ApplyMatching(result, vswr, wg.WaveImpedanceTE10(freqHz));

        double sigma = param.Material.Conductivity;
        double feedLossDb = wg.AttenuationDbPerM(freqHz, sigma) * feed.AxialLengthM;
        double reflectorEfficiency = 1.0 - MicrowavePhysics.MetalAbsorptance(freqHz, sigma);
        double conductorEfficiency = MicrowavePhysics.FromDb(-feedLossDb) * reflectorEfficiency;

        result.SpecificParameters["BlockageRatio"] = blockageRatio;
        result.SpecificParameters["TaperEfficiency"] = taperEfficiency;
        result.SpecificParameters["SpilloverEfficiency"] = spilloverEfficiency;
        result.SpecificParameters["BlockageEfficiency"] = blockageEfficiency;
        result.SpecificParameters["RuzeEfficiency"] = ruzeEfficiency;

        double patternEfficiency = taperEfficiency * spilloverEfficiency * blockageEfficiency
                                 * ruzeEfficiency * param.MiscellaneousEfficiency;
        result.Efficiency = patternEfficiency * conductorEfficiency * mismatchEfficiency;

        // -- Усиление и ДН -------------------------------------------------------
        result.GainLinear = MicrowavePhysics.ApertureGain(dish.ApertureAreaM2, result.Efficiency, lambda);
        result.GainDbi = MicrowavePhysics.ToDb(result.GainLinear);
        result.DirectivityDbi = MicrowavePhysics.ToDb(
            MicrowavePhysics.ApertureGain(dish.ApertureAreaM2, patternEfficiency, lambda));

        result.BeamwidthEPlane = beamFactor * lambda / diameter;
        result.BeamwidthHPlane = result.BeamwidthEPlane;
        result.SideLobeLevel = ApertureIllumination.SidelobeWithBlockageDb(
            ApertureIllumination.SidelobeLevelDb(taperDb), blockageRatio);
        result.FrontToBackRatio = 50.0;

        CheckRequirements(param, result);

        // -- Электрическая прочность ---------------------------------------------
        // Поле в фокусе - это поле раскрыва облучателя, а не усиление всего
        // зеркала, сфокусированное в точку: прежняя формула P G / (4 pi f^2)
        // применяла дальнезонное соотношение на расстоянии, которое лежит
        // глубоко в ближней зоне (граница дальней зоны здесь десятки метров).
        double airBreakdown = param.Environment.GetBreakdownFieldStrength();
        double throatField = wg.PeakElectricField(param.PowerWatts, freqHz);
        double feedPeakDensity = param.PowerWatts / feed.ApertureAreaM2 * FeedPeakToAverage;
        double feedField = MicrowavePhysics.PeakFieldFromPowerDensity(feedPeakDensity);
        result.PowerDensityPeak = param.PowerWatts / dish.ApertureAreaM2 / taperEfficiency;

        ApplyBreakdown(result,
            new BreakdownPoint("горловина", throatField, airBreakdown),
            new BreakdownPoint("раскрыв облучателя", feedField, airBreakdown));

        // -- Тепловой режим -------------------------------------------------------
        result.OhmicLossesW = param.PowerWatts * (1.0 - conductorEfficiency);
        result.ThermalLoadWPerM2 = result.OhmicLossesW / dish.SurfaceAreaM2;
        result.MaxTemperatureRise = TemperatureRise(
            result.ThermalLoadWPerM2, param.ReflectorSheetThicknessM, param.Material.ThermalConductivity);

        // -- Масса и стоимость -----------------------------------------------------
        double reflectorWeight = dish.WeightKg(
            param.ReflectorSheetThicknessM, param.Material.Density, param.ReflectorRibMassFraction);
        double feedWeight = feed.WeightKg(param.Material, param.WallThicknessM);

        result.WeightKg = reflectorWeight + feedWeight;
        result.SpecificParameters["ReflectorWeight_kg"] = reflectorWeight;
        result.SpecificParameters["FeedWeight_kg"] = feedWeight;
        result.CostRelative = (reflectorWeight * FormingCostFactor + feedWeight) * param.Material.Cost;

        result.FarFieldDistanceM = MicrowavePhysics.FarFieldDistance(diameter, lambda);

        // -- Предупреждения и рекомендации ----------------------------------------
        double spilloverWatts = param.PowerWatts * (1.0 - spilloverEfficiency);
        result.SpecificParameters["SpilloverPower_W"] = spilloverWatts;

        result.Recommendations.Add(
            $"Мимо зеркала уходит {spilloverWatts:F0} Вт ({100.0 * (1.0 - spilloverEfficiency):F0} %): " +
            "это излучение назад и вбок, его надо учитывать при расчёте санитарной зоны.");

        if (spilloverEfficiency < 0.80)
        {
            result.Warnings.Add(
                $"Перехват всего {spilloverEfficiency * 100.0:F0} %: облучатель слишком широк " +
                "для выбранного f/D, велики потери на перелив.");
        }

        result.Recommendations.Add(
            $"Точность поверхности зеркала: СКО не хуже lambda/20 = {lambda * 1000.0 / 20.0:F2} мм " +
            $"(при заданных {param.SurfaceToleranceMm:F2} мм потери Рузе {MicrowavePhysics.ToDb(ruzeEfficiency):F2} дБ)");
        result.Recommendations.Add("Сотовая или рёберная конструкция для жёсткости");
        result.Recommendations.Add("Радиопрозрачный обтекатель облучателя");
        result.Recommendations.Add("Юстировка облучателя по трём осям в пределах нескольких миллиметров");
        result.Recommendations.Add("Антикоррозионное покрытие: анодирование или порошковая краска");

        if (diameter > 2.0)
        {
            result.Recommendations.Add(
                "Большой диаметр: учтите ветровые нагрузки, возможна сегментная конструкция");
        }

        if (blockageRatio > 0.05)
        {
            result.Recommendations.Add(
                $"Затенение {blockageRatio * 100.0:F1} %: офсетная схема устраняет его полностью");
        }
    }

    /// <summary>
    /// КСВ облучателя по крутизне его раскрыва: чем резче раскрыв, тем хуже
    /// согласование апертуры со свободным пространством.
    /// </summary>
    private static double FeedVswr(PyramidalHorn feed)
    {
        double flare = 0.5 * (feed.FlareAngleEDeg + feed.FlareAngleHDeg);
        return flare < 5 ? 1.05 : flare < 15 ? 1.10 : flare < 25 ? 1.20 : 1.35;
    }
}
