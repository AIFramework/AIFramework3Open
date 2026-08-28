using AI.DataStructs.Algebraic;
using AI.Economics.UnitEconomics;
using Xunit;

namespace AI.Economics.UnitTests;

public class UnitEconomicsTests
{
    [Fact]
    public void Compute_CacFromSpend_DividesByNewCustomers()
    {
        var result = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            MarketingSpend = 80_000,
            SalesSpend = 20_000,
            NewCustomers = 250,
            RevenuePerPeriod = 100,
            GrossMarginRate = 0.8,
            ChurnRate = 0.05,
            Horizon = 60,
        });

        Assert.Equal(400, result.Cac, 6);
        Assert.Equal(80, result.ContributionPerPeriod, 6);
    }

    [Fact]
    public void Compute_GeometricSurvival_MatchesClosedFormLtv()
    {
        const double churn = 0.08;
        const double discount = 0.01;

        var result = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            CacOverride = 300,
            RevenuePerPeriod = 100,
            GrossMarginRate = 0.75,
            ChurnRate = churn,
            DiscountRate = discount,
            Horizon = 2000,
        });

        double closedForm = UnitEconomicsCalculator.LtvFromChurn(75, churn, discount);
        Assert.Equal(closedForm, result.Ltv, 3);
    }

    [Fact]
    public void Compute_PaybackIsFractional_AndConsistentWithCumulativeCurve()
    {
        var result = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            CacOverride = 250,
            RevenuePerPeriod = 100,
            GrossMarginRate = 1.0,
            ChurnRate = 0.0,
            Horizon = 12,
        });

        // Без оттока и дисконта вклад ровно 100 в период, CAC = 250 -> 2,5 периода
        Assert.Equal(2.5, result.CacPaybackPeriods, 6);
        Assert.True(result.CumulativeNet[1] < 0);
        Assert.True(result.CumulativeNet[2] > 0);
    }

    [Fact]
    public void Compute_ExplicitCurve_BeatsConstantChurnAssumption()
    {
        // Кривая с падающим оттоком: 100 %, 60 %, 50 %, 45 %, 43 %, 42 %
        var curve = new Vector(1.0, 0.60, 0.50, 0.45, 0.43, 0.42);

        var withCurve = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            CacOverride = 100,
            RevenuePerPeriod = 100,
            GrossMarginRate = 1.0,
            Survival = curve,
        });

        // Тот же первый месяц удержания, но принятый за константу
        var withConstant = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            CacOverride = 100,
            RevenuePerPeriod = 100,
            GrossMarginRate = 1.0,
            ChurnRate = 0.40,
            Horizon = 6,
        });

        Assert.True(withCurve.Ltv > withConstant.Ltv,
            "Кривая с затухающим оттоком обязана давать LTV выше геометрической.");
    }

    [Fact]
    public void Analyze_BlendedCacIsLowerThanPaidCac_WhenOrganicExists()
    {
        var mix = ChannelEconomics.Analyze(
        [
            new ChannelInput { Name = "Органика", Spend = 0, NewCustomers = 100,
                RevenuePerPeriod = 100, GrossMarginRate = 0.8, ChurnRate = 0.05, Horizon = 36 },
            new ChannelInput { Name = "Контекст", Spend = 100_000, NewCustomers = 200,
                RevenuePerPeriod = 100, GrossMarginRate = 0.8, ChurnRate = 0.05, Horizon = 36 },
        ]);

        Assert.Equal(100_000.0 / 300, mix.BlendedCac, 6);
        Assert.Equal(100_000.0 / 200, mix.PaidCac, 6);
        Assert.True(mix.BlendedCac < mix.PaidCac);
        Assert.Equal("Органика", mix.BestChannel);
    }
}
