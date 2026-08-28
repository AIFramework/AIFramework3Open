using System;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Experiments;

/// <summary>Расчёт размера выборки эксперимента.</summary>
public sealed record SampleSizeResult : IInterpretable
{
    /// <summary>Базовая конверсия или среднее контрольной группы.</summary>
    public double Baseline { get; init; }

    /// <summary>Минимальный обнаруживаемый эффект в относительных единицах.</summary>
    public double MinimumDetectableEffect { get; init; }

    /// <summary>Уровень значимости.</summary>
    public double Alpha { get; init; }

    /// <summary>Мощность критерия.</summary>
    public double Power { get; init; }

    /// <summary>Требуемое число наблюдений на вариант.</summary>
    public int PerVariant { get; init; }

    /// <summary>Требуемое число наблюдений всего.</summary>
    public int Total { get; init; }

    /// <summary>Число вариантов, включая контроль.</summary>
    public int Variants { get; init; }

    /// <summary>Трафик в сутки; ноль означает, что срок не рассчитывался.</summary>
    public double DailyTraffic { get; init; }

    /// <summary>Требуемая длительность эксперимента в сутках.</summary>
    public double DaysRequired { get; init; }

    /// <summary>Скорректированный на множественность уровень значимости.</summary>
    public double AdjustedAlpha { get; init; }

    /// <summary>Метрика непрерывная, а не доля.</summary>
    public bool IsContinuous { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool longRun = DailyTraffic > 0 && DaysRequired > 28;
        bool tinyEffect = MinimumDetectableEffect < 0.02;

        return new InterpretationBuilder("Размер выборки эксперимента")
            .Summary($"Чтобы обнаружить относительный эффект {Fmt.Pct(MinimumDetectableEffect)} " +
                     $"при базовом уровне {(IsContinuous ? Fmt.Num(Baseline) : Fmt.Pct(Baseline))}, " +
                     $"нужно {Fmt.Int(PerVariant)} наблюдений на вариант, всего {Fmt.Int(Total)}" +
                     (DailyTraffic > 0 ? $", это {Fmt.Num(DaysRequired, 1)} суток." : "."))
            .Metric("На вариант", PerVariant, "наблюдений", "минимальный размер группы",
                MetricQuality.Neutral, 0)
            .Metric("Всего", Total, "наблюдений", $"{Variants} варианта, включая контроль",
                MetricQuality.Neutral, 0)
            .Metric("Длительность", DailyTraffic > 0 ? Fmt.Num(DaysRequired, 1) : "не рассчитана",
                DailyTraffic > 0 ? "суток" : null, "при заданном трафике",
                longRun ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Уровень значимости", AdjustedAlpha, null,
                Variants > 2 ? "с поправкой на множественность сравнений" : "без поправки",
                MetricQuality.Neutral, 4)
            .Metric("Мощность", Fmt.Pct(Power), null, "вероятность обнаружить эффект, если он есть")
            .FindingIf(Variants > 2,
                $"Вариантов больше двух, поэтому уровень значимости ужесточён до {Fmt.Num(AdjustedAlpha, 4)} " +
                "поправкой Бонферрони. Каждый дополнительный вариант увеличивает требуемую выборку.")
            .FindingIf(tinyEffect,
                "Заявленный эффект очень мал. Проверьте, что он вообще имеет практический смысл: " +
                "обнаружить его дороже, чем стоит выигрыш от такого изменения.")
            .WarningIf(longRun,
                $"Эксперимент займёт {Fmt.Num(DaysRequired, 0)} суток. За такой срок меняется состав " +
                "аудитории и внешние условия, и рандомизация перестаёт защищать от смещения.")
            .WarningIf(DailyTraffic > 0 && DaysRequired < 7,
                "Срок меньше недели: недельная сезонность не усреднится, результат будет смещён " +
                "днями недели, попавшими в эксперимент.")
            .Warning("Расчёт предполагает фиксированный горизонт. Подглядывание в промежуточные " +
                     "результаты с остановкой при достижении значимости раздувает ошибку первого " +
                     "рода в разы — для этого нужен последовательный критерий.")
            .RecommendationIf(longRun,
                "Сократите требуемую выборку методом CUPED: он уменьшает дисперсию за счёт " +
                "предэкспериментальных данных без изменения дизайна.")
            .Recommendation("Зафиксируйте размер выборки и метрику до запуска — иначе " +
                            "рассчитанные вероятности ошибок не имеют силы.")
            .Build();
    }
}

