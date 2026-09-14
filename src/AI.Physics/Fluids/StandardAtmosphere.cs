#nullable enable
using AI.Units;

namespace AI.Physics.Fluids;

/// <summary>
/// Международная стандартная атмосфера ISA (ISO 2533, ICAO): температура, давление, плотность, скорость звука
/// и вязкость сухого воздуха в зависимости от геопотенциальной высоты.
/// </summary>
/// <remarks>
/// Атмосфера разбита на слои с постоянным градиентом температуры: тропосфера −6,5 К/км до 11 км,
/// изотермический слой до 20 км, потепление на 1 и 2,8 К/км до 47 км, изотермический слой до 51 км,
/// похолодание на 2,8 и 2 К/км до 84 852 м. Давление получено из уравнения гидростатики с идеальным газом:
/// в слое с градиентом L давление равно p₀·(T/T₀)^(−g₀/(L·R)), в изотермическом p₀·exp(−g₀·ΔH/(R·T)).
/// Высота геопотенциальная: с ней ускорение свободного падения постоянно и равно g₀. Геометрическую высоту
/// переводит <see cref="GeopotentialAltitude"/>.
/// <para>
/// Ниже 47 км таблицы совпадают с ГОСТ 4401-81 и атмосферой ICAO. ISO 2533 определена до 80 км; слой
/// от 80 до 84 852 м продолжен так же, как в стандартной атмосфере США 1976 года, где выше 80 км
/// приводится молекулярная температура: от кинетической она отличается меньше чем на 0,05 %.
/// Газовая постоянная воздуха, как и в таблицах стандарта, получена из R* = 8,31432 Дж/(моль·К)
/// и M = 28,9644 г/моль, а не из CODATA: только так давления совпадают с таблицами до седьмого знака.
/// </para>
/// </remarks>
public static class StandardAtmosphere
{
    /// <summary>Удельная газовая постоянная сухого воздуха по таблицам стандарта, около 287,05307 Дж/(кг·К)</summary>
    private const double AirGasConstant = 8.31432 / 0.0289644;

    /// <summary>Показатель адиабаты воздуха</summary>
    private const double AdiabaticIndex = 1.4;

    /// <summary>Условный радиус Земли для перевода высот по ISO 2533, м</summary>
    private const double EarthRadius = 6_356_766.0;

    /// <summary>Коэффициент формулы Сазерленда, кг/(м·с·К^½)</summary>
    private const double SutherlandBeta = 1.458e-6;

    /// <summary>Постоянная Сазерленда, К</summary>
    private const double SutherlandConstant = 110.4;

    /// <summary>Нижние границы слоев по геопотенциальной высоте, м; последняя граница есть верх модели</summary>
    private static readonly double[] LayerBases = [0, 11_000, 20_000, 32_000, 47_000, 51_000, 71_000, 84_852];

    /// <summary>Градиенты температуры в слоях, К/м</summary>
    private static readonly double[] LayerLapses = [-0.0065, 0, 0.001, 0.0028, 0, -0.0028, -0.002];

    private static readonly double[] LayerTemperatures = new double[LayerLapses.Length];

    private static readonly double[] LayerPressures = new double[LayerLapses.Length];

    static StandardAtmosphere()
    {
        LayerTemperatures[0] = 288.15;
        LayerPressures[0] = PhysicalConstants.StandardAtmosphere.SiValue;

        for (int i = 1; i < LayerLapses.Length; i++)
        {
            (LayerTemperatures[i], LayerPressures[i]) = InLayer(i - 1, LayerBases[i]);
        }
    }

    /// <summary>Нижний предел модели: −5000 м геопотенциальной высоты, продолжение тропосферы вниз</summary>
    public static Quantity MinAltitude { get; } = new(-5_000.0, Dimension.LengthDim);

    /// <summary>Верхний предел модели: 84 852 м геопотенциальной высоты, это 86 км геометрической</summary>
    public static Quantity MaxAltitude { get; } = new(84_852.0, Dimension.LengthDim);

    /// <summary>Температура воздуха</summary>
    /// <param name="altitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity Temperature(Quantity altitude)
        => new(State(altitude).Temperature, Dimension.TemperatureDim);

    /// <summary>Давление воздуха</summary>
    /// <param name="altitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity Pressure(Quantity altitude)
        => new(State(altitude).Pressure, Dimension.Pressure);

