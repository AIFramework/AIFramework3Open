using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Statistics;

namespace AI.Economics.Experiments;

/// <summary>Стратегия распределения трафика между вариантами.</summary>
public enum BanditPolicy
{
    /// <summary>Равномерное деление трафика — обычный A/B-тест.</summary>
    EqualSplit,

    /// <summary>Доля исследования фиксирована, остальное — лучшему варианту.</summary>
    EpsilonGreedy,

    /// <summary>Верхняя доверительная граница: оптимизм при неопределённости.</summary>
    UpperConfidenceBound,

    /// <summary>Сэмплирование Томпсона: вариант выбирается пропорционально вероятности быть лучшим.</summary>
    ThompsonSampling,
}

/// <summary>Итог по одному варианту в симуляции.</summary>
public sealed record BanditArmResult
{
    /// <summary>Название варианта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Истинная конверсия варианта.</summary>
    public double TrueRate { get; init; }

    /// <summary>Сколько показов получил вариант.</summary>
    public int Pulls { get; init; }

    /// <summary>Доля трафика, доставшаяся варианту.</summary>
    public double TrafficShare { get; init; }

    /// <summary>Число конверсий.</summary>
    public int Rewards { get; init; }

    /// <summary>Наблюдённая конверсия.</summary>
    public double ObservedRate => Pulls > 0 ? (double)Rewards / Pulls : 0;
}

/// <summary>Результат симуляции стратегии распределения трафика.</summary>
public sealed record BanditSimulationResult : IInterpretable
{
    /// <summary>Использованная стратегия.</summary>
    public BanditPolicy Policy { get; init; }

    /// <summary>Итоги по вариантам.</summary>
    public IReadOnlyList<BanditArmResult> Arms { get; init; } = [];

    /// <summary>Всего конверсий, полученных стратегией.</summary>
    public double TotalReward { get; init; }

    /// <summary>Конверсии, которые дал бы всезнающий выбор лучшего варианта.</summary>
    public double OracleReward { get; init; }

    /// <summary>Потери от исследования: разница с всезнающим выбором.</summary>
    public double Regret => OracleReward - TotalReward;

    /// <summary>Потери в расчёте на один показ.</summary>
    public double RegretPerRound { get; init; }

    /// <summary>Доля трафика, доставшаяся действительно лучшему варианту.</summary>
    public double BestArmShare { get; init; }

    /// <summary>Определён ли лучший вариант правильно.</summary>
    public bool IdentifiedBestArm { get; init; }

    /// <summary>Накопленные потери по шагам.</summary>
    public Vector RegretPath { get; init; } = new Vector(0);

