using AI.DataStructs.Algebraic;
using AI.Economics.Clv;
using AI.Economics.Cohorts;
using Xunit;

namespace AI.Economics.UnitTests;

public class CohortAndClvTests
{
    /// <summary>Кривая sBG с известными параметрами — эталон для проверки подгонки.</summary>
    private static Vector SbgCurve(double alpha, double beta, int periods)
    {
        var v = new Vector(periods + 1);
        v[0] = 1.0;
        for (int t = 1; t <= periods; t++) v[t] = v[t - 1] * (beta + t - 1) / (alpha + beta + t - 1);
        return v;
    }

    [Fact]
    public void Fit_Sbg_RecoversRetentionCurve()
    {
        Vector observed = SbgCurve(0.7, 2.5, 8);

        RetentionFitResult fit = RetentionFitter.Fit(
            observed, cohortSize: 20_000, RetentionModel.ShiftedBetaGeometric,
            horizon: 24, bootstrapSamples: 0);

        for (int t = 0; t <= 8; t++)
            Assert.Equal(observed[t], fit.Survival[t], 2);

        Assert.True(fit.Rmse < 0.01);
    }

    [Fact]
    public void Fit_Sbg_RetentionRateGrowsOverTime()
    {
        Vector observed = SbgCurve(0.8, 2.0, 10);

        RetentionFitResult fit = RetentionFitter.Fit(
            observed, 10_000, RetentionModel.ShiftedBetaGeometric, horizon: 36, bootstrapSamples: 0);

        // Ключевое свойство модели: мгновенное удержание растёт, а не постоянно
        Assert.True(fit.RetentionRates[24] > fit.RetentionRates[2],
            "Гетерогенность обязана давать растущее удержание.");
    }

    [Fact]
    public void FitAll_PicksModelThatGeneratedTheData()
    {
        Vector observed = SbgCurve(0.6, 1.8, 12);

        IReadOnlyList<RetentionFitResult> all =
            RetentionFitter.FitAll(observed, 50_000, horizon: 24, bootstrapSamples: 0);

        Assert.Equal(RetentionModel.ShiftedBetaGeometric, all[0].Model);
        Assert.True(all[0].Aic < all[^1].Aic);
    }

    [Fact]
    public void Fit_Bootstrap_ProducesWideningInterval()
    {
        Vector observed = SbgCurve(0.7, 2.5, 6);

        RetentionFitResult fit = RetentionFitter.Fit(
            observed, cohortSize: 500, RetentionModel.ShiftedBetaGeometric,
            horizon: 36, bootstrapSamples: 60, seed: 7);

        double insideWidth = fit.SurvivalUpper[6] - fit.SurvivalLower[6];
        double outsideWidth = fit.SurvivalUpper[36] - fit.SurvivalLower[36];

        Assert.True(fit.SurvivalLower[36] <= fit.Survival[36] + 1e-9);
        Assert.True(fit.SurvivalUpper[36] >= fit.Survival[36] - 1e-9);
        Assert.True(outsideWidth > insideWidth,
            "Интервал за горизонтом наблюдений обязан быть шире, чем внутри него.");
    }

    [Fact]
    public void CohortMatrix_PooledRetention_IgnoresUnobservedCells()
    {
        // Три когорты по 100 клиентов, удержание 100 / 60 / 40 %
        var counts = new Matrix(3, 3);
        double[,] values = { { 100, 60, 40 }, { 100, 60, 40 }, { 100, 60, 40 } };
        for (int c = 0; c < 3; c++)
            for (int t = 0; t < 3; t++) counts[c, t] = values[c, t];

        CohortMatrix cohorts = CohortMatrix.Triangular(counts);
        Vector pooled = cohorts.PooledRetention();

        Assert.Equal(1.0, pooled[0], 6);
        Assert.Equal(0.6, pooled[1], 6);
        Assert.Equal(0.4, pooled[2], 6);

        // Базой самого старшего возраста является одна когорта из трёх
        Assert.Equal(100, cohorts.EffectiveCohortSize(), 6);
    }

