using AI.Microwave.Geometry;
using AI.Microwave.Models;
using AI.Microwave.Physics;

namespace AI.Microwave.Calculators;

/// <summary>
/// Рупор с плосковыпуклой диэлектрической линзой: линза выпрямляет
/// сферический фронт облучателя, поэтому раскрыв работает синфазно при
/// коротком рупоре.
/// </summary>
public class HornWithLensCalculator : AntennaCalculatorBase
{
    /// <summary>Отношение пиковой плотности потока к средней в раскрыве облучателя.</summary>
    private const double FeedPeakToAverage = 1.0 / 0.81;

    /// <inheritdoc/>
    public override string AntennaType => "Рупор с диэлектрической линзой";

    /// <inheritdoc/>
    public override string GetDescription()
    {
        return "Рупорная антенна с диэлектрической линзой сочетает компактность рупора " +
               "с фокусирующими свойствами линзы. Линза корректирует фазовый фронт, " +
               "обеспечивая узкую ДН без наращивания длины рупора.";
    }

    /// <inheritdoc/>
    public override string GetAdvantages()
    {
        return "Короче рупора той же направленности\n" +
               "Нет затенения апертуры\n" +
               "Низкие боковые лепестки\n" +
               "Апертура освещается синфазно";
    }

    /// <inheritdoc/>
    public override string GetDisadvantages()
    {
        return "Масса диэлектрика растёт как куб апертуры\n" +
               "Отражения от обеих поверхностей поднимают КСВ\n" +
               "Диэлектрические потери и зависимость eps_r от температуры\n" +
               "Узкая полоса из-за фиксированной толщины\n" +
               "Дорогой материал";
    }

