using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;
using AI.Statistics;

namespace AI.Economics.Risk;

/// <summary>Кассовый разрыв в конкретном периоде.</summary>
/// <param name="Period">Номер периода.</param>
/// <param name="Opening">Остаток на начало.</param>
/// <param name="Inflow">Поступления.</param>
/// <param name="Outflow">Выплаты.</param>
/// <param name="Closing">Остаток на конец.</param>
/// <param name="Shortfall">Величина нехватки денег.</param>
public sealed record CashPosition(
    int Period, double Opening, double Inflow, double Outflow, double Closing, double Shortfall);

/// <summary>Результат анализа риска ликвидности.</summary>
public sealed record LiquidityResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Позиция по денежным средствам по периодам.</summary>
    public IReadOnlyList<CashPosition> Positions { get; init; } = [];

    /// <summary>Минимальный остаток на горизонте.</summary>
    public double MinimumBalance { get; init; }

    /// <summary>Период, в котором достигается минимум.</summary>
    public int MinimumPeriod { get; init; }

    /// <summary>Число периодов с кассовым разрывом.</summary>
    public int ShortfallPeriods { get; init; }

    /// <summary>Максимальная нехватка денег.</summary>
    public double MaximumShortfall { get; init; }

    /// <summary>Необходимый размер кредитной линии.</summary>
    public double RequiredCreditLine { get; init; }

    /// <summary>Вероятность кассового разрыва по стохастическому расчёту.</summary>
    public double ShortfallProbability { get; init; }

    /// <summary>Оптимальный запас денежных средств по модели Баумоля.</summary>
    public double BaumolCash { get; init; }

    /// <summary>Нижняя граница остатка по модели Миллера — Орра.</summary>
    public double MillerOrrLower { get; init; }

    /// <summary>Точка возврата по модели Миллера — Орра.</summary>
    public double MillerOrrReturn { get; init; }

    /// <summary>Верхняя граница остатка по модели Миллера — Орра.</summary>
    public double MillerOrrUpper { get; init; }

    /// <summary>Средний остаток по модели Миллера — Орра.</summary>
    public double MillerOrrAverage => (4 * MillerOrrReturn - MillerOrrLower) / 3;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool hasGap = ShortfallPeriods > 0;
        bool risky = ShortfallProbability > 0.05;

        var builder = new InterpretationBuilder($"Риск ликвидности: {Company}")
            .Summary($"Минимальный остаток {Fmt.Money(MinimumBalance)} достигается в периоде " +
                     $"{MinimumPeriod}. " +
                     (hasGap
                         ? $"Кассовый разрыв возникает в {ShortfallPeriods} периодах, " +
                           $"максимальная нехватка {Fmt.Money(MaximumShortfall)}. "
                         : "Кассовых разрывов на горизонте нет. ") +
                     $"Вероятность разрыва с учётом неопределённости потоков " +
                     $"{Fmt.Pct(ShortfallProbability, 1)}. Рекомендуемая кредитная линия " +
                     $"{Fmt.Money(RequiredCreditLine)}.")
            .Metric("Минимальный остаток", Fmt.Money(MinimumBalance), null,
                $"период {MinimumPeriod}",
                MinimumBalance > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Вероятность разрыва", ShortfallProbability, null,
                "по стохастическому расчёту потоков",
                risky ? MetricQuality.Critical : MetricQuality.Good, 3)
            .Metric("Кредитная линия", Fmt.Money(RequiredCreditLine), null,
                "покрывает максимальную нехватку с запасом",
                RequiredCreditLine > 0 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Оптимальный запас по Баумолю", Fmt.Money(BaumolCash), null,
                "при известном равномерном расходовании денег")
            .Metric("Точка возврата по Миллеру — Орру", Fmt.Money(MillerOrrReturn), null,
                $"границы {Fmt.Money(MillerOrrLower)} и {Fmt.Money(MillerOrrUpper)}")
            .Metric("Средний остаток по Миллеру — Орру", Fmt.Money(MillerOrrAverage), null,
                "ожидаемый остаток при следовании правилу");

        foreach (CashPosition position in Positions)
        {
            builder.Metric($"Период {position.Period}", position.Closing, null,
                $"поступления {Fmt.Money(position.Inflow)}, выплаты {Fmt.Money(position.Outflow)}" +
                (position.Shortfall > 0 ? $", нехватка {Fmt.Money(position.Shortfall)}" : ""),
                position.Shortfall > 0 ? MetricQuality.Critical : MetricQuality.Neutral, 0);
        }

        return builder
            .Finding("Прибыльная компания разоряется от нехватки денег, а не от убытков. " +
                     "Платёжный календарь и прибыль — разные вещи: разрыв возникает " +
                     "из-за несовпадения сроков, а не из-за отрицательной маржи.")
            .FindingIf(hasGap,
                $"Максимальная нехватка {Fmt.Money(MaximumShortfall)} требует либо кредитной " +
                "линии, либо сдвига платежей. Обычно второе дешевле: переговоры об отсрочке " +
                "стоят меньше процентов по овердрафту.")
            .FindingIf(!hasGap && risky,
                "По базовому прогнозу разрывов нет, но с учётом разброса потоков " +
                $"вероятность разрыва {Fmt.Pct(ShortfallProbability, 1)}. Планировать " +
                "нужно по этой величине, а не по точечному прогнозу.")
            .Finding("Модель Баумоля применима при равномерном известном расходовании денег, " +
                     "Миллера — Орра — при случайных колебаниях остатка. Вторая ближе " +
                     "к реальности и даёт более широкий, но и более честный коридор.")
            .WarningIf(MinimumBalance < 0,
                $"Минимальный остаток отрицателен ({Fmt.Money(MinimumBalance)}): без внешнего " +
                "финансирования компания не проходит горизонт планирования.")
            .WarningIf(risky,
                $"Вероятность кассового разрыва {Fmt.Pct(ShortfallProbability, 1)} превышает " +
                "приемлемый уровень. Резерв ликвидности нужно увеличить или сократить " +
                "разброс поступлений.")
            .Warning("Расчёт опирается на прогноз поступлений. Именно он и подводит " +
                     "в кризис: платежи задерживаются одновременно у всех покупателей, " +
                     "а поставщики одновременно требуют предоплату.")
            .Recommendation("Держите неиспользуемую кредитную линию размером с максимальную " +
                            "расчётную нехватку: её стоимость несопоставима с ценой " +
                            "срочного привлечения денег.")
            .Recommendation("Проверяйте платёжный календарь еженедельно, а не помесячно: " +
                            "разрывы внутри месяца в помесячной модели не видны.")
            .Build();
    }
}