    /// <summary>Число показов в симуляции.</summary>
    public int Rounds { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        BanditArmResult? best = Arms.OrderByDescending(a => a.TrueRate).FirstOrDefault();
        string policyName = Policy switch
        {
            BanditPolicy.EqualSplit => "равномерное деление (A/B-тест)",
            BanditPolicy.EpsilonGreedy => "эпсилон-жадная",
            BanditPolicy.UpperConfidenceBound => "верхняя доверительная граница",
            BanditPolicy.ThompsonSampling => "сэмплирование Томпсона",
            _ => Policy.ToString(),
        };

        return new InterpretationBuilder("Многорукий бандит: распределение трафика")
            .Summary($"Стратегия: {policyName}. За {Fmt.Int(Rounds)} показов получено " +
                     $"{Fmt.Int(TotalReward)} конверсий против {Fmt.Int(OracleReward)} у всезнающего " +
                     $"выбора; потери {Fmt.Int(Regret)} конверсий. Лучшему варианту досталось " +
                     $"{Fmt.Pct(BestArmShare)} трафика.")
            .Metric("Потери", Fmt.Int(Regret), "конверсий",
                "цена того, что лучший вариант заранее неизвестен",
                RegretPerRound < 0.01 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Потери на показ", RegretPerRound, null, "средняя недополученная конверсия",
                MetricQuality.Neutral, 5)
            .Metric("Доля лучшего варианта", Fmt.Pct(BestArmShare), null,
                "сколько трафика ушло действительно лучшему",
                BestArmShare > 0.7 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Лучший вариант найден", IdentifiedBestArm ? "да" : "нет", null,
                "совпал ли самый нагруженный вариант с истинно лучшим",
                IdentifiedBestArm ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Конверсий получено", Fmt.Int(TotalReward), null,
                $"из {Fmt.Int(OracleReward)} возможных")
            .Finding($"Истинно лучший вариант — «{best?.Name}» с конверсией {Fmt.Pct(best?.TrueRate ?? 0)}.")
            .FindingIf(Policy == BanditPolicy.EqualSplit,
                "Равномерное деление максимизирует точность оценки каждого варианта ценой " +
                "потерь: половина трафика уходит заведомо худшему варианту до самого конца теста.")
            .FindingIf(Policy != BanditPolicy.EqualSplit && BestArmShare > 0.6,
                "Адаптивная стратегия быстро перевела основной трафик на лучший вариант — " +
                "именно за счёт этого сокращаются потери.")
            .WarningIf(!IdentifiedBestArm,
                "Стратегия не сошлась на лучшем варианте. Либо варианты слишком близки, " +
                "либо показов не хватило.")
            .WarningIf(Policy != BanditPolicy.EqualSplit,
                "Адаптивное распределение делает оценки вариантов смещёнными: у проигрывающих " +
                "мало наблюдений, и их доверительные интервалы широки. Для точного измерения " +
                "размера эффекта бандиты не годятся.")
            .Warning("Симуляция предполагает стационарность: истинные конверсии не меняются " +
                     "во времени. При сезонности и дрейфе адаптивные стратегии закрепляют " +
                     "вариант, который был лучшим только в начале.")
            .Recommendation("Бандиты уместны, когда нужно максимизировать результат " +
                            "(подбор баннера, рассылка), а A/B-тест — когда нужно измерить " +
                            "эффект и принять решение о продукте.")
            .Build();
    }
}

/// <summary>
/// Стратегии распределения трафика между вариантами и их сравнение по потерям.
/// </summary>
/// <remarks>
/// <para>
/// A/B-тест и бандит решают разные задачи. Тест минимизирует ошибку оценки
/// эффекта: для этого трафик делится поровну и не меняется до конца. Бандит
/// минимизирует потери — суммарную разницу между полученным результатом и
/// тем, что дал бы всезнающий выбор лучшего варианта с самого начала.
/// </para>
/// <para>
/// Плата за это — смещённые оценки. У проигрывающих вариантов мало
/// наблюдений, и сказать, насколько именно они хуже, бандит не может.
/// Поэтому для решения «менять ли продукт» нужен тест, а для решения
/// «какой баннер показать сейчас» — бандит.
/// </para>
/// </remarks>
public static class Bandits
{
    /// <summary>Симулирует стратегию на вариантах с известными истинными конверсиями.</summary>
    /// <param name="names">Названия вариантов.</param>
    /// <param name="trueRates">Истинные конверсии вариантов.</param>
    /// <param name="policy">Стратегия распределения.</param>
    /// <param name="rounds">Число показов.</param>
    /// <param name="epsilon">Доля исследования для эпсилон-жадной стратегии.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Итоги по вариантам и накопленные потери.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности не совпадают.</exception>
    public static BanditSimulationResult Simulate(
        IReadOnlyList<string> names, Vector trueRates, BanditPolicy policy,
        int rounds = 10_000, double epsilon = 0.1, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(trueRates);

        int arms = trueRates.Count;
        if (arms < 2) throw new ArgumentException("Нужно минимум два варианта.", nameof(trueRates));
        if (names.Count != arms)
            throw new ArgumentException("Число названий должно совпадать с числом вариантов.", nameof(names));
        if (rounds < arms) rounds = arms;

        Random rng = RandomEngine.Create(seed);

        var pulls = new int[arms];
        var rewards = new int[arms];
        var regretPath = new Vector(rounds);

        int bestArm = 0;
        for (int a = 1; a < arms; a++) if (trueRates[a] > trueRates[bestArm]) bestArm = a;
        double bestRate = trueRates[bestArm];

        double totalReward = 0, cumulativeRegret = 0;

        for (int t = 0; t < rounds; t++)
        {
            int chosen = Choose(policy, pulls, rewards, t, arms, epsilon, rng);

            bool success = rng.NextDouble() < trueRates[chosen];
            pulls[chosen]++;
            if (success)
            {
                rewards[chosen]++;
                totalReward++;
            }

            cumulativeRegret += bestRate - trueRates[chosen];
            regretPath[t] = cumulativeRegret;
        }

        var armResults = new List<BanditArmResult>(arms);
        for (int a = 0; a < arms; a++)
        {
            armResults.Add(new BanditArmResult
            {
                Name = names[a],
                TrueRate = trueRates[a],
                Pulls = pulls[a],
                TrafficShare = (double)pulls[a] / rounds,
                Rewards = rewards[a],
            });
        }

        int mostPulled = 0;
        for (int a = 1; a < arms; a++) if (pulls[a] > pulls[mostPulled]) mostPulled = a;

        return new BanditSimulationResult
        {
            Policy = policy,
            Arms = armResults,
            TotalReward = totalReward,
            OracleReward = bestRate * rounds,
            RegretPerRound = cumulativeRegret / rounds,
            BestArmShare = (double)pulls[bestArm] / rounds,
            IdentifiedBestArm = mostPulled == bestArm,
            RegretPath = regretPath,
            Rounds = rounds,
        };
    }

