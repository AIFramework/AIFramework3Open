using AI.Economics.Equity;
using Xunit;

namespace AI.Economics.UnitTests;

public class CapTableTests
{
    private static CapTable Founders() => new CapTable()
        .AddHolding("Основатель 1", 6_000_000)
        .AddHolding("Основатель 2", 4_000_000);

    [Fact]
    public void Round_InvestorGetsExactlyMoneyOverPostMoney()
    {
        RoundResult result = FundingRound.Execute(Founders(), new RoundInput
        {
            RoundName = "Series A",
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
        });

        Assert.Equal(0.2, result.InvestorOwnership, 9);
        Assert.Equal(0.2, result.CapTable.OwnershipOf("Инвестор"), 9);
        Assert.Equal(25_000_000, result.PostMoneyValuation, 6);

        // Цена акции обязана согласовываться с оценкой после денег
        Assert.Equal(result.PostMoneyValuation / result.TotalSharesAfter, result.PricePerShare, 6);
    }

    [Fact]
    public void Round_PoolShuffle_DilutesFoundersNotInvestor()
    {
        CapTable table = Founders();

        RoundResult withoutPool = FundingRound.Execute(table, new RoundInput
        {
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
        });

        RoundResult withPool = FundingRound.Execute(table, new RoundInput
        {
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
            TargetOptionPoolPost = 0.10,
        });

        // Инвестор получает свои 20 % в обоих случаях
        Assert.Equal(0.2, withPool.InvestorOwnership, 9);

        // Пул создан «до денег» — платят за него основатели
        Assert.True(withPool.CapTable.OwnershipOf("Основатель 1")
                  < withoutPool.CapTable.OwnershipOf("Основатель 1"));

        Assert.Equal(0.10, withPool.CapTable.UnallocatedPool / withPool.TotalSharesAfter, 9);

        // Эффективная оценка для основателей ниже заявленной ровно на цену пула
        Assert.True(withPool.EffectivePreMoneyForFounders < 20_000_000);
    }

    [Fact]
    public void Round_SafeWithCap_ConvertsBelowRoundPrice()
    {
        RoundResult result = FundingRound.Execute(Founders(), new RoundInput
        {
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
            ConvertingNotes =
            [
                new SafeNote { Holder = "Ангел", Amount = 500_000, ValuationCap = 5_000_000 },
            ],
        });

        NoteConversion conversion = Assert.Single(result.Conversions);

        Assert.Equal("Потолок оценки", conversion.PriceDriver);
        Assert.True(conversion.ConversionPrice < result.PricePerShare);
        Assert.True(conversion.EffectiveValuation < result.PostMoneyValuation);

        // Ангел за 0,5 млн получил больше, чем инвестор раунда за ту же сумму
        double investorSharesPerRuble = result.InvestorShares / 5_000_000;
        Assert.True(conversion.Shares / 500_000 > investorSharesPerRuble);
    }

    [Fact]
    public void Round_ConvertibleNote_AccruesInterest()
    {
        RoundResult result = FundingRound.Execute(Founders(), new RoundInput
        {
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
            ConvertingNotes =
            [
                new SafeNote
                {
                    Holder = "Заём",
                    Amount = 1_000_000,
                    Discount = 0.2,
                    InterestRate = 0.08,
                    YearsAccrued = 2,
                },
            ],
        });

        NoteConversion conversion = Assert.Single(result.Conversions);
        Assert.Equal(1_160_000, conversion.Amount, 6);
        Assert.Equal("Скидка к раунду", conversion.PriceDriver);
        Assert.Equal(result.PricePerShare * 0.8, conversion.ConversionPrice, 4);
    }

    [Fact]
    public void Round_OwnershipSumsToOne()
    {
        RoundResult result = FundingRound.Execute(Founders(), new RoundInput
        {
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
            TargetOptionPoolPost = 0.12,
            ConvertingNotes =
            [
                new SafeNote { Holder = "Ангел А", Amount = 300_000, ValuationCap = 6_000_000 },
                new SafeNote { Holder = "Ангел Б", Amount = 200_000, Discount = 0.25 },
            ],
        });

        double total = result.CapTable.Ownership().Sum(r => r.Ownership);
        Assert.Equal(1.0, total, 9);
        Assert.Equal(0.2, result.InvestorOwnership, 9);
    }

    [Fact]
    public void Round_MostFavouredNation_AdoptsBestTerms()
    {
        RoundResult result = FundingRound.Execute(Founders(), new RoundInput
        {
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
            ConvertingNotes =
            [
                new SafeNote { Holder = "Ранний", Amount = 250_000, MostFavoredNation = true },
                new SafeNote { Holder = "Поздний", Amount = 250_000, ValuationCap = 4_000_000 },
            ],
        });

        NoteConversion mfn = result.Conversions.First(c => c.Holder == "Ранний");
        NoteConversion other = result.Conversions.First(c => c.Holder == "Поздний");

        Assert.Equal(other.ConversionPrice, mfn.ConversionPrice, 6);
    }

