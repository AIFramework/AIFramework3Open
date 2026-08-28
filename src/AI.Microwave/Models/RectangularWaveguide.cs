using AI.Microwave.Physics;

namespace AI.Microwave.Models;

/// <summary>
/// Прямоугольный волновод на основной моде TE10: широкая стенка a,
/// узкая b. Питающий тракт всех трёх типов антенн.
/// </summary>
/// <remarks>
/// Раньше волновод жил в <see cref="AntennaParameters"/> тремя разрозненными
/// полями (ширина, высота, строка-название), критическая частота нигде не
/// проверялась, а волновое сопротивление TE10 считалось по формуле для
/// TM-волн. Здесь всё это собрано в одном месте.
/// </remarks>
public class RectangularWaveguide
{
    /// <summary>Обозначение по EIA, например WR-340.</summary>
    public string Standard { get; set; } = "custom";

    /// <summary>Широкая стенка a, мм.</summary>
    public double WidthMm { get; set; }

    /// <summary>Узкая стенка b, мм.</summary>
    public double HeightMm { get; set; }

    /// <summary>Широкая стенка a, м.</summary>
    public double WidthM => WidthMm / 1000.0;

    /// <summary>Узкая стенка b, м.</summary>
    public double HeightM => HeightMm / 1000.0;

    /// <summary>Площадь поперечного сечения, м^2.</summary>
    public double CrossSectionAreaM2 => WidthM * HeightM;

    /// <summary>Критическая частота основной моды TE10, Гц: fc = c / (2a).</summary>
    public double CutoffTE10Hz => MicrowavePhysics.SpeedOfLight / (2.0 * WidthM);

    /// <summary>
    /// Критическая частота ближайшей высшей моды (TE20 либо TE01), Гц.
    /// Выше неё волновод перестаёт быть одномодовым.
    /// </summary>
    public double CutoffNextModeHz => Math.Min(
        MicrowavePhysics.SpeedOfLight / WidthM,
        MicrowavePhysics.SpeedOfLight / (2.0 * HeightM));

    /// <summary>Нижняя граница рекомендованной полосы, Гц (1.25 fc).</summary>
    public double BandLowHz => 1.25 * CutoffTE10Hz;

    /// <summary>Верхняя граница рекомендованной полосы, Гц (0.95 от высшей моды).</summary>
    public double BandHighHz => 0.95 * CutoffNextModeHz;

    /// <summary>Распространяется ли основная мода на этой частоте.</summary>
    public bool IsPropagating(double frequencyHz) => frequencyHz > CutoffTE10Hz;

    /// <summary>Одномодовый ли режим на этой частоте.</summary>
    public bool IsSingleMode(double frequencyHz)
        => frequencyHz > CutoffTE10Hz && frequencyHz < CutoffNextModeHz;

    /// <summary>Множитель дисперсии sqrt(1 - (fc/f)^2); NaN за отсечкой.</summary>
    private double Dispersion(double frequencyHz)
    {
        double ratio = CutoffTE10Hz / frequencyHz;
        double v = 1.0 - ratio * ratio;
        return v <= 0 ? double.NaN : Math.Sqrt(v);
    }

    /// <summary>Длина волны в волноводе, м.</summary>
    public double GuideWavelength(double frequencyHz)
        => MicrowavePhysics.Wavelength(frequencyHz) / Dispersion(frequencyHz);

    /// <summary>
    /// Волновое сопротивление моды TE10, Ом: Z = eta0 / sqrt(1 - (fc/f)^2).
    /// </summary>
    /// <remarks>
    /// Прежняя формула умножала на корень вместо деления (это соотношение для
    /// TM-волн) и брала отношение a/b вместо b/a. На WR-340 обе ошибки взаимно
    /// компенсировались, на любом другом волноводе - нет.
    /// </remarks>
    public double WaveImpedanceTE10(double frequencyHz)
        => MicrowavePhysics.FreeSpaceImpedance / Dispersion(frequencyHz);

    /// <summary>
    /// Эквивалентное сопротивление линии в определении напряжение-мощность:
    /// Z_pv = 2 (b/a) Z_TE10. Используется при стыковке с коаксиалом.
    /// </summary>
    public double EquivalentLineImpedance(double frequencyHz)
        => 2.0 * (HeightM / WidthM) * WaveImpedanceTE10(frequencyHz);

