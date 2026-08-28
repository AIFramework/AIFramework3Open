using AI.DataStructs.Algebraic;
using AI.Economics.Credit;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>Тесты кредитного риска: биннинг, скоркарты, резервы и структурные модели.</summary>
public class CreditTests
{
    [Fact]
    public void WoeBinning_Fit_ProducesMonotoneBinsWithInformationValue()
    {
        (Matrix values, List<bool> defaults) = EconomicsSamples.Applications();
        var column = new Vector(values.Height);
        for (int i = 0; i < values.Height; i++) column[i] = values[i, 2];

        VariableBinning binning = WoeBinning.Fit("нагрузка", column, defaults);

        Assert.True(binning.InformationValue > 0.1,
            $"Информационная ценность {binning.InformationValue:F3} слишком мала для сильного признака.");
        Assert.True(binning.IsMonotone, "Связь долговой нагрузки с риском должна быть монотонной.");
        Assert.InRange(binning.Bins.Count, 2, 6);
        Assert.Equal(values.Height, binning.Bins.Sum(b => b.Total));
    }

    [Fact]
    public void WoeBinning_Woe_MatchesDefinition()
    {
        (Matrix values, List<bool> defaults) = EconomicsSamples.Applications(1000, seed: 3);
        var column = new Vector(values.Height);
        for (int i = 0; i < values.Height; i++) column[i] = values[i, 2];

        VariableBinning binning = WoeBinning.Fit("нагрузка", column, defaults);

        double totalGood = binning.Bins.Sum(b => b.Good);
        double totalBad = binning.Bins.Sum(b => b.Bad);

        foreach (ScoreBin bin in binning.Bins)
        {
            double expected = Math.Log(
                ((bin.Good + 0.5) / (totalGood + (0.5 * binning.Bins.Count))) /
                ((bin.Bad + 0.5) / (totalBad + (0.5 * binning.Bins.Count))));

            Assert.Equal(expected, bin.Woe, 6);
        }
    }

    [Fact]
    public void Scorecard_Fit_SeparatesGoodAndBadApplicants()
    {
        (Matrix values, List<bool> defaults) = EconomicsSamples.Applications();

        var scorecard = new Scorecard();
        ScorecardResult result = scorecard.Fit(EconomicsSamples.ScoreVariables, values, defaults);

        Assert.NotEmpty(result.Variables);
        Assert.True(result.Quality.Gini > 0.5,
            $"Коэффициент Джини {result.Quality.Gini:F3} ниже приемлемого для этой выборки.");
        Assert.True(result.ScoreRange.Max > result.ScoreRange.Min);
        Assert.All(result.Points, p => Assert.True(double.IsFinite(p.Points)));
    }

    [Fact]
    public void Scorecard_Score_RoundTripsToProbability()
    {
        (Matrix values, List<bool> defaults) = EconomicsSamples.Applications();

        var scorecard = new Scorecard();
        ScorecardResult result = scorecard.Fit(EconomicsSamples.ScoreVariables, values, defaults);

        var applicant = new Dictionary<string, double>
        {
            ["доход"] = 60_000,
            ["срок_работы"] = 4,
            ["нагрузка"] = 0.5,
        };

        double score = scorecard.Score(applicant);
        double fromScore = scorecard.ProbabilityOfDefault(score);

        double logit = result.Intercept;
        for (int j = 0; j < result.Variables.Count; j++)
        {
            VariableBinning binning = result.Variables[j];
            logit += result.Coefficients[j] * binning.Transform(applicant[binning.Variable]);
        }

        double direct = 1.0 / (1.0 + Math.Exp(-logit));

        // Балл — это логарифм шансов в другой шкале: перевод должен быть обратимым
        Assert.Equal(direct, fromScore, 8);
    }

    [Fact]
    public void ScoreMetrics_Evaluate_MatchesKnownSeparation()
    {
        var perfect = new Vector(0.1, 0.2, 0.3, 0.7, 0.8, 0.9);
        var outcomes = new List<bool> { false, false, false, true, true, true };

        ScoreQuality quality = ScoreMetrics.Evaluate(perfect, outcomes, calibrationBins: 3);

        Assert.Equal(1.0, quality.Auc, 6);
        Assert.Equal(1.0, quality.Gini, 6);
        Assert.Equal(1.0, quality.Ks, 6);
        Assert.Equal(3, quality.Defaults);
    }

    [Fact]
    public void ScoreMetrics_PopulationStability_DetectsShift()
    {
        Random rng = RandomEngine.Create(13);
        var expected = new Vector(2000);
        var same = new Vector(2000);
        var shifted = new Vector(2000);

        for (int i = 0; i < 2000; i++)
        {
            expected[i] = RandomEngine.NextGaussian(rng, 600, 50);
            same[i] = RandomEngine.NextGaussian(rng, 600, 50);
            shifted[i] = RandomEngine.NextGaussian(rng, 540, 50);
        }

        PsiResult stable = ScoreMetrics.PopulationStability(expected, same);
        PsiResult drifted = ScoreMetrics.PopulationStability(expected, shifted);

        Assert.True(stable.Psi < 0.1, $"Индекс стабильности {stable.Psi:F3} должен быть малым.");
        Assert.True(drifted.Psi > 0.25, $"Индекс стабильности {drifted.Psi:F3} должен фиксировать сдвиг.");
    }

