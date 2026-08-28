namespace AI.Microwave.Heating;

/// <summary>
/// Диэлектрический нагрев: превращение поля в объёмное тепловыделение,
/// скорость нагрева, равномерность по толщине и устойчивость процесса.
/// </summary>
public static class DielectricHeating
{
    /// <summary>Электрическая постоянная, Ф/м.</summary>
    public const double VacuumPermittivity = 8.8541878128e-12;

    /// <summary>
    /// Объёмная плотность тепловыделения, Вт/м^3:
    /// P = 2 pi f eps0 eps'' E^2, где E - действующее значение поля.
    /// </summary>
    public static double VolumetricPowerWPerM3(double frequencyHz, double lossFactor,
        double fieldRmsVPerM)
        => 2.0 * Math.PI * frequencyHz * VacuumPermittivity * lossFactor
           * fieldRmsVPerM * fieldRmsVPerM;

    /// <summary>
    /// Действующее поле, необходимое для заданного объёмного тепловыделения, В/м.
    /// </summary>
    public static double FieldForVolumetricPower(double frequencyHz, double lossFactor,
        double powerWPerM3)
    {
        double k = 2.0 * Math.PI * frequencyHz * VacuumPermittivity * lossFactor;
        return k <= 0 ? double.PositiveInfinity : Math.Sqrt(powerWPerM3 / k);
    }

    /// <summary>Скорость нагрева, К/с.</summary>
    public static double HeatingRateKPerS(double volumetricPowerWPerM3, DielectricMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        double denominator = material.DensityKgPerM3 * material.SpecificHeatJPerKgK;
        return denominator > 0 ? volumetricPowerWPerM3 / denominator : 0.0;
    }

    /// <summary>Энергия на нагрев массы на заданную разницу температур, Дж.</summary>
    /// <param name="latentHeatJPerKg">Скрытая теплота фазового перехода, если он есть.</param>
    public static double EnergyRequiredJ(double massKg, DielectricMaterial material,
        double deltaTemperatureK, double latentHeatJPerKg = 0)
    {
        ArgumentNullException.ThrowIfNull(material);
        return massKg * (material.SpecificHeatJPerKgK * deltaTemperatureK + latentHeatJPerKg);
    }

    /// <summary>
    /// Время нагрева массы при подведённой мощности, с.
    /// </summary>
    /// <param name="couplingEfficiency">Доля мощности источника, попавшая в материал.</param>
    public static double HeatingTimeS(double massKg, DielectricMaterial material,
        double deltaTemperatureK, double appliedPowerW, double couplingEfficiency = 1.0,
        double latentHeatJPerKg = 0)
    {
        double useful = appliedPowerW * Math.Clamp(couplingEfficiency, 0, 1);
        if (useful <= 0) return double.PositiveInfinity;
        return EnergyRequiredJ(massKg, material, deltaTemperatureK, latentHeatJPerKg) / useful;
    }

    /// <summary>
    /// Производительность непрерывной линии, кг/ч, при заданной мощности.
    /// </summary>
    public static double ThroughputKgPerHour(double powerW, DielectricMaterial material,
        double deltaTemperatureK, double couplingEfficiency = 0.7, double latentHeatJPerKg = 0)
    {
        ArgumentNullException.ThrowIfNull(material);
        double perKg = material.SpecificHeatJPerKgK * deltaTemperatureK + latentHeatJPerKg;
        if (perKg <= 0) return double.PositiveInfinity;
        return powerW * Math.Clamp(couplingEfficiency, 0, 1) * 3600.0 / perKg;
    }

    /// <summary>
    /// Отношение тепловыделения на поверхности к тепловыделению в центре
    /// слоя при одностороннем облучении.
    /// </summary>
    /// <remarks>
    /// Значение 1 - идеально равномерно, 10 означает, что поверхность
    /// получает на порядок больше центра. Именно эта величина, а не КПД,
    /// ограничивает толщину обрабатываемого продукта.
    /// </remarks>
    public static double SurfaceToCenterRatio(double thicknessM, double penetrationDepthM)
        => penetrationDepthM <= 0 || double.IsInfinity(penetrationDepthM)
            ? 1.0
            : Math.Exp(thicknessM / (2.0 * penetrationDepthM));

    /// <summary>
    /// То же при двустороннем облучении: слои складываются, центр получает
    /// вклад с обеих сторон.
    /// </summary>
    public static double SurfaceToCenterRatioTwoSided(double thicknessM, double penetrationDepthM)
    {
        if (penetrationDepthM <= 0 || double.IsInfinity(penetrationDepthM)) return 1.0;

        double x = thicknessM / penetrationDepthM;
        double surface = 1.0 + Math.Exp(-x);
        double centre = 2.0 * Math.Exp(-x / 2.0);
        return centre > 0 ? surface / centre : double.PositiveInfinity;
    }

    /// <summary>
    /// Толщина слоя, при которой неравномерность не превышает заданной, м.
    /// </summary>
    public static double MaxThicknessForUniformity(double penetrationDepthM, double allowedRatio)
        => allowedRatio <= 1 || double.IsInfinity(penetrationDepthM)
            ? double.PositiveInfinity
            : 2.0 * penetrationDepthM * Math.Log(allowedRatio);

    /// <summary>
    /// Запас устойчивости к тепловому разгону.
    /// </summary>
    /// <remarks>
    /// Разгон начинается, когда рост тепловыделения с температурой обгоняет
    /// рост теплоотвода: 2 pi f eps0 E^2 (d eps'' / dT) больше, чем h A / V.
    /// Возвращается отношение отвода к приросту: больше единицы - устойчиво,
    /// меньше - процесс уходит вразнос. Для материалов с падающим eps''
    /// (вода выше 20 градусов) возвращается бесконечность.
    /// </remarks>
    /// <param name="heatTransferCoefficient">Коэффициент теплоотдачи, Вт/(м^2 К).</param>
    /// <param name="surfaceToVolumeRatio">Отношение площади к объёму тела, 1/м.</param>
    public static double ThermalRunawayMargin(double frequencyHz, DielectricMaterial material,
        double fieldRmsVPerM, double heatTransferCoefficient, double surfaceToVolumeRatio)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (material.LossFactorPerKelvin <= 0) return double.PositiveInfinity;

        double generationSlope = 2.0 * Math.PI * frequencyHz * VacuumPermittivity
                               * material.LossFactorPerKelvin * fieldRmsVPerM * fieldRmsVPerM;
        double removalSlope = heatTransferCoefficient * surfaceToVolumeRatio;

        return generationSlope <= 0 ? double.PositiveInfinity : removalSlope / generationSlope;
    }
}
