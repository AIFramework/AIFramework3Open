using AI.DataStructs.Algebraic;
using AI.Economics.Corporate;
using AI.Economics.Projects;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>Тесты оценки бизнеса, корпоративных финансов и проектного анализа.</summary>
public class CorporateFinanceTests
{
    [Fact]
    public void CostOfCapital_Compute_MatchesTextbookFormula()
    {
        var input = new CostOfCapitalInput
        {
            RiskFreeRate = 0.08, EquityRiskPremium = 0.06, UnleveredBeta = 1.0,
            CountryRiskPremium = 0.02, SizePremium = 0.015,
            EquityValue = 700, DebtValue = 300, CostOfDebt = 0.14, TaxRate = 0.2,
        };

        CostOfCapitalResult result = CostOfCapital.Compute(input);

        double debtBeta = (0.14 - 0.08) / 0.06;
        double expectedBeta = 1.0 + ((1.0 - debtBeta) * 0.8 * 300.0 / 700);
        double expectedEquity = 0.08 + (expectedBeta * 0.06) + 0.02 + 0.015;
        double expectedWacc = (expectedEquity * 0.7) + (0.14 * 0.8 * 0.3);

        Assert.Equal(expectedBeta, result.LeveredBeta, 8);
        Assert.Equal(expectedEquity, result.CostOfEquity, 8);
        Assert.Equal(expectedWacc, result.Wacc, 8);
        Assert.Equal(0.7, result.EquityWeight, 8);
    }

    [Fact]
    public void CostOfCapital_Unlever_InvertsLevering()
    {
        // При безрисковом долге восстанавливается классическая формула Хамады
        double levered = 1.0 * (1 + (0.8 * 0.5));
        Assert.Equal(1.0, CostOfCapital.Unlever(levered, 0.5, 0.2), 8);

        // При рисковом долге рычаг поднимает бету слабее
        double withDebtBeta = 1.0 + ((1.0 - 0.4) * 0.8 * 0.5);
        Assert.Equal(1.0, CostOfCapital.Unlever(withDebtBeta, 0.5, 0.2, 0.4), 8);
    }

    [Fact]
    public void CostOfCapital_WaccCurve_HasInteriorMinimum()
    {
        var input = new CostOfCapitalInput
        {
            RiskFreeRate = 0.08, EquityRiskPremium = 0.06, UnleveredBeta = 1.0,
            EquityValue = 1000, DebtValue = 0, CostOfDebt = 0.12, TaxRate = 0.2,
        };

        IReadOnlyList<(double DebtShare, double Wacc)> curve = CostOfCapital.WaccCurve(input);

        Assert.NotEmpty(curve);

        (double share, double _) = curve.OrderBy(p => p.Wacc).First();
        Assert.InRange(share, 0.05, 0.85);
    }

    [Fact]
    public void Dcf_Value_MatchesManualDiscounting()
    {
        var forecast = new List<ForecastYear>();
        for (int t = 0; t < 5; t++)
            forecast.Add(new ForecastYear(1000 * Math.Pow(1.1, t), 0.2, 0.2, 50, 60, 20));

        var input = new DcfInput
        {
            Name = "Компания", Forecast = forecast, DiscountRate = 0.15,
            TerminalGrowth = 0.03, NetDebt = 500, MidYearConvention = false,
        };

        DcfResult result = DiscountedCashFlow.Value(input);

        double manual = 0;
        for (int t = 0; t < 5; t++) manual += forecast[t].FreeCashFlow / Math.Pow(1.15, t + 1);

        double terminal = forecast[4].FreeCashFlow * 1.03 / (0.15 - 0.03);
        manual += terminal / Math.Pow(1.15, 5);

        Assert.Equal(manual, result.EnterpriseValue, 6);
        Assert.Equal(manual - 500, result.EquityValue, 6);
        Assert.InRange(result.TerminalShare, 0, 1);
        Assert.NotEmpty(result.Tornado);
    }

    [Fact]
    public void Dcf_MidYearConvention_RaisesValuation()
    {
        var forecast = Enumerable.Range(0, 5)
            .Select(t => new ForecastYear(1000, 0.2, 0.2, 50, 60, 10))
            .ToList();

        var input = new DcfInput { Forecast = forecast, DiscountRate = 0.15, MidYearConvention = false };

        double endOfYear = DiscountedCashFlow.Value(input).EnterpriseValue;
        double midYear = DiscountedCashFlow.Value(input with { MidYearConvention = true }).EnterpriseValue;

        Assert.True(midYear > endOfYear,
            "Поправка на середину года обязана повышать оценку.");
        Assert.Equal(Math.Sqrt(1.15), midYear / endOfYear, 6);
    }

