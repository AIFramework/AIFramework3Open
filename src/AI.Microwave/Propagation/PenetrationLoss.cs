namespace AI.Microwave.Propagation;

/// <summary>Где находится абонент относительно здания или машины</summary>
public enum TerminalEnvironment
{
    /// <summary>На улице</summary>
    Outdoor,

    /// <summary>В здании с малыми потерями: обычное стекло, 30 % фасада — стекло</summary>
    IndoorLowLoss,

    /// <summary>В здании с большими потерями: энергосберегающее стекло, 70 % фасада — стекло</summary>
    IndoorHighLoss,

    /// <summary>В машине</summary>
    InCar
}

/// <summary>
/// Потери на проникновение сигнала с улицы в здание или машину по TR 38.901 v16.1, раздел 7.4.3.
/// </summary>
/// <remarks>
/// <para>
/// Потери материалов фасада растут с частотой (таблица 7.4.3-1, f в ГГц): обычное стекло 2 + 0,2·f, стекло
/// с отражающим покрытием 23 + 0,3·f, бетон 5 + 4·f, дерево 4,85 + 0,12·f. Стена складывается из стекла и бетона
/// по мощности: <c>5 − 10·lg(a·10^(−L_стекла/10) + (1 − a)·10^(−L_бетона/10))</c>, a = 0,3 для здания с малыми
/// потерями и 0,7 (энергосберегающее стекло) для здания с большими. Внутри к ним добавляется 0,5 дБ на метр
/// от стены (таблица 7.4.3-2), разброс — 4,4 и 6,5 дБ.
/// </para>
/// <para>
/// Машина — нормальная величина со средним 9 дБ и СКО 5 дБ на 0,6–60 ГГц (раздел 7.4.3.2).
/// </para>
/// </remarks>
public static class PenetrationLoss
{
    /// <summary>
    /// Среднее расстояние от стены до абонента в UMa и UMi, м: минимум двух равномерных величин на 0–25 м, 25/3
    /// </summary>
    public const double MeanIndoorDistanceM = 25.0 / 3;

    /// <summary>Потери в обычном стекле, дБ</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double StandardGlassDb(double frequencyHz) => 2 + (0.2 * GHz(frequencyHz));

    /// <summary>Потери в стекле с отражающим инфракрасное покрытием, дБ</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double CoatedGlassDb(double frequencyHz) => 23 + (0.3 * GHz(frequencyHz));

    /// <summary>Потери в бетоне, дБ</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double ConcreteDb(double frequencyHz) => 5 + (4 * GHz(frequencyHz));

    /// <summary>Потери в дереве, дБ</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double WoodDb(double frequencyHz) => 4.85 + (0.12 * GHz(frequencyHz));

    /// <summary>Средние потери на проникновение, дБ</summary>
    /// <param name="environment">Где абонент</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="indoorDistanceM">Расстояние от стены внутрь здания, м</param>
    public static double LossDb(TerminalEnvironment environment, double frequencyHz, double indoorDistanceM = MeanIndoorDistanceM)
    {
        Guard.RequirePositive(frequencyHz, nameof(frequencyHz));

        if (!(indoorDistanceM >= 0) || double.IsInfinity(indoorDistanceM))
            throw new ArgumentOutOfRangeException(nameof(indoorDistanceM), indoorDistanceM, "Расстояние внутрь здания — конечное неотрицательное число");

        return environment switch
        {
            TerminalEnvironment.IndoorLowLoss => Wall(0.3, StandardGlassDb(frequencyHz), ConcreteDb(frequencyHz)) + (0.5 * indoorDistanceM),
            TerminalEnvironment.IndoorHighLoss => Wall(0.7, CoatedGlassDb(frequencyHz), ConcreteDb(frequencyHz)) + (0.5 * indoorDistanceM),
            TerminalEnvironment.InCar => 9,
            _ => 0,
        };
    }

    /// <summary>СКО потерь на проникновение, дБ</summary>
    /// <param name="environment">Где абонент</param>
    public static double SigmaDb(TerminalEnvironment environment) => environment switch
    {
        TerminalEnvironment.IndoorLowLoss => 4.4,
        TerminalEnvironment.IndoorHighLoss => 6.5,
        TerminalEnvironment.InCar => 5,
        _ => 0,
    };

    private static double Wall(double glassShare, double glassDb, double concreteDb)
        => 5 - (10 * Math.Log10((glassShare * Math.Pow(10, -glassDb / 10)) + ((1 - glassShare) * Math.Pow(10, -concreteDb / 10))));

    private static double GHz(double frequencyHz) => frequencyHz / 1e9;
}
