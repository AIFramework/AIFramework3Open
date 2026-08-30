using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Credit;

/// <summary>Винтаж выдач: когорта договоров одного периода выдачи.</summary>
/// <param name="Name">Название винтажа, обычно месяц или квартал выдачи.</param>
/// <param name="OriginationAmount">Сумма выдач когорты.</param>
/// <param name="CumulativeLossRate">Накопленная доля потерь по возрасту в месяцах.</param>
public sealed record VintageCohort(
    string Name, double OriginationAmount, IReadOnlyList<double> CumulativeLossRate);

/// <summary>Характеристика винтажа относительно кривой созревания портфеля.</summary>
/// <param name="Name">Название винтажа.</param>
/// <param name="OriginationAmount">Сумма выдач.</param>
/// <param name="Age">Наблюдённый возраст в месяцах.</param>
/// <param name="LossAtCommonAge">Потери на общем для всех винтажей возрасте.</param>
/// <param name="LatestLoss">Последняя наблюдённая накопленная доля потерь.</param>
/// <param name="ProjectedLifetimeLoss">Прогноз потерь за весь срок по кривой созревания.</param>
/// <param name="RelativeToAverage">Отношение потерь на общем возрасте к среднему по портфелю.</param>
public sealed record VintageProfile(
    string Name, double OriginationAmount, int Age, double LossAtCommonAge,
    double LatestLoss, double ProjectedLifetimeLoss, double RelativeToAverage);

/// <summary>Итог винтажного анализа портфеля.</summary>
public sealed record VintageResult : IInterpretable
{
    /// <summary>Характеристики винтажей в порядке выдачи.</summary>
    public IReadOnlyList<VintageProfile> Vintages { get; init; } = [];

    /// <summary>Кривая созревания: средняя накопленная доля потерь по возрасту.</summary>
    public IReadOnlyList<double> MaturityCurve { get; init; } = [];

    /// <summary>Приростные потери по возрасту: где именно портфель теряет деньги.</summary>
    public IReadOnlyList<double> MarginalCurve { get; init; } = [];

    /// <summary>Возраст, на котором сопоставимы все винтажи.</summary>
    public int CommonAge { get; init; }

    /// <summary>Максимальный наблюдённый возраст.</summary>
    public int MaxAge { get; init; }

    /// <summary>Изменение потерь на общем возрасте за один шаг винтажа.</summary>
    public double QualityTrend { get; init; }

    /// <summary>Уровень значимости тренда качества выдач.</summary>
    public double TrendPValue { get; init; } = 1;

    /// <summary>Возраст, к которому реализуется половина итоговых потерь.</summary>
    public int HalfLossAge { get; init; }

