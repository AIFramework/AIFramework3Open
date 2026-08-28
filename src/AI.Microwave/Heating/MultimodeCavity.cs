using AI.Microwave.Physics;

namespace AI.Microwave.Heating;

/// <summary>Резонансная мода прямоугольной камеры.</summary>
/// <param name="Family">Семейство: TE или TM.</param>
/// <param name="M">Индекс по ширине.</param>
/// <param name="N">Индекс по высоте.</param>
/// <param name="P">Индекс по глубине.</param>
/// <param name="FrequencyHz">Резонансная частота.</param>
public readonly record struct CavityMode(string Family, int M, int N, int P, double FrequencyHz);

/// <summary>
/// Многомодовая рабочая камера - основная схема промышленных и бытовых
/// СВЧ-установок.
/// </summary>
/// <remarks>
/// Равномерность нагрева в такой камере определяется не геометрией поля
/// (оно принципиально пятнистое), а числом мод, попадающих в полосу
/// источника: чем их больше, тем сильнее усредняется картина. Магнетрон
/// с его узкой полосой возбуждает единицы мод, твердотельный источник
/// умеет качать частоту и потому усредняет намного лучше - в этом и
/// состоит его главное преимущество.
/// </remarks>
public class MultimodeCavity
{
    /// <summary>Ширина камеры, м.</summary>
    public double WidthM { get; set; } = 0.33;

    /// <summary>Высота камеры, м.</summary>
    public double HeightM { get; set; } = 0.23;

    /// <summary>Глубина камеры, м.</summary>
    public double DepthM { get; set; } = 0.35;

    /// <summary>Объём камеры, м^3.</summary>
    public double VolumeM3 => WidthM * HeightM * DepthM;

    /// <summary>Площадь внутренней поверхности, м^2.</summary>
    public double SurfaceAreaM2
        => 2.0 * (WidthM * HeightM + HeightM * DepthM + WidthM * DepthM);

    /// <summary>Резонансная частота моды с индексами m, n, p.</summary>
    public double ResonantFrequencyHz(int m, int n, int p)
    {
        double fm = m / WidthM;
        double fn = n / HeightM;
        double fp = p / DepthM;
        return MicrowavePhysics.SpeedOfLight / 2.0 * Math.Sqrt(fm * fm + fn * fn + fp * fp);
    }

    /// <summary>
    /// Перечисляет моды в полосе частот.
    /// </summary>
    /// <remarks>
    /// TE требует p не меньше 1 и хотя бы один ненулевой из m, n;
    /// TM требует m и n не меньше 1. Моды с совпадающими частотами
    /// (вырожденные) перечисляются по отдельности - они и есть степени
    /// свободы поля.
    /// </remarks>
    public IReadOnlyList<CavityMode> Modes(double frequencyMinHz, double frequencyMaxHz)
    {
        int maxM = (int)Math.Ceiling(2.0 * frequencyMaxHz * WidthM / MicrowavePhysics.SpeedOfLight);
        int maxN = (int)Math.Ceiling(2.0 * frequencyMaxHz * HeightM / MicrowavePhysics.SpeedOfLight);
        int maxP = (int)Math.Ceiling(2.0 * frequencyMaxHz * DepthM / MicrowavePhysics.SpeedOfLight);

        var modes = new List<CavityMode>();
        for (int m = 0; m <= maxM; m++)
        for (int n = 0; n <= maxN; n++)
        for (int p = 0; p <= maxP; p++)
        {
            double f = ResonantFrequencyHz(m, n, p);
            if (f < frequencyMinHz || f > frequencyMaxHz) continue;

            if (p >= 1 && (m > 0 || n > 0)) modes.Add(new CavityMode("TE", m, n, p, f));
            if (m >= 1 && n >= 1) modes.Add(new CavityMode("TM", m, n, p, f));
        }

        modes.Sort((a, b) => a.FrequencyHz.CompareTo(b.FrequencyHz));
        return modes;
    }

    /// <summary>Число мод в полосе.</summary>
    public int ModeCount(double frequencyMinHz, double frequencyMaxHz)
        => Modes(frequencyMinHz, frequencyMaxHz).Count;

