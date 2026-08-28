using AI.DataStructs.Algebraic;
using AI.Economics.Marketing;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

public class MarketingTests
{
    /// <summary>
    /// Синтетические продажи: два канала с известными затуханием и насыщением
    /// плюс базовая линия с трендом и сезонностью.
    /// </summary>
    private static MmmInput SyntheticMarket(int weeks = 156, int seed = 3)
    {
        var rng = RandomEngine.Create(seed);

        var tvSpend = new Vector(weeks);
        var digitalSpend = new Vector(weeks);
        var sales = new Vector(weeks);

        double tvCarry = 0, digitalCarry = 0;

        for (int t = 0; t < weeks; t++)
        {
            // Телевидение идёт волнами, digital — почти постоянно
            tvSpend[t] = t % 8 < 3 ? 800_000 * (0.7 + (rng.NextDouble() * 0.6)) : 0;
            digitalSpend[t] = 300_000 * (0.6 + (rng.NextDouble() * 0.8));

            tvCarry = tvSpend[t] + (0.6 * tvCarry);
            digitalCarry = digitalSpend[t] + (0.2 * digitalCarry);

            double tvEffect = 4_000_000 * MarketingMixModel.Hill(tvCarry, 1_500_000, 1.5);
            double digitalEffect = 2_000_000 * MarketingMixModel.Hill(digitalCarry, 400_000, 1.2);

            double baseline = 8_000_000 + (12_000 * t)
                            + (900_000 * Math.Sin(2 * Math.PI * t / 52.0));

            sales[t] = baseline + tvEffect + digitalEffect + RandomEngine.NextGaussian(rng, 0, 250_000);
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

    [Fact]
    public void Mmm_Adstock_AccumulatesGeometrically()
    {
        var spend = new Vector(100.0, 0, 0, 0);
        Vector adstock = MarketingMixModel.Adstock(spend, 0.5);

        Assert.Equal(100, adstock[0], 6);
        Assert.Equal(50, adstock[1], 6);
        Assert.Equal(25, adstock[2], 6);
        Assert.Equal(12.5, adstock[3], 6);
    }

    [Fact]
    public void Mmm_Hill_IsHalfAtSaturationPoint()
    {
        Assert.Equal(0.5, MarketingMixModel.Hill(1000, 1000, 1.5), 9);
        Assert.True(MarketingMixModel.Hill(2000, 1000, 1.5) > 0.5);
        Assert.True(MarketingMixModel.Hill(500, 1000, 1.5) < 0.5);
        Assert.Equal(0, MarketingMixModel.Hill(0, 1000, 1.5), 9);
    }

    [Fact]
    public void Mmm_Fit_ExplainsSalesAndSeparatesChannels()
    {
        MmmResult result = MarketingMixModel.Fit(SyntheticMarket());

        Assert.True(result.RSquared > 0.7, $"R2 = {result.RSquared:F3} слишком низкий.");
        Assert.Equal(2, result.Channels.Count);
        Assert.All(result.Channels, c => Assert.True(c.Coefficient > 0,
            $"Канал «{c.Name}» получил отрицательный коэффициент."));

        // Реклама объясняет заметную, но не подавляющую часть продаж
        Assert.InRange(result.MediaShare, 0.05, 0.7);
    }

    [Fact]
    public void Mmm_Fit_RecoversLongerCarryoverForTelevision()
    {
        MmmResult result = MarketingMixModel.Fit(SyntheticMarket());

        ChannelEffect tv = result.Channels.First(c => c.Name == "ТВ");
        ChannelEffect digital = result.Channels.First(c => c.Name == "Digital");

        Assert.True(tv.Decay > digital.Decay,
            $"У ТВ затухание {tv.Decay:F2} должно быть выше, чем у digital {digital.Decay:F2}.");
        Assert.True(tv.HalfLife > 0);
    }

    [Fact]
    public void Mmm_Interpretation_ReportsMarginalRoi()
    {
        MmmResult result = MarketingMixModel.Fit(SyntheticMarket());
        var interpretation = result.Interpret();

        Assert.NotEmpty(interpretation.Summary);
        Assert.Contains(interpretation.Metrics, m => m.Name.StartsWith("ROI"));
        Assert.NotEmpty(interpretation.Warnings);
        Assert.Contains(interpretation.Recommendations, r => r.Contains("предельному"));
    }

    [Fact]
    public void BudgetOptimizer_EqualisesMarginalReturns()
    {
        MmmResult model = MarketingMixModel.Fit(SyntheticMarket());
        BudgetAllocationResult allocation = BudgetOptimizer.Allocate(model);

        var active = allocation.Channels.Where(c => c.OptimalSpend > 1).ToList();
        Assert.NotEmpty(active);

        double min = active.Min(c => c.MarginalReturnAtOptimum);
        double max = active.Max(c => c.MarginalReturnAtOptimum);
        Assert.True(max - min < max * 0.25 + 1e-9,
            $"Предельные отдачи в оптимуме должны выровняться: {min:F4} против {max:F4}.");
    }

    [Fact]
    public void BudgetOptimizer_DoesNotExceedBudgetAndDoesNotLoseResponse()
    {
        MmmResult model = MarketingMixModel.Fit(SyntheticMarket());
        BudgetAllocationResult allocation = BudgetOptimizer.Allocate(model);

        double total = allocation.Channels.Sum(c => c.OptimalSpend);
        Assert.Equal(allocation.TotalBudget, total, 3);
        Assert.True(allocation.OptimalResponse >= allocation.CurrentResponse - 1e-6,
            "Перераспределение того же бюджета не может ухудшить отклик.");
        Assert.NotEmpty(allocation.Interpret().Findings);
    }

    /// <summary>
    /// Промо-эксперимент: восприимчивость зависит от признака, у части
    /// клиентов эффект отрицательный.
    /// </summary>
    private static List<UpliftObservation> PromoExperiment(int n, int seed)
    {
        var rng = RandomEngine.Create(seed);
        var data = new List<UpliftObservation>(n);

        for (int i = 0; i < n; i++)
        {
            double sensitivity = rng.NextDouble();
            double loyalty = rng.NextDouble();
            bool treated = rng.NextDouble() < 0.5;

            double baseRate = 0.10 + (0.25 * loyalty);

            // Восприимчивые сильно откликаются, лояльные — наоборот, слегка
            // уменьшают конверсию: скидка достаётся тем, кто купил бы и так
            double lift = (0.30 * sensitivity) - (0.10 * loyalty);
            double rate = Math.Clamp(baseRate + (treated ? lift : 0), 0.01, 0.95);

            data.Add(new UpliftObservation
            {
                Features = new Vector(sensitivity, loyalty),
                Treated = treated,
                Converted = rng.NextDouble() < rate,
            });
        }

        return data;
    }

    [Fact]
    public void Uplift_RanksCustomersBetterThanRandom()
    {
        UpliftResult result = UpliftModeling.Fit(PromoExperiment(8000, 13), promoCost: 30, marginPerConversion: 300);

        Assert.True(result.QiniCoefficient > 0.05,
            $"Коэффициент Qini {result.QiniCoefficient:F3} не отличается от случайного ранжирования.");
        Assert.True(result.Groups[0].ActualUplift > result.Groups[^1].ActualUplift,
            "Верхняя группа обязана давать больший фактический прирост, чем нижняя.");
    }

    [Fact]
    public void Uplift_TargetedPromoBeatsBlanketPromo()
    {
        UpliftResult result = UpliftModeling.Fit(PromoExperiment(8000, 13), promoCost: 60, marginPerConversion: 300);

        Assert.True(result.ProfitTargeted >= result.ProfitTreatAll,
            "Адресное промо не может быть хуже сплошного.");
        Assert.InRange(result.TargetedShare, 0, 1);
        Assert.Equal(60.0 / 300.0, result.ProfitThreshold, 9);
    }

    [Fact]
    public void Uplift_FindsSleepingDogs()
    {
        UpliftResult result = UpliftModeling.Fit(PromoExperiment(8000, 13), promoCost: 30, marginPerConversion: 300);

        Assert.True(result.SleepingDogs > 0,
            "В выборке есть клиенты с отрицательным приростом, модель обязана их найти.");

        var interpretation = result.Interpret();
        Assert.Contains(interpretation.Findings, f => f.Contains("отрицател"));
    }

    [Fact]
    public void Uplift_RequiresBothGroups()
    {
        var onlyTreated = PromoExperiment(200, 1).Select(o => o with { Treated = true }).ToList();
        Assert.Throws<ArgumentException>(() => UpliftModeling.Fit(onlyTreated, 10, 100));
    }
}