    [Fact]
    public void Dcf_Simulate_ProducesDistributionAroundBaseCase()
    {
        var forecast = Enumerable.Range(0, 5)
            .Select(t => new ForecastYear(1000 * Math.Pow(1.08, t), 0.18, 0.2, 50, 60, 15))
            .ToList();

        var input = new DcfInput { Forecast = forecast, DiscountRate = 0.16, TerminalGrowth = 0.03 };

        DcfSimulationResult simulation = DiscountedCashFlow.Simulate(input, simulations: 800, seed: 5);

        Assert.True(simulation.Simulations > 700);
        Assert.True(simulation.LowerPercentile < simulation.MedianEquityValue);
        Assert.True(simulation.MedianEquityValue < simulation.UpperPercentile);
        Assert.True(simulation.StandardDeviation > 0);
        Assert.InRange(simulation.ProbabilityOfLoss, 0, 1);
    }

    [Fact]
    public void Comparables_Value_UsesPeerMultiples()
    {
        var peers = new List<Peer>();
        var rng = new Random(11);

        for (int i = 0; i < 12; i++)
        {
            double revenue = 1000 * (0.5 + rng.NextDouble());
            double margin = 0.15 + (0.1 * rng.NextDouble());
            double ebitda = revenue * margin;

            peers.Add(new Peer
            {
                Name = $"Аналог {i + 1}",
                Revenue = revenue,
                Ebitda = ebitda,
                NetIncome = ebitda * 0.5,
                EnterpriseValue = ebitda * 8,
                MarketCapitalization = ebitda * 6,
                Growth = 0.05 + (0.1 * rng.NextDouble()),
            });
        }

        var target = new Peer
        {
            Name = "Компания", Revenue = 1000, Ebitda = 200,
            NetIncome = 100, Growth = 0.1,
        };

        ComparablesResult result = Comparables.Value(target, peers, peerCount: 8);

        Assert.Equal(8, result.SelectedPeers.Count);
        Assert.Equal(3, result.Multiples.Count);

        MultipleStatistic evEbitda = result.Multiples.First(m => m.Name == "EV/EBITDA");
        Assert.Equal(8.0, evEbitda.Median, 6);
        Assert.Equal(1600, evEbitda.ImpliedValue, 6);
    }

    [Fact]
    public void Lbo_Run_DecomposesReturnSources()
    {
        var input = new LboInput
        {
            Name = "Сделка", EntryEbitda = 100, EntryMultiple = 7, ExitMultiple = 7,
            HoldingPeriod = 5, EbitdaGrowth = 0.08, CashConversionDrag = 0.25, TaxRate = 0.2,
            Tranches =
            [
                new DebtTranche("Старший", 300, 0.12, 0.1, 1),
                new DebtTranche("Мезонин", 100, 0.16, 0, 2),
            ],
        };

        LboResult result = LeveragedBuyout.Run(input);

        Assert.Equal(700, result.EntryValue, 6);
        Assert.Equal(300, result.EquityInvested, 6);
        Assert.Equal(4.0, result.EntryLeverage, 6);
        Assert.Equal(5, result.Schedule.Count);

        // При равных мультипликаторах вход и выход эффект мультипликатора нулевой
        Assert.Equal(0, result.MultipleContribution, 6);
        Assert.True(result.GrowthContribution > 0);
        Assert.True(result.DeleveragingContribution > 0);
        Assert.True(result.Irr > 0);
    }

    [Fact]
    public void Lbo_MaximumEntryMultiple_HitsTargetReturn()
    {
        var input = new LboInput
        {
            EntryEbitda = 100, ExitMultiple = 7, HoldingPeriod = 5, EbitdaGrowth = 0.08,
            Tranches = [new DebtTranche("Старший", 300, 0.12, 0.1, 1)],
        };

        double multiple = LeveragedBuyout.MaximumEntryMultiple(input, targetIrr: 0.2);
        LboResult atMultiple = LeveragedBuyout.Run(input with { EntryMultiple = multiple });

        Assert.InRange(atMultiple.Irr, 0.19, 0.21);
    }