    /// <summary>Взвешенный прогноз потерь по всему портфелю выдач.</summary>
    public double ProjectedPortfolioLoss { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        VintageProfile? worst = Vintages.OrderByDescending(v => v.LossAtCommonAge).FirstOrDefault();
        VintageProfile? best = Vintages.OrderBy(v => v.LossAtCommonAge).FirstOrDefault();
        VintageProfile? latest = Vintages.LastOrDefault();

        double averageAtCommon = Vintages.Count > 0 ? Vintages.Average(v => v.LossAtCommonAge) : 0;
        bool trendSignificant = TrendPValue < 0.05;
        double maturityShare = MaturityCurve.Count > 0 && MaturityCurve[^1] > 0 && CommonAge > 0
            ? MaturityCurve[Math.Min(CommonAge, MaturityCurve.Count) - 1] / MaturityCurve[^1]
            : 0;

        var builder = new InterpretationBuilder("Винтажный анализ портфеля")
            .Summary($"Сопоставлено {Vintages.Count} винтажей на общем возрасте {CommonAge} мес. " +
                     $"Средние потери на этом возрасте {Fmt.Pct(averageAtCommon, 2)}, прогноз " +
                     $"потерь за весь срок по портфелю — {Fmt.Money(ProjectedPortfolioLoss)}. " +
                     $"Качество выдач меняется на {Fmt.Pct(QualityTrend, 3)} за винтаж " +
                     $"({(trendSignificant ? "тренд значим" : "тренд незначим")}).")
            .Metric("Средние потери на общем возрасте", averageAtCommon, null,
                $"возраст сопоставления {CommonAge} мес.",
                averageAtCommon > 0.05 ? MetricQuality.Warning : MetricQuality.Neutral, 4)
            .Metric("Тренд качества", QualityTrend, null,
                QualityTrend > 0 ? "новые выдачи хуже старых" : "новые выдачи лучше старых",
                QualityTrend > 0 && trendSignificant ? MetricQuality.Critical
                    : QualityTrend < 0 && trendSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Значимость тренда", TrendPValue, null,
                trendSignificant ? "изменение качества не случайно" : "изменения в пределах шума",
                MetricQuality.Neutral, 3)
            .Metric("Половина потерь реализуется к", HalfLossAge, "мес.",
                "срок созревания риска: до него сравнивать винтажи бессмысленно",
                MetricQuality.Neutral, 0)
            .Metric("Зрелость на общем возрасте", maturityShare, null,
                "какая доля итоговых потерь уже видна на возрасте сопоставления",
                maturityShare < 0.5 ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Прогноз потерь портфеля", Fmt.Money(ProjectedPortfolioLoss), null,
                "экстраполяция незрелых винтажей по кривой созревания");

        foreach (VintageProfile vintage in Vintages)
        {
            builder.Metric($"Винтаж {vintage.Name}", vintage.LossAtCommonAge, null,
                $"возраст {vintage.Age} мес., прогноз за срок {Fmt.Pct(vintage.ProjectedLifetimeLoss, 2)}, " +
                $"относительно среднего {Fmt.Num(vintage.RelativeToAverage, 2)}",
                MetricQuality.Unknown, 4);
        }

        return builder
            .FindingIf(worst is not null && best is not null,
                $"Разброс качества между винтажами «{best?.Name}» и «{worst?.Name}» — " +
                $"{Fmt.Pct(best?.LossAtCommonAge ?? 0, 2)} против {Fmt.Pct(worst?.LossAtCommonAge ?? 0, 2)} " +
                "на одном возрасте. Такой разрыв обычно объясняется изменением критериев " +
                "одобрения или сменой каналов привлечения, а не макроэкономикой.")
            .FindingIf(latest is not null && latest.RelativeToAverage > 1.2,
                $"Последний винтаж «{latest?.Name}» на общем возрасте хуже среднего в " +
                $"{Fmt.Num(latest?.RelativeToAverage ?? 0, 2)} раза. Это самый ранний измеримый " +
                "сигнал о том, что политика выдач ослабла.")
            .Finding($"Половина итоговых потерь реализуется к {HalfLossAge} месяцу жизни договора. " +
                     "Оценивать винтаж раньше этого возраста можно только через кривую созревания.")
            .WarningIf(CommonAge < 6,
                $"Общий возраст сопоставления всего {CommonAge} мес. На таком горизонте различия " +
                "между винтажами чаще отражают шум и сезонность выдач, а не качество.")
            .WarningIf(Vintages.Any(v => v.Age < MaxAge / 2),
                "Часть винтажей заметно моложе остальных, и их прогноз получен экстраполяцией. " +
                "Точность такой оценки полностью определяется устойчивостью кривой созревания.")
            .Warning("Кривая созревания усредняет винтажи с разной макросредой. Если условия " +
                     "кредитования резко менялись, форма кривой у новых выдач будет другой, " +
                     "и экстраполяция даст смещённый прогноз.")
            .Recommendation("Сравнивайте винтажи только на одинаковом возрасте: сравнение " +
                            "накопленных потерь у выдач разной зрелости — самая частая ошибка " +
                            "в отчётности по портфелю.")
            .Recommendation("Разложите винтажи по каналам и продуктам: ухудшение почти всегда " +
                            "локализовано в одном сегменте, а не размазано по портфелю.")
            .Build();
    }
}

