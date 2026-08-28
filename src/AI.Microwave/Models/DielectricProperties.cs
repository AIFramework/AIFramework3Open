namespace AI.Microwave.Models;

/// <summary>
/// Свойства диэлектрика для линзы, обтекателя или окна вывода энергии.
/// </summary>
/// <remarks>
/// Раньше материал линзы был зашит константами прямо в теле расчёта
/// (eps_r = 2.08, tg = 0.0004, плотность 2200, цена 50), поэтому сменить
/// фторопласт на полиэтилен можно было только правкой кода.
/// </remarks>
public class DielectricProperties
{
    public required string Name { get; set; }

    /// <summary>Относительная диэлектрическая проницаемость eps_r.</summary>
    public double RelativePermittivity { get; set; }

    /// <summary>Тангенс угла диэлектрических потерь в диапазоне 2...10 ГГц.</summary>
    public double LossTangent { get; set; }

    /// <summary>Плотность, кг/м^3.</summary>
    public double Density { get; set; }

    /// <summary>Теплопроводность, Вт/(м К).</summary>
    public double ThermalConductivity { get; set; }

    /// <summary>Электрическая прочность, В/м.</summary>
    public double DielectricStrength { get; set; }

    /// <summary>Максимальная рабочая температура, градусы Цельсия.</summary>
    public double MaxServiceTemperature { get; set; }

    /// <summary>Относительная стоимость единицы массы (медь = 1.0).</summary>
    public double Cost { get; set; }

    /// <summary>Показатель преломления n = sqrt(eps_r).</summary>
    public double RefractiveIndex => Math.Sqrt(RelativePermittivity);

    /// <summary>
    /// Погонное затухание в диэлектрике, Нп/м: alpha = pi n tg / lambda.
    /// </summary>
    public double AttenuationNpPerM(double lambdaM)
        => Math.PI * RefractiveIndex * LossTangent / lambdaM;

    /// <summary>
    /// Коэффициент отражения по мощности от одной границы с воздухом
    /// при нормальном падении: R = ((n-1)/(n+1))^2.
    /// </summary>
    public double SurfaceReflectance
    {
        get
        {
            double n = RefractiveIndex;
            double g = (n - 1.0) / (n + 1.0);
            return g * g;
        }
    }

    /// <summary>
    /// Пропускание плоскопараллельной пластины по мощности с учётом обеих
    /// границ и многократных переотражений: T = (1-R)/(1+R).
    /// </summary>
    /// <remarks>
    /// Прежняя запись 1 - 2R становилась отрицательной при eps_r больше 34,
    /// после чего логарифм от неё давал NaN.
    /// </remarks>
    public double SlabTransmittance
    {
        get
        {
            double r = SurfaceReflectance;
            return (1.0 - r) / (1.0 + r);
        }
    }

    /// <summary>
    /// Худший (синфазный) КСВ непросветлённой пластины: две границы могут
    /// сложиться в фазе, давая |G| = 2g/(1+g^2).
    /// </summary>
    public double WorstCaseVswr
    {
        get
        {
            double n = RefractiveIndex;
            double g = (n - 1.0) / (n + 1.0);
            double gTotal = 2.0 * g / (1.0 + g * g);
            return Physics.MicrowavePhysics.VswrFromReflection(gTotal);
        }
    }

    /// <summary>
    /// Толщина ступени зонирования линзы, м: lambda / (n - 1). Срезание
    /// профиля на эту величину не меняет фазу на выходе, но снимает массу.
    /// </summary>
    public double ZoneStepM(double lambdaM) => lambdaM / (RefractiveIndex - 1.0);

    /// <summary>
    /// Толщина просветляющего слоя, м: lambda / (4 sqrt(n)) при показателе
    /// преломления слоя sqrt(n).
    /// </summary>
    public double AntiReflectionLayerThicknessM(double lambdaM)
        => lambdaM / (4.0 * Math.Sqrt(RefractiveIndex));

    /// <summary>Типовые СВЧ-диэлектрики.</summary>
    public static List<DielectricProperties> GetStandardDielectrics() =>
    [
        new() { Name = "Фторопласт-4 (PTFE)", RelativePermittivity = 2.08, LossTangent = 0.0004,
                Density = 2200, ThermalConductivity = 0.25, DielectricStrength = 20e6,
                MaxServiceTemperature = 260, Cost = 50 },
        new() { Name = "Rexolite 1422",       RelativePermittivity = 2.53, LossTangent = 0.00066,
                Density = 1050, ThermalConductivity = 0.13, DielectricStrength = 22e6,
                MaxServiceTemperature = 95,  Cost = 60 },
        new() { Name = "Полиэтилен HDPE",     RelativePermittivity = 2.30, LossTangent = 0.0004,
                Density = 950,  ThermalConductivity = 0.45, DielectricStrength = 22e6,
                MaxServiceTemperature = 80,  Cost = 5 },
        new() { Name = "Полипропилен",        RelativePermittivity = 2.25, LossTangent = 0.0005,
                Density = 905,  ThermalConductivity = 0.22, DielectricStrength = 24e6,
                MaxServiceTemperature = 100, Cost = 4 },
        new() { Name = "Полистирол",          RelativePermittivity = 2.55, LossTangent = 0.0003,
                Density = 1050, ThermalConductivity = 0.14, DielectricStrength = 20e6,
                MaxServiceTemperature = 75,  Cost = 6 },
        new() { Name = "Кварц плавленый",     RelativePermittivity = 3.78, LossTangent = 0.0001,
                Density = 2200, ThermalConductivity = 1.40, DielectricStrength = 25e6,
                MaxServiceTemperature = 1000, Cost = 300 },
    ];
}