    [Fact]
    public void EconomicValueAdded_Compute_SeparatesCreatorsFromDestroyers()
    {
        var units = new List<BusinessUnit>
        {
            new() { Name = "Сильное", Revenue = 1000, OperatingProfit = 200, InvestedCapital = 800 },
            new() { Name = "Слабое", Revenue = 800, OperatingProfit = 40, InvestedCapital = 700 },
        };

        EconomicProfitResult result = EconomicValueAdded.Compute("Компания", units, 0.15);

        UnitEconomicProfit strong = result.Units.First(u => u.Name == "Сильное");
        UnitEconomicProfit weak = result.Units.First(u => u.Name == "Слабое");

        Assert.Equal(200 * 0.8 / 800, strong.Roic, 8);
        Assert.True(strong.EconomicProfit > 0);
        Assert.True(weak.EconomicProfit < 0);
        Assert.Equal(700, result.CapitalDestroying, 6);
        Assert.True(result.ReallocationUpside > 0);
    }

    [Fact]
    public void RealOption_LongstaffSchwartz_ValuesFlexibilityAboveZero()
    {
        var input = new ProjectOptionInput
        {
            Name = "Проект", Option = ProjectOption.Defer,
            ProjectValue = 100, InvestmentCost = 105, Horizon = 3,
            Volatility = 0.4, RiskFreeRate = 0.08, Steps = 12, Paths = 4000, Seed = 7,
        };

        ProjectOptionResult result = LongstaffSchwartz.Value(input);

        Assert.True(result.TotalValue > 0,
            "Опцион на отсрочку не может стоить меньше нуля.");
        Assert.True(result.TotalValue > Math.Max(input.ProjectValue - input.InvestmentCost, 0),
            "Гибкость обязана добавлять стоимость к немедленному исполнению.");
        Assert.InRange(result.ExerciseProbability, 0, 1);
        Assert.True(result.StandardError > 0);
    }

    [Fact]
    public void RealOption_HigherVolatility_RaisesOptionValue()
    {
        var input = new ProjectOptionInput
        {
            Option = ProjectOption.Defer, ProjectValue = 100, InvestmentCost = 100,
            Horizon = 3, RiskFreeRate = 0.08, Steps = 10, Paths = 4000, Seed = 3,
        };

        double low = LongstaffSchwartz.Value(input with { Volatility = 0.2 }).TotalValue;
        double high = LongstaffSchwartz.Value(input with { Volatility = 0.6 }).TotalValue;

        Assert.True(high > low, "Рост неопределённости обязан повышать стоимость опциона.");
    }

    [Fact]
    public void InvestmentCriteria_Appraise_MatchesKnownValues()
    {
        var flows = new Vector(-1000, 400, 400, 400, 400);
        InvestmentAppraisal appraisal = InvestmentCriteria.Appraise(flows, 0.1);

        double manual = -1000;
        for (int t = 1; t <= 4; t++) manual += 400 / Math.Pow(1.1, t);

        Assert.Equal(manual, appraisal.NetPresentValue, 6);
        Assert.True(appraisal.IsAccepted);
        Assert.InRange(appraisal.InternalRateOfReturn, 0.2, 0.24);
        Assert.Equal(1, appraisal.SignChanges);
        Assert.Equal(2.5, appraisal.PaybackPeriod, 6);
        Assert.True(appraisal.ProfitabilityIndex > 1);
        Assert.NotEmpty(appraisal.NpvProfile);
    }

    [Fact]
    public void InvestmentCriteria_ExtendedFunctions_HandleIrregularDates()
    {
        var flows = new List<DatedCashFlow>
        {
            new(new DateOnly(2024, 1, 1), -1000),
            new(new DateOnly(2024, 7, 1), 600),
            new(new DateOnly(2025, 1, 1), 600),
        };

        double rate = InvestmentCriteria.ExtendedInternalRateOfReturn(flows);
        double npv = InvestmentCriteria.ExtendedNetPresentValue(flows, rate);

        Assert.True(double.IsFinite(rate));
        Assert.Equal(0, npv, 6);
        Assert.True(rate > 0.15, $"Доходность {rate:P1} слишком мала для такого потока.");
    }

    [Fact]
    public void Depreciation_Methods_ShareTotalButDifferInPresentValue()
    {
        IReadOnlyList<DepreciationSchedule> schedules =
            Depreciation.CompareMethods(1_000_000, 5, salvage: 0, taxRate: 0.2, discountRate: 0.12);

        Assert.Equal(4, schedules.Count);

        foreach (DepreciationSchedule schedule in schedules)
            Assert.Equal(1_000_000, schedule.Periods.Sum(p => p.Charge), 3);

        DepreciationSchedule straight = schedules.First(s => s.Method == DepreciationMethod.StraightLine);
        DepreciationSchedule accelerated = schedules.First(s => s.Method == DepreciationMethod.SumOfYearsDigits);

        Assert.True(accelerated.PresentValueOfShield > straight.PresentValueOfShield,
            "Ускоренное списание обязано давать более дорогой налоговый щит.");
        Assert.True(accelerated.FrontLoading > straight.FrontLoading);
    }

