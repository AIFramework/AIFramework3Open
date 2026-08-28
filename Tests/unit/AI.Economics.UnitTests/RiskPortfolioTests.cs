using AI.DataStructs.Algebraic;
using AI.Economics.Portfolio;
using AI.Economics.Risk;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>Тесты риск-менеджмента и портфельной аналитики.</summary>
public class RiskPortfolioTests
{
    [Fact]
    public void ValueAtRisk_Parametric_MatchesNormalQuantile()
    {
        Random rng = RandomEngine.Create(1);
        var returns = new Vector(4000);

        for (int i = 0; i < returns.Count; i++) returns[i] = RandomEngine.NextGaussian(rng, 0, 0.02);

        VarResultSet result = ValueAtRisk.Compute(
            returns, portfolioValue: 1_000_000, confidence: 0.99, method: VarMethod.Parametric);

        // Для нормального закона квантиль 99% равен 2,326 стандартного отклонения
        Assert.InRange(result.ValueAtRisk, 0.99 * 2.326 * 0.02, 1.01 * 2.326 * 0.02);
        Assert.Equal(result.ValueAtRisk * 1_000_000, result.ValueAtRiskAmount, 6);
        Assert.True(result.ExpectedShortfall > result.ValueAtRisk,
            "Ожидаемые потери в хвосте всегда превышают порог.");
        Assert.Equal(4, result.Comparison.Count);
    }

    [Fact]
    public void ValueAtRisk_FatTails_RaiseHistoricalAboveParametric()
    {
        Random rng = RandomEngine.Create(2);
        var returns = new Vector(4000);

        for (int i = 0; i < returns.Count; i++)
        {
            // Смесь распределений даёт тяжёлые хвосты при той же дисперсии
            returns[i] = rng.NextDouble() < 0.95
                ? RandomEngine.NextGaussian(rng, 0, 0.01)
                : RandomEngine.NextGaussian(rng, 0, 0.06);
        }

        VarResultSet result = ValueAtRisk.Compute(returns, confidence: 0.99);

        double historical = result.Comparison.First(c => c.Method == VarMethod.Historical).Var;
        double parametric = result.Comparison.First(c => c.Method == VarMethod.Parametric).Var;

        Assert.True(result.Kurtosis > 3.5, $"Эксцесс {result.Kurtosis:F2} должен указывать на тяжёлые хвосты.");
        Assert.True(historical > parametric,
            "При тяжёлых хвостах исторический метод обязан давать более высокую оценку.");
    }

    [Fact]
    public void ExtremeValue_Fit_RecoversHeavyTail()
    {
        Random rng = RandomEngine.Create(3);
        var returns = new Vector(3000);

        // Убытки со степенным хвостом: параметр формы должен получиться положительным
        for (int i = 0; i < returns.Count; i++)
        {
            double uniform = Math.Max(rng.NextDouble(), 1e-9);
            returns[i] = -0.01 * (Math.Pow(uniform, -0.25) - 1);
        }

        ExtremeValueResult result = ExtremeValue.Fit(returns, thresholdQuantile: 0.95);

        Assert.True(result.Shape > 0.05, $"Параметр формы {result.Shape:F3} должен быть положительным.");
        Assert.True(result.Exceedances > 100);
        Assert.Equal(3, result.TailQuantiles.Count);

        // Квантили монотонно растут с уровнем доверия
        for (int i = 1; i < result.TailQuantiles.Count; i++)
            Assert.True(result.TailQuantiles[i].ValueAtRisk > result.TailQuantiles[i - 1].ValueAtRisk);

        Assert.NotEmpty(ExtremeValue.MeanExcessPlot(returns));
    }

    [Fact]
    public void Copulas_Fit_DetectsLowerTailDependence()
    {
        Matrix sample = Copulas.Simulate(CopulaFamily.Clayton, 4.0, 3000, seed: 4);

        var first = new Vector(sample.Height);
        var second = new Vector(sample.Height);

        for (int i = 0; i < sample.Height; i++)
        {
            first[i] = sample[i, 0];
            second[i] = sample[i, 1];
        }

        CopulaResult clayton = Copulas.Fit(first, second, CopulaFamily.Clayton, ("A", "B"));
        CopulaResult gaussian = Copulas.Fit(first, second, CopulaFamily.Gaussian, ("A", "B"));

        Assert.True(clayton.KendallTau > 0.4, $"Ранговая корреляция {clayton.KendallTau:F3} слишком мала.");
        Assert.True(clayton.LowerTailDependence > 0.5,
            "Копула Клейтона обязана показывать связь нижних хвостов.");
        Assert.Equal(0, gaussian.LowerTailDependence, 8);
        Assert.True(clayton.EmpiricalLowerTail > 0.3);
        Assert.Equal(4, clayton.Comparison.Count);
    }