/// <summary>
/// Винтажный анализ: сравнение когорт выдач на одинаковом возрасте.
/// </summary>
/// <remarks>
/// <para>
/// Портфельная доля просрочки смешивает два эффекта — качество выдач и
/// возрастную структуру портфеля. Быстро растущий портфель выглядит здоровым
/// просто потому, что большая часть договоров ещё не дожила до возраста, на
/// котором происходят дефолты. Винтажный анализ разделяет эти эффекты: каждая
/// когорта выдач наблюдается по возрасту от момента выдачи.
/// </para>
/// <para>
/// Кривая созревания — усреднённая по винтажам накопленная доля потерь по
/// возрасту. Она позволяет экстраполировать молодые винтажи:
/// </para>
/// <code>
/// projected(v) = loss(v, age_v) * maturity(maxAge) / maturity(age_v)
/// </code>
/// <para>
/// Тренд качества оценивается регрессией потерь на общем возрасте по номеру
/// винтажа. Значимый положительный наклон означает, что политика выдач
/// ослабевает, и это видно за год до того, как ухудшение проявится в
/// портфельных показателях.
/// </para>
/// </remarks>
public static class VintageAnalysis
{
    /// <summary>Проводит винтажный анализ по когортам выдач.</summary>
    /// <param name="cohorts">Винтажи в хронологическом порядке выдачи.</param>
    /// <returns>Кривая созревания, тренд качества и прогноз потерь.</returns>
    /// <exception cref="ArgumentNullException">Когорты не заданы.</exception>
    /// <exception cref="ArgumentException">Когорт меньше двух или ряд потерь пуст.</exception>
    public static VintageResult Analyze(IReadOnlyList<VintageCohort> cohorts)
    {
        ArgumentNullException.ThrowIfNull(cohorts);

        if (cohorts.Count < 2)
            throw new ArgumentException("Нужно как минимум два винтажа.", nameof(cohorts));
        if (cohorts.Any(c => c.CumulativeLossRate is null || c.CumulativeLossRate.Count == 0))
            throw new ArgumentException("У каждого винтажа должен быть ряд накопленных потерь.", nameof(cohorts));

        int commonAge = cohorts.Min(c => c.CumulativeLossRate.Count);
        int maxAge = cohorts.Max(c => c.CumulativeLossRate.Count);

        var maturity = new double[maxAge];
        for (int age = 0; age < maxAge; age++)
        {
            double weight = 0, sum = 0;

            foreach (VintageCohort cohort in cohorts)
            {
                if (cohort.CumulativeLossRate.Count <= age) continue;

                sum += cohort.OriginationAmount * cohort.CumulativeLossRate[age];
                weight += cohort.OriginationAmount;
            }

            maturity[age] = weight > 0 ? sum / weight : age > 0 ? maturity[age - 1] : 0;
        }

        // Кривая созревания должна быть неубывающей: накопленные потери не возвращаются.
        for (int age = 1; age < maxAge; age++)
            maturity[age] = Math.Max(maturity[age], maturity[age - 1]);

        var marginal = new double[maxAge];
        for (int age = 0; age < maxAge; age++)
            marginal[age] = age == 0 ? maturity[0] : maturity[age] - maturity[age - 1];

        double terminal = maturity[maxAge - 1];
        int halfLossAge = maxAge;
        for (int age = 0; age < maxAge; age++)
        {
            if (terminal > 0 && maturity[age] >= terminal / 2)
            {
                halfLossAge = age + 1;
                break;
            }
        }

        double averageAtCommon = cohorts.Average(c => c.CumulativeLossRate[commonAge - 1]);

        var profiles = new List<VintageProfile>(cohorts.Count);
        double projectedPortfolio = 0;

        foreach (VintageCohort cohort in cohorts)
        {
            int age = cohort.CumulativeLossRate.Count;
            double latest = cohort.CumulativeLossRate[age - 1];
            double scale = maturity[age - 1] > 0 ? terminal / maturity[age - 1] : 1;
            double projected = latest * scale;

            profiles.Add(new VintageProfile(
                cohort.Name,
                cohort.OriginationAmount,
                age,
                cohort.CumulativeLossRate[commonAge - 1],
                latest,
                projected,
                averageAtCommon > 0 ? cohort.CumulativeLossRate[commonAge - 1] / averageAtCommon : 1));

            projectedPortfolio += cohort.OriginationAmount * projected;
        }

        (double slope, double pValue) = Trend(profiles);

        return new VintageResult
        {
            Vintages = profiles,
            MaturityCurve = maturity,
            MarginalCurve = marginal,
            CommonAge = commonAge,
            MaxAge = maxAge,
            QualityTrend = slope,
            TrendPValue = pValue,
            HalfLossAge = halfLossAge,
            ProjectedPortfolioLoss = projectedPortfolio,
        };
    }

    /// <summary>Строит матрицу «винтаж x возраст» для тепловой карты.</summary>
    /// <param name="cohorts">Винтажи.</param>
    /// <returns>Матрица накопленных потерь; незаполненные ячейки равны <see cref="double.NaN"/>.</returns>
    /// <exception cref="ArgumentNullException">Когорты не заданы.</exception>
    /// <exception cref="ArgumentException">Когорты пусты.</exception>
    public static Matrix Triangle(IReadOnlyList<VintageCohort> cohorts)
    {
        ArgumentNullException.ThrowIfNull(cohorts);
        if (cohorts.Count == 0) throw new ArgumentException("Список винтажей пуст.", nameof(cohorts));

        int maxAge = cohorts.Max(c => c.CumulativeLossRate.Count);
        var triangle = new Matrix(cohorts.Count, maxAge);

        for (int i = 0; i < cohorts.Count; i++)
            for (int age = 0; age < maxAge; age++)
                triangle[i, age] = age < cohorts[i].CumulativeLossRate.Count
                    ? cohorts[i].CumulativeLossRate[age]
                    : double.NaN;

        return triangle;
    }

    /// <summary>Оценивает наклон качества выдач по номеру винтажа.</summary>
    private static (double Slope, double PValue) Trend(IReadOnlyList<VintageProfile> profiles)
    {
        if (profiles.Count < 3) return (0, 1);

        var design = new double[profiles.Count, 2];
        var response = new double[profiles.Count];

        for (int i = 0; i < profiles.Count; i++)
        {
            design[i, 0] = 1;
            design[i, 1] = i;
            response[i] = profiles[i].LossAtCommonAge;
        }

        OlsFit? fit = Ols.Fit(design, response);
        return fit is null ? (0, 1) : (fit.Beta[1], fit.PValue(1));
    }
}
