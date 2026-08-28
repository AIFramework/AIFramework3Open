using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Statements;

/// <summary>Множитель разложения Дюпона.</summary>
/// <param name="Name">Название множителя.</param>
/// <param name="Value">Значение в анализируемом периоде.</param>
/// <param name="Previous">Значение в предыдущем периоде; совпадает с текущим, если сравнения нет.</param>
/// <param name="Contribution">Вклад изменения множителя в изменение рентабельности капитала.</param>
/// <param name="Meaning">Экономический смысл множителя.</param>
public sealed record DuPontFactor(
    string Name, double Value, double Previous, double Contribution, string Meaning)
{
    /// <summary>Изменение множителя за период.</summary>
    public double Change => Value - Previous;

    /// <summary>Относительное изменение множителя.</summary>
    public double RelativeChange => Math.Abs(Previous) > 1e-12 ? (Value - Previous) / Math.Abs(Previous) : 0;
}

/// <summary>Результат разложения рентабельности капитала по Дюпону.</summary>
public sealed record DuPontResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Рентабельность собственного капитала.</summary>
    public double ReturnOnEquity { get; init; }

    /// <summary>Рентабельность капитала в предыдущем периоде.</summary>
    public double PreviousReturnOnEquity { get; init; }

    /// <summary>Трёхфакторное разложение.</summary>
    public IReadOnlyList<DuPontFactor> ThreeFactor { get; init; } = [];

    /// <summary>Пятифакторное разложение.</summary>
    public IReadOnlyList<DuPontFactor> FiveFactor { get; init; } = [];

    /// <summary>Есть ли сопоставление с предыдущим периодом.</summary>
    public bool HasComparison { get; init; }

    /// <summary>Изменение рентабельности капитала за период.</summary>
    public double Change => ReturnOnEquity - PreviousReturnOnEquity;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        DuPontFactor? margin = FiveFactor.FirstOrDefault(f => f.Name.Contains("рентабельность", StringComparison.OrdinalIgnoreCase));
        DuPontFactor? leverage = FiveFactor.FirstOrDefault(f => f.Name.Contains("рычаг", StringComparison.OrdinalIgnoreCase));
        DuPontFactor? turnover = FiveFactor.FirstOrDefault(f => f.Name.Contains("Оборачиваемость", StringComparison.Ordinal));
        DuPontFactor? interest = FiveFactor.FirstOrDefault(f => f.Name.Contains("процент", StringComparison.OrdinalIgnoreCase));

        DuPontFactor? driver = HasComparison
            ? FiveFactor.OrderByDescending(f => Math.Abs(f.Contribution)).FirstOrDefault()
            : null;

        bool leverageDriven = leverage is not null && leverage.Value > 2.5;

        var builder = new InterpretationBuilder($"Разложение Дюпона: {Company}, {Period}")
            .Summary($"Рентабельность собственного капитала {Fmt.Pct(ReturnOnEquity, 2)}. " +
                     (HasComparison
                         ? $"За период она изменилась на {Fmt.Pct(Change, 2)}, и главный вклад " +
                           $"внёс множитель «{driver?.Name}» ({Fmt.Pct(driver?.Contribution ?? 0, 2)}). "
                         : "Сопоставление с предыдущим периодом не задано, поэтому показан только уровень. ") +
                     $"Прибыль складывается из маржи {Fmt.Pct(margin?.Value ?? 0, 2)}, оборачиваемости " +
                     $"{Fmt.Num(turnover?.Value ?? 0, 2)} и рычага {Fmt.Num(leverage?.Value ?? 0, 2)}.")
            .Metric("Рентабельность капитала", ReturnOnEquity, null,
                HasComparison ? $"было {Fmt.Pct(PreviousReturnOnEquity, 2)}" : "за период",
                ReturnOnEquity >= 0.15 ? MetricQuality.Good
                    : ReturnOnEquity >= 0 ? MetricQuality.Neutral : MetricQuality.Critical, 4);

        foreach (DuPontFactor factor in ThreeFactor)
        {
            builder.Metric($"3ф: {factor.Name}", factor.Value, null,
                HasComparison
                    ? $"{factor.Meaning}; вклад в изменение {Fmt.Pct(factor.Contribution, 2)}"
                    : factor.Meaning,
                MetricQuality.Unknown, 4);
        }

        foreach (DuPontFactor factor in FiveFactor)
        {
            builder.Metric($"5ф: {factor.Name}", factor.Value, null,
                HasComparison
                    ? $"{factor.Meaning}; вклад в изменение {Fmt.Pct(factor.Contribution, 2)}"
                    : factor.Meaning,
                MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Разложение отвечает на вопрос, откуда берётся доходность собственника: " +
                     "из маржи, из скорости оборота или из долга. Одинаковая рентабельность " +
                     "капитала у двух компаний может означать совершенно разные бизнес-модели " +
                     "и совершенно разный риск.")
            .FindingIf(leverageDriven,
                $"Финансовый рычаг {Fmt.Num(leverage?.Value ?? 0, 2)} — заметная часть " +
                "рентабельности капитала создаётся долгом, а не операционной эффективностью. " +
                "При падении выручки этот же рычаг ускорит падение прибыли.")
            .FindingIf(HasComparison && driver is not null,
                $"Изменение рентабельности капитала на {Fmt.Pct(Change, 2)} почти целиком " +
                $"объясняется множителем «{driver?.Name}»: его вклад {Fmt.Pct(driver?.Contribution ?? 0, 2)} " +
                $"при изменении самого множителя на {Fmt.Pct(driver?.RelativeChange ?? 0, 1)}.")
            .FindingIf(interest is not null && interest.Value < 0.7,
                $"Процентное бремя {Fmt.Num(interest?.Value ?? 0, 2)}: проценты съедают более " +
                "трети операционной прибыли. Дальнейший рост долга будет уменьшать, " +
                "а не увеличивать доходность собственников.")
            .WarningIf(leverageDriven,
                "Высокий рычаг делает рентабельность капитала плохой метрикой сравнения. " +
                "Сопоставляйте такие компании по рентабельности активов или инвестированного капитала.")
            .WarningIf(!HasComparison,
                "Расчёт выполнен по одному периоду. Вклады множителей в изменение равны нулю " +
                "по построению, а не потому, что бизнес не менялся.")
            .Warning("Разложение мультипликативно, поэтому вклады множителей зависят от " +
                     "порядка подстановки. Здесь применена последовательная подстановка " +
                     "слева направо: сумма вкладов точно равна изменению рентабельности, " +
                     "но перестановка множителей слегка изменит распределение между ними.")
            .Recommendation("Сравнивайте множители с отраслевыми: низкая маржа при высокой " +
                            "оборачиваемости — нормальная модель ритейла и аномалия для производства.")
            .Recommendation("Если рентабельность капитала держится рычагом, проверьте " +
                            "покрытие процентов и график погашения долга до того, как считать " +
                            "такую доходность устойчивой.")
            .Build();
    }
}

