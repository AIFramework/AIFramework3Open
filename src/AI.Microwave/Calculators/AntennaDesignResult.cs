namespace AI.Microwave.Calculators;

/// <summary>
/// Результат расчёта антенны: единый формат для всех типов, чтобы варианты
/// можно было сравнивать построчно.
/// </summary>
public class AntennaDesignResult
{
    // -- Геометрия -------------------------------------------------------------

    /// <summary>Ширина раскрыва (для осесимметричных - диаметр), м.</summary>
    public double ApertureWidthM { get; set; }

    /// <summary>Высота раскрыва, м.</summary>
    public double ApertureHeightM { get; set; }

    /// <summary>Осевая длина основного элемента, м.</summary>
    public double AxialLengthM { get; set; }

    /// <summary>Полная длина конструкции по оси, м.</summary>
    public double TotalLengthM { get; set; }

    /// <summary>
    /// Величины, специфичные для конкретного типа: диаметр зеркала, фокус,
    /// толщина линзы, углы раскрыва и так далее.
    /// </summary>
    public Dictionary<string, double> SpecificParameters { get; set; } = [];

    // -- Электрические характеристики ------------------------------------------

    /// <summary>Коэффициент усиления, дБи (с учётом всех потерь).</summary>
    public double GainDbi { get; set; }

    /// <summary>Коэффициент усиления в разах.</summary>
    public double GainLinear { get; set; }

    /// <summary>Коэффициент направленного действия, дБи (без диссипативных потерь).</summary>
    public double DirectivityDbi { get; set; }

    /// <summary>Полный КИП апертуры.</summary>
    public double Efficiency { get; set; }

    /// <summary>КСВ на входе.</summary>
    public double VSWR { get; set; }

    /// <summary>Возвратные потери, дБ (отрицательные).</summary>
    public double ReturnLossDb { get; set; }

    /// <summary>Входное сопротивление, Ом.</summary>
    public double Impedance { get; set; }

    // -- Диаграмма направленности ----------------------------------------------

    /// <summary>Ширина луча в E-плоскости по уровню -3 дБ, град.</summary>
    public double BeamwidthEPlane { get; set; }

    /// <summary>Ширина луча в H-плоскости по уровню -3 дБ, град.</summary>
    public double BeamwidthHPlane { get; set; }

    /// <summary>Уровень первого бокового лепестка в худшей плоскости, дБ.</summary>
    public double SideLobeLevel { get; set; }

    /// <summary>Отношение излучения вперёд/назад, дБ.</summary>
    public double FrontToBackRatio { get; set; }

    // -- Безопасность и надёжность ---------------------------------------------

    /// <summary>Максимальная напряжённость поля в наиболее нагруженной точке, В/м.</summary>
    public double MaxElectricField { get; set; }

    /// <summary>Порог пробоя в этой же точке, В/м.</summary>
    public double BreakdownThreshold { get; set; }

    /// <summary>Запас по пробою, раз.</summary>
    public double SafetyMargin { get; set; }

    /// <summary>Пиковая плотность потока мощности в раскрыве, Вт/м^2.</summary>
    public double PowerDensityPeak { get; set; }

    /// <summary>Где находится наиболее нагруженная точка тракта.</summary>
    public string HotSpot { get; set; } = string.Empty;

    /// <summary>Требуемый запас по пробою, раз.</summary>
    public double RequiredSafetyMargin { get; set; } = 2.0;

    /// <summary>Выполнен ли запас по электрической прочности.</summary>
    public bool IsSafe => SafetyMargin >= RequiredSafetyMargin;

    // -- Тепловой режим ---------------------------------------------------------

    /// <summary>Диссипативные потери, Вт.</summary>
    public double OhmicLossesW { get; set; }

    /// <summary>Удельная тепловая нагрузка, Вт/м^2.</summary>
    public double ThermalLoadWPerM2 { get; set; }

    /// <summary>Перегрев над окружающей средой, К.</summary>
    public double MaxTemperatureRise { get; set; }

    // -- Физические параметры ---------------------------------------------------

    /// <summary>Масса конструкции, кг.</summary>
    public double WeightKg { get; set; }

    /// <summary>Относительная стоимость материала.</summary>
    public double CostRelative { get; set; }

    /// <summary>Граница дальней зоны, м.</summary>
    public double FarFieldDistanceM { get; set; }

    // -- Замечания --------------------------------------------------------------

    public List<string> Warnings { get; set; } = [];

    public List<string> Recommendations { get; set; } = [];

    // -- Соответствие требованиям ------------------------------------------------

    public bool MeetsBeamwidthRequirement { get; set; }

    public bool MeetsSidelobeRequirement { get; set; }

    public bool MeetsAllRequirements => MeetsBeamwidthRequirement && MeetsSidelobeRequirement && IsSafe;
}