    [Fact]
    public void Copulas_KendallTau_MatchesDefinition()
    {
        var first = new Vector(1.0, 2.0, 3.0, 4.0);
        var second = new Vector(1.0, 3.0, 2.0, 4.0);

        // Из шести пар пять согласованы и одна нет
        Assert.Equal((5.0 - 1.0) / 6.0, Copulas.KendallTau(first, second), 8);
    }

    [Fact]
    public void VarBacktesting_Backtest_AcceptsCorrectModelAndRejectsBad()
    {
        Random rng = RandomEngine.Create(5);
        const int n = 1000;

        var returns = new Vector(n);
        var honest = new Vector(n);
        var optimistic = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            returns[i] = RandomEngine.NextGaussian(rng, 0, 0.02);
            honest[i] = 2.326 * 0.02;
            optimistic[i] = 1.0 * 0.02;
        }

        BacktestVarResult good = VarBacktesting.Backtest(returns, honest, 0.99, "верная");
        BacktestVarResult bad = VarBacktesting.Backtest(returns, optimistic, 0.99, "заниженная");

        Assert.True(good.KupiecPValue > 0.05,
            $"Верная модель отвергнута тестом Купца, p = {good.KupiecPValue:F4}.");
        Assert.Equal("зелёная", good.TrafficLight);

