using System.Numerics;
using AI.HighLevelFunctions;

namespace AI.Microwave.Propagation;

/// <summary>
/// Дифракция на остром крае — хребте, крыше, краю здания (ITU-R P.526).
/// </summary>
/// <remarks>
/// <para>
/// Препятствие описывается одним числом — параметром Френеля — Кирхгофа
/// <c>ν = h·√(2(d₁ + d₂)/(λ·d₁·d₂))</c>, где h — высота края над линией прямой видимости (отрицательная, если
/// край ниже линии). Поле за краем относительно свободного пространства
/// <c>F(ν) = ((1 + j)/2)·∫_ν^∞ e^(−jπt²/2) dt</c> выражается через интегралы Френеля; при ν = 0, когда край
/// касается линии, ослабление ровно 6,02 дБ, а просвет в первую зону Френеля (ν ≈ −0,8) уже почти
/// ничего не отнимает.
/// </para>
/// <para>
/// Точное значение считается по интегралам Френеля из ядра; приближение рекомендации
/// <c>J(ν) = 6,9 + 20·lg(√((ν − 0,1)² + 1) + ν − 0,1)</c> дано для сравнения с расчётами по ней. Край считается
/// бесконечно тонким и бесконечно длинным; округлость вершины и несколько краёв подряд не учтены.
/// </para>
/// </remarks>
public static class KnifeEdgeDiffraction
{
    /// <summary>Параметр Френеля — Кирхгофа ν</summary>
    /// <param name="clearanceM">Высота края над линией прямой видимости, м; отрицательна при просвете</param>
    /// <param name="d1">Расстояние от передатчика до края, м</param>
    /// <param name="d2">Расстояние от края до приёмника, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double FresnelParameter(double clearanceM, double d1, double d2, double frequencyHz)
    {
        Wave.RequirePositive(d1, nameof(d1));
        Wave.RequirePositive(d2, nameof(d2));
        Wave.RequirePositive(frequencyHz, nameof(frequencyHz));

        double wavelength = Wave.SpeedOfLight / frequencyHz;

        return clearanceM * Math.Sqrt(2 * (d1 + d2) / (wavelength * d1 * d2));
    }

    /// <summary>Комплексное поле за краем относительно свободного пространства F(ν)</summary>
    /// <param name="nu">Параметр Френеля — Кирхгофа</param>
    public static Complex Coefficient(double nu)
    {
        (double c, double s) = SpecialFunctions.Fresnel(nu);

        return new Complex(0.5, 0.5) * new Complex(0.5 - c, -(0.5 - s));
    }

    /// <summary>Ослабление за краем относительно свободного пространства, дБ</summary>
    /// <param name="nu">Параметр Френеля — Кирхгофа</param>
    public static double LossDb(double nu) => -20 * Math.Log10(Coefficient(nu).Magnitude);

    /// <summary>Приближение ITU-R P.526 для ослабления, дБ; ноль при ν ≤ −0,78</summary>
    /// <param name="nu">Параметр Френеля — Кирхгофа</param>
    public static double ItuApproximationLossDb(double nu)
        => nu <= -0.78 ? 0 : 6.9 + (20 * Math.Log10(Math.Sqrt(((nu - 0.1) * (nu - 0.1)) + 1) + nu - 0.1));

    /// <summary>
    /// Путь через край для многолучевого канала: амплитуда прямого луча, умноженная на F(ν)
    /// </summary>
    /// <param name="txHeightM">Высота передатчика, м</param>
    /// <param name="rxHeightM">Высота приёмника, м</param>
    /// <param name="edgeHeightM">Высота края, м</param>
    /// <param name="d1">Горизонтальное расстояние от передатчика до края, м</param>
    /// <param name="d2">Горизонтальное расстояние от края до приёмника, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <remarks>
    /// Задержка — по ломаной через вершину края. Фаза берётся из F(ν) относительно прямого луча — это
    /// верно для узкополосного сигнала; в широкой полосе F зависит от частоты, и одним путём дифракция
    /// описывается лишь приближённо.
    /// </remarks>
    public static PropagationPath Path(double txHeightM, double rxHeightM, double edgeHeightM, double d1, double d2, double frequencyHz)
    {
        Wave.RequirePositive(d1, nameof(d1));
        Wave.RequirePositive(d2, nameof(d2));
        Wave.RequirePositive(frequencyHz, nameof(frequencyHz));

        double wavelength = Wave.SpeedOfLight / frequencyHz;
        double lineHeight = txHeightM + ((rxHeightM - txHeightM) * d1 / (d1 + d2));
        double nu = FresnelParameter(edgeHeightM - lineHeight, d1, d2, frequencyHz);
        double direct = Math.Sqrt(((d1 + d2) * (d1 + d2)) + ((rxHeightM - txHeightM) * (rxHeightM - txHeightM)));
        double viaEdge = Math.Sqrt((d1 * d1) + ((edgeHeightM - txHeightM) * (edgeHeightM - txHeightM)))
            + Math.Sqrt((d2 * d2) + ((edgeHeightM - rxHeightM) * (edgeHeightM - rxHeightM)));

        return new PropagationPath(viaEdge / Wave.SpeedOfLight, Coefficient(nu) * Wave.FreeSpaceGain(direct, wavelength), 0, PathKind.Diffraction)
        {
            LengthMetres = viaEdge,
            Order = 1,
            AzimuthOfDepartureDeg = 0,
            ElevationOfDepartureDeg = Math.Atan2(edgeHeightM - txHeightM, d1) * 180 / Math.PI,
            AzimuthOfArrivalDeg = 180,
            ElevationOfArrivalDeg = Math.Atan2(edgeHeightM - rxHeightM, d2) * 180 / Math.PI
        };
    }
}
