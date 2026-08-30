using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Statements;

/// <summary>Направление, в котором коэффициент считается улучшающимся.</summary>
public enum RatioDirection
{
    /// <summary>Чем больше, тем лучше.</summary>
    HigherIsBetter,

    /// <summary>Чем меньше, тем лучше.</summary>
    LowerIsBetter,

    /// <summary>Оценка зависит от отрасли и стратегии.</summary>
    Neutral,
}

/// <summary>Отдельный финансовый коэффициент.</summary>
/// <param name="Group">Группа: ликвидность, рентабельность, оборачиваемость, долговая нагрузка, денежный поток.</param>
/// <param name="Name">Название коэффициента.</param>
/// <param name="Value">Значение.</param>
/// <param name="Unit">Единица измерения; <c>null</c> для безразмерных величин.</param>
/// <param name="Benchmark">Ориентир для сравнения.</param>
/// <param name="Direction">Направление улучшения.</param>
/// <param name="Comment">Экономический смысл значения.</param>
public sealed record FinancialRatio(
    string Group, string Name, double Value, string? Unit,
    double Benchmark, RatioDirection Direction, string Comment)
{
    /// <summary>Соответствует ли значение ориентиру.</summary>
    public bool MeetsBenchmark => Direction switch
    {
        RatioDirection.HigherIsBetter => Value >= Benchmark,
        RatioDirection.LowerIsBetter => Value <= Benchmark,
        _ => true,
    };
}

