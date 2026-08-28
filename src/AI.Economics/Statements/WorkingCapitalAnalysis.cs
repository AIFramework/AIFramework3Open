using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Statements;

/// <summary>Целевые сроки оборота для расчёта потенциала высвобождения денег.</summary>
public sealed record WorkingCapitalTargets
{
    /// <summary>Целевой период сбора дебиторской задолженности в днях.</summary>
    public double DaysSalesOutstanding { get; init; } = 40;

    /// <summary>Целевой период оборота запасов в днях.</summary>
    public double DaysInventoryOutstanding { get; init; } = 45;

    /// <summary>Целевой период оплаты поставщикам в днях.</summary>
    public double DaysPayablesOutstanding { get; init; } = 45;

    /// <summary>Стоимость финансирования оборотного капитала, годовая.</summary>
    public double CostOfFunding { get; init; } = 0.18;
}

/// <summary>Драйвер оборотного капитала и потенциал высвобождения денег по нему.</summary>
/// <param name="Name">Название драйвера.</param>
/// <param name="Days">Текущий срок в днях.</param>
/// <param name="TargetDays">Целевой срок в днях.</param>
/// <param name="AmountPerDay">Сколько денег стоит один день срока.</param>
/// <param name="CashImpact">Высвобождение денег при достижении цели; отрицательное значение означает отток.</param>
/// <param name="Comment">Пояснение к драйверу.</param>
public sealed record WorkingCapitalDriver(
    string Name, double Days, double TargetDays, double AmountPerDay, double CashImpact, string Comment);