    [Fact]
    public void Ifrs9_Compute_AssignsStagesAndLifetimeReserve()
    {
        EclResult result = Ifrs9.Compute(EconomicsSamples.Portfolio());

        Assert.Equal(5, result.Exposures.Count);
        Assert.Contains(result.Exposures, e => e.Stage == CreditStage.Performing);
        Assert.Contains(result.Exposures, e => e.Stage == CreditStage.UnderPerforming);
        Assert.Contains(result.Exposures, e => e.Stage == CreditStage.NonPerforming);

        // Резерв на весь срок не может быть меньше двенадцатимесячного
        Assert.All(result.Exposures, e => Assert.True(e.EclLifetime >= e.Ecl12Month - 1e-6));

        Assert.True(result.TotalEcl > result.TotalEcl12Month,
            "Стадирование обязано увеличивать резерв относительно 12-месячной базы.");
        Assert.True(result.TotalEcl <= result.TotalEclLifetime + 1e-6);
        Assert.InRange(result.CoverageRatio, 0, 1);
    }

    [Fact]
    public void Ifrs9_AssignStage_FollowsDelinquencyAndSicr()
    {
        var clean = new CreditExposure
        {
            ExposureAtDefault = 100, ProbabilityOfDefault = 0.02,
            ProbabilityOfDefaultAtOrigination = 0.019,
        };
        var sicr = clean with { ProbabilityOfDefault = 0.08 };
        var late = clean with { DaysPastDue = 45 };
        var impaired = clean with { DaysPastDue = 120 };

        Assert.Equal(CreditStage.Performing, Ifrs9.AssignStage(clean).Stage);
        Assert.Equal(CreditStage.UnderPerforming, Ifrs9.AssignStage(sicr).Stage);
        Assert.Equal(CreditStage.UnderPerforming, Ifrs9.AssignStage(late).Stage);
        Assert.Equal(CreditStage.NonPerforming, Ifrs9.AssignStage(impaired).Stage);
    }

    [Fact]
    public void Ifrs9_Scenarios_WeightedReserveLiesBetweenExtremes()
    {
        EclResult result = Ifrs9.Compute(EconomicsSamples.Portfolio());

        double best = result.Scenarios.Min(s => s.Ecl);
        double worst = result.Scenarios.Max(s => s.Ecl);

        Assert.True(result.TotalEcl >= best - 1e-6 && result.TotalEcl <= worst + 1e-6,
            "Взвешенный резерв должен лежать между лучшим и худшим сценариями.");
        Assert.Equal(1.0, result.Scenarios.Sum(s => s.Probability), 8);
    }

    [Fact]
    public void MigrationMatrix_Estimate_RecoversKnownTransitions()
    {
        MigrationMatrixResult result = MigrationMatrix.Estimate(
            EconomicsSamples.Ratings, EconomicsSamples.Transitions(20_000));

        double[] expectedDiagonal = [0.90, 0.85, 0.83, 0.82];

        for (int i = 0; i < expectedDiagonal.Length; i++)
            Assert.InRange(result.Transitions[i, i], expectedDiagonal[i] - 0.03, expectedDiagonal[i] + 0.03);

        // Вероятность дефолта должна расти с ухудшением рейтинга
        for (int i = 1; i < expectedDiagonal.Length; i++)
            Assert.True(result.Profiles[i].DefaultRate >= result.Profiles[i - 1].DefaultRate);

        for (int i = 0; i < result.Ratings.Count; i++)
        {
            double row = 0;
            for (int j = 0; j < result.Ratings.Count; j++) row += result.Transitions[i, j];
            Assert.Equal(1.0, row, 8);
        }
    }

    [Fact]
    public void MigrationMatrix_CumulativeDefault_GrowsWithHorizon()
    {
        MigrationMatrixResult result = MigrationMatrix.Estimate(
            EconomicsSamples.Ratings, EconomicsSamples.Transitions());

        IReadOnlyList<Vector> curves = MigrationMatrix.CumulativeDefault(result, 10);

        Assert.Equal(result.Ratings.Count, curves.Count);

        for (int rating = 0; rating < 4; rating++)
        {
            for (int t = 1; t < 10; t++)
                Assert.True(curves[rating][t] >= curves[rating][t - 1] - 1e-12);

            Assert.True(curves[rating][9] > curves[rating][0]);
        }

        // Худший рейтинг накапливает дефолты быстрее лучшего
        Assert.True(curves[3][9] > curves[0][9]);
    }

    [Fact]
    public void RollRate_Analyze_ComputesPathToLoss()
    {
        RollRateResult result = RollRate.Analyze(
            RollRate.DefaultBuckets(), EconomicsSamples.DelinquencyBalances());

        Assert.Equal(5, result.Steps.Count);
        Assert.All(result.Steps, s => Assert.True(s.AverageRollRate > 0));

        // Доля дохода до списания — произведение ставок вдоль цепочки
        double product = result.Steps.Aggregate(1.0, (acc, s) => acc * s.AverageRollRate);
        Assert.Equal(product, result.RollToLoss[0], 10);

        Assert.True(result.ImpliedLoss > 0);
        Assert.InRange(result.ImpliedLossRate, 0, 1);
    }

