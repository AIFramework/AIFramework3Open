using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Credit;

/// <summary>Стадия обесценения по МСФО 9.</summary>
public enum CreditStage
{
    /// <summary>Стадия 1: значимого роста кредитного риска не произошло, резерв на 12 месяцев.</summary>
    Performing = 1,

    /// <summary>Стадия 2: значимый рост кредитного риска, резерв на весь срок.</summary>
    UnderPerforming = 2,

    /// <summary>Стадия 3: кредитно-обесцененный актив, резерв на весь срок.</summary>
    NonPerforming = 3,
}

/// <summary>Кредитная экспозиция для расчёта ожидаемых кредитных убытков.</summary>
public sealed record CreditExposure
{
    /// <summary>Идентификатор договора.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Величина под риском дефолта (EAD).</summary>
    public double ExposureAtDefault { get; init; }

    /// <summary>Годовая вероятность дефолта на отчётную дату.</summary>
    public double ProbabilityOfDefault { get; init; }

    /// <summary>Годовая вероятность дефолта при первоначальном признании.</summary>
    public double ProbabilityOfDefaultAtOrigination { get; init; }

    /// <summary>Потери при дефолте (LGD), доля от экспозиции.</summary>
    public double LossGivenDefault { get; init; } = 0.45;

    /// <summary>Эффективная процентная ставка, годовая, для дисконтирования.</summary>
    public double EffectiveInterestRate { get; init; }

    /// <summary>Оставшийся срок договора в месяцах.</summary>
    public int RemainingMonths { get; init; } = 12;

    /// <summary>Число дней просрочки на отчётную дату.</summary>
    public int DaysPastDue { get; init; }

    /// <summary>Признак кредитного обесценения: дефолт уже произошёл.</summary>
    public bool IsCreditImpaired { get; init; }

    /// <summary>Сегмент портфеля для группировки отчётности.</summary>
    public string Segment { get; init; } = "портфель";
}

/// <summary>Макроэкономический сценарий прогнозной информации.</summary>
/// <param name="Name">Название сценария.</param>
/// <param name="Probability">Вероятность сценария.</param>
/// <param name="PdMultiplier">Множитель вероятности дефолта.</param>
/// <param name="LgdMultiplier">Множитель потерь при дефолте.</param>
public sealed record MacroScenario(string Name, double Probability, double PdMultiplier, double LgdMultiplier = 1.0);

/// <summary>Ожидаемые кредитные убытки по одной экспозиции.</summary>
public sealed record ExposureEcl
{
    /// <summary>Идентификатор договора.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Сегмент портфеля.</summary>
    public string Segment { get; init; } = string.Empty;

    /// <summary>Присвоенная стадия.</summary>
    public CreditStage Stage { get; init; }

    /// <summary>Причина отнесения к стадии.</summary>
    public string StageReason { get; init; } = string.Empty;

    /// <summary>Величина под риском дефолта.</summary>
    public double ExposureAtDefault { get; init; }

    /// <summary>Убыток за 12 месяцев, взвешенный по сценариям.</summary>
    public double Ecl12Month { get; init; }

    /// <summary>Убыток за весь срок, взвешенный по сценариям.</summary>
    public double EclLifetime { get; init; }

    /// <summary>Признанный резерв согласно стадии.</summary>
    public double Ecl { get; init; }

    /// <summary>Отношение резерва к экспозиции.</summary>
    public double CoverageRatio => ExposureAtDefault > 0 ? Ecl / ExposureAtDefault : 0;

    /// <summary>Кривая предельных вероятностей дефолта по месяцам в базовом сценарии.</summary>
    public IReadOnlyList<double> MarginalDefaultCurve { get; init; } = [];
}

/// <summary>Свод резерва по стадии.</summary>
/// <param name="Stage">Стадия.</param>
/// <param name="Count">Число договоров.</param>
/// <param name="ExposureAtDefault">Суммарная экспозиция.</param>
/// <param name="Ecl">Суммарный резерв.</param>
/// <param name="CoverageRatio">Покрытие резервом.</param>
public sealed record StageSummary(
    CreditStage Stage, int Count, double ExposureAtDefault, double Ecl, double CoverageRatio);

