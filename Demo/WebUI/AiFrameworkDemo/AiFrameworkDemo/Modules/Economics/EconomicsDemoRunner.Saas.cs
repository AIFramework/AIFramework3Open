using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Runway;
using AI.Economics.Insights;
using AI.Economics.Saas;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoMrrBridge(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int customers = I(p, "customers", 200);
        int months = I(p, "months", 12);
        double mrr = N(p, "mrr", 25_000);
        double newRate = N(p, "new_rate", 0.08);
        double churnRate = N(p, "churn_rate", 0.03);
        double expansion = N(p, "expansion", 0.12);
        double contraction = N(p, "contraction", 0.06);
        var rng = new Random(I(p, "seed", 19));

        // ── Генерация снимков «клиент — выручка» по месяцам ──────────────
        var snapshots = new List<IReadOnlyDictionary<string, double>>(months);
        var current = new Dictionary<string, double>();
        for (int i = 0; i < customers; i++)
            current[$"c{i + 1:D4}"] = mrr * (0.5 + rng.NextDouble());

        snapshots.Add(new Dictionary<string, double>(current));
        int nextId = customers + 1;

        for (int m = 1; m < months; m++)
        {
            var next = new Dictionary<string, double>();

            foreach ((string id, double value) in current)
            {
                double roll = rng.NextDouble();
                if (roll < churnRate) continue;

                if (roll < churnRate + expansion) next[id] = value * (1.05 + (rng.NextDouble() * 0.3));
                else if (roll < churnRate + expansion + contraction) next[id] = value * (0.6 + (rng.NextDouble() * 0.3));
                else next[id] = value;
            }

            int born = (int)Math.Round(current.Count * newRate);
            for (int i = 0; i < born; i++)
                next[$"c{nextId++:D4}"] = mrr * (0.5 + rng.NextDouble());

            snapshots.Add(next);
            current = next;
        }

        IReadOnlyList<MrrBridgeResult> series = MrrBridge.BuildSeries(snapshots);
        MrrBridgeResult last = series[^1];

        // ── График: компоненты по месяцам ────────────────────────────────
        Vector axis = Axis(series.Count, 1);
        cv.AddBar(axis, Vec(series.Select(r => r.NewMrr)), "Новые", C(0));
        cv.AddBar(axis, Vec(series.Select(r => r.ExpansionMrr)), "Расширение", C(1));
        cv.AddBar(axis, Vec(series.Select(r => -r.ContractionMrr)), "Сжатие", C(2));
        cv.AddBar(axis, Vec(series.Select(r => -r.ChurnedMrr)), "Отток", C(3));
        cv.AddPlot(axis, Vec(series.Select(r => r.NetNewMrr)), "Чистый прирост", C(4), 3);

        cv.ChartName = "MRR-мостик по месяцам";
        cv.LabelX = "Месяц";
        cv.LabelY = "Изменение MRR, ₽";

        double avgNdr = series.Average(r => r.NetDollarRetention);
        double avgGrr = series.Average(r => r.GrossRevenueRetention);
        double avgQuick = series.Where(r => double.IsFinite(r.QuickRatio)).DefaultIfEmpty(last).Average(r => r.QuickRatio);

        rep.Metric("MRR на конец", Money(last.EndingMrr), "₽", $"Через {months} месяцев")
           .Metric("NDR (среднее)", Pct(avgNdr), null,
               "Удержание выручки с расширениями, без новых клиентов",
               avgNdr >= 1.1 ? MetricTone.Good : avgNdr >= 1.0 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("GRR (среднее)", Pct(avgGrr), null, "Удержание без расширений — потолок честности",
               avgGrr >= 0.9 ? MetricTone.Good : avgGrr >= 0.8 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Quick ratio", Num(avgQuick), "", "Прирост делить на потери; ниже единицы — сжатие",
               avgQuick >= 4 ? MetricTone.Good : avgQuick >= 1 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Отток логотипов", Pct(series.Average(r => r.LogoChurnRate)), "в месяц",
               "Доля ушедших клиентов");

        var table = rep.Table("Мостик по месяцам",
            ["Месяц", "MRR начало", "Новые", "Расширение", "Сжатие", "Отток", "MRR конец", "NDR", "GRR"],
            [true, true, true, true, true, true, true, true, true]);

        foreach ((MrrBridgeResult r, int i) in series.Select((r, i) => (r, i)))
        {
            table.Row((i + 1).ToString(),
                Money(r.StartingMrr), Money(r.NewMrr), Money(r.ExpansionMrr),
                "−" + Money(r.ContractionMrr), "−" + Money(r.ChurnedMrr),
                Money(r.EndingMrr), Pct(r.NetDollarRetention), Pct(r.GrossRevenueRetention));
        }

        rep.Note("Рост MRR сам по себе ничего не говорит: тот же прирост может складываться из " +
                 "большого притока при большом оттоке — такая машина требует всё больше денег " +
                 "на привлечение только чтобы стоять на месте.");

        var log = new StringBuilder();
        log.AppendLine($"MRR: {Money(series[0].StartingMrr)} → {Money(last.EndingMrr)} ₽ за {months} мес.");
        log.AppendLine($"Средний NDR: {Pct(avgNdr)}, GRR: {Pct(avgGrr)}, quick ratio: {Num(avgQuick)}");
        log.AppendLine($"Клиентов: {series[0].StartingCustomers} → {last.EndingCustomers}");

        return Narrate(rep, last, log.ToString());
    }

    private static string DoSaasHealth(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double arrStart = N(p, "arr_start", 120_000_000);
        double growth = N(p, "growth", 0.7);
        double smShare = N(p, "sm_share", 0.4);
        double burnShare = N(p, "burn_share", 0.5);
        double fcf = N(p, "fcf", -0.35);
        double cac = N(p, "cac", 300_000);
        double arpa = N(p, "arpa", 60_000);
        double margin = N(p, "margin", 0.8);
        double ndr = N(p, "ndr", 1.12);

        double arrEnd = arrStart * (1.0 + growth);

        var input = new SaasHealthInput
        {
            ArrStart = arrStart,
            ArrEnd = arrEnd,
            ArrYearAgo = arrStart,
            SalesAndMarketing = arrStart * smShare,
            NetBurn = arrStart * burnShare,
            FreeCashFlowMargin = fcf,
            GrossMarginRate = margin,
            ArpaMonthly = arpa,
            Cac = cac,
            NetDollarRetention = ndr,
        };

        IReadOnlyList<SaasMetric> metrics = SaasMetrics.Evaluate(input);

        // ── График: метрики относительно порога «хорошо» ─────────────────
        double[] targets = [40, 40, 0.75, 1.5, 12, 110];
        var ratios = new Vector(metrics.Count);
        for (int i = 0; i < metrics.Count; i++)
        {
            double target = i < targets.Length ? targets[i] : 1;
            bool lowerIsBetter = metrics[i].Name is "Burn multiple" or "CAC payback";
            double ratio = lowerIsBetter
                ? (metrics[i].Value > 0 ? target / metrics[i].Value : 2)
                : metrics[i].Value / target;
            ratios[i] = double.IsFinite(ratio) ? Math.Clamp(ratio, 0, 2.5) : 0;
        }

        cv.AddBar(Axis(metrics.Count, 1), ratios, "Отношение к целевому порогу", C(0));
        Segment(cv, 0.5, 1, metrics.Count + 0.5, 1, C(3), "Порог «хорошо»", 2);

        cv.ChartName = "Здоровье бизнеса: " + string.Join(" · ", metrics.Select((m, i) => $"{i + 1}. {m.Name}"));
        cv.LabelX = "Метрика";
        cv.LabelY = "Отношение к порогу (1 = норма)";

        foreach (SaasMetric m in metrics.Take(5))
        {
            rep.Metric(m.Name, Num(m.Value, m.Unit == "%" ? 1 : 2), m.Unit, m.Comment, m.Verdict switch
            {
                MetricVerdict.Good => MetricTone.Good,
                MetricVerdict.Warning => MetricTone.Warn,
                _ => MetricTone.Bad,
            });
        }

        var table = rep.Table("Метрики и оценки",
            ["Метрика", "Значение", "Оценка", "Как читать"], [false, true, false, false]);

        foreach (SaasMetric m in metrics)
        {
            table.Row(m.Name,
                Num(m.Value, m.Unit == "%" ? 1 : 2) + (string.IsNullOrEmpty(m.Unit) ? "" : " " + m.Unit),
                m.Verdict switch
                {
                    MetricVerdict.Good => "норма",
                    MetricVerdict.Warning => "внимание",
                    _ => "проблема",
                },
                m.Comment);
        }

        rep.Table("Исходные данные", ["Показатель", "Значение"], [false, true])
           .Row("ARR на начало года", Money(arrStart) + " ₽")
           .Row("ARR на конец года", Money(arrEnd) + " ₽")
           .Row("Затраты на S&M", Money(input.SalesAndMarketing) + " ₽")
           .Row("Чистое сжигание", Money(input.NetBurn) + " ₽")
           .Row("CAC", Money(cac) + " ₽")
           .Row("ARPA в месяц", Money(arpa) + " ₽");

        rep.Note("Метрики противоречат друг другу намеренно: рост можно купить бюджетом, " +
                 "но тогда испортятся magic number и burn multiple. Одновременно нарисовать " +
                 "все четыре невозможно — в этом их ценность.");

        var log = new StringBuilder();
        foreach (SaasMetric m in metrics)
            log.AppendLine($"{m.Name,-22} {Num(m.Value, 2),12} {m.Unit,-6} {m.Verdict}");

        return Narrate(rep, metrics.Interpret(), log.ToString());
    }

    private static string DoRunway(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var input = new RunwayInput
        {
            Cash = N(p, "cash", 60_000_000),
            MonthlyRevenue = N(p, "revenue", 6_000_000),
            RevenueGrowthMean = N(p, "growth", 0.07),
            RevenueGrowthVolatility = N(p, "vol", 0.15),
            MonthlyCosts = N(p, "costs", 12_000_000),
            CostGrowthMean = N(p, "cost_growth", 0.03),
            CostGrowthVolatility = 0.03,
            GrossMarginRate = N(p, "margin", 0.75),
            Horizon = I(p, "horizon", 30),
            Simulations = I(p, "sims", 4000),
            Seed = I(p, "seed", 42),
        };

        RunwayResult result = RunwaySimulator.Simulate(input);

        // ── График: коридор остатка денег ────────────────────────────────
        Vector axis = Axis(result.CashP50.Count);
        cv.AddPlot(axis, result.CashP90, "Оптимистичный сценарий (P90)", C(0), 1);
        cv.AddPlot(axis, result.CashP50, "Медианная траектория", C(1), 3);
        cv.AddPlot(axis, result.CashP10, "Пессимистичный сценарий (P10)", C(3), 1);
        Segment(cv, 0, 0, input.Horizon, 0, C(2), "Ноль на счету", 2);

        if (double.IsFinite(result.DeterministicRunwayMonths) && result.DeterministicRunwayMonths <= input.Horizon)
            Segment(cv, result.DeterministicRunwayMonths, result.CashP10.Min(),
                result.DeterministicRunwayMonths, result.CashP90.Max(), C(5), "Runway «касса / burn»", 1);

        cv.ChartName = "Остаток денег: коридор вместо одной линии";
        cv.LabelX = "Месяц";
        cv.LabelY = "Деньги на счету, ₽";

        string Months(double v) => double.IsPositiveInfinity(v) ? "> горизонта" : Num(v, 1);

        rep.Metric("Детерминированный runway", Months(result.DeterministicRunwayMonths), "мес.",
               "Касса делить на стартовый burn — то, что обычно называют runway", MetricTone.Warn)
           .Metric("Медиана исчерпания", Months(result.CashOutP50), "мес.",
               "50 % траекторий кончаются раньше этого месяца")
           .Metric("Пессимистично (P10)", Months(result.CashOutP10), "мес.",
               "В одном случае из десяти деньги кончатся уже к этому месяцу", MetricTone.Bad)
           .Metric("Дожили до горизонта", Pct(result.SurvivalProbability), null,
               $"Вероятность продержаться все {input.Horizon} мес.",
               result.SurvivalProbability > 0.8 ? MetricTone.Good
                   : result.SurvivalProbability > 0.5 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Риск кассового разрыва за год", Pct(result.ProbabilityCashOutIn12), null,
               "Вероятность остаться без денег в ближайшие 12 месяцев",
               result.ProbabilityCashOutIn12 < 0.1 ? MetricTone.Good
                   : result.ProbabilityCashOutIn12 < 0.3 ? MetricTone.Warn : MetricTone.Bad);

        rep.Table("Распределение срока жизни",
            ["Показатель", "Значение"], [false, true])
           .Row("Деньги кончатся за 6 мес.", Pct(result.ProbabilityCashOutIn6))
           .Row("Деньги кончатся за 12 мес.", Pct(result.ProbabilityCashOutIn12))
           .Row("Дожили до конца горизонта", Pct(result.SurvivalProbability))
           .Row("Вышли в плюс по денежному потоку", Pct(result.ProbabilityBreakEven))
           .Row("Медианный месяц выхода в плюс",
               double.IsNaN(result.MedianBreakEvenMonth) ? "не выходят" : Num(result.MedianBreakEvenMonth, 1));

        var path = rep.Table("Коридор остатка денег по месяцам",
            ["Месяц", "P10", "Медиана", "P90", "Доля живых"], [true, true, true, true, true]);

        int step = Math.Max(1, input.Horizon / 10);
        for (int m = 0; m <= input.Horizon; m += step)
            path.Row(m.ToString(), Money(result.CashP10[m]), Money(result.CashP50[m]),
                Money(result.CashP90[m]), Pct(result.SurvivalCurve[m]));

        rep.Note("Детерминированный runway — это медиана в лучшем случае и оптимистичная оценка " +
                 "в худшем. Решение о выходе на раунд принимается по левому хвосту: важно не то, " +
                 "когда деньги кончатся «в среднем», а когда они кончатся в неудачном сценарии.");

        var log = new StringBuilder();
        log.AppendLine($"Детерминированный runway: {Months(result.DeterministicRunwayMonths)} мес.");
        log.AppendLine($"P10 / медиана / P90:      {Months(result.CashOutP10)} / {Months(result.CashOutP50)} / {Months(result.CashOutP90)} мес.");
        log.AppendLine($"Вероятность дожить:       {Pct(result.SurvivalProbability)}");
        log.AppendLine($"Траекторий:               {input.Simulations}");

        return Narrate(rep, result, log.ToString());
    }
}
