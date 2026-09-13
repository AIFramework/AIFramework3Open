using AI.Statistics;
using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AI.Microwave.Propagation;

/// <summary>Класс помех на местности по ITU-R P.2108 (таблица 3) и P.1812</summary>
public enum ClutterCategory
{
    /// <summary>Вода, море</summary>
    WaterSea,

    /// <summary>Открытая сельская местность</summary>
    OpenRural,

    /// <summary>Пригород</summary>
    Suburban,

    /// <summary>Город</summary>
    Urban,

    /// <summary>Лес, деревья</summary>
    TreesForest,

    /// <summary>Плотная городская застройка</summary>
    DenseUrban
}

/// <summary>
/// Потери в застройке и растительности у концов трассы по ITU-R P.2108: поправка на окружение антенны (раздел 3.1)
/// и статистические потери для наземных трасс (раздел 3.2).
/// </summary>
/// <remarks>
/// <para>
/// Поправка на окружение антенны (30 МГц — 3 ГГц) отвечает на вопрос, сколько теряет антенна ниже крыш и крон.
/// Если антенна выше представительной высоты помех R, поправки нет. Иначе на воде и открытой местности сигнал
/// ослабевает как высотный множитель, −K_h2·lg(h/R), K_h2 = 21,8 + 6,2·lg f, а в застройке и лесу огибает край
/// крыши: J(ν) − 6,03 с ν = 0,342·√f·√(h_dif·θ_clut), где h_dif = R − h, θ_clut = arctg(h_dif/w_s) в градусах, w_s —
/// ширина улицы (27 м по умолчанию), J — приближение P.526 (<see cref="KnifeEdgeDiffraction.ItuApproximationLossDb"/>).
/// </para>
/// <para>
/// Статистическая модель (0,5–67 ГГц, трассы от 250 м) — для абонента в городе или пригороде, когда застройка
/// не описана поимённо: потери по трассе с прямой видимостью L_l и по улицам L_s складываются по мощности,
/// а разброс по местам — нормальный с σ, смешанным из 4 и 6 дБ. Потери не растут дальше 2 км.
/// </para>
/// <para>
/// Формулы и представительные высоты сверены с реализацией NTIA/ITS (github.com/NTIA/p2108).
/// </para>
/// </remarks>
public static class ItuP2108
{
    /// <summary>Ширина улицы по умолчанию, м</summary>
    public const double DefaultStreetWidthM = 27;

    /// <summary>Представительная высота помех R по умолчанию, м (таблица 3 P.2108)</summary>
    /// <param name="category">Класс помех</param>
    public static double RepresentativeHeightM(ClutterCategory category) => category switch
    {
        ClutterCategory.Urban or ClutterCategory.TreesForest => 15,
        ClutterCategory.DenseUrban => 20,
        _ => 10,
    };

    /// <summary>
    /// Высота помех для профиля трассы P.1812, м: на суше — R по P.2108, над водой — 0, препятствий там нет
    /// </summary>
    /// <param name="category">Класс помех</param>
    public static double ProfileClutterHeightM(ClutterCategory category)
        => category == ClutterCategory.WaterSea ? 0 : RepresentativeHeightM(category);