        Assert.True(bad.Exceptions > good.Exceptions * 3);
        Assert.True(bad.KupiecPValue < 0.01, "Заниженная модель обязана отвергаться.");
        Assert.Equal("красная", bad.TrafficLight);
    }

    [Fact]
    public void VarBacktesting_StressTest_FindsWorstScenarioAndReverseShocks()
    {
        var exposures = new Vector(1_000_000.0, 500_000.0);
        var volatility = new Vector(0.02, 0.03);

        var scenarios = new List<(string, Vector)>
        {
            ("Кризис 2008", new Vector(-0.4, -0.25)),
            ("Ставочный шок", new Vector(-0.1, -0.3)),
            ("Мягкий спад", new Vector(-0.05, -0.05)),
        };

        StressTestResult result = VarBacktesting.StressTest(
            exposures, volatility, scenarios, ["акции", "облигации"],
            valueAtRisk: 100_000, reverseTarget: 200_000);

        Assert.Equal("Кризис 2008", result.Scenarios[0].Name);
        Assert.Equal(525_000, result.WorstLoss, 3);
        Assert.Equal(2, result.ReverseStressShocks.Count);
        Assert.True(result.ReverseStressShocks.All(s => s < 0),
            "Обратный сценарий должен состоять из отрицательных шоков.");
        Assert.True(result.ReverseStressDistance > 0);
    }

    [Fact]
    public void LiquidityRisk_Analyze_FindsCashGapAndOptimalBalances()
    {
        var inflows = new Vector(100.0, 80, 60, 120, 140, 100);
        var outflows = new Vector(90.0, 110, 130, 90, 80, 95);

        LiquidityResult result = LiquidityRisk.Analyze(
            openingBalance: 50, inflows, outflows,
            inflowVolatility: 0.2, transactionCost: 1, interestRate: 0.02,
            simulations: 2000, seed: 7);

        Assert.Equal(6, result.Positions.Count);
        Assert.True(result.MinimumBalance < 50, "Остаток обязан просесть в середине горизонта.");
        Assert.InRange(result.ShortfallProbability, 0, 1);
        Assert.True(result.BaumolCash > 0);
        Assert.True(result.MillerOrrUpper > result.MillerOrrReturn);
        Assert.True(result.MillerOrrReturn > result.MillerOrrLower);
    }

    [Fact]
    public void LiquidityRisk_MillerOrr_MatchesFormula()
    {
        (double lower, double returnPoint, double upper) =
            LiquidityRisk.MillerOrr(minimumBalance: 100, dailyVariance: 400, transactionCost: 50, dailyRate: 0.0002);

        double spread = Math.Pow(3 * 50 * 400 / (4 * 0.0002), 1.0 / 3.0);

        Assert.Equal(100, lower, 8);
        Assert.Equal(100 + spread, returnPoint, 6);
        Assert.Equal((3 * returnPoint) - 200, upper, 6);
    }

    [Fact]
    public void PortfolioMetrics_Compute_MatchesKnownRelations()
    {
        Random rng = RandomEngine.Create(8);
        const int n = 120;

        var returns = new Vector(n);
        var benchmark = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double market = RandomEngine.NextGaussian(rng, 0.008, 0.04);
            benchmark[i] = market;
            returns[i] = 0.002 + (1.2 * market) + RandomEngine.NextGaussian(rng, 0, 0.01);
        }

        PerformanceMetrics metrics = PortfolioMetrics.Compute(returns, benchmark, 0.003, 12);

        Assert.True(metrics.HasBenchmark);
        Assert.InRange(metrics.Beta, 1.05, 1.35);
        Assert.True(metrics.MaxDrawdown > 0);
        Assert.InRange(metrics.HitRate, 0, 1);
        Assert.True(metrics.TrackingError > 0);
        Assert.Equal(n, metrics.Drawdowns.Count);
    }

    [Fact]
    public void PortfolioMetrics_Drawdown_MatchesManualCalculation()
    {
        var returns = new Vector(0.1, -0.2, -0.1, 0.05, 0.4);

        (Vector drawdowns, double maxDrawdown, int length, int recovery) =
            PortfolioMetrics.DrawdownProfile(returns);

        // Пик 1,1; минимум 1,1 * 0,8 * 0,9 = 0,792
        Assert.Equal(1 - (0.792 / 1.1), maxDrawdown, 6);
        Assert.Equal(2, length);
        Assert.Equal(5, drawdowns.Count);
        Assert.True(recovery > 0, "После просадки портфель восстанавливается на последнем шаге.");
    }

    [Fact]
    public void MeanVariance_Optimize_BuildsFrontierAndRespectsConstraints()
    {
        var expected = new Vector(0.08, 0.12, 0.15);
        var covariance = new Matrix(3, 3);

        double[,] values =
        {
            { 0.0100, 0.0020, 0.0010 },
            { 0.0020, 0.0225, 0.0030 },
            { 0.0010, 0.0030, 0.0400 },
        };

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++) covariance[i, j] = values[i, j];

        OptimizationResult result = MeanVariance.Optimize(
            expected, covariance, ["облигации", "акции", "венчур"],
            riskFreeRate: 0.05,
            constraints: new PortfolioConstraints { MaximumWeight = 0.6 });

        Assert.Equal(1.0, result.Weights.Sum(), 6);
        Assert.All(result.Weights, w => Assert.InRange(w, -1e-6, 0.6 + 1e-6));
        Assert.NotEmpty(result.Frontier);
        Assert.NotNull(result.MinimumVariance);

        // Портфель минимального риска не может быть рискованнее любого на границе
        Assert.True(result.MinimumVariance!.Risk <= result.Frontier.Min(p => p.Risk) + 1e-6);
        Assert.Equal(1.0, result.RiskBudget.Sum(r => r.RiskContribution), 6);
    }

    [Fact]
    public void RiskParity_EqualRiskContribution_EqualisesRiskShares()
    {
        var covariance = new Matrix(3, 3);

        double[,] values =
        {
            { 0.0100, 0.0020, 0.0005 },
            { 0.0020, 0.0400, 0.0010 },
            { 0.0005, 0.0010, 0.0900 },
        };

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++) covariance[i, j] = values[i, j];

        RiskParityResult parity = RiskParity.Build(
            covariance, ["A", "B", "C"], RiskParityMethod.EqualRiskContribution);

        Assert.Equal(1.0, parity.Weights.Sum(), 6);
        Assert.True(parity.MaximumDeviation < 0.01,
            $"Вклады в риск разошлись на {parity.MaximumDeviation:F4}.");

        // Наименее волатильный актив получает наибольший вес
        Assert.True(parity.Weights[0] > parity.Weights[1]);
        Assert.True(parity.Weights[1] > parity.Weights[2]);
        Assert.True(parity.DiversificationRatio > 1);
    }

    [Fact]
    public void RiskParity_Hierarchical_ProducesValidWeights()
    {
        Random rng = RandomEngine.Create(9);
        const int n = 6, t = 300;
        var returns = new Matrix(t, n);

        for (int i = 0; i < t; i++)
        {
            double clusterA = RandomEngine.NextGaussian(rng, 0, 0.02);
            double clusterB = RandomEngine.NextGaussian(rng, 0, 0.03);

            for (int j = 0; j < 3; j++) returns[i, j] = clusterA + RandomEngine.NextGaussian(rng, 0, 0.005);
            for (int j = 3; j < 6; j++) returns[i, j] = clusterB + RandomEngine.NextGaussian(rng, 0, 0.005);
        }

        Matrix covariance = MeanVariance.Covariance(returns);
        RiskParityResult parity = RiskParity.Build(
            covariance, null, RiskParityMethod.HierarchicalRiskParity);

        Assert.Equal(1.0, parity.Weights.Sum(), 6);
        Assert.All(parity.Weights, w => Assert.True(w >= 0));
        Assert.NotEmpty(parity.Clusters);
        Assert.True(parity.EffectiveAssets > 2);
    }

    [Fact]
    public void BlackLitterman_WithoutViews_ReproducesMarketPortfolio()
    {
        var marketWeights = new Vector(0.6, 0.3, 0.1);
        var covariance = new Matrix(3, 3);

        double[,] values =
        {
            { 0.0100, 0.0020, 0.0010 },
            { 0.0020, 0.0225, 0.0030 },
            { 0.0010, 0.0030, 0.0400 },
        };

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++) covariance[i, j] = values[i, j];

        BlackLittermanResult result = BlackLitterman.Blend(marketWeights, covariance);

        for (int i = 0; i < 3; i++)
            Assert.Equal(marketWeights[i], result.OptimalWeights[i], 8);

        Assert.Equal(0, result.ActiveShare, 8);
    }

    [Fact]
    public void BlackLitterman_WithView_ShiftsWeightsTowardFavouredAsset()
    {
        var marketWeights = new Vector(0.6, 0.3, 0.1);
        var covariance = new Matrix(3, 3);

        double[,] values =
        {
            { 0.0100, 0.0020, 0.0010 },
            { 0.0020, 0.0225, 0.0030 },
            { 0.0010, 0.0030, 0.0400 },
        };

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++) covariance[i, j] = values[i, j];

        var views = new List<InvestorView>
        {
            BlackLitterman.Relative(3, 1, 0, 0.03, 0.6, "акции обгонят облигации на 3%"),
        };

        BlackLittermanResult result = BlackLitterman.Blend(marketWeights, covariance, views);

        Assert.True(result.PosteriorReturns[1] > result.ImpliedReturns[1],
            "Взгляд обязан поднять ожидаемую доходность фаворита.");
        Assert.True(result.OptimalWeights[1] > marketWeights[1],
            "И увеличить его вес относительно рыночного.");
        Assert.True(result.ActiveShare > 0);
    }

    [Fact]
    public void CvarOptimization_Optimize_ReducesTailLossVersusMinimumVariance()
    {
        Random rng = RandomEngine.Create(10);
        const int t = 1500, n = 3;
        var scenarios = new Matrix(t, n);

        for (int s = 0; s < t; s++)
        {
            // Третий актив редко, но глубоко падает: дисперсия этого не отражает
            bool crash = rng.NextDouble() < 0.03;

            scenarios[s, 0] = RandomEngine.NextGaussian(rng, 0.004, 0.02);
            scenarios[s, 1] = RandomEngine.NextGaussian(rng, 0.006, 0.025);
            scenarios[s, 2] = crash
                ? RandomEngine.NextGaussian(rng, -0.15, 0.03)
                : RandomEngine.NextGaussian(rng, 0.012, 0.02);
        }

        CvarOptimizationResult result = CvarOptimization.Optimize(
            scenarios, ["A", "B", "хвостовой"], confidence: 0.95);

        Assert.Equal(1.0, result.Weights.Sum(), 4);
        Assert.All(result.Weights, w => Assert.True(w >= -1e-6));
        Assert.True(result.ConditionalValueAtRisk <= result.MeanVarianceCvar + 1e-6,
            "Оптимизация по хвосту не может дать худший хвост, чем минимизация дисперсии.");
        Assert.True(result.TailScenarios > 30);
    }

    [Fact]
    public void FactorModels_Fit_RecoversLoadingsAndAlpha()
    {
        Random rng = RandomEngine.Create(11);
        const int n = 240;

        var factors = new Matrix(n, 2);
        var excess = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double market = RandomEngine.NextGaussian(rng, 0.006, 0.04);
            double size = RandomEngine.NextGaussian(rng, 0.002, 0.02);

            factors[i, 0] = market;
            factors[i, 1] = size;
            excess[i] = 0.001 + (1.1 * market) + (0.4 * size) + RandomEngine.NextGaussian(rng, 0, 0.008);
        }

        FactorModelResult model = FactorModels.Fit(excess, factors, ["рынок", "размер"], 12, "фонд");

        Assert.Equal(1.1, model.Loadings[0].Loading, 1);
        Assert.Equal(0.4, model.Loadings[1].Loading, 1);
        Assert.InRange(model.Alpha, 0.006, 0.018);
        Assert.True(model.RSquared > 0.9);
        Assert.True(model.Loadings[0].IsSignificant);
    }

    [Fact]
    public void FactorModels_PrincipalComponents_ExtractsCommonFactor()
    {
        Random rng = RandomEngine.Create(12);
        const int t = 400, n = 5;
        var returns = new Matrix(t, n);

        for (int i = 0; i < t; i++)
        {
            double common = RandomEngine.NextGaussian(rng, 0, 0.03);
            for (int j = 0; j < n; j++)
                returns[i, j] = common + RandomEngine.NextGaussian(rng, 0, 0.008);
        }

        (Matrix factors, Vector explained, Matrix loadings) =
            FactorModels.PrincipalComponents(returns, 2);

        Assert.Equal(t, factors.Height);
        Assert.Equal(2, factors.Width);
        Assert.Equal(n, loadings.Height);
        Assert.True(explained[0] > 0.7, $"Первая компонента объясняет лишь {explained[0]:P0}.");
        Assert.True(explained[0] > explained[1]);
    }

    [Fact]
    public void FactorModels_BrinsonAttribution_SumsToActiveReturn()
    {
        var portfolioWeights = new Vector(0.5, 0.3, 0.2);
        var benchmarkWeights = new Vector(0.4, 0.4, 0.2);
        var portfolioReturns = new Vector(0.10, 0.05, 0.02);
        var benchmarkReturns = new Vector(0.08, 0.06, 0.03);

        AttributionResult attribution = FactorModels.BrinsonAttribution(
            ["акции", "облигации", "деньги"],
            portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        double sum = attribution.TotalAllocation + attribution.TotalSelection + attribution.TotalInteraction;

        Assert.Equal(attribution.ActiveReturn, sum, 8);
        Assert.Equal(3, attribution.Segments.Count);
        Assert.Equal(attribution.ActiveReturn, attribution.Segments.Sum(s => s.Total), 8);
    }

    [Fact]
    public void Rebalancing_CompareRules_ShowsCostOfTurnover()
    {
        Random rng = RandomEngine.Create(13);
        const int t = 120, n = 3;
        var returns = new Matrix(t, n);

        for (int i = 0; i < t; i++)
        {
            returns[i, 0] = RandomEngine.NextGaussian(rng, 0.004, 0.015);
            returns[i, 1] = RandomEngine.NextGaussian(rng, 0.008, 0.045);
            returns[i, 2] = RandomEngine.NextGaussian(rng, 0.002, 0.008);
        }

        var target = new Vector(0.4, 0.4, 0.2);
        IReadOnlyList<RebalancingResult> results = Rebalancing.CompareRules(returns, target);

        Assert.Equal(4, results.Count);

        RebalancingResult hold = results.First(r => r.Rule == RebalancingRule.BuyAndHold);
        RebalancingResult calendar = results.First(r => r.Rule == RebalancingRule.Calendar);

        Assert.Equal(0, hold.TotalCost, 10);
        Assert.Equal(0, hold.RebalanceCount);
        Assert.True(calendar.RebalanceCount > 0);
        Assert.True(calendar.TotalCost > 0);
        Assert.True(hold.MaximumDrift >= calendar.MaximumDrift - 1e-9,
            "Без перебалансировки дрейф весов не может быть меньше.");
        Assert.All(results, r => Assert.Equal(1.0, r.FinalWeights.Sum(), 6));
    }
}