/// <summary>Свод финансовых коэффициентов компании за период.</summary>
public sealed record RatioReport : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Все рассчитанные коэффициенты.</summary>
    public IReadOnlyList<FinancialRatio> Ratios { get; init; } = [];

    /// <summary>Текущая ликвидность.</summary>
    public double CurrentRatio { get; init; }

    /// <summary>Быстрая ликвидность.</summary>
    public double QuickRatio { get; init; }

    /// <summary>Рентабельность продаж по чистой прибыли.</summary>
    public double NetMargin { get; init; }

    /// <summary>Рентабельность активов.</summary>
    public double ReturnOnAssets { get; init; }

    /// <summary>Рентабельность собственного капитала.</summary>
    public double ReturnOnEquity { get; init; }

    /// <summary>Оборачиваемость активов.</summary>
    public double AssetTurnover { get; init; }

    /// <summary>Долг к собственному капиталу.</summary>
    public double DebtToEquity { get; init; }

    /// <summary>Покрытие процентов операционной прибылью.</summary>
    public double InterestCoverage { get; init; }

    /// <summary>Финансовый цикл в днях.</summary>
    public double CashConversionCycle { get; init; }

    /// <summary>Доля коэффициентов, укладывающихся в ориентиры.</summary>
    public double BenchmarkPassRate =>
        Ratios.Count > 0
            ? (double)Ratios.Count(r => r.Direction != RatioDirection.Neutral && r.MeetsBenchmark) /
              Math.Max(1, Ratios.Count(r => r.Direction != RatioDirection.Neutral))
            : 0;

    /// <summary>Коэффициенты выбранной группы.</summary>
    /// <param name="group">Название группы.</param>
    /// <returns>Коэффициенты группы в порядке расчёта.</returns>
    public IReadOnlyList<FinancialRatio> Group(string group) =>
        [.. Ratios.Where(r => string.Equals(r.Group, group, StringComparison.Ordinal))];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var failing = Ratios
            .Where(r => r.Direction != RatioDirection.Neutral && !r.MeetsBenchmark)
            .ToList();

        var builder = new InterpretationBuilder($"Коэффициентный анализ: {Company}, {Period}")
            .Summary($"Рассчитано {Ratios.Count} коэффициентов по пяти группам. " +
                     $"Ориентирам соответствует {Fmt.Pct(BenchmarkPassRate, 0)} показателей. " +
                     $"Рентабельность капитала {Fmt.Pct(ReturnOnEquity, 1)}, текущая ликвидность " +
                     $"{Fmt.Num(CurrentRatio, 2)}, долг к капиталу {Fmt.Num(DebtToEquity, 2)}, " +
                     $"финансовый цикл {Fmt.Num(CashConversionCycle, 0)} дн.")
            .Metric("Текущая ликвидность", CurrentRatio, null,
                "оборотные активы к краткосрочным обязательствам",
                CurrentRatio >= 1.5 ? MetricQuality.Good
                    : CurrentRatio >= 1 ? MetricQuality.Warning : MetricQuality.Critical, 2)
            .Metric("Быстрая ликвидность", QuickRatio, null,
                "ликвидность без учёта запасов",
                QuickRatio >= 1 ? MetricQuality.Good : MetricQuality.Warning, 2)
            .Metric("Рентабельность капитала", ReturnOnEquity, null,
                "чистая прибыль к собственному капиталу",
                ReturnOnEquity >= 0.15 ? MetricQuality.Good
                    : ReturnOnEquity >= 0 ? MetricQuality.Neutral : MetricQuality.Critical, 3)
            .Metric("Рентабельность активов", ReturnOnAssets, null,
                "отдача на весь вложенный капитал",
                ReturnOnAssets >= 0.05 ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Рентабельность продаж", NetMargin, null,
                "чистая прибыль на рубль выручки",
                NetMargin >= 0.05 ? MetricQuality.Good
                    : NetMargin >= 0 ? MetricQuality.Warning : MetricQuality.Critical, 3)
            .Metric("Оборачиваемость активов", AssetTurnover, "раз",
                "сколько выручки приносит рубль активов", MetricQuality.Neutral, 2)
            .Metric("Долг к капиталу", DebtToEquity, null, "долговая нагрузка на собственников",
                DebtToEquity <= 1 ? MetricQuality.Good
                    : DebtToEquity <= 2 ? MetricQuality.Warning : MetricQuality.Critical, 2)
            .Metric("Покрытие процентов", InterestCoverage, "раз",
                "во сколько раз операционная прибыль покрывает проценты",
                InterestCoverage >= 3 ? MetricQuality.Good
                    : InterestCoverage >= 1.5 ? MetricQuality.Warning : MetricQuality.Critical, 1)
            .Metric("Финансовый цикл", CashConversionCycle, "дн.",
                "срок между оплатой поставщику и получением денег от покупателя",
                CashConversionCycle <= 30 ? MetricQuality.Good
                    : CashConversionCycle <= 90 ? MetricQuality.Neutral : MetricQuality.Warning, 0);

        foreach (FinancialRatio ratio in Ratios)
        {
            builder.Metric($"{ratio.Group}: {ratio.Name}", ratio.Value, ratio.Unit,
                $"{ratio.Comment}; ориентир {Fmt.Num(ratio.Benchmark, 2)}",
                ratio.Direction == RatioDirection.Neutral ? MetricQuality.Unknown
                    : ratio.MeetsBenchmark ? MetricQuality.Good : MetricQuality.Warning, 3);
        }

        return builder
            .Finding($"Из {Ratios.Count(r => r.Direction != RatioDirection.Neutral)} коэффициентов " +
                     $"с однозначным направлением {failing.Count} не дотягивают до ориентира." +
                     (failing.Count > 0
                         ? $" Худшие: {string.Join(", ", failing.Take(3).Select(f => f.Name))}."
                         : " Отклонений нет."))
            .FindingIf(ReturnOnEquity > ReturnOnAssets * 2 && DebtToEquity > 1,
                "Рентабельность капитала заметно выше рентабельности активов: прибыль " +
                "собственников создаётся рычагом. Такая структура повышает и доходность, " +
                "и чувствительность к падению выручки.")
            .FindingIf(CashConversionCycle < 0,
                "Финансовый цикл отрицателен: компания получает деньги от покупателей раньше, " +
                "чем платит поставщикам, и фактически финансируется оборотным капиталом " +
                "контрагентов.")
            .WarningIf(CurrentRatio < 1,
                $"Текущая ликвидность {Fmt.Num(CurrentRatio, 2)} ниже единицы: краткосрочные " +
                "обязательства не покрываются оборотными активами.")
            .WarningIf(InterestCoverage < 1.5,
                $"Покрытие процентов {Fmt.Num(InterestCoverage, 1)} — операционной прибыли " +
                "едва хватает на обслуживание долга. Это первый по значимости признак " +
                "приближающихся проблем с ликвидностью.")
            .Warning("Коэффициенты сопоставимы только внутри отрасли и только при одинаковой " +
                     "учётной политике. Ориентиры в этом отчёте усреднённые: для решения " +
                     "нужен отраслевой бенчмарк и динамика за несколько периодов.")
            .Recommendation("Смотрите коэффициенты в динамике: направление изменения говорит " +
                            "о компании больше, чем уровень на одну отчётную дату.")
            .Recommendation("Сопоставьте прибыль с денежным потоком. Расхождение между ними " +
                            "устойчиво предсказывает пересмотр отчётности и проблемы с качеством прибыли.")
            .Build();
    }
}

/// <summary>
/// Расчёт полного набора финансовых коэффициентов по отчётности.
/// </summary>
/// <remarks>
/// <para>
/// Коэффициенты сгруппированы в пять блоков: ликвидность, рентабельность,
/// оборачиваемость, долговая нагрузка и денежный поток. Каждый коэффициент
/// снабжён ориентиром и направлением улучшения, поэтому вывод читается без
/// внешнего справочника.
/// </para>
/// <para>
/// Если передана отчётность предыдущего периода, показатели оборачиваемости
/// считаются по средним остаткам баланса, а не по остаткам на конец периода.
/// Разница существенна для растущих компаний: расчёт по конечным остаткам
/// систематически занижает оборачиваемость.
/// </para>
/// </remarks>
public static class FinancialRatios
{
    /// <summary>Условное значение покрытия процентов при отсутствии процентных расходов.</summary>
    private const double NoInterestCoverage = 99;