/// <summary>Результат анализа оборотного капитала.</summary>
public sealed record WorkingCapitalResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Период сбора дебиторской задолженности в днях.</summary>
    public double DaysSalesOutstanding { get; init; }

    /// <summary>Период оборота запасов в днях.</summary>
    public double DaysInventoryOutstanding { get; init; }

    /// <summary>Период оплаты поставщикам в днях.</summary>
    public double DaysPayablesOutstanding { get; init; }

    /// <summary>Финансовый цикл в днях.</summary>
    public double CashConversionCycle { get; init; }

    /// <summary>Операционный цикл в днях: от закупки до получения денег.</summary>
    public double OperatingCycle { get; init; }

    /// <summary>Чистый оборотный капитал, связанный в цикле.</summary>
    public double WorkingCapital { get; init; }

    /// <summary>Оборотный капитал к выручке.</summary>
    public double WorkingCapitalToRevenue { get; init; }

    /// <summary>Драйверы цикла с потенциалом высвобождения.</summary>
    public IReadOnlyList<WorkingCapitalDriver> Drivers { get; init; } = [];

    /// <summary>Суммарное высвобождение денег при достижении целей.</summary>
    public double PotentialCashRelease { get; init; }

    /// <summary>Годовая экономия на процентах при высвобождении.</summary>
    public double AnnualFundingSaving { get; init; }

    /// <summary>Дополнительная потребность в деньгах на каждый процент роста выручки.</summary>
    public double FundingPerGrowthPoint { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        WorkingCapitalDriver? biggest = Drivers.OrderByDescending(d => d.CashImpact).FirstOrDefault();
        bool negativeCycle = CashConversionCycle < 0;

        var builder = new InterpretationBuilder($"Оборотный капитал: {Company}, {Period}")
            .Summary($"Финансовый цикл {Fmt.Num(CashConversionCycle, 0)} дн. складывается из " +
                     $"{Fmt.Num(DaysSalesOutstanding, 0)} дн. сбора дебиторской задолженности, " +
                     $"{Fmt.Num(DaysInventoryOutstanding, 0)} дн. оборота запасов и " +
                     $"{Fmt.Num(DaysPayablesOutstanding, 0)} дн. отсрочки от поставщиков. " +
                     $"В цикле связано {Fmt.Money(WorkingCapital)}; выход на целевые сроки " +
                     $"высвободил бы {Fmt.Money(PotentialCashRelease)}.")
            .Metric("Финансовый цикл", CashConversionCycle, "дн.",
                negativeCycle
                    ? "цикл отрицателен: поставщики финансируют операции"
                    : "срок отвлечения денег в оборот",
                CashConversionCycle <= 30 ? MetricQuality.Good
                    : CashConversionCycle <= 90 ? MetricQuality.Neutral : MetricQuality.Warning, 0)
            .Metric("Операционный цикл", OperatingCycle, "дн.",
                "от поступления товара до получения денег", MetricQuality.Neutral, 0)
            .Metric("Сбор дебиторской задолженности", DaysSalesOutstanding, "дн.",
                "сколько дней покупатели пользуются деньгами компании",
                DaysSalesOutstanding <= 45 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Metric("Оборот запасов", DaysInventoryOutstanding, "дн.",
                "сколько дней товар лежит на складе",
                DaysInventoryOutstanding <= 60 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Metric("Оплата поставщикам", DaysPayablesOutstanding, "дн.",
                "сколько дней компания пользуется деньгами поставщиков", MetricQuality.Neutral, 0)
            .Metric("Связано в обороте", Fmt.Money(WorkingCapital), null,
                $"{Fmt.Pct(WorkingCapitalToRevenue, 1)} от выручки",
                WorkingCapitalToRevenue > 0.3 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Потенциал высвобождения", Fmt.Money(PotentialCashRelease), null,
                $"экономия на процентах {Fmt.Money(AnnualFundingSaving)} в год",
                PotentialCashRelease > 0 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Финансирование роста", Fmt.Money(FundingPerGrowthPoint), null,
                "сколько денег нужно на каждый процент прироста выручки",
                MetricQuality.Neutral);

        foreach (WorkingCapitalDriver driver in Drivers)
        {
            builder.Metric(driver.Name, driver.Days, "дн.",
                $"{driver.Comment}; цель {Fmt.Num(driver.TargetDays, 0)} дн., " +
                $"эффект {Fmt.Money(driver.CashImpact)}",
                driver.CashImpact > 0 ? MetricQuality.Warning : MetricQuality.Good, 0);
        }

        return builder
            .FindingIf(biggest is not null && biggest.CashImpact > 0,
                $"Наибольший резерв даёт драйвер «{biggest?.Name}»: сокращение срока до целевого " +
                $"высвободит {Fmt.Money(biggest?.CashImpact ?? 0)}. Один день здесь стоит " +
                $"{Fmt.Money(biggest?.AmountPerDay ?? 0)}.")
            .Finding($"Каждый процент роста выручки требует {Fmt.Money(FundingPerGrowthPoint)} " +
                     "дополнительного финансирования оборота. Это и есть причина, по которой " +
                     "прибыльная растущая компания может остаться без денег.")
            .FindingIf(negativeCycle,
                "Отрицательный финансовый цикл: компания получает деньги от покупателей " +
                "раньше, чем расплачивается с поставщиками. Рост в такой модели сам себя " +
                "финансирует, но делает бизнес зависимым от условий поставщиков.")
            .WarningIf(DaysSalesOutstanding > DaysPayablesOutstanding * 1.5,
                $"Дебиторская задолженность оборачивается за {Fmt.Num(DaysSalesOutstanding, 0)} дн. " +
                $"при отсрочке поставщиков {Fmt.Num(DaysPayablesOutstanding, 0)} дн. Разрыв " +
                "компания финансирует сама — либо своими деньгами, либо кредитом.")
            .WarningIf(WorkingCapitalToRevenue > 0.3,
                $"В обороте связано {Fmt.Pct(WorkingCapitalToRevenue, 1)} годовой выручки. " +
                "Такая доля обычно означает избыточные запасы или слабую платёжную дисциплину покупателей.")
            .Warning("Расчёт по годовым показателям сглаживает сезонность. Если продажи " +
                     "неравномерны, сроки оборота на отчётную дату могут заметно расходиться " +
                     "со средними за период, и потенциал высвобождения окажется завышенным.")
            .Recommendation("Считайте сроки оборота по сегментам покупателей и товарным группам: " +
                            "резерв почти всегда сконцентрирован в узкой части портфеля.")
            .Recommendation("Прежде чем удлинять отсрочку поставщикам, сравните её стоимость " +
                            "со скидкой за раннюю оплату: отказ от скидки в 2% за 20 дней " +
                            "обходится дороже банковского кредита.")
            .Build();
    }
}

/// <summary>
/// Анализ оборотного капитала: финансовый цикл, драйверы и потенциал
/// высвобождения денег.
/// </summary>
/// <remarks>
/// <para>
/// Финансовый цикл показывает, сколько дней деньги компании связаны в обороте:
/// </para>
/// <code>
/// DSO = AR / Revenue * 365
/// DIO = Inventory / COGS * 365
/// DPO = AP / COGS * 365
/// CCC = DSO + DIO - DPO
/// </code>
/// <para>
/// Каждый день цикла имеет цену: для дебиторской задолженности это дневная
/// выручка, для запасов и кредиторской задолженности — дневная себестоимость.
/// Умножив отклонение от целевого срока на эту цену, получаем сумму, которую
/// можно высвободить, не меняя ни выручку, ни маржу.
/// </para>
/// <para>
/// Отдельно считается потребность в финансировании роста: оборотный капитал
/// масштабируется вместе с выручкой, поэтому быстрый рост при неизменном цикле
/// требует денег, которых у прибыльной компании может не оказаться. Это самая
/// частая причина кассовых разрывов в растущем бизнесе.
/// </para>
/// </remarks>
public static class WorkingCapitalAnalysis
{
    /// <summary>Анализирует оборотный капитал по отчётности.</summary>
    /// <param name="statement">Отчётность за период.</param>
    /// <param name="targets">Целевые сроки оборота; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Сроки оборота, драйверы и потенциал высвобождения денег.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Выручка или себестоимость неположительны.</exception>
    public static WorkingCapitalResult Analyze(
        FinancialStatement statement, WorkingCapitalTargets? targets = null)
    {
        ArgumentNullException.ThrowIfNull(statement);

        if (statement.Revenue <= 0)
            throw new ArgumentException("Выручка должна быть положительной.", nameof(statement));
        if (statement.CostOfGoodsSold <= 0)
            throw new ArgumentException("Себестоимость должна быть положительной.", nameof(statement));

        targets ??= new WorkingCapitalTargets();

        double revenuePerDay = statement.Revenue / 365;
        double costPerDay = statement.CostOfGoodsSold / 365;

        double dso = statement.AccountsReceivable / revenuePerDay;
        double dio = statement.Inventory / costPerDay;
        double dpo = statement.AccountsPayable / costPerDay;
        double ccc = dso + dio - dpo;

        double operatingWorkingCapital =
            statement.AccountsReceivable + statement.Inventory - statement.AccountsPayable;

        var drivers = new List<WorkingCapitalDriver>
        {
            new("Дебиторская задолженность", dso, targets.DaysSalesOutstanding, revenuePerDay,
                (dso - targets.DaysSalesOutstanding) * revenuePerDay,
                "деньги, которыми пользуются покупатели"),
            new("Запасы", dio, targets.DaysInventoryOutstanding, costPerDay,
                (dio - targets.DaysInventoryOutstanding) * costPerDay,
                "деньги, замороженные на складе"),
            new("Кредиторская задолженность", dpo, targets.DaysPayablesOutstanding, costPerDay,
                (targets.DaysPayablesOutstanding - dpo) * costPerDay,
                "бесплатное финансирование от поставщиков"),
        };

        double release = drivers.Sum(d => Math.Max(0, d.CashImpact));

        return new WorkingCapitalResult
        {
            Company = statement.Company,
            Period = statement.Period,
            DaysSalesOutstanding = dso,
            DaysInventoryOutstanding = dio,
            DaysPayablesOutstanding = dpo,
            CashConversionCycle = ccc,
            OperatingCycle = dso + dio,
            WorkingCapital = operatingWorkingCapital,
            WorkingCapitalToRevenue = operatingWorkingCapital / statement.Revenue,
            Drivers = drivers,
            PotentialCashRelease = release,
            AnnualFundingSaving = release * targets.CostOfFunding,
            FundingPerGrowthPoint = operatingWorkingCapital / 100,
        };
    }
}
