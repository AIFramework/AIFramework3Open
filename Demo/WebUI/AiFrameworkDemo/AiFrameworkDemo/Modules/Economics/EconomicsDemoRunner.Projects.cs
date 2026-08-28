using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Projects;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категории «Проектный анализ и кредит».</summary>
public static partial class EconomicsDemoRunner
{
    #region Критерии оценки проекта

    private static string DoInvestmentCriteria(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double investment = p.GetValueOrDefault("investment", 100_000_000);
        int years = (int)p.GetValueOrDefault("years", 6);
        double inflow = p.GetValueOrDefault("inflow", 28_000_000);
        double growth = p.GetValueOrDefault("growth", 0.05);
        double rate = p.GetValueOrDefault("rate", 0.14);
        double reinvest = p.GetValueOrDefault("reinvest", 0.08);
        double overhaul = p.GetValueOrDefault("overhaul", 0);

        var flows = new Vector(years + 1);
        flows[0] = -investment;

        double current = inflow;
        for (int t = 1; t <= years; t++)
        {
            flows[t] = current;
            current *= 1 + growth;

            // Капремонт в середине срока даёт вторую смену знака потока
            if (overhaul > 0 && t == (years / 2) + 1) flows[t] -= overhaul;
        }

        InvestmentAppraisal result = InvestmentCriteria.Appraise(flows, rate, reinvest, "проект");

        cv.AddPlot(Vec(result.NpvProfile.Select(x => x.Rate)), Vec(result.NpvProfile.Select(x => x.Npv)),
            "Приведённая стоимость по ставке", C(0), 3);
        Segment(cv, 0, 0, result.NpvProfile.Max(x => x.Rate), 0, C(5), "Ноль", 1);
        Segment(cv, rate, result.NpvProfile.Min(x => x.Npv), rate, result.NpvProfile.Max(x => x.Npv),
            C(3), $"Ставка: {Pct(rate, 0)}", 2);
        cv.ChartName = $"NPV {Money(result.NetPresentValue)}, IRR {Pct(result.InternalRateOfReturn, 1)}";
        cv.LabelX = "Ставка дисконтирования";
        cv.LabelY = "Приведённая стоимость, руб.";

        var table = rep.Table("Денежный поток",
            ["Период", "Поток", "Дисконтированный", "Накопленный"], [true, true, true, true]);

        for (int t = 0; t < flows.Count; t++)
        {
            table.Row($"{t}", Money(flows[t]),
                Money(flows[t] / Math.Pow(1 + rate, t)), Money(result.CumulativeDiscounted[t]));
        }

        var criteria = rep.Table("Критерии", ["Критерий", "Значение"], [false, true]);
        criteria.Row("Чистая приведённая стоимость", Money(result.NetPresentValue));
        criteria.Row("Внутренняя норма доходности", Pct(result.InternalRateOfReturn, 2));
        criteria.Row("Модифицированная норма", Pct(result.ModifiedIrr, 2));
        criteria.Row("Индекс прибыльности", Num(result.ProfitabilityIndex, 3));
        criteria.Row("Срок окупаемости", $"{Num(result.PaybackPeriod, 1)} периодов");
        criteria.Row("Дисконтированный срок", $"{Num(result.DiscountedPayback, 1)} периодов");

        string log =
            $"Проект {(result.IsAccepted ? "принимается" : "отклоняется")} при ставке {Pct(rate, 2)}.\n" +
            $"NPV {Money(result.NetPresentValue)}, IRR {Pct(result.InternalRateOfReturn, 2)}, " +
            $"MIRR {Pct(result.ModifiedIrr, 2)}.\n" +
            $"Смен знака потока: {result.SignChanges}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Амортизация

    private static string DoDepreciation(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var method = (DepreciationMethod)(int)p.GetValueOrDefault("method", 2);
        double cost = p.GetValueOrDefault("cost", 100_000_000);
        int life = (int)p.GetValueOrDefault("life", 5);
        double salvageShare = p.GetValueOrDefault("salvage", 0);
        double tax = p.GetValueOrDefault("tax", 0.2);
        double rate = p.GetValueOrDefault("rate", 0.14);
        double factor = p.GetValueOrDefault("factor", 2);

        DepreciationSchedule result = Depreciation.Build(
            cost, life, method, cost * salvageShare, tax, rate, factor);

        IReadOnlyList<DepreciationSchedule> all =
            Depreciation.CompareMethods(cost, life, cost * salvageShare, tax, rate);

        var years = Axis(life, 1);
        cv.AddPlot(years, Depreciation.Charges(result), "Выбранный метод", C(0), 3);

        for (int i = 0; i < all.Count && i < 3; i++)
        {
            if (all[i].Method == method) continue;
            cv.AddPlot(years, Depreciation.Charges(all[i]), MethodLabel(all[i].Method), C(i + 2), 1);
        }

        cv.ChartName = $"Приведённый налоговый щит {Money(result.PresentValueOfShield)}";
        cv.LabelX = "Год";
        cv.LabelY = "Начисленная амортизация, руб.";

        var schedule = rep.Table("График начислений",
            ["Год", "Остаток на начало", "Амортизация", "Остаток на конец", "Щит", "Приведённый щит"],
            [true, true, true, true, true, true]);

        foreach (DepreciationPeriod period in result.Periods)
        {
            schedule.Row($"{period.Period}", Money(period.OpeningValue), Money(period.Charge),
                Money(period.ClosingValue), Money(period.TaxShield), Money(period.DiscountedShield));
        }

        var comparison = rep.Table("Сравнение методов",
            ["Метод", "Приведённый щит", "Списано в первой трети"], [false, true, true]);

        foreach (DepreciationSchedule item in all)
            comparison.Row(MethodLabel(item.Method), Money(item.PresentValueOfShield), Pct(item.FrontLoading, 0));

        string log =
            $"Метод: {MethodLabel(method)}. Стоимость {Money(cost)} за {life} лет.\n" +
            $"Приведённая стоимость налогового щита {Money(result.PresentValueOfShield)}.\n" +
            $"В первой трети срока списано {Pct(result.FrontLoading, 0)} стоимости.\n" +
            $"Лучший по щиту метод: {MethodLabel(all[0].Method)} ({Money(all[0].PresentValueOfShield)}).";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название метода амортизации.</summary>
    private static string MethodLabel(DepreciationMethod method) => method switch
    {
        DepreciationMethod.StraightLine => "линейный",
        DepreciationMethod.DecliningBalance => "уменьшаемого остатка",
        DepreciationMethod.SumOfYearsDigits => "сумма чисел лет",
        _ => "нелинейный налоговый",
    };

    #endregion

    #region График кредита

    private static string DoLoanSchedule(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var type = (RepaymentType)(int)p.GetValueOrDefault("type", 0);
        double principal = p.GetValueOrDefault("principal", 5_000_000);
        double rate = p.GetValueOrDefault("rate", 0.18);
        int months = (int)p.GetValueOrDefault("months", 60);
        double fee = p.GetValueOrDefault("fee", 0.01);
        int prepayMonth = (int)p.GetValueOrDefault("prepay_month", 12);
        double prepayAmount = p.GetValueOrDefault("prepay_amount", 500_000);
        bool reducePayment = p.GetValueOrDefault("prepay_mode", 0) > 0.5;

        IReadOnlyList<Prepayment> prepayments = prepayMonth > 0 && prepayAmount > 0
            ? [new Prepayment(prepayMonth, prepayAmount, reducePayment)]
            : [];

        LoanScheduleResult result = LoanSchedule.Build(
            principal, rate, months, type, 12, fee, prepayments);

        LoanScheduleResult plain = LoanSchedule.Build(principal, rate, months, type, 12, fee);

        var periods = Axis(result.Payments.Count, 1);
        cv.AddPlot(periods, Vec(result.Payments.Select(x => x.Interest)), "Проценты", C(3), 3);
        cv.AddPlot(periods, Vec(result.Payments.Select(x => x.Principal)), "Погашение тела", C(0), 3);
        cv.ChartName = $"Переплата {Money(result.TotalInterest)}, полная стоимость " +
                       $"{Pct(result.AnnualPercentageRate, 2)}";
        cv.LabelX = "Месяц";
        cv.LabelY = "Составляющие платежа, руб.";

        var schedule = rep.Table("График платежей",
            ["Месяц", "Долг на начало", "Платёж", "Проценты", "Тело", "Досрочно", "Остаток"],
            [true, true, true, true, true, true, true]);

        foreach (LoanPayment payment in result.Payments.Take(24))
        {
            schedule.Row($"{payment.Period}", Money(payment.OpeningBalance), Money(payment.Payment),
                Money(payment.Interest), Money(payment.Principal),
                payment.Prepayment > 0 ? Money(payment.Prepayment) : "—", Money(payment.ClosingBalance));
        }

        var summary = rep.Table("Сравнение с графиком без досрочных погашений",
            ["Показатель", "С досрочным", "Без досрочного"], [false, true, true]);
        summary.Row("Срок, мес.", $"{result.ActualTerm}", $"{plain.ActualTerm}");
        summary.Row("Переплата", Money(result.TotalInterest), Money(plain.TotalInterest));
        summary.Row("Всего выплачено", Money(result.TotalPaid), Money(plain.TotalPaid));

        string log =
            $"Кредит {Money(principal)} под {Pct(rate, 2)} на {months} мес.\n" +
            $"Эффективная ставка {Pct(result.EffectiveAnnualRate, 2)}, полная стоимость " +
            $"{Pct(result.AnnualPercentageRate, 2)}.\n" +
            $"Переплата {Money(result.TotalInterest)} — {Pct(result.Overpayment, 0)} от тела.\n" +
            (prepayments.Count > 0
                ? $"Досрочное погашение сэкономило {Money(result.PrepaymentSaving)} и сократило срок " +
                  $"на {plain.ActualTerm - result.ActualTerm} мес."
                : "Досрочные погашения не заданы.");

        return Explain(rep, result, log);
    }

    #endregion

    #region Лизинг против кредита

    private static string DoLeaseVsBuy(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var input = new LeaseVsBuyInput
        {
            Asset = "Оборудование",
            AssetCost = p.GetValueOrDefault("cost", 10_000_000),
            Years = (int)p.GetValueOrDefault("years", 5),
            TaxRate = p.GetValueOrDefault("tax", 0.2),
            DiscountRate = p.GetValueOrDefault("rate", 0.12),
            DepreciationLife = (int)p.GetValueOrDefault("years", 5),
            CreditRate = p.GetValueOrDefault("credit_rate", 0.16),
            CreditDownPayment = p.GetValueOrDefault("down", 0.2),
            LeaseMarkup = p.GetValueOrDefault("markup", 0.09),
            LeaseAdvance = p.GetValueOrDefault("advance", 0.2),
            ResidualValue = p.GetValueOrDefault("cost", 10_000_000) * p.GetValueOrDefault("residual", 0.15),
        };

        LeaseVsBuyResult result = LeaseVsBuy.Compare(input);

        var years = Axis(input.Years + 1, 0);
        foreach (AcquisitionOption option in result.Options)
            cv.AddPlot(years, option.CashFlows, OptionLabel(option.Mode), C(result.Options.ToList().IndexOf(option)), 2);

        cv.ChartName = $"Дешевле всего {OptionLabel(result.Best)}: {Money(result.Options[0].PresentValueOfCost)}";
        cv.LabelX = "Год";
        cv.LabelY = "Денежный поток после налога, руб.";

        var table = rep.Table("Сравнение вариантов",
            ["Вариант", "Приведённые затраты", "Номинальные выплаты", "Налоговый щит"],
            [false, true, true, true]);

        foreach (AcquisitionOption option in result.Options)
        {
            table.Row(OptionLabel(option.Mode), Money(option.PresentValueOfCost),
                Money(option.TotalOutflow), Money(option.TaxShield));
        }

        var sensitivity = rep.Table("Затраты по лизингу при разном удорожании",
            ["Удорожание", "Приведённые затраты"], [true, true]);

        for (double markup = 0.03; markup <= 0.24; markup += 0.03)
        {
            LeaseVsBuyResult variant = LeaseVsBuy.Compare(input with { LeaseMarkup = markup });
            AcquisitionOption lease = variant.Options.First(o => o.Mode == AcquisitionMode.Lease);

            sensitivity.Row(Pct(markup, 0), Money(lease.PresentValueOfCost));
        }

        string log =
            $"Актив {Money(input.AssetCost)} на {input.Years} лет при ставке {Pct(input.DiscountRate, 1)}.\n" +
            $"Лучший вариант: {OptionLabel(result.Best)}, выигрыш {Money(result.Advantage)}.\n" +
            $"Порог безразличия по удорожанию лизинга: {Pct(result.BreakEvenLeaseMarkup, 2)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название способа приобретения.</summary>
    private static string OptionLabel(AcquisitionMode mode) => mode switch
    {
        AcquisitionMode.Purchase => "покупка",
        AcquisitionMode.Credit => "кредит",
        _ => "лизинг",
    };

    #endregion

    #region Безубыточность

    private static string DoBreakEven(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double price = p.GetValueOrDefault("price", 1000);
        double variable = Math.Min(p.GetValueOrDefault("variable", 600), price - 1);
        double fixedCosts = p.GetValueOrDefault("fixed", 2_000_000);
        double volume = p.GetValueOrDefault("volume", 8000);
        double interest = p.GetValueOrDefault("interest", 300_000);
        double target = p.GetValueOrDefault("target", 1_000_000);
        double tax = p.GetValueOrDefault("tax", 0.2);

        BreakEvenResult result = BreakEven.Analyze(
            price, variable, fixedCosts, volume, interest, target, tax, "продукт");

        int points = 41;
        var volumes = new Vector(points);
        var revenue = new Vector(points);
        var costs = new Vector(points);

        double maximum = Math.Max(volume, result.BreakEvenUnits) * 1.6;

        for (int i = 0; i < points; i++)
        {
            double q = maximum * i / (points - 1);
            volumes[i] = q;
            revenue[i] = price * q;
            costs[i] = fixedCosts + (variable * q);
        }

        cv.AddPlot(volumes, revenue, "Выручка", C(0), 3);
        cv.AddPlot(volumes, costs, "Полные затраты", C(3), 3);
        Segment(cv, result.BreakEvenUnits, 0, result.BreakEvenUnits, revenue.Max(), C(5),
            $"Безубыточность: {Int(result.BreakEvenUnits)} ед.", 2);
        cv.ChartName = $"Безубыточность {Int(result.BreakEvenUnits)} ед., запас прочности " +
                       $"{Pct(result.MarginOfSafety, 0)}";
        cv.LabelX = "Объём продаж, ед.";
        cv.LabelY = "Рубли";

        var table = rep.Table("Показатели", ["Показатель", "Значение"], [false, true]);
        table.Row("Маржинальная прибыль на единицу", Money(result.ContributionPerUnit));
        table.Row("Норма маржинальной прибыли", Pct(result.ContributionMargin, 1));
        table.Row("Точка безубыточности", $"{Int(result.BreakEvenUnits)} ед.");
        table.Row("В деньгах", Money(result.BreakEvenRevenue));
        table.Row("Объём для целевой прибыли", $"{Int(result.TargetUnits)} ед.");
        table.Row("Операционная прибыль", Money(result.OperatingProfit));
        table.Row("Чистая прибыль", Money(result.NetProfit));

        var leverage = rep.Table("Чувствительность к падению выручки",
            ["Падение выручки", "Изменение чистой прибыли"], [true, true]);

        foreach (double drop in new[] { 0.05, 0.1, 0.2, 0.3 })
            leverage.Row(Pct(drop, 0), Pct(-drop * result.CombinedLeverage, 0));

        string log =
            $"Точка безубыточности {Int(result.BreakEvenUnits)} ед. ({Money(result.BreakEvenRevenue)}).\n" +
            $"Запас прочности {Pct(result.MarginOfSafety, 1)} при объёме {Int(volume)} ед.\n" +
            $"Операционный рычаг {Num(result.OperatingLeverage, 2)}, финансовый " +
            $"{Num(result.FinancialLeverage, 2)}, совокупный {Num(result.CombinedLeverage, 2)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Структура капитала

    private static string DoCapitalStructure(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        CapitalStructureResult result = BreakEven.OptimalStructure(
            "Компания",
            p.GetValueOrDefault("ke", 0.18),
            p.GetValueOrDefault("kd", 0.12),
            p.GetValueOrDefault("tax", 0.2),
            p.GetValueOrDefault("ebit", 500_000_000),
            p.GetValueOrDefault("current", 0.25),
            p.GetValueOrDefault("distress", 0.35));

        var shares = Vec(result.Curve.Select(c => c.DebtShare));

        cv.AddPlot(shares, Vec(result.Curve.Select(c => c.Wacc)), "Стоимость капитала", C(0), 3);
        cv.AddPlot(shares, Vec(result.Curve.Select(c => c.CostOfEquity)), "Стоимость собственного", C(1), 2);
        cv.AddPlot(shares, Vec(result.Curve.Select(c => c.CostOfDebt)), "Стоимость долга", C(3), 2);
        Segment(cv, result.OptimalDebtShare, result.Curve.Min(c => c.Wacc),
            result.OptimalDebtShare, result.Curve.Max(c => c.CostOfEquity), C(5),
            $"Оптимум: {Pct(result.OptimalDebtShare, 0)}", 2);
        cv.ChartName = $"Минимум {Pct(result.MinimumWacc, 2)} при доле долга " +
                       $"{Pct(result.OptimalDebtShare, 0)}";
        cv.LabelX = "Доля долга";
        cv.LabelY = "Ставка";

        var table = rep.Table("Кривая структуры капитала",
            ["Доля долга", "Стоимость капитала", "Стоимость долга", "WACC", "Стоимость компании"],
            [true, true, true, true, true]);

        foreach (CapitalStructurePoint point in result.Curve)
        {
            table.Row(Pct(point.DebtShare, 0), Pct(point.CostOfEquity, 2), Pct(point.CostOfDebt, 2),
                Pct(point.Wacc, 3), Money(point.FirmValue));
        }

        string log =
            $"Оптимальная доля долга {Pct(result.OptimalDebtShare, 0)} при ставке " +
            $"{Pct(result.MinimumWacc, 3)}.\n" +
            $"Текущая структура {Pct(result.CurrentDebtShare, 0)} при ставке {Pct(result.CurrentWacc, 3)}.\n" +
            $"Переход к оптимуму добавляет {Money(result.ValueGain)} стоимости.";

        return Explain(rep, result, log);
    }

    #endregion
}
