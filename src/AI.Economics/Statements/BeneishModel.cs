using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Statements;

/// <summary>Индекс модели Бениша.</summary>
/// <param name="Code">Кодовое обозначение индекса.</param>
/// <param name="Name">Название индекса.</param>
/// <param name="Value">Значение индекса.</param>
/// <param name="Weight">Вес в модели.</param>
/// <param name="Contribution">Вклад в итоговый балл.</param>
/// <param name="Neutral">Значение, при котором индекс не сигнализирует о манипуляции.</param>
/// <param name="Comment">Что означает отклонение индекса.</param>
public sealed record BeneishIndex(
    string Code, string Name, double Value, double Weight,
    double Contribution, double Neutral, string Comment)
{
    /// <summary>Отклонение индекса от нейтрального значения.</summary>
    public double Deviation => Value - Neutral;

    /// <summary>Указывает ли индекс на возможную манипуляцию.</summary>
    public bool IsFlagged => Weight >= 0 ? Value > Neutral * 1.1 : Value < Neutral * 0.9;
}

/// <summary>Результат оценки риска манипуляций с отчётностью.</summary>
public sealed record BeneishResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Анализируемый период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Итоговый M-балл.</summary>
    public double MScore { get; init; }

    /// <summary>Вероятность манипуляции по пробит-шкале модели.</summary>
    public double Probability { get; init; }

    /// <summary>Превышает ли балл порог, при котором компания относится к манипуляторам.</summary>
    public bool IsLikelyManipulator { get; init; }

    /// <summary>Порог отнесения к манипуляторам.</summary>
    public double Threshold { get; init; } = -1.78;

    /// <summary>Индексы модели.</summary>
    public IReadOnlyList<BeneishIndex> Indices { get; init; } = [];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var flagged = Indices.Where(i => i.IsFlagged).ToList();
        BeneishIndex? driver = Indices
            .Where(i => i.Code != "CONST")
            .OrderByDescending(i => i.Contribution)
            .FirstOrDefault();

        BeneishIndex? accruals = Indices.FirstOrDefault(i => i.Code == "TATA");
        BeneishIndex? receivables = Indices.FirstOrDefault(i => i.Code == "DSRI");

        var builder = new InterpretationBuilder($"M-score Бениша: {Company}, {Period}")
            .Summary($"M-балл {Fmt.Num(MScore, 3)} при пороге {Fmt.Num(Threshold, 2)}. " +
                     (IsLikelyManipulator
                         ? "Компания попадает в зону, где модель относит отчётность к вероятно искажённой. "
                         : "Компания не попадает в зону вероятной манипуляции. ") +
                     $"Настораживающих индексов: {flagged.Count} из {Indices.Count(i => i.Code != "CONST")}.")
            .Metric("M-балл", MScore, null,
                IsLikelyManipulator ? "выше порога отнесения к манипуляторам" : "ниже порога",
                IsLikelyManipulator ? MetricQuality.Critical : MetricQuality.Good, 3)
            .Metric("Вероятность манипуляции", Probability, null,
                "пробит-вероятность по шкале модели",
                Probability > 0.1 ? MetricQuality.Critical
                    : Probability > 0.04 ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Настораживающих индексов", flagged.Count, null,
                $"из {Indices.Count(i => i.Code != "CONST")} рассчитанных",
                flagged.Count >= 3 ? MetricQuality.Critical
                    : flagged.Count > 0 ? MetricQuality.Warning : MetricQuality.Good, 0);

        foreach (BeneishIndex index in Indices.Where(i => i.Code != "CONST"))
        {
            builder.Metric($"{index.Code}: {index.Name}", index.Value, null,
                $"{index.Comment}; нейтральное значение {Fmt.Num(index.Neutral, 2)}, " +
                $"вклад {Fmt.Num(index.Contribution, 3)}",
                index.IsFlagged ? MetricQuality.Warning : MetricQuality.Good, 3);
        }

        return builder
            .FindingIf(driver is not null,
                $"Наибольший вклад в балл даёт индекс {driver?.Code} — {Fmt.Num(driver?.Contribution ?? 0, 3)}.")
            .FindingIf(accruals is not null && accruals.Value > 0.03,
                $"Начисления составляют {Fmt.Pct(accruals?.Value ?? 0, 1)} активов: прибыль " +
                "заметно опережает денежный поток. Это самый весомый индекс модели и " +
                "одновременно самый частый признак агрессивного признания выручки.")
            .FindingIf(receivables is not null && receivables.Value > 1.2,
                $"Дебиторская задолженность растёт быстрее выручки (индекс {Fmt.Num(receivables?.Value ?? 0, 2)}). " +
                "Классическая картина при отгрузках в канал и признании выручки до оплаты.")
            .Finding("Модель не доказывает манипуляцию: она отбирает компании, чья отчётность " +
                     "статистически похожа на отчётность известных манипуляторов. Результат — " +
                     "основание для проверки, а не вывод.")
            .WarningIf(IsLikelyManipulator,
                "Балл выше порога. Проверьте признание выручки, движение дебиторской " +
                "задолженности и капитализацию расходов до того, как опираться на эту отчётность.")
            .WarningIf(flagged.Count >= 3,
                $"Сразу {flagged.Count} индексов отклоняются от нейтральных значений. " +
                "Совпадение нескольких признаков информативнее итогового балла.")
            .Warning("Модель откалибрована на публичных компаниях США и даёт заметную долю " +
                     "ложных срабатываний: быстро растущие компании выглядят манипуляторами " +
                     "из-за опережающего роста дебиторской задолженности и начислений.")
            .Recommendation("Сопоставьте индексы с отраслью и стадией роста: у компании, " +
                            "удваивающей выручку, высокий индекс дебиторской задолженности — " +
                            "норма, а не сигнал.")
            .Recommendation("Дополните оценку законом Бенфорда по первичным транзакциям: " +
                            "модель Бениша работает с агрегатами и не видит искажений " +
                            "на уровне отдельных проводок.")
            .Build();
    }
}