/// <summary>
/// Риск ликвидности: кассовые разрывы и оптимальный запас денежных средств.
/// </summary>
/// <remarks>
/// <para>
/// Платёжный календарь строится по периодам с учётом начального остатка;
/// кассовый разрыв возникает там, где остаток уходит ниже нуля. Стохастический
/// расчёт добавляет разброс поступлений и даёт вероятность разрыва, а не только
/// его наличие в точечном прогнозе.
/// </para>
/// <para>
/// Оптимальный запас денег определяется компромиссом между издержками
/// конвертации и упущенным процентом. Модель Баумоля предполагает равномерное
/// расходование:
/// </para>
/// <code>
/// C* = sqrt(2 * T * F / r)
/// </code>
/// <para>
/// Модель Миллера — Орра описывает случайные колебания остатка и задаёт коридор:
/// при достижении верхней границы деньги размещаются, при достижении нижней —
/// привлекаются, и в обоих случаях остаток возвращается к точке возврата:
/// </para>
/// <code>
/// Z = L + (3 * F * sigma^2 / (4 * r))^(1/3)
/// H = 3 * Z - 2 * L
/// среднее = (4 * Z - L) / 3
/// </code>
/// <para>
/// Вторая модель ближе к реальности: она не требует знания будущих платежей,
/// а только разброса их сальдо.
/// </para>
/// </remarks>
public static class LiquidityRisk
{
    /// <summary>Строит платёжный календарь и оценивает риск разрыва.</summary>
    /// <param name="openingBalance">Начальный остаток денежных средств.</param>
    /// <param name="inflows">Поступления по периодам.</param>
    /// <param name="outflows">Выплаты по периодам.</param>
    /// <param name="inflowVolatility">Относительный разброс поступлений.</param>
    /// <param name="transactionCost">Издержки одной операции конвертации.</param>
    /// <param name="interestRate">Ставка размещения свободных денег за период.</param>
    /// <param name="minimumBalance">Неснижаемый остаток.</param>
    /// <param name="company">Название компании.</param>
    /// <param name="simulations">Число симуляций для оценки вероятности разрыва.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Календарь, вероятность разрыва и оптимальные остатки.</returns>
    /// <exception cref="ArgumentNullException">Потоки не заданы.</exception>
    /// <exception cref="ArgumentException">Длины рядов не совпадают.</exception>
    public static LiquidityResult Analyze(
        double openingBalance, Vector inflows, Vector outflows,
        double inflowVolatility = 0.15, double transactionCost = 5000,
        double interestRate = 0.01, double minimumBalance = 0,
        string company = "компания", int simulations = 5000, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(inflows);
        ArgumentNullException.ThrowIfNull(outflows);

        if (inflows.Count != outflows.Count)
            throw new ArgumentException("Ряды поступлений и выплат должны совпадать по длине.", nameof(outflows));
        if (inflows.Count == 0)
            throw new ArgumentException("Горизонт планирования пуст.", nameof(inflows));

        var positions = new List<CashPosition>(inflows.Count);
        double balance = openingBalance;
        double minimum = openingBalance;
        int minimumPeriod = 0;
        double maximumShortfall = 0;
        int shortfallPeriods = 0;

        for (int t = 0; t < inflows.Count; t++)
        {
            double opening = balance;
            balance += inflows[t] - outflows[t];

            double shortfall = Math.Max(minimumBalance - balance, 0);
            if (shortfall > 0)
            {
                shortfallPeriods++;
                maximumShortfall = Math.Max(maximumShortfall, shortfall);
            }

            if (balance < minimum) { minimum = balance; minimumPeriod = t + 1; }

            positions.Add(new CashPosition(t + 1, opening, inflows[t], outflows[t], balance, shortfall));
        }

        double probability = ShortfallProbability(
            openingBalance, inflows, outflows, inflowVolatility, minimumBalance, simulations, seed);

        double totalOutflow = outflows.Sum();
        double baumol = interestRate > 0 && totalOutflow > 0
            ? Math.Sqrt(2 * totalOutflow * transactionCost / interestRate)
            : 0;

        double[] net = [.. Enumerable.Range(0, inflows.Count).Select(t => inflows[t] - outflows[t])];
        double netMean = net.Average();
        double netVariance = net.Length > 1
            ? net.Sum(v => (v - netMean) * (v - netMean)) / (net.Length - 1)
            : 0;

        double spread = interestRate > 0
            ? Math.Pow(3 * transactionCost * netVariance / (4 * interestRate), 1.0 / 3.0)
            : 0;

        double lower = minimumBalance;
        double returnPoint = lower + spread;
        double upper = (3 * returnPoint) - (2 * lower);

        return new LiquidityResult
        {
            Company = company,
            Positions = positions,
            MinimumBalance = minimum,
            MinimumPeriod = minimumPeriod,
            ShortfallPeriods = shortfallPeriods,
            MaximumShortfall = maximumShortfall,
            RequiredCreditLine = maximumShortfall * 1.2,
            ShortfallProbability = probability,
            BaumolCash = baumol,
            MillerOrrLower = lower,
            MillerOrrReturn = returnPoint,
            MillerOrrUpper = upper,
        };
    }

