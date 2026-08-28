using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Projects;

/// <summary>Платёж проекта с точной датой.</summary>
/// <param name="Date">Дата платежа.</param>
/// <param name="Amount">Сумма: отрицательная для оттока, положительная для притока.</param>
public sealed record DatedCashFlow(DateOnly Date, double Amount);

/// <summary>Результат оценки инвестиционного проекта.</summary>
public sealed record InvestmentAppraisal : IInterpretable
{
    /// <summary>Название проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Чистая приведённая стоимость.</summary>
    public double NetPresentValue { get; init; }

    /// <summary>Внутренняя норма доходности.</summary>
    public double InternalRateOfReturn { get; init; }

    /// <summary>Модифицированная внутренняя норма доходности.</summary>
    public double ModifiedIrr { get; init; }

    /// <summary>Индекс прибыльности.</summary>
    public double ProfitabilityIndex { get; init; }

    /// <summary>Простой срок окупаемости в периодах.</summary>
    public double PaybackPeriod { get; init; }

    /// <summary>Дисконтированный срок окупаемости в периодах.</summary>
    public double DiscountedPayback { get; init; }

    /// <summary>Ставка дисконтирования.</summary>
    public double DiscountRate { get; init; }

    /// <summary>Накопленный дисконтированный поток по периодам.</summary>
    public Vector CumulativeDiscounted { get; init; } = new(0);

    /// <summary>Сумма первоначальных вложений.</summary>
    public double InitialInvestment { get; init; }

    /// <summary>Число смен знака в потоке: показатель множественности внутренней нормы.</summary>
    public int SignChanges { get; init; }

    /// <summary>Профиль чистой приведённой стоимости по ставкам.</summary>
    public IReadOnlyList<(double Rate, double Npv)> NpvProfile { get; init; } = [];

    /// <summary>Принимается ли проект по правилу приведённой стоимости.</summary>
    public bool IsAccepted => NetPresentValue > 0;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool multipleRoots = SignChanges > 1;
        bool irrValid = double.IsFinite(InternalRateOfReturn);
        double margin = DiscountRate > 0 && irrValid ? InternalRateOfReturn - DiscountRate : 0;