/// <summary>Резерв в отдельном макросценарии.</summary>
/// <param name="Name">Название сценария.</param>
/// <param name="Probability">Нормированная вероятность сценария.</param>
/// <param name="Ecl">Резерв в сценарии.</param>
/// <param name="CoverageRatio">Покрытие резервом в сценарии.</param>
public sealed record ScenarioEcl(string Name, double Probability, double Ecl, double CoverageRatio);

/// <summary>Итог расчёта ожидаемых кредитных убытков по портфелю.</summary>
public sealed record EclResult : IInterpretable
{
    /// <summary>Резервы по каждой экспозиции.</summary>
    public IReadOnlyList<ExposureEcl> Exposures { get; init; } = [];

    /// <summary>Свод по стадиям.</summary>
    public IReadOnlyList<StageSummary> Stages { get; init; } = [];

    /// <summary>Резерв в каждом макросценарии.</summary>
    public IReadOnlyList<ScenarioEcl> Scenarios { get; init; } = [];

    /// <summary>Суммарная экспозиция портфеля.</summary>
    public double TotalExposure { get; init; }

    /// <summary>Суммарный признанный резерв.</summary>
    public double TotalEcl { get; init; }

    /// <summary>Резерв, если бы весь портфель считался по 12-месячному горизонту.</summary>
    public double TotalEcl12Month { get; init; }

    /// <summary>Резерв, если бы весь портфель считался на весь срок.</summary>
    public double TotalEclLifetime { get; init; }

    /// <summary>Покрытие портфеля резервом.</summary>
    public double CoverageRatio => TotalExposure > 0 ? TotalEcl / TotalExposure : 0;

    /// <summary>Вклад стадирования в размер резерва сверх 12-месячной базы.</summary>
    public double StagingEffect => TotalEcl - TotalEcl12Month;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        StageSummary? stage2 = Stages.FirstOrDefault(s => s.Stage == CreditStage.UnderPerforming);
        StageSummary? stage3 = Stages.FirstOrDefault(s => s.Stage == CreditStage.NonPerforming);

        double stage2Share = TotalExposure > 0 ? (stage2?.ExposureAtDefault ?? 0) / TotalExposure : 0;
        double stage3Share = TotalExposure > 0 ? (stage3?.ExposureAtDefault ?? 0) / TotalExposure : 0;

        ScenarioEcl? worst = Scenarios.OrderByDescending(s => s.Ecl).FirstOrDefault();
        ScenarioEcl? best = Scenarios.OrderBy(s => s.Ecl).FirstOrDefault();
        double scenarioSpread = TotalExposure > 0 && worst is not null && best is not null
            ? (worst.Ecl - best.Ecl) / TotalExposure
            : 0;
        double stagingLift = TotalEcl12Month > 0 ? StagingEffect / TotalEcl12Month : 0;

        var builder = new InterpretationBuilder("Ожидаемые кредитные убытки по МСФО 9")
            .Summary($"Портфель из {Exposures.Count} договоров на {Fmt.Money(TotalExposure)} " +
                     $"требует резерва {Fmt.Money(TotalEcl)}, покрытие {Fmt.Pct(CoverageRatio, 2)}. " +
                     $"Из этой суммы {Fmt.Money(StagingEffect)} даёт перевод части портфеля " +
                     "на расчёт убытков за весь срок.")
            .Metric("Экспозиция", Fmt.Money(TotalExposure), null, "суммарная величина под риском дефолта")
            .Metric("Резерв", Fmt.Money(TotalEcl), null,
                $"покрытие {Fmt.Pct(CoverageRatio, 2)}",
                CoverageRatio > 0.1 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Резерв на 12 месяцев", Fmt.Money(TotalEcl12Month), null,
                "если бы весь портфель остался на стадии 1")
            .Metric("Резерв на весь срок", Fmt.Money(TotalEclLifetime), null,
                "верхняя граница при переводе всего портфеля на стадию 2")
            .Metric("Эффект стадирования", Fmt.Money(StagingEffect), null,
                $"прирост к базе на {Fmt.Pct(stagingLift, 0)}",
                stagingLift > 1 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Доля стадии 2", stage2Share, null,
                $"{stage2?.Count ?? 0} договоров со значимым ростом риска",
                stage2Share > 0.2 ? MetricQuality.Warning : MetricQuality.Neutral, 3)
            .Metric("Доля стадии 3", stage3Share, null,
                $"{stage3?.Count ?? 0} кредитно-обесцененных договоров",
                stage3Share > 0.05 ? MetricQuality.Critical : MetricQuality.Neutral, 3);

