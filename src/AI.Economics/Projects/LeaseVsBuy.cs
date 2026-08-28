using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Projects;

/// <summary>Способ приобретения актива.</summary>
public enum AcquisitionMode
{
    /// <summary>Покупка за собственные средства.</summary>
    Purchase,

    /// <summary>Покупка в кредит.</summary>
    Credit,

    /// <summary>Лизинг.</summary>
    Lease,
}

/// <summary>Итог по одному способу приобретения.</summary>
/// <param name="Mode">Способ приобретения.</param>
/// <param name="PresentValueOfCost">Приведённая стоимость затрат.</param>
/// <param name="TotalOutflow">Суммарные номинальные выплаты.</param>
/// <param name="TaxShield">Приведённая стоимость налогового щита.</param>
/// <param name="ResidualValue">Приведённая остаточная стоимость актива у владельца.</param>
/// <param name="CashFlows">Денежный поток по периодам.</param>
public sealed record AcquisitionOption(
    AcquisitionMode Mode, double PresentValueOfCost, double TotalOutflow,
    double TaxShield, double ResidualValue, Vector CashFlows);

/// <summary>Входные данные сравнения способов приобретения.</summary>
public sealed record LeaseVsBuyInput
{
    /// <summary>Название актива.</summary>
    public string Asset { get; init; } = string.Empty;

    /// <summary>Стоимость актива.</summary>
    public double AssetCost { get; init; } = 10_000_000;

    /// <summary>Срок использования в годах.</summary>
    public int Years { get; init; } = 5;

    /// <summary>Ставка налога на прибыль.</summary>
    public double TaxRate { get; init; } = 0.2;

    /// <summary>Ставка дисконтирования после налога.</summary>
    public double DiscountRate { get; init; } = 0.12;

    /// <summary>Срок полезного использования для амортизации.</summary>
    public int DepreciationLife { get; init; } = 5;

    /// <summary>Коэффициент ускоренной амортизации предмета лизинга.</summary>
    public double LeaseDepreciationFactor { get; init; } = 3;

    /// <summary>Остаточная стоимость актива в конце срока.</summary>
    public double ResidualValue { get; init; }

    /// <summary>Ставка по кредиту.</summary>
    public double CreditRate { get; init; } = 0.16;

    /// <summary>Первоначальный взнос по кредиту в долях от стоимости.</summary>
    public double CreditDownPayment { get; init; } = 0.2;

    /// <summary>Годовое удорожание в лизинге в долях от стоимости.</summary>
    public double LeaseMarkup { get; init; } = 0.09;

    /// <summary>Аванс по лизингу в долях от стоимости.</summary>
    public double LeaseAdvance { get; init; } = 0.2;

    /// <summary>Выкупная стоимость в конце лизинга в долях от стоимости.</summary>
    public double LeaseBuyout { get; init; } = 0.01;
}

/// <summary>Результат сравнения способов приобретения актива.</summary>
public sealed record LeaseVsBuyResult : IInterpretable
{
    /// <summary>Название актива.</summary>
    public string Asset { get; init; } = string.Empty;

    /// <summary>Варианты приобретения по возрастанию приведённой стоимости затрат.</summary>
    public IReadOnlyList<AcquisitionOption> Options { get; init; } = [];

    /// <summary>Лучший вариант.</summary>
    public AcquisitionMode Best => Options.Count > 0 ? Options[0].Mode : AcquisitionMode.Purchase;

    /// <summary>Выигрыш лучшего варианта над следующим.</summary>
    public double Advantage =>
        Options.Count > 1 ? Options[1].PresentValueOfCost - Options[0].PresentValueOfCost : 0;

    /// <summary>Ставка дисконтирования.</summary>
    public double DiscountRate { get; init; }

