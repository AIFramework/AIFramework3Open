using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>
/// Распад одного радионуклида: постоянная распада, активность, удельная активность, датирование.
/// </summary>
/// <remarks>
/// Закон распада <c>N = N₀·2^(−t/T½)</c> статистический: он точен для числа ядер, а не для одного
/// ядра. Цепочки, где дочерние нуклиды тоже распадаются, — в <see cref="DecayChain"/>.
/// </remarks>
public static class RadioactiveDecay
{
    /// <summary>Постоянная распада <c>λ = ln 2 / T½</c></summary>
    /// <param name="halfLife">Период полураспада</param>
    public static Quantity DecayConstant(Quantity halfLife)
        => new(Math.Log(2) / Seconds(halfLife, nameof(halfLife)), Dimension.Frequency);

    /// <summary>Среднее время жизни <c>τ = T½ / ln 2</c></summary>
    /// <param name="halfLife">Период полураспада</param>
    public static Quantity MeanLife(Quantity halfLife)
        => new(Seconds(halfLife, nameof(halfLife)) / Math.Log(2), Dimension.TimeDim);

    /// <summary>Доля ядер, не распавшихся за время <paramref name="elapsed"/></summary>
    /// <param name="halfLife">Период полураспада</param>
    /// <param name="elapsed">Прошедшее время</param>
    public static double RemainingFraction(Quantity halfLife, Quantity elapsed)
    {
        double t = elapsed.RequireSi(Dimension.TimeDim, nameof(elapsed));

        if (!(t >= 0))
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Прошедшее время неотрицательно");

        return Math.Pow(2, -t / Seconds(halfLife, nameof(halfLife)));
    }

    /// <summary>
    /// Время, за которое осталась заданная доля ядер, — основа радиометрического датирования
    /// </summary>
    /// <remarks>
    /// Радиоуглеродный возраст — это время, за которое доля углерода-14 упала до измеренной.
    /// Метод предполагает, что начальная доля известна и что ядра не приносились и не уносились
    /// извне; нарушение любого из допущений сдвигает возраст сильнее, чем погрешность измерения.
    /// </remarks>
    /// <param name="halfLife">Период полураспада</param>
    /// <param name="remainingFraction">Оставшаяся доля, от нуля до единицы</param>
    public static Quantity ElapsedTime(Quantity halfLife, double remainingFraction)
    {
        if (remainingFraction is not (> 0 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(remainingFraction), "Оставшаяся доля лежит в (0, 1]");

        return new Quantity(Seconds(halfLife, nameof(halfLife)) * Math.Log2(1 / remainingFraction), Dimension.TimeDim);
    }

    /// <summary>Активность — число распадов в секунду: <c>A = λN</c></summary>
    /// <param name="halfLife">Период полураспада</param>
    /// <param name="atoms">Число ядер</param>
    public static Quantity Activity(Quantity halfLife, double atoms)
    {
        if (!(atoms >= 0) || double.IsInfinity(atoms))
            throw new ArgumentOutOfRangeException(nameof(atoms), "Число ядер — конечное неотрицательное число");

        return new Quantity(atoms * Math.Log(2) / Seconds(halfLife, nameof(halfLife)), Dimension.Frequency);
    }

    /// <summary>Число ядер, дающее заданную активность: <c>N = A/λ</c></summary>
    /// <param name="activity">Активность</param>
    /// <param name="halfLife">Период полураспада</param>
    public static double AtomsForActivity(Quantity activity, Quantity halfLife)
    {
        double a = activity.RequireSi(Dimension.Frequency, nameof(activity));

        if (!(a >= 0))
            throw new ArgumentOutOfRangeException(nameof(activity), "Активность неотрицательна");

        return a * Seconds(halfLife, nameof(halfLife)) / Math.Log(2);
    }

    /// <summary>
    /// Удельная активность чистого нуклида: <c>λ·N_A/M</c>, распадов в секунду на килограмм
    /// </summary>
    /// <remarks>
    /// Кюри когда-то определили как активность грамма радия-226; эта формула даёт для него
    /// 3.66·10¹⁰ Бк — на процент меньше принятых 3.7·10¹⁰, потому что период полураспада радия
    /// с тех пор уточнили.
    /// </remarks>
    /// <param name="halfLife">Период полураспада</param>
    /// <param name="molarMass">Молярная масса</param>
    public static Quantity SpecificActivity(Quantity halfLife, Quantity molarMass)
    {
        double m = molarMass.RequireSi(Dimension.MassDim / Dimension.AmountDim, nameof(molarMass));

        if (!(m > 0))
            throw new ArgumentOutOfRangeException(nameof(molarMass), "Молярная масса положительна");

        double lambda = Math.Log(2) / Seconds(halfLife, nameof(halfLife));

        return new Quantity(lambda * PhysicalConstants.AvogadroConstant.SiValue / m, Dimension.Frequency / Dimension.MassDim);
    }

    /// <summary>Период полураспада в секундах с проверкой</summary>
    internal static double Seconds(Quantity halfLife, string paramName)
    {
        double seconds = halfLife.RequireSi(Dimension.TimeDim, paramName);

        if (!(seconds > 0) || double.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(paramName, "Период полураспада — конечное положительное время");

        return seconds;
    }
}
