using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.UnitEconomics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoUnitEconomics(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double spend = N(p, "spend", 900_000);
        double customers = N(p, "customers", 300);
        double arpu = N(p, "arpu", 6000);
        double margin = N(p, "margin", 0.8);
        double churn = N(p, "churn", 0.045);
        double discount = N(p, "discount", 0.01);
        int horizon = I(p, "horizon", 36);

        var input = new UnitEconomicsInput
        {
            MarketingSpend = spend,
            NewCustomers = customers,
            RevenuePerPeriod = arpu,
            GrossMarginRate = margin,
            ChurnRate = churn,
            DiscountRate = discount,
            Horizon = horizon,
        };

        UnitEconomicsResult result = UnitEconomicsCalculator.Compute(input);
        UnitEconomicsResult naive = UnitEconomicsCalculator.Compute(input with { DiscountRate = 0, GrossMarginRate = 1.0 });

        // ── График: накопленный вклад против CAC ──────────────────────────
        Vector months = Axis(result.CumulativeNet.Count);
        cv.AddPlot(months, result.CumulativeNet, "Накопленная прибыль с клиента", C(0), 3);
        Segment(cv, 0, 0, horizon - 1, 0, C(3), "Точка окупаемости", 2);

        var revenueCurve = new Vector(result.CumulativeNet.Count);
        double acc = -result.Cac;
        for (int t = 0; t < revenueCurve.Count; t++)
        {
            acc += arpu * result.Survival[t];
            revenueCurve[t] = acc;
        }
        cv.AddPlot(months, revenueCurve, "Если считать по выручке (так делать нельзя)", C(1), 2);

        cv.ChartName = $"Окупаемость клиента: CAC {Money(result.Cac)} ₽, LTV {Money(result.Ltv)} ₽";
        cv.LabelX = "Месяц жизни клиента";
        cv.LabelY = "Накопленный результат, ₽";

        // ── Отчёт ────────────────────────────────────────────────────────
        rep.Metric("CAC", Money(result.Cac), "₽", "Затраты на привлечение делить на число клиентов")
           .Metric("LTV", Money(result.Ltv), "₽", "Дисконтированный маржинальный вклад за весь срок жизни")
           .Metric("LTV / CAC", Num(result.LtvToCac), "",
               "Ориентир рынка — не ниже 3",
               result.LtvToCac >= 3 ? MetricTone.Good : result.LtvToCac >= 1 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Окупаемость", double.IsNaN(result.CacPaybackPeriods) ? "не окупается" : Num(result.CacPaybackPeriods, 1),
               "мес.", "Дробный срок возврата затрат на привлечение",
               double.IsNaN(result.CacPaybackPeriods) ? MetricTone.Bad
                   : result.CacPaybackPeriods <= 12 ? MetricTone.Good
                   : result.CacPaybackPeriods <= 18 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Срок жизни", Num(result.ExpectedLifetimePeriods, 1), "мес.",
               "Сумма кривой удержания на горизонте расчёта")
           .Note("Синяя кривая считает ценность по марже и с дисконтом, вторая — по выручке. " +
                 "Разрыв между ними и есть типичная ошибка расчёта юнит-экономики.");

        rep.Table("Из чего складывается LTV", ["Показатель", "Значение"], [false, true])
           .Row("Выручка с клиента за месяц", Money(arpu) + " ₽")
           .Row("Маржинальный вклад за месяц", Money(result.ContributionPerPeriod) + " ₽")
           .Row("Маржинальность", Pct(result.ContributionMarginRate))
           .Row("LTV дисконтированный", Money(result.Ltv) + " ₽")
           .Row("LTV без дисконтирования", Money(result.UndiscountedLtv) + " ₽")
           .Row("«LTV» по выручке (завышенный)", Money(naive.Ltv) + " ₽")
           .Row("Прибыль с клиента за вычетом CAC", Money(result.NetContribution) + " ₽");

        var log = new StringBuilder();
        log.AppendLine($"CAC                    {Money(result.Cac)} ₽");
        log.AppendLine($"LTV (маржа, дисконт)   {Money(result.Ltv)} ₽");
        log.AppendLine($"LTV/CAC                {Num(result.LtvToCac)}");
        log.AppendLine($"Окупаемость            {Num(result.CacPaybackPeriods, 2)} мес.");
        log.AppendLine($"Ожидаемый срок жизни   {Num(result.ExpectedLifetimePeriods, 2)} мес.");
        log.AppendLine();
        log.AppendLine($"Завышение при счёте по выручке без дисконта: {Num(naive.Ltv / Math.Max(result.Ltv, 1e-9))}×");

        return Narrate(rep, result, log.ToString());
    }

    private static string DoChannelMix(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double budget = N(p, "budget", 3_000_000);
        double shareCtx = N(p, "share_ctx", 0.5);
        double cpa = N(p, "cpa", 9000);
        double organic = N(p, "organic", 120);
        double arpu = N(p, "arpu", 6000);
        double churn = N(p, "churn", 0.04);
        double gap = N(p, "quality_gap", 2.0);

        double ctxSpend = budget * shareCtx;
        double rest = budget - ctxSpend;
        double targetSpend = rest * 0.6;
        double partnerSpend = rest * 0.4;

        ChannelInput[] channels =
        [
            new() { Name = "Органика", Spend = 0, NewCustomers = organic,
                RevenuePerPeriod = arpu, GrossMarginRate = 0.8, ChurnRate = churn * 0.7, Horizon = 36 },
            new() { Name = "Контекст", Spend = ctxSpend, NewCustomers = ctxSpend / cpa,
                RevenuePerPeriod = arpu, GrossMarginRate = 0.8, ChurnRate = churn, Horizon = 36 },
            new() { Name = "Таргет", Spend = targetSpend, NewCustomers = targetSpend / (cpa * 1.4),
                RevenuePerPeriod = arpu * 1.1, GrossMarginRate = 0.8, ChurnRate = churn * 1.2, Horizon = 36 },
            new() { Name = "Партнёрка", Spend = partnerSpend, NewCustomers = partnerSpend / (cpa * 0.8),
                RevenuePerPeriod = arpu * 0.8, GrossMarginRate = 0.8, ChurnRate = churn * gap, Horizon = 36 },
        ];

        ChannelMixResult mix = ChannelEconomics.Analyze(channels);

        // ── График: LTV и CAC по каналам ─────────────────────────────────
        int n = mix.Channels.Count;
        Vector x = Axis(n, 1);
        cv.AddBar(x, Vec(mix.Channels.Select(c => c.Economics.Ltv)), "LTV канала", C(0));
        cv.AddBar(x, Vec(mix.Channels.Select(c => c.Economics.Cac)), "CAC канала", C(3));
        Segment(cv, 0.5, mix.BlendedCac, n + 0.5, mix.BlendedCac, C(1), "Blended CAC", 2);
        Segment(cv, 0.5, mix.PaidCac, n + 0.5, mix.PaidCac, C(2), "Paid CAC", 2);

        cv.ChartName = "LTV и CAC по каналам: " + string.Join(" · ", mix.Channels.Select((c, i) => $"{i + 1}. {c.Name}"));
        cv.LabelX = "Канал";
        cv.LabelY = "Рубли на клиента";

        int unprofitable = mix.Channels.Count(c => c.Economics.LtvToCac < 1);

        rep.Metric("Blended CAC", Money(mix.BlendedCac), "₽", "Все затраты делить на всех клиентов, включая органику")
           .Metric("Paid CAC", Money(mix.PaidCac), "₽", "Затраты платных каналов на клиентов платных каналов")
           .Metric("LTV / Paid CAC", Num(mix.LtvToPaidCac), "", "Честное отношение для решений о бюджете",
               mix.LtvToPaidCac >= 3 ? MetricTone.Good : mix.LtvToPaidCac >= 1 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Убыточных каналов", unprofitable, "шт.", "Каналы с LTV/CAC ниже единицы",
               unprofitable == 0 ? MetricTone.Good : MetricTone.Bad)
           .Metric("Итог микса", Money(mix.TotalNetContribution), "₽",
               "Суммарный маржинальный вклад минус все затраты",
               mix.TotalNetContribution > 0 ? MetricTone.Good : MetricTone.Bad);

        var table = rep.Table("Каналы по убыванию LTV/CAC",
            ["Канал", "Затраты", "Клиентов", "CAC", "LTV", "LTV/CAC", "Окупаемость, мес.", "Итог"],
            [false, true, true, true, true, true, true, true]);

        foreach (ChannelResult c in mix.Channels)
        {
            ChannelInput source = channels.First(ch => ch.Name == c.Name);
            table.Row(
                c.Name,
                Money(source.Spend),
                Int(source.NewCustomers),
                Money(c.Economics.Cac),
                Money(c.Economics.Ltv),
                Num(c.Economics.LtvToCac),
                double.IsNaN(c.Economics.CacPaybackPeriods) ? "—" : Num(c.Economics.CacPaybackPeriods, 1),
                Money(c.TotalNetContribution));
        }

        rep.Note("Blended CAC ниже Paid CAC ровно на вклад органики. Решение о бюджете, принятое " +
                 "по blended-показателю, финансирует убыточный канал за счёт бесплатного трафика.");

        var log = new StringBuilder();
        log.AppendLine($"Всего затрат      {Money(mix.TotalSpend)} ₽");
        log.AppendLine($"Всего клиентов    {Int(mix.TotalCustomers)}");
        log.AppendLine($"Blended CAC       {Money(mix.BlendedCac)} ₽");
        log.AppendLine($"Paid CAC          {Money(mix.PaidCac)} ₽");
        log.AppendLine($"Лучший канал      {mix.BestChannel}");
        log.AppendLine($"Худший канал      {mix.WorstChannel}");

        return Narrate(rep, mix, log.ToString());
    }
}
