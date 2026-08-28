using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Projects;

/// <summary>Результат анализа безубыточности и рычагов.</summary>
public sealed record BreakEvenResult : IInterpretable
{
    /// <summary>Название продукта или бизнеса.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Точка безубыточности в натуральных единицах.</summary>
    public double BreakEvenUnits { get; init; }

    /// <summary>Точка безубыточности в деньгах.</summary>
    public double BreakEvenRevenue { get; init; }

    /// <summary>Объём, обеспечивающий заданную целевую прибыль.</summary>
    public double TargetUnits { get; init; }

    /// <summary>Маржинальная прибыль на единицу.</summary>
    public double ContributionPerUnit { get; init; }

    /// <summary>Норма маржинальной прибыли.</summary>
    public double ContributionMargin { get; init; }

    /// <summary>Запас прочности по объёму.</summary>
    public double MarginOfSafety { get; init; }

    /// <summary>Операционный рычаг.</summary>
    public double OperatingLeverage { get; init; }

    /// <summary>Финансовый рычаг.</summary>
    public double FinancialLeverage { get; init; }

    /// <summary>Совокупный рычаг.</summary>
    public double CombinedLeverage => OperatingLeverage * FinancialLeverage;

    /// <summary>Операционная прибыль при текущем объёме.</summary>
    public double OperatingProfit { get; init; }

    /// <summary>Чистая прибыль при текущем объёме.</summary>
    public double NetProfit { get; init; }

    /// <summary>Падение объёма, обнуляющее чистую прибыль.</summary>
    public double UnitsToZeroProfit { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool risky = CombinedLeverage > 5;
        bool thinSafety = MarginOfSafety < 0.2;

        return new InterpretationBuilder($"Безубыточность и рычаги: {Name}")
            .Summary($"Точка безубыточности {Fmt.Int(BreakEvenUnits)} единиц " +
                     $"({Fmt.Money(BreakEvenRevenue)}), запас прочности " +
                     $"{Fmt.Pct(MarginOfSafety, 1)}. Маржинальная прибыль " +
                     $"{Fmt.Money(ContributionPerUnit)} на единицу " +
                     $"({Fmt.Pct(ContributionMargin, 1)} от цены). Операционный рычаг " +
                     $"{Fmt.Num(OperatingLeverage, 2)}, финансовый {Fmt.Num(FinancialLeverage, 2)}, " +
                     $"совокупный {Fmt.Num(CombinedLeverage, 2)}.")
            .Metric("Точка безубыточности", BreakEvenUnits, "ед.",
                $"в деньгах {Fmt.Money(BreakEvenRevenue)}", MetricQuality.Neutral, 0)
            .Metric("Запас прочности", MarginOfSafety, null,
                "на сколько может упасть объём до нуля прибыли",
                MarginOfSafety > 0.3 ? MetricQuality.Good
                    : MarginOfSafety > 0.15 ? MetricQuality.Warning : MetricQuality.Critical, 3)
            .Metric("Маржинальная прибыль", Fmt.Money(ContributionPerUnit), null,
                $"{Fmt.Pct(ContributionMargin, 1)} от цены",
                ContributionMargin > 0.3 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Операционный рычаг", OperatingLeverage, "×",
                "во сколько раз прибыль реагирует на изменение выручки",
                OperatingLeverage > 3 ? MetricQuality.Warning : MetricQuality.Neutral, 2)
            .Metric("Финансовый рычаг", FinancialLeverage, "×",
                "усиление колебаний прибыли долгом",
                FinancialLeverage > 2 ? MetricQuality.Warning : MetricQuality.Neutral, 2)
            .Metric("Совокупный рычаг", CombinedLeverage, "×",
                "изменение чистой прибыли на процент изменения выручки",
                risky ? MetricQuality.Critical : MetricQuality.Neutral, 2)
            .Metric("Целевой объём", TargetUnits, "ед.",
                "для достижения заданной прибыли", MetricQuality.Neutral, 0)
            .Metric("Операционная прибыль", Fmt.Money(OperatingProfit), null,
                $"чистая {Fmt.Money(NetProfit)}",
                OperatingProfit > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .Finding($"Совокупный рычаг {Fmt.Num(CombinedLeverage, 2)} означает: падение " +
                     $"выручки на 10% сокращает чистую прибыль примерно на " +
                     $"{Fmt.Pct(CombinedLeverage * 0.1, 0)}. Это и есть цена постоянных " +
                     "затрат и долга, взятая вместе.")
            .FindingIf(OperatingLeverage > FinancialLeverage * 1.5,
                "Основной источник чувствительности — постоянные затраты, а не долг. " +
                "Снижение риска здесь достигается переводом затрат в переменные, " +
                "а не изменением структуры финансирования.")
            .FindingIf(FinancialLeverage > OperatingLeverage * 1.5,
                "Основной источник чувствительности — долг. Операционная модель " +
                "устойчива, риск создан структурой финансирования и снимается ею же.")
            .WarningIf(thinSafety,
                $"Запас прочности всего {Fmt.Pct(MarginOfSafety, 1)}. Обычное сезонное " +
                "колебание спроса уводит бизнес в убыток.")
            .WarningIf(risky,
                $"Совокупный рычаг {Fmt.Num(CombinedLeverage, 2)} очень высок. Сочетание " +
                "больших постоянных затрат и долга делает прибыль крайне неустойчивой: " +
                "такая конфигурация не переживает спад.")
            .Warning("Анализ предполагает линейность: цена и переменные затраты на единицу " +
                     "постоянны, а постоянные затраты не меняются в рассматриваемом " +
                     "диапазоне объёмов. За пределами этого диапазона выводы неверны.")
            .Recommendation("Считайте точку безубыточности по маржинальной прибыли, а не " +
                            "по валовой: в неё нельзя включать переменную часть " +
                            "коммерческих расходов, иначе порог занижается.")
            .Recommendation("При высоком операционном рычаге сначала работайте с постоянными " +
                            "затратами, и только потом с ценой: эффект на устойчивость " +
                            "у первого способа больше.")
            .Build();
    }
}

/// <summary>Точка на кривой оптимальной структуры капитала.</summary>
/// <param name="DebtShare">Доля долга в капитале.</param>
/// <param name="CostOfEquity">Стоимость собственного капитала.</param>
/// <param name="CostOfDebt">Стоимость долга до налога.</param>
/// <param name="Wacc">Средневзвешенная стоимость капитала.</param>
/// <param name="FirmValue">Стоимость компании.</param>
public sealed record CapitalStructurePoint(
    double DebtShare, double CostOfEquity, double CostOfDebt, double Wacc, double FirmValue);

/// <summary>Результат подбора структуры капитала.</summary>
public sealed record CapitalStructureResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Кривая по долям долга.</summary>
    public IReadOnlyList<CapitalStructurePoint> Curve { get; init; } = [];

