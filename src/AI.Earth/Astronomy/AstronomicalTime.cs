namespace AI.Earth.Astronomy;

/// <summary>
/// Астрономические шкалы времени: юлианская дата и звёздное время.
/// </summary>
/// <remarks>
/// <para>
/// Гражданский календарь непригоден для расчётов: месяцы разной длины, високосные годы,
/// смена стиля в 1582 году. Юлианская дата — непрерывный счёт суток от одной эпохи,
/// и разность двух дат в ней есть просто число суток.
/// </para>
/// <para>
/// Разница между шкалами UT1 и UTC (до 0.9 секунды) и поправка на динамическое время здесь
/// не учитываются: для расчёта положения Солнца с точностью до минуты это несущественно,
/// для точной астрометрии — существенно.
/// </para>
/// </remarks>
public static class AstronomicalTime
{
    /// <summary>Юлианская дата эпохи J2000.0 — полдень 1 января 2000 года</summary>
    public const double J2000 = 2451545.0;

    /// <summary>Юлианская дата момента времени</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static double JulianDate(DateTime moment)
    {
        DateTime utc = moment.Kind == DateTimeKind.Utc ? moment : moment.ToUniversalTime();

        int year = utc.Year;
        int month = utc.Month;

        if (month <= 2)
        {
            year--;
            month += 12;
        }

        int a = year / 100;
        int b = 2 - a + (a / 4);

        double day = utc.Day
            + (utc.Hour / 24.0)
            + (utc.Minute / 1440.0)
            + ((utc.Second + (utc.Millisecond / 1000.0)) / 86400.0);

        return Math.Floor(365.25 * (year + 4716))
            + Math.Floor(30.6001 * (month + 1))
            + day + b - 1524.5;
    }

    /// <summary>Модифицированная юлианская дата: отсчёт от полуночи 17 ноября 1858 года</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static double ModifiedJulianDate(DateTime moment) => JulianDate(moment) - 2400000.5;

    /// <summary>Момент времени по юлианской дате</summary>
    /// <param name="julianDate">Юлианская дата</param>
    public static DateTime ToDateTime(double julianDate)
    {
        double shifted = julianDate + 0.5;
        int integer = (int)Math.Floor(shifted);
        double fraction = shifted - integer;

        int a = integer;

        if (integer >= 2299161)
        {
            int alpha = (int)Math.Floor((integer - 1867216.25) / 36524.25);
            a = integer + 1 + alpha - (alpha / 4);
        }

        int b = a + 1524;
        int c = (int)Math.Floor((b - 122.1) / 365.25);
        int d = (int)Math.Floor(365.25 * c);
        int e = (int)Math.Floor((b - d) / 30.6001);

        int day = b - d - (int)Math.Floor(30.6001 * e);
        int month = e < 14 ? e - 1 : e - 13;
        int year = month > 2 ? c - 4716 : c - 4715;

        double hours = fraction * 24;
        int hour = (int)hours;
        double minutes = (hours - hour) * 60;
        int minute = (int)minutes;
        double seconds = (minutes - minute) * 60;

        return new DateTime(year, month, day, hour, minute, (int)Math.Round(seconds) % 60, DateTimeKind.Utc);
    }

    /// <summary>Число юлианских столетий от эпохи J2000.0</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static double CenturiesSinceJ2000(DateTime moment) => (JulianDate(moment) - J2000) / 36525.0;

    /// <summary>
    /// Гринвичское среднее звёздное время в градусах
    /// </summary>
    /// <param name="moment">Момент в шкале UTC</param>
    /// <remarks>
    /// Звёздные сутки короче солнечных примерно на четыре минуты: за сутки Земля успевает
    /// сместиться по орбите, и чтобы Солнце вновь оказалось на юге, ей нужно довернуться.
    /// </remarks>
    public static double GreenwichSiderealTime(DateTime moment)
    {
        double julian = JulianDate(moment);
        double t = (julian - J2000) / 36525.0;

        double degrees = 280.46061837
            + (360.98564736629 * (julian - J2000))
            + (0.000387933 * t * t)
            - (t * t * t / 38710000.0);

        return ((degrees % 360) + 360) % 360;
    }

    /// <summary>Местное звёздное время в градусах</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    /// <param name="longitude">Долгота в градусах, положительная к востоку</param>
    public static double LocalSiderealTime(DateTime moment, double longitude)
    {
        double local = GreenwichSiderealTime(moment) + longitude;

        return ((local % 360) + 360) % 360;
    }

    /// <summary>Номер дня в году</summary>
    /// <param name="moment">Дата</param>
    public static int DayOfYear(DateTime moment) => moment.DayOfYear;
}
