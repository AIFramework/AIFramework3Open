using System;
using System.Collections.Generic;
using AI.DataStructs.Algebraic;
using AI.Econometrics.Numerics;
using AI.Statistics;

using AI.Insights;

namespace AI.Economics.Runway;

/// <summary>Поступление денег в определённый месяц (транш инвестиций, грант, кредит).</summary>
/// <param name="Month">Номер месяца от старта симуляции.</param>
/// <param name="Amount">Сумма поступления.</param>
/// <param name="Probability">Вероятность того, что поступление состоится.</param>
public sealed record FundingEvent(int Month, double Amount, double Probability = 1.0);

/// <summary>Параметры стохастической модели денежного потока.</summary>
public sealed record RunwayInput
{
    /// <summary>Денежные средства на старте.</summary>
    public double Cash { get; init; }

    /// <summary>Выручка первого месяца.</summary>
    public double MonthlyRevenue { get; init; }

    /// <summary>Средний месячный темп роста выручки, доля (0,08 — рост на 8 %).</summary>
    public double RevenueGrowthMean { get; init; }

    /// <summary>Волатильность месячного темпа роста выручки.</summary>
    public double RevenueGrowthVolatility { get; init; }

    /// <summary>Доля валовой маржи в выручке.</summary>
    public double GrossMarginRate { get; init; } = 0.8;

    /// <summary>Операционные затраты первого месяца.</summary>
    public double MonthlyCosts { get; init; }

    /// <summary>Средний месячный темп роста затрат, доля.</summary>
    public double CostGrowthMean { get; init; }

    /// <summary>Волатильность месячного темпа роста затрат.</summary>
    public double CostGrowthVolatility { get; init; }

    /// <summary>Горизонт симуляции в месяцах.</summary>
    public int Horizon { get; init; } = 36;

    /// <summary>Число траекторий Монте-Карло.</summary>
    public int Simulations { get; init; } = 5000;

    /// <summary>Зерно генератора для воспроизводимости.</summary>
    public int Seed { get; init; } = 42;

    /// <summary>Запланированные поступления денег.</summary>
    public IReadOnlyList<FundingEvent>? Funding { get; init; }
}

/// <summary>Результат стохастической оценки запаса прочности.</summary>
public sealed partial record RunwayResult
{
    /// <summary>
    /// Детерминированная оценка «касса делить на средний burn» — то, что
    /// обычно называют runway. Приводится для сравнения с распределением.
    /// </summary>
    public double DeterministicRunwayMonths { get; init; }

    /// <summary>Пессимистичный срок исчерпания денег (10-й процентиль), месяцев.</summary>
    public double CashOutP10 { get; init; }

    /// <summary>Медианный срок исчерпания денег, месяцев.</summary>
    public double CashOutP50 { get; init; }

    /// <summary>Оптимистичный срок исчерпания денег (90-й процентиль), месяцев.</summary>
    public double CashOutP90 { get; init; }

    /// <summary>Доля траекторий, дошедших до конца горизонта без исчерпания денег.</summary>
    public double SurvivalProbability { get; init; }

    /// <summary>Вероятность исчерпания денег в ближайшие шесть месяцев.</summary>
    public double ProbabilityCashOutIn6 { get; init; }

    /// <summary>Вероятность исчерпания денег в ближайшие двенадцать месяцев.</summary>
    public double ProbabilityCashOutIn12 { get; init; }

    /// <summary>Доля траекторий, вышедших на положительный денежный поток.</summary>
    public double ProbabilityBreakEven { get; init; }

    /// <summary>Медианный месяц выхода в плюс; <c>NaN</c>, если большинство не выходит.</summary>
    public double MedianBreakEvenMonth { get; init; }

    /// <summary>Нижняя граница коридора остатка денег по месяцам (10-й процентиль).</summary>
    public Vector CashP10 { get; init; } = new Vector(0);

    /// <summary>Медианная траектория остатка денег по месяцам.</summary>
    public Vector CashP50 { get; init; } = new Vector(0);

    /// <summary>Верхняя граница коридора остатка денег по месяцам (90-й процентиль).</summary>
    public Vector CashP90 { get; init; } = new Vector(0);

    /// <summary>Доля траекторий, ещё живых к каждому месяцу.</summary>
    public Vector SurvivalCurve { get; init; } = new Vector(0);
}

/// <summary>
/// Стохастическая оценка запаса прочности: сколько компания живёт на своих
/// деньгах, если выручка и затраты — случайные величины.
/// </summary>
/// <remarks>
/// <para>
/// Обычный расчёт «касса делить на средний burn» отвечает на вопрос, который
/// никто не задавал: что будет, если всё пойдёт ровно по плану. Он ничего не
/// говорит о риске. Между тем именно риск определяет решение: выходить ли на
/// раунд сейчас или через квартал.
/// </para>
/// <para>
/// Здесь выручка и затраты моделируются геометрическим случайным блужданием
/// с логарифмически нормальными приращениями — так они остаются
/// положительными, а медианный темп роста совпадает с заданным. Результат —
/// не одно число, а распределение месяца исчерпания денег и вероятность
/// дожить до конца горизонта.
/// </para>
/// </remarks>
public static class RunwaySimulator
{
    /// <summary>Запускает симуляцию.</summary>
    /// <param name="input">Параметры модели.</param>
    /// <returns>Распределение срока жизни и коридор остатка денег.</returns>
    /// <exception cref="ArgumentNullException">Параметры не заданы.</exception>
    /// <exception cref="ArgumentException">Некорректный горизонт или число траекторий.</exception>
    public static RunwayResult Simulate(RunwayInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Horizon < 1) throw new ArgumentException("Горизонт должен быть положительным.", nameof(input));
        if (input.Simulations < 1) throw new ArgumentException("Нужна хотя бы одна траектория.", nameof(input));

