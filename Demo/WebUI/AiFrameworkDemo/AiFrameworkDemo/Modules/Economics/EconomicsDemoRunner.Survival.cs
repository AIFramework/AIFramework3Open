using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Survival;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoKaplanMeier(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 150);
        double rateA = N(p, "rate_a", 0.05);
        double rateB = N(p, "rate_b", 0.09);
        double censor = N(p, "censor", 24);
        var rng = new Random(I(p, "seed", 5));

        var data = new List<SurvivalRecord>(n * 2);
        for (int group = 0; group < 2; group++)
        {
            double rate = group == 0 ? rateA : rateB;
            for (int i = 0; i < n; i++)
            {
                double time = ExponentialTime(rng, rate);
                data.Add(new SurvivalRecord
                {
                    Time = Math.Min(time, censor),
                    Event = time <= censor,
                    Group = group,
                });
            }
        }

        var all = new KaplanMeier();
        all.Fit(data);

        var a = new KaplanMeier();
        a.Fit([.. data.Where(r => r.Group == 0)]);
        var b = new KaplanMeier();
        b.Fit([.. data.Where(r => r.Group == 1)]);

        (double chi, double pValue) = KaplanMeier.LogRankTest(data);

        // ── График ───────────────────────────────────────────────────────
        cv.AddPlot(a.Times, a.SurvivalCurve, "Группа A", C(0), 3);
        cv.AddPlot(a.Times, a.Lower, "", C(0), 1);
        cv.AddPlot(a.Times, a.Upper, "Коридор 95 % группы A", C(0), 1);
        cv.AddPlot(b.Times, b.SurvivalCurve, "Группа B", C(3), 3);
        cv.AddPlot(b.Times, b.Lower, "", C(3), 1);
        cv.AddPlot(b.Times, b.Upper, "Коридор 95 % группы B", C(3), 1);

        cv.ChartName = $"Каплан — Мейер: лог-ранг p = {(pValue < 0.001 ? "< 0,001" : Num(pValue, 4))}";
        cv.LabelX = "Месяц жизни";
        cv.LabelY = "Доля доживших";

        int censored = data.Count(r => !r.Event);

        rep.Metric("Медиана A", double.IsNaN(a.MedianSurvivalTime) ? "> наблюдения" : Num(a.MedianSurvivalTime, 1),
               "мес.", "Момент, когда доживает половина группы")
           .Metric("Медиана B", double.IsNaN(b.MedianSurvivalTime) ? "> наблюдения" : Num(b.MedianSurvivalTime, 1),
               "мес.", "То же для второй группы")
           .Metric("Лог-ранг p", pValue < 0.001 ? "< 0,001" : Num(pValue, 4), "",
               "Различаются ли кривые статистически значимо",
               pValue < 0.05 ? MetricTone.Good : MetricTone.Warn)
           .Metric("Цензурировано", Pct(censored / (double)data.Count), null,
               "Клиенты, дожившие до конца наблюдения: выбросить их значило бы сместить оценку")
           .Metric("RMST A / B", $"{Num(a.RestrictedMeanSurvival(censor), 1)} / {Num(b.RestrictedMeanSurvival(censor), 1)}",
               "мес.", "Ограниченное среднее время жизни — определено всегда, в отличие от медианы");

        var table = rep.Table("Кривая дожития группы A",
            ["Месяц", "Под риском", "Ушло", "S(t)", "Нижняя 95 %", "Верхняя 95 %"],
            [true, true, true, true, true, true]);

        int step = Math.Max(1, a.Times.Count / 12);
        for (int i = 0; i < a.Times.Count; i += step)
            table.Row(Num(a.Times[i], 1), Int(a.AtRisk[i]), Int(a.Events[i]),
                Pct(a.SurvivalCurve[i]), Pct(a.Lower[i]), Pct(a.Upper[i]));

        rep.Note("Ступеньки кривой стоят в моменты реальных уходов; цензурированные клиенты " +
                 "не создают ступеньку, но уменьшают число под риском.");

        var log = new StringBuilder();
        log.AppendLine($"Группа A: n={n}, интенсивность {Num(rateA, 3)}/мес.");
        log.AppendLine($"Группа B: n={n}, интенсивность {Num(rateB, 3)}/мес.");
        log.AppendLine($"Лог-ранг: chi2 = {Num(chi, 3)}, p = {Num(pValue, 5)}");
        log.AppendLine($"Цензурировано: {censored} из {data.Count}");

        return Narrate(rep, all, log.ToString());
    }

    private static string DoCoxPh(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 400);
        double betaUsage = N(p, "beta_usage", -1.2);
        double betaSupport = N(p, "beta_support", 0.8);
        double baseRate = N(p, "base_rate", 0.05);
        double censor = N(p, "censor", 24);
        var rng = new Random(I(p, "seed", 11));

        var data = new List<SurvivalRecord>(n);
        for (int i = 0; i < n; i++)
        {
            double usage = rng.NextDouble();
            double support = Math.Round(rng.NextDouble() * 4) / 4.0;

            double rate = baseRate * Math.Exp((betaUsage * usage) + (betaSupport * support));
            double time = ExponentialTime(rng, rate);

            data.Add(new SurvivalRecord
            {
                Id = $"c{i + 1:D4}",
                Time = Math.Min(time, censor),
                Event = time <= censor,
                Covariates = new Vector(usage, support),
            });
        }

        var cox = new CoxProportionalHazards();
        cox.Fit(data, ["Интенсивность использования", "Обращения в поддержку"]);

        // ── График: кривые дожития для трёх профилей клиента ─────────────
        (string Name, double Usage, double Support)[] profiles =
        [
            ("Активный, без обращений", 0.9, 0.0),
            ("Средний клиент", 0.5, 0.5),
            ("Пассивный, много обращений", 0.1, 1.0),
        ];

        for (int i = 0; i < profiles.Length; i++)
        {
            Vector curve = cox.PredictSurvival(new Vector(profiles[i].Usage, profiles[i].Support));
            cv.AddPlot(cox.BaselineTimes, curve, profiles[i].Name, C(i), 3);
        }

        cv.ChartName = $"Регрессия Кокса: индекс конкордации {Num(cox.ConcordanceIndex, 3)}";
        cv.LabelX = "Месяц жизни";
        cv.LabelY = "Доля доживших";

        CoxCoefficient usageCoefficient = cox.Coefficients[0];
        CoxCoefficient supportCoefficient = cox.Coefficients[1];

        rep.Metric("Конкордация", Num(cox.ConcordanceIndex, 3), "",
               "Доля верно упорядоченных пар; 0,5 — не лучше монетки",
               cox.ConcordanceIndex > 0.7 ? MetricTone.Good : cox.ConcordanceIndex > 0.6 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("HR использования", Num(usageCoefficient.HazardRatio, 3), "×",
               $"Истинное значение: {Num(Math.Exp(betaUsage), 3)}")
           .Metric("HR обращений", Num(supportCoefficient.HazardRatio, 3), "×",
               $"Истинное значение: {Num(Math.Exp(betaSupport), 3)}")
           .Metric("Событий", Int(data.Count(r => r.Event)), "шт.",
               $"Из {n} клиентов; остальные цензурированы")
           .Metric("Итераций Ньютона", cox.Iterations, "", "Сходимость частичного правдоподобия");

        var table = rep.Table("Коэффициенты модели",
            ["Признак", "beta", "SE", "HR", "95 % ДИ", "p"],
            [false, true, true, true, false, true]);

        foreach (CoxCoefficient c in cox.Coefficients)
            table.Row(c.Name, Num(c.Beta, 4), Num(c.StandardError, 4), Num(c.HazardRatio, 3),
                $"{Num(c.HazardRatioLower, 2)} – {Num(c.HazardRatioUpper, 2)}",
                c.PValue < 0.001 ? "< 0,001" : Num(c.PValue, 4));

        var risky = rep.Table("Десять клиентов в наибольшей зоне риска",
            ["Клиент", "Использование", "Обращений", "Риск-скор", "Прожил, мес."],
            [false, true, true, true, true]);

        foreach (SurvivalRecord r in data
                     .Where(r => !r.Event)
                     .OrderByDescending(r => cox.RiskScore(r.Covariates!))
                     .Take(10))
        {
            risky.Row(r.Id, Num(r.Covariates![0], 2), Num(r.Covariates[1], 2),
                Num(cox.RiskScore(r.Covariates), 3), Num(r.Time, 1));
        }

        rep.Note("Отношение рисков читается так: HR = 2 означает удвоение мгновенной интенсивности " +
                 "оттока при росте признака на единицу. Список выше — действующие клиенты, " +
                 "отсортированные по риску: это и есть ответ на вопрос «кто уйдёт».");

        var log = new StringBuilder();
        log.AppendLine($"Истинные коэффициенты: usage = {Num(betaUsage, 3)}, support = {Num(betaSupport, 3)}");
        foreach (CoxCoefficient c in cox.Coefficients)
            log.AppendLine($"{c.Name,-32} beta={Num(c.Beta, 4),9}  SE={Num(c.StandardError, 4),8}  HR={Num(c.HazardRatio, 3),7}  p={Num(c.PValue, 5)}");
        log.AppendLine($"lnL(partial) = {Num(cox.LogPartialLikelihood, 2)}");
        log.AppendLine($"C-index      = {Num(cox.ConcordanceIndex, 4)}");

        return Narrate(rep, cox, log.ToString());
    }

    private static string DoCompetingRisks(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 600);
        double ratePrice = N(p, "rate_price", 0.04);
        double rateProduct = N(p, "rate_product", 0.03);
        double rateExternal = N(p, "rate_external", 0.02);
        double censor = N(p, "censor", 24);
        var rng = new Random(I(p, "seed", 13));

        var causeNames = new Dictionary<int, string>
        {
            [1] = "Ушёл из-за цены",
            [2] = "Ушёл из-за продукта",
            [3] = "Закрылся сам",
        };

        var data = new List<SurvivalRecord>(n);
        for (int i = 0; i < n; i++)
        {
            double tPrice = ExponentialTime(rng, ratePrice);
            double tProduct = ExponentialTime(rng, rateProduct);
            double tExternal = ExponentialTime(rng, rateExternal);

            double first = Math.Min(tPrice, Math.Min(tProduct, tExternal));
            int cause = first == tPrice ? 1 : first == tProduct ? 2 : 3;

            bool observed = first <= censor;
            data.Add(new SurvivalRecord
            {
                Time = observed ? first : censor,
                Event = observed,
                Cause = observed ? cause : 0,
            });
        }

        IReadOnlyList<CumulativeIncidence> cif = CompetingRisks.Analyze(data, causeNames);

        for (int i = 0; i < cif.Count; i++)
        {
            cv.AddPlot(cif[i].Times, cif[i].Incidence, cif[i].Name, C(i), 3);
            cv.AddPlot(cif[i].Times, cif[i].NaiveIncidence, $"{cif[i].Name} — наивно 1−KM", C(i), 1);
        }

        cv.ChartName = "Конкурирующие риски: Аален — Йохансен против наивной оценки";
        cv.LabelX = "Месяц жизни";
        cv.LabelY = "Доля ушедших по причине";

        double sum = cif.Sum(c => c.FinalIncidence);
        double naiveSum = cif.Sum(c => c.FinalNaiveIncidence);

        rep.Metric("Сумма причин", Pct(sum), null,
               "Аален — Йохансен: не превышает общую долю ушедших", MetricTone.Good)
           .Metric("Сумма наивных оценок", Pct(naiveSum), null,
               "1−KM по каждой причине: превышает единицу и потому бессмысленна",
               naiveSum > 1 ? MetricTone.Bad : MetricTone.Warn)
           .Metric("Завышение", Pct(naiveSum - sum), null, "Абсолютная величина ошибки наивного подхода")
           .Metric("Наблюдений", n, "шт.", "Размер выборки");

        var table = rep.Table("Итог по причинам на конец наблюдения",
            ["Причина", "Аален — Йохансен", "Наивно 1−KM", "Завышение"],
            [false, true, true, true]);

        foreach (CumulativeIncidence c in cif)
            table.Row(c.Name, Pct(c.FinalIncidence), Pct(c.FinalNaiveIncidence),
                Pct(c.FinalNaiveIncidence - c.FinalIncidence));

        rep.Note("Наивная оценка считает уход по другой причине цензурированием, то есть " +
                 "предполагает, что такой клиент «мог бы» уйти по нашей причине позже. Он не мог — " +
                 "его уже нет. Отсюда систематическое завышение и сумма долей больше единицы.");

        var log = new StringBuilder();
        foreach (CumulativeIncidence c in cif)
            log.AppendLine($"{c.Name,-24} CIF={Pct(c.FinalIncidence),8}   1-KM={Pct(c.FinalNaiveIncidence),8}");
        log.AppendLine();
        log.AppendLine($"Сумма CIF   = {Pct(sum)}");
        log.AppendLine($"Сумма 1-KM  = {Pct(naiveSum)}");

        return Narrate(rep, cif.Interpret(), log.ToString());
    }
}