    /// <summary>Синтетический портфель: частые покупатели и «спящие».</summary>
    private static List<CustomerSummary> SyntheticPortfolio()
    {
        var rng = new Random(17);
        var list = new List<CustomerSummary>(400);

        for (int i = 0; i < 400; i++)
        {
            bool active = i % 3 != 0;
            double age = 40 + (rng.NextDouble() * 12);
            double frequency = active ? Math.Round(4 + (rng.NextDouble() * 10)) : Math.Round(rng.NextDouble() * 3);
            double recency = active ? age * (0.75 + (rng.NextDouble() * 0.24)) : age * rng.NextDouble() * 0.4;

            list.Add(new CustomerSummary
            {
                Id = $"c{i}",
                Frequency = frequency,
                Recency = recency,
                Age = age,
                MonetaryValue = 30 + (rng.NextDouble() * 120),
            });
        }

        return list;
    }

    [Fact]
    public void BgNbd_Fit_ProducesPositiveParametersAndFiniteLikelihood()
    {
        var model = new BgNbdModel();
        model.Fit(SyntheticPortfolio());

        Assert.True(model.R > 0 && model.Alpha > 0 && model.A > 0 && model.B > 0);
        Assert.True(double.IsFinite(model.LogLikelihood));
    }

    [Fact]
    public void BgNbd_ProbabilityAlive_FallsWithSilence()
    {
        var model = new BgNbdModel();
        model.Fit(SyntheticPortfolio());

        var recent = new CustomerSummary { Frequency = 10, Recency = 49, Age = 50 };
        var silent = new CustomerSummary { Frequency = 10, Recency = 10, Age = 50 };

        Assert.True(model.ProbabilityAlive(recent) > model.ProbabilityAlive(silent));
        Assert.True(model.ExpectedTransactions(recent, 12) > model.ExpectedTransactions(silent, 12));
    }

    [Fact]
    public void ParetoNbd_Fit_MatchesBgNbdOrdering()
    {
        var model = new ParetoNbdModel();
        model.Fit(SyntheticPortfolio());

        var recent = new CustomerSummary { Frequency = 10, Recency = 49, Age = 50 };
        var silent = new CustomerSummary { Frequency = 10, Recency = 10, Age = 50 };

        Assert.True(double.IsFinite(model.LogLikelihood));
        Assert.True(model.ProbabilityAlive(recent) > model.ProbabilityAlive(silent));
    }

    [Fact]
    public void GammaGamma_ShrinksRareCustomerTowardsPopulationMean()
    {
        List<CustomerSummary> portfolio = SyntheticPortfolio();
        var model = new GammaGammaModel();
        model.Fit(portfolio);

        // Один дорогой чек: оценка обязана уехать в сторону среднего
        var rare = new CustomerSummary { Frequency = 1, MonetaryValue = 5000, Age = 50, Recency = 40 };
        double estimate = model.ConditionalExpectedValue(rare);

        Assert.True(estimate < rare.MonetaryValue);
        Assert.True(estimate > model.PopulationMean);
    }

    [Fact]
    public void ClvCalculator_DiscountingLowersValue()
    {
        List<CustomerSummary> portfolio = SyntheticPortfolio();

        var frequency = new BgNbdModel();
        frequency.Fit(portfolio);
        var monetary = new GammaGammaModel();
        monetary.Fit(portfolio);

        ClvPortfolio plain = ClvCalculator.Compute(frequency, monetary, portfolio, 12, 12, 0);
        ClvPortfolio discounted = ClvCalculator.Compute(frequency, monetary, portfolio, 12, 12, 0.01);

        Assert.True(discounted.TotalClv < plain.TotalClv);
        Assert.True(plain.Top10PercentShare > 0.1);
    }
}
