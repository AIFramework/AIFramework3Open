using AI.Insights;
using AI.Statistics;

namespace AI.Biology.Genetics;

/// <summary>Результат проверки равновесия Харди — Вайнберга</summary>
/// <param name="AlleleFrequency">Частота доминантного аллеля p</param>
/// <param name="ExpectedHomozygousDominant">Ожидаемое число доминантных гомозигот</param>
/// <param name="ExpectedHeterozygous">Ожидаемое число гетерозигот</param>
/// <param name="ExpectedHomozygousRecessive">Ожидаемое число рецессивных гомозигот</param>
/// <param name="ChiSquare">Статистика хи-квадрат</param>
/// <param name="PValue">Достигнутый уровень значимости</param>
/// <param name="InEquilibrium">Согласуются ли наблюдения с равновесием</param>
public readonly record struct HardyWeinbergResult(
    double AlleleFrequency,
    double ExpectedHomozygousDominant,
    double ExpectedHeterozygous,
    double ExpectedHomozygousRecessive,
    double ChiSquare,
    double PValue,
    bool InEquilibrium) : IInterpretable
{
    /// <summary>Частота рецессивного аллеля q</summary>
    public double RecessiveFrequency => 1 - AlleleFrequency;

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Равновесие Харди — Вайнберга")
            .Summary($"Частота доминантного аллеля {Fmt.Num(AlleleFrequency, 4)}, рецессивного "
                + $"{Fmt.Num(RecessiveFrequency, 4)}. Хи-квадрат {Fmt.Num(ChiSquare, 3)}, p = {Fmt.Num(PValue, 4)}: "
                + (InEquilibrium
                    ? "наблюдаемые частоты генотипов согласуются с равновесием."
                    : "наблюдаемые частоты значимо отклоняются от равновесных."))
            .Metric("p", Fmt.Num(AlleleFrequency, 4), null, "частота доминантного аллеля")
            .Metric("q", Fmt.Num(RecessiveFrequency, 4), null, "частота рецессивного аллеля")
            .Metric("χ²", Fmt.Num(ChiSquare, 3), null, "отклонение наблюдений от ожидания",
                InEquilibrium ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("p-значение", Fmt.Num(PValue, 4), null, "вероятность такого отклонения при равновесии")
            .FindingIf(!InEquilibrium,
                "Отклонение значимо. Причин обычно пять: отбор, неслучайное скрещивание, дрейф в малой "
                + "популяции, миграция и мутации. Сам критерий не говорит, какая из них подействовала.")
            .FindingIf(InEquilibrium,
                "Согласие с равновесием — не доказательство отсутствия отбора: критерий слаб на малых "
                + "выборках и не замечает слабого давления.")
            .Warning("Равновесие предполагает бесконечную популяцию, случайное скрещивание, отсутствие "
                + "отбора, миграции и мутаций. Ни одно из условий в природе не выполняется точно — "
                + "модель служит точкой отсчёта, а не описанием.")
            .Build();
}

/// <summary>
/// Популяционная генетика: равновесие Харди — Вайнберга и частоты аллелей.
/// </summary>
public static class HardyWeinberg
{
    /// <summary>
    /// Проверяет соответствие наблюдаемых частот генотипов равновесию
    /// </summary>
    /// <param name="homozygousDominant">Число особей с генотипом AA</param>
    /// <param name="heterozygous">Число особей Aa</param>
    /// <param name="homozygousRecessive">Число особей aa</param>
    /// <param name="alpha">Уровень значимости</param>
    public static HardyWeinbergResult Test(
        int homozygousDominant, int heterozygous, int homozygousRecessive, double alpha = 0.05)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(homozygousDominant);
        ArgumentOutOfRangeException.ThrowIfNegative(heterozygous);
        ArgumentOutOfRangeException.ThrowIfNegative(homozygousRecessive);

        int total = homozygousDominant + heterozygous + homozygousRecessive;

        if (total == 0)
            throw new ArgumentException("Выборка пуста", nameof(homozygousDominant));

        double p = ((2.0 * homozygousDominant) + heterozygous) / (2.0 * total);
        double q = 1 - p;

        double expectedDominant = p * p * total;
        double expectedHeterozygous = 2 * p * q * total;
        double expectedRecessive = q * q * total;

        double chi = Contribution(homozygousDominant, expectedDominant)
            + Contribution(heterozygous, expectedHeterozygous)
            + Contribution(homozygousRecessive, expectedRecessive);

        // Одна степень свободы: три генотипа минус одна оценённая частота минус связь долей
        double pValue = 1 - StatInference.ChiSquaredCdf(chi, 1);

        return new HardyWeinbergResult(
            p, expectedDominant, expectedHeterozygous, expectedRecessive, chi, pValue, pValue > alpha);
    }

    /// <summary>
    /// Частота носителей рецессивного признака при известной частоте больных
    /// </summary>
    /// <param name="affectedFrequency">Доля особей с рецессивным фенотипом</param>
    /// <remarks>
    /// Именно этот расчёт показывает, почему редкие рецессивные болезни не исчезают:
    /// при частоте больных один на десять тысяч носителем оказывается каждый пятидесятый.
    /// </remarks>
    public static double CarrierFrequency(double affectedFrequency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(affectedFrequency);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(affectedFrequency, 1.0);

        double q = Math.Sqrt(affectedFrequency);

        return 2 * q * (1 - q);
    }

    private static double Contribution(int observed, double expected)
        => expected <= 0 ? 0 : (observed - expected) * (observed - expected) / expected;
}

