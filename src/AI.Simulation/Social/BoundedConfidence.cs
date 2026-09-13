using AI.Insights;
using AI.Simulation.Space;

namespace AI.Simulation.Social;

/// <summary>Группа агентов с практически одинаковым мнением</summary>
/// <param name="Position">Среднее мнение группы</param>
/// <param name="Size">Число агентов</param>
public readonly record struct OpinionCluster(double Position, int Size);

/// <summary>Итог динамики мнений с ограниченным доверием</summary>
public sealed class OpinionDynamicsResult : IInterpretable
{
    internal OpinionDynamicsResult(
        string model, double confidence, IReadOnlyList<double> initial, IReadOnlyList<double> opinions, long steps, bool converged)
    {
        Model = model;
        Confidence = confidence;
        Opinions = opinions;
        Steps = steps;
        Converged = converged;
        InitialMean = initial.Average();
        FinalMean = opinions.Average();
        Clusters = BoundedConfidence.Clusters(opinions, confidence / 2);
    }

    /// <summary>Название модели</summary>
    public string Model { get; }

    /// <summary>Порог доверия ε: мнения дальше него агент не слышит</summary>
    public double Confidence { get; }

    /// <summary>Мнения агентов в конце</summary>
    public IReadOnlyList<double> Opinions { get; }

    /// <summary>Группы мнений: соседние мнения ближе ε/2 относятся к одной группе</summary>
    public IReadOnlyList<OpinionCluster> Clusters { get; }

    /// <summary>Выполнено шагов или парных взаимодействий</summary>
    public long Steps { get; }

    /// <summary>Сошлась ли динамика: внутри групп мнения совпали, а сами группы дальше ε друг от друга</summary>
    public bool Converged { get; }

    /// <summary>Среднее мнение в начале</summary>
    public double InitialMean { get; }

    /// <summary>Среднее мнение в конце</summary>
    public double FinalMean { get; }

    /// <summary>Пришли ли все к одному мнению</summary>
    public bool IsConsensus => Clusters.Count == 1;

    /// <summary>Число групп, в каждой из которых не меньше заданной доли агентов</summary>
    /// <param name="share">Доля агентов</param>
    public int MajorClusterCount(double share = 0.05) => Clusters.Count(cluster => cluster.Size >= share * Opinions.Count);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        OpinionCluster largest = Clusters.MaxBy(cluster => cluster.Size);

        return new InterpretationBuilder(Model)
            .Summary(IsConsensus
                ? $"Все {Opinions.Count} агентов пришли к мнению {Fmt.Num(largest.Position, 3)}."
                : $"Мнения {Opinions.Count} агентов разошлись на {Clusters.Count} групп; самая большая — {largest.Size} агентов у мнения {Fmt.Num(largest.Position, 3)}.")
            .Metric("Агентов", Opinions.Count, null, null, MetricQuality.Unknown, 0)
            .Metric("Групп", Clusters.Count, null, "соседние мнения ближе ε/2 — одна группа", MetricQuality.Unknown, 0)
            .Metric("Крупных групп", MajorClusterCount(), null, "не меньше 5 % агентов", MetricQuality.Unknown, 0)
            .Metric("Порог доверия ε", Confidence, null, null, MetricQuality.Unknown, 3)
            .Metric("Среднее мнение", $"{Fmt.Num(InitialMean, 3)} → {Fmt.Num(FinalMean, 3)}", null, null)
            .Metric("Шагов", Steps, null, null, MetricQuality.Unknown, 0)
            .FindingIf(Converged && Clusters.Count > 1,
                "Группы разделены больше чем порогом доверия: друг друга они уже не слышат, поэтому расхождение устойчиво и само не исчезнет.")
            .FindingIf(Math.Abs(FinalMean - InitialMean) > 1e-6 * (1 + Math.Abs(InitialMean)),
                "Среднее мнение сдвинулось: при несимметричном влиянии итог зависит не только от начального среднего, но и от того, кто кого слышал.")
            .WarningIf(!Converged, "Предел шагов исчерпан раньше, чем динамика сошлась: группы могут ещё сливаться.")
            .Warning("Порог доверия здесь один на всех и не меняется со временем. У реальных людей он разный, а упрямые агенты "
                + "с малым порогом способны удержать общество от согласия.")
            .Build();
    }
}