        int horizon = input.Horizon;
        int paths = input.Simulations;
        Random rng = RandomEngine.Create(input.Seed);

        // Параметры логарифмически нормальных приращений: медианный рост
        // совпадает с заданным средним, положительность гарантирована
        double revMu = Math.Log(1.0 + input.RevenueGrowthMean);
        double costMu = Math.Log(1.0 + input.CostGrowthMean);

        var cashOut = new double[paths];
        var breakEven = new List<double>(paths);
        var cashByMonth = new double[horizon + 1][];
        for (int m = 0; m <= horizon; m++) cashByMonth[m] = new double[paths];

        var alive = new int[horizon + 1];
        int survived = 0, brokeEven = 0;

        for (int p = 0; p < paths; p++)
        {
            double cash = input.Cash;
            double revenue = input.MonthlyRevenue;
            double costs = input.MonthlyCosts;
            bool dead = false;
            double breakEvenMonth = double.NaN;

            cashByMonth[0][p] = cash;
            alive[0]++;

            for (int m = 1; m <= horizon; m++)
            {
                revenue *= Math.Exp(RandomEngine.NextGaussian(rng, revMu, input.RevenueGrowthVolatility));
                costs *= Math.Exp(RandomEngine.NextGaussian(rng, costMu, input.CostGrowthVolatility));

                double net = (revenue * input.GrossMarginRate) - costs;
                if (net > 0 && double.IsNaN(breakEvenMonth)) breakEvenMonth = m;

                cash += net;
                cash += Funding(input, m, rng);

                cashByMonth[m][p] = cash;

                if (!dead && cash < 0)
                {
                    cashOut[p] = m;
                    dead = true;
                    break;
                }

                alive[m]++;
            }

            if (!dead)
            {
                cashOut[p] = double.PositiveInfinity;
                survived++;
            }
            else
            {
                // Оборванную траекторию продлеваем последним значением: иначе
                // квантили коридора считались бы по разному числу путей
                for (int m = (int)cashOut[p] + 1; m <= horizon; m++)
                    cashByMonth[m][p] = cashByMonth[m - 1][p];
            }

            if (!double.IsNaN(breakEvenMonth)) { brokeEven++; breakEven.Add(breakEvenMonth); }
        }

        Array.Sort(cashOut);

        var p10 = new double[horizon + 1];
        var p50 = new double[horizon + 1];
        var p90 = new double[horizon + 1];
        var survival = new double[horizon + 1];

        for (int m = 0; m <= horizon; m++)
        {
            Array.Sort(cashByMonth[m]);
            p10[m] = EconMath.Quantile(cashByMonth[m], 0.10);
            p50[m] = EconMath.Quantile(cashByMonth[m], 0.50);
            p90[m] = EconMath.Quantile(cashByMonth[m], 0.90);
            survival[m] = (double)alive[m] / paths;
        }

        double startBurn = input.MonthlyCosts - (input.MonthlyRevenue * input.GrossMarginRate);
        breakEven.Sort();

        return new RunwayResult
        {
            DeterministicRunwayMonths = startBurn > 0 ? input.Cash / startBurn : double.PositiveInfinity,
            CashOutP10 = EconMath.Quantile(cashOut, 0.10),
            CashOutP50 = EconMath.Quantile(cashOut, 0.50),
            CashOutP90 = EconMath.Quantile(cashOut, 0.90),
            SurvivalProbability = (double)survived / paths,
            ProbabilityCashOutIn6 = Fraction(cashOut, 6),
            ProbabilityCashOutIn12 = Fraction(cashOut, 12),
            ProbabilityBreakEven = (double)brokeEven / paths,
            MedianBreakEvenMonth = breakEven.Count > paths / 2
                ? EconMath.Quantile([.. breakEven], 0.5)
                : double.NaN,
            CashP10 = new Vector(p10),
            CashP50 = new Vector(p50),
            CashP90 = new Vector(p90),
            SurvivalCurve = new Vector(survival),
        };
    }

    /// <summary>Поступление денег в месяце с учётом вероятности его наступления.</summary>
    private static double Funding(RunwayInput input, int month, Random rng)
    {
        if (input.Funding is null) return 0;

        double sum = 0;
        foreach (FundingEvent f in input.Funding)
            if (f.Month == month && rng.NextDouble() < f.Probability) sum += f.Amount;

        return sum;
    }

    /// <summary>Доля траекторий, в которых деньги закончились не позже указанного месяца.</summary>
    private static double Fraction(double[] sortedCashOut, double month)
    {
        int count = 0;
        for (int i = 0; i < sortedCashOut.Length && sortedCashOut[i] <= month; i++) count++;
        return (double)count / sortedCashOut.Length;
    }
}