    /// <summary>Рассчитывает коэффициенты по отчётности.</summary>
    /// <param name="current">Отчётность за анализируемый период.</param>
    /// <param name="previous">Отчётность за предыдущий период для расчёта средних остатков.</param>
    /// <returns>Свод коэффициентов с ориентирами и интерпретацией.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Активы или выручка неположительны.</exception>
    public static RatioReport Compute(FinancialStatement current, FinancialStatement? previous = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.TotalAssets <= 0)
            throw new ArgumentException("Совокупные активы должны быть положительными.", nameof(current));
        if (current.Revenue <= 0)
            throw new ArgumentException("Выручка должна быть положительной.", nameof(current));

        double assets = Average(current.TotalAssets, previous?.TotalAssets);
        double equity = Average(current.Equity, previous?.Equity);
        double receivables = Average(current.AccountsReceivable, previous?.AccountsReceivable);
        double inventory = Average(current.Inventory, previous?.Inventory);
        double payables = Average(current.AccountsPayable, previous?.AccountsPayable);

        double dso = Div(receivables * 365, current.Revenue);
        double dio = Div(inventory * 365, current.CostOfGoodsSold);
        double dpo = Div(payables * 365, current.CostOfGoodsSold);
        double ccc = dso + dio - dpo;

        double currentRatio = Div(current.CurrentAssets, current.CurrentLiabilities);
        double quickRatio = Div(current.CurrentAssets - current.Inventory, current.CurrentLiabilities);
        double netMargin = Div(current.NetIncome, current.Revenue);
        double roa = Div(current.NetIncome, assets);
        double roe = Div(current.NetIncome, equity);
        double assetTurnover = Div(current.Revenue, assets);
        double debtToEquity = Div(current.TotalDebt, equity);

        double coverage = current.InterestExpense > 0
            ? current.OperatingIncome / current.InterestExpense
            : NoInterestCoverage;

        double investedCapital = equity + Average(current.TotalDebt, previous?.TotalDebt);
        double taxRate = current.PretaxIncome > 0
            ? Clamp01(Div(current.IncomeTax, current.PretaxIncome))
            : 0.2;