    /// <inheritdoc/>
    protected override void CalculateCore(AntennaParameters param, AntennaDesignResult result)
    {
        double lambda = param.WavelengthM;
        double freqHz = param.FrequencyHz;
        var wg = param.Waveguide;
        var material = param.LensMaterial;
        double taperDb = param.EdgeTaperDb;

        // -- Синтез от требования -----------------------------------------------
        // Луч формирует линза, поэтому её диаметр и определяется требуемой ШДН.
        // Прежний расчёт шёл в обратную сторону: сначала назначал рупор в 2.5
        // раза шире требования, потом брал линзу в 1.3 раза шире рупора - и
        // получал луч, который заведомо не мог уложиться в ТЗ.
        double beamFactor = ApertureIllumination.BeamwidthFactor(taperDb);
        double lensDiameter = beamFactor * lambda / param.RequiredBeamwidthDegrees;
        double focalLength = param.LensFocalToDiameterRatio * lensDiameter;

        var lens = new DielectricLens(material, lensDiameter, focalLength, lambda, param.LensEdgeThicknessM);

        double feedHpbw = ApertureIllumination.FeedBeamwidthForTaper(lens.RimHalfAngleDeg, taperDb);
        var feed = PyramidalHorn.ForBeamwidth(feedHpbw, wg, lambda);

        result.ApertureWidthM = lensDiameter;
        result.ApertureHeightM = lensDiameter;
        result.AxialLengthM = feed.AxialLengthM;
        result.TotalLengthM = feed.AxialLengthM + focalLength + lens.CenterThicknessM;

        result.SpecificParameters["LensDiameter_m"] = lensDiameter;
        result.SpecificParameters["LensFocalLength_m"] = focalLength;
        result.SpecificParameters["Lens_F_over_D"] = param.LensFocalToDiameterRatio;
        result.SpecificParameters["LensCenterThickness_m"] = lens.CenterThicknessM;
        result.SpecificParameters["LensEdgeThickness_m"] = lens.EdgeThicknessM;
        result.SpecificParameters["LensRimHalfAngle_deg"] = lens.RimHalfAngleDeg;
        result.SpecificParameters["LensVolume_m3"] = lens.VolumeM3;
        result.SpecificParameters["LensMeanPath_m"] = lens.MeanPathM;
        result.SpecificParameters["DielectricConstant"] = material.RelativePermittivity;
        result.SpecificParameters["LossTangent"] = material.LossTangent;
        result.SpecificParameters["HornLength_m"] = feed.AxialLengthM;
        result.SpecificParameters["FeedBeamwidth_deg"] = feedHpbw;
        result.SpecificParameters["EdgeTaper_dB"] = taperDb;

        // -- Потери в линзе -------------------------------------------------------
        // Пропускание пластины (1-R)/(1+R) учитывает обе границы и
        // многократные переотражения. Прежняя запись 1 - 2R уходила в минус
        // при eps_r выше 34, после чего логарифм давал NaN.
        double slabTransmittance = material.SlabTransmittance;
        double dielectricLossDb = material.AttenuationNpPerM(lambda) * lens.MeanPathM
                                * MicrowavePhysics.NeperToDb;
        double dielectricTransmittance = MicrowavePhysics.FromDb(-dielectricLossDb);

        result.SpecificParameters["ReflectionLoss_dB"] = -MicrowavePhysics.ToDb(slabTransmittance);
        result.SpecificParameters["DielectricLoss_dB"] = dielectricLossDb;

        // -- Согласование ----------------------------------------------------------
        // Отражения от двух граней линзы могут сложиться в фазе: берём худший
        // случай. Потери на это отражение уже сидят в slabTransmittance,
        // поэтому отдельно на MismatchEfficiency КИП не умножается.
        double vswr = Math.Max(1.05, material.WorstCaseVswr);
        ApplyMatching(result, vswr, MicrowavePhysics.FreeSpaceImpedance);

        // -- КИП и усиление ---------------------------------------------------------
        double sigma = param.Material.Conductivity;
        double feedLossDb = wg.AttenuationDbPerM(freqHz, sigma) * feed.AxialLengthM;
        double conductorEfficiency = MicrowavePhysics.FromDb(-feedLossDb);

        double taperEfficiency = ApertureIllumination.TaperEfficiency(taperDb);
        double spilloverEfficiency = ApertureIllumination.SpilloverEfficiency(taperDb);

        result.SpecificParameters["TaperEfficiency"] = taperEfficiency;
        result.SpecificParameters["SpilloverEfficiency"] = spilloverEfficiency;
        result.SpecificParameters["SlabTransmittance"] = slabTransmittance;

        double patternEfficiency = taperEfficiency * spilloverEfficiency * param.MiscellaneousEfficiency;
        result.Efficiency = patternEfficiency * slabTransmittance
                          * dielectricTransmittance * conductorEfficiency;

        double apertureArea = lens.ApertureAreaM2;
        result.GainLinear = MicrowavePhysics.ApertureGain(apertureArea, result.Efficiency, lambda);
        result.GainDbi = MicrowavePhysics.ToDb(result.GainLinear);
        result.DirectivityDbi = MicrowavePhysics.ToDb(
            MicrowavePhysics.ApertureGain(apertureArea, patternEfficiency, lambda));

        result.BeamwidthEPlane = beamFactor * lambda / lensDiameter;
        result.BeamwidthHPlane = result.BeamwidthEPlane;
        result.SideLobeLevel = ApertureIllumination.SidelobeLevelDb(taperDb);
        result.FrontToBackRatio = 45.0;

        CheckRequirements(param, result);

        // -- Электрическая прочность -------------------------------------------------
        // В диэлектрике поле не усиливается, а слабеет: при том же потоке
        // мощности E = sqrt(2 S eta0 / n). Прежний расчёт умножал поле на
        // sqrt(eps_r), то есть ошибался в самом знаке эффекта.
        // Опасность создают воздушные включения: на границе непрерывна
        // нормальная составляющая D, поэтому в поре поле в eps_r раз выше.
        double airBreakdown = param.Environment.GetBreakdownFieldStrength();
        double throatField = wg.PeakElectricField(param.PowerWatts, freqHz);

        double feedPeakDensity = param.PowerWatts / feed.ApertureAreaM2 * FeedPeakToAverage;
        double feedField = MicrowavePhysics.PeakFieldFromPowerDensity(feedPeakDensity);

        result.PowerDensityPeak = param.PowerWatts / apertureArea / taperEfficiency;
        double lensBulkField = MicrowavePhysics.PeakFieldFromPowerDensity(
            result.PowerDensityPeak, MicrowavePhysics.FreeSpaceImpedance / material.RefractiveIndex);
        double voidField = lensBulkField * material.RelativePermittivity;

        ApplyBreakdown(result,
            new BreakdownPoint("горловина", throatField, airBreakdown),
            new BreakdownPoint("раскрыв облучателя", feedField, airBreakdown),
            new BreakdownPoint("тело линзы", lensBulkField, material.DielectricStrength),
            new BreakdownPoint("пора в линзе", voidField, airBreakdown));

        // -- Тепловой режим -----------------------------------------------------------
        double dissipatedInLens = param.PowerWatts * (1.0 - dielectricTransmittance);
        result.OhmicLossesW = param.PowerWatts * (1.0 - conductorEfficiency) + dissipatedInLens;
        result.ThermalLoadWPerM2 = dissipatedInLens / (2.0 * apertureArea);
        result.MaxTemperatureRise = TemperatureRise(
            result.ThermalLoadWPerM2, lens.CenterThicknessM / 2.0, material.ThermalConductivity);

        double lensTemperature = param.Environment.Temperature + result.MaxTemperatureRise;
        result.SpecificParameters["LensTemperature_C"] = lensTemperature;

        if (lensTemperature > material.MaxServiceTemperature)
        {
            result.Warnings.Add(
                $"Температура линзы {lensTemperature:F0} C выше предельной для материала " +
                $"{material.Name} ({material.MaxServiceTemperature:F0} C).");
        }

        // -- Масса и стоимость ----------------------------------------------------------
        double feedWeight = feed.WeightKg(param.Material, param.WallThicknessM);
        result.WeightKg = feedWeight + lens.WeightKg;
        result.CostRelative = feedWeight * param.Material.Cost + lens.WeightKg * material.Cost;

        result.SpecificParameters["HornWeight_kg"] = feedWeight;
        result.SpecificParameters["LensWeight_kg"] = lens.WeightKg;
        result.SpecificParameters["ZoneStep_m"] = lens.ZoneStepM;
        result.SpecificParameters["ZoneCount"] = lens.ZoneCount;
        result.SpecificParameters["ZonedLensWeight_kg"] = lens.ZonedWeightKg;
        result.SpecificParameters["ZonedMaxThickness_m"] = lens.ZonedMaxThicknessM;

        result.FarFieldDistanceM = MicrowavePhysics.FarFieldDistance(lensDiameter, lambda);

        // -- Предупреждения и рекомендации -------------------------------------------
        if (lens.WeightKg > 50)
        {
            result.Warnings.Add(
                $"Линза толщиной {lens.CenterThicknessM * 100:F0} см при диаметре " +
                $"{lensDiameter * 100:F0} см конструктивно нереализуема: масса диэлектрика " +
                $"{lens.WeightKg:F0} кг. Толщина сплошной линзы растёт вместе с апертурой, " +
                "поэтому на длинных волнах линзовая схема проигрывает зеркальной.");
        }

        if (lens.ZoneCount > 1)
        {
            result.Recommendations.Add(
                $"Зонирование по Френелю ({lens.ZoneCount} зоны, ступень {lens.ZoneStepM * 1000:F0} мм) " +
                $"снижает массу с {lens.WeightKg:F0} до {lens.ZonedWeightKg:F0} кг " +
                $"ценой сужения полосы и роста боковых лепестков.");
        }

        result.Recommendations.Add(
            $"Просветляющий слой толщиной {material.AntiReflectionLayerThicknessM(lambda) * 1000:F1} мм " +
            $"с показателем преломления {Math.Sqrt(material.RefractiveIndex):F2} снимет отражение " +
            $"({-MicrowavePhysics.ToDb(slabTransmittance):F2} дБ) и снизит КСВ с {vswr:F2} почти до единицы");
        result.Recommendations.Add($"Точность изготовления профиля линзы: lambda/10 = {lambda * 100:F1} мм");
        result.Recommendations.Add("Крепление линзы через радиопрозрачный каркас (сотовый пластик)");
        result.Recommendations.Add("Гидрофобное покрытие: плёнка воды на линзе резко поднимает потери");
        result.Recommendations.Add(
            $"Вакуумирование или контроль пористости заготовки: пора в диэлектрике " +
            $"поднимает локальное поле в {material.RelativePermittivity:F1} раза");

        if (param.FrequencyMHz > 10000)
        {
            result.Warnings.Add(
                "Выше 10 ГГц тангенс потерь большинства полимеров растёт: рассмотрите кварц.");
        }
    }
}
