namespace AI.Earth.Astronomy;

/// <summary>Фаза Луны</summary>
public enum MoonPhaseName
{
    /// <summary>Новолуние</summary>
    New,

    /// <summary>Растущий серп</summary>
    WaxingCrescent,

    /// <summary>Первая четверть</summary>
    FirstQuarter,

    /// <summary>Растущая Луна</summary>
    WaxingGibbous,

    /// <summary>Полнолуние</summary>
    Full,

    /// <summary>Убывающая Луна</summary>
    WaningGibbous,

    /// <summary>Последняя четверть</summary>
    LastQuarter,

    /// <summary>Убывающий серп</summary>
    WaningCrescent
}

/// <summary>Состояние Луны на заданный момент</summary>
/// <param name="Age">Возраст в сутках от новолуния</param>
/// <param name="Phase">Доля цикла от нуля до единицы</param>
/// <param name="Illumination">Освещённая доля диска</param>
/// <param name="Name">Название фазы</param>
public readonly record struct MoonState(double Age, double Phase, double Illumination, MoonPhaseName Name)
{
    /// <summary>Растёт ли Луна</summary>
    public bool IsWaxing => Phase < 0.5;
}

/// <summary>
/// Фазы Луны.
/// </summary>
/// <remarks>
/// <para>
/// Расчёт ведётся по среднему синодическому месяцу от известного новолуния. Действительные
/// новолуния отклоняются от среднего на несколько часов из-за эллиптичности лунной орбиты
/// и возмущений от Солнца, поэтому возраст Луны здесь верен примерно до полусуток.
/// </para>
/// <para>
/// Положение Луны на небе и точные моменты фаз требуют теории движения с сотнями членов —
/// её здесь нет.
/// </para>
/// </remarks>
public static class Moon
{
    /// <summary>Средний синодический месяц в сутках — период смены фаз</summary>
    public const double SynodicMonth = 29.530588853;

    /// <summary>Юлианская дата новолуния 6 января 2000 года, принятая за начало отсчёта</summary>
    public const double ReferenceNewMoon = 2451550.09766;

    /// <summary>Состояние Луны на заданный момент</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static MoonState State(DateTime moment)
    {
        double julian = AstronomicalTime.JulianDate(moment);
        double cycles = (julian - ReferenceNewMoon) / SynodicMonth;
        double phase = cycles - Math.Floor(cycles);

        double age = phase * SynodicMonth;

        // Освещённая доля меняется по косинусу фазового угла
        double illumination = (1 - Math.Cos(2 * Math.PI * phase)) / 2;

        return new MoonState(age, phase, illumination, Name(phase));
    }

    /// <summary>Ближайшее новолуние после заданного момента</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static DateTime NextNewMoon(DateTime moment)
    {
        double julian = AstronomicalTime.JulianDate(moment);
        double cycles = Math.Ceiling((julian - ReferenceNewMoon) / SynodicMonth);

        return AstronomicalTime.ToDateTime(ReferenceNewMoon + (cycles * SynodicMonth));
    }

    /// <summary>Ближайшее полнолуние после заданного момента</summary>
    /// <param name="moment">Момент в шкале UTC</param>
    public static DateTime NextFullMoon(DateTime moment)
    {
        double julian = AstronomicalTime.JulianDate(moment);
        double cycles = Math.Ceiling(((julian - ReferenceNewMoon) / SynodicMonth) - 0.5);

        return AstronomicalTime.ToDateTime(ReferenceNewMoon + ((cycles + 0.5) * SynodicMonth));
    }

    private static MoonPhaseName Name(double phase) => phase switch
    {
        < 0.02 or >= 0.98 => MoonPhaseName.New,
        < 0.23 => MoonPhaseName.WaxingCrescent,
        < 0.27 => MoonPhaseName.FirstQuarter,
        < 0.48 => MoonPhaseName.WaxingGibbous,
        < 0.52 => MoonPhaseName.Full,
        < 0.73 => MoonPhaseName.WaningGibbous,
        < 0.77 => MoonPhaseName.LastQuarter,
        _ => MoonPhaseName.WaningCrescent
    };
}
