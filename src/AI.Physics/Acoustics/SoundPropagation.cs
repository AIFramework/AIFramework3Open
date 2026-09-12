using AI.DataStructs.Algebraic;
using AI.Units;

namespace AI.Physics.Acoustics;

/// <summary>
/// Ослабление звука на открытом воздухе по ISO 9613: расхождение фронта, поглощение воздухом,
/// густая листва, пористый грунт. Всё в децибелах; итог в полосе — сумма слагаемых.
/// </summary>
public static class SoundPropagation
{
    /// <summary>Опорная температура ISO 9613-1, К</summary>
    private const double ReferenceTemperature = 293.15;

    /// <summary>Опорное давление ISO 9613-1, Па</summary>
    private const double ReferencePressure = 101_325.0;

    /// <summary>Температура тройной точки воды, К</summary>
    private const double TriplePoint = 273.16;

    /// <summary>Потери в листве на пути 10–20 м по октавным полосам, дБ (ISO 9613-2, табл. A.1)</summary>
    private static readonly double[] FoliageShortDb = [0, 0, 1, 1, 1, 1, 2, 3];

    /// <summary>Потери в листве на пути 20–200 м по октавным полосам, дБ/м (ISO 9613-2, табл. A.1)</summary>
    private static readonly double[] FoliagePerMetreDb = [0.02, 0.03, 0.04, 0.05, 0.06, 0.08, 0.09, 0.12];

    /// <summary>
    /// Ослабление за счёт расхождения фронта: 10·lg(4πd²) ≈ 20·lg d + 11 дБ для точечного источника
    /// и 10·lg(2πd) ≈ 10·lg d + 8 дБ для линейного
    /// </summary>
    /// <param name="distance">Расстояние до источника; до линии — по перпендикуляру</param>
    /// <param name="geometry">Геометрия источника</param>
    public static double DivergenceDb(Quantity distance, SourceGeometry geometry)
    {
        double d = RequirePositiveLength(distance, nameof(distance));

        return geometry == SourceGeometry.Line
            ? 10.0 * Math.Log10(2.0 * Math.PI * d)
            : 10.0 * Math.Log10(4.0 * Math.PI * d * d);
    }

    /// <summary>
    /// Коэффициент поглощения звука воздухом по ISO 9613-1, дБ/м: релаксация кислорода и азота плюс
    /// классическое поглощение. Растёт с частотой почти квадратично — высокие частоты гаснут на расстоянии первыми
    /// </summary>
    /// <param name="frequency">Частота</param>
    /// <param name="temperature">Температура воздуха</param>
    /// <param name="relativeHumidityPercent">Относительная влажность, %</param>
    /// <param name="pressure">Атмосферное давление; по умолчанию стандартное</param>
    public static double AirAbsorptionDbPerMetre(Quantity frequency, Quantity temperature, double relativeHumidityPercent, Quantity pressure = default)
    {
        double f = frequency.RequireSi(Dimension.Frequency, nameof(frequency));
        double t = temperature.RequireSi(Dimension.TemperatureDim, nameof(temperature));
        if (!(f > 0))
            throw new ArgumentOutOfRangeException(nameof(frequency), "Частота должна быть положительной");
        if (!(t > 0))
            throw new ArgumentOutOfRangeException(nameof(temperature), "Абсолютная температура должна быть положительной");
        if (!(relativeHumidityPercent >= 0 && relativeHumidityPercent <= 100))
            throw new ArgumentOutOfRangeException(nameof(relativeHumidityPercent), "Относительная влажность лежит на 0–100 %");

        double pa = pressure.Dimension.IsDimensionless && pressure.SiValue == 0.0
            ? ReferencePressure
            : pressure.RequireSi(Dimension.Pressure, nameof(pressure));
        double relativePressure = pa / ReferencePressure;
        double relativeTemperature = t / ReferenceTemperature;

        // Молярная концентрация водяного пара, %, через давление насыщения относительно тройной точки
        double saturation = Math.Pow(10.0, (-6.8346 * Math.Pow(TriplePoint / t, 1.261)) + 4.6151);
        double h = relativeHumidityPercent * saturation / relativePressure;

        // Частоты релаксации кислорода и азота, Гц
        double oxygen = relativePressure * (24.0 + (4.04e4 * h * (0.02 + h) / (0.391 + h)));
        double nitrogen = relativePressure * Math.Pow(relativeTemperature, -0.5)
            * (9.0 + (280.0 * h * Math.Exp(-4.170 * (Math.Pow(relativeTemperature, -1.0 / 3.0) - 1.0))));

        double f2 = f * f;
        double classical = 1.84e-11 / relativePressure * Math.Sqrt(relativeTemperature);
        double relaxation = Math.Pow(relativeTemperature, -2.5)
            * ((0.01275 * Math.Exp(-2239.1 / t) / (oxygen + (f2 / oxygen)))
                + (0.1068 * Math.Exp(-3352.0 / t) / (nitrogen + (f2 / nitrogen))));

        return 8.686 * f2 * (classical + relaxation);
    }

