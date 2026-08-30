using AI.Units;

namespace AI.Microwave.Physics;

/// <summary>
/// Типизированный слой над <see cref="MicrowavePhysics"/>: те же соотношения,
/// но с физическими величинами вместо чисел с единицами в именах параметров.
/// </summary>
/// <remarks>
/// Расчёт выполняют методы <see cref="MicrowavePhysics"/>; здесь только проверка
/// размерностей на входе и восстановление размерности на выходе. Ошибка вида
/// «частота в мегагерцах передана в метод, ожидающий герцы» становится
/// исключением <see cref="DimensionMismatchException"/> либо снимается вовсе:
/// величина сама знает свою единицу.
/// </remarks>
/// <example>
/// <code>
/// Quantity lambda = MicrowaveQuantities.Wavelength(Quantity.Of(2.45, "GHz"));
/// double mm = lambda.In("mm");   // 122.36
/// </code>
/// </example>
public static class MicrowaveQuantities
{
    /// <summary>Удельная электрическая проводимость, См/м</summary>
    public static Dimension Conductivity { get; } = Dimension.Resistance.Pow(-1) / Dimension.LengthDim;

    /// <summary>Плотность потока мощности, Вт/м²</summary>
    public static Dimension PowerDensity { get; } = Dimension.Power / Dimension.Area;

    /// <summary>Напряжённость электрического поля, В/м</summary>
    public static Dimension ElectricField { get; } = Dimension.Voltage / Dimension.LengthDim;

    /// <summary>Длина волны в свободном пространстве</summary>
    /// <param name="frequency">Частота</param>
    public static Quantity Wavelength(Quantity frequency)
    {
        double hz = frequency.RequireSi(Dimension.Frequency, nameof(frequency));
        return new Quantity(MicrowavePhysics.Wavelength(hz), Dimension.LengthDim);
    }

    /// <summary>Глубина скин-слоя</summary>
    /// <param name="frequency">Частота</param>
    /// <param name="conductivity">Удельная проводимость материала</param>
    public static Quantity SkinDepth(Quantity frequency, Quantity conductivity)
    {
        double hz = frequency.RequireSi(Dimension.Frequency, nameof(frequency));
        double sigma = conductivity.RequireSi(Conductivity, nameof(conductivity));

        return new Quantity(MicrowavePhysics.SkinDepth(hz, sigma), Dimension.LengthDim);
    }

    /// <summary>Поверхностное сопротивление проводника (Ом на квадрат)</summary>
    /// <param name="frequency">Частота</param>
    /// <param name="conductivity">Удельная проводимость материала</param>
    public static Quantity SurfaceResistance(Quantity frequency, Quantity conductivity)
    {
        double hz = frequency.RequireSi(Dimension.Frequency, nameof(frequency));
        double sigma = conductivity.RequireSi(Conductivity, nameof(conductivity));

        return new Quantity(MicrowavePhysics.SurfaceResistance(hz, sigma), Dimension.Resistance);
    }

    /// <summary>Доля мощности, поглощаемая металлическим зеркалом при нормальном падении</summary>
    /// <param name="frequency">Частота</param>
    /// <param name="conductivity">Удельная проводимость материала</param>
    public static double MetalAbsorptance(Quantity frequency, Quantity conductivity)
    {
        double hz = frequency.RequireSi(Dimension.Frequency, nameof(frequency));
        double sigma = conductivity.RequireSi(Conductivity, nameof(conductivity));

        return MicrowavePhysics.MetalAbsorptance(hz, sigma);
    }

    /// <summary>Амплитуда поля бегущей плоской волны</summary>
    /// <param name="powerDensity">Плотность потока мощности</param>
    /// <param name="impedance">Волновое сопротивление среды; по умолчанию свободного пространства</param>
    public static Quantity PeakFieldFromPowerDensity(Quantity powerDensity, Quantity impedance = default)
    {
        double s = powerDensity.RequireSi(PowerDensity, nameof(powerDensity));

        double z = impedance.Dimension.IsDimensionless && impedance.SiValue == 0.0
            ? MicrowavePhysics.FreeSpaceImpedance
            : impedance.RequireSi(Dimension.Resistance, nameof(impedance));

        return new Quantity(MicrowavePhysics.PeakFieldFromPowerDensity(s, z), ElectricField);
    }

    /// <summary>Усиление апертурной антенны (в разах)</summary>
    /// <param name="physicalArea">Физическая площадь раскрыва</param>
    /// <param name="efficiency">Апертурный КПД</param>
    /// <param name="wavelength">Длина волны</param>
    public static double ApertureGain(Quantity physicalArea, double efficiency, Quantity wavelength)
    {
        double area = physicalArea.RequireSi(Dimension.Area, nameof(physicalArea));
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));

        return MicrowavePhysics.ApertureGain(area, efficiency, lambda);
    }

    /// <summary>Граница дальней зоны (зоны Фраунгофера)</summary>
    /// <param name="maxAperture">Наибольший размер раскрыва</param>
    /// <param name="wavelength">Длина волны</param>
    public static Quantity FarFieldDistance(Quantity maxAperture, Quantity wavelength)
    {
        double d = maxAperture.RequireSi(Dimension.LengthDim, nameof(maxAperture));
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));

        return new Quantity(MicrowavePhysics.FarFieldDistance(d, lambda), Dimension.LengthDim);
    }

    /// <summary>Потери на неточность отражающей поверхности (формула Рузе)</summary>
    /// <param name="rmsSurfaceError">СКО профиля поверхности</param>
    /// <param name="wavelength">Длина волны</param>
    public static double RuzeEfficiency(Quantity rmsSurfaceError, Quantity wavelength)
    {
        double eps = rmsSurfaceError.RequireSi(Dimension.LengthDim, nameof(rmsSurfaceError));
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));

        return MicrowavePhysics.RuzeEfficiency(eps, lambda);
    }
}
