using AI.Economics.Statements;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>Тесты анализа отчётности: коэффициенты, модели банкротства и форензика.</summary>
public class StatementsTests
{
    [Fact]
    public void FinancialStatement_Derived_AreInternallyConsistent()
    {
        FinancialStatement statement = EconomicsSamples.Statement("Компания", "2024");

        Assert.Equal(statement.TotalAssets - statement.TotalLiabilities, statement.Equity, 6);
        Assert.Equal(statement.Revenue - statement.CostOfGoodsSold, statement.GrossProfit, 6);
        Assert.Equal(statement.OperatingIncome + statement.Depreciation, statement.Ebitda, 6);
        Assert.Equal(statement.OperatingIncome - statement.InterestExpense, statement.PretaxIncome, 6);
        Assert.Empty(statement.Validate());
    }

    [Fact]
    public void FinancialStatement_Validate_DetectsBrokenBalance()
    {
        FinancialStatement broken = EconomicsSamples.Statement("Компания", "2024") with
        {
            CurrentAssets = 2_000_000_000,
        };

        Assert.Contains(broken.Validate(), p => p.Contains("оборотные активы"));
    }

    [Fact]
    public void FinancialRatios_Compute_MatchesManualCalculation()
    {
        FinancialStatement s = EconomicsSamples.Statement("Компания", "2024");
        RatioReport report = FinancialRatios.Compute(s);

        Assert.Equal(s.CurrentAssets / s.CurrentLiabilities, report.CurrentRatio, 8);
        Assert.Equal(s.NetIncome / s.Revenue, report.NetMargin, 8);
        Assert.Equal(s.NetIncome / s.Equity, report.ReturnOnEquity, 8);
        Assert.Equal(s.TotalDebt / s.Equity, report.DebtToEquity, 8);
        Assert.Equal(s.OperatingIncome / s.InterestExpense, report.InterestCoverage, 8);

        double dso = s.AccountsReceivable * 365 / s.Revenue;
        double dio = s.Inventory * 365 / s.CostOfGoodsSold;
        double dpo = s.AccountsPayable * 365 / s.CostOfGoodsSold;
        Assert.Equal(dso + dio - dpo, report.CashConversionCycle, 6);
    }

    [Fact]
    public void FinancialRatios_Compute_CoversAllGroups()
    {
        RatioReport report = FinancialRatios.Compute(EconomicsSamples.Statement("Компания", "2024"));

        string[] groups = ["Ликвидность", "Рентабельность", "Оборачиваемость", "Долговая нагрузка", "Денежный поток"];

        foreach (string group in groups)
            Assert.NotEmpty(report.Group(group));

        Assert.True(report.Ratios.Count >= 25);
        Assert.All(report.Ratios, r => Assert.True(double.IsFinite(r.Value)));
        Assert.InRange(report.BenchmarkPassRate, 0, 1);
    }

    [Fact]
    public void FinancialRatios_Compute_UsesAverageBalancesWhenPreviousGiven()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();

        RatioReport endingOnly = FinancialRatios.Compute(current);
        RatioReport averaged = FinancialRatios.Compute(current, previous);

