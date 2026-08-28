using AI.Economics.Runway;
using AI.Economics.Saas;
using Xunit;

namespace AI.Economics.UnitTests;

public class SaasAndRunwayTests
{
    [Fact]
    public void MrrBridge_ComponentsReconcileWithEndingMrr()
    {
        var start = new Dictionary<string, double>
        {
            ["a"] = 1000, ["b"] = 500, ["c"] = 300, ["d"] = 200,
        };
        var end = new Dictionary<string, double>
        {
            ["a"] = 1400,   // расширение +400
            ["b"] = 300,    // сжатие -200
            ["c"] = 0,      // отток -300
            ["d"] = 200,    // без изменений
            ["e"] = 700,    // новый
        };

        MrrBridgeResult bridge = MrrBridge.Build(start, end);

        Assert.Equal(2000, bridge.StartingMrr, 6);
        Assert.Equal(400, bridge.ExpansionMrr, 6);
        Assert.Equal(200, bridge.ContractionMrr, 6);
        Assert.Equal(300, bridge.ChurnedMrr, 6);
        Assert.Equal(700, bridge.NewMrr, 6);

        double reconstructed = bridge.StartingMrr + bridge.NewMrr + bridge.ExpansionMrr
                             + bridge.ReactivationMrr - bridge.ContractionMrr - bridge.ChurnedMrr;
        Assert.Equal(bridge.EndingMrr, reconstructed, 6);
    }

    [Fact]
    public void MrrBridge_NdrExceedsGrr_WhenExpansionExists()
    {
        var start = new Dictionary<string, double> { ["a"] = 1000, ["b"] = 1000 };
        var end = new Dictionary<string, double> { ["a"] = 1500, ["b"] = 900 };

        MrrBridgeResult bridge = MrrBridge.Build(start, end);

        Assert.Equal(1.20, bridge.NetDollarRetention, 6);
        Assert.Equal(0.95, bridge.GrossRevenueRetention, 6);
        Assert.True(bridge.NetDollarRetention > bridge.GrossRevenueRetention);
    }

    [Fact]
    public void MrrBridge_Series_TracksReactivationSeparately()
    {
        var snapshots = new List<IReadOnlyDictionary<string, double>>
        {
            new Dictionary<string, double> { ["a"] = 100, ["b"] = 100 },
            new Dictionary<string, double> { ["a"] = 100 },              // b ушёл
            new Dictionary<string, double> { ["a"] = 100, ["b"] = 100 }, // b вернулся
        };

        IReadOnlyList<MrrBridgeResult> series = MrrBridge.BuildSeries(snapshots);

        Assert.Equal(2, series.Count);
        Assert.Equal(100, series[0].ChurnedMrr, 6);
        Assert.Equal(100, series[1].ReactivationMrr, 6);
        Assert.Equal(0, series[1].NewMrr, 6);
    }

    [Fact]
    public void SaasMetrics_KnownValues()
    {
        Assert.Equal(0.8, SaasMetrics.MagicNumber(1_000_000, 1_400_000, 500_000), 6);
        Assert.Equal(2.0, SaasMetrics.BurnMultiple(800_000, 400_000), 6);
        Assert.Equal(12.5, SaasMetrics.CacPaybackMonths(1000, 100, 0.8), 6);
        Assert.Equal(45, SaasMetrics.RuleOf40(60, -15), 6);
    }

    [Fact]
    public void SaasMetrics_Evaluate_FlagsWeakBurnMultiple()
    {
        IReadOnlyList<SaasMetric> metrics = SaasMetrics.Evaluate(new SaasHealthInput
        {
            ArrStart = 1_000_000,
            ArrEnd = 1_200_000,
            ArrYearAgo = 900_000,
            SalesAndMarketing = 600_000,
            NetBurn = 900_000,
            FreeCashFlowMargin = -0.4,
            GrossMarginRate = 0.8,
            ArpaMonthly = 200,
            Cac = 4000,
        });

        SaasMetric burn = metrics.First(m => m.Name == "Burn multiple");
        Assert.Equal(4.5, burn.Value, 6);
        Assert.Equal(MetricVerdict.Poor, burn.Verdict);

        SaasMetric payback = metrics.First(m => m.Name == "CAC payback");
        Assert.Equal(25, payback.Value, 6);
        Assert.Equal(MetricVerdict.Poor, payback.Verdict);
    }

    [Fact]
    public void Runway_ZeroVolatility_MatchesDeterministicPath()
    {
        var result = RunwaySimulator.Simulate(new RunwayInput
        {
            Cash = 1_000_000,
            MonthlyRevenue = 0,
            RevenueGrowthMean = 0,
            RevenueGrowthVolatility = 0,
            GrossMarginRate = 1.0,
            MonthlyCosts = 100_000,
            CostGrowthMean = 0,
            CostGrowthVolatility = 0,
            Horizon = 24,
            Simulations = 200,
            Seed = 3,
        });

        // Ровно 100 тысяч в месяц: деньги кончаются на 11-м месяце
        Assert.Equal(10, result.DeterministicRunwayMonths, 6);
        Assert.Equal(11, result.CashOutP50, 6);
        Assert.Equal(0, result.SurvivalProbability, 6);
    }

    [Fact]
    public void Runway_VolatilitySpreadsCashOutDistribution()
    {
        var result = RunwaySimulator.Simulate(new RunwayInput
        {
            Cash = 3_000_000,
            MonthlyRevenue = 200_000,
            RevenueGrowthMean = 0.06,
            RevenueGrowthVolatility = 0.18,
            GrossMarginRate = 0.8,
            MonthlyCosts = 400_000,
            CostGrowthMean = 0.03,
            CostGrowthVolatility = 0.05,
            Horizon = 36,
            Simulations = 3000,
            Seed = 42,
        });

        Assert.True(result.CashOutP10 <= result.CashOutP50);
        Assert.True(result.CashOutP50 <= result.CashOutP90);
        Assert.True(result.ProbabilityCashOutIn6 <= result.ProbabilityCashOutIn12);
        Assert.InRange(result.SurvivalProbability, 0, 1);

        for (int m = 0; m < result.CashP50.Count; m++)
            Assert.True(result.CashP10[m] <= result.CashP50[m] && result.CashP50[m] <= result.CashP90[m]);
    }

    [Fact]
    public void Runway_FundingEventExtendsLife()
    {
        RunwayInput baseline = new()
        {
            Cash = 500_000,
            MonthlyRevenue = 0,
            GrossMarginRate = 1.0,
            MonthlyCosts = 100_000,
            Horizon = 24,
            Simulations = 100,
            Seed = 9,
        };

        RunwayResult without = RunwaySimulator.Simulate(baseline);
        RunwayResult with = RunwaySimulator.Simulate(baseline with
        {
            Funding = [new FundingEvent(4, 1_000_000)],
        });

        Assert.True(with.CashOutP50 > without.CashOutP50);
    }
}