/// <summary>
/// Разложение рентабельности собственного капитала по методу Дюпона.
/// </summary>
/// <remarks>
/// <para>
/// Трёхфакторная модель раскладывает рентабельность капитала на маржу,
/// оборачиваемость активов и финансовый рычаг:
/// </para>
/// <code>
/// ROE = (NetIncome / Revenue) * (Revenue / Assets) * (Assets / Equity)
/// </code>
/// <para>
/// Пятифакторная дополнительно отделяет налоговое и процентное бремя от
/// операционной эффективности:
/// </para>
/// <code>
/// ROE = (NetIncome / EBT) * (EBT / EBIT) * (EBIT / Revenue) * (Revenue / Assets) * (Assets / Equity)
/// </code>
/// <para>
/// При наличии отчётности предыдущего периода вклад каждого множителя в
/// изменение рентабельности считается методом последовательной подстановки:
/// множители заменяются на новые значения по одному, и прирост на каждом шаге
/// относится к соответствующему фактору. Сумма вкладов точно равна изменению
/// рентабельности, что делает разложение пригодным для управленческой отчётности.
/// </para>
/// </remarks>
public static class DuPontAnalysis
{
    /// <summary>Раскладывает рентабельность капитала на множители.</summary>
    /// <param name="current">Отчётность анализируемого периода.</param>
    /// <param name="previous">Отчётность предыдущего периода для анализа изменений.</param>
    /// <returns>Трёх- и пятифакторное разложение с вкладами в изменение.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Выручка, активы или капитал неположительны.</exception>
    public static DuPontResult Analyze(FinancialStatement current, FinancialStatement? previous = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.Revenue <= 0)
            throw new ArgumentException("Выручка должна быть положительной.", nameof(current));
        if (current.TotalAssets <= 0)
            throw new ArgumentException("Активы должны быть положительными.", nameof(current));
        if (current.Equity <= 0)
            throw new ArgumentException(
                "Собственный капитал должен быть положительным: при отрицательном капитале " +
                "рентабельность капитала не интерпретируется.", nameof(current));