    /// <summary>Сравнивает все стратегии на одних и тех же вариантах.</summary>
    /// <param name="names">Названия вариантов.</param>
    /// <param name="trueRates">Истинные конверсии.</param>
    /// <param name="rounds">Число показов.</param>
    /// <param name="epsilon">Доля исследования для эпсилон-жадной стратегии.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Результаты по стратегиям в порядке возрастания потерь.</returns>
    public static IReadOnlyList<BanditSimulationResult> CompareAll(
        IReadOnlyList<string> names, Vector trueRates,
        int rounds = 10_000, double epsilon = 0.1, int seed = 42)
    {
        BanditPolicy[] policies =
        [
            BanditPolicy.EqualSplit,
            BanditPolicy.EpsilonGreedy,
            BanditPolicy.UpperConfidenceBound,
            BanditPolicy.ThompsonSampling,
        ];

        return [.. policies
            .Select(p => Simulate(names, trueRates, p, rounds, epsilon, seed))
            .OrderBy(r => r.Regret)];
    }

    private static int Choose(
        BanditPolicy policy, int[] pulls, int[] rewards, int round, int arms, double epsilon, Random rng)
    {
        // Каждый вариант должен получить хотя бы один показ, иначе
        // оптимистичные стратегии делят на ноль
        for (int a = 0; a < arms; a++) if (pulls[a] == 0) return a;

        switch (policy)
        {
            case BanditPolicy.EqualSplit:
                return round % arms;

            case BanditPolicy.EpsilonGreedy:
                return rng.NextDouble() < epsilon ? rng.Next(arms) : ArgMaxRate(pulls, rewards, arms);

            case BanditPolicy.UpperConfidenceBound:
            {
                int best = 0;
                double bestValue = double.NegativeInfinity;
                for (int a = 0; a < arms; a++)
                {
                    double mean = (double)rewards[a] / pulls[a];
                    double bonus = Math.Sqrt(2 * Math.Log(round + 1) / pulls[a]);
                    if (mean + bonus > bestValue) { bestValue = mean + bonus; best = a; }
                }
                return best;
            }

            case BanditPolicy.ThompsonSampling:
            {
                int best = 0;
                double bestDraw = double.NegativeInfinity;
                for (int a = 0; a < arms; a++)
                {
                    double draw = RandomEngine.NextBeta(rng, rewards[a] + 1, pulls[a] - rewards[a] + 1);
                    if (draw > bestDraw) { bestDraw = draw; best = a; }
                }
                return best;
            }

            default:
                return round % arms;
        }
    }

    private static int ArgMaxRate(int[] pulls, int[] rewards, int arms)
    {
        int best = 0;
        double bestRate = double.NegativeInfinity;

        for (int a = 0; a < arms; a++)
        {
            double rate = (double)rewards[a] / pulls[a];
            if (rate > bestRate) { bestRate = rate; best = a; }
        }

        return best;
    }
}