        return new InterpretationBuilder($"Оценка проекта: {Name}")
            .Summary($"Чистая приведённая стоимость {Fmt.Money(NetPresentValue)} при ставке " +
                     $"{Fmt.Pct(DiscountRate, 2)} — проект {(IsAccepted ? "принимается" : "отклоняется")}. " +
                     $"Внутренняя норма доходности " +
                     $"{(irrValid ? Fmt.Pct(InternalRateOfReturn, 2) : "не определена")}, " +
                     $"модифицированная {Fmt.Pct(ModifiedIrr, 2)}. Индекс прибыльности " +
                     $"{Fmt.Num(ProfitabilityIndex, 2)}, окупаемость " +
                     $"{Fmt.Num(PaybackPeriod, 1)} периодов (дисконтированная " +
                     $"{Fmt.Num(DiscountedPayback, 1)}).")
            .Metric("Приведённая стоимость", Fmt.Money(NetPresentValue), null,
                "прирост стоимости компании от проекта",
                IsAccepted ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Внутренняя норма", irrValid ? InternalRateOfReturn : 0, null,
                irrValid ? $"запас над ставкой {Fmt.Pct(margin, 2)}" : "поток не допускает единственного решения",
                irrValid && margin > 0.05 ? MetricQuality.Good
                    : irrValid && margin > 0 ? MetricQuality.Neutral : MetricQuality.Warning, 4)
            .Metric("Модифицированная норма", ModifiedIrr, null,
                "с реалистичной ставкой реинвестирования промежуточных поступлений",
                MetricQuality.Neutral, 4)
            .Metric("Индекс прибыльности", ProfitabilityIndex, null,
                "приведённые притоки на рубль вложений",
                ProfitabilityIndex > 1.2 ? MetricQuality.Good
                    : ProfitabilityIndex > 1 ? MetricQuality.Neutral : MetricQuality.Critical, 3)
            .Metric("Срок окупаемости", PaybackPeriod, "периодов",
                $"дисконтированный {Fmt.Num(DiscountedPayback, 1)}",
                double.IsFinite(DiscountedPayback) ? MetricQuality.Neutral : MetricQuality.Warning, 1)
            .Metric("Первоначальные вложения", Fmt.Money(InitialInvestment), null,
                "суммарный отток до первого притока")
            .Metric("Смен знака потока", SignChanges, null,
                multipleRoots ? "внутренняя норма может быть неединственной" : "единственный корень",
                multipleRoots ? MetricQuality.Warning : MetricQuality.Good, 0)
            .Finding("Правило приведённой стоимости — единственное, которое всегда даёт " +
                     "верный ответ при выборе между проектами. Внутренняя норма удобна " +
                     "для разговора, но при сравнении проектов разного масштаба " +
                     "и длительности она вводит в заблуждение.")
            .FindingIf(irrValid && Math.Abs(InternalRateOfReturn - ModifiedIrr) > 0.03,
                $"Внутренняя норма {Fmt.Pct(InternalRateOfReturn, 2)} и модифицированная " +
                $"{Fmt.Pct(ModifiedIrr, 2)} заметно расходятся. Разница — целиком следствие " +
                "предпосылки о реинвестировании: обычная норма неявно предполагает, " +
                "что промежуточные поступления вкладываются под неё же.")
            .FindingIf(!double.IsFinite(DiscountedPayback),
                "Дисконтированный срок окупаемости не достигается на горизонте проекта: " +
                "приведённые притоки не покрывают вложения полностью.")
            .WarningIf(multipleRoots,
                $"Поток меняет знак {SignChanges} раза. У такого проекта внутренняя норма " +
                "может иметь несколько корней или не иметь ни одного, и опираться " +
                "на неё нельзя — только на приведённую стоимость.")
            .WarningIf(!IsAccepted && irrValid && InternalRateOfReturn > 0,
                $"Проект окупается ({Fmt.Pct(InternalRateOfReturn, 2)} годовых), но не " +
                "покрывает стоимость капитала. Положительная доходность и создание " +
                "стоимости — разные вещи.")
            .Warning("Срок окупаемости игнорирует всё, что происходит после его достижения. " +
                     "Как самостоятельный критерий он систематически отвергает длинные " +
                     "проекты с большим итоговым эффектом.")
            .Recommendation("Сравнивайте взаимоисключающие проекты по приведённой стоимости, " +
                            "а не по внутренней норме: при разном масштабе они дают " +
                            "противоположные ответы.")
            .Recommendation("Стройте профиль приведённой стоимости по ставке: точка " +
                            "пересечения с нулём и есть внутренняя норма, а наклон " +
                            "показывает чувствительность проекта к стоимости капитала.")
            .Build();
    }
}

