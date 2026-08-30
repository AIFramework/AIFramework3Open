using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Statements;

/// <summary>Показатель качества прибыли.</summary>
/// <param name="Name">Название показателя.</param>
/// <param name="Value">Значение.</param>
/// <param name="Threshold">Пороговое значение, отделяющее благоприятное состояние.</param>
/// <param name="IsFavourable">Благоприятно ли значение.</param>
/// <param name="Score">Нормированная оценка от нуля до единицы.</param>
/// <param name="Comment">Экономический смысл.</param>
public sealed record EarningsQualityMetric(
    string Name, double Value, double Threshold, bool IsFavourable, double Score, string Comment);

/// <summary>Результат оценки качества прибыли.</summary>
public sealed record EarningsQualityResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Доля начислений в прибыли относительно активов.</summary>
    public double AccrualRatio { get; init; }

    /// <summary>Отношение операционного денежного потока к чистой прибыли.</summary>
    public double CashFlowToNetIncome { get; init; }

    /// <summary>Отношение свободного денежного потока к чистой прибыли.</summary>
    public double FreeCashFlowConversion { get; init; }

    /// <summary>Расхождение темпов роста дебиторской задолженности и выручки.</summary>
    public double ReceivablesDivergence { get; init; }

    /// <summary>Расхождение темпов роста запасов и себестоимости.</summary>
    public double InventoryDivergence { get; init; }

    /// <summary>Покрытие капитальных затрат и дивидендов операционным потоком.</summary>
    public double CashFlowAdequacy { get; init; }

    /// <summary>Сводная оценка качества прибыли от нуля до ста.</summary>
    public double QualityScore { get; init; }

    /// <summary>Словесная оценка.</summary>
    public string Verdict { get; init; } = string.Empty;

    /// <summary>Показатели с оценками.</summary>
    public IReadOnlyList<EarningsQualityMetric> Metrics { get; init; } = [];

    /// <summary>Есть ли сопоставление с предыдущим периодом.</summary>
    public bool HasComparison { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var unfavourable = Metrics.Where(m => !m.IsFavourable).ToList();
        EarningsQualityMetric? weakest = Metrics.OrderBy(m => m.Score).FirstOrDefault();

        var builder = new InterpretationBuilder($"Качество прибыли: {Company}, {Period}")
            .Summary($"Сводная оценка {Fmt.Num(QualityScore, 0)} из 100 — {Verdict}. " +
                     $"Денежный поток покрывает прибыль на {Fmt.Pct(CashFlowToNetIncome, 0)}, " +
                     $"доля начислений {Fmt.Pct(AccrualRatio, 2)} от активов. " +
                     $"Неблагоприятных показателей: {unfavourable.Count} из {Metrics.Count}.")
            .Metric("Сводная оценка", QualityScore, "из 100", Verdict,
                QualityScore >= 70 ? MetricQuality.Good
                    : QualityScore >= 45 ? MetricQuality.Warning : MetricQuality.Critical, 0)
            .Metric("Доля начислений", AccrualRatio, null,
                "разрыв между прибылью и денежным потоком относительно активов",
                AccrualRatio <= 0.02 ? MetricQuality.Good
                    : AccrualRatio <= 0.06 ? MetricQuality.Warning : MetricQuality.Critical, 4)
            .Metric("Поток к прибыли", CashFlowToNetIncome, "раз",
                "насколько прибыль подтверждена деньгами",
                CashFlowToNetIncome >= 1 ? MetricQuality.Good
                    : CashFlowToNetIncome >= 0.7 ? MetricQuality.Warning : MetricQuality.Critical, 2)
            .Metric("Свободный поток к прибыли", FreeCashFlowConversion, "раз",
                "сколько свободных денег остаётся на рубль прибыли",
                FreeCashFlowConversion >= 0.6 ? MetricQuality.Good : MetricQuality.Warning, 2)
            .Metric("Покрытие затрат потоком", CashFlowAdequacy, "раз",
                "хватает ли операционного потока на капзатраты и дивиденды",
                CashFlowAdequacy >= 1 ? MetricQuality.Good : MetricQuality.Warning, 2);

        if (HasComparison)
        {
            builder
                .Metric("Опережение дебиторской задолженности", ReceivablesDivergence, null,
                    "насколько рост дебиторской задолженности обгоняет рост выручки",
                    ReceivablesDivergence <= 0.05 ? MetricQuality.Good
                        : ReceivablesDivergence <= 0.2 ? MetricQuality.Warning : MetricQuality.Critical, 3)
                .Metric("Опережение запасов", InventoryDivergence, null,
                    "насколько рост запасов обгоняет рост себестоимости",
                    InventoryDivergence <= 0.05 ? MetricQuality.Good
                        : InventoryDivergence <= 0.2 ? MetricQuality.Warning : MetricQuality.Critical, 3);
        }

        foreach (EarningsQualityMetric metric in Metrics)
        {
            builder.Metric(metric.Name, metric.Value, null,
                $"{metric.Comment}; порог {Fmt.Num(metric.Threshold, 2)}",
                metric.IsFavourable ? MetricQuality.Good : MetricQuality.Warning, 3);
        }

        return builder
            .Finding("Прибыль состоит из денежной части и начислений. Начисления держатся " +
                     "хуже: доходность компаний с высокой их долей систематически падает " +
                     "в следующем году. Это устойчивый эффект, а не особенность отдельной отрасли.")
            .FindingIf(CashFlowToNetIncome < 1,
                $"Операционный поток меньше прибыли ({Fmt.Num(CashFlowToNetIncome, 2)} раза). " +
                "Разрыв нормален для растущей компании, финансирующей оборот, но требует " +
                "проверки, если сохраняется несколько периодов подряд.")
            .FindingIf(HasComparison && ReceivablesDivergence > 0.2,
                $"Дебиторская задолженность растёт быстрее выручки на " +
                $"{Fmt.Pct(ReceivablesDivergence, 1)}. Компания продаёт, но не получает деньги — " +
                "либо смягчила условия оплаты, либо признаёт выручку раньше отгрузки.")
            .FindingIf(weakest is not null,
                $"Самый слабый показатель — «{weakest?.Name}» с оценкой {Fmt.Pct(weakest?.Score ?? 0, 0)}.")
            .WarningIf(AccrualRatio > 0.06,
                $"Доля начислений {Fmt.Pct(AccrualRatio, 2)} превышает уровень, при котором " +
                "прибыль обычно оказывается неустойчивой. Проверьте признание выручки " +
                "и капитализацию расходов.")
            .WarningIf(CashFlowAdequacy < 1,
                "Операционного потока не хватает на капитальные затраты и дивиденды. " +
                "Разрыв финансируется долгом или сокращением денежных остатков — " +
                "и то, и другое конечно.")
            .Warning("Качество прибыли оценивается по агрегатам отчётности и не отличает " +
                     "агрессивный учёт от нормального финансирования роста. Вывод следует " +
                     "проверять по расшифровкам и динамике за несколько лет.")
            .Recommendation("Смотрите начисления в динамике за три-пять лет: устойчиво высокая " +
                            "доля информативнее одного всплеска.")
            .Recommendation("Сопоставьте результат с M-score Бениша: совпадение сигналов " +
                            "по начислениям и по дебиторской задолженности резко повышает " +
                            "обоснованность углублённой проверки.")
            .Build();
    }
}

