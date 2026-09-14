#nullable enable
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.Ballistics;

/// <summary>
/// Выстрел или бросок точечного снаряда; все величины в СИ. Оси: x на восток, y на север, z вверх; начало
/// координат на земле под точкой выстрела. Азимут отсчитывается от севера по часовой стрелке, как на карте
/// </summary>
/// <param name="Speed">Начальная скорость относительно земли, м/с</param>
/// <param name="ElevationDegrees">Угол возвышения над горизонтом, градусы</param>
public sealed record BallisticLaunch(double Speed, double ElevationDegrees)
{
    /// <summary>Плотность воздуха по умолчанию: стандартная атмосфера у уровня моря, кг/м³</summary>
    public const double SeaLevelDensity = 1.225;

    /// <summary>Скорость звука по умолчанию: стандартная атмосфера у уровня моря, м/с</summary>
    public const double SeaLevelSpeedOfSound = 340.294;

    /// <summary>Азимут, градусы от севера по часовой стрелке</summary>
    public double AzimuthDegrees { get; init; }

    /// <summary>Высота точки выстрела над землей, м</summary>
    public double Height { get; init; }

    /// <summary>Высота земли над уровнем моря, м: от нее считаются плотность воздуха и скорость звука</summary>
    public double GroundAltitude { get; init; }

    /// <summary>Масса снаряда, кг</summary>
    public double Mass { get; init; } = 1;

    /// <summary>Площадь миделя, к которой отнесен коэффициент сопротивления, м²</summary>
    public double ReferenceArea { get; init; }

    /// <summary>Закон сопротивления; по умолчанию сопротивления нет</summary>
    public DragModel Drag { get; init; } = DragModel.None;

    /// <summary>Ветер относительно земли, м/с; сопротивление считается по скорости снаряда относительно воздуха</summary>
    public Vector3 Wind { get; init; }

    /// <summary>
    /// Плотность воздуха, кг/м³, как функция высоты над уровнем моря, м. Не задана: постоянная
    /// <see cref="SeaLevelDensity"/>
    /// </summary>
    public Func<double, double>? AirDensity { get; init; }

    /// <summary>
    /// Скорость звука, м/с, как функция высоты над уровнем моря, м; нужна для числа Маха. Не задана: постоянная
    /// <see cref="SeaLevelSpeedOfSound"/>
    /// </summary>
    public Func<double, double>? SpeedOfSound { get; init; }

    /// <summary>Ускорение свободного падения, м/с², постоянное и направленное против оси z</summary>
    public double Gravity { get; init; } = 9.80665;

    /// <summary>Предельное время полета, с: не упавший к этому времени снаряд считается не упавшим</summary>
    public double MaxFlightTime { get; init; } = 3600;
}