/// <summary>
/// Критерии оценки инвестиционных проектов.
/// </summary>
/// <remarks>
/// <para>
/// Приведённая стоимость дисконтирует поток по стоимости капитала:
/// </para>
/// <code>
/// NPV = sum_t CF_t / (1 + r)^t
/// PI  = PV(притоки) / PV(оттоки)
/// </code>
/// <para>
/// Внутренняя норма — ставка, обнуляющая приведённую стоимость. Её удобно
/// обсуждать, но у неё два известных дефекта: при нескольких сменах знака
/// потока корень не единственный, и она неявно предполагает реинвестирование
/// промежуточных поступлений под саму себя. Модифицированная норма устраняет
/// второй дефект, задавая ставку реинвестирования явно:
/// </para>
/// <code>
/// MIRR = (FV(притоки по ставке реинвестирования) / PV(оттоки по ставке финансирования))^(1/n) - 1
/// </code>
/// <para>
/// Для потоков с произвольными датами используются функции с точным
/// дисконтированием по числу дней, деленному на 365.
/// </para>
/// </remarks>
public static class InvestmentCriteria
{
    /// <summary>Оценивает проект по регулярному потоку платежей.</summary>
    /// <param name="cashFlows">Поток по периодам; нулевой элемент — момент вложения.</param>
    /// <param name="discountRate">Ставка дисконтирования за период.</param>
    /// <param name="reinvestmentRate">Ставка реинвестирования притоков; при отрицательной берётся ставка дисконтирования.</param>
    /// <param name="name">Название проекта.</param>
    /// <returns>Полный набор критериев с интерпретацией.</returns>
    /// <exception cref="ArgumentNullException">Поток не задан.</exception>
    /// <exception cref="ArgumentException">Поток пуст или не содержит оттока.</exception>
    public static InvestmentAppraisal Appraise(
        Vector cashFlows, double discountRate, double reinvestmentRate = -1, string name = "проект")
    {
        ArgumentNullException.ThrowIfNull(cashFlows);
        if (cashFlows.Count < 2) throw new ArgumentException("Нужно минимум два платежа.", nameof(cashFlows));

        double reinvest = reinvestmentRate >= 0 ? reinvestmentRate : discountRate;

        var cumulative = new Vector(cashFlows.Count);
        double running = 0, positive = 0, negative = 0;
        int signChanges = 0;

        for (int t = 0; t < cashFlows.Count; t++)
        {
            double discounted = cashFlows[t] / Math.Pow(1 + discountRate, t);
            running += discounted;
            cumulative[t] = running;

            if (cashFlows[t] > 0) positive += discounted;
            else negative -= discounted;

            if (t > 0 && Math.Sign(cashFlows[t]) != 0 && Math.Sign(cashFlows[t - 1]) != 0
                && Math.Sign(cashFlows[t]) != Math.Sign(cashFlows[t - 1]))
                signChanges++;
        }

        if (negative <= 0) throw new ArgumentException("В потоке нет вложений.", nameof(cashFlows));

        var profile = new List<(double, double)>();
        for (int i = 0; i <= 20; i++)
        {
            double rate = i * 0.05;
            profile.Add((rate, NetPresentValue(cashFlows, rate)));
        }

        return new InvestmentAppraisal
        {
            Name = name,
            NetPresentValue = running,
            InternalRateOfReturn = InternalRateOfReturn(cashFlows),
            ModifiedIrr = ModifiedInternalRateOfReturn(cashFlows, discountRate, reinvest),
            ProfitabilityIndex = positive / negative,
            PaybackPeriod = Payback(cashFlows, 0),
            DiscountedPayback = Payback(cashFlows, discountRate),
            DiscountRate = discountRate,
            CumulativeDiscounted = cumulative,
            InitialInvestment = cashFlows.TakeWhile(v => v <= 0).Sum(v => -v),
            SignChanges = signChanges,
            NpvProfile = profile,
        };
    }

    /// <summary>Чистая приведённая стоимость регулярного потока.</summary>
    /// <param name="cashFlows">Поток по периодам.</param>
    /// <param name="rate">Ставка за период.</param>
    /// <returns>Приведённая стоимость.</returns>
    /// <exception cref="ArgumentNullException">Поток не задан.</exception>
    public static double NetPresentValue(Vector cashFlows, double rate)
    {
        ArgumentNullException.ThrowIfNull(cashFlows);

        double total = 0;
        for (int t = 0; t < cashFlows.Count; t++) total += cashFlows[t] / Math.Pow(1 + rate, t);

        return total;
    }

    /// <summary>Внутренняя норма доходности регулярного потока.</summary>
    /// <param name="cashFlows">Поток по периодам.</param>
    /// <returns>Ставка, обнуляющая приведённую стоимость, или <see cref="double.NaN"/>, если её нет.</returns>
    /// <exception cref="ArgumentNullException">Поток не задан.</exception>
    public static double InternalRateOfReturn(Vector cashFlows)
    {
        ArgumentNullException.ThrowIfNull(cashFlows);

        double low = -0.9, high = 10.0;
        double valueLow = NetPresentValue(cashFlows, low);
        double valueHigh = NetPresentValue(cashFlows, high);

        if (valueLow * valueHigh > 0) return double.NaN;

        for (int i = 0; i < 200; i++)
        {
            double mid = (low + high) / 2;
            double value = NetPresentValue(cashFlows, mid);

            if (value * valueLow <= 0) { high = mid; valueHigh = value; }
            else { low = mid; valueLow = value; }
        }

        return (low + high) / 2;
    }