    /// <summary>
    /// Плотность мод по формуле Вейля, 1/Гц: dN/df = 8 pi V f^2 / c^3.
    /// Асимптотика, полезная для быстрой прикидки без перебора.
    /// </summary>
    public double ModeDensityPerHz(double frequencyHz)
        => 8.0 * Math.PI * VolumeM3 * frequencyHz * frequencyHz
           / Math.Pow(MicrowavePhysics.SpeedOfLight, 3);

    /// <summary>
    /// Число мод, попадающих в полосу источника - основной показатель
    /// равномерности многомодовой камеры.
    /// </summary>
    public int ModesInSourceBandwidth(double centerFrequencyHz, double bandwidthHz)
        => ModeCount(centerFrequencyHz - bandwidthHz / 2.0, centerFrequencyHz + bandwidthHz / 2.0);

    /// <summary>
    /// Добротность пустой камеры по потерям в стенках: Q = 2V / (delta S).
    /// </summary>
    public double WallQualityFactor(double frequencyHz, double conductivity)
    {
        double skinDepth = MicrowavePhysics.SkinDepth(frequencyHz, conductivity);
        return 2.0 * VolumeM3 / (skinDepth * SurfaceAreaM2);
    }

    /// <summary>
    /// Доля запасённой энергии, приходящаяся на загрузку.
    /// </summary>
    public double FillingFactor(double loadVolumeM3, double loadPermittivity)
    {
        double occupied = Math.Clamp(loadVolumeM3, 0, VolumeM3);
        double stored = loadPermittivity * occupied;
        double rest = VolumeM3 - occupied;
        return stored + rest > 0 ? stored / (stored + rest) : 0.0;
    }

    /// <summary>Добротность по потерям в загрузке.</summary>
    public double LoadQualityFactor(double loadVolumeM3, DielectricMaterial load)
    {
        ArgumentNullException.ThrowIfNull(load);
        double fill = FillingFactor(loadVolumeM3, load.RelativePermittivity);
        double tan = load.LossTangent;
        return fill * tan > 0 ? 1.0 / (fill * tan) : double.PositiveInfinity;
    }

    /// <summary>Нагруженная добротность.</summary>
    public double LoadedQualityFactor(double frequencyHz, double conductivity,
        double loadVolumeM3, DielectricMaterial load)
    {
        double wall = WallQualityFactor(frequencyHz, conductivity);
        double dielectric = LoadQualityFactor(loadVolumeM3, load);
        return 1.0 / (1.0 / wall + 1.0 / dielectric);
    }

    /// <summary>
    /// Доля подведённой мощности, поглощённая загрузкой, а не стенками.
    /// </summary>
    public double HeatingEfficiency(double frequencyHz, double conductivity,
        double loadVolumeM3, DielectricMaterial load)
    {
        double dielectric = LoadQualityFactor(loadVolumeM3, load);
        if (double.IsInfinity(dielectric)) return 0.0;
        return LoadedQualityFactor(frequencyHz, conductivity, loadVolumeM3, load) / dielectric;
    }

    /// <summary>
    /// Ширина резонансной кривой одной моды, Гц: f / Q. Если она меньше
    /// расстояния между модами, поле в камере пятнистое.
    /// </summary>
    public double ModeBandwidthHz(double frequencyHz, double loadedQualityFactor)
        => loadedQualityFactor > 0 ? frequencyHz / loadedQualityFactor : double.PositiveInfinity;

    /// <summary>
    /// Число мод, реально возбуждаемых источником.
    /// </summary>
    /// <remarks>
    /// Считать резонансы в одной лишь полосе источника недостаточно: у
    /// нагруженной камеры добротность падает до единиц, каждая мода
    /// расплывается на сотни мегагерц и перекрывается с соседями. Поэтому
    /// эффективная полоса возбуждения складывается из полосы источника и
    /// ширины самой моды f / Q.
    /// <para>
    /// Именно это число, а не геометрия, определяет равномерность:
    /// пустая камера с узкополосным магнетроном даёт единицы мод и жёсткие
    /// стоячие волны, загруженная - десятки и почти ровное поле.
    /// </para>
    /// </remarks>
    public int EffectiveModeCount(double centerFrequencyHz, double sourceBandwidthHz,
        double loadedQualityFactor)
    {
        double modeWidth = ModeBandwidthHz(centerFrequencyHz, loadedQualityFactor);
        double effective = sourceBandwidthHz + modeWidth;
        return ModesInSourceBandwidth(centerFrequencyHz, effective);
    }
}
