using AI.Microwave.Physics;

namespace AI.Microwave.Heating;

/// <summary>
/// Материал, нагреваемый в СВЧ-поле: комплексная проницаемость плюс
/// теплофизика.
/// </summary>
/// <remarks>
/// Ключевая величина - фактор потерь eps'' : именно он превращает поле в
/// тепло. Его зависимость от температуры определяет, устойчив процесс или
/// пойдёт вразнос: у воды eps'' с нагревом падает (процесс сам себя
/// стабилизирует), у многих полимеров и керамик растёт - и тогда более
/// горячий участок поглощает сильнее, разогревается ещё быстрее и
/// прожигает материал.
/// <para>
/// Значения в таблице - типовые литературные для 2.45 ГГц. Реальные сильно
/// зависят от влажности, плотности и состава, поэтому для проектирования
/// установки их измеряют.
/// </para>
/// </remarks>
public class DielectricMaterial
{
    public required string Name { get; set; }

    /// <summary>Действительная часть относительной проницаемости eps'.</summary>
    public double RelativePermittivity { get; set; }

    /// <summary>Фактор потерь eps'' при 20 градусах Цельсия.</summary>
    public double LossFactor { get; set; }

    /// <summary>Производная фактора потерь по температуре, 1/К.</summary>
    public double LossFactorPerKelvin { get; set; }

    /// <summary>Плотность, кг/м^3.</summary>
    public double DensityKgPerM3 { get; set; }

    /// <summary>Удельная теплоёмкость, Дж/(кг К).</summary>
    public double SpecificHeatJPerKgK { get; set; }

    /// <summary>Теплопроводность, Вт/(м К).</summary>
    public double ThermalConductivity { get; set; }

    /// <summary>Предельная рабочая температура, градусы Цельсия.</summary>
    public double MaxTemperatureC { get; set; } = 200;

    /// <summary>Тангенс угла потерь при 20 градусах.</summary>
    public double LossTangent => RelativePermittivity > 0 ? LossFactor / RelativePermittivity : 0;

    /// <summary>Фактор потерь при заданной температуре, не ниже нуля.</summary>
    public double LossFactorAt(double temperatureC)
        => Math.Max(LossFactor + LossFactorPerKelvin * (temperatureC - 20.0), 0.0);

    /// <summary>Растёт ли поглощение с нагревом - признак теплового разгона.</summary>
    public bool IsRunawayProne => LossFactorPerKelvin > 0;

    /// <summary>
    /// Глубина проникновения, м: расстояние, на котором поглощаемая мощность
    /// падает в e раз.
    /// </summary>
    /// <remarks>
    /// D = lambda0 / (2 pi sqrt(2 eps') [sqrt(1 + tg^2) - 1]^(1/2)).
    /// Для воды на 2.45 ГГц даёт около 14 мм - отсюда и вся неравномерность
    /// микроволнового нагрева: толстый кусок прогревается только снаружи.
    /// </remarks>
    public double PenetrationDepthM(double lambdaM, double temperatureC = 20)
    {
        double epsSecond = LossFactorAt(temperatureC);
        if (RelativePermittivity <= 0 || epsSecond <= 0) return double.PositiveInfinity;

        double tan = epsSecond / RelativePermittivity;
        double bracket = Math.Sqrt(Math.Sqrt(1.0 + tan * tan) - 1.0);
        return lambdaM / (2.0 * Math.PI * Math.Sqrt(2.0 * RelativePermittivity) * bracket);
    }

    /// <summary>Длина волны внутри материала, м.</summary>
    public double WavelengthInMaterialM(double lambdaM)
        => lambdaM / Math.Sqrt(RelativePermittivity);

    /// <summary>Волновое сопротивление среды, Ом.</summary>
    public double IntrinsicImpedance
        => MicrowavePhysics.FreeSpaceImpedance / Math.Sqrt(RelativePermittivity);

    /// <summary>
    /// Доля мощности, отражённая от плоской границы с воздухом при
    /// нормальном падении.
    /// </summary>
    public double SurfaceReflectance
    {
        get
        {
            double n = Math.Sqrt(RelativePermittivity);
            double g = (n - 1.0) / (n + 1.0);
            return g * g;
        }
    }

    /// <summary>Типовые материалы СВЧ-нагрева на 2.45 ГГц.</summary>
    public static List<DielectricMaterial> GetStandardLoads() =>
    [
        new() { Name = "Вода (20 C)", RelativePermittivity = 78, LossFactor = 12.0,
                LossFactorPerKelvin = -0.22, DensityKgPerM3 = 1000, SpecificHeatJPerKgK = 4186,
                ThermalConductivity = 0.60, MaxTemperatureC = 100 },
        new() { Name = "Лёд (-10 C)", RelativePermittivity = 3.2, LossFactor = 0.003,
                LossFactorPerKelvin = 0.0, DensityKgPerM3 = 917, SpecificHeatJPerKgK = 2100,
                ThermalConductivity = 2.2, MaxTemperatureC = 0 },
        new() { Name = "Тесто, хлеб", RelativePermittivity = 20, LossFactor = 6.0,
                LossFactorPerKelvin = 0.02, DensityKgPerM3 = 600, SpecificHeatJPerKgK = 2800,
                ThermalConductivity = 0.20, MaxTemperatureC = 180 },
        new() { Name = "Мясо постное", RelativePermittivity = 50, LossFactor = 16.0,
                LossFactorPerKelvin = -0.10, DensityKgPerM3 = 1050, SpecificHeatJPerKgK = 3400,
                ThermalConductivity = 0.45, MaxTemperatureC = 120 },
        new() { Name = "Древесина, 20 % влаги", RelativePermittivity = 3.0, LossFactor = 0.50,
                LossFactorPerKelvin = 0.004, DensityKgPerM3 = 600, SpecificHeatJPerKgK = 2000,
                ThermalConductivity = 0.15, MaxTemperatureC = 200 },
        new() { Name = "Резиновая смесь", RelativePermittivity = 3.5, LossFactor = 0.30,
                LossFactorPerKelvin = 0.006, DensityKgPerM3 = 1200, SpecificHeatJPerKgK = 1800,
                ThermalConductivity = 0.25, MaxTemperatureC = 200 },
        new() { Name = "Растительное масло", RelativePermittivity = 2.6, LossFactor = 0.20,
                LossFactorPerKelvin = 0.002, DensityKgPerM3 = 920, SpecificHeatJPerKgK = 1900,
                ThermalConductivity = 0.17, MaxTemperatureC = 200 },
        new() { Name = "Керамика (глинозём)", RelativePermittivity = 9.0, LossFactor = 0.002,
                LossFactorPerKelvin = 0.0008, DensityKgPerM3 = 3800, SpecificHeatJPerKgK = 880,
                ThermalConductivity = 25, MaxTemperatureC = 1500 },
    ];
}
