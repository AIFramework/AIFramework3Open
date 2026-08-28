using AI.Microwave.Geometry;
using AI.Microwave.Models;
using AI.Microwave.Physics;

namespace AI.Microwave.Calculators;

/// <summary>
/// Пирамидальный рупор оптимальной длины, питаемый прямоугольным волноводом
/// на моде TE10.
/// </summary>
public class HornAntennaCalculator : AntennaCalculatorBase
{
    /// <summary>
    /// Отношение пиковой плотности потока к средней по раскрыву:
    /// равномерное распределение по E, косинусное по H (КИП спадания 0.81).
    /// </summary>
    private const double AperturePeakToAverage = 1.0 / 0.81;

    /// <inheritdoc/>
    public override string AntennaType => "Пирамидальный рупор";

    /// <inheritdoc/>
    public override string GetDescription()
    {
        return "Пирамидальная рупорная антенна представляет собой плавно расширяющийся " +
               "волновод. Обеспечивает умеренное усиление (15-25 дБи), широкую полосу частот " +
               "и хорошую развязку. Идеальна для мощных применений.";
    }

    /// <inheritdoc/>
    public override string GetAdvantages()
    {
        return "Простая конструкция\n" +
               "Высокая допустимая мощность\n" +
               "Широкая полоса\n" +
               "Низкий КСВ\n" +
               "Надёжность";
    }

    /// <inheritdoc/>
    public override string GetDisadvantages()
    {
        return "Умеренное усиление\n" +
               "Большие габариты: узкий луч требует длины в десятки длин волн\n" +
               "Высокий уровень боковых лепестков в E-плоскости";
    }

