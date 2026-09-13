using System.Numerics;

namespace AI.Microwave.Propagation;

/// <summary>Поляризация антенн относительно земли</summary>
public enum Polarization
{
    /// <summary>Горизонтальная: поле параллельно земле</summary>
    Horizontal,

    /// <summary>Вертикальная: поле в вертикальной плоскости трассы</summary>
    Vertical
}

/// <summary>
/// Двухлучевая модель: прямой луч и луч, отражённый от плоской земли.
/// </summary>
/// <remarks>
/// <para>
/// Лучи складываются с разностью хода <c>√(d² + (h₁+h₂)²) − √(d² + (h₁−h₂)²) ≈ 2h₁h₂/d</c>. Пока разность
/// больше полуволны, интерференция даёт чередование максимумов и провалов вокруг потерь свободного
/// пространства. За точкой перелома <c>d_c = 4h₁h₂/λ</c> разность меньше половины длины волны, отражённый
/// луч почти в противофазе с прямым, и мощность спадает как d⁻⁴: потери стремятся к
/// <c>40·lg d − 20·lg(h₁h₂)</c> и перестают зависеть от частоты.
/// </para>
/// <para>
/// Земля плоская и гладкая; кривизна Земли, шероховатость и поверхностная волна не учтены.
/// </para>
/// </remarks>
public static class TwoRayGround
{
    /// <summary>Канал из прямого и отражённого лучей</summary>
    /// <param name="distanceM">Горизонтальное расстояние, м</param>
    /// <param name="txHeightM">Высота передатчика над землёй, м</param>
    /// <param name="rxHeightM">Высота приёмника над землёй, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="ground">Материал земли</param>
    /// <param name="polarization">Поляризация антенн</param>
    public static MultipathChannel Channel(
        double distanceM, double txHeightM, double rxHeightM, double frequencyHz,
        RadioMaterial ground, Polarization polarization = Polarization.Vertical)
    {
        ArgumentNullException.ThrowIfNull(ground);
        Wave.RequirePositive(distanceM, nameof(distanceM));
        Wave.RequirePositive(txHeightM, nameof(txHeightM));
        Wave.RequirePositive(rxHeightM, nameof(rxHeightM));
        Wave.RequirePositive(frequencyHz, nameof(frequencyHz));

        double wavelength = Wave.SpeedOfLight / frequencyHz;
        double direct = Math.Sqrt((distanceM * distanceM) + ((txHeightM - rxHeightM) * (txHeightM - rxHeightM)));
        double reflected = Math.Sqrt((distanceM * distanceM) + ((txHeightM + rxHeightM) * (txHeightM + rxHeightM)));
        double grazing = Math.Atan((txHeightM + rxHeightM) / distanceM);

        ReflectionCoefficients coefficients = FresnelReflection.Coefficients(ground, frequencyHz, grazing);
        Complex gamma = polarization == Polarization.Horizontal ? coefficients.Perpendicular : coefficients.Parallel;
        double slope = Math.Atan((txHeightM - rxHeightM) / distanceM) * 180 / Math.PI;
        double grazingDeg = grazing * 180 / Math.PI;

        var lineOfSight = new PropagationPath(direct / Wave.SpeedOfLight, Wave.FreeSpaceGain(direct, wavelength), 0, PathKind.LineOfSight)
        {
            LengthMetres = direct,
            AzimuthOfDepartureDeg = 0,
            ElevationOfDepartureDeg = -slope,
            AzimuthOfArrivalDeg = 180,
            ElevationOfArrivalDeg = slope
        };

        var groundBounce = new PropagationPath(reflected / Wave.SpeedOfLight, gamma * Wave.FreeSpaceGain(reflected, wavelength), 0, PathKind.Reflection)
        {
            LengthMetres = reflected,
            Order = 1,
            AzimuthOfDepartureDeg = 0,
            ElevationOfDepartureDeg = -grazingDeg,
            AzimuthOfArrivalDeg = 180,
            ElevationOfArrivalDeg = -grazingDeg
        };

        return new MultipathChannel([lineOfSight, groundBounce], frequencyHz);
    }

    /// <summary>Потери на трассе по двухлучевой модели, дБ: минус узкополосное усиление</summary>
    /// <param name="distanceM">Горизонтальное расстояние, м</param>
    /// <param name="txHeightM">Высота передатчика, м</param>
    /// <param name="rxHeightM">Высота приёмника, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="ground">Материал земли</param>
    /// <param name="polarization">Поляризация</param>
    public static double PathLossDb(
        double distanceM, double txHeightM, double rxHeightM, double frequencyHz,
        RadioMaterial ground, Polarization polarization = Polarization.Vertical)
        => -Channel(distanceM, txHeightM, rxHeightM, frequencyHz, ground, polarization).NarrowbandGainDb;

    /// <summary>Точка перелома 4h₁h₂/λ, м: дальше потери растут на 40 дБ за декаду</summary>
    /// <param name="txHeightM">Высота передатчика, м</param>
    /// <param name="rxHeightM">Высота приёмника, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double BreakpointDistanceM(double txHeightM, double rxHeightM, double frequencyHz)
    {
        Wave.RequirePositive(frequencyHz, nameof(frequencyHz));

        return 4 * txHeightM * rxHeightM * frequencyHz / Wave.SpeedOfLight;
    }

    /// <summary>
    /// Асимптота плоской земли за точкой перелома: 40·lg d − 20·lg(h₁h₂), дБ
    /// </summary>
    /// <param name="distanceM">Расстояние, м</param>
    /// <param name="txHeightM">Высота передатчика, м</param>
    /// <param name="rxHeightM">Высота приёмника, м</param>
    public static double PlaneEarthLossDb(double distanceM, double txHeightM, double rxHeightM)
    {
        Wave.RequirePositive(distanceM, nameof(distanceM));
        Wave.RequirePositive(txHeightM, nameof(txHeightM));
        Wave.RequirePositive(rxHeightM, nameof(rxHeightM));

        return (40 * Math.Log10(distanceM)) - (20 * Math.Log10(txHeightM * rxHeightM));
    }
}
