using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Projects;

/// <summary>Тип графика погашения кредита.</summary>
public enum RepaymentType
{
    /// <summary>Аннуитетный: равный суммарный платёж каждый период.</summary>
    Annuity,

    /// <summary>Дифференцированный: равное погашение тела, убывающий платёж.</summary>
    Differentiated,

    /// <summary>Только проценты, тело в конце срока.</summary>
    InterestOnly,
}

/// <summary>Досрочное погашение.</summary>
/// <param name="Period">Номер периода.</param>
/// <param name="Amount">Сумма досрочного погашения.</param>
/// <param name="ReducePayment">Уменьшать ли платёж вместо срока.</param>
public sealed record Prepayment(int Period, double Amount, bool ReducePayment = false);

/// <summary>Платёж по графику.</summary>
/// <param name="Period">Номер периода.</param>
/// <param name="OpeningBalance">Долг на начало периода.</param>
/// <param name="Payment">Платёж по графику.</param>
/// <param name="Interest">Процентная часть.</param>
/// <param name="Principal">Погашение тела.</param>
/// <param name="Prepayment">Досрочное погашение.</param>
/// <param name="ClosingBalance">Долг на конец периода.</param>
public sealed record LoanPayment(
    int Period, double OpeningBalance, double Payment, double Interest,
    double Principal, double Prepayment, double ClosingBalance);

/// <summary>График погашения кредита.</summary>
public sealed record LoanScheduleResult : IInterpretable
{
    /// <summary>Тип графика.</summary>
    public RepaymentType Type { get; init; }

    /// <summary>Платежи по периодам.</summary>
    public IReadOnlyList<LoanPayment> Payments { get; init; } = [];

    /// <summary>Сумма кредита.</summary>
    public double Principal { get; init; }

    /// <summary>Номинальная годовая ставка.</summary>
    public double NominalRate { get; init; }

    /// <summary>Эффективная годовая ставка с учётом капитализации.</summary>
    public double EffectiveAnnualRate { get; init; }

    /// <summary>Полная стоимость кредита с учётом комиссий.</summary>
    public double AnnualPercentageRate { get; init; }

    /// <summary>Суммарные проценты за срок.</summary>
    public double TotalInterest { get; init; }

    /// <summary>Суммарные выплаты.</summary>
    public double TotalPaid { get; init; }

    /// <summary>Фактический срок в периодах с учётом досрочных погашений.</summary>
    public int ActualTerm => Payments.Count;

    /// <summary>Исходный срок в периодах.</summary>
    public int OriginalTerm { get; init; }

    /// <summary>Переплата процентов относительно тела.</summary>
    public double Overpayment => Principal > 0 ? TotalInterest / Principal : 0;

