using System.Reflection;
using AI.DataStructs.Algebraic;
using AI.Economics.Cohorts;
using AI.Economics.Corporate;
using AI.Economics.Credit;
using AI.Economics.Econometrics;
using AI.Economics.Experiments;
using AI.Economics.Insights;
using AI.Economics.Equity;
using AI.Economics.Forecasting;
using AI.Economics.Market;
using AI.Economics.Marketing;
using AI.Economics.Portfolio;
using AI.Economics.Pricing;
using AI.Economics.Projects;
using AI.Economics.Risk;
using AI.Economics.Runway;
using AI.Economics.Saas;
using AI.Economics.Statements;
using AI.Economics.Survival;
using AI.Economics.UnitEconomics;
using AI.Economics.Valuation;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>
/// Контракт интерпретации: каждый результат обязан объяснять себя словами.
/// </summary>
public class InterpretationTests
{
    /// <summary>
    /// Все публичные типы результатов реализуют <see cref="IInterpretable"/>.
    /// </summary>
    /// <remarks>
    /// Тест защищает от появления нового расчёта без разбора вывода: любой
    /// новый публичный тип с именем на Result или Report должен либо
    /// реализовать интерфейс, либо быть явно исключён здесь.
    /// </remarks>
    [Fact]
    public void EveryResultType_ImplementsInterpretable()
    {
        Assembly assembly = typeof(UnitEconomicsResult).Assembly;

        // Промежуточные структуры, к которым разбор неприменим:
        // они всегда входят в состав другого результата
        string[] exempt =
        [
            nameof(BacktestSummary),
            nameof(CumulativeIncidence),
            nameof(ChannelResult),
            nameof(BanditArmResult),
        ];

        var missing = assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract)
            .Where(t => t.Name.EndsWith("Result") || t.Name.EndsWith("Report"))
            .Where(t => !exempt.Contains(t.Name))
            .Where(t => !typeof(IInterpretable).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(missing.Count == 0,
            "Типы результатов без интерпретации: " + string.Join(", ", missing));
    }