        foreach (ScenarioEcl scenario in Scenarios)
        {
            builder.Metric($"Сценарий: {scenario.Name}", Fmt.Money(scenario.Ecl), null,
                $"вероятность {Fmt.Pct(scenario.Probability, 0)}, покрытие {Fmt.Pct(scenario.CoverageRatio, 2)}");
        }

        return builder
            .Finding($"Перевод договоров на стадии 2 и 3 добавляет к резерву {Fmt.Money(StagingEffect)}. " +
                     "Волатильность резерва в отчётности почти всегда определяется стадированием, " +
                     "а не точностью самой модели вероятности дефолта.")
            .FindingIf(worst is not null && best is not null,
                $"Разброс между сценариями «{worst?.Name}» и «{best?.Name}» составляет " +
                $"{Fmt.Pct(scenarioSpread, 2)} экспозиции — это цена макронеопределённости в резерве.")
            .FindingIf(stage3Share > 0,
                $"Кредитно-обесцененная часть портфеля покрыта резервом на " +
                $"{Fmt.Pct(stage3?.CoverageRatio ?? 0, 1)}.")
            .FindingIf(stage3Share <= 0,
                "Кредитно-обесцененных договоров в портфеле нет, весь резерв формируется " +
                "ожиданиями, а не фактическими дефолтами.")
            .WarningIf(stage2Share > 0.2,
                $"На стадии 2 находится {Fmt.Pct(stage2Share, 1)} портфеля. Массовая миграция " +
                "на стадию 2 — типичный источник скачка резерва между отчётными датами.")
            .WarningIf(Scenarios.Count < 3,
                "Задано менее трёх макросценариев. Стандарт требует отражать нелинейность " +
                "убытков по макроэкономике, а для этого нужны как минимум базовый, " +
                "оптимистичный и стрессовый сценарии.")
            .Warning("Расчёт опирается на заданные вероятности дефолта, потери при дефолте " +
                     "и срок до погашения. Резерв чувствителен к этим входным данным сильнее, " +
                     "чем к формуле дисконтирования, поэтому обосновывать нужно именно их.")
            .Recommendation("Раскрывайте эффект стадирования отдельно от эффекта изменения " +
                            "вероятности дефолта: и аудитор, и аналитик хотят видеть, что именно " +
                            "изменило резерв за период.")
            .Recommendation("Проверьте критерий значимого роста кредитного риска на исторических " +
                            "данных: слишком чувствительный порог заставляет договоры «мигать» " +
                            "между стадиями от даты к дате.")
            .Build();
    }
}

/// <summary>Настройки расчёта ожидаемых кредитных убытков.</summary>
public sealed record Ifrs9Options
{
    /// <summary>Во сколько раз должна вырасти вероятность дефолта для перевода на стадию 2.</summary>
    public double SicrRelativeThreshold { get; init; } = 2.0;

    /// <summary>Минимальный абсолютный прирост вероятности дефолта для перевода на стадию 2.</summary>
    public double SicrAbsoluteThreshold { get; init; } = 0.005;

    /// <summary>Число дней просрочки, после которого договор переводится на стадию 2.</summary>
    public int DaysPastDueStage2 { get; init; } = 30;

    /// <summary>Число дней просрочки, после которого договор признаётся обесцененным.</summary>
    public int DaysPastDueStage3 { get; init; } = 90;
}