    /// <summary>Экономия на процентах от досрочных погашений.</summary>
    public double PrepaymentSaving { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double firstInterestShare = Payments.Count > 0 && Payments[0].Payment > 0
            ? Payments[0].Interest / Payments[0].Payment
            : 0;

        bool hasPrepayments = Payments.Any(p => p.Prepayment > 0);
        int savedPeriods = OriginalTerm - ActualTerm;

        var builder = new InterpretationBuilder($"График погашения: {TypeName()}")
            .Summary($"Кредит {Fmt.Money(Principal)} на {OriginalTerm} периодов под " +
                     $"{Fmt.Pct(NominalRate, 2)} годовых. Переплата {Fmt.Money(TotalInterest)} " +
                     $"({Fmt.Pct(Overpayment, 0)} от тела), всего выплачено {Fmt.Money(TotalPaid)}. " +
                     $"Эффективная ставка {Fmt.Pct(EffectiveAnnualRate, 2)}, полная стоимость " +
                     $"{Fmt.Pct(AnnualPercentageRate, 2)}." +
                     (hasPrepayments
                         ? $" Досрочные погашения сократили срок на {savedPeriods} периодов " +
                           $"и сэкономили {Fmt.Money(PrepaymentSaving)}."
                         : ""))
            .Metric("Переплата", Fmt.Money(TotalInterest), null,
                $"{Fmt.Pct(Overpayment, 0)} от суммы кредита",
                Overpayment > 0.5 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Эффективная ставка", EffectiveAnnualRate, null,
                "с учётом периодичности начисления процентов", MetricQuality.Neutral, 4)
            .Metric("Полная стоимость кредита", AnnualPercentageRate, null,
                "с учётом комиссий: величина, сопоставимая между предложениями",
                AnnualPercentageRate > NominalRate * 1.2 ? MetricQuality.Warning : MetricQuality.Neutral, 4)
            .Metric("Доля процентов в первом платеже", firstInterestShare, null,
                Type == RepaymentType.Annuity
                    ? "в аннуитете начало платежей почти целиком проценты"
                    : "процентная часть убывает с самого начала",
                MetricQuality.Neutral, 3)
            .Metric("Фактический срок", ActualTerm, "периодов",
                hasPrepayments ? $"вместо {OriginalTerm} по договору" : "по договору",
                MetricQuality.Neutral, 0);

        if (hasPrepayments)
        {
            builder.Metric("Экономия от досрочных погашений", Fmt.Money(PrepaymentSaving), null,
                $"сокращение срока на {savedPeriods} периодов", MetricQuality.Good);
        }

        foreach (LoanPayment payment in Payments.Take(12))
        {
            builder.Metric($"Период {payment.Period}", payment.Payment, null,
                $"проценты {Fmt.Money(payment.Interest)}, тело {Fmt.Money(payment.Principal)}, " +
                $"остаток {Fmt.Money(payment.ClosingBalance)}",
                MetricQuality.Unknown, 0);
        }

        return builder
            .Finding("Сравнивать кредиты нужно по полной стоимости, а не по номинальной " +
                     "ставке: комиссии и периодичность начисления меняют реальную цену " +
                     "денег заметно сильнее, чем разница в объявленной ставке.")
            .FindingIf(Type == RepaymentType.Annuity,
                $"В первом платеже {Fmt.Pct(firstInterestShare, 0)} приходится на проценты. " +
                "Поэтому досрочное погашение в начале срока экономит несопоставимо больше, " +
                "чем в конце: тело в этот момент почти не убывает.")
            .FindingIf(Type == RepaymentType.Differentiated,
                "Дифференцированный график дороже на старте, но суммарная переплата " +
                "по нему меньше аннуитетной при том же сроке и ставке.")
            .FindingIf(hasPrepayments,
                $"Досрочные погашения сэкономили {Fmt.Money(PrepaymentSaving)} процентов. " +
                "Сокращение срока выгоднее уменьшения платежа: экономия там больше " +
                "при той же сумме досрочного взноса.")
            .WarningIf(AnnualPercentageRate > NominalRate * 1.3,
                $"Полная стоимость {Fmt.Pct(AnnualPercentageRate, 2)} заметно выше " +
                $"номинальной ставки {Fmt.Pct(NominalRate, 2)}: значительную часть " +
                "цены составляют комиссии, а не проценты.")
            .WarningIf(Overpayment > 1,
                $"Переплата превышает сумму кредита ({Fmt.Pct(Overpayment, 0)}). " +
                "Это следствие длинного срока: каждый дополнительный год увеличивает " +
                "переплату сильнее, чем снижает платёж.")
            .Warning("График строится по фиксированной ставке. При плавающей ставке " +
                     "фактическая переплата отличается от расчётной, и сравнение " +
                     "предложений по полной стоимости теряет смысл.")
            .Recommendation("Направляйте досрочные погашения на сокращение срока, если цель — " +
                            "минимизировать переплату, и на уменьшение платежа, если важна " +
                            "текущая нагрузка на бюджет.")
            .Recommendation("Считайте полную стоимость самостоятельно: она включает все " +
                            "обязательные платежи и является единственной сопоставимой " +
                            "величиной между кредиторами.")
            .Build();
    }

    /// <summary>Читаемое название типа графика.</summary>
    private string TypeName() => Type switch
    {
        RepaymentType.Annuity => "аннуитетный",
        RepaymentType.Differentiated => "дифференцированный",
        _ => "только проценты",
    };
}

