using AI.DataStructs.Algebraic;
using AI.Economics.Market;
using AI.Economics.Valuation;
using Xunit;

namespace AI.Economics.UnitTests;

public class ValuationAndMarketTests
{
    [Fact]
    public void VcMethod_ReturnsConsistentOwnershipAndValuation()
    {
        VcMethodResult result = StartupValuation.VcMethod(new VcMethodInput
        {
            Investment = 2_000_000,
            ExitRevenue = 50_000_000,
            ExitMultiple = 4,
            YearsToExit = 5,
            TargetIrr = 0.5,
        });

        Assert.Equal(200_000_000, result.ExitValue, 6);
        Assert.Equal(Math.Pow(1.5, 5), result.MoneyMultiple, 6);

        // Доля при выходе умноженная на стоимость выхода = вложение с доходностью
        Assert.Equal(result.OwnershipAtExit * result.ExitValue,
            2_000_000 * result.MoneyMultiple, 3);

        Assert.Equal(result.PostMoneyValuation - 2_000_000, result.PreMoneyValuation, 6);
    }

    [Fact]
    public void VcMethod_FutureDilutionRaisesRequiredOwnership()
    {
        var input = new VcMethodInput
        {
            Investment = 2_000_000,
            ExitValueOverride = 200_000_000,
            YearsToExit = 5,
            TargetIrr = 0.5,
        };

        VcMethodResult plain = StartupValuation.VcMethod(input);
        VcMethodResult diluted = StartupValuation.VcMethod(input with { ExpectedFutureDilution = 0.5 });

        Assert.Equal(plain.OwnershipNow * 2, diluted.OwnershipNow, 9);
        Assert.True(diluted.PreMoneyValuation < plain.PreMoneyValuation);
    }

    [Fact]
    public void Berkus_SumsCappedContributions()
    {
        IReadOnlyList<BerkusFactor> factors = StartupValuation.BerkusDefaults(500_000, 1.0, 0.5, 0.8, 0.2, 0.0);
        double value = StartupValuation.Berkus(factors);

        Assert.Equal(500_000 * (1.0 + 0.5 + 0.8 + 0.2 + 0.0), value, 6);
    }

    [Fact]
    public void Scorecard_AverageCompanyGetsMarketValuation()
    {
        IReadOnlyList<ScorecardFactor> average = StartupValuation.ScorecardDefaults(1, 1, 1, 1, 1, 1, 1);
        Assert.Equal(10_000_000, StartupValuation.Scorecard(10_000_000, average), 3);

        IReadOnlyList<ScorecardFactor> strongTeam = StartupValuation.ScorecardDefaults(1.5, 1, 1, 1, 1, 1, 1);
        Assert.Equal(10_000_000 * 1.15, StartupValuation.Scorecard(10_000_000, strongTeam), 3);
    }

    [Fact]
    public void FirstChicago_NormalisesProbabilitiesAndReportsConcentration()
    {
        ScenarioValuationResult result = StartupValuation.FirstChicago(
        [
            new ValuationScenario("Прорыв", 0.10, 200_000_000),
            new ValuationScenario("База", 0.30, 40_000_000),
            new ValuationScenario("Провал", 0.60, 0),
        ]);

        Assert.Equal((0.1 * 200_000_000) + (0.3 * 40_000_000), result.ExpectedValuation, 3);
        Assert.True(result.BestCaseShare > 0.6, "Оценка держится на одном сценарии — это должно быть видно.");
        Assert.True(result.StandardDeviation > result.ExpectedValuation);
    }

    [Fact]
    public void RealOption_ValueExceedsStaticNpv()
    {
        RealOptionResult result = RealOptionValuation.Evaluate(new RealOptionInput
        {
            ProjectValue = 8_000_000,
            InvestmentCost = 10_000_000,
            YearsToDecision = 3,
            Volatility = 0.6,
            RiskFreeRate = 0.08,
        });

        Assert.True(result.StaticNpv < 0, "Проект с отрицательным NPV — самый показательный случай.");
        Assert.True(result.BinomialValue > 0, "Право подождать имеет положительную стоимость.");
        Assert.True(result.FlexibilityPremium > 0);
        Assert.InRange(result.Delta, 0, 1);
    }

    [Fact]
    public void RealOption_BinomialAgreesWithBlackScholes_WithoutLeakage()
    {
        RealOptionResult result = RealOptionValuation.Evaluate(new RealOptionInput
        {
            ProjectValue = 12_000_000,
            InvestmentCost = 10_000_000,
            YearsToDecision = 2,
            Volatility = 0.4,
            RiskFreeRate = 0.05,
            ValueLeakage = 0,
            Steps = 400,
        });

        // Без утечки стоимости досрочное исполнение не выгодно: оба метода сходятся
        double relative = Math.Abs(result.BinomialValue - result.BlackScholesValue)
                        / result.BlackScholesValue;
        Assert.True(relative < 0.01, $"Расхождение методов {relative:P2} слишком велико.");
    }

    [Fact]
    public void MarketSizing_ConsistentEstimatesProduceLowDivergence()
    {
        MarketSizingResult result = MarketSizing.Estimate(
            new TopDownInput
            {
                TotalMarketValue = 10_000_000_000,
                GeographyShare = 0.1,
                SegmentShare = 0.3,
                AddressableShare = 0.5,
                AchievableShare = 0.04,
            },
            new BottomUpInput
            {
                TargetAccounts = 5_000,
                QualifiedShare = 0.6,
                AnnualRevenuePerAccount = 200_000,
                ReachableShare = 0.5,
                WinRate = 0.05,
            });

        Assert.Equal(1_000_000_000, result.TamTopDown, 3);
        Assert.Equal(1_000_000_000, result.TamBottomUp, 3);
        Assert.Equal(1.0, result.TamDivergence, 6);
        Assert.Contains("согласован", result.Verdict);
    }

    [Fact]
    public void MarketSizing_DivergentEstimatesAreFlagged()
    {
        MarketSizingResult result = MarketSizing.Estimate(
            new TopDownInput { TotalMarketValue = 40_000_000_000, AchievableShare = 0.01 },
            new BottomUpInput { TargetAccounts = 200, AnnualRevenuePerAccount = 50_000, WinRate = 0.1 });

        Assert.True(result.TamDivergence > 10);
        Assert.Contains("несовместимы", result.Verdict);
    }

    [Fact]
    public void Bass_RecoversParametersFromGeneratedCurve()
    {
        const double m = 100_000, p = 0.02, q = 0.4;

        var truth = new BassDiffusion();
        truth.SetParameters(m, p, q);
        Vector cumulative = truth.Cumulative(24);

        var fitted = new BassDiffusion();
        fitted.Fit(cumulative);

        Assert.InRange(fitted.MarketPotential, m * 0.98, m * 1.02);
        Assert.Equal(p, fitted.Innovation, 3);
        Assert.Equal(q, fitted.Imitation, 2);
        Assert.True(fitted.RSquared > 0.999);
    }

    [Fact]
    public void Bass_PeakTimeMatchesMaximumOfAdopterCurve()
    {
        var model = new BassDiffusion();
        model.SetParameters(100_000, 0.02, 0.4);

        Vector adopters = model.Adopters(60);
        int argmax = 0;
        for (int i = 1; i < adopters.Count; i++)
            if (adopters[i] > adopters[argmax]) argmax = i;

        // Дискретный пик приходится на период, содержащий непрерывный максимум
        Assert.InRange(model.PeakTime, argmax - 0.5, argmax + 1.5);
        Assert.True(model.PeakAdopters > adopters[argmax]);
    }
}