    /// <summary>Плотность воздуха по уравнению состояния идеального газа: <c>ρ = p/(R·T)</c></summary>
    /// <param name="altitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity Density(Quantity altitude)
    {
        var (temperature, pressure) = State(altitude);

        return new Quantity(pressure / (AirGasConstant * temperature), Dimension.Density);
    }

    /// <summary>Скорость звука: <c>a = √(γ·R·T)</c>, γ = 1,4</summary>
    /// <param name="altitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity SpeedOfSound(Quantity altitude)
        => new(Math.Sqrt(AdiabaticIndex * AirGasConstant * State(altitude).Temperature), Dimension.Velocity);

    /// <summary>
    /// Динамическая вязкость по формуле Сазерленда: <c>μ = β·T^1,5/(T + S)</c>, β = 1,458·10⁻⁶, S = 110,4 К
    /// </summary>
    /// <param name="altitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity DynamicViscosity(Quantity altitude)
        => new(Sutherland(State(altitude).Temperature), FlowDynamics.ViscosityDimension);

    /// <summary>Кинематическая вязкость: <c>ν = μ/ρ</c></summary>
    /// <param name="altitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity KinematicViscosity(Quantity altitude)
    {
        var (temperature, pressure) = State(altitude);
        double density = pressure / (AirGasConstant * temperature);

        return new Quantity(Sutherland(temperature) / density, FlowDynamics.KinematicViscosityDimension);
    }

    /// <summary>
    /// Геопотенциальная высота по геометрической: <c>H = r·z/(r + z)</c>, r = 6 356 766 м
    /// </summary>
    /// <param name="geometricAltitude">Геометрическая высота над уровнем моря</param>
    public static Quantity GeopotentialAltitude(Quantity geometricAltitude)
    {
        double z = geometricAltitude.RequireSi(Dimension.LengthDim, nameof(geometricAltitude));

        if (!(z > -EarthRadius) || double.IsInfinity(z))
            throw new ArgumentOutOfRangeException(nameof(geometricAltitude), "Высота должна быть конечной и выше центра Земли");

        return new Quantity(EarthRadius * z / (EarthRadius + z), Dimension.LengthDim);
    }

    /// <summary>
    /// Геометрическая высота по геопотенциальной: <c>z = r·H/(r − H)</c>, r = 6 356 766 м
    /// </summary>
    /// <param name="geopotentialAltitude">Геопотенциальная высота над уровнем моря</param>
    public static Quantity GeometricAltitude(Quantity geopotentialAltitude)
    {
        double h = geopotentialAltitude.RequireSi(Dimension.LengthDim, nameof(geopotentialAltitude));

        if (!(h < EarthRadius) || !(h > -EarthRadius))
            throw new ArgumentOutOfRangeException(nameof(geopotentialAltitude), "Геопотенциальная высота должна быть меньше радиуса Земли");

        return new Quantity(EarthRadius * h / (EarthRadius - h), Dimension.LengthDim);
    }

    private static double Sutherland(double temperature)
        => SutherlandBeta * temperature * Math.Sqrt(temperature) / (temperature + SutherlandConstant);

    /// <summary>Температура, К, и давление, Па, на геопотенциальной высоте</summary>
    private static (double Temperature, double Pressure) State(Quantity altitude)
    {
        double h = altitude.RequireSi(Dimension.LengthDim, nameof(altitude));

        if (!(h >= MinAltitude.SiValue && h <= MaxAltitude.SiValue))
            throw new ArgumentOutOfRangeException(nameof(altitude), "Стандартная атмосфера определена от −5000 до 84 852 м");

        int layer = LayerBases.Length - 2;

        while (layer > 0 && h < LayerBases[layer])
            layer--;

        return InLayer(layer, h);
    }

    /// <summary>Температура и давление на высоте h внутри слоя с номером layer</summary>
    private static (double Temperature, double Pressure) InLayer(int layer, double h)
    {
        double g = PhysicalConstants.StandardGravity.SiValue;
        double baseTemperature = LayerTemperatures[layer];
        double basePressure = LayerPressures[layer];
        double lapse = LayerLapses[layer];
        double rise = h - LayerBases[layer];

        if (lapse == 0)
            return (baseTemperature, basePressure * Math.Exp(-g * rise / (AirGasConstant * baseTemperature)));

        double temperature = baseTemperature + (lapse * rise);

        return (temperature, basePressure * Math.Pow(temperature / baseTemperature, -g / (lapse * AirGasConstant)));
    }
}