/// <summary>
/// Динамика мнений с ограниченным доверием: агент слушает только тех, чьё мнение близко к его собственному.
/// </summary>
/// <remarks>
/// <para>
/// В модели Хегсельмана — Краузе все одновременно переходят к среднему мнению тех, кто ближе ε
/// (включая себя). В модели Деффюана встречаются двое, и если их мнения ближе ε, каждый сдвигается
/// к другому на долю μ расстояния. Обе модели показывают то, чего нет у Де Грота: общество
/// раскалывается на группы, если ε мало, и группы потом друг друга не слышат.
/// </para>
/// <para>
/// Проверяемые свойства. У Хегсельмана — Краузе сохраняется порядок мнений, динамика сходится за
/// конечное число шагов, и в конце группы разделены больше чем ε. У Деффюана каждое взаимодействие
/// симметрично, поэтому среднее мнение сохраняется точно, а число групп при равномерных начальных
/// мнениях близко к 1/(2ε).
/// </para>
/// </remarks>
public static class BoundedConfidence
{
    private const double StationaryChange = 1e-12;

    /// <summary>
    /// Модель Хегсельмана — Краузе: одновременное усреднение по доверенным соседям до схождения
    /// </summary>
    /// <param name="initial">Начальные мнения</param>
    /// <param name="confidence">Порог доверия ε</param>
    /// <param name="maxSteps">Предел шагов</param>
    public static OpinionDynamicsResult HegselmannKrause(IReadOnlyList<double> initial, double confidence, int maxSteps = 10_000)
    {
        Require(initial, confidence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSteps);

        double[] opinions = [.. initial];
        int steps = 0;
        bool converged = false;

        while (steps < maxSteps)
        {
            double[] next = HegselmannKrauseStep(opinions, confidence);
            steps++;
            double change = opinions.Select((value, i) => Math.Abs(next[i] - value)).Max();
            opinions = next;

            if (change <= StationaryChange * (1 + Math.Abs(opinions[0])))
            {
                converged = true;
                break;
            }
        }

        return new OpinionDynamicsResult("Модель Хегсельмана — Краузе", confidence, initial, opinions, steps, converged);
    }

    /// <summary>Один шаг Хегсельмана — Краузе: каждый переходит к среднему мнений не дальше ε от своего</summary>
    /// <param name="opinions">Мнения</param>
    /// <param name="confidence">Порог доверия ε</param>
    public static double[] HegselmannKrauseStep(IReadOnlyList<double> opinions, double confidence)
    {
        Require(opinions, confidence);

        int n = opinions.Count;
        int[] order = [.. Enumerable.Range(0, n).OrderBy(i => opinions[i])];
        double[] sorted = [.. order.Select(i => opinions[i])];
        var prefix = new double[n + 1];

        for (int k = 0; k < n; k++)
            prefix[k + 1] = prefix[k] + sorted[k];

        // Окно доверия в упорядоченном ряду — два указателя: всё за O(n log n), а не O(n²)
        var next = new double[n];
        int low = 0, high = 0;

        for (int k = 0; k < n; k++)
        {
            while (sorted[k] - sorted[low] > confidence)
                low++;

            high = Math.Max(high, k);

            while (high + 1 < n && sorted[high + 1] - sorted[k] <= confidence)
                high++;

            next[order[k]] = (prefix[high + 1] - prefix[low]) / (high - low + 1);
        }

        return next;
    }

