using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Equity;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoFundingRound(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double preMoney = N(p, "premoney", 400_000_000);
        double investment = N(p, "investment", 100_000_000);
        double pool = N(p, "pool", 0.1);
        double safeAmount = N(p, "safe_amount", 20_000_000);
        double safeCap = N(p, "safe_cap", 120_000_000);
        double safeDiscount = N(p, "safe_discount", 0.2);
        double split = N(p, "founder_split", 0.6);

        var table = new CapTable()
            .AddHolding("Основатель 1", 10_000_000 * split)
            .AddHolding("Основатель 2", 10_000_000 * (1 - split));

        SafeNote[]? notes = safeAmount > 0
            ?
            [
                new SafeNote
                {
                    Holder = "Ангел (SAFE)",
                    Amount = safeAmount,
                    ValuationCap = safeCap,
                    Discount = safeDiscount,
                },
            ]
            : null;

        var round = new RoundInput
        {
            RoundName = "Series A",
            InvestorName = "Фонд",
            PreMoneyValuation = preMoney,
            Investment = investment,
            TargetOptionPoolPost = pool,
            ConvertingNotes = notes,
        };

        RoundResult withEverything = FundingRound.Execute(table, round);
        RoundResult naive = FundingRound.Execute(table, round with
        {
            TargetOptionPoolPost = 0,
            ConvertingNotes = null,
        });

        // ── График: доли до и после раунда ───────────────────────────────
        var holders = withEverything.CapTable.Ownership().ToList();
        Vector axis = Axis(holders.Count, 1);

        cv.AddBar(axis, Vec(holders.Select(h => h.Ownership * 100)), "Доля после раунда, %", C(0));

        var before = table.Ownership().ToDictionary(r => r.Holder, r => r.Ownership);
        cv.AddBar(axis, Vec(holders.Select(h => before.TryGetValue(h.Holder, out double v) ? v * 100 : 0)),
            "Доля до раунда, %", C(1));

        cv.ChartName = "Разводнение: " + string.Join(" · ", holders.Select((h, i) => $"{i + 1}. {h.Holder}"));
        cv.LabelX = "Держатель";
        cv.LabelY = "Доля, %";

        double foundersAfter = withEverything.CapTable.OwnershipOf("Основатель 1")
                             + withEverything.CapTable.OwnershipOf("Основатель 2");
        double foundersNaive = naive.CapTable.OwnershipOf("Основатель 1")
                             + naive.CapTable.OwnershipOf("Основатель 2");

        rep.Metric("Цена акции", Money(withEverything.PricePerShare), "₽",
               "Оценка после денег делить на полностью разводнённые акции")
           .Metric("Доля фонда", Pct(withEverything.InvestorOwnership), null,
               "Ровно инвестиция делить на оценку после денег")
           .Metric("Основатели после", Pct(foundersAfter), null,
               $"Без пула и SAFE было бы {Pct(foundersNaive)}",
               foundersAfter > 0.5 ? MetricTone.Good : MetricTone.Warn)
           .Metric("Цена пула и SAFE", Pct(foundersNaive - foundersAfter), "п.п.",
               "Скрытая часть разводнения, которой нет в term sheet", MetricTone.Bad)
           .Metric("Эффективная оценка", Money(withEverything.EffectivePreMoneyForFounders), "₽",
               $"Против заявленной {Money(preMoney)} ₽", MetricTone.Warn);

        var ownership = rep.Table("Таблица капитализации после раунда",
            ["Держатель", "Акций", "Доля до", "Доля после", "Разводнение"],
            [false, true, true, true, true]);

        foreach (OwnershipRow row in holders)
        {
            double had = before.TryGetValue(row.Holder, out double v) ? v : 0;
            ownership.Row(row.Holder, Int(row.Shares), had > 0 ? Pct(had) : "—",
                Pct(row.Ownership), had > 0 ? Pct((had - row.Ownership) / had) : "—");
        }

        if (withEverything.Conversions.Count > 0)
        {
            var conversions = rep.Table("Конвертация SAFE",
                ["Инвестор", "Сумма", "Цена конвертации", "Что сработало", "Акций", "Доля", "Эффективная оценка"],
                [false, true, true, false, true, true, true]);

            foreach (NoteConversion c in withEverything.Conversions)
                conversions.Row(c.Holder, Money(c.Amount), Num(c.ConversionPrice, 2), c.PriceDriver,
                    Int(c.Shares), Pct(c.OwnershipAfter), Money(c.EffectiveValuation));
        }

        rep.Note("Опционный пул создаётся до денег, поэтому его оплачивают только существующие " +
                 "акционеры: доля фонда остаётся ровно расчётной. Это и есть pool shuffle — " +
                 "самая дорогая строка term sheet, о которой в нём не сказано ни слова.");

        var log = new StringBuilder();
        log.AppendLine($"Оценка до денег:       {Money(preMoney)} ₽");
        log.AppendLine($"Оценка после денег:    {Money(withEverything.PostMoneyValuation)} ₽");
        log.AppendLine($"Цена акции:            {Num(withEverything.PricePerShare, 4)} ₽");
        log.AppendLine($"Новых опционов:        {Int(withEverything.NewPoolShares)}");
        log.AppendLine($"Всего акций после:     {Int(withEverything.TotalSharesAfter)}");
        log.AppendLine();
        log.AppendLine($"Основатели: {Pct(foundersNaive)} без пула и SAFE  →  {Pct(foundersAfter)} фактически");

        return Narrate(rep, withEverything, log.ToString());
    }

    private static string DoExitWaterfall(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double exit = N(p, "exit", 1_200_000_000);
        int prefType = I(p, "pref_type", 0);
        double multiple = N(p, "multiple", 1.0);
        double cap = N(p, "cap", 2.0);
        double aInvestment = N(p, "a_investment", 100_000_000);
        double aPreMoney = N(p, "a_premoney", 400_000_000);
        double bInvestment = N(p, "b_investment", 300_000_000);
        double bPreMoney = N(p, "b_premoney", 1_500_000_000);

        PreferenceType preference = prefType == 0 ? PreferenceType.NonParticipating : PreferenceType.Participating;
        double participationCap = prefType == 2 ? cap : double.NaN;

        var seed = new CapTable()
            .AddHolding("Основатель 1", 6_000_000)
            .AddHolding("Основатель 2", 4_000_000);

        CapTable afterA = FundingRound.Execute(seed, new RoundInput
        {
            RoundName = "Series A",
            InvestorName = "Фонд A",
            PreMoneyValuation = aPreMoney,
            Investment = aInvestment,
            TargetOptionPoolPost = 0.1,
        }).CapTable;

        CapTable afterB = FundingRound.Execute(afterA, new RoundInput
        {
            RoundName = "Series B",
            InvestorName = "Фонд B",
            PreMoneyValuation = bPreMoney,
            Investment = bInvestment,
            LiquidationMultiple = multiple,
            Preference = preference,
            ParticipationCap = participationCap,
        }).CapTable;

        ExitWaterfallResult result = ExitWaterfall.Distribute(afterB, exit);
        (Vector exits, IReadOnlyDictionary<string, Vector> curves) =
            ExitWaterfall.PayoutCurve(afterB, Math.Max(exit * 2, 4_000_000_000), 80);

        // ── График: выплаты как функция цены продажи ─────────────────────
        int color = 0;
        foreach ((string holder, Vector curve) in curves)
        {
            if (holder.Contains("пул", StringComparison.OrdinalIgnoreCase)) continue;
            cv.AddPlot(exits, curve, holder, C(color++), 2);
        }

        Segment(cv, exit, 0, exit, curves.Values.Max(c => c.Max()), C(7), "Текущая цена сделки", 2);

        cv.ChartName = "Кто сколько получит при разной цене продажи";
        cv.LabelX = "Цена продажи компании, ₽";
        cv.LabelY = "Выплата держателю, ₽";

        double founders = result.Payouts
            .Where(x => x.Holder.StartsWith("Основатель", StringComparison.Ordinal))
            .Sum(x => x.Payout);
        double foundersOwnership = result.Payouts
            .Where(x => x.Holder.StartsWith("Основатель", StringComparison.Ordinal))
            .Sum(x => x.Ownership);

        rep.Metric("Цена сделки", Money(exit), "₽", "Для этой суммы построена разбивка")
           .Metric("Преференции", Money(result.TotalPreferences), "₽",
               "Выплачивается раньше всех остальных", MetricTone.Warn)
           .Metric("Основатели получат", Money(founders), "₽",
               $"При доле в капитале {Pct(foundersOwnership)}",
               founders / Math.Max(exit, 1) >= foundersOwnership * 0.9 ? MetricTone.Good : MetricTone.Bad)
           .Metric("Доля основателей в деньгах", Pct(exit > 0 ? founders / exit : 0), null,
               "Сравните с их долей в капитале — это и есть эффект преференций")
           .Metric("Цена обыкновенной акции", Num(result.CommonPerShare, 2), "₽",
               "Столько стоит акция сотрудника при этой сделке");

        var payouts = rep.Table("Выплаты держателям",
            ["Держатель", "Класс", "Доля в капитале", "Выплата", "Доля в деньгах", "Возврат на вложенное"],
            [false, false, true, true, true, true]);

        foreach (HolderPayout row in result.Payouts)
            payouts.Row(row.Holder, row.ShareClass, Pct(row.Ownership), Money(row.Payout),
                Pct(row.ShareOfExit),
                double.IsNaN(row.MultipleOnInvested) ? "—" : Num(row.MultipleOnInvested) + "×");

        var classes = rep.Table("Решения по классам акций",
            ["Класс", "Решение", "Преференция", "Участие", "Всего", "Возврат"],
            [false, false, true, true, true, true]);

        foreach (ClassOutcome c in result.Classes)
            classes.Row(c.ClassName, c.Decision, Money(c.PreferencePaid), Money(c.ParticipationPaid),
                Money(c.TotalPaid), double.IsNaN(c.MultipleOnInvested) ? "—" : Num(c.MultipleOnInvested) + "×");

        rep.Note("Излом на кривой основателей — точка, где привилегированному классу становится " +
                 "выгоднее сконвертироваться в обыкновенные акции, чем брать преференцию. " +
                 "До неё рост цены сделки достаётся инвесторам почти целиком.");

        var log = new StringBuilder();
        log.AppendLine($"Цена сделки:          {Money(exit)} ₽");
        log.AppendLine($"Выплачено преференций: {Money(result.TotalPreferences)} ₽");
        log.AppendLine($"Остаток к разделу:     {Money(result.Residual)} ₽");
        log.AppendLine();
        foreach (HolderPayout row in result.Payouts)
            log.AppendLine($"{row.Holder,-22} {row.ShareClass,-18} {Money(row.Payout),14} ₽   ({Pct(row.ShareOfExit)})");

        return Narrate(rep, result, log.ToString());
    }
}
