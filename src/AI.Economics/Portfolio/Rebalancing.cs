using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Portfolio;

/// <summary>Правило перебалансировки портфеля.</summary>
public enum RebalancingRule
{
    /// <summary>Без перебалансировки: веса дрейфуют вместе с рынком.</summary>
    BuyAndHold,

    /// <summary>По календарю с заданной периодичностью.</summary>
    Calendar,

    /// <summary>По достижении порога отклонения от целевых весов.</summary>
    Threshold,

    /// <summary>Частичная: сокращение отклонения на заданную долю.</summary>
    Partial,
}

/// <summary>Сделка перебалансировки.</summary>
/// <param name="Period">Номер периода.</param>
/// <param name="Turnover">Оборот в долях портфеля.</param>
/// <param name="Cost">Издержки сделки.</param>
/// <param name="RealizedGain">Реализованная прибыль, попадающая под налог.</param>
/// <param name="Tax">Налог с реализованной прибыли.</param>
public sealed record RebalanceTrade(
    int Period, double Turnover, double Cost, double RealizedGain, double Tax);

/// <summary>Результат моделирования правила перебалансировки.</summary>
public sealed record RebalancingResult : IInterpretable
{
    /// <summary>Использованное правило.</summary>
    public RebalancingRule Rule { get; init; }

    /// <summary>Названия активов.</summary>
    public IReadOnlyList<string> Assets { get; init; } = [];

    /// <summary>Целевые веса.</summary>
    public Vector TargetWeights { get; init; } = new(0);

    /// <summary>Итоговые веса.</summary>
    public Vector FinalWeights { get; init; } = new(0);

    /// <summary>Совершённые сделки.</summary>
    public IReadOnlyList<RebalanceTrade> Trades { get; init; } = [];

    /// <summary>Итоговая стоимость портфеля с учётом издержек и налогов.</summary>
    public double FinalValue { get; init; }

    /// <summary>Итоговая стоимость без издержек и налогов.</summary>
    public double GrossValue { get; init; }

    /// <summary>Суммарные издержки сделок.</summary>
    public double TotalCost { get; init; }

    /// <summary>Суммарный уплаченный налог.</summary>
    public double TotalTax { get; init; }

    /// <summary>Суммарный оборот за период.</summary>
    public double TotalTurnover { get; init; }

    /// <summary>Годовая доходность после издержек.</summary>
    public double AnnualReturn { get; init; }

    /// <summary>Годовая волатильность портфеля.</summary>
    public double Volatility { get; init; }

    /// <summary>Максимальное отклонение от целевых весов за период.</summary>
    public double MaximumDrift { get; init; }

    /// <summary>Число перебалансировок.</summary>
    public int RebalanceCount => Trades.Count;