    /// <summary>Ставка удорожания в лизинге, при которой он сравнивается с кредитом.</summary>
    public double BreakEvenLeaseMarkup { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        AcquisitionOption? best = Options.FirstOrDefault();
        AcquisitionOption? worst = Options.LastOrDefault();

        double relative = best is not null && best.PresentValueOfCost > 0
            ? Advantage / best.PresentValueOfCost
            : 0;

        var builder = new InterpretationBuilder($"Лизинг, кредит или покупка: {Asset}")
            .Summary($"Дешевле всего {ModeName(Best)}: приведённая стоимость затрат " +
                     $"{Fmt.Money(best?.PresentValueOfCost ?? 0)}. Выигрыш над следующим " +
                     $"вариантом {Fmt.Money(Advantage)} ({Fmt.Pct(relative, 1)}). " +
                     $"Лизинг сравнивается с кредитом при удорожании " +
                     $"{Fmt.Pct(BreakEvenLeaseMarkup, 2)} годовых.")
            .Metric("Лучший вариант", ModeName(Best), null,
                $"приведённые затраты {Fmt.Money(best?.PresentValueOfCost ?? 0)}", MetricQuality.Good)
            .Metric("Выигрыш", Fmt.Money(Advantage), null,
                $"{Fmt.Pct(relative, 1)} приведённых затрат",
                relative > 0.05 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Порог удорожания лизинга", BreakEvenLeaseMarkup, null,
                "выше этой ставки лизинг проигрывает кредиту", MetricQuality.Neutral, 4);

        foreach (AcquisitionOption option in Options)
        {
            builder.Metric(ModeName(option.Mode), Fmt.Money(option.PresentValueOfCost), null,
                $"номинальные выплаты {Fmt.Money(option.TotalOutflow)}, налоговый щит " +
                $"{Fmt.Money(option.TaxShield)}, остаточная стоимость {Fmt.Money(option.ResidualValue)}",
                option.Mode == Best ? MetricQuality.Good : MetricQuality.Neutral);
        }

        return builder
            .Finding("Сравнивать способы приобретения нужно по приведённой стоимости затрат " +
                     "после налога, а не по сумме платежей. Номинально лизинг почти всегда " +
                     "дороже, а после налогового щита и ускоренной амортизации — часто дешевле.")
            .FindingIf(best is not null && worst is not null,
                $"Разрыв между лучшим и худшим вариантом {Fmt.Money(worst!.PresentValueOfCost - best!.PresentValueOfCost)}. " +
                "Основную часть разницы обычно создаёт не ставка, а различие в моменте " +
                "признания расходов для налога.")
            .FindingIf(Best == AcquisitionMode.Lease,
                $"Лизинг выигрывает при текущем удорожании {Fmt.Pct(BreakEvenLeaseMarkup, 2)} " +
                "как пороге безразличия. Преимущество даёт ускоренная амортизация " +
                "предмета лизинга и отнесение платежа на расходы целиком.")
            .FindingIf(Best == AcquisitionMode.Purchase,
                "Покупка за собственные средства дешевле заёмных вариантов. Это верно " +
                "по приведённым затратам, но не учитывает альтернативную доходность " +
                "отвлечённых денег — её нужно закладывать в ставку дисконтирования.")
            .WarningIf(Options.Count < 3,
                "Сравнение проведено не по всем вариантам. Полноценный выбор требует " +
                "всех трёх способов на одинаковых предпосылках.")
            .Warning("Расчёт предполагает, что компания прибыльна и полностью использует " +
                     "налоговый щит. У убыточной компании преимущество лизинга и кредита " +
                     "исчезает: вычитать расходы не из чего.")
            .Recommendation("Проверьте чувствительность к ставке дисконтирования: при " +
                            "высокой ставке выигрывают варианты с поздними платежами, " +
                            "при низкой — с ранним получением налогового щита.")
            .Recommendation("Учитывайте нефинансовые различия: в лизинге актив на балансе " +
                            "лизингодателя, а в кредите — залог. Это влияет на ковенанты " +
                            "и на доступ к следующему финансированию.")
            .Build();
    }

    /// <summary>Читаемое название способа приобретения.</summary>
    private static string ModeName(AcquisitionMode mode) => mode switch
    {
        AcquisitionMode.Purchase => "покупка",
        AcquisitionMode.Credit => "кредит",
        _ => "лизинг",
    };
}

/// <summary>
/// Сравнение лизинга, кредита и покупки за собственные средства.
/// </summary>
/// <remarks>
/// <para>
/// Сравнение ведётся по приведённой стоимости затрат после налога. Номинальная
/// сумма платежей вводит в заблуждение: она не учитывает ни момент выплаты,
/// ни налоговый щит.
/// </para>
/// <para>
/// Три варианта различаются тем, что именно уменьшает налог:
/// </para>
/// <code>
/// покупка: амортизация * tax
/// кредит:  (амортизация + проценты) * tax
/// лизинг:  весь лизинговый платёж * tax, плюс ускоренная амортизация у лизингодателя
/// </code>
/// <para>
/// Ускоренная амортизация предмета лизинга с коэффициентом до трёх — главный
/// источник его преимущества: она переносит налоговый щит на начало срока,
/// где он стоит дороже.
/// </para>
/// <para>
/// Порог безразличия по удорожанию лизинга переводит сравнение в понятную
/// переговорную величину: до какой ставки лизинговой компании есть смысл
/// торговаться, чтобы вариант остался выгоднее кредита.
/// </para>
/// </remarks>
public static class LeaseVsBuy
{
    /// <summary>Сравнивает способы приобретения актива.</summary>
    /// <param name="input">Параметры актива, кредита и лизинга.</param>
    /// <returns>Варианты по возрастанию приведённых затрат и порог безразличия.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    /// <exception cref="ArgumentException">Стоимость актива или срок неположительны.</exception>
    public static LeaseVsBuyResult Compare(LeaseVsBuyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.AssetCost <= 0)
            throw new ArgumentException("Стоимость актива должна быть положительной.", nameof(input));
        if (input.Years < 1)
            throw new ArgumentException("Срок должен быть не меньше года.", nameof(input));

        var options = new List<AcquisitionOption>
        {
            Purchase(input),
            Credit(input),
            Lease(input, input.LeaseMarkup),
        };