        // Средние остатки меньше конечных у растущей компании, поэтому оборачиваемость выше
        Assert.True(averaged.AssetTurnover > endingOnly.AssetTurnover);
    }

    [Fact]
    public void DuPont_Analyze_FactorsMultiplyToReturnOnEquity()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();
        DuPontResult result = DuPontAnalysis.Analyze(current, previous);

        double three = result.ThreeFactor.Aggregate(1.0, (acc, f) => acc * f.Value);
        double five = result.FiveFactor.Aggregate(1.0, (acc, f) => acc * f.Value);

        Assert.Equal(current.NetIncome / current.Equity, result.ReturnOnEquity, 8);
        Assert.Equal(result.ReturnOnEquity, three, 8);
        Assert.Equal(result.ReturnOnEquity, five, 8);
    }

    [Fact]
    public void DuPont_Analyze_ContributionsSumToChange()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();
        DuPontResult result = DuPontAnalysis.Analyze(current, previous);

        Assert.True(result.HasComparison);
        Assert.Equal(result.Change, result.ThreeFactor.Sum(f => f.Contribution), 8);
        Assert.Equal(result.Change, result.FiveFactor.Sum(f => f.Contribution), 8);
    }

    [Fact]
    public void DuPont_Analyze_WithoutPreviousPeriodHasZeroContributions()
    {
        DuPontResult result = DuPontAnalysis.Analyze(EconomicsSamples.Statement("Компания", "2024"));

        Assert.False(result.HasComparison);
        Assert.All(result.FiveFactor, f => Assert.Equal(0, f.Contribution, 10));
    }

    [Fact]
    public void DistressScores_Evaluate_RanksStrongCompanyAboveWeak()
    {
        FinancialStatement strong = EconomicsSamples.Statement("Сильная", "2024", quality: 0.95);
        FinancialStatement weak = EconomicsSamples.Statement("Слабая", "2024", quality: 0.05);

        DistressReport strongReport = DistressScores.Evaluate(strong);
        DistressReport weakReport = DistressScores.Evaluate(weak);

        Assert.True(DistressScores.Altman(strong).Value > DistressScores.Altman(weak).Value);
        Assert.True(DistressScores.AltmanDoublePrime(strong).Value > DistressScores.AltmanDoublePrime(weak).Value);
        Assert.True(DistressScores.Springate(strong).Value > DistressScores.Springate(weak).Value);
        Assert.True(strongReport.DistressVotes <= weakReport.DistressVotes);
        Assert.Equal(5, strongReport.Scores.Count);
    }

    [Fact]
    public void DistressScores_Ohlson_GivesProbabilityBetweenZeroAndOne()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();
        DistressScore score = DistressScores.Ohlson(current, previous);

        Assert.NotNull(score.ProbabilityOfDefault);
        Assert.InRange(score.ProbabilityOfDefault!.Value, 0, 1);
        Assert.Equal(score.Value, score.Components.Sum(c => c.Contribution), 8);
    }

    [Fact]
    public void DistressScores_Piotroski_CountsPassedCriteria()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();
        (int score, var criteria) = DistressScores.Piotroski(current, previous);

        Assert.Equal(9, criteria.Count);
        Assert.InRange(score, 0, 9);
        Assert.Equal(score, criteria.Count(c => c.Passed));
    }

    [Fact]
    public void Beneish_Compute_ReactsToInflatedReceivablesAndAccruals()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();

        FinancialStatement manipulated = current with
        {
            AccountsReceivable = current.AccountsReceivable * 1.8,
            OperatingCashFlow = current.OperatingCashFlow * 0.2,
        };

        BeneishResult clean = BeneishModel.Compute(current, previous);
        BeneishResult suspect = BeneishModel.Compute(manipulated, previous);

        Assert.True(suspect.MScore > clean.MScore);
        Assert.True(suspect.Probability > clean.Probability);
        Assert.Equal(suspect.MScore, suspect.Indices.Sum(i => i.Contribution), 8);
        Assert.Equal(9, suspect.Indices.Count);
    }

    [Fact]
    public void Benford_Analyze_SeparatesNaturalFromFabricated()
    {
        BenfordResult natural = BenfordAnalysis.Analyze(
            EconomicsSamples.NaturalPayments(), BenfordScope.FirstDigit, "естественные платежи");
        BenfordResult fabricated = BenfordAnalysis.Analyze(
            EconomicsSamples.FabricatedPayments(), BenfordScope.FirstDigit, "подобранные платежи");

        Assert.True(natural.MeanAbsoluteDeviation < fabricated.MeanAbsoluteDeviation);
        Assert.True(fabricated.PValue < 0.01, "Равномерные суммы обязаны отвергаться критерием.");
        Assert.True(natural.MeanAbsoluteDeviation < 0.015);
        Assert.NotEmpty(fabricated.Suspicious);

        Assert.Equal(9, natural.Digits.Count);
        Assert.Equal(1.0, natural.Digits.Sum(d => d.ExpectedShare), 8);
        Assert.Equal(natural.SampleSize, natural.Digits.Sum(d => d.Observed));
    }

    [Fact]
    public void Benford_Analyze_SupportsFirstTwoDigits()
    {
        BenfordResult result = BenfordAnalysis.Analyze(
            EconomicsSamples.NaturalPayments(6000), BenfordScope.FirstTwoDigits);

        Assert.Equal(90, result.Digits.Count);
        Assert.Equal(10, result.Digits[0].Digit);
        Assert.Equal(99, result.Digits[^1].Digit);
        Assert.Equal(1.0, result.Digits.Sum(d => d.ExpectedShare), 8);
    }

    [Theory]
    [InlineData(0.00123, 1, 12)]
    [InlineData(45_600, 4, 45)]
    [InlineData(-987.6, 9, 98)]
    [InlineData(0, 0, 0)]
    public void Benford_LeadingDigits_IsScaleInvariant(double value, int first, int firstTwo)
    {
        Assert.Equal(first, BenfordAnalysis.LeadingDigits(value, BenfordScope.FirstDigit));
        Assert.Equal(firstTwo, BenfordAnalysis.LeadingDigits(value, BenfordScope.FirstTwoDigits));
    }

    [Fact]
    public void WorkingCapital_Analyze_ComputesCycleAndCashRelease()
    {
        FinancialStatement s = EconomicsSamples.Statement("Компания", "2024");
        WorkingCapitalResult result = WorkingCapitalAnalysis.Analyze(s);

        double dso = s.AccountsReceivable / (s.Revenue / 365);
        double dio = s.Inventory / (s.CostOfGoodsSold / 365);
        double dpo = s.AccountsPayable / (s.CostOfGoodsSold / 365);

        Assert.Equal(dso, result.DaysSalesOutstanding, 6);
        Assert.Equal(dio, result.DaysInventoryOutstanding, 6);
        Assert.Equal(dpo, result.DaysPayablesOutstanding, 6);
        Assert.Equal(dso + dio - dpo, result.CashConversionCycle, 6);
        Assert.Equal(dso + dio, result.OperatingCycle, 6);

        Assert.Equal(3, result.Drivers.Count);
        Assert.True(result.PotentialCashRelease >= 0);
        Assert.Equal(
            s.AccountsReceivable + s.Inventory - s.AccountsPayable, result.WorkingCapital, 6);
    }

    [Fact]
    public void WorkingCapital_Analyze_TighterTargetsReleaseMoreCash()
    {
        FinancialStatement s = EconomicsSamples.Statement("Компания", "2024");

        WorkingCapitalResult relaxed = WorkingCapitalAnalysis.Analyze(
            s, new WorkingCapitalTargets { DaysSalesOutstanding = 60, DaysInventoryOutstanding = 60 });
        WorkingCapitalResult tight = WorkingCapitalAnalysis.Analyze(
            s, new WorkingCapitalTargets { DaysSalesOutstanding = 20, DaysInventoryOutstanding = 20 });

        Assert.True(tight.PotentialCashRelease > relaxed.PotentialCashRelease);
    }

    [Fact]
    public void EarningsQuality_Evaluate_PenalisesAccruals()
    {
        (FinancialStatement previous, FinancialStatement current) = EconomicsSamples.StatementPair();

        FinancialStatement aggressive = current with
        {
            OperatingCashFlow = current.OperatingCashFlow * 0.2,
            AccountsReceivable = current.AccountsReceivable * 1.7,
        };

        EarningsQualityResult clean = EarningsQuality.Evaluate(current, previous);
        EarningsQualityResult weak = EarningsQuality.Evaluate(aggressive, previous);

        Assert.True(weak.AccrualRatio > clean.AccrualRatio);
        Assert.True(weak.CashFlowToNetIncome < clean.CashFlowToNetIncome);
        Assert.True(weak.QualityScore < clean.QualityScore);
        Assert.InRange(clean.QualityScore, 0, 100);
        Assert.True(clean.HasComparison);
    }

    [Fact]
    public void Bankruptcy_Train_LearnsAboveChanceOnCrossValidation()
    {
        var predictor = new BankruptcyPredictor();
        BankruptcyModelResult result = predictor.Train(EconomicsSamples.BankruptcySample());

        Assert.True(result.CrossValidated.Gini > 0.5,
            $"Джини на контроле {result.CrossValidated.Gini:F3} слишком мал для этой выборки.");
        Assert.True(result.Bankruptcies > 0 && result.Bankruptcies < result.Observations);
        Assert.Equal(predictor.FeatureNames.Count, result.Importances.Count);
        Assert.True(result.Importances[0].Importance >= result.Importances[^1].Importance);
        Assert.True(predictor.IsTrained);
    }

    [Fact]
    public void Bankruptcy_Predict_GivesLowerRiskToStrongCompany()
    {
        var predictor = new BankruptcyPredictor();
        predictor.Train(EconomicsSamples.BankruptcySample());

        BankruptcyPrediction strong = predictor.Predict(
            EconomicsSamples.Statement("Сильная", "2024", quality: 0.95));
        BankruptcyPrediction weak = predictor.Predict(
            EconomicsSamples.Statement("Слабая", "2024", quality: 0.1));

        Assert.True(strong.Probability < weak.Probability);
        Assert.True(strong.AltmanZ > weak.AltmanZ);
        Assert.Equal(predictor.FeatureNames.Count, strong.Features.Count);
        Assert.InRange(strong.Probability, 0, 1);
    }

    [Fact]
    public void Bankruptcy_CompareAll_ReturnsEveryModelOrderedByQuality()
    {
        IReadOnlyList<BankruptcyModelResult> results =
            BankruptcyPredictor.CompareAll(EconomicsSamples.BankruptcySample(200), folds: 4);

        Assert.Equal(Enum.GetValues<BankruptcyModelKind>().Length, results.Count);
        Assert.Equal(results.Count, results.Select(r => r.Model).Distinct().Count());

        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].CrossValidated.Gini >= results[i].CrossValidated.Gini);
    }

    [Fact]
    public void Bankruptcy_Predict_ThrowsWhenNotTrained()
    {
        var predictor = new BankruptcyPredictor();

        Assert.Throws<InvalidOperationException>(
            () => predictor.Predict(EconomicsSamples.Statement("Компания", "2024")));
    }
}
