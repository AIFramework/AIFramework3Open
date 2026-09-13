using AI.Psychology.Internal;

namespace AI.Psychology.Memory;

/// <summary>
/// Кривые забывания: экспоненциальная, степенная и формула Эббингауза.
/// </summary>
/// <remarks>
/// <para>
/// Эббингауз (1885) заучивал списки бессмысленных слогов и мерил сбережение — долю времени,
/// сэкономленную при повторном заучивании. Сам он описал свои данные формулой
/// b = 100·k/((lg t)^c + k) с k = 1,84 и c = 1,25, где t в минутах.
/// </para>
/// <para>
/// Экспонента R = e^(−t/S) предполагает постоянную скорость забывания; данные о долговременной
/// памяти лучше описывает степенная кривая R = (1 + t/τ)^(−d) (Уикстед и Эббесен, 1991): чем
/// старее след, тем медленнее он теряется. Отсюда же совет интервальных повторений — повторять
/// реже по мере того, как след крепнет.
/// </para>
/// </remarks>
public static class Forgetting
{
    /// <summary>
    /// Измерения Эббингауза (1885): интервал в минутах и доля сбережения при повторном заучивании
    /// </summary>
    public static IReadOnlyList<(double Minutes, double Savings)> EbbinghausData { get; } =
    [
        (20, 0.582),
        (60, 0.442),
        (8.8 * 60, 0.358),
        (24 * 60, 0.337),
        (2 * 24 * 60, 0.278),
        (6 * 24 * 60, 0.254),
        (31 * 24 * 60, 0.211)
    ];

    /// <summary>Экспоненциальное сохранение: e^(−t/S)</summary>
    /// <param name="time">Время с заучивания</param>
    /// <param name="stability">Стабильность S в тех же единицах</param>
    public static double Exponential(double time, double stability)
    {
        RequireTime(time);
        Numerics.RequirePositive(stability, nameof(stability));

        return Math.Exp(-time / stability);
    }

    /// <summary>Степенное сохранение: (1 + t/τ)^(−d)</summary>
    /// <param name="time">Время с заучивания</param>
    /// <param name="scale">Масштаб τ в тех же единицах</param>
    /// <param name="decay">Показатель d</param>
    public static double Power(double time, double scale, double decay)
    {
        RequireTime(time);
        Numerics.RequirePositive(scale, nameof(scale));
        Numerics.RequirePositive(decay, nameof(decay));

        return Math.Pow(1 + (time / scale), -decay);
    }

    /// <summary>Сбережение по формуле Эббингауза: 1,84/((lg t)^1,25 + 1,84), t в минутах, не меньше минуты</summary>
    /// <param name="minutes">Интервал, мин</param>
    public static double EbbinghausSavings(double minutes)
    {
        if (!(minutes >= 1) || double.IsInfinity(minutes))
            throw new ArgumentOutOfRangeException(nameof(minutes), "Формула Эббингауза определена от одной минуты");

        return 1.84 / (Math.Pow(Math.Log10(minutes), 1.25) + 1.84);
    }

    private static void RequireTime(double time)
    {
        if (!(time >= 0) || double.IsInfinity(time))
            throw new ArgumentOutOfRangeException(nameof(time), "Время — конечное неотрицательное число");
    }
}