    /// <inheritdoc/>
    protected override void CalculateCore(AntennaParameters param, AntennaDesignResult result)
    {
        double lambda = param.WavelengthM;
        double freqHz = param.FrequencyHz;
        var wg = param.Waveguide;

        // -- Геометрия ---------------------------------------------------------
        // Апертура синтезируется теми же коэффициентами 56 и 67, по которым
        // потом проверяется ШДН, поэтому раскрыв получается неквадратным
        // и требование выполняется точно.
        var horn = PyramidalHorn.ForBeamwidth(param.RequiredBeamwidthDegrees, wg, lambda);

        result.ApertureHeightM = horn.ApertureHeightM;
        result.ApertureWidthM = horn.ApertureWidthM;
        result.AxialLengthM = horn.AxialLengthM;
        result.TotalLengthM = horn.AxialLengthM;

        result.SpecificParameters["ApertureE_m"] = horn.ApertureHeightM;
        result.SpecificParameters["ApertureH_m"] = horn.ApertureWidthM;
        result.SpecificParameters["SlantLengthE_m"] = horn.SlantEPlaneM;
        result.SpecificParameters["SlantLengthH_m"] = horn.SlantHPlaneM;
        result.SpecificParameters["FlareAngleE_deg"] = horn.FlareAngleEDeg;
        result.SpecificParameters["FlareAngleH_deg"] = horn.FlareAngleHDeg;
        result.SpecificParameters["PhaseErrorE"] = horn.PhaseErrorE;
        result.SpecificParameters["PhaseErrorH"] = horn.PhaseErrorH;
        result.SpecificParameters["WallArea_m2"] = horn.WallAreaM2;

        if (horn.ApertureClampedToThroat)
        {
            result.Warnings.Add(
                "Требуемая ШДН настолько широка, что расчётная апертура меньше сечения " +
                "волновода: рупор вырождается в открытый конец волновода.");
        }

        // -- Согласование -------------------------------------------------------
        double flareAvg = 0.5 * (horn.FlareAngleEDeg + horn.FlareAngleHDeg);
        double vswr = flareAvg < 5 ? 1.02 : flareAvg < 15 ? 1.05 : flareAvg < 25 ? 1.15 : 1.30;
        ApplyMatching(result, vswr, wg.WaveImpedanceTE10(freqHz));

        result.SpecificParameters["WaveImpedanceTE10_ohm"] = wg.WaveImpedanceTE10(freqHz);
        result.SpecificParameters["EquivalentLineImpedance_ohm"] = wg.EquivalentLineImpedance(freqHz);
        result.SpecificParameters["GuideWavelength_mm"] = wg.GuideWavelength(freqHz) * 1000.0;
        result.SpecificParameters["CutoffTE10_MHz"] = wg.CutoffTE10Hz / 1e6;

        // -- Потери в стенках ---------------------------------------------------
        // Погонное затухание TE10 на длине рупора: оценка сверху, поскольку
        // по мере расширения сечения потери падают. Раньше здесь стояла
        // эвристика 0.1 + (1 - sigma/sigma_Cu) * 0.3, а честно вычисленные
        // скин-слой и поверхностное сопротивление никуда не использовались.
        double sigma = param.Material.Conductivity;
        double attenuationDb = wg.AttenuationDbPerM(freqHz, sigma) * horn.AxialLengthM;
        double conductorEfficiency = MicrowavePhysics.FromDb(-attenuationDb);
        double mismatchEfficiency = MicrowavePhysics.MismatchEfficiency(vswr);

        result.SpecificParameters["SkinDepth_um"] = MicrowavePhysics.SkinDepth(freqHz, sigma) * 1e6;
        result.SpecificParameters["SurfaceResistance_mOhm"] =
            MicrowavePhysics.SurfaceResistance(freqHz, sigma) * 1000.0;
        result.SpecificParameters["ConductorLoss_dB"] = attenuationDb;

        // -- Усиление -----------------------------------------------------------
        // КИП учитывает потери до расчёта усиления, иначе Efficiency и GainDbi
        // описывают разные антенны.
        double apertureArea = horn.ApertureAreaM2;
        result.Efficiency = PyramidalHorn.OptimalApertureEfficiency
                          * conductorEfficiency * mismatchEfficiency;
        result.GainLinear = MicrowavePhysics.ApertureGain(apertureArea, result.Efficiency, lambda);
        result.GainDbi = MicrowavePhysics.ToDb(result.GainLinear);
        result.DirectivityDbi = MicrowavePhysics.ToDb(MicrowavePhysics.ApertureGain(
            apertureArea, PyramidalHorn.OptimalApertureEfficiency, lambda));

        // -- Диаграмма направленности -------------------------------------------
        result.BeamwidthEPlane = horn.BeamwidthEPlaneDeg(lambda);
        result.BeamwidthHPlane = horn.BeamwidthHPlaneDeg(lambda);

        // УБЛ определяется распределением поля в раскрыве и различается по
        // плоскостям: равномерное по E даёт -13.3 дБ, косинусное по H -23 дБ.
        // Требование ТЗ проверяется по худшей плоскости, а не по лучшей.
        result.SpecificParameters["SidelobeE_dB"] = PyramidalHorn.SidelobeEPlaneDb;
        result.SpecificParameters["SidelobeH_dB"] = PyramidalHorn.SidelobeHPlaneDb;
        result.SideLobeLevel = Math.Max(PyramidalHorn.SidelobeEPlaneDb, PyramidalHorn.SidelobeHPlaneDb);
        result.FrontToBackRatio = 35.0;

        CheckRequirements(param, result);

        if (!result.MeetsSidelobeRequirement)
        {
            result.Recommendations.Add(
                "УБЛ ограничен равномерным распределением поля в E-плоскости. Ниже -20 дБ " +
                "выводят гофрированный рупор, диэлектрическая вставка в раскрыве или " +
                "поглощающая кромка.");
        }

        // -- Электрическая прочность --------------------------------------------
        double airBreakdown = param.Environment.GetBreakdownFieldStrength();
        double throatField = wg.PeakElectricField(param.PowerWatts, freqHz);
        result.PowerDensityPeak = param.PowerWatts / apertureArea * AperturePeakToAverage;
        double apertureField = MicrowavePhysics.PeakFieldFromPowerDensity(result.PowerDensityPeak);

        ApplyBreakdown(result,
            new BreakdownPoint("горловина", throatField, airBreakdown),
            new BreakdownPoint("раскрыв", apertureField, airBreakdown));

        // -- Тепловой режим ------------------------------------------------------
        result.OhmicLossesW = param.PowerWatts * (1.0 - conductorEfficiency);
        result.ThermalLoadWPerM2 = result.OhmicLossesW / horn.WallAreaM2;
        result.MaxTemperatureRise = TemperatureRise(
            result.ThermalLoadWPerM2, param.WallThicknessM, param.Material.ThermalConductivity);

        if (result.MaxTemperatureRise > 50)
        {
            result.Warnings.Add(
                $"Перегрев стенок {result.MaxTemperatureRise:F0} К при потерях " +
                $"{result.OhmicLossesW:F1} Вт: нужен обдув или материал с большей проводимостью.");
        }

        // -- Масса, стоимость, дальняя зона --------------------------------------
        result.WeightKg = horn.WeightKg(param.Material, param.WallThicknessM);
        result.CostRelative = result.WeightKg * param.Material.Cost;
        result.FarFieldDistanceM = MicrowavePhysics.FarFieldDistance(
            Math.Max(horn.ApertureWidthM, horn.ApertureHeightM), lambda);

        // -- Рекомендации --------------------------------------------------------
        result.Recommendations.Add("Фланцевое соединение с прокладкой для герметичности");
        result.Recommendations.Add(
            "Диэлектрическое окно (кварц, фторопласт) в раскрыве, если тракт герметизирован");
        result.Recommendations.Add("Заземление конструкции");

        if (horn.AxialLengthM > 20 * lambda)
        {
            result.Recommendations.Add(
                $"Длина {horn.AxialLengthM:F2} м ({horn.AxialLengthM / lambda:F0} длин волн) - " +
                "плата за узкий луч у чистого рупора. Зеркало или линза той же " +
                "направленности будут кратно компактнее.");
        }

        if (horn.PhaseErrorE > 0.26 || horn.PhaseErrorH > 0.39)
        {
            result.Recommendations.Add(
                "Фазовая ошибка в раскрыве выше оптимальной: удлинение рупора поднимет КИП.");
        }
    }
}