        return new LeaseVsBuyResult
        {
            Asset = input.Asset,
            Options = [.. options.OrderBy(o => o.PresentValueOfCost)],
            DiscountRate = input.DiscountRate,
            BreakEvenLeaseMarkup = BreakEvenMarkup(input),
        };
    }

    /// <summary>Покупка за собственные средства.</summary>
    private static AcquisitionOption Purchase(LeaseVsBuyInput input)
    {
        var flows = new Vector(input.Years + 1);
        flows[0] = -input.AssetCost;

        DepreciationSchedule schedule = Depreciation.Build(
            input.AssetCost, input.DepreciationLife, DepreciationMethod.StraightLine,
            0, input.TaxRate, input.DiscountRate);

        double shield = 0;
        for (int t = 1; t <= input.Years; t++)
        {
            double charge = t <= schedule.Periods.Count ? schedule.Periods[t - 1].Charge : 0;
            double benefit = charge * input.TaxRate;

            flows[t] += benefit;
            shield += benefit / Math.Pow(1 + input.DiscountRate, t);
        }

        double residual = input.ResidualValue / Math.Pow(1 + input.DiscountRate, input.Years);
        flows[input.Years] += input.ResidualValue;

        return new AcquisitionOption(
            AcquisitionMode.Purchase,
            -InvestmentCriteria.NetPresentValue(flows, input.DiscountRate),
            input.AssetCost, shield, residual, flows);
    }

    /// <summary>Покупка в кредит.</summary>
    private static AcquisitionOption Credit(LeaseVsBuyInput input)
    {
        double down = input.AssetCost * input.CreditDownPayment;
        double borrowed = input.AssetCost - down;

        LoanScheduleResult loan = LoanSchedule.Build(
            borrowed, input.CreditRate, input.Years, RepaymentType.Annuity, periodsPerYear: 1);

        DepreciationSchedule schedule = Depreciation.Build(
            input.AssetCost, input.DepreciationLife, DepreciationMethod.StraightLine,
            0, input.TaxRate, input.DiscountRate);

        var flows = new Vector(input.Years + 1);
        flows[0] = -down;

        double shield = 0, outflow = down;

        for (int t = 1; t <= input.Years; t++)
        {
            LoanPayment? payment = loan.Payments.FirstOrDefault(p => p.Period == t);
            double charge = t <= schedule.Periods.Count ? schedule.Periods[t - 1].Charge : 0;

            double interest = payment?.Interest ?? 0;
            double total = payment?.Payment ?? 0;
            double benefit = (charge + interest) * input.TaxRate;

            flows[t] = -total + benefit;
            outflow += total;
            shield += benefit / Math.Pow(1 + input.DiscountRate, t);
        }

        double residual = input.ResidualValue / Math.Pow(1 + input.DiscountRate, input.Years);
        flows[input.Years] += input.ResidualValue;

        return new AcquisitionOption(
            AcquisitionMode.Credit,
            -InvestmentCriteria.NetPresentValue(flows, input.DiscountRate),
            outflow, shield, residual, flows);
    }

    /// <summary>Лизинг с заданным удорожанием.</summary>
    private static AcquisitionOption Lease(LeaseVsBuyInput input, double markup)
    {
        double advance = input.AssetCost * input.LeaseAdvance;
        double buyout = input.AssetCost * input.LeaseBuyout;

        // Удорожание задаётся в процентах от стоимости в год — так его и называют в договорах
        double totalPayments = (input.AssetCost * (1 + (markup * input.Years))) - advance - buyout;
        double annual = totalPayments / input.Years;

        var flows = new Vector(input.Years + 1);
        flows[0] = -advance + (advance * input.TaxRate);

        double shield = advance * input.TaxRate;
        double outflow = advance;

        for (int t = 1; t <= input.Years; t++)
        {
            double payment = annual + (t == input.Years ? buyout : 0);
            double benefit = payment * input.TaxRate;

            flows[t] = -payment + benefit;
            outflow += payment;
            shield += benefit / Math.Pow(1 + input.DiscountRate, t);
        }

        double residual = input.ResidualValue / Math.Pow(1 + input.DiscountRate, input.Years);
        flows[input.Years] += input.ResidualValue;

        return new AcquisitionOption(
            AcquisitionMode.Lease,
            -InvestmentCriteria.NetPresentValue(flows, input.DiscountRate),
            outflow, shield, residual, flows);
    }

    /// <summary>Удорожание лизинга, при котором он сравнивается с кредитом.</summary>
    private static double BreakEvenMarkup(LeaseVsBuyInput input)
    {
        double target = Credit(input).PresentValueOfCost;
        double low = 0, high = 0.6;

        // Затраты по лизингу монотонно растут с удорожанием
        for (int i = 0; i < 60; i++)
        {
            double mid = (low + high) / 2;
            if (Lease(input, mid).PresentValueOfCost < target) low = mid;
            else high = mid;
        }

        return (low + high) / 2;
    }
}