/// <summary>
/// Модель Бениша (M-score) для выявления манипуляций с финансовой отчётностью.
/// </summary>
/// <remarks>
/// <para>
/// Модель сравнивает восемь показателей текущего периода с предыдущим и
/// объединяет их в пробит-балл:
/// </para>
/// <code>
/// M = -4.84 + 0.920*DSRI + 0.528*GMI + 0.404*AQI + 0.892*SGI
///     + 0.115*DEPI - 0.172*SGAI + 4.679*TATA - 0.327*LVGI
/// </code>
/// <para>
/// Каждый индекс — отношение показателя текущего года к прошлогоднему, поэтому
/// нейтральное значение равно единице. Отклонение вверх означает, что компания
/// быстрее признаёт выручку, дольше держит дебиторскую задолженность, растит
/// долю мягких активов или замедляет амортизацию — то есть делает ровно то,
/// что делали компании, впоследствии уличённые в искажении отчётности.
/// </para>
/// <para>
/// Наибольший вес имеет индекс начислений TATA — разрыв между прибылью и
/// операционным денежным потоком. Именно он чаще всего и срабатывает: прибыль
/// можно нарисовать, деньги — нет. Порог отнесения к манипуляторам равен
/// -1,78; при нём модель ловит около 76% манипуляторов ценой примерно 17,5%
/// ложных срабатываний на здоровых компаниях.
/// </para>
/// </remarks>
public static class BeneishModel
{
    /// <summary>Рассчитывает M-балл по двум периодам отчётности.</summary>
    /// <param name="current">Отчётность анализируемого периода.</param>
    /// <param name="previous">Отчётность предыдущего периода.</param>
    /// <returns>Балл, вероятность манипуляции и разбор индексов.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Выручка или активы неположительны.</exception>
    public static BeneishResult Compute(FinancialStatement current, FinancialStatement previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);

        if (current.Revenue <= 0 || previous.Revenue <= 0)
            throw new ArgumentException("Выручка обоих периодов должна быть положительной.", nameof(current));
        if (current.TotalAssets <= 0 || previous.TotalAssets <= 0)
            throw new ArgumentException("Активы обоих периодов должны быть положительными.", nameof(current));