        var ratios = new List<FinancialRatio>
        {
            new("Ликвидность", "Текущая ликвидность", currentRatio, null, 1.5,
                RatioDirection.HigherIsBetter, "оборотные активы к краткосрочным обязательствам"),
            new("Ликвидность", "Быстрая ликвидность", quickRatio, null, 1.0,
                RatioDirection.HigherIsBetter, "ликвидность без запасов"),
            new("Ликвидность", "Абсолютная ликвидность",
                Div(current.Cash + current.ShortTermInvestments, current.CurrentLiabilities), null, 0.2,
                RatioDirection.HigherIsBetter, "денежные средства к краткосрочным обязательствам"),
            new("Ликвидность", "Рабочий капитал к активам",
                Div(current.WorkingCapital, current.TotalAssets), null, 0.1,
                RatioDirection.HigherIsBetter, "запас оборотных средств относительно масштаба"),

            new("Рентабельность", "Валовая рентабельность",
                Div(current.GrossProfit, current.Revenue), null, 0.25,
                RatioDirection.HigherIsBetter, "маржа до операционных расходов"),
            new("Рентабельность", "Операционная рентабельность",
                Div(current.OperatingIncome, current.Revenue), null, 0.1,
                RatioDirection.HigherIsBetter, "прибыль от основной деятельности к выручке"),
            new("Рентабельность", "Рентабельность по прибыли до амортизации",
                Div(current.Ebitda, current.Revenue), null, 0.15,
                RatioDirection.HigherIsBetter, "денежная маржа операций"),
            new("Рентабельность", "Чистая рентабельность", netMargin, null, 0.05,
                RatioDirection.HigherIsBetter, "чистая прибыль на рубль выручки"),
            new("Рентабельность", "Рентабельность активов", roa, null, 0.05,
                RatioDirection.HigherIsBetter, "отдача на весь капитал компании"),
            new("Рентабельность", "Рентабельность капитала", roe, null, 0.15,
                RatioDirection.HigherIsBetter, "отдача на средства собственников"),
            new("Рентабельность", "Рентабельность инвестированного капитала",
                Div(current.OperatingIncome * (1 - taxRate), investedCapital), null, 0.1,
                RatioDirection.HigherIsBetter, "прибыль после налога на весь инвестированный капитал"),

            new("Оборачиваемость", "Оборачиваемость активов", assetTurnover, "раз", 1.0,
                RatioDirection.HigherIsBetter, "выручка на рубль активов"),
            new("Оборачиваемость", "Оборачиваемость дебиторской задолженности",
                Div(current.Revenue, receivables), "раз", 6,
                RatioDirection.HigherIsBetter, "число оборотов дебиторской задолженности за год"),
            new("Оборачиваемость", "Оборачиваемость запасов",
                Div(current.CostOfGoodsSold, inventory), "раз", 6,
                RatioDirection.HigherIsBetter, "число оборотов запасов за год"),
            new("Оборачиваемость", "Период сбора дебиторской задолженности", dso, "дн.", 45,
                RatioDirection.LowerIsBetter, "сколько дней покупатели держат деньги компании"),
            new("Оборачиваемость", "Период оборота запасов", dio, "дн.", 60,
                RatioDirection.LowerIsBetter, "сколько дней товар лежит на складе"),
            new("Оборачиваемость", "Период оплаты поставщикам", dpo, "дн.", 30,
                RatioDirection.Neutral, "сколько дней компания пользуется деньгами поставщиков"),
            new("Оборачиваемость", "Финансовый цикл", ccc, "дн.", 60,
                RatioDirection.LowerIsBetter, "срок отвлечения денег в оборотный капитал"),

            new("Долговая нагрузка", "Долг к капиталу", debtToEquity, null, 1.0,
                RatioDirection.LowerIsBetter, "заёмные средства на рубль собственных"),
            new("Долговая нагрузка", "Долг к активам",
                Div(current.TotalDebt, current.TotalAssets), null, 0.5,
                RatioDirection.LowerIsBetter, "доля активов, профинансированная долгом"),
            new("Долговая нагрузка", "Чистый долг к прибыли до амортизации",
                current.Ebitda > 0 ? current.NetDebt / current.Ebitda : NoInterestCoverage, "раз", 3,
                RatioDirection.LowerIsBetter, "сколько лет прибыли нужно на погашение долга"),
            new("Долговая нагрузка", "Покрытие процентов", coverage, "раз", 3,
                RatioDirection.HigherIsBetter, "запас операционной прибыли над процентами"),
            new("Долговая нагрузка", "Коэффициент автономии",
                Div(current.Equity, current.TotalAssets), null, 0.4,
                RatioDirection.HigherIsBetter, "доля собственного капитала в активах"),
            new("Долговая нагрузка", "Финансовый рычаг", Div(assets, equity), "раз", 2,
                RatioDirection.LowerIsBetter, "активы на рубль собственного капитала"),

            new("Денежный поток", "Денежный поток к прибыли",
                Div(current.OperatingCashFlow, current.NetIncome), "раз", 1.0,
                RatioDirection.HigherIsBetter, "насколько прибыль подтверждена деньгами"),
            new("Денежный поток", "Свободный денежный поток к выручке",
                Div(current.FreeCashFlow, current.Revenue), null, 0.05,
                RatioDirection.HigherIsBetter, "сколько свободных денег даёт рубль выручки"),
            new("Денежный поток", "Капитальные затраты к выручке",
                Div(current.CapitalExpenditures, current.Revenue), null, 0.1,
                RatioDirection.Neutral, "капиталоёмкость бизнеса"),
            new("Денежный поток", "Покрытие долга денежным потоком",
                Div(current.OperatingCashFlow, current.TotalDebt), null, 0.2,
                RatioDirection.HigherIsBetter, "какую часть долга закрывает годовой поток"),
        };

        return new RatioReport
        {
            Company = current.Company,
            Period = current.Period,
            Ratios = ratios,
            CurrentRatio = currentRatio,
            QuickRatio = quickRatio,
            NetMargin = netMargin,
            ReturnOnAssets = roa,
            ReturnOnEquity = roe,
            AssetTurnover = assetTurnover,
            DebtToEquity = debtToEquity,
            InterestCoverage = coverage,
            CashConversionCycle = ccc,
        };
    }

    /// <summary>Среднее между текущим и предыдущим остатком, если предыдущий известен.</summary>
    private static double Average(double current, double? previous) =>
        previous.HasValue ? (current + previous.Value) / 2 : current;

    /// <summary>Деление с защитой от нулевого знаменателя.</summary>
    private static double Div(double numerator, double denominator) =>
        Math.Abs(denominator) < 1e-12 ? 0 : numerator / denominator;

    /// <summary>Ограничение доли отрезком от нуля до единицы.</summary>
    private static double Clamp01(double value) => Math.Min(1, Math.Max(0, value));
}
