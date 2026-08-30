using AI.Earth.Geodesy;
using AI.Insights;

namespace AI.Earth.Astronomy;

/// <summary>Положение светила на небе для наблюдателя</summary>
/// <param name="Altitude">Высота над горизонтом, градусы</param>
/// <param name="Azimuth">Азимут от севера по часовой стрелке, градусы</param>
/// <param name="Declination">Склонение, градусы</param>
/// <param name="HourAngle">Часовой угол, градусы</param>
public readonly record struct HorizontalPosition(double Altitude, double Azimuth, double Declination, double HourAngle)
{
    /// <summary>Находится ли светило над горизонтом</summary>
    public bool IsAboveHorizon => Altitude > 0;
}

/// <summary>Времена восхода, захода и полудня</summary>
/// <param name="Sunrise">Восход; <c>null</c> в полярный день или ночь</param>
/// <param name="Sunset">Заход; <c>null</c> в полярный день или ночь</param>
/// <param name="SolarNoon">Истинный полдень</param>
/// <param name="DayLengthHours">Продолжительность дня в часах</param>
/// <param name="IsPolarDay">Полярный день: светило не заходит</param>
/// <param name="IsPolarNight">Полярная ночь: светило не восходит</param>
public readonly record struct SunTimes(
    DateTime? Sunrise, DateTime? Sunset, DateTime SolarNoon,
    double DayLengthHours, bool IsPolarDay, bool IsPolarNight) : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Восход и заход Солнца")
            .Summary(IsPolarDay
                ? "Полярный день: Солнце не опускается за горизонт."
                : IsPolarNight
                    ? "Полярная ночь: Солнце не поднимается над горизонтом."
                    : $"Восход в {Sunrise:HH:mm} UTC, заход в {Sunset:HH:mm} UTC, "
                      + $"истинный полдень в {SolarNoon:HH:mm} UTC. Долгота дня "
                      + $"{Fmt.Num(DayLengthHours, 2)} часа.")
            .Metric("Долгота дня", Fmt.Num(DayLengthHours, 2), "ч", "от восхода до захода")
            .Metric("Истинный полдень", SolarNoon.ToString("HH:mm:ss"), "UTC",
                "момент наибольшей высоты Солнца")
            .FindingIf(!IsPolarDay && !IsPolarNight,
                "Истинный полдень не совпадает с двенадцатью часами: сдвиг складывается из долготы "
                + "относительно осевого меридиана пояса и уравнения времени, доходящего до четверти часа.")
            .FindingIf(IsPolarDay || IsPolarNight,
                "За полярным кругом сутки перестают делиться на день и ночь, и понятия восхода "
                + "и захода теряют смысл на недели и месяцы.")
            .Warning("Расчёт ведётся для видимого центра диска с поправкой на рефракцию в 0.833°. "
                + "Действительный момент зависит от состояния атмосферы и высоты наблюдателя "
                + "и отличается на минуту-другую.")
            .Build();
}

/// <summary>
/// Положение Солнца и времена восхода и захода.
/// </summary>
/// <remarks>
/// <para>
/// Используются формулы низкой точности: склонение и уравнение времени вычисляются
/// по средним элементам орбиты без учёта возмущений от Луны и планет. Погрешность
/// по положению — около сотой доли градуса, по времени восхода — около минуты.
/// Для навигации и астрометрии этого мало, для расчёта освещённости и энергетики — достаточно.
/// </para>
/// <para>
/// Орбитальная механика в общем виде — законы Кеплера, скорости, переходы — живёт
/// в <c>AI.Physics.Mechanics.Orbits</c> и здесь не повторяется.
/// </para>
/// </remarks>
public static class SolarPosition
{
    private const double Degree = Math.PI / 180.0;

    /// <summary>Поправка на рефракцию и радиус диска при восходе, градусы</summary>
    public const double RefractionCorrection = 0.833;

    /// <summary>Наклон эклиптики к экватору, градусы</summary>
    public const double Obliquity = 23.4392911;

    /// <summary>
    /// Склонение Солнца — угол между направлением на светило и плоскостью экватора
    /// </summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static double Declination(DateTime moment)
    {
        double lambda = EclipticLongitude(moment) * Degree;

        return Math.Asin(Math.Sin(Obliquity * Degree) * Math.Sin(lambda)) / Degree;
    }

