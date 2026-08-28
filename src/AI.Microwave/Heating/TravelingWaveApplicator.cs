using AI.Microwave.Models;

namespace AI.Microwave.Heating;

/// <summary>
/// Аппликатор бегущей волны: материал в виде слоя проходит через волновод,
/// поглощая мощность за один проход. Схема непрерывных линий - сушки,
/// пастеризации, вулканизации.
/// </summary>
/// <remarks>
/// В отличие от многомодовой камеры здесь нет резонанса, поэтому картина
/// поля предсказуема, а неравномерность задаётся только глубиной
/// проникновения. Плата - непоглощённая мощность, которую приходится
/// сбрасывать в балластную нагрузку.
/// </remarks>
public class TravelingWaveApplicator
{
    /// <summary>Питающий волновод.</summary>
    public required RectangularWaveguide Waveguide { get; set; }

    /// <summary>Обрабатываемый материал.</summary>
    public required DielectricMaterial Load { get; set; }

    /// <summary>Толщина слоя материала, м.</summary>
    public double ThicknessM { get; set; } = 0.02;

    /// <summary>Длина участка взаимодействия вдоль волновода, м.</summary>
    public double InteractionLengthM { get; set; } = 1.0;

    /// <summary>Облучается ли слой с двух сторон.</summary>
    public bool TwoSided { get; set; }

    /// <summary>Температура материала, влияющая на его потери, градусы Цельсия.</summary>
    public double TemperatureC { get; set; } = 20;

    /// <summary>Глубина проникновения при текущей температуре, м.</summary>
    public double PenetrationDepthM(double lambdaM) => Load.PenetrationDepthM(lambdaM, TemperatureC);

    /// <summary>Доля мощности, отражённая от поверхности слоя.</summary>
    public double ReflectedFraction => Load.SurfaceReflectance;

    /// <summary>
    /// Доля подведённой мощности, поглощённая слоем за один проход.
    /// </summary>
    public double AbsorbedFraction(double lambdaM)
    {
        double depth = PenetrationDepthM(lambdaM);
        if (double.IsInfinity(depth) || depth <= 0) return 0.0;

        double entering = 1.0 - ReflectedFraction;
        double path = TwoSided ? ThicknessM / 2.0 : ThicknessM;
        return entering * (1.0 - Math.Exp(-path / depth));
    }

    /// <summary>
    /// Доля мощности, дошедшая до балластной нагрузки на дальнем конце.
    /// </summary>
    public double TransmittedFraction(double lambdaM)
        => Math.Max(1.0 - ReflectedFraction - AbsorbedFraction(lambdaM), 0.0);

    /// <summary>
    /// Неравномерность прогрева по толщине: отношение тепловыделения на
    /// поверхности к тепловыделению в середине слоя.
    /// </summary>
    public double UniformityRatio(double lambdaM)
    {
        double depth = PenetrationDepthM(lambdaM);
        return TwoSided
            ? DielectricHeating.SurfaceToCenterRatioTwoSided(ThicknessM, depth)
            : DielectricHeating.SurfaceToCenterRatio(ThicknessM, depth);
    }

    /// <summary>
    /// Максимальная толщина слоя при допустимой неравномерности, м.
    /// </summary>
    public double MaxThicknessM(double lambdaM, double allowedRatio = 2.0)
    {
        double depth = DielectricHeating.MaxThicknessForUniformity(
            PenetrationDepthM(lambdaM), allowedRatio);
        return TwoSided ? 2.0 * depth : depth;
    }

    /// <summary>
    /// Действующее поле в материале при заданной подведённой мощности, В/м.
    /// </summary>
    /// <remarks>
    /// Поглощённая мощность распределяется по объёму материала, занятому
    /// в сечении волновода; отсюда обратным ходом находится поле.
    /// </remarks>
    public double FieldInLoadVPerM(double powerW, double lambdaM, double frequencyHz)
    {
        double absorbed = powerW * AbsorbedFraction(lambdaM);
        double slabHeight = Math.Min(ThicknessM, Waveguide.HeightM);
        double volume = Waveguide.WidthM * slabHeight * InteractionLengthM;
        if (volume <= 0) return 0.0;

        return DielectricHeating.FieldForVolumetricPower(
            frequencyHz, Load.LossFactorAt(TemperatureC), absorbed / volume);
    }

    /// <summary>
    /// Производительность линии, кг/ч, при заданной мощности и нагреве.
    /// </summary>
    public double ThroughputKgPerHour(double powerW, double lambdaM, double deltaTemperatureK,
        double latentHeatJPerKg = 0)
        => DielectricHeating.ThroughputKgPerHour(
            powerW, Load, deltaTemperatureK, AbsorbedFraction(lambdaM), latentHeatJPerKg);

    /// <summary>
    /// Мощность, которую надо подвести для заданной производительности, Вт.
    /// </summary>
    public double RequiredPowerW(double throughputKgPerHour, double lambdaM,
        double deltaTemperatureK, double latentHeatJPerKg = 0)
    {
        double absorbed = AbsorbedFraction(lambdaM);
        if (absorbed <= 0) return double.PositiveInfinity;

        double perKg = Load.SpecificHeatJPerKgK * deltaTemperatureK + latentHeatJPerKg;
        return throughputKgPerHour * perKg / 3600.0 / absorbed;
    }
}