    /// <summary>Оптимальная доля долга.</summary>
    public double OptimalDebtShare { get; init; }

    /// <summary>Минимальная средневзвешенная стоимость капитала.</summary>
    public double MinimumWacc { get; init; }

    /// <summary>Текущая доля долга.</summary>
    public double CurrentDebtShare { get; init; }

    /// <summary>Средневзвешенная стоимость капитала при текущей структуре.</summary>
    public double CurrentWacc { get; init; }

    /// <summary>Прирост стоимости компании при переходе к оптимальной структуре.</summary>
    public double ValueGain { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double improvement = CurrentWacc - MinimumWacc;
        bool nearOptimal = Math.Abs(CurrentDebtShare - OptimalDebtShare) < 0.05;

        var builder = new InterpretationBuilder($"Оптимальная структура капитала: {Company}")
            .Summary($"Минимум стоимости капитала {Fmt.Pct(MinimumWacc, 2)} достигается при " +
                     $"доле долга {Fmt.Pct(OptimalDebtShare, 0)}. Текущая структура — " +
                     $"{Fmt.Pct(CurrentDebtShare, 0)} долга при ставке {Fmt.Pct(CurrentWacc, 2)}. " +
                     $"Переход к оптимуму снижает ставку на {Fmt.Pct(improvement, 2)} " +
                     $"и добавляет {Fmt.Money(ValueGain)} стоимости.")
            .Metric("Оптимальная доля долга", OptimalDebtShare, null,
                $"текущая {Fmt.Pct(CurrentDebtShare, 0)}",
                nearOptimal ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Минимальная ставка", MinimumWacc, null,
                $"текущая {Fmt.Pct(CurrentWacc, 2)}", MetricQuality.Neutral, 4)
            .Metric("Снижение ставки", improvement, null,
                "разница между текущей и минимальной",
                improvement > 0.005 ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Прирост стоимости", Fmt.Money(ValueGain), null,
                "от перехода к оптимальной структуре",
                ValueGain > 0 ? MetricQuality.Good : MetricQuality.Neutral);

        foreach (CapitalStructurePoint point in Curve)
        {
            builder.Metric($"Доля долга {Fmt.Pct(point.DebtShare, 0)}", point.Wacc, null,
                $"стоимость капитала {Fmt.Pct(point.CostOfEquity, 1)}, долга " +
                $"{Fmt.Pct(point.CostOfDebt, 1)}, стоимость компании {Fmt.Money(point.FirmValue)}",
                Math.Abs(point.DebtShare - OptimalDebtShare) < 1e-9 ? MetricQuality.Good : MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Кривая стоимости капитала имеет минимум, потому что налоговый щит " +
                     "растёт линейно с долгом, а издержки финансовых затруднений — быстрее. " +
                     "Оптимум лежит там, где предельная выгода от щита сравнивается " +
                     "с предельным ростом стоимости капитала и долга.")
            .FindingIf(nearOptimal,
                "Текущая структура близка к оптимальной: выигрыш от её изменения " +
                "меньше, чем издержки самой операции.")
            .FindingIf(CurrentDebtShare < OptimalDebtShare - 0.05,
                $"Компания недоиспользует долг: {Fmt.Pct(CurrentDebtShare, 0)} против " +
                $"оптимальных {Fmt.Pct(OptimalDebtShare, 0)}. Привлечение долга снизит " +
                "стоимость капитала, но повысит чувствительность прибыли к спаду.")
            .FindingIf(CurrentDebtShare > OptimalDebtShare + 0.05,
                $"Долговая нагрузка выше оптимальной ({Fmt.Pct(CurrentDebtShare, 0)} против " +
                $"{Fmt.Pct(OptimalDebtShare, 0)}). Налоговый щит уже не компенсирует " +
                "рост стоимости заимствований.")
            .WarningIf(OptimalDebtShare > 0.6,
                $"Расчётный оптимум {Fmt.Pct(OptimalDebtShare, 0)} очень высок. " +
                "Проверьте, как задан рост стоимости долга с нагрузкой: при слишком " +
                "плоской кривой модель всегда рекомендует максимум долга.")
            .Warning("Кривая построена на предпосылке о том, как стоимость долга растёт " +
                     "с нагрузкой. Эта зависимость не наблюдается напрямую и задаётся " +
                     "экспертно, поэтому положение оптимума следует считать ориентиром, " +
                     "а не точкой.")
            .Recommendation("Сопоставьте оптимум с ковенантами действующих кредитов " +
                            "и с рейтинговыми требованиями: формальный минимум ставки " +
                            "может оказаться недостижимым.")
            .Build();
    }
}

/// <summary>
/// Анализ безубыточности, операционного и финансового рычага, подбор структуры
/// капитала.
/// </summary>
/// <remarks>
/// <para>
/// Точка безубыточности определяется маржинальной прибылью — разницей цены и
/// переменных затрат на единицу:
/// </para>
/// <code>
/// BE = FixedCosts / (Price - VariableCost)
/// DOL = Contribution / EBIT
/// DFL = EBIT / (EBIT - Interest)
/// DCL = DOL * DFL
/// </code>
/// <para>
/// Операционный рычаг показывает, во сколько раз прибыль реагирует на изменение
/// выручки, финансовый — во сколько раз чистая прибыль реагирует на изменение
/// операционной. Их произведение и есть настоящая мера чувствительности бизнеса
/// к спаду.
/// </para>
/// <para>
/// Оптимальная структура капитала находится минимизацией средневзвешенной
/// стоимости: налоговый щит снижает её линейно, а рост стоимости долга и
/// собственного капитала с нагрузкой — повышает быстрее, что и создаёт минимум.
/// </para>
/// </remarks>
public static class BreakEven
{
    /// <summary>Рассчитывает безубыточность и рычаги.</summary>
    /// <param name="price">Цена единицы.</param>
    /// <param name="variableCost">Переменные затраты на единицу.</param>
    /// <param name="fixedCosts">Постоянные затраты за период.</param>
    /// <param name="volume">Текущий объём продаж.</param>
    /// <param name="interest">Процентные расходы за период.</param>
    /// <param name="targetProfit">Целевая операционная прибыль.</param>
    /// <param name="taxRate">Ставка налога на прибыль.</param>
    /// <param name="name">Название продукта или бизнеса.</param>
    /// <returns>Точка безубыточности, запас прочности и рычаги.</returns>
    /// <exception cref="ArgumentException">Маржинальная прибыль неположительна.</exception>
    public static BreakEvenResult Analyze(
        double price, double variableCost, double fixedCosts, double volume,
        double interest = 0, double targetProfit = 0, double taxRate = 0.2, string name = "продукт")
    {
        double contribution = price - variableCost;

        if (contribution <= 0)
            throw new ArgumentException(
                "Цена должна превышать переменные затраты на единицу.", nameof(price));
        if (volume < 0)
            throw new ArgumentException("Объём не может быть отрицательным.", nameof(volume));

        double breakEvenUnits = fixedCosts / contribution;
        double totalContribution = contribution * volume;
        double operatingProfit = totalContribution - fixedCosts;
        double pretax = operatingProfit - interest;
        double net = pretax > 0 ? pretax * (1 - taxRate) : pretax;

        double operatingLeverage = Math.Abs(operatingProfit) > 1e-9
            ? totalContribution / operatingProfit
            : double.PositiveInfinity;

        double financialLeverage = Math.Abs(pretax) > 1e-9
            ? operatingProfit / pretax
            : double.PositiveInfinity;

        double zeroProfitUnits = (fixedCosts + interest) / contribution;

        return new BreakEvenResult
        {
            Name = name,
            BreakEvenUnits = breakEvenUnits,
            BreakEvenRevenue = breakEvenUnits * price,
            TargetUnits = (fixedCosts + targetProfit) / contribution,
            ContributionPerUnit = contribution,
            ContributionMargin = price > 0 ? contribution / price : 0,
            MarginOfSafety = volume > 0 ? (volume - breakEvenUnits) / volume : 0,
            OperatingLeverage = double.IsFinite(operatingLeverage) ? operatingLeverage : 0,
            FinancialLeverage = double.IsFinite(financialLeverage) ? financialLeverage : 0,
            OperatingProfit = operatingProfit,
            NetProfit = net,
            UnitsToZeroProfit = zeroProfitUnits,
        };
    }