        double dsri = Ratio(
            Div(current.AccountsReceivable, current.Revenue),
            Div(previous.AccountsReceivable, previous.Revenue));

        double grossMarginCurrent = Div(current.GrossProfit, current.Revenue);
        double grossMarginPrevious = Div(previous.GrossProfit, previous.Revenue);
        double gmi = Ratio(grossMarginPrevious, grossMarginCurrent);

        double softCurrent = 1 - Div(current.CurrentAssets + current.PropertyPlantEquipment, current.TotalAssets);
        double softPrevious = 1 - Div(previous.CurrentAssets + previous.PropertyPlantEquipment, previous.TotalAssets);
        double aqi = Ratio(softCurrent, softPrevious);

        double sgi = Ratio(current.Revenue, previous.Revenue);

        double depreciationRateCurrent = Div(current.Depreciation, current.Depreciation + current.PropertyPlantEquipment);
        double depreciationRatePrevious = Div(previous.Depreciation, previous.Depreciation + previous.PropertyPlantEquipment);
        double depi = Ratio(depreciationRatePrevious, depreciationRateCurrent);

        double sgai = Ratio(
            Div(current.OperatingExpenses, current.Revenue),
            Div(previous.OperatingExpenses, previous.Revenue));

        double leverageCurrent = Div(current.CurrentLiabilities + current.LongTermDebt, current.TotalAssets);
        double leveragePrevious = Div(previous.CurrentLiabilities + previous.LongTermDebt, previous.TotalAssets);
        double lvgi = Ratio(leverageCurrent, leveragePrevious);

        double tata = Div(current.NetIncome - current.OperatingCashFlow, current.TotalAssets);

        var indices = new List<BeneishIndex>
        {
            new("CONST", "Свободный член", 1, -4.84, -4.84, 1,
                "калибровочная константа модели"),
            new("DSRI", "Индекс дебиторской задолженности", dsri, 0.920, 0.920 * dsri, 1,
                "рост срока сбора выручки относительно прошлого года"),
            new("GMI", "Индекс валовой маржи", gmi, 0.528, 0.528 * gmi, 1,
                "падение валовой маржи как мотив приукрасить результат"),
            new("AQI", "Индекс качества активов", aqi, 0.404, 0.404 * aqi, 1,
                "рост доли активов, не являющихся оборотными или основными средствами"),
            new("SGI", "Индекс роста выручки", sgi, 0.892, 0.892 * sgi, 1,
                "быстрый рост создаёт и возможность, и давление искажать отчётность"),
            new("DEPI", "Индекс амортизации", depi, 0.115, 0.115 * depi, 1,
                "замедление амортизации завышает прибыль"),
            new("SGAI", "Индекс коммерческих расходов", sgai, -0.172, -0.172 * sgai, 1,
                "опережающий рост расходов на продажи снижает подозрения"),
            new("TATA", "Индекс начислений", tata, 4.679, 4.679 * tata, 0.02,
                "разрыв между прибылью и денежным потоком относительно активов"),
            new("LVGI", "Индекс долговой нагрузки", lvgi, -0.327, -0.327 * lvgi, 1,
                "рост долга усиливает давление ковенант на отчётность"),
        };

        double m = indices.Sum(i => i.Contribution);

        return new BeneishResult
        {
            Company = current.Company,
            Period = current.Period,
            MScore = m,
            Probability = EconMath.NormalCdf(m),
            IsLikelyManipulator = m > -1.78,
            Threshold = -1.78,
            Indices = indices,
        };
    }

    /// <summary>Отношение показателей двух периодов с защитой от вырожденных значений.</summary>
    private static double Ratio(double numerator, double denominator) =>
        Math.Abs(denominator) < 1e-12 ? 1 : numerator / denominator;

    /// <summary>Деление с защитой от нулевого знаменателя.</summary>
    private static double Div(double numerator, double denominator) =>
        Math.Abs(denominator) < 1e-12 ? 0 : numerator / denominator;
}