    /// <summary>
    /// Пиковая напряжённость поля в центре волновода при передаваемой
    /// мощности P, В/м. Из P = a b E0^2 / (4 Z_TE10).
    /// </summary>
    /// <remarks>
    /// Именно здесь, в горловине, поле максимально: сечение минимально по
    /// всему тракту. Проверять электрическую прочность по апертуре рупора,
    /// как делалось раньше, значит проверять самое безопасное место.
    /// </remarks>
    public double PeakElectricField(double powerWatts, double frequencyHz)
        => Math.Sqrt(4.0 * powerWatts * WaveImpedanceTE10(frequencyHz) / CrossSectionAreaM2);

    /// <summary>Погонное затухание TE10 в стенках, Нп/м.</summary>
    public double AttenuationNpPerM(double frequencyHz, double conductivity)
    {
        double rs = MicrowavePhysics.SurfaceResistance(frequencyHz, conductivity);
        double d = Dispersion(frequencyHz);
        double fcOverF = CutoffTE10Hz / frequencyHz;
        return rs / (HeightM * MicrowavePhysics.FreeSpaceImpedance * d)
             * (1.0 + 2.0 * (HeightM / WidthM) * fcOverF * fcOverF);
    }

    /// <summary>Погонное затухание TE10 в стенках, дБ/м.</summary>
    public double AttenuationDbPerM(double frequencyHz, double conductivity)
        => AttenuationNpPerM(frequencyHz, conductivity) * MicrowavePhysics.NeperToDb;

    /// <summary>Стандартный ряд EIA от дециметров до Ka-диапазона.</summary>
    public static List<RectangularWaveguide> GetStandards() =>
    [
        new() { Standard = "WR-975", WidthMm = 247.65, HeightMm = 123.82 },
        new() { Standard = "WR-650", WidthMm = 165.10, HeightMm = 82.55 },
        new() { Standard = "WR-430", WidthMm = 109.22, HeightMm = 54.61 },
        new() { Standard = "WR-340", WidthMm = 86.36,  HeightMm = 43.18 },
        new() { Standard = "WR-284", WidthMm = 72.14,  HeightMm = 34.04 },
        new() { Standard = "WR-229", WidthMm = 58.17,  HeightMm = 29.08 },
        new() { Standard = "WR-187", WidthMm = 47.55,  HeightMm = 22.15 },
        new() { Standard = "WR-159", WidthMm = 40.39,  HeightMm = 20.19 },
        new() { Standard = "WR-137", WidthMm = 34.85,  HeightMm = 15.80 },
        new() { Standard = "WR-112", WidthMm = 28.50,  HeightMm = 12.62 },
        new() { Standard = "WR-90",  WidthMm = 22.86,  HeightMm = 10.16 },
        new() { Standard = "WR-75",  WidthMm = 19.05,  HeightMm = 9.53  },
        new() { Standard = "WR-62",  WidthMm = 15.80,  HeightMm = 7.90  },
        new() { Standard = "WR-42",  WidthMm = 10.67,  HeightMm = 4.32  },
        new() { Standard = "WR-28",  WidthMm = 7.11,   HeightMm = 3.56  },
    ];

    /// <summary>Поиск по обозначению; null, если такого нет в ряду.</summary>
    public static RectangularWaveguide? Find(string standard)
        => GetStandards().FirstOrDefault(
            w => string.Equals(w.Standard, standard, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Подбор волновода под частоту: тот, чья рекомендованная полоса лучше
    /// всего центрирована на f. Для 2.45 ГГц даёт WR-340.
    /// </summary>
    public static RectangularWaveguide SelectForFrequency(double frequencyHz)
    {
        var all = GetStandards();
        var usable = all.Where(w => frequencyHz >= w.BandLowHz && frequencyHz <= w.BandHighHz).ToList();
        if (usable.Count == 0)
            return all.OrderBy(w => Math.Abs(Math.Log(frequencyHz / w.CutoffTE10Hz))).First();
        return usable.OrderBy(w => Math.Abs(frequencyHz - 0.5 * (w.BandLowHz + w.BandHighHz))).First();
    }
}