    /// <summary>Поправка на окружение антенны A_h, раздел 3.1, дБ; 0, если антенна выше помех</summary>
    /// <param name="frequencyHz">Частота, Гц, от 30 МГц до 3 ГГц</param>
    /// <param name="antennaHeightM">Высота антенны над землёй, м</param>
    /// <param name="category">Класс помех вокруг антенны</param>
    /// <param name="clutterHeightM">Высота помех R, м; null — по таблице</param>
    /// <param name="streetWidthM">Ширина улицы w_s, м</param>
    public static double HeightGainCorrectionDb(
        double frequencyHz,
        double antennaHeightM,
        ClutterCategory category,
        double? clutterHeightM = null,
        double streetWidthM = DefaultStreetWidthM)
    {
        double f = frequencyHz / 1e9;

        if (!(f >= 0.03 && f <= 3))
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), frequencyHz, "Поправка P.2108 на окружение антенны определена на 30 МГц — 3 ГГц");

        Guard.RequirePositive(antennaHeightM, nameof(antennaHeightM));
        Guard.RequirePositive(streetWidthM, nameof(streetWidthM));

        double r = clutterHeightM ?? RepresentativeHeightM(category);
        Guard.RequirePositive(r, nameof(clutterHeightM));

        if (antennaHeightM >= r)
            return 0;

        double difference = r - antennaHeightM;

        if (category is ClutterCategory.WaterSea or ClutterCategory.OpenRural)
            return -(21.8 + (6.2 * Math.Log10(f))) * Math.Log10(antennaHeightM / r);

        double angle = Math.Atan(difference / streetWidthM) * 180 / Math.PI;
        double nu = 0.342 * Math.Sqrt(f) * Math.Sqrt(difference * angle);

        return KnifeEdgeDiffraction.ItuApproximationLossDb(nu) - 6.03;
    }

    /// <summary>
    /// Статистические потери в застройке для наземной трассы, не превышаемые в <paramref name="locationPercent"/>
    /// процентах мест, раздел 3.2, дБ
    /// </summary>
    /// <param name="frequencyHz">Частота, Гц, от 0,5 до 67 ГГц</param>
    /// <param name="distanceM">Длина трассы, м, не меньше 250</param>
    /// <param name="locationPercent">Процент мест, от 0 до 100</param>
    public static double TerrestrialStatisticalLossDb(double frequencyHz, double distanceM, double locationPercent = 50)
    {
        RequireStatisticalFrequency(frequencyHz);

        if (!(distanceM >= 250) || double.IsInfinity(distanceM))
            throw new ArgumentOutOfRangeException(nameof(distanceM), distanceM, "Статистическая модель P.2108 определена для трасс от 250 м");

        if (!(locationPercent > 0 && locationPercent < 100))
            throw new ArgumentOutOfRangeException(nameof(locationPercent), locationPercent, "Процент мест лежит строго между 0 и 100");

        // Обратная дополнительная функция распределения: Q⁻¹(x) = Φ⁻¹(1 − x)
        double quantile = StatInference.NormalQuantile(1 - (locationPercent / 100));

        double Loss(double distanceKm)
        {
            (double median, double sigma) = Statistical(frequencyHz / 1e9, distanceKm);
            return median - (sigma * quantile);
        }

        // Потери не превышают значения на 2 км, формула (6)
        return Math.Min(Loss(2), Loss(distanceM / 1000));
    }

    /// <summary>
    /// Медиана и СКО статистических потерь в застройке, дБ, на расстоянии не дальше 2 км; ближе 250 м формула
    /// продолжена за пределы рекомендации
    /// </summary>
    internal static (double MedianDb, double SigmaDb) TerrestrialStatistical(double frequencyHz, double distanceM)
    {
        RequireStatisticalFrequency(frequencyHz);

        return Statistical(frequencyHz / 1e9, Math.Min(Math.Max(distanceM, PropagationGeometry.MinimumDistanceM), 2000) / 1000);
    }

    // Формулы (3)–(5): трасса с прямой видимостью L_l (σ = 4 дБ) и по улицам L_s (σ = 6 дБ)
    private static (double MedianDb, double SigmaDb) Statistical(double frequencyGHz, double distanceKm)
    {
        const double sigmaLine = 4, sigmaStreet = 6;

        double line = -2 * Math.Log10(Math.Pow(10, (-5 * Math.Log10(frequencyGHz)) - 12.5) + Math.Pow(10, -16.5));
        double street = 32.98 + (23.9 * Math.Log10(distanceKm)) + (3 * Math.Log10(frequencyGHz));
        double weightLine = Math.Pow(10, -0.2 * line), weightStreet = Math.Pow(10, -0.2 * street);

        double sigma = Math.Sqrt(((sigmaLine * sigmaLine * weightLine) + (sigmaStreet * sigmaStreet * weightStreet)) / (weightLine + weightStreet));

        return (-5 * Math.Log10(weightLine + weightStreet), sigma);
    }

    private static void RequireStatisticalFrequency(double frequencyHz)
    {
        if (!(frequencyHz >= 0.5e9 && frequencyHz <= 67e9))
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), frequencyHz, "Статистическая модель P.2108 определена на 0,5–67 ГГц");
    }
}

/// <summary>
/// Статистические потери в застройке P.2108 (раздел 3.2) поверх другой модели: медиана прибавляется к потерям,
/// разброс складывается с затенением в квадратуре.
/// </summary>
/// <remarks>
/// Годится для моделей без застройки — свободного пространства, логарифмической, рельефа. Сценарии TR 38.901 и
/// модель Хаты застройку уже содержат, и поправка поверх них посчитала бы её дважды.
/// </remarks>
public sealed class ClutterLossModel : IPropagationModel
{
    /// <summary>Создаёт модель</summary>
    /// <param name="inner">Модель без застройки</param>
    public ClutterLossModel(IPropagationModel inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        Inner = inner;
    }

    /// <summary>Модель без застройки</summary>
    public IPropagationModel Inner { get; }

    /// <summary>
    /// Обе антенны в застройке: поправка на трассах от 1 км прибавляется дважды, как разрешает P.2108
    /// </summary>
    public bool BothEnds { get; init; }

    /// <inheritdoc />
    public string Name => $"{Inner.Name} с застройкой P.2108";

    /// <inheritdoc />
    public double ShadowCorrelationDistanceM => Inner.ShadowCorrelationDistanceM;

    /// <inheritdoc />
    public double StateCorrelationDistanceM => Inner.StateCorrelationDistanceM;

    /// <inheritdoc />
    public LinkLoss Loss(Vector3 transmitter, Vector3 receiver, double frequencyHz)
    {
        LinkLoss inner = Inner.Loss(transmitter, receiver, frequencyHz);
        double distance = PropagationGeometry.Horizontal(transmitter, receiver);
        (double median, double sigma) = ItuP2108.TerrestrialStatistical(frequencyHz, distance);
        int ends = BothEnds && distance >= 1000 ? 2 : 1;

        double Combined(double s) => Math.Sqrt((s * s) + (ends * sigma * sigma));

        return new LinkLoss(
            inner.LineOfSightProbability,
            inner.LineOfSightDb + (ends * median),
            Combined(inner.LineOfSightSigmaDb),
            inner.NonLineOfSightDb + (ends * median),
            Combined(inner.NonLineOfSightSigmaDb));
    }
}
