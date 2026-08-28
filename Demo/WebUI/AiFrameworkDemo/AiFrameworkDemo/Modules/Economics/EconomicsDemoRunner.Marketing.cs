using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Experiments;
using AI.Economics.Marketing;
using AI.Statistics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    /// <summary>Синтетический рынок с двумя каналами известной динамики.</summary>
    private static MmmInput BuildMarket(
        int weeks, double tvDecay, double digitalDecay, double saturation, double noise, int seed)
    {
        Random rng = RandomEngine.Create(seed);

        var tvSpend = new Vector(weeks);
        var digitalSpend = new Vector(weeks);
        var sales = new Vector(weeks);

        double tvCarry = 0, digitalCarry = 0;

        for (int t = 0; t < weeks; t++)
        {
            tvSpend[t] = t % 8 < 3 ? 800_000 * (0.7 + (rng.NextDouble() * 0.6)) : 0;
            digitalSpend[t] = 300_000 * (0.6 + (rng.NextDouble() * 0.8));

            tvCarry = tvSpend[t] + (tvDecay * tvCarry);
            digitalCarry = digitalSpend[t] + (digitalDecay * digitalCarry);

            double tvEffect = 4_000_000 * MarketingMixModel.Hill(tvCarry, 1_500_000 * saturation, 1.5);
            double digitalEffect = 2_000_000 * MarketingMixModel.Hill(digitalCarry, 400_000 * saturation, 1.2);

            double baseline = 8_000_000 + (12_000 * t) + (900_000 * Math.Sin(2 * Math.PI * t / 52.0));
            sales[t] = baseline + tvEffect + digitalEffect
                     + RandomEngine.NextGaussian(rng, 0, noise * 1_000_000);
        }

        return new MmmInput
        {
            Sales = sales,
            Channels =
            [
                new MediaChannel { Name = "ТВ", Spend = tvSpend },
                new MediaChannel { Name = "Digital", Spend = digitalSpend },
            ],
            SeasonalPeriod = 52,
            FourierTerms = 2,
            Ridge = 1e-4,
            MarginRate = 0.4,
            TuningIterations = 2000,
        };
    }

    private static string DoMarketingMix(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int weeks = I(p, "weeks", 156);
        double tvDecay = N(p, "tv_decay", 0.6);
        double digitalDecay = N(p, "digital_decay", 0.2);
        double saturation = N(p, "saturation", 1.0);
        double noise = N(p, "noise", 0.25);

        MmmInput input = BuildMarket(weeks, tvDecay, digitalDecay, saturation, noise, I(p, "seed", 3));
        MmmResult result = MarketingMixModel.Fit(input);

        // ── График: факт, модель и вклад каналов ─────────────────────────
        Vector axis = Axis(weeks);
        cv.AddPlot(axis, input.Sales, "Фактические продажи", C(1), 2);
        cv.AddPlot(axis, result.Fitted, "Модель", C(0), 3);
        cv.AddPlot(axis, result.Baseline, "Базовая линия без рекламы", C(4), 2);

        for (int i = 0; i < result.Channels.Count; i++)
            cv.AddPlot(axis, result.Channels[i].Contribution, $"Вклад: {result.Channels[i].Name}", C(i + 5), 2);

        cv.ChartName = $"Маркетинг-микс: реклама объясняет {Pct(result.MediaShare)} продаж";
        cv.LabelX = "Неделя";
        cv.LabelY = "Продажи, ₽";

        var table = rep.Table("Каналы",
            ["Канал", "Затраты", "Вклад", "Доля продаж", "ROI", "Предельный ROI", "Полураспад", "Насыщение"],
            [false, true, true, true, true, true, true, true]);

        foreach (ChannelEffect channel in result.Channels)
            table.Row(channel.Name, Money(channel.TotalSpend), Money(channel.TotalContribution),
                Pct(channel.ContributionShare), Num(channel.Roi), Num(channel.MarginalRoi),
                Num(channel.HalfLife, 1) + " нед.", Num(channel.SaturationLevel, 2));

        var log = new StringBuilder();
        log.AppendLine($"Истинное затухание: ТВ {Num(tvDecay)}, digital {Num(digitalDecay)}");
        foreach (ChannelEffect channel in result.Channels)
            log.AppendLine($"{channel.Name,-10} оценка затухания {Num(channel.Decay)}, " +
                           $"крутизна {Num(channel.SaturationShape)}");

        return Explain(rep, result, log.ToString());
    }

    private static string DoBudgetAllocation(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double budgetScale = N(p, "budget_scale", 1.0);
        double tvDecay = N(p, "tv_decay", 0.6);
        double saturation = N(p, "saturation", 1.0);

        MmmInput input = BuildMarket(156, tvDecay, 0.2, saturation, 0.25, I(p, "seed", 3));
        MmmResult model = MarketingMixModel.Fit(input);

        double currentBudget = model.Channels.Sum(c => c.TotalSpend) / 156.0;
        BudgetAllocationResult allocation = BudgetOptimizer.Allocate(model, currentBudget * budgetScale);

        // ── График: кривые отклика и точки текущего и оптимального бюджета ──
        const int grid = 50;
        double maxSpend = allocation.Channels.Max(c => Math.Max(c.CurrentSpend, c.OptimalSpend)) * 2;

        for (int i = 0; i < model.Channels.Count; i++)
        {
            ChannelEffect effect = model.Channels[i];
            var x = new Vector(grid);
            var y = new Vector(grid);

            for (int g = 0; g < grid; g++)
            {
                x[g] = maxSpend * (g + 1) / grid;
                y[g] = BudgetOptimizer.Response(effect, x[g]);
            }

            cv.AddPlot(x, y, $"Отклик: {effect.Name}", C(i), 3);
        }

        foreach (ChannelBudget row in allocation.Channels)
        {
            cv.AddScatter(new Vector(row.CurrentSpend), new Vector(row.CurrentResponse), "", C(3));
            cv.AddScatter(new Vector(row.OptimalSpend), new Vector(row.OptimalResponse), "", C(0));
        }

        cv.ChartName = "Кривые насыщения: точки текущего и рекомендованного бюджета";
        cv.LabelX = "Затраты за период, ₽";
        cv.LabelY = "Отклик в продажах, ₽";

        var table = rep.Table("Распределение бюджета",
            ["Канал", "Сейчас", "Рекомендация", "Изменение", "Отклик сейчас", "Отклик после", "Предельная отдача"],
            [false, true, true, true, true, true, true]);

        foreach (ChannelBudget row in allocation.Channels)
            table.Row(row.Name, Money(row.CurrentSpend), Money(row.OptimalSpend), Pct(row.Change),
                Money(row.CurrentResponse), Money(row.OptimalResponse),
                Num(row.MarginalReturnAtOptimum, 3));

        return Explain(rep, allocation,
            $"Бюджет на период: {Money(allocation.TotalBudget)} ₽ " +
            $"({Num(budgetScale)}× от текущего)");
    }

    private static string DoUplift(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 8000);
        double effect = N(p, "effect", 0.30);
        double sleeping = N(p, "sleeping", 0.10);
        double promoCost = N(p, "promo_cost", 60);
        double margin = N(p, "margin", 300);
        var rng = RandomEngine.Create(I(p, "seed", 13));

        var data = new List<UpliftObservation>(n);
        for (int i = 0; i < n; i++)
        {
            double sensitivity = rng.NextDouble();
            double loyalty = rng.NextDouble();
            bool treated = rng.NextDouble() < 0.5;

            double baseRate = 0.10 + (0.25 * loyalty);
            double lift = (effect * sensitivity) - (sleeping * loyalty);
            double rate = Math.Clamp(baseRate + (treated ? lift : 0), 0.01, 0.95);

            data.Add(new UpliftObservation
            {
                Features = new Vector(sensitivity, loyalty),
                Treated = treated,
                Converted = rng.NextDouble() < rate,
            });
        }

        UpliftResult result = UpliftModeling.Fit(data, promoCost, margin);

        // ── График: кривая Qini против случайного охвата ─────────────────
        cv.AddPlot(result.QiniX, result.QiniY, "Ранжирование моделью", C(0), 3);
        cv.AddPlot(result.QiniX, result.RandomY, "Случайный охват", C(3), 2);
        Segment(cv, result.TargetedShare, 0, result.TargetedShare, result.QiniY.Max(), C(2),
            "Порог окупаемости", 2);

        cv.ChartName = $"Кривая Qini: коэффициент {Num(result.QiniCoefficient, 3)}";
        cv.LabelX = "Доля охваченных клиентов";
        cv.LabelY = "Накопленный прирост конверсий";

        var table = rep.Table("Группы по предсказанному приросту",
            ["Группа", "Клиентов", "Предсказано", "Фактически", "Конверсия с промо", "Без промо"],
            [true, true, true, true, true, true]);

        foreach (UpliftDecile group in result.Groups)
            table.Row(group.Group.ToString(), Int(group.Count), Pct(group.PredictedUplift),
                Pct(group.ActualUplift), Pct(group.TreatedRate), Pct(group.ControlRate));

        var economics = rep.Table("Экономика охвата",
            ["Сценарий", "Прибыль"], [false, true])
            .Row("Не давать промо никому", Money(0))
            .Row("Дать промо всем", Money(result.ProfitTreatAll))
            .Row("Дать промо восприимчивым", Money(result.ProfitTargeted));

        var log = new StringBuilder();
        log.AppendLine($"Стоимость промо: {Money(promoCost)} ₽, маржа с конверсии: {Money(margin)} ₽");
        log.AppendLine($"Порог прироста для окупаемости: {Pct(result.ProfitThreshold)}");

        return Explain(rep, result, log.ToString());
    }

    private static string DoExperimentDesign(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double baseline = N(p, "baseline", 0.05);
        double effect = N(p, "effect", 0.10);
        double alpha = N(p, "alpha", 0.05);
        double power = N(p, "power", 0.8);
        int variants = I(p, "variants", 2);
        double traffic = N(p, "traffic", 2000);

        SampleSizeResult result = ExperimentDesign.ForProportions(
            baseline, effect, alpha, power, variants, traffic);

        // ── График: требуемая выборка как функция обнаруживаемого эффекта ──
        const int grid = 40;
        var effects = new Vector(grid);
        var sizes = new Vector(grid);
        var days = new Vector(grid);

        for (int i = 0; i < grid; i++)
        {
            double mde = 0.02 + (i * 0.01);
            SampleSizeResult point = ExperimentDesign.ForProportions(
                baseline, mde, alpha, power, variants, traffic);

            effects[i] = mde * 100;
            sizes[i] = point.PerVariant;
            days[i] = point.DaysRequired;
        }

        cv.AddPlot(effects, sizes, "Наблюдений на вариант", C(0), 3);
        cv.AddPlot(effects, Vec(days.Select(d => d * traffic / variants)), "Эквивалент по трафику", C(1), 2);
        Segment(cv, effect * 100, 0, effect * 100, sizes.Max(), C(3), "Заданный эффект", 2);

        cv.ChartName = "Размер выборки резко растёт при уменьшении обнаруживаемого эффекта";
        cv.LabelX = "Обнаруживаемый эффект, %";
        cv.LabelY = "Наблюдений на вариант";

        var table = rep.Table("Что можно обнаружить за разумный срок",
            ["Срок", "Наблюдений на вариант", "Обнаруживаемый эффект"],
            [false, true, true]);

        foreach (int weeks in new[] { 1, 2, 4, 8 })
        {
            int perVariant = (int)(traffic * weeks * 7 / variants);
            if (perVariant < 10) continue;

            table.Row($"{weeks} нед.", Int(perVariant),
                Pct(ExperimentDesign.MinimumDetectableEffect(baseline, perVariant, alpha, power)));
        }

        return Explain(rep, result,
            $"Базовая конверсия {Pct(baseline)}, трафик {Int(traffic)} в сутки, вариантов {variants}");
    }

    private static string DoCuped(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 2000);
        double correlation = N(p, "correlation", 0.8);
        double effect = N(p, "effect", 0.5);
        var rng = RandomEngine.Create(I(p, "seed", 31));

        var controlPre = new Vector(n);
        var controlPost = new Vector(n);
        var treatmentPre = new Vector(n);
        var treatmentPost = new Vector(n);

        double noise = Math.Sqrt(Math.Max(1 - (correlation * correlation), 1e-6)) / Math.Max(correlation, 1e-6);

        for (int i = 0; i < n; i++)
        {
            double levelA = RandomEngine.NextGaussian(rng, 10, 3);
            double levelB = RandomEngine.NextGaussian(rng, 10, 3);

            controlPre[i] = levelA + RandomEngine.NextGaussian(rng, 0, noise);
            controlPost[i] = levelA + RandomEngine.NextGaussian(rng, 0, noise);
            treatmentPre[i] = levelB + RandomEngine.NextGaussian(rng, 0, noise);
            treatmentPost[i] = levelB + effect + RandomEngine.NextGaussian(rng, 0, noise);
        }

        CupedResult result = Cuped.Apply(controlPre, controlPost, treatmentPre, treatmentPost);

        // ── График: распределение метрики до и после коррекции ───────────
        cv.AddScatter(controlPre, controlPost, "Контроль: до и после", C(1));
        cv.AddScatter(treatmentPre, treatmentPost, "Воздействие: до и после", C(0));

        double min = controlPre.Min(), max = controlPre.Max();
        Segment(cv, min, min, max, max, C(3), "Линия равенства", 2);

        cv.ChartName = $"CUPED: снижение дисперсии на {Pct(result.VarianceReduction)}";
        cv.LabelX = "Метрика до эксперимента";
        cv.LabelY = "Метрика в эксперименте";

        rep.Table("Что дала коррекция",
            ["Показатель", "Без CUPED", "С CUPED"], [false, true, true])
           .Row("Оценка эффекта", Num(result.RawEffect, 4), Num(result.AdjustedEffect, 4))
           .Row("Стандартная ошибка", Num(result.RawStandardError, 4), Num(result.AdjustedStandardError, 4))
           .Row("p-значение", Num(result.RawPValue, 4), Num(result.AdjustedPValue, 4))
           .Row("Эквивалентный размер выборки", Int(n), Int(n * result.EffectiveSampleGain));

        return Explain(rep, result,
            $"Коэффициент коррекции theta = {Num(result.Theta, 4)}, " +
            $"корреляция с прошлым {Num(result.Correlation, 3)}");
    }

    private static string DoSequentialTesting(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int method = I(p, "method", 0);
        int n = I(p, "n", 4000);
        double baseline = N(p, "baseline", 0.10);
        double lift = N(p, "lift", 0.30);
        double tau = N(p, "tau", 0.05);
        var rng = RandomEngine.Create(I(p, "seed", 77));

        var control = new Vector(n);
        var treatment = new Vector(n);
        int successA = 0, successB = 0;

        for (int i = 0; i < n; i++)
        {
            bool a = rng.NextDouble() < baseline;
            bool b = rng.NextDouble() < baseline * (1 + lift);

            control[i] = a ? 1 : 0;
            treatment[i] = b ? 1 : 0;
            if (a) successA++;
            if (b) successB++;
        }

        if (method == 1)
        {
            BayesianAbResult bayes = SequentialTest.Bayesian(successA, n, successB, n);

            // ── График: апостериорные плотности вариантов ────────────────
            const int grid = 200;
            var x = new Vector(grid);
            var densityA = new Vector(grid);
            var densityB = new Vector(grid);

            double lo = Math.Max(0, Math.Min(bayes.PosteriorMeanA, bayes.PosteriorMeanB) - 0.05);
            double hi = Math.Max(bayes.PosteriorMeanA, bayes.PosteriorMeanB) + 0.05;

            for (int i = 0; i < grid; i++)
            {
                double v = lo + ((hi - lo) * i / (grid - 1));
                x[i] = v;
                densityA[i] = BetaDensity(v, successA + 1, n - successA + 1);
                densityB[i] = BetaDensity(v, successB + 1, n - successB + 1);
            }

            cv.AddPlot(x, densityA, "Вариант A", C(1), 3);
            cv.AddPlot(x, densityB, "Вариант B", C(0), 3);

            cv.ChartName = $"Апостериорные распределения: P(B лучше A) = {Pct(bayes.ProbabilityBetter)}";
            cv.LabelX = "Конверсия";
            cv.LabelY = "Плотность";

            rep.Table("Итог сравнения", ["Показатель", "Значение"], [false, true])
               .Row("Конверсия A", Pct(bayes.PosteriorMeanA, 2))
               .Row("Конверсия B", Pct(bayes.PosteriorMeanB, 2))
               .Row("P(B лучше A)", Pct(bayes.ProbabilityBetter))
               .Row("Ожидаемые потери от B", Num(bayes.ExpectedLossChoosingB, 5))
               .Row("Интервал разности",
                   $"[{Pct(bayes.CredibleLow, 2)}; {Pct(bayes.CredibleHigh, 2)}]");

            return Explain(rep, bayes, $"Наблюдений в каждой группе: {n}");
        }

        SequentialTestResult result = SequentialTest.Run(control, treatment, tau);

        Vector axis = Axis(result.PValues.Count, 1);
        cv.AddPlot(axis, result.PValues, "Всегда допустимое p-значение", C(0), 3);
        cv.AddPlot(axis, result.EffectPath, "Оценка эффекта", C(1), 2);
        cv.AddPlot(axis, Finite(result.LowerBound), "Нижняя граница", C(4), 1);
        cv.AddPlot(axis, Finite(result.UpperBound), "Верхняя граница", C(4), 1);
        Segment(cv, 1, result.Alpha, result.PValues.Count, result.Alpha, C(3), "Порог значимости", 2);

        if (result.StoppingPoint > 0)
            Segment(cv, result.StoppingPoint, 0, result.StoppingPoint, 1, C(2), "Точка остановки", 2);

        cv.ChartName = "Последовательный критерий: на график можно смотреть в любой момент";
        cv.LabelX = "Наблюдений в каждой группе";
        cv.LabelY = "p-значение и эффект";

        return Explain(rep, result,
            $"Истинный прирост: {Pct(lift)} к базовой конверсии {Pct(baseline)}");
    }

    /// <summary>Плотность бета-распределения через логарифм бета-функции.</summary>
    private static double BetaDensity(double x, double alpha, double beta)
    {
        if (x is <= 0 or >= 1) return 0;

        double logNumerator = ((alpha - 1) * Math.Log(x)) + ((beta - 1) * Math.Log(1 - x));
        double logBeta = LogGamma(alpha) + LogGamma(beta) - LogGamma(alpha + beta);
        return Math.Exp(logNumerator - logBeta);
    }

    private static double LogGamma(double x)
    {
        double[] c =
        [
            0.99999999999980993, 676.5203681218851, -1259.1392167224028,
            771.32342877765313, -176.61502916214059, 12.507343278686905,
            -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
        ];

        double z = x - 1.0;
        double a = c[0];
        for (int i = 1; i < c.Length; i++) a += c[i] / (z + i);

        double t = z + 7.5;
        return (0.5 * Math.Log(2 * Math.PI)) + ((z + 0.5) * Math.Log(t)) - t + Math.Log(a);
    }

    private static string DoBandits(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int arms = I(p, "arms", 3);
        double best = N(p, "best_rate", 0.12);
        double gap = N(p, "gap", 0.03);
        int rounds = I(p, "rounds", 20_000);
        double epsilon = N(p, "epsilon", 0.1);
        int policy = I(p, "policy", 3);

        var rates = new Vector(arms);
        var names = new List<string>(arms);
        for (int a = 0; a < arms; a++)
        {
            rates[a] = Math.Max(best - (gap * (arms - 1 - a)), 0.005);
            names.Add($"Вариант {(char)('A' + a)}");
        }

        IReadOnlyList<BanditSimulationResult> all =
            Bandits.CompareAll(names, rates, rounds, epsilon, I(p, "seed", 9));

        BanditSimulationResult chosen = all.FirstOrDefault(r => (int)r.Policy == policy) ?? all[0];

        // ── График: накопленные потери по стратегиям ─────────────────────
        Vector axis = Axis(rounds, 1);
        for (int i = 0; i < all.Count; i++)
            cv.AddPlot(axis, all[i].RegretPath, PolicyName(all[i].Policy), C(i), i == 0 ? 3 : 2);

        cv.ChartName = "Накопленные потери: чем ниже кривая, тем лучше стратегия";
        cv.LabelX = "Показов";
        cv.LabelY = "Недополученные конверсии";

        var comparison = rep.Table("Сравнение стратегий",
            ["Стратегия", "Потери", "Доля лучшего варианта", "Конверсий получено"],
            [false, true, true, true]);

        foreach (BanditSimulationResult r in all)
            comparison.Row(PolicyName(r.Policy), Int(r.Regret), Pct(r.BestArmShare), Int(r.TotalReward));

        var traffic = rep.Table($"Распределение трафика: {PolicyName(chosen.Policy)}",
            ["Вариант", "Истинная конверсия", "Показов", "Доля трафика", "Наблюдённая конверсия"],
            [false, true, true, true, true]);

        foreach (BanditArmResult arm in chosen.Arms)
            traffic.Row(arm.Name, Pct(arm.TrueRate), Int(arm.Pulls), Pct(arm.TrafficShare),
                Pct(arm.ObservedRate));

        return Explain(rep, chosen, $"Вариантов: {arms}, показов: {Int(rounds)}");
    }

    private static string PolicyName(BanditPolicy policy) => policy switch
    {
        BanditPolicy.EqualSplit => "Равномерное деление",
        BanditPolicy.EpsilonGreedy => "Эпсилон-жадная",
        BanditPolicy.UpperConfidenceBound => "UCB",
        _ => "Томпсон",
    };
}
