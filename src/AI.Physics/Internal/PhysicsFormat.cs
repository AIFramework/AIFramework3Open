using System.Globalization;
using AI.Insights;
using AI.Units;

namespace AI.Physics.Internal;

/// <summary>Запись энергий, длительностей и больших чисел в разборах результатов</summary>
internal static class PhysicsFormat
{
    private static readonly double JoulesPerMeV = Si.ElectronVolt.Factor * 1e6;

    /// <summary>Энергия в мегаэлектронвольтах</summary>
    /// <param name="joules">Энергия, Дж</param>
    internal static double ToMeV(double joules) => joules / JoulesPerMeV;

    /// <summary>Энергия в подходящих единицах: кэВ, МэВ или ГэВ</summary>
    /// <param name="joules">Энергия, Дж</param>
    internal static string Energy(double joules)
    {
        double mev = ToMeV(joules);

        return Math.Abs(mev) switch
        {
            0.0 => "0 МэВ",
            < 1.0 => Fmt.Num(mev * 1e3, 3) + " кэВ",
            >= 1e3 => Fmt.Num(mev / 1e3, 4) + " ГэВ",
            _ => Fmt.Num(mev, 4) + " МэВ",
        };
    }

    /// <summary>Длительность в подходящих единицах: секунды, минуты, часы, сутки, годы</summary>
    /// <param name="seconds">Длительность, с</param>
    internal static string Duration(double seconds)
    {
        const double Year = 365.25 * 86_400;

        return seconds switch
        {
            < 120 => Fmt.Num(seconds, 3) + " с",
            < 7_200 => Fmt.Num(seconds / 60, 2) + " мин",
            < 3 * 86_400 => Fmt.Num(seconds / 3_600, 2) + " ч",
            < 2 * Year => Fmt.Num(seconds / 86_400, 2) + " сут",
            _ => Scientific(seconds / Year) + " лет",
        };
    }

    /// <summary>Число в экспоненциальной записи</summary>
    /// <param name="value">Число</param>
    internal static string Scientific(double value) => value.ToString("0.###E+0", CultureInfo.InvariantCulture);
}
