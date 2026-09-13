using AI.Psychology.Internal;

namespace AI.Psychology.Decision;

/// <summary>
/// Обесценивание отсроченной награды: экспоненциальное и гиперболическое.
/// </summary>
/// <remarks>
/// <para>
/// Экономическая модель дисконтирует экспоненциально, V = A·e^(−kD): предпочтения постоянны во
/// времени, и если сегодня больше хочется 100 через месяц, чем 50 через неделю, то и через год
/// захочется того же. Люди и животные обесценивают гиперболически, V = A/(1 + kD) (Мазур, 1987):
/// близкая награда падает в цене круто, далёкая — полого. Отсюда смена предпочтений: издалека
/// выбирают большее позднее, а когда меньшее раннее становится близким — переходят на него. Это и
/// есть математика импульсивности, прокрастинации и срыва диет.
/// </para>
/// <para>
/// Скорость k по точкам безразличия находится линеаризацией A/V − 1 = k·D и прямой через начало
/// координат. Линеаризация взвешивает ошибки неравномерно, поэтому при шумных данных оценка
/// нелинейным методом наименьших квадратов может отличаться. Площадь под кривой (Майерсон и др., 2001)
/// не зависит от выбора модели.
/// </para>
/// </remarks>
public static class Discounting
{
    /// <summary>Экспоненциальное обесценивание: A·e^(−kD)</summary>
    /// <param name="amount">Величина награды</param>
    /// <param name="delay">Отсрочка</param>
    /// <param name="rate">Скорость k в единицах, обратных отсрочке</param>
    public static double Exponential(double amount, double delay, double rate)
    {
        Validate(amount, delay, rate);

        return amount * Math.Exp(-rate * delay);
    }

    /// <summary>Гиперболическое обесценивание: A/(1 + kD)</summary>
    /// <param name="amount">Величина награды</param>
    /// <param name="delay">Отсрочка</param>
    /// <param name="rate">Скорость k в единицах, обратных отсрочке</param>
    public static double Hyperbolic(double amount, double delay, double rate)
    {
        Validate(amount, delay, rate);

        return amount / (1 + (rate * delay));
    }

    /// <summary>
    /// Скорость гиперболического обесценивания по точкам безразличия
    /// </summary>
    /// <param name="delays">Отсрочки</param>
    /// <param name="indifferenceFractions">Доля отсроченной награды, равноценная немедленной: V/A ∈ (0; 1]</param>
    public static double FitHyperbolicRate(IReadOnlyList<double> delays, IReadOnlyList<double> indifferenceFractions)
    {
        ArgumentNullException.ThrowIfNull(delays);
        ArgumentNullException.ThrowIfNull(indifferenceFractions);

        if (delays.Count != indifferenceFractions.Count || delays.Count == 0)
            throw new ArgumentException("Отсрочек и точек безразличия должно быть поровну, хотя бы по одной");

        double numerator = 0, denominator = 0;

        for (int i = 0; i < delays.Count; i++)
        {
            double fraction = indifferenceFractions[i];

            if (!(fraction > 0 && fraction <= 1))
                throw new ArgumentOutOfRangeException(nameof(indifferenceFractions), "Доля безразличия лежит в (0; 1]");

            numerator += delays[i] * ((1 / fraction) - 1);
            denominator += delays[i] * delays[i];
        }

        return denominator == 0
            ? throw new ArgumentException("Все отсрочки нулевые: скорость не определена", nameof(delays))
            : numerator / denominator;
    }

    /// <summary>
    /// Площадь под кривой обесценивания: отсрочки делятся на наибольшую, кривая начинается в (0; 1)
    /// </summary>
    /// <param name="delays">Отсрочки по возрастанию</param>
    /// <param name="indifferenceFractions">Доли безразличия</param>
    /// <returns>От 0 (награда обесценивается мгновенно) до 1 (не обесценивается)</returns>
    public static double AreaUnderCurve(IReadOnlyList<double> delays, IReadOnlyList<double> indifferenceFractions)
    {
        ArgumentNullException.ThrowIfNull(delays);
        ArgumentNullException.ThrowIfNull(indifferenceFractions);

        if (delays.Count != indifferenceFractions.Count || delays.Count == 0)
            throw new ArgumentException("Отсрочек и точек безразличия должно быть поровну, хотя бы по одной");

        double longest = delays.Max();
        Numerics.RequirePositive(longest, nameof(delays));

        double area = 0, previousDelay = 0, previousValue = 1;

        for (int i = 0; i < delays.Count; i++)
        {
            double x = delays[i] / longest;

            if (x < previousDelay)
                throw new ArgumentException("Отсрочки должны идти по возрастанию", nameof(delays));

            area += (x - previousDelay) * (previousValue + indifferenceFractions[i]) / 2;
            previousDelay = x;
            previousValue = indifferenceFractions[i];
        }

        return area;
    }

    private static void Validate(double amount, double delay, double rate)
    {
        Numerics.RequireFinite(amount, nameof(amount));

        if (!(delay >= 0) || double.IsInfinity(delay))
            throw new ArgumentOutOfRangeException(nameof(delay), "Отсрочка — конечное неотрицательное число");

        if (!(rate >= 0) || double.IsInfinity(rate))
            throw new ArgumentOutOfRangeException(nameof(rate), "Скорость обесценивания — конечное неотрицательное число");
    }
}