    /// <summary>Компания после раунда: 20 % у инвестора с преференцией 1x.</summary>
    private static CapTable AfterSeriesA(PreferenceType preference, double cap = double.NaN)
    {
        var table = Founders();
        return FundingRound.Execute(table, new RoundInput
        {
            RoundName = "Series A",
            PreMoneyValuation = 20_000_000,
            Investment = 5_000_000,
            Preference = preference,
            ParticipationCap = cap,
        }).CapTable;
    }

    [Fact]
    public void Waterfall_TotalPayoutsEqualExitValue()
    {
        CapTable table = AfterSeriesA(PreferenceType.NonParticipating);

        foreach (double exit in new[] { 0d, 1_000_000, 5_000_000, 25_000_000, 100_000_000 })
        {
            ExitWaterfallResult result = ExitWaterfall.Distribute(table, exit);
            Assert.Equal(exit, result.Payouts.Sum(p => p.Payout), 4);
        }
    }

    [Fact]
    public void Waterfall_LowExit_PreferenceLeavesNothingToFounders()
    {
        CapTable table = AfterSeriesA(PreferenceType.NonParticipating);

        // Продажа за 5 млн равна размеру преференции — основателям ноль
        ExitWaterfallResult result = ExitWaterfall.Distribute(table, 5_000_000);

        Assert.Equal(5_000_000, result.Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout), 4);
        Assert.Equal(0, result.Payouts.Where(p => p.Holder.StartsWith("Основатель")).Sum(p => p.Payout), 4);
    }

    [Fact]
    public void Waterfall_HighExit_NonParticipatingConverts()
    {
        CapTable table = AfterSeriesA(PreferenceType.NonParticipating);
        ExitWaterfallResult result = ExitWaterfall.Distribute(table, 100_000_000);

        ClassOutcome seriesA = result.Classes.First(c => c.ClassName == "Series A");
        Assert.True(seriesA.Converted);

        // Сконвертировавшись, инвестор получает ровно свою долю
        Assert.Equal(20_000_000, result.Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout), 3);
    }

    [Fact]
    public void Waterfall_ParticipatingGetsPreferenceAndResidual()
    {
        CapTable plain = AfterSeriesA(PreferenceType.NonParticipating);
        CapTable participating = AfterSeriesA(PreferenceType.Participating);

        const double exit = 30_000_000;
        double plainPayout = ExitWaterfall.Distribute(plain, exit)
            .Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout);
        double participatingPayout = ExitWaterfall.Distribute(participating, exit)
            .Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout);

        // Преференция 5 млн плюс 20 % от остатка 25 млн = 10 млн
        Assert.Equal(10_000_000, participatingPayout, 3);
        Assert.True(participatingPayout > plainPayout);
    }

    [Fact]
    public void Waterfall_ParticipationCapIsRespectedAndConversionTakesOver()
    {
        CapTable capped = AfterSeriesA(PreferenceType.Participating, cap: 2.0);

        // При умеренном выходе работает потолок 2x = 10 млн
        ExitWaterfallResult moderate = ExitWaterfall.Distribute(capped, 40_000_000);
        double moderatePayout = moderate.Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout);
        Assert.True(moderatePayout <= 10_000_000 + 1e-6);

        // При крупном выходе выгоднее конвертация: 20 % от 100 млн больше потолка
        ExitWaterfallResult large = ExitWaterfall.Distribute(capped, 100_000_000);
        double largePayout = large.Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout);
        Assert.Equal(20_000_000, largePayout, 3);
        Assert.True(large.Classes.First(c => c.ClassName == "Series A").Converted);
    }

    [Fact]
    public void Waterfall_SeniorityIsRespected()
    {
        CapTable afterA = AfterSeriesA(PreferenceType.NonParticipating);
        CapTable afterB = FundingRound.Execute(afterA, new RoundInput
        {
            RoundName = "Series B",
            InvestorName = "Инвестор B",
            PreMoneyValuation = 60_000_000,
            Investment = 15_000_000,
        }).CapTable;

        // Денег хватает только на старшую преференцию Series B
        ExitWaterfallResult result = ExitWaterfall.Distribute(afterB, 15_000_000);

        Assert.Equal(15_000_000, result.Payouts.Where(p => p.Holder == "Инвестор B").Sum(p => p.Payout), 3);
        Assert.Equal(0, result.Payouts.Where(p => p.Holder == "Инвестор").Sum(p => p.Payout), 3);
    }

    [Fact]
    public void PayoutCurve_IsMonotoneInExitValue()
    {
        CapTable table = AfterSeriesA(PreferenceType.NonParticipating);
        (var exits, var payouts) = ExitWaterfall.PayoutCurve(table, 60_000_000, 40);

        Assert.Equal(40, exits.Count);

        foreach ((string holder, var curve) in payouts)
            for (int i = 1; i < curve.Count; i++)
                Assert.True(curve[i] >= curve[i - 1] - 1e-6,
                    $"Выплата держателю «{holder}» не может падать при росте цены сделки.");
    }
}