/// <summary>
/// Расчёт ожидаемых кредитных убытков по МСФО 9 (IFRS 9).
/// </summary>
/// <remarks>
/// <para>
/// Стандарт заменил модель понесённых убытков на модель ожидаемых: резерв
/// создаётся в момент выдачи, а не в момент просрочки. Величина резерва
/// определяется стадией договора — на стадии 1 признаются убытки от дефолтов
/// ближайших двенадцати месяцев, на стадиях 2 и 3 за весь оставшийся срок.
/// </para>
/// <para>
/// Расчёт идёт по месяцам жизни договора. Годовая вероятность дефолта
/// раскладывается в помесячную интенсивность, из неё строится кривая выживания
/// и предельных вероятностей дефолта, а каждый месячный убыток дисконтируется
/// по эффективной ставке:
/// </para>
/// <code>
/// h = 1 - (1 - PD)^(1/12)
/// S(t) = (1 - h)^t
/// marginalPD(t) = S(t-1) * h
/// ECL = sum_t marginalPD(t) * LGD * EAD / (1 + EIR)^(t/12)
/// </code>
/// <para>
/// Прогнозная информация вводится набором макросценариев с вероятностями:
/// резерв считается в каждом сценарии и взвешивается. Это принципиальный момент
/// стандарта — из-за выпуклости убытков по вероятности дефолта взвешенный резерв
/// оказывается выше резерва, посчитанного по одному среднему сценарию.
/// </para>
/// </remarks>
public static class Ifrs9
{
    /// <summary>Базовый набор сценариев: базовый, оптимистичный и стрессовый.</summary>
    /// <returns>Три сценария с суммарной вероятностью единица.</returns>
    public static IReadOnlyList<MacroScenario> DefaultScenarios() =>
    [
        new MacroScenario("Базовый", 0.55, 1.0, 1.0),
        new MacroScenario("Оптимистичный", 0.20, 0.7, 0.9),
        new MacroScenario("Стрессовый", 0.25, 1.8, 1.2),
    ];