        bool hasComparison = previous is not null && previous.Revenue > 0 &&
                             previous.TotalAssets > 0 && previous.Equity > 0;

        double[] currentFive = FiveFactors(current);
        double[] currentThree = ThreeFactors(current);
        double[] previousFive = hasComparison ? FiveFactors(previous!) : currentFive;
        double[] previousThree = hasComparison ? ThreeFactors(previous!) : currentThree;

        double[] fiveContributions = Contributions(previousFive, currentFive);
        double[] threeContributions = Contributions(previousThree, currentThree);

        string[] fiveNames =
        [
            "Налоговое бремя", "Процентное бремя", "Операционная рентабельность",
            "Оборачиваемость активов", "Финансовый рычаг",
        ];

        string[] fiveMeanings =
        [
            "какая доля прибыли до налога остаётся после налога",
            "какая доля операционной прибыли остаётся после процентов",
            "операционная прибыль на рубль выручки",
            "выручка на рубль активов",
            "активы на рубль собственного капитала",
        ];

        string[] threeNames = ["Чистая рентабельность", "Оборачиваемость активов", "Финансовый рычаг"];
        string[] threeMeanings =
        [
            "чистая прибыль на рубль выручки",
            "выручка на рубль активов",
            "активы на рубль собственного капитала",
        ];

        var five = new List<DuPontFactor>(5);
        for (int i = 0; i < 5; i++)
            five.Add(new DuPontFactor(fiveNames[i], currentFive[i], previousFive[i], fiveContributions[i], fiveMeanings[i]));

        var three = new List<DuPontFactor>(3);
        for (int i = 0; i < 3; i++)
            three.Add(new DuPontFactor(threeNames[i], currentThree[i], previousThree[i], threeContributions[i], threeMeanings[i]));

        return new DuPontResult
        {
            Company = current.Company,
            Period = current.Period,
            ReturnOnEquity = currentThree.Aggregate(1.0, (a, b) => a * b),
            PreviousReturnOnEquity = previousThree.Aggregate(1.0, (a, b) => a * b),
            ThreeFactor = three,
            FiveFactor = five,
            HasComparison = hasComparison,
        };
    }

    /// <summary>Множители трёхфакторной модели.</summary>
    private static double[] ThreeFactors(FinancialStatement s) =>
    [
        s.NetIncome / s.Revenue,
        s.Revenue / s.TotalAssets,
        s.TotalAssets / s.Equity,
    ];

    /// <summary>Множители пятифакторной модели.</summary>
    private static double[] FiveFactors(FinancialStatement s)
    {
        double pretax = Math.Abs(s.PretaxIncome) > 1e-9 ? s.PretaxIncome : 1e-9;
        double ebit = Math.Abs(s.OperatingIncome) > 1e-9 ? s.OperatingIncome : 1e-9;

        return
        [
            s.NetIncome / pretax,
            pretax / ebit,
            ebit / s.Revenue,
            s.Revenue / s.TotalAssets,
            s.TotalAssets / s.Equity,
        ];
    }

    /// <summary>Вклады множителей в изменение произведения методом последовательной подстановки.</summary>
    private static double[] Contributions(double[] previous, double[] current)
    {
        var contributions = new double[current.Length];
        var state = (double[])previous.Clone();
        double baseline = state.Aggregate(1.0, (a, b) => a * b);

        for (int i = 0; i < current.Length; i++)
        {
            state[i] = current[i];
            double updated = state.Aggregate(1.0, (a, b) => a * b);
            contributions[i] = updated - baseline;
            baseline = updated;
        }

        return contributions;
    }
}