    /// <summary>Потери доходности на издержках и налогах в годовом выражении.</summary>
    public double CostDrag { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool expensive = CostDrag > 0.005;
        double costShare = GrossValue > 0 ? (TotalCost + TotalTax) / GrossValue : 0;

        var builder = new InterpretationBuilder($"Перебалансировка: {RuleName()}")
            .Summary($"За период совершено {RebalanceCount} перебалансировок с суммарным " +
                     $"оборотом {Fmt.Pct(TotalTurnover, 0)}. Издержки {Fmt.Money(TotalCost)}, " +
                     $"налоги {Fmt.Money(TotalTax)} — вместе {Fmt.Pct(costShare, 2)} портфеля " +
                     $"и {Fmt.Pct(CostDrag, 3)} годовой доходности. Итоговая доходность " +
                     $"{Fmt.Pct(AnnualReturn, 2)} при волатильности {Fmt.Pct(Volatility, 2)}. " +
                     $"Максимальный дрейф весов {Fmt.Pct(MaximumDrift, 1)}.")
            .Metric("Доходность после издержек", AnnualReturn, null,
                $"потери на издержках {Fmt.Pct(CostDrag, 3)} годовых",
                MetricQuality.Neutral, 4)
            .Metric("Перебалансировок", RebalanceCount, null,
                $"суммарный оборот {Fmt.Pct(TotalTurnover, 0)}",
                RebalanceCount > 24 ? MetricQuality.Warning : MetricQuality.Neutral, 0)
            .Metric("Издержки и налоги", Fmt.Money(TotalCost + TotalTax), null,
                $"{Fmt.Pct(costShare, 2)} стоимости портфеля",
                expensive ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Потери доходности", CostDrag, null,
                "во сколько обходится следование правилу",
                expensive ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Максимальный дрейф", MaximumDrift, null,
                "наибольшее отклонение весов от целевых",
                MaximumDrift > 0.1 ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Волатильность", Volatility, null, "годовое стандартное отклонение",
                MetricQuality.Neutral, 4);

        for (int i = 0; i < Assets.Count && i < FinalWeights.Count; i++)
        {
            double target = i < TargetWeights.Count ? TargetWeights[i] : 0;

            builder.Metric(Assets[i], FinalWeights[i], null,
                $"целевой вес {Fmt.Pct(target, 1)}, отклонение " +
                $"{Fmt.Pct(FinalWeights[i] - target, 2)}",
                Math.Abs(FinalWeights[i] - target) > 0.05 ? MetricQuality.Warning : MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Перебалансировка — это компромисс между дрейфом риска и издержками. " +
                     "Портфель без перебалансировки постепенно смещается в самый выросший " +
                     "актив, и его риск перестаёт соответствовать заданному.")
            .FindingIf(Rule == RebalancingRule.BuyAndHold,
                $"Без перебалансировки веса ушли от целевых на {Fmt.Pct(MaximumDrift, 1)}. " +
                "Издержек нет, но и заявленный профиль риска не соблюдается.")
            .FindingIf(Rule == RebalancingRule.Threshold,
                "Пороговое правило совершает сделки только при существенном отклонении. " +
                "Обычно оно даёт меньший оборот при том же контроле риска, чем календарное.")
            .FindingIf(Rule == RebalancingRule.Partial,
                "Частичная перебалансировка сокращает отклонение не полностью. Это " +
                "снижает оборот и налог, оставляя риск в приемлемом коридоре.")
            .FindingIf(TotalTax > TotalCost,
                $"Налог ({Fmt.Money(TotalTax)}) превышает комиссии ({Fmt.Money(TotalCost)}). " +
                "На налогооблагаемом счёте главный аргумент против частой " +
                "перебалансировки — не комиссия, а досрочная реализация прибыли.")
            .WarningIf(expensive,
                $"Издержки съедают {Fmt.Pct(CostDrag, 3)} годовой доходности. Для портфеля " +
                "с ожидаемой премией в несколько процентов это существенная часть результата.")
            .WarningIf(RebalanceCount > 24,
                $"{RebalanceCount} перебалансировок за период. Такая частота редко " +
                "окупается: выигрыш от контроля весов быстро исчерпывается, а издержки растут линейно.")
            .Warning("Модель считает издержки пропорциональными обороту. В реальности " +
                     "к ним добавляется влияние на цену, которое растёт нелинейно " +
                     "с размером сделки и особенно заметно на неликвидных активах.")
            .Recommendation("Сравните пороговое правило с календарным на своих данных: " +
                            "при одинаковом контроле риска первое обычно даёт меньший оборот.")
            .Recommendation("На налогооблагаемом счёте направляйте новые взносы в " +
                            "недовешенные активы: это перебалансировка без реализации прибыли.")
            .Build();
    }

    /// <summary>Читаемое название правила.</summary>
    private string RuleName() => Rule switch
    {
        RebalancingRule.BuyAndHold => "без перебалансировки",
        RebalancingRule.Calendar => "по календарю",
        RebalancingRule.Threshold => "по порогу отклонения",
        _ => "частичная",
    };
}

/// <summary>
/// Моделирование правил перебалансировки с учётом издержек и налогов.
/// </summary>
/// <remarks>
/// <para>
/// Портфель без перебалансировки дрейфует: выросшие активы наращивают долю, и
/// риск постепенно перестаёт соответствовать заданному. Перебалансировка
/// возвращает веса к целевым, но стоит денег — комиссий и налога с
/// реализованной прибыли.
/// </para>
/// <para>
/// Правила различаются тем, когда именно совершать сделки. Календарное
/// перебалансирует с фиксированной периодичностью, пороговое — при отклонении
/// сверх заданного, частичное сокращает отклонение лишь на часть, оставляя
/// портфель в коридоре.
/// </para>
/// <para>
/// Налоговая составляющая часто важнее комиссионной. Продажа выросшего актива
/// реализует прибыль и вызывает налог, который иначе был бы отложен на годы.
/// Поэтому на налогооблагаемом счёте предпочтительны редкие частичные
/// перебалансировки и направление новых взносов в недовешенные активы.
/// </para>
/// </remarks>
public static class Rebalancing
{
    /// <summary>Моделирует правило перебалансировки на исторических доходностях.</summary>
    /// <param name="returns">Доходности активов: строка — период, столбец — актив.</param>
    /// <param name="targetWeights">Целевые веса.</param>
    /// <param name="rule">Правило перебалансировки.</param>
    /// <param name="assets">Названия активов.</param>
    /// <param name="transactionCost">Издержки в долях от оборота.</param>
    /// <param name="taxRate">Ставка налога на реализованную прибыль.</param>
    /// <param name="interval">Периодичность для календарного правила.</param>
    /// <param name="threshold">Порог отклонения для порогового правила.</param>
    /// <param name="partialShare">Доля сокращения отклонения для частичного правила.</param>
    /// <param name="periodsPerYear">Число периодов в году.</param>
    /// <returns>Итоговые веса, издержки и доходность после них.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static RebalancingResult Simulate(
        Matrix returns, Vector targetWeights, RebalancingRule rule = RebalancingRule.Threshold,
        IReadOnlyList<string>? assets = null, double transactionCost = 0.001,
        double taxRate = 0.13, int interval = 12, double threshold = 0.05,
        double partialShare = 0.5, int periodsPerYear = 12)
    {
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(targetWeights);

        int t = returns.Height, n = returns.Width;
        if (targetWeights.Count != n)
            throw new ArgumentException("Число весов должно совпадать с числом активов.", nameof(targetWeights));
        if (t < 2) throw new ArgumentException("Нужно минимум два периода.", nameof(returns));

        var names = new List<string>(n);
        for (int i = 0; i < n; i++)
            names.Add(assets is not null && i < assets.Count ? assets[i] : $"актив {i + 1}");

        // Учёт ведётся в деньгах, а не в весах: только так корректно
        // отслеживается налоговая база позиций при частичных продажах
        var positions = new double[n];
        var basis = new double[n];
        double gross = 1;

        for (int i = 0; i < n; i++)
        {
            positions[i] = targetWeights[i];
            basis[i] = targetWeights[i];
        }

        var trades = new List<RebalanceTrade>();
        var portfolioReturns = new Vector(t);
        double totalCost = 0, totalTax = 0, totalTurnover = 0, maximumDrift = 0;

        for (int period = 0; period < t; period++)
        {
            double previous = positions.Sum();

            for (int i = 0; i < n; i++) positions[i] *= 1 + returns[period, i];

            double value = positions.Sum();
            double periodReturn = previous > 0 ? (value / previous) - 1 : 0;

            portfolioReturns[period] = periodReturn;
            gross *= 1 + periodReturn;

            double drift = 0;
            for (int i = 0; i < n; i++)
                drift = Math.Max(drift, Math.Abs((value > 0 ? positions[i] / value : 0) - targetWeights[i]));

            maximumDrift = Math.Max(maximumDrift, drift);

            bool shouldRebalance = rule switch
            {
                RebalancingRule.BuyAndHold => false,
                RebalancingRule.Calendar => (period + 1) % Math.Max(1, interval) == 0,
                _ => drift > threshold,
            };

            if (!shouldRebalance || value <= 0) continue;

            double share = rule == RebalancingRule.Partial ? Math.Clamp(partialShare, 0.1, 1) : 1;
            double turnover = 0, realized = 0;

            for (int i = 0; i < n; i++)
            {
                double target = positions[i] + (((targetWeights[i] * value) - positions[i]) * share);
                double change = target - positions[i];

                turnover += Math.Abs(change) / (2 * value);

                if (change < 0 && positions[i] > 0)
                {
                    // Продажа реализует прибыль пропорционально доле проданного
                    double soldShare = Math.Min(-change / positions[i], 1);
                    realized += Math.Max((positions[i] - basis[i]) * soldShare, 0);
                    basis[i] *= 1 - soldShare;
                }
                else if (change > 0)
                {
                    basis[i] += change;
                }

                positions[i] = target;
            }

            double cost = turnover * transactionCost * value;
            double tax = realized * taxRate;
            double deduction = cost + tax;

            for (int i = 0; i < n; i++)
                positions[i] -= deduction * (value > 0 ? positions[i] / value : 0);

            totalCost += cost;
            totalTax += tax;
            totalTurnover += turnover;

            trades.Add(new RebalanceTrade(period + 1, turnover, cost, realized, tax));
        }

        double finalValue = positions.Sum();
        double years = (double)t / periodsPerYear;
        double netReturn = years > 0 ? Math.Pow(Math.Max(finalValue, 1e-9), 1 / years) - 1 : 0;
        double grossReturn = years > 0 ? Math.Pow(Math.Max(gross, 1e-9), 1 / years) - 1 : 0;

        double mean = portfolioReturns.Average();
        double variance = portfolioReturns.Sum(r => (r - mean) * (r - mean)) / Math.Max(1, t - 1);

        var final = new Vector(n);
        for (int i = 0; i < n; i++) final[i] = finalValue > 0 ? positions[i] / finalValue : 0;

        return new RebalancingResult
        {
            Rule = rule,
            Assets = names,
            TargetWeights = targetWeights,
            FinalWeights = final,
            Trades = trades,
            FinalValue = finalValue,
            GrossValue = gross,
            TotalCost = totalCost,
            TotalTax = totalTax,
            TotalTurnover = totalTurnover,
            AnnualReturn = netReturn,
            Volatility = Math.Sqrt(Math.Max(variance, 0)) * Math.Sqrt(periodsPerYear),
            MaximumDrift = maximumDrift,
            CostDrag = grossReturn - netReturn,
        };
    }

    /// <summary>Сравнивает все правила перебалансировки на одних данных.</summary>
    /// <param name="returns">Доходности активов.</param>
    /// <param name="targetWeights">Целевые веса.</param>
    /// <param name="assets">Названия активов.</param>
    /// <param name="transactionCost">Издержки в долях от оборота.</param>
    /// <param name="taxRate">Ставка налога.</param>
    /// <returns>Результаты по убыванию доходности после издержек.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static IReadOnlyList<RebalancingResult> CompareRules(
        Matrix returns, Vector targetWeights, IReadOnlyList<string>? assets = null,
        double transactionCost = 0.001, double taxRate = 0.13)
    {
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(targetWeights);

        var results = new List<RebalancingResult>();

        foreach (RebalancingRule rule in Enum.GetValues<RebalancingRule>())
            results.Add(Simulate(returns, targetWeights, rule, assets, transactionCost, taxRate));

        return [.. results.OrderByDescending(r => r.AnnualReturn)];
    }
}