/// <summary>
/// Графики погашения кредитов, эффективная ставка и досрочные погашения.
/// </summary>
/// <remarks>
/// <para>
/// Аннуитетный платёж находится из условия равенства приведённой стоимости
/// платежей телу кредита:
/// </para>
/// <code>
/// A = P * i / (1 - (1 + i)^-n),  i — ставка за период
/// </code>
/// <para>
/// Дифференцированный график гасит тело равными долями, поэтому платёж убывает,
/// а суммарная переплата оказывается меньше при том же сроке.
/// </para>
/// <para>
/// Эффективная годовая ставка учитывает капитализацию внутри года, полная
/// стоимость кредита — ещё и комиссии. Она находится как внутренняя норма
/// доходности денежного потока заёмщика и является единственной величиной,
/// по которой предложения разных кредиторов сопоставимы.
/// </para>
/// <code>
/// EAR = (1 + i)^m - 1
/// APR: сумма (выдано - комиссии) = сумма платежей, дисконтированных по APR
/// </code>
/// </remarks>
public static class LoanSchedule
{
    /// <summary>Строит график погашения кредита.</summary>
    /// <param name="principal">Сумма кредита.</param>
    /// <param name="annualRate">Годовая номинальная ставка.</param>
    /// <param name="periods">Срок в периодах.</param>
    /// <param name="type">Тип графика.</param>
    /// <param name="periodsPerYear">Число периодов в году.</param>
    /// <param name="upfrontFee">Единовременная комиссия в долях от суммы кредита.</param>
    /// <param name="prepayments">Досрочные погашения.</param>
    /// <returns>График платежей, эффективная ставка и полная стоимость кредита.</returns>
    /// <exception cref="ArgumentException">Сумма, срок или ставка вне допустимых значений.</exception>
    public static LoanScheduleResult Build(
        double principal, double annualRate, int periods,
        RepaymentType type = RepaymentType.Annuity, int periodsPerYear = 12,
        double upfrontFee = 0, IReadOnlyList<Prepayment>? prepayments = null)
    {
        if (principal <= 0) throw new ArgumentException("Сумма кредита должна быть положительной.", nameof(principal));
        if (periods < 1) throw new ArgumentException("Срок должен быть не меньше периода.", nameof(periods));
        if (annualRate < 0) throw new ArgumentException("Ставка не может быть отрицательной.", nameof(annualRate));

        double rate = annualRate / periodsPerYear;
        double balance = principal;

        double annuity = rate > 0
            ? principal * rate / (1 - Math.Pow(1 + rate, -periods))
            : principal / periods;

        var schedule = new List<LoanPayment>(periods);
        var flows = new List<double> { principal * (1 - upfrontFee) };
        double totalInterest = 0;
        double payment = annuity;

        for (int t = 1; t <= periods && balance > 1e-9; t++)
        {
            double interest = balance * rate;

            double scheduled = type switch
            {
                RepaymentType.Annuity => Math.Min(payment, balance + interest),
                RepaymentType.Differentiated => (principal / periods) + interest,
                _ => t == periods ? balance + interest : interest,
            };

            double principalPart = Math.Min(scheduled - interest, balance);
            double opening = balance;
            balance -= principalPart;

            double prepaid = 0;
            Prepayment? prepayment = prepayments?.FirstOrDefault(p => p.Period == t);

            if (prepayment is not null && balance > 0)
            {
                prepaid = Math.Min(prepayment.Amount, balance);
                balance -= prepaid;

                // Уменьшение платежа пересчитывает аннуитет на оставшийся срок
                if (prepayment.ReducePayment && type == RepaymentType.Annuity && balance > 0)
                {
                    int remaining = periods - t;
                    payment = rate > 0 && remaining > 0
                        ? balance * rate / (1 - Math.Pow(1 + rate, -remaining))
                        : balance / Math.Max(1, remaining);
                }
            }

            totalInterest += interest;
            flows.Add(-(scheduled + prepaid));

            schedule.Add(new LoanPayment(
                t, opening, scheduled, interest, principalPart, prepaid, balance));
        }

        var flowVector = new Vector(flows.Count);
        for (int i = 0; i < flows.Count; i++) flowVector[i] = flows[i];

        double periodApr = InvestmentCriteria.InternalRateOfReturn(flowVector);
        double apr = double.IsFinite(periodApr) ? Math.Pow(1 + periodApr, periodsPerYear) - 1 : annualRate;

        double baselineInterest = BaselineInterest(principal, rate, periods, type);

        return new LoanScheduleResult
        {
            Type = type,
            Payments = schedule,
            Principal = principal,
            NominalRate = annualRate,
            EffectiveAnnualRate = Math.Pow(1 + rate, periodsPerYear) - 1,
            AnnualPercentageRate = apr,
            TotalInterest = totalInterest,
            TotalPaid = schedule.Sum(p => p.Payment + p.Prepayment),
            OriginalTerm = periods,
            PrepaymentSaving = Math.Max(baselineInterest - totalInterest, 0),
        };
    }

    /// <summary>Аннуитетный платёж по сумме, ставке и сроку.</summary>
    /// <param name="principal">Сумма кредита.</param>
    /// <param name="ratePerPeriod">Ставка за период.</param>
    /// <param name="periods">Число периодов.</param>
    /// <returns>Размер равного платежа.</returns>
    public static double AnnuityPayment(double principal, double ratePerPeriod, int periods) =>
        ratePerPeriod > 0
            ? principal * ratePerPeriod / (1 - Math.Pow(1 + ratePerPeriod, -periods))
            : principal / Math.Max(1, periods);

    /// <summary>Эффективная годовая ставка по номинальной.</summary>
    /// <param name="nominalRate">Номинальная годовая ставка.</param>
    /// <param name="compoundingPerYear">Число начислений в году.</param>
    /// <returns>Эффективная годовая ставка.</returns>
    public static double EffectiveRate(double nominalRate, int compoundingPerYear) =>
        Math.Pow(1 + (nominalRate / Math.Max(1, compoundingPerYear)), Math.Max(1, compoundingPerYear)) - 1;

    /// <summary>Суммарные проценты по графику без досрочных погашений.</summary>
    private static double BaselineInterest(double principal, double rate, int periods, RepaymentType type)
    {
        double balance = principal;
        double annuity = AnnuityPayment(principal, rate, periods);
        double total = 0;

        for (int t = 1; t <= periods && balance > 1e-9; t++)
        {
            double interest = balance * rate;

            double scheduled = type switch
            {
                RepaymentType.Annuity => Math.Min(annuity, balance + interest),
                RepaymentType.Differentiated => (principal / periods) + interest,
                _ => t == periods ? balance + interest : interest,
            };

            balance -= Math.Min(scheduled - interest, balance);
            total += interest;
        }

        return total;
    }
}
