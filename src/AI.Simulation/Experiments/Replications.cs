using AI.Statistics;

namespace AI.Simulation.Experiments;

/// <summary>
/// Серия независимых прогонов модели и доверительные интервалы по ней.
/// </summary>
/// <remarks>
/// <para>
/// Один прогон имитационной модели — одна случайная выборка, и его показатели содержат
/// случайную погрешность. Оценить её изнутри прогона нельзя: наблюдения в нём зависимы.
/// Поэтому модель прогоняется несколько раз с разными зёрнами генератора, каждый прогон
/// даёт одно число, и интервал строится уже по этим независимым числам.
/// </para>
/// <para>
/// Интервал — t-интервал Стьюдента из <see cref="StatInference.ConfidenceIntervalT"/>: средние
/// по прогонам близки к нормальным по центральной предельной теореме, а дисперсия неизвестна.
/// </para>
/// </remarks>
public static class Replications
{
    /// <summary>
    /// Прогоняет модель заданное число раз и оценивает показатель
    /// </summary>
    /// <param name="name">Название показателя</param>
    /// <param name="replications">Число прогонов, не меньше двух</param>
    /// <param name="model">Модель: получает зерно генератора, возвращает показатель прогона</param>
    /// <param name="firstSeed">Зерно первого прогона; следующие берут зёрна подряд</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static ReplicationEstimate Run(
        string name, int replications, Func<int, double> model, int firstSeed = 1, double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(model);
        RequireReplications(replications);

        var observations = new double[replications];

        for (int i = 0; i < replications; i++)
            observations[i] = model(firstSeed + i);

        return Estimate(name, observations, confidence);
    }

    /// <summary>
    /// Прогоняет модель и оценивает сразу несколько показателей
    /// </summary>
    /// <remarks>
    /// Все показатели берутся из одних и тех же прогонов, поэтому их оценки согласованы между
    /// собой: ожидание и длина очереди получены на одних и тех же случайных потоках.
    /// </remarks>
    /// <param name="replications">Число прогонов, не меньше двух</param>
    /// <param name="model">Модель: получает зерно генератора, возвращает показатели прогона по именам</param>
    /// <param name="firstSeed">Зерно первого прогона; следующие берут зёрна подряд</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static IReadOnlyDictionary<string, ReplicationEstimate> Run(
        int replications,
        Func<int, IReadOnlyDictionary<string, double>> model,
        int firstSeed = 1,
        double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(model);
        RequireReplications(replications);

        var byName = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        var order = new List<string>();

        for (int i = 0; i < replications; i++)
        {
            IReadOnlyDictionary<string, double> run = model(firstSeed + i)
                ?? throw new InvalidOperationException($"Прогон с зерном {firstSeed + i} не вернул показателей");

            foreach (KeyValuePair<string, double> metric in run)
            {
                if (!byName.TryGetValue(metric.Key, out List<double>? values))
                {
                    values = [];
                    byName[metric.Key] = values;
                    order.Add(metric.Key);
                }

                values.Add(metric.Value);
            }
        }

        var result = new Dictionary<string, ReplicationEstimate>(StringComparer.Ordinal);

        foreach (string metric in order)
        {
            if (byName[metric].Count != replications)
                throw new InvalidOperationException($"Показатель «{metric}» есть не во всех прогонах");

            result[metric] = Estimate(metric, byName[metric], confidence);
        }

        return result;
    }

    /// <summary>
    /// Оценка по готовым значениям показателя в независимых прогонах
    /// </summary>
    /// <param name="name">Название показателя</param>
    /// <param name="observations">Значение показателя в каждом прогоне</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static ReplicationEstimate Estimate(string name, IReadOnlyList<double> observations, double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(observations);
        RequireReplications(observations.Count);

        if (confidence is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(confidence),
                "Доверительная вероятность лежит строго между нулём и единицей");

        double mean = observations.Average();
        double variance = observations.Sum(x => (x - mean) * (x - mean)) / (observations.Count - 1);
        double deviation = Math.Sqrt(variance);

        (double lower, double upper) = StatInference.ConfidenceIntervalT(mean, deviation, observations.Count, confidence);

        return new ReplicationEstimate(name ?? string.Empty, observations.ToArray(), mean, deviation, lower, upper, confidence);
    }

    /// <summary>
    /// Сколько прогонов нужно, чтобы полуширина интервала не превышала заданную
    /// </summary>
    /// <remarks>
    /// Оценка по пилотной серии: полуширина убывает как 1/√n, поэтому <c>n* = n · (h / h*)²</c>.
    /// Разброс берётся пилотный, а он сам случаен — полученное число стоит считать ориентиром
    /// и проверить новой серией.
    /// </remarks>
    /// <param name="pilot">Оценка по пилотной серии</param>
    /// <param name="targetHalfWidth">Желаемая полуширина интервала</param>
    public static int RequiredReplications(ReplicationEstimate pilot, double targetHalfWidth)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHalfWidth);

        double ratio = pilot.HalfWidth / targetHalfWidth;

        return Math.Max(pilot.Replications, (int)Math.Ceiling(pilot.Replications * ratio * ratio));
    }

    private static void RequireReplications(int replications)
    {
        if (replications < 2)
            throw new ArgumentOutOfRangeException(nameof(replications), "Для интервала нужно не меньше двух прогонов");
    }
}