    /// <summary>
    /// Уравнение времени в минутах: насколько истинное солнечное время опережает среднее
    /// </summary>
    /// <param name="moment">Момент в шкале UTC</param>
    /// <remarks>
    /// Складывается из двух причин: орбита Земли эллиптична, и ось наклонена. Их сумма
    /// доходит до плюс шестнадцати минут в ноябре и минус четырнадцати в феврале —
    /// именно поэтому солнечные часы расходятся с обычными.
    /// </remarks>
    public static double EquationOfTime(DateTime moment)
    {
        double t = AstronomicalTime.CenturiesSinceJ2000(moment);
        double meanLongitude = Normalize(280.46646 + (36000.76983 * t) + (0.0003032 * t * t));
        double meanAnomaly = Normalize(357.52911 + (35999.05029 * t) - (0.0001537 * t * t)) * Degree;

        double eccentricity = 0.016708634 - (0.000042037 * t) - (0.0000001267 * t * t);
        double epsilon = (Obliquity - (0.0130042 * t)) * Degree;
        double y = Math.Tan(epsilon / 2) * Math.Tan(epsilon / 2);

        double l = meanLongitude * Degree;

        double minutes = 4 / Degree * (
            (y * Math.Sin(2 * l))
            - (2 * eccentricity * Math.Sin(meanAnomaly))
            + (4 * eccentricity * y * Math.Sin(meanAnomaly) * Math.Cos(2 * l))
            - (0.5 * y * y * Math.Sin(4 * l))
            - (1.25 * eccentricity * eccentricity * Math.Sin(2 * meanAnomaly)));

        return minutes;
    }

    /// <summary>
    /// Положение Солнца на небе для наблюдателя
    /// </summary>
    /// <param name="moment">Момент в шкале UTC</param>
    /// <param name="observer">Положение наблюдателя</param>
    public static HorizontalPosition Position(DateTime moment, GeoPoint observer)
    {
        double declination = Declination(moment);
        double hourAngle = HourAngle(moment, observer.Longitude);

        double latitude = observer.LatitudeRadians;
        double dec = declination * Degree;
        double ha = hourAngle * Degree;

        double altitude = Math.Asin(
            (Math.Sin(latitude) * Math.Sin(dec)) + (Math.Cos(latitude) * Math.Cos(dec) * Math.Cos(ha)));

        double azimuth = Math.Atan2(
            -Math.Sin(ha),
            (Math.Tan(dec) * Math.Cos(latitude)) - (Math.Sin(latitude) * Math.Cos(ha)));

        return new HorizontalPosition(
            altitude / Degree,
            Normalize(azimuth / Degree),
            declination,
            hourAngle);
    }

    /// <summary>
    /// Времена восхода, захода и истинного полудня
    /// </summary>
    /// <param name="date">Дата</param>
    /// <param name="observer">Положение наблюдателя</param>
    /// <param name="horizonAngle">
    /// Угол погружения под горизонт: 0.833° — обычный восход, 6° — гражданские сумерки,
    /// 12° — навигационные, 18° — астрономические
    /// </param>
    public static SunTimes Times(DateTime date, GeoPoint observer, double horizonAngle = RefractionCorrection)
    {
        var noonUtc = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);

        double declination = Declination(noonUtc) * Degree;
        double latitude = observer.LatitudeRadians;

        double cosHourAngle = (Math.Cos((90 + horizonAngle) * Degree)
            - (Math.Sin(latitude) * Math.Sin(declination)))
            / (Math.Cos(latitude) * Math.Cos(declination));

        double equation = EquationOfTime(noonUtc);
        double noonMinutes = (720 - (4 * observer.Longitude) - equation) / 60.0;
        DateTime solarNoon = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddHours(noonMinutes);

        if (cosHourAngle < -1)
            return new SunTimes(null, null, solarNoon, 24, true, false);

        if (cosHourAngle > 1)
            return new SunTimes(null, null, solarNoon, 0, false, true);

        double hourAngle = Math.Acos(cosHourAngle) / Degree;
        double halfDay = hourAngle / 15.0;

        return new SunTimes(
            solarNoon.AddHours(-halfDay),
            solarNoon.AddHours(halfDay),
            solarNoon,
            2 * halfDay,
            false,
            false);
    }

    /// <summary>Часовой угол Солнца в градусах: нуль в истинный полдень</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    /// <param name="longitude">Долгота наблюдателя</param>
    public static double HourAngle(DateTime moment, double longitude)
    {
        double minutes = (moment.TimeOfDay.TotalMinutes + EquationOfTime(moment) + (4 * longitude)) % 1440;

        return (minutes / 4) - 180;
    }

    /// <summary>Эклиптическая долгота Солнца в градусах</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static double EclipticLongitude(DateTime moment)
    {
        double t = AstronomicalTime.CenturiesSinceJ2000(moment);

        double meanLongitude = Normalize(280.46646 + (36000.76983 * t) + (0.0003032 * t * t));
        double meanAnomaly = Normalize(357.52911 + (35999.05029 * t) - (0.0001537 * t * t)) * Degree;

        double centre = ((1.914602 - (0.004817 * t) - (0.000014 * t * t)) * Math.Sin(meanAnomaly))
            + ((0.019993 - (0.000101 * t)) * Math.Sin(2 * meanAnomaly))
            + (0.000289 * Math.Sin(3 * meanAnomaly));

        return Normalize(meanLongitude + centre);
    }

    private static double Normalize(double degrees)
    {
        double value = degrees % 360;

        return value < 0 ? value + 360 : value;
    }
}