/// <summary>
/// Менделевское наследование: расщепление в потомстве и проверка соответствия ожиданию.
/// </summary>
public static class Mendel
{
    /// <summary>Ожидаемое расщепление при скрещивании гетерозигот по одному признаку</summary>
    public static IReadOnlyList<double> MonohybridRatio => [0.75, 0.25];

    /// <summary>Ожидаемое расщепление по двум независимым признакам</summary>
    public static IReadOnlyList<double> DihybridRatio => [9.0 / 16, 3.0 / 16, 3.0 / 16, 1.0 / 16];

    /// <summary>
    /// Проверяет соответствие наблюдаемого расщепления ожидаемому критерием хи-квадрат
    /// </summary>
    /// <param name="observed">Наблюдаемые численности классов</param>
    /// <param name="expectedRatio">Ожидаемые доли классов</param>
    /// <param name="alpha">Уровень значимости</param>
    /// <returns>Достигнутый уровень значимости и вывод о согласии</returns>
    public static (double ChiSquare, double PValue, bool Fits) TestRatio(
        IReadOnlyList<int> observed, IReadOnlyList<double> expectedRatio, double alpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(expectedRatio);

        if (observed.Count != expectedRatio.Count)
            throw new ArgumentException("Число классов должно совпадать", nameof(expectedRatio));

        int total = observed.Sum();
        double chi = 0;

        for (int i = 0; i < observed.Count; i++)
        {
            double expected = expectedRatio[i] * total;

            if (expected <= 0)
                continue;

            chi += (observed[i] - expected) * (observed[i] - expected) / expected;
        }

        int degrees = observed.Count - 1;
        double pValue = 1 - StatInference.ChiSquaredCdf(chi, degrees);

        return (chi, pValue, pValue > alpha);
    }

    /// <summary>
    /// Частота рекомбинации: доля потомков с перекомбинированными признаками
    /// </summary>
    /// <param name="recombinants">Число рекомбинантных потомков</param>
    /// <param name="total">Общее число потомков</param>
    public static double RecombinationFrequency(int recombinants, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(total);

        return (double)recombinants / total;
    }

    /// <summary>
    /// Расстояние между генами в сантиморганидах — частота рекомбинации в процентах
    /// </summary>
    /// <param name="recombinationFrequency">Частота рекомбинации</param>
    /// <remarks>
    /// Соответствие «процент рекомбинации — сантиморганида» верно лишь для близких генов.
    /// На расстояниях свыше двадцати сантиморганид двойные перекрёсты занижают наблюдаемую
    /// частоту, и карту строят по сумме коротких отрезков, а не по прямому измерению.
    /// </remarks>
    public static double MapDistance(double recombinationFrequency) => recombinationFrequency * 100;
}
