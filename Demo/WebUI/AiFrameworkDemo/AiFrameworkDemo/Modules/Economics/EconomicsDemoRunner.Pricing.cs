using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Pricing;
using AI.Statistics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoElasticity(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double truth = N(p, "elasticity", -1.8);
        int n = I(p, "n", 400);
        double endogeneity = N(p, "endogeneity", 0.8);
        double noise = N(p, "noise", 0.1);
        int method = I(p, "method", 2);
        var rng = RandomEngine.Create(I(p, "seed", 11));

        // Цена реагирует и на издержки (инструмент), и на ненаблюдаемый спрос
        var data = new List<PriceObservation>(n);
        for (int i = 0; i < n; i++)
        {
            double cost = Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.35));
            double shock = RandomEngine.NextGaussian(rng, 0, 0.5);
            double price = Math.Exp(2.0 + (0.7 * Math.Log(cost)) + (endogeneity * shock)
                                    + RandomEngine.NextGaussian(rng, 0, noise));
            double quantity = Math.Exp(6.0 + (truth * Math.Log(price)) + shock
                                       + RandomEngine.NextGaussian(rng, 0, noise));

            data.Add(new PriceObservation
            {
                Price = price,
                Quantity = quantity,
                Instrument = cost,
                Unit = i % 5,
                Period = i / 5,
            });
        }

        var estimator = (ElasticityEstimator)Math.Clamp(method, 0, 2);
        IReadOnlyList<ElasticityResult> all = DemandElasticity.EstimateAll(data);
        ElasticityResult chosen = all.FirstOrDefault(r => r.Estimator == estimator) ?? all[0];

        // ── График: облако наблюдений и подогнанные кривые спроса ────────
        Vector logPrice = Vec(data.Select(o => Math.Log(o.Price)));
        Vector logQuantity = Vec(data.Select(o => Math.Log(o.Quantity)));
        cv.AddScatter(logPrice, logQuantity, "Наблюдения", C(1));

        double minLog = logPrice.Min(), maxLog = logPrice.Max();
        double meanLogP = logPrice.Average(), meanLogQ = logQuantity.Average();

        for (int i = 0; i < all.Count; i++)
        {
            var x = new Vector(minLog, maxLog);
            var y = new Vector(
                meanLogQ + (all[i].Elasticity * (minLog - meanLogP)),
                meanLogQ + (all[i].Elasticity * (maxLog - meanLogP)));
            cv.AddPlot(x, y, $"{Name(all[i].Estimator)}: {Num(all[i].Elasticity)}", C(i == 0 ? 3 : i + 3), 3);
        }

        var truthLine = new Vector(
            meanLogQ + (truth * (minLog - meanLogP)),
            meanLogQ + (truth * (maxLog - meanLogP)));
        cv.AddPlot(new Vector(minLog, maxLog), truthLine, $"Истина: {Num(truth)}", C(0), 2);

        cv.ChartName = "Эластичность спроса: наивный МНК против инструментальной оценки";
        cv.LabelX = "ln(цена)";
        cv.LabelY = "ln(объём)";

        var table = rep.Table("Сравнение способов оценки",
            ["Способ", "Эластичность", "Ошибка против истины", "p-значение", "F инструмента"],
            [false, true, true, true, true]);

        foreach (ElasticityResult r in all)
            table.Row(Name(r.Estimator), Num(r.Elasticity), Num(r.Elasticity - truth),
                Num(r.PValue, 4), double.IsNaN(r.FirstStageF) ? "—" : Num(r.FirstStageF, 1));

        var log = new StringBuilder();
        log.AppendLine($"Истинная эластичность: {Num(truth)}");
        log.AppendLine($"Сила эндогенности: {Num(endogeneity)}");
        foreach (ElasticityResult r in all)
            log.AppendLine($"{Name(r.Estimator),-34} {Num(r.Elasticity),8}   ошибка {Num(r.Elasticity - truth),7}");

        return Explain(rep, chosen, log.ToString());
    }

    private static string Name(ElasticityEstimator estimator) => estimator switch
    {
        ElasticityEstimator.LogLogOls => "Лог-лог МНК (наивный)",
        ElasticityEstimator.PanelFixedEffects => "Панель с фикс. эффектами",
        _ => "Двухшаговый МНК (инструмент)",
    };

    private static string DoPriceOptimization(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double ownElasticity = N(p, "own", -2.2);
        double crossElasticity = N(p, "cross", 0.6);
        double maxChange = N(p, "max_change", 0.25);
        double minMargin = N(p, "min_margin", 0.25);
        double minVolume = N(p, "min_volume", 0);

        ProductPricing[] products =
        [
            new() { Name = "Базовый", CurrentPrice = 1000, CurrentQuantity = 5000, UnitCost = 400 },
            new() { Name = "Стандарт", CurrentPrice = 1800, CurrentQuantity = 2500, UnitCost = 700 },
            new() { Name = "Премиум", CurrentPrice = 3200, CurrentQuantity = 800, UnitCost = 1200 },
        ];

        int k = products.Length;
        var elasticities = new Matrix(k, k);
        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++)
                elasticities[i, j] = i == j ? ownElasticity : crossElasticity / (k - 1);

        var constraints = new PriceConstraints
        {
            MaxPriceChange = maxChange,
            MinMarginRate = minMargin,
            MinTotalVolume = minVolume,
        };

        PriceOptimizationResult result = PriceOptimizer.Optimize(products, elasticities, constraints);

        // ── График: текущие и рекомендованные цены ───────────────────────
        Vector axis = Axis(k, 1);
        cv.AddBar(axis, Vec(result.Products.Select(r => r.CurrentPrice)), "Текущая цена", C(1));
        cv.AddBar(axis, Vec(result.Products.Select(r => r.OptimalPrice)), "Рекомендованная цена", C(0));

        cv.ChartName = "Цены: " + string.Join(" · ", result.Products.Select((r, i) => $"{i + 1}. {r.Name}"));
        cv.LabelX = "Товар";
        cv.LabelY = "Цена, ₽";

        var table = rep.Table("Рекомендации по позициям",
            ["Товар", "Цена сейчас", "Рекомендация", "Изменение", "Объём", "Маржа", "Прибыль", "Граница"],
            [false, true, true, true, true, true, true, false]);

        foreach (ProductPriceRecommendation row in result.Products)
            table.Row(row.Name, Money(row.CurrentPrice), Money(row.OptimalPrice), Pct(row.PriceChange),
                Int(row.OptimalQuantity), Pct(row.NewMargin),
                Money(row.OptimalProfit - row.CurrentProfit), row.AtBound ? "упёрлась" : "");

        var log = new StringBuilder();
        log.AppendLine($"Собственная эластичность: {Num(ownElasticity)}, перекрёстная: {Num(crossElasticity)}");
        log.AppendLine($"Прибыль: {Money(result.CurrentProfit)} → {Money(result.OptimalProfit)} ₽");
        log.AppendLine($"Оптимум без учёта каннибализации дал бы {Money(result.IndependentOptimumProfit)} ₽");

        return Explain(rep, result, log.ToString());
    }

    private static string DoWillingnessToPay(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int method = I(p, "method", 0);
        int respondents = I(p, "respondents", 300);
        double centre = N(p, "centre", 1000);
        double spread = N(p, "spread", 0.25);
        double unitCost = N(p, "cost", 300);
        var rng = RandomEngine.Create(I(p, "seed", 21));

        if (method == 0)
        {
            var answers = new List<VanWestendorpAnswer>(respondents);
            for (int i = 0; i < respondents; i++)
            {
                double personal = centre * Math.Exp(RandomEngine.NextGaussian(rng, 0, spread));
                answers.Add(new VanWestendorpAnswer(
                    personal * 0.45 * (0.9 + (rng.NextDouble() * 0.2)),
                    personal * 0.75 * (0.9 + (rng.NextDouble() * 0.2)),
                    personal * 1.25 * (0.9 + (rng.NextDouble() * 0.2)),
                    personal * 1.85 * (0.9 + (rng.NextDouble() * 0.2))));
            }

            VanWestendorpResult result = WillingnessToPay.VanWestendorp(answers);

            cv.AddPlot(result.Prices, result.TooCheapCurve, "Слишком дёшево", C(0), 2);
            cv.AddPlot(result.Prices, result.CheapCurve, "Выгодно", C(1), 2);
            cv.AddPlot(result.Prices, result.ExpensiveCurve, "Дорого", C(2), 2);
            cv.AddPlot(result.Prices, result.TooExpensiveCurve, "Слишком дорого", C(3), 2);
            Segment(cv, result.OptimalPricePoint, 0, result.OptimalPricePoint, 1, C(4),
                "Оптимальная цена", 3);

            cv.ChartName = "Ван Вестендорп: четыре кривые и точки их пересечения";
            cv.LabelX = "Цена, ₽";
            cv.LabelY = "Доля респондентов";

            rep.Table("Характерные точки", ["Точка", "Цена", "Смысл"], [false, true, false])
               .Row("Нижняя граница", Money(result.PointOfMarginalCheapness), "ниже — сомнения в качестве")
               .Row("Оптимальная цена", Money(result.OptimalPricePoint), "минимум суммарного отказа")
               .Row("Точка безразличия", Money(result.IndifferencePricePoint), "обычно цена лидера рынка")
               .Row("Верхняя граница", Money(result.PointOfMarginalExpensiveness), "выше — массовый отказ");

            return Explain(rep, result, $"Респондентов: {result.Respondents}, отброшено {result.InconsistentAnswers}");
        }

        // Габор — Грейнджер: лестница цен и доля согласившихся купить
        const int steps = 8;
        var prices = new Vector(steps);
        var acceptance = new Vector(steps);

        for (int i = 0; i < steps; i++)
        {
            double price = centre * (0.4 + (i * 0.25));
            prices[i] = price;

            int yes = 0;
            for (int r = 0; r < respondents; r++)
            {
                double reservation = centre * Math.Exp(RandomEngine.NextGaussian(rng, 0.2, spread));
                if (price <= reservation) yes++;
            }

            acceptance[i] = (double)yes / respondents;
        }

        GaborGrangerResult ladder = WillingnessToPay.GaborGranger(prices, acceptance, unitCost, respondents);

        cv.AddPlot(prices, acceptance, "Доля согласных купить", C(1), 3);
        cv.AddPlot(prices, Vec(ladder.Revenue.Select(v => v / prices.Max())), "Выручка (норм.)", C(2), 2);
        cv.AddPlot(prices, Vec(ladder.Profit.Select(v => v / prices.Max())), "Прибыль (норм.)", C(0), 3);
        Segment(cv, ladder.ProfitOptimalPrice, 0, ladder.ProfitOptimalPrice, 1, C(4), "Оптимум по прибыли", 2);

        cv.ChartName = "Габор — Грейнджер: кривая спроса и оптимум по прибыли";
        cv.LabelX = "Цена, ₽";
        cv.LabelY = "Доля / нормированные деньги";

        var ladderTable = rep.Table("Лестница цен",
            ["Цена", "Согласны купить", "Выручка", "Прибыль"], [true, true, true, true]);

        for (int i = 0; i < steps; i++)
            ladderTable.Row(Money(prices[i]), Pct(acceptance[i]),
                Money(ladder.Revenue[i]), Money(ladder.Profit[i]));

        return Explain(rep, ladder, $"Переменные издержки: {Money(unitCost)} ₽");
    }

    private static string DoConjoint(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int method = I(p, "method", 0);
        int respondents = I(p, "respondents", 150);
        int tasksPer = I(p, "tasks", 10);
        double heterogeneity = N(p, "heterogeneity", 0.6);
        var rng = RandomEngine.Create(I(p, "seed", 42));

        var design = new ConjointDesign(
        [
            new ConjointAttribute("Бренд", ["Базовый", "Известный"]),
            new ConjointAttribute("Поддержка", ["Почта", "Чат", "Персональный менеджер"]),
            new ConjointAttribute("Цена", ["1000", "2000", "3000"], [1000, 2000, 3000]),
        ]);

        double[] truth = [1.2, 0.6, 1.4, -0.0015];
        var tasks = new List<ChoiceTask>(respondents * tasksPer);

        for (int r = 0; r < respondents; r++)
        {
            var personal = new double[truth.Length];
            for (int a = 0; a < truth.Length; a++)
                personal[a] = truth[a] + (heterogeneity * RandomEngine.NextGaussian(rng) * Math.Abs(truth[a]));

            for (int t = 0; t < tasksPer; t++)
            {
                var alternatives = new List<ConjointProfile>(3);
                for (int j = 0; j < 3; j++)
                    alternatives.Add(new ConjointProfile([rng.Next(2), rng.Next(3), rng.Next(3)]));

                var utilities = new double[3];
                for (int j = 0; j < 3; j++)
                {
                    double[] row = design.Encode(alternatives[j]);
                    double u = 0;
                    for (int a = 0; a < row.Length; a++) u += personal[a] * row[a];
                    u += -Math.Log(-Math.Log(Math.Max(rng.NextDouble(), 1e-12)));
                    utilities[j] = u;
                }

                int chosen = 0;
                for (int j = 1; j < 3; j++) if (utilities[j] > utilities[chosen]) chosen = j;
                tasks.Add(new ChoiceTask { Respondent = r, Alternatives = alternatives, ChosenIndex = chosen });
            }
        }

        ConjointResult result;
        Vector shares;
        var showcase = new List<ConjointProfile>
        {
            new([1, 2, 0]),
            new([1, 1, 1]),
            new([0, 0, 2]),
        };

        if (method == 1)
        {
            var hb = new HierarchicalBayesConjoint();
            result = hb.Fit(tasks, design, draws: 400, burnIn: 300, seed: 7);
            shares = hb.SimulateShares(showcase);
        }
        else
        {
            var mnl = new MultinomialLogit();
            result = mnl.Fit(tasks, design);
            shares = mnl.SimulateShares(showcase);
        }

        // ── График: важность атрибутов и доли конфигураций ───────────────
        var importance = result.AttributeImportance.OrderByDescending(kv => kv.Value).ToList();
        cv.AddBar(Axis(importance.Count, 1), Vec(importance.Select(kv => kv.Value * 100)),
            "Важность атрибута, %", C(0));
        cv.AddBar(Axis(shares.Count, 1), Vec(shares.Select(v => v * 100)),
            "Доля конфигурации, %", C(1));

        cv.ChartName = "Важность: " + string.Join(" · ", importance.Select((kv, i) => $"{i + 1}. {kv.Key}"));
        cv.LabelX = "Атрибут / конфигурация";
        cv.LabelY = "Проценты";

        var worths = rep.Table("Частные полезности",
            ["Уровень", "Полезность", "Готовность платить", "p-значение"],
            [false, true, true, true]);

        foreach (PartWorth worth in result.PartWorths)
            worths.Row(worth.Name, Num(worth.Utility, 4),
                double.IsNaN(worth.WillingnessToPay) ? "—" : Money(worth.WillingnessToPay),
                Num(worth.PValue, 4));

        var simulator = rep.Table("Симулятор долей",
            ["Конфигурация", "Доля"], [false, true],
            note: "Три конфигурации товара, предъявленные вместе.");

        string[] labels = ["Известный + менеджер, 1000", "Известный + чат, 2000", "Базовый + почта, 3000"];
        for (int i = 0; i < shares.Count; i++) simulator.Row(labels[i], Pct(shares[i]));

        var log = new StringBuilder();
        log.AppendLine($"Респондентов: {respondents}, заданий на каждого: {tasksPer}");
        log.AppendLine($"Истинные полезности: {string.Join(", ", truth.Select(v => Num(v, 4)))}");

        return Explain(rep, result, log.ToString());
    }
}