/// <summary>
/// Оценка качества прибыли: начисления, подтверждение прибыли денежным потоком
/// и расхождение динамики оборотных статей с выручкой.
/// </summary>
/// <remarks>
/// <para>
/// Прибыль складывается из денежной части и начислений. Начисления —
/// бухгалтерская оценка будущих поступлений и расходов, и они хуже
/// воспроизводятся в следующих периодах:
/// </para>
/// <code>
/// accruals = (NetIncome - OperatingCashFlow) / AverageAssets
/// </code>
/// <para>
/// Эффект начислений, описанный Слоуном, устойчив: компании с высокой долей
/// начислений в прибыли систематически показывают худшие результаты в
/// следующем году. Поэтому качество прибыли — самостоятельный фактор, а не
/// техническая деталь учёта.
/// </para>
/// <para>
/// Дополнительно сравнивается динамика дебиторской задолженности с выручкой и
/// запасов с себестоимостью. Устойчивое опережение оборотных статей означает,
/// что рост показателей отчётности не подкреплён движением денег.
/// </para>
/// </remarks>
public static class EarningsQuality
{
    /// <summary>Оценивает качество прибыли по отчётности.</summary>
    /// <param name="current">Отчётность анализируемого периода.</param>
    /// <param name="previous">Отчётность предыдущего периода для оценки динамики.</param>
    /// <returns>Показатели качества прибыли и сводная оценка.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Активы или выручка неположительны.</exception>
    public static EarningsQualityResult Evaluate(
        FinancialStatement current, FinancialStatement? previous = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.TotalAssets <= 0)
            throw new ArgumentException("Активы должны быть положительными.", nameof(current));
        if (current.Revenue <= 0)
            throw new ArgumentException("Выручка должна быть положительной.", nameof(current));

