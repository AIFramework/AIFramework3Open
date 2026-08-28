namespace AI.Microwave.Physics;

/// <summary>
/// Физические константы и базовые соотношения СВЧ-техники.
/// Вынесены из калькуляторов: раньше каждый из них держал собственную копию
/// скорости света, 377 Ом и формулы возвратных потерь, поэтому ошибка в
/// формуле тиражировалась во все три.
/// </summary>
public static class MicrowavePhysics
{
    /// <summary>Скорость света в вакууме, м/с.</summary>
    public const double SpeedOfLight = 299792458.0;

    /// <summary>Магнитная постоянная, Гн/м.</summary>
    public const double VacuumPermeability = 4e-7 * Math.PI;

    /// <summary>Волновое сопротивление свободного пространства, Ом.</summary>
    public const double FreeSpaceImpedance = 376.730313412;

    /// <summary>Множитель перевода неперов в децибелы.</summary>
    public const double NeperToDb = 8.685889638065035;

    /// <summary>Длина волны в свободном пространстве, м.</summary>
    public static double Wavelength(double frequencyHz) => SpeedOfLight / frequencyHz;

    /// <summary>Глубина скин-слоя, м: d = sqrt(1 / (pi f mu0 sigma)).</summary>
    public static double SkinDepth(double frequencyHz, double conductivity)
        => Math.Sqrt(1.0 / (Math.PI * frequencyHz * VacuumPermeability * conductivity));

    /// <summary>Поверхностное сопротивление проводника, Ом на квадрат: Rs = 1 / (sigma d).</summary>
    public static double SurfaceResistance(double frequencyHz, double conductivity)
        => 1.0 / (conductivity * SkinDepth(frequencyHz, conductivity));

    /// <summary>
    /// Доля мощности, поглощаемая металлическим зеркалом при нормальном
    /// падении: A = 4 Rs / eta0. Для меди на 2.45 ГГц это 1.4e-4, а не 1 %.
    /// </summary>
    public static double MetalAbsorptance(double frequencyHz, double conductivity)
        => 4.0 * SurfaceResistance(frequencyHz, conductivity) / FreeSpaceImpedance;

    /// <summary>Модуль коэффициента отражения по напряжению из КСВ.</summary>
    public static double ReflectionCoefficient(double vswr)
        => vswr <= 1.0 ? 0.0 : (vswr - 1.0) / (vswr + 1.0);

    /// <summary>КСВ из модуля коэффициента отражения по напряжению.</summary>
    public static double VswrFromReflection(double gamma)
    {
        gamma = Math.Clamp(Math.Abs(gamma), 0.0, 1.0 - 1e-9);
        return (1.0 + gamma) / (1.0 - gamma);
    }

    /// <summary>
    /// Возвратные потери, дБ, в отрицательной записи: -20 дБ означает, что
    /// отражается 1 % мощности.
    /// </summary>
    /// <remarks>
    /// Прежняя запись -20*lg((VSWR+1)/(VSWR-1)) делила на ноль при идеальном
    /// согласовании. Здесь идеальный случай ограничен -200 дБ, то есть числом.
    /// </remarks>
    public static double ReturnLossDb(double vswr)
    {
        double gamma = ReflectionCoefficient(vswr);
        return gamma <= 1e-10 ? -200.0 : 20.0 * Math.Log10(gamma);
    }

    /// <summary>Доля мощности, прошедшая через рассогласованный стык: 1 - |G|^2.</summary>
    public static double MismatchEfficiency(double vswr)
    {
        double g = ReflectionCoefficient(vswr);
        return 1.0 - g * g;
    }

    /// <summary>Амплитуда поля бегущей плоской волны: E = sqrt(2 S Z).</summary>
    public static double PeakFieldFromPowerDensity(double powerDensityWPerM2,
        double impedanceOhm = FreeSpaceImpedance)
        => Math.Sqrt(2.0 * powerDensityWPerM2 * impedanceOhm);

    /// <summary>Перевод разов в децибелы по мощности.</summary>
    public static double ToDb(double ratio) => 10.0 * Math.Log10(ratio);

    /// <summary>Перевод децибел в разы по мощности.</summary>
    public static double FromDb(double db) => Math.Pow(10.0, db / 10.0);

    /// <summary>Усиление апертурной антенны: G = 4 pi A eta / lambda^2.</summary>
    public static double ApertureGain(double physicalAreaM2, double efficiency, double lambdaM)
        => 4.0 * Math.PI * physicalAreaM2 * efficiency / (lambdaM * lambdaM);

    /// <summary>Граница дальней зоны (зоны Фраунгофера): R = 2 D^2 / lambda.</summary>
    public static double FarFieldDistance(double maxApertureM, double lambdaM)
        => 2.0 * maxApertureM * maxApertureM / lambdaM;

    /// <summary>
    /// Потери на неточность отражающей поверхности (формула Рузе):
    /// eta = exp(-(4 pi eps / lambda)^2), eps - СКО профиля.
    /// </summary>
    public static double RuzeEfficiency(double rmsSurfaceErrorM, double lambdaM)
    {
        double x = 4.0 * Math.PI * rmsSurfaceErrorM / lambdaM;
        return Math.Exp(-x * x);
    }
}
