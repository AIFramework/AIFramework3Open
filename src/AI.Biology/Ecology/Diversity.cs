namespace AI.Biology.Ecology;

/// <summary>
/// Меры разнообразия сообщества.
/// </summary>
/// <remarks>
/// <para>
/// Индексы отвечают на разные вопросы. Число видов не различает сообщество из ста особей
/// поровну от сообщества, где девяносто девять особей одного вида. Индекс Шеннона чувствителен
/// к редким видам, индекс Симпсона — к массовым. Поэтому их приводят вместе, а не выбирают один.
/// </para>
/// <para>
/// Все индексы считаются по выборке и потому смещены: редкие виды в неё не попадают,
/// и разнообразие занижается тем сильнее, чем меньше выборка.
/// </para>
/// </remarks>
public static class Diversity
{
    /// <summary>Число видов в выборке</summary>
    /// <param name="abundances">Численности видов</param>
    public static int Richness(IReadOnlyList<int> abundances)
    {
        ArgumentNullException.ThrowIfNull(abundances);

        return abundances.Count(a => a > 0);
    }

    /// <summary>
    /// Индекс Шеннона: <c>H = −Σ pᵢ·ln pᵢ</c>
    /// </summary>
    /// <param name="abundances">Численности видов</param>
    /// <remarks>
    /// Обращается в нуль, когда вид один, и достигает <c>ln S</c> при равном обилии всех
    /// видов — это и есть верхняя граница для заданного числа видов.
    /// </remarks>
    public static double Shannon(IReadOnlyList<int> abundances)
    {
        double total = Total(abundances);
        double sum = 0;

        foreach (int count in abundances)
        {
            if (count <= 0)
                continue;

            double share = count / total;
            sum -= share * Math.Log(share);
        }

        return sum;
    }

    /// <summary>
    /// Индекс Симпсона: вероятность того, что две случайные особи окажутся разных видов
    /// </summary>
    /// <param name="abundances">Численности видов</param>
    public static double Simpson(IReadOnlyList<int> abundances)
    {
        double total = Total(abundances);
        double sum = 0;

        foreach (int count in abundances)
        {
            if (count <= 0)
                continue;

            double share = count / total;
            sum += share * share;
        }

        return 1 - sum;
    }

    /// <summary>
    /// Выравненность Пиелу: отношение индекса Шеннона к наибольшему возможному
    /// </summary>
    /// <param name="abundances">Численности видов</param>
    public static double Evenness(IReadOnlyList<int> abundances)
    {
        int species = Richness(abundances);

        return species <= 1 ? 1.0 : Shannon(abundances) / Math.Log(species);
    }

    /// <summary>
    /// Оценка истинного числа видов по Чао: поправка на не попавшие в выборку редкие виды
    /// </summary>
    /// <param name="abundances">Численности видов</param>
    /// <remarks>
    /// Поправка опирается на число видов, встреченных один и два раза: если единичных находок
    /// много, значит выборка не исчерпала сообщество.
    /// </remarks>
    public static double Chao1(IReadOnlyList<int> abundances)
    {
        ArgumentNullException.ThrowIfNull(abundances);

        int singletons = abundances.Count(a => a == 1);
        int doubletons = abundances.Count(a => a == 2);
        int observed = Richness(abundances);

        return doubletons == 0
            ? observed + (singletons * (singletons - 1) / 2.0)
            : observed + (singletons * singletons / (2.0 * doubletons));
    }

    /// <summary>
    /// Мера Жаккара: доля общих видов среди всех встреченных
    /// </summary>
    /// <param name="first">Виды первого сообщества</param>
    /// <param name="second">Виды второго сообщества</param>
    public static double Jaccard(IReadOnlySet<string> first, IReadOnlySet<string> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        int shared = first.Count(second.Contains);
        int union = first.Count + second.Count - shared;

        return union == 0 ? 1.0 : (double)shared / union;
    }

    /// <summary>
    /// Различие Брея — Кёртиса: мера несходства сообществ с учётом обилия
    /// </summary>
    /// <param name="first">Численности видов первого сообщества</param>
    /// <param name="second">Численности видов второго сообщества в том же порядке</param>
    public static double BrayCurtis(IReadOnlyList<int> first, IReadOnlyList<int> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count != second.Count)
            throw new ArgumentException("Списки должны описывать один и тот же набор видов", nameof(second));

        double difference = 0;
        double total = 0;

        for (int i = 0; i < first.Count; i++)
        {
            difference += Math.Abs(first[i] - second[i]);
            total += first[i] + second[i];
        }

        return total == 0 ? 0 : difference / total;
    }

    private static double Total(IReadOnlyList<int> abundances)
    {
        ArgumentNullException.ThrowIfNull(abundances);

        double total = abundances.Where(a => a > 0).Sum();

        return total <= 0 ? throw new ArgumentException("Выборка пуста", nameof(abundances)) : total;
    }
}