    [Fact]
    public void LoanSchedule_Annuity_MatchesFormulaAndAmortizesFully()
    {
        LoanScheduleResult loan = LoanSchedule.Build(1_000_000, 0.12, 12, RepaymentType.Annuity);

        double expected = LoanSchedule.AnnuityPayment(1_000_000, 0.01, 12);

        Assert.Equal(12, loan.Payments.Count);
        Assert.Equal(expected, loan.Payments[0].Payment, 4);
        Assert.True(loan.Payments[^1].ClosingBalance < 1e-6, "Кредит должен погаситься полностью.");
        Assert.Equal(1_000_000, loan.Payments.Sum(p => p.Principal), 3);
        Assert.Equal(Math.Pow(1.01, 12) - 1, loan.EffectiveAnnualRate, 8);
    }

    [Fact]
    public void LoanSchedule_Differentiated_CostsLessThanAnnuity()
    {
        LoanScheduleResult annuity = LoanSchedule.Build(1_000_000, 0.15, 24, RepaymentType.Annuity);
        LoanScheduleResult differentiated = LoanSchedule.Build(1_000_000, 0.15, 24, RepaymentType.Differentiated);

        Assert.True(differentiated.TotalInterest < annuity.TotalInterest,
            "Дифференцированный график суммарно дешевле аннуитетного.");
        Assert.True(differentiated.Payments[0].Payment > annuity.Payments[0].Payment,
            "Но первый платёж по нему выше.");
    }

    [Fact]
    public void LoanSchedule_Prepayment_ShortensTermAndSavesInterest()
    {
        LoanScheduleResult plain = LoanSchedule.Build(1_000_000, 0.15, 36);
        LoanScheduleResult withPrepayment = LoanSchedule.Build(
            1_000_000, 0.15, 36, RepaymentType.Annuity, 12, 0,
            [new Prepayment(6, 200_000)]);

        Assert.True(withPrepayment.ActualTerm < plain.ActualTerm);
        Assert.True(withPrepayment.TotalInterest < plain.TotalInterest);
        Assert.True(withPrepayment.PrepaymentSaving > 0);
    }

    [Fact]
    public void LeaseVsBuy_Compare_RanksAllThreeOptions()
    {
        var input = new LeaseVsBuyInput
        {
            Asset = "Оборудование", AssetCost = 10_000_000, Years = 5, TaxRate = 0.2,
            DiscountRate = 0.12, CreditRate = 0.16, LeaseMarkup = 0.09,
        };

        LeaseVsBuyResult result = LeaseVsBuy.Compare(input);

        Assert.Equal(3, result.Options.Count);
        Assert.Equal(result.Options.Min(o => o.PresentValueOfCost),
            result.Options[0].PresentValueOfCost, 6);
        Assert.True(result.BreakEvenLeaseMarkup > 0);
        Assert.All(result.Options, o => Assert.True(o.TaxShield > 0));
    }

    [Fact]
    public void BreakEven_Analyze_MatchesFormulasAndLeverage()
    {
        BreakEvenResult result = BreakEven.Analyze(
            price: 1000, variableCost: 600, fixedCosts: 2_000_000,
            volume: 8000, interest: 300_000, taxRate: 0.2);

        Assert.Equal(5000, result.BreakEvenUnits, 6);
        Assert.Equal(5_000_000, result.BreakEvenRevenue, 6);
        Assert.Equal(400, result.ContributionPerUnit, 6);
        Assert.Equal(0.4, result.ContributionMargin, 6);
        Assert.Equal(0.375, result.MarginOfSafety, 6);

        double contribution = 400 * 8000;
        double operating = contribution - 2_000_000;

        Assert.Equal(contribution / operating, result.OperatingLeverage, 6);
        Assert.Equal(operating / (operating - 300_000), result.FinancialLeverage, 6);
    }

    [Fact]
    public void BreakEven_OptimalStructure_FindsInteriorMinimum()
    {
        CapitalStructureResult result = BreakEven.OptimalStructure(
            "Компания", unleveredCostOfEquity: 0.18, baseCostOfDebt: 0.12,
            taxRate: 0.2, operatingProfit: 200, currentDebtShare: 0.1);

        Assert.NotEmpty(result.Curve);
        Assert.InRange(result.OptimalDebtShare, 0.05, 0.85);
        Assert.Equal(result.Curve.Min(p => p.Wacc), result.MinimumWacc, 8);
        Assert.True(result.MinimumWacc < result.CurrentWacc + 1e-9);
    }
}