    /// <summary>Подбирает структуру капитала, минимизирующую стоимость капитала.</summary>
    /// <param name="company">Название компании.</param>
    /// <param name="unleveredCostOfEquity">Стоимость капитала без долга.</param>
    /// <param name="baseCostOfDebt">Стоимость долга при нулевой нагрузке.</param>
    /// <param name="taxRate">Ставка налога.</param>
    /// <param name="operatingProfit">Операционная прибыль для расчёта стоимости компании.</param>
    /// <param name="currentDebtShare">Текущая доля долга.</param>
    /// <param name="distressSlope">Скорость роста стоимости долга с нагрузкой.</param>
    /// <param name="steps">Число точек кривой.</param>
    /// <returns>Кривая стоимости капитала и положение оптимума.</returns>
    /// <exception cref="ArgumentException">Параметры вне допустимого диапазона.</exception>
    public static CapitalStructureResult OptimalStructure(
        string company, double unleveredCostOfEquity, double baseCostOfDebt, double taxRate,
        double operatingProfit, double currentDebtShare = 0.3,
        double distressSlope = 0.35, int steps = 19)
    {
        if (unleveredCostOfEquity <= 0)
            throw new ArgumentException("Стоимость капитала должна быть положительной.", nameof(unleveredCostOfEquity));
        if (steps < 3)
            throw new ArgumentException("Нужно минимум три точки кривой.", nameof(steps));

        var curve = new List<CapitalStructurePoint>(steps);
        double bestWacc = double.MaxValue, bestShare = 0;
        double nopat = operatingProfit * (1 - taxRate);

        for (int i = 0; i < steps; i++)
        {
            double share = i * 0.9 / (steps - 1);
            double ratio = share < 1 ? share / (1 - share) : 100;

            // Стоимость долга растёт квадратично: издержки затруднений ускоряются
            double costOfDebt = baseCostOfDebt + (distressSlope * share * share);
            double costOfEquity = unleveredCostOfEquity
                + ((unleveredCostOfEquity - baseCostOfDebt) * (1 - taxRate) * ratio);

            double wacc = (costOfEquity * (1 - share)) + (costOfDebt * (1 - taxRate) * share);
            double firmValue = wacc > 1e-6 ? nopat / wacc : 0;

            curve.Add(new CapitalStructurePoint(share, costOfEquity, costOfDebt, wacc, firmValue));

            if (wacc < bestWacc) { bestWacc = wacc; bestShare = share; }
        }

        CapitalStructurePoint current = curve
            .OrderBy(p => Math.Abs(p.DebtShare - currentDebtShare))
            .First();

        double optimalValue = bestWacc > 1e-6 ? nopat / bestWacc : 0;

        return new CapitalStructureResult
        {
            Company = company,
            Curve = curve,
            OptimalDebtShare = bestShare,
            MinimumWacc = bestWacc,
            CurrentDebtShare = current.DebtShare,
            CurrentWacc = current.Wacc,
            ValueGain = optimalValue - current.FirmValue,
        };
    }
}