    /// <summary>
    /// Ослабление в густой листве по ISO 9613-2 (приложение A) по октавным полосам, дБ. Первые 10 м листва
    /// звук почти не задерживает, на 10–20 м даёт фиксированные потери, дальше — потери на метр; путь
    /// длиннее 200 м по стандарту считается как 200 м
    /// </summary>
    /// <param name="depth">Путь звука сквозь листву</param>
    public static Vector FoliageAttenuationDb(Quantity depth)
    {
        double d = depth.RequireSi(Dimension.LengthDim, nameof(depth));
        if (!(d >= 0))
            throw new ArgumentOutOfRangeException(nameof(depth), "Путь сквозь листву не может быть отрицательным");

        var attenuation = new Vector(OctaveBands.Count);
        if (d < 10.0)
            return attenuation;

        for (int band = 0; band < OctaveBands.Count; band++)
            attenuation[band] = d <= 20.0 ? FoliageShortDb[band] : FoliagePerMetreDb[band] * Math.Min(d, 200.0);

        return attenuation;
    }

    /// <summary>
    /// Ослабление над пористым грунтом — лесная подстилка, поле, — упрощённый метод ISO 9613-2, п. 7.3.2,
    /// для уровня с коррекцией А: A = 4.8 − (2·hm/d)·(17 + 300/d), не меньше нуля
    /// </summary>
    /// <param name="distance">Расстояние от источника до приёмника</param>
    /// <param name="meanHeight">Средняя высота трассы над землёй; на ровной местности — полусумма высот источника и приёмника</param>
    public static double PorousGroundAttenuationDb(Quantity distance, Quantity meanHeight)
    {
        double d = RequirePositiveLength(distance, nameof(distance));
        double h = meanHeight.RequireSi(Dimension.LengthDim, nameof(meanHeight));
        if (!(h >= 0))
            throw new ArgumentOutOfRangeException(nameof(meanHeight), "Высота не может быть отрицательной");

        return Math.Max(0.0, 4.8 - (2.0 * h / d * (17.0 + (300.0 / d))));
    }

    /// <summary>
    /// Уровни звукового давления по октавам в точке приёма: уровни звуковой мощности источника минус
    /// расхождение, поглощение воздухом и листва. Грунт в полосах не учитывается — для уровня
    /// с коррекцией А есть <see cref="PorousGroundAttenuationDb"/>
    /// </summary>
    /// <param name="sourcePowerLevels">Уровни звуковой мощности по октавам, дБ; для линейного источника — на метр длины</param>
    /// <param name="distance">Расстояние до источника; до линии — по перпендикуляру</param>
    /// <param name="geometry">Геометрия источника</param>
    /// <param name="foliageDepth">Путь звука сквозь густую листву</param>
    /// <param name="temperature">Температура воздуха</param>
    /// <param name="relativeHumidityPercent">Относительная влажность, %</param>
    public static Vector ReceivedLevels(
        Vector sourcePowerLevels,
        Quantity distance,
        SourceGeometry geometry,
        Quantity foliageDepth,
        Quantity temperature,
        double relativeHumidityPercent)
    {
        OctaveBands.RequireBands(sourcePowerLevels);
        double d = RequirePositiveLength(distance, nameof(distance));
        double divergence = DivergenceDb(distance, geometry);
        Vector foliage = FoliageAttenuationDb(foliageDepth);
        Vector frequencies = OctaveBands.MidbandFrequencies;

        var received = new Vector(OctaveBands.Count);
        for (int band = 0; band < OctaveBands.Count; band++)
        {
            double absorption = AirAbsorptionDbPerMetre(new Quantity(frequencies[band], Dimension.Frequency), temperature, relativeHumidityPercent) * d;
            received[band] = sourcePowerLevels[band] - divergence - absorption - foliage[band];
        }

        return received;
    }

    private static double RequirePositiveLength(Quantity length, string name)
    {
        double value = length.RequireSi(Dimension.LengthDim, name);
        if (!(value > 0))
            throw new ArgumentOutOfRangeException(name, "Расстояние должно быть положительным");

        return value;
    }
}