    /// <summary>Рассчитывает резерв по портфелю.</summary>
    /// <param name="exposures">Кредитные экспозиции.</param>
    /// <param name="scenarios">Макросценарии; при <c>null</c> берётся базовый набор.</param>
    /// <param name="options">Настройки стадирования; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Резервы по договорам, свод по стадиям и по сценариям.</returns>
    /// <exception cref="ArgumentNullException">Экспозиции не заданы.</exception>
    /// <exception cref="ArgumentException">Портфель пуст или вероятности сценариев неположительны.</exception>
    public static EclResult Compute(
        IReadOnlyList<CreditExposure> exposures,
        IReadOnlyList<MacroScenario>? scenarios = null,
        Ifrs9Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(exposures);
        if (exposures.Count == 0) throw new ArgumentException("Портфель пуст.", nameof(exposures));

        options ??= new Ifrs9Options();
        IReadOnlyList<MacroScenario> list = scenarios is { Count: > 0 } ? scenarios : DefaultScenarios();

        double weightSum = list.Sum(s => s.Probability);
        if (weightSum <= 0)
            throw new ArgumentException("Сумма вероятностей сценариев должна быть положительной.", nameof(scenarios));

        var results = new List<ExposureEcl>(exposures.Count);
        var scenarioTotals = new double[list.Count];

        foreach (CreditExposure exposure in exposures)
        {
            (CreditStage stage, string reason) = AssignStage(exposure, options);

            int months = Math.Max(1, exposure.RemainingMonths);
            bool twelveMonthBasis = stage == CreditStage.Performing;

            double weighted12 = 0, weightedLifetime = 0;
            double[] curve = [];

            for (int s = 0; s < list.Count; s++)
            {
                MacroScenario scenario = list[s];
                double weight = scenario.Probability / weightSum;

                double pd = EconMath.Clamp(exposure.ProbabilityOfDefault * scenario.PdMultiplier, 0, 0.999);
                double lgd = EconMath.Clamp(exposure.LossGivenDefault * scenario.LgdMultiplier, 0, 1);

                double[] marginal = MarginalCurve(pd, months);
                if (s == 0) curve = marginal;

                double ecl12 = Discounted(marginal, Math.Min(12, months), lgd, exposure);
                double eclLife = Discounted(marginal, months, lgd, exposure);

                weighted12 += weight * ecl12;
                weightedLifetime += weight * eclLife;
                scenarioTotals[s] += twelveMonthBasis ? ecl12 : eclLife;
            }

            results.Add(new ExposureEcl
            {
                Id = exposure.Id,
                Segment = exposure.Segment,
                Stage = stage,
                StageReason = reason,
                ExposureAtDefault = exposure.ExposureAtDefault,
                Ecl12Month = weighted12,
                EclLifetime = weightedLifetime,
                Ecl = twelveMonthBasis ? weighted12 : weightedLifetime,
                MarginalDefaultCurve = curve,
            });
        }

        double totalEad = results.Sum(r => r.ExposureAtDefault);

        var stages = results
            .GroupBy(r => r.Stage)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                double ead = g.Sum(r => r.ExposureAtDefault);
                double ecl = g.Sum(r => r.Ecl);
                return new StageSummary(g.Key, g.Count(), ead, ecl, ead > 0 ? ecl / ead : 0);
            })
            .ToList();

        var scenarioResults = new List<ScenarioEcl>(list.Count);
        for (int s = 0; s < list.Count; s++)
        {
            scenarioResults.Add(new ScenarioEcl(
                list[s].Name,
                list[s].Probability / weightSum,
                scenarioTotals[s],
                totalEad > 0 ? scenarioTotals[s] / totalEad : 0));
        }

        return new EclResult
        {
            Exposures = results,
            Stages = stages,
            Scenarios = scenarioResults,
            TotalExposure = totalEad,
            TotalEcl = results.Sum(r => r.Ecl),
            TotalEcl12Month = results.Sum(r => r.Ecl12Month),
            TotalEclLifetime = results.Sum(r => r.EclLifetime),
        };
    }

    /// <summary>Определяет стадию обесценения договора.</summary>
    /// <param name="exposure">Экспозиция.</param>
    /// <param name="options">Настройки стадирования; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Стадия и причина её присвоения.</returns>
    /// <exception cref="ArgumentNullException">Экспозиция не задана.</exception>
    public static (CreditStage Stage, string Reason) AssignStage(
        CreditExposure exposure, Ifrs9Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(exposure);
        options ??= new Ifrs9Options();

        if (exposure.IsCreditImpaired)
            return (CreditStage.NonPerforming, "договор признан кредитно-обесцененным");

        if (exposure.DaysPastDue >= options.DaysPastDueStage3)
            return (CreditStage.NonPerforming, $"просрочка {exposure.DaysPastDue} дней");

        if (exposure.DaysPastDue >= options.DaysPastDueStage2)
            return (CreditStage.UnderPerforming, $"просрочка {exposure.DaysPastDue} дней");

        double origination = exposure.ProbabilityOfDefaultAtOrigination;
        if (origination > 0)
        {
            double ratio = exposure.ProbabilityOfDefault / origination;
            double absolute = exposure.ProbabilityOfDefault - origination;

            if (ratio >= options.SicrRelativeThreshold && absolute >= options.SicrAbsoluteThreshold)
                return (CreditStage.UnderPerforming,
                    $"вероятность дефолта выросла в {Fmt.Num(ratio, 1)} раза с момента выдачи");
        }

        return (CreditStage.Performing, "значимого роста кредитного риска нет");
    }

    /// <summary>Строит кривую предельных вероятностей дефолта по месяцам.</summary>
    /// <param name="annualPd">Годовая вероятность дефолта.</param>
    /// <param name="months">Число месяцев.</param>
    /// <returns>Безусловная вероятность дефолта в каждом месяце.</returns>
    private static double[] MarginalCurve(double annualPd, int months)
    {
        double hazard = 1 - Math.Pow(1 - annualPd, 1.0 / 12.0);
        var curve = new double[months];
        double survival = 1.0;

        for (int t = 0; t < months; t++)
        {
            curve[t] = survival * hazard;
            survival *= 1 - hazard;
        }

        return curve;
    }

    /// <summary>Дисконтированная сумма убытков за первые месяцы кривой.</summary>
    private static double Discounted(
        IReadOnlyList<double> marginal, int months, double lgd, CreditExposure exposure)
    {
        double monthlyRate = Math.Pow(1 + Math.Max(0, exposure.EffectiveInterestRate), 1.0 / 12.0);
        double total = 0;

        for (int t = 0; t < Math.Min(months, marginal.Count); t++)
            total += marginal[t] * lgd * exposure.ExposureAtDefault / Math.Pow(monthlyRate, t + 1);

        return total;
    }
}