    /// <summary>
    /// Модель Деффюана: случайные пары сближают мнения, если те ближе ε
    /// </summary>
    /// <param name="initial">Начальные мнения</param>
    /// <param name="confidence">Порог доверия ε</param>
    /// <param name="convergence">Доля сближения μ ∈ (0; 0,5]: при 0,5 оба приходят к середине</param>
    /// <param name="interactions">Число встреч</param>
    /// <param name="random">Генератор</param>
    /// <param name="network">Сеть контактов; <c>null</c> — встречаются любые двое</param>
    public static OpinionDynamicsResult Deffuant(
        IReadOnlyList<double> initial, double confidence, double convergence, long interactions, Random random, ContactNetwork? network = null)
    {
        Require(initial, confidence);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(interactions);

        if (!(convergence > 0 && convergence <= 0.5))
            throw new ArgumentOutOfRangeException(nameof(convergence), "Доля сближения лежит на (0; 0,5]");

        if (initial.Count < 2)
            throw new ArgumentException("Для встреч нужно хотя бы два агента", nameof(initial));

        if (network is not null && network.NodeCount != initial.Count)
            throw new ArgumentException($"В сети {network.NodeCount} узлов, а агентов {initial.Count}", nameof(network));

        double[] opinions = [.. initial];
        int n = opinions.Length;

        for (long t = 0; t < interactions; t++)
        {
            int i = random.Next(n);
            int j;

            if (network is null)
            {
                j = random.Next(n - 1);

                if (j >= i)
                    j++;
            }
            else
            {
                int[] contacts = network.Contacts(i);

                if (contacts.Length == 0)
                    continue;

                j = contacts[random.Next(contacts.Length)];
            }

            double gap = opinions[j] - opinions[i];

            // Сдвиги равны и противоположны: сумма мнений, а значит и среднее, не меняется
            if (Math.Abs(gap) < confidence)
            {
                opinions[i] += convergence * gap;
                opinions[j] -= convergence * gap;
            }
        }

        return new OpinionDynamicsResult("Модель Деффюана", confidence, initial, opinions, interactions, IsSettled(opinions, confidence));
    }

    /// <summary>
    /// Группы мнений: упорядоченные мнения разбиваются там, где соседние отстоят больше чем на допуск
    /// </summary>
    /// <param name="opinions">Мнения</param>
    /// <param name="tolerance">Наибольший разрыв внутри группы</param>
    public static IReadOnlyList<OpinionCluster> Clusters(IReadOnlyList<double> opinions, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(opinions);

        if (!(tolerance >= 0))
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Допуск — неотрицательное число");

        if (opinions.Count == 0)
            return [];

        double[] sorted = [.. opinions.Order()];
        var clusters = new List<OpinionCluster>();
        int start = 0;

        for (int k = 1; k <= sorted.Length; k++)
        {
            if (k < sorted.Length && sorted[k] - sorted[k - 1] <= tolerance)
                continue;

            double sum = 0;

            for (int m = start; m < k; m++)
                sum += sorted[m];

            clusters.Add(new OpinionCluster(sum / (k - start), k - start));
            start = k;
        }

        return clusters;
    }

    /// <summary>Устоялись ли мнения: соседние в упорядоченном ряду либо совпали, либо уже не слышат друг друга</summary>
    private static bool IsSettled(IReadOnlyList<double> opinions, double confidence)
    {
        double[] sorted = [.. opinions.Order()];
        double scale = 1e-6 * Math.Max(confidence, 1e-12);

        for (int k = 1; k < sorted.Length; k++)
        {
            double gap = sorted[k] - sorted[k - 1];

            if (gap > scale && gap < confidence)
                return false;
        }

        return true;
    }

    private static void Require(IReadOnlyList<double> opinions, double confidence)
    {
        ArgumentNullException.ThrowIfNull(opinions);

        if (opinions.Count == 0)
            throw new ArgumentException("Нужен хотя бы один агент", nameof(opinions));

        if (opinions.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Мнения — конечные числа", nameof(opinions));

        if (!(confidence > 0) || double.IsInfinity(confidence))
            throw new ArgumentOutOfRangeException(nameof(confidence), "Порог доверия — конечное положительное число");
    }
}