    /// <summary>Оптимальный запас денег по модели Баумоля.</summary>
    /// <param name="periodDemand">Потребность в деньгах за период.</param>
    /// <param name="transactionCost">Издержки одной конвертации.</param>
    /// <param name="interestRate">Ставка размещения за период.</param>
    /// <returns>Размер одной конвертации, минимизирующий суммарные издержки.</returns>
    /// <exception cref="ArgumentException">Параметры неположительны.</exception>
    public static double Baumol(double periodDemand, double transactionCost, double interestRate)
    {
        if (periodDemand <= 0) throw new ArgumentException("Потребность должна быть положительной.", nameof(periodDemand));
        if (interestRate <= 0) throw new ArgumentException("Ставка должна быть положительной.", nameof(interestRate));

        return Math.Sqrt(2 * periodDemand * transactionCost / interestRate);
    }

    /// <summary>Границы остатка по модели Миллера — Орра.</summary>
    /// <param name="minimumBalance">Неснижаемый остаток.</param>
    /// <param name="dailyVariance">Дисперсия дневного сальдо потоков.</param>
    /// <param name="transactionCost">Издержки одной конвертации.</param>
    /// <param name="dailyRate">Дневная ставка размещения.</param>
    /// <returns>Нижняя граница, точка возврата и верхняя граница.</returns>
    /// <exception cref="ArgumentException">Ставка неположительна.</exception>
    public static (double Lower, double Return, double Upper) MillerOrr(
        double minimumBalance, double dailyVariance, double transactionCost, double dailyRate)
    {
        if (dailyRate <= 0) throw new ArgumentException("Ставка должна быть положительной.", nameof(dailyRate));

        double spread = Math.Pow(3 * transactionCost * dailyVariance / (4 * dailyRate), 1.0 / 3.0);
        double returnPoint = minimumBalance + spread;

        return (minimumBalance, returnPoint, (3 * returnPoint) - (2 * minimumBalance));
    }

    /// <summary>Вероятность кассового разрыва при случайных поступлениях.</summary>
    private static double ShortfallProbability(
        double openingBalance, Vector inflows, Vector outflows,
        double volatility, double minimumBalance, int simulations, int seed)
    {
        if (simulations < 100 || volatility <= 0) return 0;

        Random rng = RandomEngine.Create(seed);
        int breaches = 0;

        for (int s = 0; s < simulations; s++)
        {
            double balance = openingBalance;
            bool breached = false;

            for (int t = 0; t < inflows.Count; t++)
            {
                // Поступления случайны, выплаты договорные и потому детерминированы
                double received = inflows[t] * Math.Max(0, 1 + (RandomEngine.NextGaussian(rng) * volatility));
                balance += received - outflows[t];

                if (balance < minimumBalance) { breached = true; break; }
            }

            if (breached) breaches++;
        }

        return (double)breaches / simulations;
    }
}
