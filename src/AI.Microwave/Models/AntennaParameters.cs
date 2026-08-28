using AI.Microwave.Physics;

namespace AI.Microwave.Models;

/// <summary>
/// Техническое задание на антенну и конструктивные допущения расчёта.
/// </summary>
/// <remarks>
/// Прежняя версия хранила здесь же полтора десятка выходных величин
/// (усиление, КСВ, КИП, поле пробоя), которые дублировали
/// <c>AntennaDesignResult</c> и не заполнялись ни одним калькулятором.
/// Они убраны: результат живёт только в результате.
/// <para>
/// Вместо этого сюда вынесены константы, которые раньше были зашиты в тела
/// расчётов: толщина стенки, толщина листа зеркала, допуск на профиль,
/// спад к краю апертуры и отношения f/D.
/// </para>
/// </remarks>
public class AntennaParameters
{
    // -- Техническое задание ---------------------------------------------------

    /// <summary>Рабочая частота, МГц.</summary>
    public double FrequencyMHz { get; set; } = 2450;

    /// <summary>Подводимая мощность (CW), Вт.</summary>
    public double PowerWatts { get; set; } = 900;

    /// <summary>Требуемая ширина луча по уровню -3 дБ, град.</summary>
    public double RequiredBeamwidthDegrees { get; set; } = 5;

    /// <summary>Требуемый уровень боковых лепестков, дБ (отрицательный).</summary>
    public double RequiredSidelobeLevelDb { get; set; } = -20;

    // -- Материалы и среда -----------------------------------------------------

    /// <summary>Конструкционный металл.</summary>
    public MaterialProperties Material { get; set; } = MaterialProperties.GetStandardMaterials()[0];

    /// <summary>Диэлектрик линзы (используется только линзовой антенной).</summary>
    public DielectricProperties LensMaterial { get; set; } =
        DielectricProperties.GetStandardDielectrics()[0];

    /// <summary>Условия эксплуатации.</summary>
    public EnvironmentalConditions Environment { get; set; } = new();

    /// <summary>Питающий волновод.</summary>
    public RectangularWaveguide Waveguide { get; set; } =
        RectangularWaveguide.Find("WR-340")!;

    // -- Конструктивные допущения ----------------------------------------------

    /// <summary>Толщина стенки рупора, мм.</summary>
    public double WallThicknessMm { get; set; } = 2.0;

    /// <summary>Толщина листа отражателя, мм.</summary>
    public double ReflectorSheetThicknessMm { get; set; } = 1.0;

    /// <summary>Доля массы, добавляемая рёбрами жёсткости зеркала.</summary>
    public double ReflectorRibMassFraction { get; set; } = 0.5;

    /// <summary>СКО отклонения профиля зеркала, мм (входит в формулу Рузе).</summary>
    public double SurfaceToleranceMm { get; set; } = 0.5;

    /// <summary>Целевой спад поля на краю апертуры, дБ.</summary>
    public double EdgeTaperDb { get; set; } = ApertureIllumination.DefaultEdgeTaperDb;

    /// <summary>Относительное фокусное расстояние зеркала f/D.</summary>
    public double ReflectorFocalToDiameterRatio { get; set; } = 0.40;

    /// <summary>Относительное фокусное расстояние линзы f/D.</summary>
    public double LensFocalToDiameterRatio { get; set; } = 1.0;

    /// <summary>Толщина линзы на краю (из условия прочности), мм.</summary>
    public double LensEdgeThicknessMm { get; set; } = 10.0;

    /// <summary>
    /// Прочие потери зеркальной и линзовой систем (смещение фазового центра,
    /// кросс-поляризация, рассеяние на стойках), доля прошедшей мощности.
    /// </summary>
    public double MiscellaneousEfficiency { get; set; } = 0.90;

    // -- Производные величины --------------------------------------------------

    /// <summary>Рабочая частота, Гц.</summary>
    public double FrequencyHz => FrequencyMHz * 1e6;

    /// <summary>Длина волны в свободном пространстве, м.</summary>
    public double WavelengthM => MicrowavePhysics.Wavelength(FrequencyHz);

    /// <summary>Толщина стенки рупора, м.</summary>
    public double WallThicknessM => WallThicknessMm / 1000.0;

    /// <summary>Толщина листа отражателя, м.</summary>
    public double ReflectorSheetThicknessM => ReflectorSheetThicknessMm / 1000.0;

    /// <summary>СКО отклонения профиля зеркала, м.</summary>
    public double SurfaceToleranceM => SurfaceToleranceMm / 1000.0;

    /// <summary>Толщина линзы на краю, м.</summary>
    public double LensEdgeThicknessM => LensEdgeThicknessMm / 1000.0;

    /// <summary>
    /// Проверка физической состоятельности задания: режим волновода,
    /// положительность величин, разумность требований.
    /// </summary>
    /// <returns>Список замечаний; пустой, если всё в порядке.</returns>
    public IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();

        if (FrequencyMHz <= 0) issues.Add("Частота должна быть положительной.");
        if (PowerWatts <= 0) issues.Add("Мощность должна быть положительной.");
        if (RequiredBeamwidthDegrees <= 0)
            issues.Add("Требуемая ширина луча должна быть положительной.");
        if (RequiredSidelobeLevelDb >= 0)
            issues.Add("Требование по УБЛ задаётся отрицательным числом дБ.");
        if (Environment.Temperature <= -273.15)
            issues.Add("Температура ниже абсолютного нуля.");

        if (FrequencyMHz <= 0) return issues;

        if (!Waveguide.IsPropagating(FrequencyHz))
        {
            issues.Add(
                $"Волновод {Waveguide.Standard} заперт: критическая частота TE10 " +
                $"{Waveguide.CutoffTE10Hz / 1e6:F0} МГц выше рабочей {FrequencyMHz:F0} МГц. " +
                $"Подходит {RectangularWaveguide.SelectForFrequency(FrequencyHz).Standard}.");
        }
        else if (!Waveguide.IsSingleMode(FrequencyHz))
        {
            issues.Add(
                $"Волновод {Waveguide.Standard} многомодовый на {FrequencyMHz:F0} МГц " +
                $"(высшая мода с {Waveguide.CutoffNextModeHz / 1e6:F0} МГц). " +
                "Возможен перекос ДН и рост КСВ.");
        }
        else if (FrequencyHz < Waveguide.BandLowHz || FrequencyHz > Waveguide.BandHighHz)
        {
            issues.Add(
                $"Частота вне рекомендованной полосы {Waveguide.Standard} " +
                $"({Waveguide.BandLowHz / 1e6:F0}...{Waveguide.BandHighHz / 1e6:F0} МГц): " +
                "растут потери или падает запас до высшей моды.");
        }

        return issues;
    }
}