    /// <summary>Модифицированная внутренняя норма доходности.</summary>
    /// <param name="cashFlows">Поток по периодам.</param>
    /// <param name="financeRate">Ставка финансирования оттоков.</param>
    /// <param name="reinvestmentRate">Ставка реинвестирования притоков.</param>
    /// <returns>Модифицированная норма доходности.</returns>
    /// <exception cref="ArgumentNullException">Поток не задан.</exception>
    public static double ModifiedInternalRateOfReturn(
        Vector cashFlows, double financeRate, double reinvestmentRate)
    {
        ArgumentNullException.ThrowIfNull(cashFlows);

        int n = cashFlows.Count - 1;
        if (n <= 0) return double.NaN;

        double futureInflows = 0, presentOutflows = 0;

        for (int t = 0; t <= n; t++)
        {
            if (cashFlows[t] > 0) futureInflows += cashFlows[t] * Math.Pow(1 + reinvestmentRate, n - t);
            else presentOutflows -= cashFlows[t] / Math.Pow(1 + financeRate, t);
        }

        return presentOutflows > 0 && futureInflows > 0
            ? Math.Pow(futureInflows / presentOutflows, 1.0 / n) - 1
            : double.NaN;
    }

    /// <summary>Приведённая стоимость потока с произвольными датами.</summary>
    /// <param name="flows">Датированные платежи.</param>
    /// <param name="annualRate">Годовая ставка.</param>
    /// <returns>Приведённая к дате первого платежа стоимость.</returns>
    /// <exception cref="ArgumentNullException">Поток не задан.</exception>
    /// <exception cref="ArgumentException">Поток пуст.</exception>
    public static double ExtendedNetPresentValue(IReadOnlyList<DatedCashFlow> flows, double annualRate)
    {
        ArgumentNullException.ThrowIfNull(flows);
        if (flows.Count == 0) throw new ArgumentException("Поток пуст.", nameof(flows));

        DateOnly start = flows.Min(f => f.Date);
        double total = 0;

        foreach (DatedCashFlow flow in flows)
        {
            double years = (flow.Date.DayNumber - start.DayNumber) / 365.0;
            total += flow.Amount / Math.Pow(1 + annualRate, years);
        }

        return total;
    }

    /// <summary>Внутренняя норма доходности потока с произвольными датами.</summary>
    /// <param name="flows">Датированные платежи.</param>
    /// <returns>Годовая ставка, обнуляющая приведённую стоимость.</returns>
    /// <exception cref="ArgumentNullException">Поток не задан.</exception>
    public static double ExtendedInternalRateOfReturn(IReadOnlyList<DatedCashFlow> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);

        double low = -0.9, high = 10.0;
        double valueLow = ExtendedNetPresentValue(flows, low);

        if (valueLow * ExtendedNetPresentValue(flows, high) > 0) return double.NaN;

        for (int i = 0; i < 200; i++)
        {
            double mid = (low + high) / 2;
            double value = ExtendedNetPresentValue(flows, mid);

            if (value * valueLow <= 0) high = mid;
            else { low = mid; valueLow = value; }
        }

        return (low + high) / 2;
    }

    /// <summary>Срок окупаемости с линейной интерполяцией внутри периода.</summary>
    private static double Payback(Vector cashFlows, double rate)
    {
        double running = 0;

        for (int t = 0; t < cashFlows.Count; t++)
        {
            double discounted = cashFlows[t] / Math.Pow(1 + rate, t);
            double previous = running;
            running += discounted;

            if (previous < 0 && running >= 0 && Math.Abs(discounted) > 1e-12)
                return t - 1 + (-previous / discounted);
        }

        return double.PositiveInfinity;
    }
}