        bool hasComparison = previous is not null && previous.Revenue > 0 && previous.TotalAssets > 0;
        double averageAssets = hasComparison
            ? (current.TotalAssets + previous!.TotalAssets) / 2
            : current.TotalAssets;

        double accrualRatio = (current.NetIncome - current.OperatingCashFlow) / averageAssets;
        double cashToIncome = Math.Abs(current.NetIncome) > 1e-9
            ? current.OperatingCashFlow / current.NetIncome
            : 0;
        double fcfConversion = Math.Abs(current.NetIncome) > 1e-9
            ? current.FreeCashFlow / current.NetIncome
            : 0;

        double obligations = current.CapitalExpenditures + current.DividendsPaid;
        double adequacy = obligations > 1e-9 ? current.OperatingCashFlow / obligations : 2;

        double receivablesDivergence = 0, inventoryDivergence = 0;
        if (hasComparison)
        {
            double revenueGrowth = Growth(current.Revenue, previous!.Revenue);
            double receivablesGrowth = Growth(current.AccountsReceivable, previous.AccountsReceivable);
            receivablesDivergence = receivablesGrowth - revenueGrowth;

            double costGrowth = Growth(current.CostOfGoodsSold, previous.CostOfGoodsSold);
            double inventoryGrowth = Growth(current.Inventory, previous.Inventory);
            inventoryDivergence = inventoryGrowth - costGrowth;
        }

        var metrics = new List<EarningsQualityMetric>
        {
            Metric("Доля начислений", accrualRatio, 0.02, accrualRatio <= 0.02,
                1 - Ramp(accrualRatio, -0.02, 0.10),
                "разрыв между прибылью и денежным потоком относительно активов"),
            Metric("Поток к прибыли", cashToIncome, 1.0, cashToIncome >= 1,
                Ramp(cashToIncome, 0.3, 1.3),
                "во сколько раз операционный поток покрывает чистую прибыль"),
            Metric("Свободный поток к прибыли", fcfConversion, 0.6, fcfConversion >= 0.6,
                Ramp(fcfConversion, -0.2, 1.0),
                "сколько свободных денег остаётся на рубль прибыли"),
            Metric("Покрытие затрат потоком", adequacy, 1.0, adequacy >= 1,
                Ramp(adequacy, 0.3, 1.5),
                "хватает ли операционного потока на капзатраты и дивиденды"),
        };

        if (hasComparison)
        {
            metrics.Add(Metric("Опережение дебиторской задолженности", receivablesDivergence, 0.05,
                receivablesDivergence <= 0.05, 1 - Ramp(receivablesDivergence, 0, 0.4),
                "рост дебиторской задолженности сверх роста выручки"));
            metrics.Add(Metric("Опережение запасов", inventoryDivergence, 0.05,
                inventoryDivergence <= 0.05, 1 - Ramp(inventoryDivergence, 0, 0.4),
                "рост запасов сверх роста себестоимости"));
        }

        double score = metrics.Average(m => m.Score) * 100;

        return new EarningsQualityResult
        {
            Company = current.Company,
            Period = current.Period,
            AccrualRatio = accrualRatio,
            CashFlowToNetIncome = cashToIncome,
            FreeCashFlowConversion = fcfConversion,
            ReceivablesDivergence = receivablesDivergence,
            InventoryDivergence = inventoryDivergence,
            CashFlowAdequacy = adequacy,
            QualityScore = score,
            Verdict = score >= 70 ? "прибыль подтверждена денежным потоком"
                : score >= 45 ? "прибыль подтверждена частично"
                : "прибыль в основном сформирована начислениями",
            Metrics = metrics,
            HasComparison = hasComparison,
        };
    }

    /// <summary>Собирает показатель с ограниченной оценкой.</summary>
    private static EarningsQualityMetric Metric(
        string name, double value, double threshold, bool favourable, double score, string comment) =>
        new(name, value, threshold, favourable, Math.Min(1, Math.Max(0, score)), comment);

    /// <summary>Относительный прирост показателя.</summary>
    private static double Growth(double current, double previous) =>
        Math.Abs(previous) > 1e-9 ? (current - previous) / Math.Abs(previous) : 0;

    /// <summary>Кусочно-линейное отображение значения в отрезок от нуля до единицы.</summary>
    private static double Ramp(double value, double low, double high) =>
        high <= low ? 0 : Math.Min(1, Math.Max(0, (value - low) / (high - low)));
}