    /// <summary>Собирает по одному экземпляру каждого разбираемого результата.</summary>
    public static TheoryData<string, IInterpretable> AllResults()
    {
        var data = new TheoryData<string, IInterpretable>();

        data.Add("UnitEconomics", UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            MarketingSpend = 500_000, NewCustomers = 200, RevenuePerPeriod = 5000,
            GrossMarginRate = 0.8, ChurnRate = 0.05, DiscountRate = 0.01, Horizon = 36,
        }));

        data.Add("ChannelMix", ChannelEconomics.Analyze(
        [
            new ChannelInput { Name = "Органика", NewCustomers = 100, RevenuePerPeriod = 5000, ChurnRate = 0.03 },
            new ChannelInput { Name = "Платный", Spend = 900_000, NewCustomers = 150,
                RevenuePerPeriod = 5000, ChurnRate = 0.06 },
        ]));

        var survival = new Vector(1.0, 0.6, 0.5, 0.45, 0.42, 0.40, 0.39);
        data.Add("RetentionFit", RetentionFitter.Fit(
            survival, 2000, RetentionModel.ShiftedBetaGeometric, 24, bootstrapSamples: 0));

        var counts = new Matrix(4, 4);
        double[] shape = [1.0, 0.6, 0.5, 0.45];
        for (int c = 0; c < 4; c++)
            for (int t = 0; t < 4; t++) counts[c, t] = Math.Round(500 * shape[t]);
        data.Add("CohortMatrix", CohortMatrix.Triangular(counts));

        var start = new Dictionary<string, double> { ["a"] = 1000, ["b"] = 500 };
        var end = new Dictionary<string, double> { ["a"] = 1200, ["c"] = 400 };
        data.Add("MrrBridge", MrrBridge.Build(start, end));

        data.Add("Runway", RunwaySimulator.Simulate(new RunwayInput
        {
            Cash = 30_000_000, MonthlyRevenue = 3_000_000, RevenueGrowthMean = 0.05,
            RevenueGrowthVolatility = 0.12, MonthlyCosts = 6_000_000, Horizon = 24,
            Simulations = 400, Seed = 3,
        }));

        var table = new CapTable().AddHolding("Основатель", 10_000_000);
        RoundResult round = FundingRound.Execute(table, new RoundInput
        {
            PreMoneyValuation = 400_000_000, Investment = 100_000_000, TargetOptionPoolPost = 0.1,
        });
        data.Add("FundingRound", round);
        data.Add("ExitWaterfall", ExitWaterfall.Distribute(round.CapTable, 800_000_000));

        data.Add("VcMethod", StartupValuation.VcMethod(new VcMethodInput
        {
            Investment = 50_000_000, ExitValueOverride = 2_000_000_000, YearsToExit = 5, TargetIrr = 0.5,
        }));

        data.Add("FirstChicago", StartupValuation.FirstChicago(
        [
            new ValuationScenario("Прорыв", 0.1, 800_000_000),
            new ValuationScenario("База", 0.4, 150_000_000),
            new ValuationScenario("Провал", 0.5, 0),
        ]));

        data.Add("RealOption", RealOptionValuation.Evaluate(new RealOptionInput
        {
            ProjectValue = 150_000_000, InvestmentCost = 200_000_000, YearsToDecision = 3,
            Volatility = 0.6, RiskFreeRate = 0.08, Steps = 60,
        }));

        data.Add("MarketSizing", MarketSizing.Estimate(
            new TopDownInput { TotalMarketValue = 500_000_000_000, GeographyShare = 0.1 },
            new BottomUpInput { TargetAccounts = 10_000, AnnualRevenuePerAccount = 1_000_000 }));

        var bass = new BassDiffusion();
        bass.SetParameters(100_000, 0.02, 0.4);
        bass.Fit(bass.Cumulative(20));
        data.Add("BassDiffusion", bass);

        var records = new List<SurvivalRecord>();
        var rng = new Random(5);
        for (int i = 0; i < 200; i++)
        {
            double x = rng.NextDouble();
            double time = -Math.Log(1 - rng.NextDouble()) / (0.05 * Math.Exp(1.0 * x));
            records.Add(new SurvivalRecord
            {
                Time = Math.Min(time, 24),
                Event = time <= 24,
                Covariates = new Vector(x),
                Cause = time <= 24 ? (i % 2) + 1 : 0,
                Group = i % 2,
            });
        }

        var km = new KaplanMeier();
        km.Fit(records);
        data.Add("KaplanMeier", km);

        var cox = new CoxProportionalHazards();
        cox.Fit(records, ["признак"]);
        data.Add("CoxProportionalHazards", cox);

        var series = new Vector(60);
        for (int t = 0; t < 60; t++) series[t] = 100 + (0.5 * t) + (10 * Math.Sin(2 * Math.PI * t / 12));
        data.Add("Forecast", ExponentialSmoothing.AutoFit(series, 12, 12));
        data.Add("Stl", StlDecomposition.Decompose(series, 12));

        var intermittent = new Vector(40);
        for (int t = 0; t < 40; t++) intermittent[t] = t % 4 == 0 ? 10 : 0;
        data.Add("Intermittent", IntermittentDemand.Fit(intermittent));

        (Matrix summing, IReadOnlyList<HierarchyNode> nodes) =
            HierarchicalReconciliation.BuildTwoLevel([2, 2]);
        var forecasts = new Matrix(nodes.Count, 1);
        double[] values = [900, 300, 500, 140, 150, 240, 250];
        for (int i = 0; i < nodes.Count; i++) forecasts[i, 0] = values[i];
        data.Add("Reconciliation", HierarchicalReconciliation.Reconcile(nodes, summing, forecasts));

        data.Add("Backtest", ForecastBacktest.Run(
            series, [("Theta", (train, h) => ThetaMethod.Fit(train, h, 12))],
            horizon: 6, folds: 3, seasonalPeriod: 12));

        data.Add("Conformal", ConformalPrediction.Calibrate(
            series, (train, h) => ThetaMethod.Fit(train, h, 12), horizon: 4, calibrationFolds: 12));

        AddPricingAndMarketing(data);
        AddCreditAndStatements(data);
        AddEconometrics(data);
        AddCorporateAndProjects(data);
        AddRiskAndPortfolio(data);
        return data;
    }

    /// <summary>Результаты эконометрического движка.</summary>
    private static void AddEconometrics(TheoryData<string, IInterpretable> data)
    {
        var rng = new Random(101);
        const int n = 300;

        var x = new Matrix(n, 2);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            double b = RandomEngine.NextGaussian(rng);

            x[i, 0] = a;
            x[i, 1] = b;
            y[i] = 1 + (2 * a) - (0.5 * b) + RandomEngine.NextGaussian(rng, 0, 0.5);
        }

        data.Add("Regression", LinearRegression.Fit(x, y, ["a", "b"]));
        data.Add("Diagnostics", Diagnostics.Run(x, y, ["a", "b"]));

        var endogenous = new Matrix(n, 1);
        var instruments = new Matrix(n, 1);
        var ivResponse = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double instrument = RandomEngine.NextGaussian(rng);
            double confounder = RandomEngine.NextGaussian(rng);
            double price = (0.9 * instrument) + confounder;

            endogenous[i, 0] = price;
            instruments[i, 0] = instrument;
            ivResponse[i] = 1 - (2 * price) + (1.5 * confounder) + RandomEngine.NextGaussian(rng, 0, 0.3);
        }

        data.Add("InstrumentalVariables",
            InstrumentalVariables.TwoStage(endogenous, null, instruments, ivResponse, ["цена"]));

        PanelDataset panel = BuildPanel(rng);
        PanelResult within = PanelData.Fit(panel, PanelEstimator.FixedEffects);

        data.Add("Panel", within);
        data.Add("Hausman", PanelData.Hausman(within, PanelData.Fit(panel, PanelEstimator.RandomEffects)));
        data.Add("DynamicPanel", DynamicPanel.ArellanoBond(BuildDynamicPanel(rng), maxLags: 2));

        var binaryX = new Matrix(n, 1);
        var binaryY = new Vector(n);
        var countY = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            binaryX[i, 0] = value;
            binaryY[i] = rng.NextDouble() < 1.0 / (1.0 + Math.Exp(-(0.3 + value))) ? 1 : 0;
            countY[i] = RandomEngine.NextPoisson(rng, Math.Exp(0.6 + (0.5 * value)));
        }

        data.Add("Logit", LimitedDependent.Fit(binaryX, binaryY, LimitedDependentModel.Logit, ["x"]));
        data.Add("Poisson", LimitedDependent.Fit(binaryX, countY, LimitedDependentModel.Poisson, ["x"]));

        data.Add("QuantileRegression", QuantileRegression.Fit(x, y, 0.5, ["a", "b"], bootstrapSamples: 20));
        data.Add("QuantileProcess",
            QuantileRegression.FitProcess(x, y, [0.25, 0.5, 0.75], ["a", "b"], bootstrapSamples: 15));

        data.Add("DifferenceInDifferences",
            DifferenceInDifferences.Estimate(BuildDid(rng), bootstrapSamples: 30, seed: 7));

        var rdd = new List<RddObservation>(600);
        for (int i = 0; i < 600; i++)
        {
            double running = (rng.NextDouble() * 4) - 2;
            rdd.Add(new RddObservation(running,
                1 + (0.5 * running) + (running >= 0 ? 2 : 0) + RandomEngine.NextGaussian(rng, 0, 0.3)));
        }

        data.Add("RegressionDiscontinuity", RegressionDiscontinuity.Estimate(rdd));

        var covariates = new Matrix(600, 2);
        var treatment = new Vector(600);
        var outcome = new Vector(600);

        for (int i = 0; i < 600; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            double b = RandomEngine.NextGaussian(rng);

            covariates[i, 0] = a;
            covariates[i, 1] = b;

            bool treated = rng.NextDouble() < 1.0 / (1.0 + Math.Exp(-(0.8 * a)));
            treatment[i] = treated ? 1 : 0;
            outcome[i] = (1.2 * a) + b + (treated ? 1 : 0) + RandomEngine.NextGaussian(rng, 0, 0.5);
        }

        data.Add("Matching",
            PropensityScoreMatching.Estimate(covariates, treatment, outcome, ["a", "b"], 0.25, 3));
        data.Add("CausalForest",
            CausalForest.Fit(covariates, treatment, outcome, ["a", "b"], 40, 20, 3, 5));

        var donors = new Matrix(30, 5);
        var affected = new Vector(30);

        for (int t = 0; t < 30; t++)
        {
            double factor = 10 + (0.3 * t) + RandomEngine.NextGaussian(rng, 0, 0.5);
            for (int j = 0; j < 5; j++) donors[t, j] = factor * (0.9 + (0.05 * j));

            affected[t] = (0.5 * donors[t, 0]) + (0.5 * donors[t, 1]) + (t >= 20 ? 4 : 0);
        }

        data.Add("SyntheticControl", SyntheticControl.Build(affected, donors, null, 20, "регион"));

        var series = new Vector(200);
        double level = 0;
        for (int t = 0; t < 200; t++)
        {
            level = (0.5 * level) + RandomEngine.NextGaussian(rng);
            series[t] = level + 10;
        }

        data.Add("Stationarity", StationarityTests.Analyze(series, name: "ряд"));

        var system = new Matrix(300, 2);
        double first = 0, second = 0;

        for (int t = 0; t < 300; t++)
        {
            double nextFirst = (0.5 * first) + RandomEngine.NextGaussian(rng, 0, 0.5);
            second = (0.3 * second) + (0.6 * first) + RandomEngine.NextGaussian(rng, 0, 0.5);
            first = nextFirst;

            system[t, 0] = first;
            system[t, 1] = second;
        }

        data.Add("Var", VectorAutoregression.Fit(system, 1, ["первая", "вторая"]));

        var cointegrated = new Matrix(300, 2);
        double common = 0;

        for (int t = 0; t < 300; t++)
        {
            common += RandomEngine.NextGaussian(rng);
            cointegrated[t, 0] = common + RandomEngine.NextGaussian(rng, 0, 0.4);
            cointegrated[t, 1] = (2 * common) + RandomEngine.NextGaussian(rng, 0, 0.4);
        }

        data.Add("Johansen", Cointegration.Johansen(cointegrated, 1, ["первый", "второй"]));
        data.Add("Vecm", Cointegration.ErrorCorrection(cointegrated, 1, 1, ["первый", "второй"]));

        var returns = new Vector(800);
        double variance = 0.0004;

        for (int t = 0; t < 800; t++)
        {
            double shock = RandomEngine.NextGaussian(rng) * Math.Sqrt(variance);
            returns[t] = shock;
            variance = 0.00002 + (0.1 * shock * shock) + (0.85 * variance);
        }

        data.Add("Garch", Garch.Fit(returns, GarchModel.Garch, 10));
        data.Add("StateSpace", StateSpace.Fit(series, StateSpaceModel.LocalLevel, 6));
    }

    /// <summary>Результаты корпоративных финансов и проектного анализа.</summary>
    private static void AddCorporateAndProjects(TheoryData<string, IInterpretable> data)
    {
        var rng = new Random(202);

        data.Add("CostOfCapital", CostOfCapital.Compute(new CostOfCapitalInput
        {
            Name = "Компания", EquityValue = 700, DebtValue = 300,
            CountryRiskPremium = 0.02, SizePremium = 0.01,
        }));

        var forecast = new List<ForecastYear>();
        for (int t = 0; t < 5; t++)
            forecast.Add(new ForecastYear(1000 * Math.Pow(1.1, t), 0.2, 0.2, 50, 60, 20));

        var dcfInput = new DcfInput
        {
            Name = "Компания", Forecast = forecast, DiscountRate = 0.16,
            TerminalGrowth = 0.03, NetDebt = 400,
        };

        data.Add("Dcf", DiscountedCashFlow.Value(dcfInput));
        data.Add("DcfSimulation", DiscountedCashFlow.Simulate(dcfInput, simulations: 400, seed: 3));

        var peers = new List<Peer>();
        for (int i = 0; i < 12; i++)
        {
            double revenue = 1000 * (0.5 + rng.NextDouble());
            double ebitda = revenue * (0.15 + (0.1 * rng.NextDouble()));

            peers.Add(new Peer
            {
                Name = $"Аналог {i + 1}", Revenue = revenue, Ebitda = ebitda,
                NetIncome = ebitda * 0.5, EnterpriseValue = ebitda * (6 + (4 * rng.NextDouble())),
                MarketCapitalization = ebitda * 6, Growth = 0.05 + (0.1 * rng.NextDouble()),
            });
        }

        data.Add("Comparables", Comparables.Value(
            new Peer { Name = "Компания", Revenue = 1000, Ebitda = 200, NetIncome = 100, Growth = 0.1 },
            peers, 8));

        data.Add("Lbo", LeveragedBuyout.Run(new LboInput
        {
            Name = "Сделка", EntryEbitda = 100, EntryMultiple = 7, ExitMultiple = 7.5,
            HoldingPeriod = 5, EbitdaGrowth = 0.08,
            Tranches =
            [
                new DebtTranche("Старший", 300, 0.12, 0.1, 1),
                new DebtTranche("Мезонин", 100, 0.16, 0, 2),
            ],
        }));

        data.Add("EconomicProfit", EconomicValueAdded.Compute("Компания",
        [
            new BusinessUnit { Name = "Сильное", Revenue = 1000, OperatingProfit = 200, InvestedCapital = 800 },
            new BusinessUnit { Name = "Слабое", Revenue = 800, OperatingProfit = 40, InvestedCapital = 700 },
        ], 0.15));

        data.Add("RealOptionLsm", LongstaffSchwartz.Value(new ProjectOptionInput
        {
            Name = "Проект", ProjectValue = 100, InvestmentCost = 105,
            Horizon = 3, Volatility = 0.4, Steps = 10, Paths = 2000, Seed = 9,
        }));

        data.Add("InvestmentAppraisal",
            InvestmentCriteria.Appraise(new Vector(-1000, 400, 400, 400, 400), 0.12));

        data.Add("Depreciation",
            Depreciation.Build(1_000_000, 5, DepreciationMethod.SumOfYearsDigits));

        data.Add("LoanSchedule", LoanSchedule.Build(
            1_000_000, 0.15, 24, RepaymentType.Annuity, 12, 0.01,
            [new Prepayment(6, 150_000)]));

        data.Add("LeaseVsBuy", LeaseVsBuy.Compare(new LeaseVsBuyInput { Asset = "Оборудование" }));

        data.Add("BreakEven", BreakEven.Analyze(1000, 600, 2_000_000, 8000, 300_000));
        data.Add("CapitalStructure",
            BreakEven.OptimalStructure("Компания", 0.18, 0.12, 0.2, 200, 0.15));
    }

    /// <summary>Результаты риск-менеджмента и портфельной аналитики.</summary>
    private static void AddRiskAndPortfolio(TheoryData<string, IInterpretable> data)
    {
        var rng = new Random(303);

        var returns = new Vector(1200);
        for (int i = 0; i < returns.Count; i++)
        {
            returns[i] = rng.NextDouble() < 0.95
                ? RandomEngine.NextGaussian(rng, 0.0004, 0.012)
                : RandomEngine.NextGaussian(rng, -0.01, 0.05);
        }

        data.Add("ValueAtRisk", ValueAtRisk.Compute(returns, 1_000_000, 0.99, 1, VarMethod.Historical));
        data.Add("ExtremeValue", ExtremeValue.Fit(returns, 0.95));

        Matrix copulaSample = Copulas.Simulate(CopulaFamily.Clayton, 3.0, 1000, seed: 4);
        var first = new Vector(copulaSample.Height);
        var second = new Vector(copulaSample.Height);

        for (int i = 0; i < copulaSample.Height; i++)
        {
            first[i] = copulaSample[i, 0];
            second[i] = copulaSample[i, 1];
        }

        data.Add("Copula", Copulas.Fit(first, second, CopulaFamily.Clayton, ("A", "B")));

        var forecasts = new Vector(returns.Count);
        for (int i = 0; i < forecasts.Count; i++) forecasts[i] = 0.03;

        data.Add("VarBacktest", VarBacktesting.Backtest(returns, forecasts, 0.99, "модель"));

        data.Add("StressTest", VarBacktesting.StressTest(
            new Vector(1_000_000.0, 500_000.0), new Vector(0.02, 0.03),
            [
                ("Кризис", new Vector(-0.4, -0.25)),
                ("Ставочный шок", new Vector(-0.1, -0.3)),
            ],
            ["акции", "облигации"], 100_000, 200_000));

        data.Add("Liquidity", LiquidityRisk.Analyze(
            50, new Vector(100.0, 80, 60, 120, 140, 100), new Vector(90.0, 110, 130, 90, 80, 95),
            simulations: 500, seed: 11));

        const int months = 120, assets = 3;
        var assetReturns = new Matrix(months, assets);
        var benchmark = new Vector(months);

        for (int t = 0; t < months; t++)
        {
            double market = RandomEngine.NextGaussian(rng, 0.007, 0.04);
            benchmark[t] = market;

            assetReturns[t, 0] = (0.4 * market) + RandomEngine.NextGaussian(rng, 0.002, 0.01);
            assetReturns[t, 1] = (1.1 * market) + RandomEngine.NextGaussian(rng, 0.001, 0.02);
            assetReturns[t, 2] = (0.8 * market) + RandomEngine.NextGaussian(rng, 0.003, 0.03);
        }

        var weights = new Vector(0.4, 0.4, 0.2);
        Vector portfolio = PortfolioMetrics.PortfolioReturns(weights, assetReturns);

        data.Add("PortfolioMetrics", PortfolioMetrics.Compute(portfolio, benchmark, 0.003, 12, 0, "портфель"));

        Matrix covariance = MeanVariance.Covariance(assetReturns);
        var expected = new Vector(0.06, 0.10, 0.08);

        data.Add("MeanVariance", MeanVariance.Optimize(
            expected, covariance, ["облигации", "акции", "сырьё"], 0.04,
            new PortfolioConstraints { MaximumWeight = 0.7 }));

        data.Add("RiskParity", RiskParity.Build(
            covariance, ["облигации", "акции", "сырьё"], RiskParityMethod.EqualRiskContribution));

        data.Add("BlackLitterman", BlackLitterman.Blend(
            new Vector(0.5, 0.3, 0.2), covariance,
            [BlackLitterman.Relative(3, 1, 0, 0.02, 0.5, "акции обгонят облигации")],
            ["облигации", "акции", "сырьё"]));

        data.Add("CvarOptimization", CvarOptimization.Optimize(
            assetReturns, ["облигации", "акции", "сырьё"], 0.95));

        var factors = new Matrix(months, 2);
        var excess = new Vector(months);

        for (int t = 0; t < months; t++)
        {
            factors[t, 0] = benchmark[t];
            factors[t, 1] = RandomEngine.NextGaussian(rng, 0.002, 0.02);
            excess[t] = 0.001 + (1.1 * factors[t, 0]) + (0.3 * factors[t, 1])
                + RandomEngine.NextGaussian(rng, 0, 0.008);
        }

        data.Add("FactorModel", FactorModels.Fit(excess, factors, ["рынок", "размер"], 12, "фонд"));

        data.Add("Attribution", FactorModels.BrinsonAttribution(
            ["акции", "облигации", "деньги"],
            new Vector(0.5, 0.3, 0.2), new Vector(0.4, 0.4, 0.2),
            new Vector(0.10, 0.05, 0.02), new Vector(0.08, 0.06, 0.03)));

        data.Add("Rebalancing", Rebalancing.Simulate(assetReturns, weights, RebalancingRule.Threshold));
    }

    /// <summary>Панель с коррелированными индивидуальными эффектами.</summary>
    private static PanelDataset BuildPanel(Random rng)
    {
        const int units = 40, periods = 6;
        int n = units * periods;

        var x = new Matrix(n, 1);
        var y = new Vector(n);
        var unitIds = new List<int>(n);
        var periodIds = new List<int>(n);

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, 1.5);

            for (int t = 0; t < periods; t++)
            {
                int i = (u * periods) + t;
                double value = (0.7 * effect) + RandomEngine.NextGaussian(rng);

                x[i, 0] = value;
                y[i] = value + effect + RandomEngine.NextGaussian(rng, 0, 0.4);

                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        return new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };
    }

    /// <summary>Панель с лагом отклика для разностного метода моментов.</summary>
    private static PanelDataset BuildDynamicPanel(Random rng)
    {
        const int units = 60, periods = 7;
        var xs = new List<double>();
        var ys = new List<double>();
        var unitIds = new List<int>();
        var periodIds = new List<int>();

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, 0.5);
            double level = effect * 2;

            for (int t = 0; t < periods; t++)
            {
                double regressor = RandomEngine.NextGaussian(rng);
                level = (0.5 * level) + (0.4 * regressor) + effect + RandomEngine.NextGaussian(rng, 0, 0.3);

                xs.Add(regressor);
                ys.Add(level);
                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        var x = new Matrix(xs.Count, 1);
        var y = new Vector(ys.Count);

        for (int i = 0; i < xs.Count; i++) { x[i, 0] = xs[i]; y[i] = ys[i]; }

        return new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };
    }

    /// <summary>Панель для разности разностей с разновременным внедрением.</summary>
    private static List<DidObservation> BuildDid(Random rng)
    {
        var observations = new List<DidObservation>();

        for (int u = 0; u < 40; u++)
        {
            int first = u % 3 == 0 ? 0 : u % 3 == 1 ? 4 : 6;
            double level = RandomEngine.NextGaussian(rng, 10, 2);

            for (int t = 1; t <= 8; t++)
            {
                double outcome = level + (0.5 * t) + RandomEngine.NextGaussian(rng, 0, 0.4);
                if (first > 0 && t >= first) outcome += 3;

                observations.Add(new DidObservation(u, t, outcome, first));
            }
        }

        return observations;
    }

    /// <summary>Результаты блоков кредитного риска и анализа отчётности.</summary>
    private static void AddCreditAndStatements(TheoryData<string, IInterpretable> data)
    {
        (Matrix applications, List<bool> defaults) = EconomicsSamples.Applications(1500, seed: 17);

        var scorecard = new Scorecard();
        data.Add("Scorecard", scorecard.Fit(EconomicsSamples.ScoreVariables, applications, defaults));

        var expected = new Vector(1000);
        var actual = new Vector(1000);
        var psiRng = new Random(23);
        for (int i = 0; i < 1000; i++)
        {
            expected[i] = RandomEngine.NextGaussian(psiRng, 600, 50);
            actual[i] = RandomEngine.NextGaussian(psiRng, 560, 55);
        }
        data.Add("Psi", ScoreMetrics.PopulationStability(expected, actual));

        data.Add("Ifrs9", Ifrs9.Compute(EconomicsSamples.Portfolio()));

        MigrationMatrixResult migration = MigrationMatrix.Estimate(
            EconomicsSamples.Ratings, EconomicsSamples.Transitions());
        data.Add("MigrationMatrix", migration);

        data.Add("RollRate", RollRate.Analyze(
            RollRate.DefaultBuckets(), EconomicsSamples.DelinquencyBalances()));

        data.Add("Vintage", VintageAnalysis.Analyze(EconomicsSamples.Vintages()));
        data.Add("Merton", MertonModel.Estimate(EconomicsSamples.PublicCompany()));
        data.Add("Counterparty", CounterpartyScoring.Score(EconomicsSamples.Counterparty(false)));

        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();

        data.Add("Ratios", FinancialRatios.Compute(current, previous));
        data.Add("DuPont", DuPontAnalysis.Analyze(current, previous));
        data.Add("Distress", DistressScores.Evaluate(current, previous));
        data.Add("Beneish", BeneishModel.Compute(current, previous));
        data.Add("Benford", BenfordAnalysis.Analyze(EconomicsSamples.FabricatedPayments()));
        data.Add("WorkingCapital", WorkingCapitalAnalysis.Analyze(current));
        data.Add("EarningsQuality", EarningsQuality.Evaluate(current, previous));

        var predictor = new BankruptcyPredictor();
        data.Add("BankruptcyModel", predictor.Train(EconomicsSamples.BankruptcySample(200)));
        data.Add("BankruptcyPrediction", predictor.Predict(current));
    }

    /// <summary>Результаты блоков ценообразования, маркетинга и экспериментов.</summary>
    private static void AddPricingAndMarketing(TheoryData<string, IInterpretable> data)
    {
        var rng = new Random(3);

        var observations = new List<PriceObservation>(300);
        for (int i = 0; i < 300; i++)
        {
            double cost = Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.3));
            double shock = RandomEngine.NextGaussian(rng, 0, 0.4);
            double price = Math.Exp(2 + (0.7 * Math.Log(cost)) + (0.6 * shock));
            double quantity = Math.Exp(6 - (1.7 * Math.Log(price)) + shock);

            observations.Add(new PriceObservation
            {
                Price = price, Quantity = quantity, Instrument = cost, Unit = i % 4, Period = i / 4,
            });
        }

        data.Add("Elasticity", DemandElasticity.Estimate(observations, ElasticityEstimator.InstrumentalVariables));

        var products = new[]
        {
            new ProductPricing { Name = "Базовый", CurrentPrice = 1000, CurrentQuantity = 5000, UnitCost = 400 },
            new ProductPricing { Name = "Премиум", CurrentPrice = 2000, CurrentQuantity = 1500, UnitCost = 800 },
        };
        var elasticities = new Matrix(2, 2);
        elasticities[0, 0] = -2.0; elasticities[0, 1] = 0.5;
        elasticities[1, 0] = 0.4; elasticities[1, 1] = -2.4;
        data.Add("PriceOptimization", PriceOptimizer.Optimize(products, elasticities));

        var answers = new List<VanWestendorpAnswer>(200);
        for (int i = 0; i < 200; i++)
        {
            double centre = 1000 * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.25));
            answers.Add(new VanWestendorpAnswer(centre * 0.5, centre * 0.8, centre * 1.3, centre * 1.8));
        }
        data.Add("VanWestendorp", WillingnessToPay.VanWestendorp(answers));

        data.Add("GaborGranger", WillingnessToPay.GaborGranger(
            new Vector(100.0, 200, 300, 400, 500),
            new Vector(0.9, 0.75, 0.5, 0.3, 0.15),
            unitCost: 120, respondents: 200));

        var design = new ConjointDesign(
        [
            new ConjointAttribute("Бренд", ["Базовый", "Известный"]),
            new ConjointAttribute("Цена", ["1000", "2000"], [1000, 2000]),
        ]);

        var tasks = new List<ChoiceTask>(600);
        for (int r = 0; r < 60; r++)
        {
            for (int t = 0; t < 10; t++)
            {
                var alternatives = new List<ConjointProfile>
                {
                    new([rng.Next(2), rng.Next(2)]),
                    new([rng.Next(2), rng.Next(2)]),
                };
                tasks.Add(new ChoiceTask
                {
                    Respondent = r, Alternatives = alternatives, ChosenIndex = rng.Next(2),
                });
            }
        }

        // Агрегатная модель не оценивает гетерогенность: её разбор не должен
        // обращаться к пустому набору стандартных отклонений
        data.Add("ConjointMnl", new MultinomialLogit().Fit(tasks, design));
        data.Add("ConjointHb", new HierarchicalBayesConjoint().Fit(tasks, design, draws: 100, burnIn: 100));

        var mmm = BuildMmm();
        MmmResult mmmResult = MarketingMixModel.Fit(mmm);
        data.Add("Mmm", mmmResult);
        data.Add("BudgetAllocation", BudgetOptimizer.Allocate(mmmResult));

        var uplift = new List<UpliftObservation>(3000);
        for (int i = 0; i < 3000; i++)
        {
            double sensitivity = rng.NextDouble();
            bool treated = rng.NextDouble() < 0.5;
            double rate = Math.Clamp(0.15 + (treated ? 0.25 * sensitivity : 0), 0.01, 0.95);

            uplift.Add(new UpliftObservation
            {
                Features = new Vector(sensitivity, rng.NextDouble()),
                Treated = treated,
                Converted = rng.NextDouble() < rate,
            });
        }
        data.Add("Uplift", UpliftModeling.Fit(uplift, 30, 300));

        data.Add("SampleSize", ExperimentDesign.ForProportions(0.05, 0.1, dailyTraffic: 2000));

        var pre = new Vector(500);
        var post = new Vector(500);
        var prePlus = new Vector(500);
        var postPlus = new Vector(500);
        for (int i = 0; i < 500; i++)
        {
            double levelA = RandomEngine.NextGaussian(rng, 10, 3);
            double levelB = RandomEngine.NextGaussian(rng, 10, 3);
            pre[i] = levelA + RandomEngine.NextGaussian(rng, 0, 1);
            post[i] = levelA + RandomEngine.NextGaussian(rng, 0, 1);
            prePlus[i] = levelB + RandomEngine.NextGaussian(rng, 0, 1);
            postPlus[i] = levelB + 0.4 + RandomEngine.NextGaussian(rng, 0, 1);
        }
        data.Add("Cuped", Cuped.Apply(pre, post, prePlus, postPlus));

        var control = new Vector(1000);
        var treatment = new Vector(1000);
        for (int i = 0; i < 1000; i++)
        {
            control[i] = rng.NextDouble() < 0.10 ? 1 : 0;
            treatment[i] = rng.NextDouble() < 0.14 ? 1 : 0;
        }
        data.Add("SequentialTest", SequentialTest.Run(control, treatment));
        data.Add("BayesianAb", SequentialTest.Bayesian(100, 1000, 140, 1000, draws: 5000));

        data.Add("Bandit", Bandits.Simulate(
            ["A", "B", "C"], new Vector(0.05, 0.08, 0.12),
            BanditPolicy.ThompsonSampling, rounds: 3000, seed: 5));
    }

    private static MmmInput BuildMmm()
    {
        var rng = new Random(11);
        const int weeks = 120;

        var tv = new Vector(weeks);
        var digital = new Vector(weeks);
        var sales = new Vector(weeks);
        double tvCarry = 0, digitalCarry = 0;

        for (int t = 0; t < weeks; t++)
        {
            tv[t] = t % 8 < 3 ? 800_000 * (0.7 + (rng.NextDouble() * 0.6)) : 0;
            digital[t] = 300_000 * (0.6 + (rng.NextDouble() * 0.8));

            tvCarry = tv[t] + (0.6 * tvCarry);
            digitalCarry = digital[t] + (0.2 * digitalCarry);

            sales[t] = 8_000_000 + (12_000 * t)
                     + (4_000_000 * MarketingMixModel.Hill(tvCarry, 1_500_000, 1.5))
                     + (2_000_000 * MarketingMixModel.Hill(digitalCarry, 400_000, 1.2))
                     + RandomEngine.NextGaussian(rng, 0, 250_000);
        }

        return new MmmInput
        {
            Sales = sales,
            Channels =
            [
                new MediaChannel { Name = "ТВ", Spend = tv },
                new MediaChannel { Name = "Digital", Spend = digital },
            ],
            SeasonalPeriod = 52,
            FourierTerms = 1,
            Ridge = 1e-4,
            MarginRate = 0.4,
            TuningIterations = 600,
        };
    }

    [Theory]
    [MemberData(nameof(AllResults))]
    public void Interpretation_IsCompleteAndReadable(string name, IInterpretable result)
    {
        Interpretation interpretation = result.Interpret();

        Assert.False(string.IsNullOrWhiteSpace(interpretation.Title), $"{name}: пустой заголовок.");
        Assert.False(string.IsNullOrWhiteSpace(interpretation.Summary), $"{name}: пустой итог.");
        Assert.NotEmpty(interpretation.Metrics);

        foreach (InterpretedMetric metric in interpretation.Metrics)
        {
            Assert.False(string.IsNullOrWhiteSpace(metric.Name), $"{name}: метрика без названия.");
            Assert.False(string.IsNullOrWhiteSpace(metric.Value), $"{name}: метрика «{metric.Name}» без значения.");
            Assert.DoesNotContain("NaN", metric.Value);
            Assert.DoesNotContain("∞", metric.Value);
            Assert.DoesNotContain("Infinity", metric.Value);
        }

        // Каждый метод обязан честно называть границы применимости
        Assert.NotEmpty(interpretation.Warnings);

        string text = interpretation.ToLlmText();
        Assert.Contains(interpretation.Title, text);
        Assert.Contains("Метрики:", text);
        Assert.DoesNotContain("NaN", text);
        Assert.True(text.Length > 200, $"{name}: разбор слишком короткий для полезного вывода.");
    }

    [Fact]
    public void Interpretation_ListResults_HaveExtensionInterpreters()
    {
        var records = new List<SurvivalRecord>();
        var rng = new Random(9);
        for (int i = 0; i < 300; i++)
        {
            double time = -Math.Log(1 - rng.NextDouble()) / 0.06;
            bool observed = time <= 24;
            records.Add(new SurvivalRecord
            {
                Time = observed ? time : 24,
                Event = observed,
                Cause = observed ? (i % 3) + 1 : 0,
            });
        }

        Interpretation risks = CompetingRisks.Analyze(records).Interpret();
        Assert.Contains("конкурир", risks.Title.ToLowerInvariant());
        Assert.NotEmpty(risks.Metrics);

        Interpretation saas = SaasMetrics.Evaluate(new SaasHealthInput
        {
            ArrStart = 100_000_000, ArrEnd = 160_000_000, ArrYearAgo = 100_000_000,
            SalesAndMarketing = 40_000_000, NetBurn = 50_000_000, FreeCashFlowMargin = -0.3,
            GrossMarginRate = 0.8, ArpaMonthly = 50_000, Cac = 250_000,
        }).Interpret();

        Assert.NotEmpty(saas.Metrics);
        Assert.NotEmpty(saas.Warnings);
    }

    [Fact]
    public void Interpretation_TextIsStructuredForLanguageModels()
    {
        UnitEconomicsResult result = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            CacOverride = 5000, RevenuePerPeriod = 1000, GrossMarginRate = 0.8,
            ChurnRate = 0.04, DiscountRate = 0.01, Horizon = 36,
        });

        string text = result.Interpret().ToLlmText();

        Assert.StartsWith("### ", text);
        Assert.Contains("Метрики:", text);
        Assert.Contains("Предупреждения:", text);
        Assert.Contains("Рекомендации:", text);

        // Оценки метрик подписаны словами, а не кодами перечисления
        Assert.Contains("[норма]", text);
    }
}
