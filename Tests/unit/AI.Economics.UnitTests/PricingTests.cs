using AI.DataStructs.Algebraic;
using AI.Economics.Pricing;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

public class PricingTests
{
    /// <summary>
    /// Данные с эндогенной ценой: продавец поднимает цену тогда, когда
    /// ненаблюдаемый спрос высок. Инструмент — себестоимость.
    /// </summary>
    private static List<PriceObservation> EndogenousMarket(double trueElasticity, int n, int seed)
    {
        var rng = RandomEngine.Create(seed);
        var data = new List<PriceObservation>(n);

        for (int i = 0; i < n; i++)
        {
            double cost = Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.35));
            double shock = RandomEngine.NextGaussian(rng, 0, 0.5);

            // Цена реагирует и на издержки, и на ненаблюдаемый спрос
            double logPrice = (0.7 * Math.Log(cost)) + (0.8 * shock) + RandomEngine.NextGaussian(rng, 0, 0.1);
            double price = Math.Exp(logPrice + 2.0);

            double logQuantity = 6.0 + (trueElasticity * Math.Log(price)) + shock
                               + RandomEngine.NextGaussian(rng, 0, 0.1);

            data.Add(new PriceObservation
            {
                Price = price,
                Quantity = Math.Exp(logQuantity),
                Instrument = cost,
                Unit = i % 4,
                Period = i / 4,
            });
        }

        return data;
    }

    [Fact]
    public void Elasticity_NaiveOls_IsBiasedTowardsZero()
    {
        const double truth = -1.8;
        List<PriceObservation> data = EndogenousMarket(truth, 600, 11);

        ElasticityResult ols = DemandElasticity.Estimate(data, ElasticityEstimator.LogLogOls);

        // Смещение вверх: цена коррелирует с положительным шоком спроса
        Assert.True(ols.Elasticity > truth + 0.3,
            $"Наивный МНК обязан быть смещён к нулю, получено {ols.Elasticity:F3}.");
    }

    [Fact]
    public void Elasticity_InstrumentalVariables_RecoversTruth()
    {
        const double truth = -1.8;
        List<PriceObservation> data = EndogenousMarket(truth, 600, 11);

        ElasticityResult iv = DemandElasticity.Estimate(data, ElasticityEstimator.InstrumentalVariables);

        Assert.InRange(iv.Elasticity, truth - 0.35, truth + 0.35);
        Assert.True(iv.FirstStageF > 10, $"Инструмент должен быть сильным, F = {iv.FirstStageF:F1}.");
        Assert.True(iv.IsElastic);
    }

    [Fact]
    public void Elasticity_EstimateAll_ReturnsNaiveForComparison()
    {
        List<PriceObservation> data = EndogenousMarket(-1.5, 400, 5);
        IReadOnlyList<ElasticityResult> all = DemandElasticity.EstimateAll(data);

        Assert.Equal(3, all.Count);
        foreach (ElasticityResult result in all)
            Assert.Equal(all[0].Elasticity, result.NaiveElasticity, 6);
    }

    [Fact]
    public void Elasticity_Interpretation_WarnsAboutPlainOls()
    {
        ElasticityResult ols = DemandElasticity.Estimate(EndogenousMarket(-1.6, 300, 3));
        var interpretation = ols.Interpret();

        Assert.NotEmpty(interpretation.Summary);
        Assert.NotEmpty(interpretation.Metrics);
        Assert.Contains(interpretation.Warnings, w => w.Contains("эндогенн"));
        Assert.Contains(interpretation.Recommendations, r => r.Contains("инструмент"));
    }

    [Fact]
    public void PriceOptimizer_SingleProduct_MatchesLernerRule()
    {
        const double elasticity = -2.5;
        const double cost = 100.0;
        double lerner = PriceOptimizer.LernerPrice(cost, elasticity);

        var products = new[]
        {
            new ProductPricing { Name = "Товар", CurrentPrice = 150, CurrentQuantity = 1000, UnitCost = cost },
        };

        PriceOptimizationResult result = PriceOptimizer.Optimize(
            products,
            CrossElasticity.Diagonal(new Vector(elasticity)),
            new PriceConstraints { MaxPriceChange = 0.9 });

        // Правило Лернера: p = c * e / (1 + e) = 100 * 2,5/1,5 = 166,67
        Assert.Equal(lerner, result.Products[0].OptimalPrice, 0);
        Assert.True(result.OptimalProfit >= result.CurrentProfit);
    }

    [Fact]
    public void PriceOptimizer_RespectsPriceChangeBound()
    {
        var products = new[]
        {
            new ProductPricing { Name = "Товар", CurrentPrice = 100, CurrentQuantity = 1000, UnitCost = 20 },
        };

        PriceOptimizationResult result = PriceOptimizer.Optimize(
            products,
            CrossElasticity.Diagonal(new Vector(-1.2)),
            new PriceConstraints { MaxPriceChange = 0.1 });

        Assert.InRange(result.Products[0].OptimalPrice, 89.99, 110.01);
        Assert.True(result.ConstraintsBinding);
    }

    [Fact]
    public void PriceOptimizer_RespectsMinimumMargin()
    {
        var products = new[]
        {
            new ProductPricing { Name = "Товар", CurrentPrice = 100, CurrentQuantity = 1000, UnitCost = 60 },
        };

        // Спрос очень эластичен: без ограничения цена ушла бы вниз
        PriceOptimizationResult result = PriceOptimizer.Optimize(
            products,
            CrossElasticity.Diagonal(new Vector(-6.0)),
            new PriceConstraints { MaxPriceChange = 0.5, MinMarginRate = 0.35 });

        Assert.True(result.Products[0].NewMargin >= 0.34,
            $"Маржа {result.Products[0].NewMargin:P1} нарушила ограничение.");
    }

    [Fact]
    public void PriceOptimizer_CannibalizationCostIsNonNegative()
    {
        var products = new[]
        {
            new ProductPricing { Name = "Базовый", CurrentPrice = 100, CurrentQuantity = 2000, UnitCost = 40 },
            new ProductPricing { Name = "Премиум", CurrentPrice = 180, CurrentQuantity = 800, UnitCost = 70 },
        };

        // Товары-заменители: снижение цены одного забирает спрос у другого
        var elasticities = new Matrix(2, 2);
        elasticities[0, 0] = -2.0; elasticities[0, 1] = 0.8;
        elasticities[1, 0] = 0.9; elasticities[1, 1] = -2.2;

        PriceOptimizationResult result = PriceOptimizer.Optimize(products, elasticities);

        Assert.True(result.CannibalizationCost >= 0);
        Assert.True(result.OptimalProfit >= result.IndependentOptimumProfit - 1e-6,
            "Учёт каннибализации не может дать прибыль ниже, чем её игнорирование.");
        Assert.NotEmpty(result.Interpret().Metrics);
    }

    [Fact]
    public void CrossElasticity_RecoversSubstitutionSign()
    {
        var rng = RandomEngine.Create(7);
        const int periods = 200;

        var prices = new Matrix(periods, 2);
        var quantities = new Matrix(periods, 2);

        for (int t = 0; t < periods; t++)
        {
            double p1 = 100 * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.2));
            double p2 = 150 * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.2));

            prices[t, 0] = p1;
            prices[t, 1] = p2;

            // Товар 1: собственная -2, перекрёстная +0,7 (заменители)
            quantities[t, 0] = Math.Exp(12 + (-2.0 * Math.Log(p1)) + (0.7 * Math.Log(p2))
                                        + RandomEngine.NextGaussian(rng, 0, 0.05));
            quantities[t, 1] = Math.Exp(12 + (0.5 * Math.Log(p1)) + (-1.6 * Math.Log(p2))
                                        + RandomEngine.NextGaussian(rng, 0, 0.05));
        }

        Matrix estimated = CrossElasticity.Estimate(prices, quantities);

        Assert.InRange(estimated[0, 0], -2.3, -1.7);
        Assert.InRange(estimated[0, 1], 0.4, 1.0);
        Assert.InRange(estimated[1, 1], -1.9, -1.3);
    }

    [Fact]
    public void VanWestendorp_FindsOrderedPricePoints()
    {
        var rng = RandomEngine.Create(21);
        var answers = new List<VanWestendorpAnswer>();

        for (int i = 0; i < 300; i++)
        {
            double centre = 1000 * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.25));
            answers.Add(new VanWestendorpAnswer(
                TooCheap: centre * 0.5,
                Cheap: centre * 0.8,
                Expensive: centre * 1.3,
                TooExpensive: centre * 1.8));
        }

        VanWestendorpResult result = WillingnessToPay.VanWestendorp(answers);

        Assert.True(result.PointOfMarginalCheapness < result.PointOfMarginalExpensiveness,
            "Нижняя граница диапазона обязана быть ниже верхней.");
        Assert.InRange(result.OptimalPricePoint,
            result.PointOfMarginalCheapness, result.PointOfMarginalExpensiveness);
        Assert.Equal(300, result.Respondents);
        Assert.NotEmpty(result.Interpret().Findings);
    }

    [Fact]
    public void VanWestendorp_RejectsInconsistentAnswers()
    {
        var answers = new List<VanWestendorpAnswer>
        {
            new(100, 200, 300, 400),
            new(400, 300, 200, 100),   // порядок нарушен
            new(90, 180, 280, 380),
        };

        VanWestendorpResult result = WillingnessToPay.VanWestendorp(answers);

        Assert.Equal(2, result.Respondents);
        Assert.Equal(1, result.InconsistentAnswers);
    }

    [Fact]
    public void GaborGranger_ProfitOptimumIsAboveRevenueOptimum()
    {
        var prices = new Vector(100, 200, 300, 400, 500, 600);
        var acceptance = new Vector(0.95, 0.80, 0.60, 0.40, 0.22, 0.10);

        GaborGrangerResult result = WillingnessToPay.GaborGranger(prices, acceptance, unitCost: 150, respondents: 400);

        Assert.True(result.ProfitOptimalPrice >= result.RevenueOptimalPrice,
            "При положительных издержках оптимум по прибыли не ниже оптимума по выручке.");
        Assert.True(result.ElasticityAtOptimum < 0);
        Assert.NotEmpty(result.Interpret().Warnings);
    }

    /// <summary>Генерирует задания на выбор по известным полезностям.</summary>
    private static (List<ChoiceTask> Tasks, ConjointDesign Design, double[] Truth) ConjointData(
        int respondents, int tasksPer, int seed, double heterogeneity = 0)
    {
        var attributes = new List<ConjointAttribute>
        {
            new("Бренд", ["Базовый", "Известный"]),
            new("Поддержка", ["Почта", "Чат", "Персональный менеджер"]),
            new("Цена", ["1000", "2000", "3000"], [1000, 2000, 3000]),
        };

        var design = new ConjointDesign(attributes);
        double[] truth = [1.2, 0.6, 1.4, -0.0015];

        var rng = RandomEngine.Create(seed);
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

                    // Ошибка Гумбеля даёт в точности логит-модель выбора
                    u += -Math.Log(-Math.Log(Math.Max(rng.NextDouble(), 1e-12)));
                    utilities[j] = u;
                }

                int chosen = 0;
                for (int j = 1; j < 3; j++) if (utilities[j] > utilities[chosen]) chosen = j;

                tasks.Add(new ChoiceTask { Respondent = r, Alternatives = alternatives, ChosenIndex = chosen });
            }
        }

        return (tasks, design, truth);
    }

    [Fact]
    public void Conjoint_MultinomialLogit_RecoversPartWorths()
    {
        (List<ChoiceTask> tasks, ConjointDesign design, double[] truth) = ConjointData(200, 10, 42);

        var model = new MultinomialLogit();
        ConjointResult result = model.Fit(tasks, design);

        for (int a = 0; a < truth.Length; a++)
            Assert.InRange(result.PartWorths[a].Utility, truth[a] - 0.35, truth[a] + 0.35);

        Assert.True(result.PriceCoefficient < 0, "Коэффициент при цене обязан быть отрицательным.");
        Assert.True(result.McFaddenR2 > 0.05);
        Assert.True(result.HitRate > 0.4);
    }

    [Fact]
    public void Conjoint_WillingnessToPay_IsPositiveForValuableLevel()
    {
        (List<ChoiceTask> tasks, ConjointDesign design, _) = ConjointData(200, 10, 42);

        ConjointResult result = new MultinomialLogit().Fit(tasks, design);
        PartWorth brand = result.PartWorths.First(p => p.Name.Contains("Известный"));

        // Полезность бренда 1,2 при цене -0,0015 за рубль даёт около 800 рублей
        Assert.InRange(brand.WillingnessToPay, 300, 1600);
    }

    [Fact]
    public void Conjoint_ShareSimulator_SumsToOne()
    {
        (List<ChoiceTask> tasks, ConjointDesign design, _) = ConjointData(150, 8, 9);

        var model = new MultinomialLogit();
        model.Fit(tasks, design);

        Vector shares = model.SimulateShares(
        [
            new ConjointProfile([1, 2, 0]),
            new ConjointProfile([0, 0, 2]),
            new ConjointProfile([1, 1, 1]),
        ]);

        Assert.Equal(1.0, shares.Sum(), 6);
        Assert.True(shares[0] > shares[1], "Лучшая конфигурация обязана получить большую долю.");
    }

    [Fact]
    public void Conjoint_HierarchicalBayes_DetectsHeterogeneity()
    {
        (List<ChoiceTask> tasks, ConjointDesign design, _) = ConjointData(120, 12, 17, heterogeneity: 0.8);

        var hb = new HierarchicalBayesConjoint();
        ConjointResult result = hb.Fit(tasks, design, draws: 300, burnIn: 200, seed: 5);

        Assert.True(result.IsHierarchical);
        Assert.Equal(120, hb.IndividualUtilities.Count);
        Assert.NotEmpty(result.HeterogeneityStdDev);
        Assert.True(result.HeterogeneityStdDev.Max() > 0.1,
            "На неоднородных данных разброс полезностей обязан быть заметен.");
        Assert.InRange(hb.AcceptanceRate, 0.05, 0.95);

        Vector shares = hb.SimulateShares([new ConjointProfile([1, 2, 0]), new ConjointProfile([0, 0, 2])]);
        Assert.Equal(1.0, shares.Sum(), 6);
    }
}
