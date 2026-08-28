namespace AI.Microwave.Physics;

/// <summary>
/// Апертура, облучаемая из фокуса (зеркало или линза): связь спадания поля
/// к краю апертуры с КИП, коэффициентом перехвата, шириной луча и уровнем
/// боковых лепестков.
/// </summary>
/// <remarks>
/// ДН облучателя аппроксимируется гауссовой, то есть спад в децибелах
/// T(theta) = 12 (theta / theta_0.5)^2. Все величины ниже оказываются
/// функциями одного аргумента - спада на краю T, что и позволяет свести
/// проектирование зеркала и линзы к общему коду.
/// <para>
/// Раньше КИП, перехват и УБЛ были константами (0.55, 0.95, -26 дБ),
/// не зависящими от геометрии: проверка соответствия требованию по УБЛ
/// давала один и тот же ответ при любом раскрыве.
/// </para>
/// </remarks>
public static class ApertureIllumination
{
    /// <summary>Классический компромиссный спад на краю апертуры, дБ.</summary>
    public const double DefaultEdgeTaperDb = 10.0;

    /// <summary>Спад поля облучателя на угле <paramref name="offAxisDeg"/> от оси, дБ.</summary>
    public static double EdgeTaperDb(double offAxisDeg, double feedHpbwDeg)
    {
        if (feedHpbwDeg <= 0) return 0.0;
        double r = offAxisDeg / feedHpbwDeg;
        return 12.0 * r * r;
    }

    /// <summary>
    /// Ширина луча облучателя, при которой на угле <paramref name="offAxisDeg"/>
    /// достигается заданный спад, град.
    /// </summary>
    public static double FeedBeamwidthForTaper(double offAxisDeg, double edgeTaperDb)
        => edgeTaperDb <= 0
            ? double.PositiveInfinity
            : offAxisDeg / Math.Sqrt(edgeTaperDb / 12.0);

    /// <summary>
    /// КИП по спаданию (taper efficiency) круглой апертуры:
    /// eta_t = 2 (1-e^-b)^2 / (b (1-e^-2b)), где b = T ln10 / 20.
    /// </summary>
    public static double TaperEfficiency(double edgeTaperDb)
    {
        double beta = Math.Max(edgeTaperDb, 1e-9) * Math.Log(10.0) / 20.0;
        double e1 = Math.Exp(-beta);
        double e2 = Math.Exp(-2.0 * beta);
        return 2.0 * (1.0 - e1) * (1.0 - e1) / (beta * (1.0 - e2));
    }

    /// <summary>
    /// Коэффициент перехвата: доля мощности облучателя, попавшая на апертуру.
    /// eta_s = 1 - exp(-4 ln2 T / 12).
    /// </summary>
    public static double SpilloverEfficiency(double edgeTaperDb)
        => 1.0 - Math.Exp(-4.0 * Math.Log(2.0) * Math.Max(edgeTaperDb, 0.0) / 12.0);

    /// <summary>
    /// Уровень первого бокового лепестка круглой апертуры, дБ. Аппроксимация
    /// табличных данных: спад 0 дБ (равномерная апертура) даёт -17.6 дБ,
    /// 10 дБ даёт -24.1 дБ, 20 дБ даёт -30.6 дБ.
    /// </summary>
    public static double SidelobeLevelDb(double edgeTaperDb)
        => -(17.6 + 0.65 * Math.Clamp(edgeTaperDb, 0.0, 25.0));

    /// <summary>
    /// Коэффициент k в theta_0.5 = k lambda / D для круглой апертуры:
    /// 58.4 при равномерном облучении, 70 при классическом спаде -10 дБ.
    /// </summary>
    public static double BeamwidthFactor(double edgeTaperDb)
        => 58.4 + 1.16 * Math.Clamp(edgeTaperDb, 0.0, 25.0);

    /// <summary>
    /// Ухудшение УБЛ затенением апертуры: затеняющий диск снимает с апертуры
    /// равномерную составляющую, добавляя к боковому лепестку пьедестал
    /// амплитудой, равной коэффициенту затенения по мощности.
    /// </summary>
    public static double SidelobeWithBlockageDb(double sidelobeDb, double blockageRatio)
    {
        double level = 20.0 * Math.Log10(
            Math.Pow(10.0, sidelobeDb / 20.0) + Math.Clamp(blockageRatio, 0.0, 1.0));

        // Выше -3 дБ говорить о боковом лепестке уже нельзя: это не лепесток,
        // а развал диаграммы. Ограничение не даёт вернуть положительный УБЛ
        // при вырожденной геометрии, когда облучатель сравним с зеркалом.
        return Math.Min(level, -3.0);
    }
}