    [Fact]
    public void VintageAnalysis_Analyze_DetectsDeterioratingQuality()
    {
        VintageResult result = VintageAnalysis.Analyze(EconomicsSamples.Vintages());

        Assert.Equal(6, result.Vintages.Count);
        Assert.Equal(9, result.CommonAge);
        Assert.Equal(24, result.MaxAge);

        Assert.True(result.QualityTrend > 0, "Качество выдач в выборке ухудшается по построению.");
        Assert.True(result.TrendPValue < 0.05, "Тренд задан детерминированно и обязан быть значимым.");

        // Кривая созревания не убывает
        for (int age = 1; age < result.MaturityCurve.Count; age++)
            Assert.True(result.MaturityCurve[age] >= result.MaturityCurve[age - 1] - 1e-12);

        Assert.True(result.ProjectedPortfolioLoss > 0);
        Assert.InRange(result.HalfLossAge, 1, result.MaxAge);
    }

    [Fact]
    public void MertonModel_Estimate_ConvergesAndReproducesEquity()
    {
        MertonResult result = MertonModel.Estimate(EconomicsSamples.PublicCompany());

        Assert.True(result.Converged);
        Assert.True(result.AssetValue > result.DefaultPoint);
        Assert.InRange(result.ProbabilityOfDefault, 0, 1);
        Assert.True(result.DistanceToDefault > 0);

        // Проверка первого уравнения системы: капитал как колл на активы
        MertonInput input = EconomicsSamples.PublicCompany();
        double sigma = result.AssetVolatility;
        double t = input.Horizon;
        double d1 = (Math.Log(result.AssetValue / result.DefaultPoint) +
                     ((input.RiskFreeRate + (sigma * sigma / 2)) * t)) / (sigma * Math.Sqrt(t));
        double d2 = d1 - (sigma * Math.Sqrt(t));

        double equity = (result.AssetValue * Cdf(d1)) -
                        (result.DefaultPoint * Math.Exp(-input.RiskFreeRate * t) * Cdf(d2));

        Assert.InRange(equity, input.EquityValue * 0.999, input.EquityValue * 1.001);
    }

    [Fact]
    public void MertonModel_Estimate_HigherLeverageRaisesDefaultRisk()
    {
        MertonInput baseline = EconomicsSamples.PublicCompany();
        MertonInput leveraged = baseline with { LongTermDebt = baseline.LongTermDebt * 2 };

        MertonResult safe = MertonModel.Estimate(baseline);
        MertonResult risky = MertonModel.Estimate(leveraged);

        Assert.True(risky.ProbabilityOfDefault > safe.ProbabilityOfDefault);
        Assert.True(risky.DistanceToDefault < safe.DistanceToDefault);
        Assert.True(risky.ImpliedCreditSpread >= safe.ImpliedCreditSpread);
    }

    [Fact]
    public void CounterpartyScoring_Score_RanksStrongAboveWeak()
    {
        CounterpartyScore strong = CounterpartyScoring.Score(EconomicsSamples.Counterparty(true));
        CounterpartyScore weak = CounterpartyScoring.Score(EconomicsSamples.Counterparty(false));

        Assert.True(strong.Score > weak.Score);
        Assert.True(strong.ProbabilityOfDefault < weak.ProbabilityOfDefault);
        Assert.True(strong.RecommendedLimit > weak.RecommendedLimit);
        Assert.True(strong.AdvanceRate > weak.AdvanceRate);

        Assert.Empty(strong.StopFactors);
        Assert.NotEmpty(weak.StopFactors);

        Assert.Equal(strong.Score, strong.Factors.Sum(f => f.Contribution), 6);
        Assert.True(strong.RecommendedLimit <= strong.RequestedLimit);
    }

    [Fact]
    public void CounterpartyScoring_ScoreAll_OrdersByScore()
    {
        IReadOnlyList<CounterpartyScore> scores = CounterpartyScoring.ScoreAll(
            [EconomicsSamples.Counterparty(false), EconomicsSamples.Counterparty(true)]);

        Assert.Equal(2, scores.Count);
        Assert.True(scores[0].Score >= scores[1].Score);
    }

    /// <summary>Функция стандартного нормального распределения для проверки модели Мертона.</summary>
    private static double Cdf(double x) => 0.5 * (1 + Erf(x / Math.Sqrt(2)));

    /// <summary>Функция ошибок через приближение Абрамовица-Стиган.</summary>
    private static double Erf(double x)
    {
        double sign = Math.Sign(x);
        double abs = Math.Abs(x);
        double t = 1.0 / (1.0 + (0.3275911 * abs));
        double y = 1.0 - ((((((1.061405429 * t) - 1.453152027) * t) + 1.421413741) * t
            - 0.284496736) * t + 0.254829592) * t * Math.Exp(-abs * abs);

        return sign * y;
    }
}