/// <summary>
/// Планирование эксперимента: размер выборки, минимальный обнаруживаемый
/// эффект и длительность.
/// </summary>
/// <remarks>
/// Расчёт до запуска решает две задачи. Первая очевидна: понять, хватит ли
/// трафика. Вторая важнее — увидеть, что эксперимент на доступной выборке
/// способен обнаружить только эффект в 15 %, а обсуждаемое изменение даёт
/// от силы 2 %. Такой эксперимент бессмысленно запускать: его отрицательный
/// результат ничего не докажет.
/// </remarks>
public static class ExperimentDesign
{
    /// <summary>Размер выборки для сравнения долей.</summary>
    /// <param name="baselineRate">Базовая конверсия контрольной группы.</param>
    /// <param name="relativeEffect">Относительный эффект, который нужно обнаружить.</param>
    /// <param name="alpha">Уровень значимости.</param>
    /// <param name="power">Требуемая мощность.</param>
    /// <param name="variants">Число вариантов, включая контроль.</param>
    /// <param name="dailyTraffic">Суточный трафик; 0 — не считать длительность.</param>
    /// <returns>Требуемый размер выборки и его разбор.</returns>
    /// <exception cref="ArgumentException">Некорректные вероятности.</exception>
    public static SampleSizeResult ForProportions(
        double baselineRate, double relativeEffect,
        double alpha = 0.05, double power = 0.8, int variants = 2, double dailyTraffic = 0)
    {
        if (baselineRate is <= 0 or >= 1)
            throw new ArgumentException("Базовая конверсия должна лежать в интервале (0; 1).", nameof(baselineRate));
        if (relativeEffect <= 0)
            throw new ArgumentException("Эффект должен быть положительным.", nameof(relativeEffect));

        double adjustedAlpha = AdjustAlpha(alpha, variants);
        double treatmentRate = Math.Min(baselineRate * (1 + relativeEffect), 0.999999);

        double za = EconMath.NormalInv(1 - (adjustedAlpha / 2));
        double zb = EconMath.NormalInv(power);

        double variance = (baselineRate * (1 - baselineRate)) + (treatmentRate * (1 - treatmentRate));
        double delta = treatmentRate - baselineRate;

        int perVariant = (int)Math.Ceiling(Math.Pow(za + zb, 2) * variance / (delta * delta));

        return Build(baselineRate, relativeEffect, alpha, adjustedAlpha, power,
            perVariant, variants, dailyTraffic, isContinuous: false);
    }

    /// <summary>Размер выборки для сравнения средних.</summary>
    /// <param name="baselineMean">Среднее контрольной группы.</param>
    /// <param name="standardDeviation">Стандартное отклонение метрики.</param>
    /// <param name="relativeEffect">Относительный эффект, который нужно обнаружить.</param>
    /// <param name="alpha">Уровень значимости.</param>
    /// <param name="power">Требуемая мощность.</param>
    /// <param name="variants">Число вариантов, включая контроль.</param>
    /// <param name="dailyTraffic">Суточный трафик; 0 — не считать длительность.</param>
    /// <returns>Требуемый размер выборки и его разбор.</returns>
    /// <exception cref="ArgumentException">Некорректные параметры.</exception>
    public static SampleSizeResult ForMeans(
        double baselineMean, double standardDeviation, double relativeEffect,
        double alpha = 0.05, double power = 0.8, int variants = 2, double dailyTraffic = 0)
    {
        if (standardDeviation <= 0)
            throw new ArgumentException("Стандартное отклонение должно быть положительным.",
                nameof(standardDeviation));
        if (relativeEffect <= 0)
            throw new ArgumentException("Эффект должен быть положительным.", nameof(relativeEffect));

        double adjustedAlpha = AdjustAlpha(alpha, variants);
        double za = EconMath.NormalInv(1 - (adjustedAlpha / 2));
        double zb = EconMath.NormalInv(power);
        double delta = Math.Abs(baselineMean * relativeEffect);

        int perVariant = (int)Math.Ceiling(
            2 * Math.Pow(za + zb, 2) * standardDeviation * standardDeviation / (delta * delta));

        return Build(baselineMean, relativeEffect, alpha, adjustedAlpha, power,
            perVariant, variants, dailyTraffic, isContinuous: true);
    }

    /// <summary>
    /// Минимальный эффект, обнаруживаемый на заданной выборке.
    /// </summary>
    /// <param name="baselineRate">Базовая конверсия.</param>
    /// <param name="perVariant">Размер группы.</param>
    /// <param name="alpha">Уровень значимости.</param>
    /// <param name="power">Мощность.</param>
    /// <returns>Относительный эффект, который эксперимент способен обнаружить.</returns>
    /// <remarks>
    /// Абсолютный эффект зависит от дисперсии, а та — от самого эффекта,
    /// поэтому решение ищется простой итерацией: два-три шага дают точность
    /// заведомо выше практической.
    /// </remarks>
    public static double MinimumDetectableEffect(
        double baselineRate, int perVariant, double alpha = 0.05, double power = 0.8)
    {
        if (baselineRate is <= 0 or >= 1)
            throw new ArgumentException("Базовая конверсия должна лежать в интервале (0; 1).", nameof(baselineRate));
        if (perVariant < 2)
            throw new ArgumentException("Размер группы должен быть не меньше двух.", nameof(perVariant));

        double za = EconMath.NormalInv(1 - (alpha / 2));
        double zb = EconMath.NormalInv(power);
        double delta = 0;

        for (int iteration = 0; iteration < 8; iteration++)
        {
            double treatment = EconMath.Clamp(baselineRate + delta, 1e-6, 1 - 1e-6);
            double variance = (baselineRate * (1 - baselineRate)) + (treatment * (1 - treatment));
            delta = (za + zb) * Math.Sqrt(variance / perVariant);
        }

        return delta / baselineRate;
    }

    private static double AdjustAlpha(double alpha, int variants) =>
        variants > 2 ? alpha / (variants - 1) : alpha;

    private static SampleSizeResult Build(
        double baseline, double effect, double alpha, double adjustedAlpha, double power,
        int perVariant, int variants, double dailyTraffic, bool isContinuous)
    {
        int total = perVariant * Math.Max(variants, 2);

        return new SampleSizeResult
        {
            Baseline = baseline,
            MinimumDetectableEffect = effect,
            Alpha = alpha,
            AdjustedAlpha = adjustedAlpha,
            Power = power,
            PerVariant = perVariant,
            Total = total,
            Variants = Math.Max(variants, 2),
            DailyTraffic = dailyTraffic,
            DaysRequired = dailyTraffic > 0 ? total / dailyTraffic : 0,
            IsContinuous = isContinuous,
        };
    }
}
