using System.Numerics;

namespace AI.Microwave.Propagation;

/// <summary>Происхождение пути распространения</summary>
public enum PathKind
{
    /// <summary>Прямая видимость</summary>
    LineOfSight,

    /// <summary>Зеркальное отражение от поверхностей</summary>
    Reflection,

    /// <summary>Дифракция на крае препятствия</summary>
    Diffraction,

    /// <summary>Рассеянная составляющая стохастической модели</summary>
    Scattered
}

/// <summary>
/// Один путь многолучевого канала: задержка, комплексная амплитуда, углы и доплеровский сдвиг.
/// </summary>
/// <remarks>
/// <para>
/// Амплитуда — отношение напряжений в комплексной огибающей относительно несущей: для прямого
/// луча в свободном пространстве это <c>λ/(4πd)·e^(−j2πd/λ)</c>, так что квадрат модуля — потери на трассе
/// в разах, а фаза — набег на несущей. Временная зависимость принята <c>e^(jωt)</c>.
/// </para>
/// <para>
/// Азимут отсчитывается от оси X против часовой стрелки при взгляде сверху, угол места — от горизонтали
/// вверх. Угол прихода — направление, откуда волна пришла к приёмнику; угол ухода — куда она ушла
/// от передатчика.
/// </para>
/// </remarks>
/// <param name="DelaySeconds">Задержка распространения, с</param>
/// <param name="Gain">Комплексная амплитуда</param>
/// <param name="DopplerHz">Доплеровский сдвиг, Гц: положителен, когда путь укорачивается</param>
/// <param name="Kind">Происхождение пути</param>
public sealed record PropagationPath(double DelaySeconds, Complex Gain, double DopplerHz = 0, PathKind Kind = PathKind.Scattered)
{
    /// <summary>Азимут прихода, градусы</summary>
    public double AzimuthOfArrivalDeg { get; init; }

    /// <summary>Угол места прихода, градусы</summary>
    public double ElevationOfArrivalDeg { get; init; }

    /// <summary>Азимут ухода, градусы</summary>
    public double AzimuthOfDepartureDeg { get; init; }

    /// <summary>Угол места ухода, градусы</summary>
    public double ElevationOfDepartureDeg { get; init; }

    /// <summary>Число взаимодействий с поверхностями: 0 у прямого луча</summary>
    public int Order { get; init; }

    /// <summary>Длина пути, м; NaN, если путь задан без геометрии</summary>
    public double LengthMetres { get; init; } = double.NaN;

    /// <summary>Мощность пути в разах: квадрат модуля амплитуды</summary>
    public double Power => (Gain.Real * Gain.Real) + (Gain.Imaginary * Gain.Imaginary);

    /// <summary>Мощность пути, дБ</summary>
    public double PowerDb => 10 * Math.Log10(Power);

    /// <summary>Тот же путь в момент t: фаза повёрнута на доплеровский набег 2π·f_D·t</summary>
    /// <param name="timeSeconds">Момент времени, с</param>
    public PropagationPath At(double timeSeconds)
        => this with { Gain = Gain * Complex.FromPolarCoordinates(1, 2 * Math.PI * DopplerHz * timeSeconds) };
}
