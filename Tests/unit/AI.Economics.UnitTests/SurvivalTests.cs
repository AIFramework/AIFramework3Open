using AI.DataStructs.Algebraic;
using AI.Economics.Survival;
using Xunit;

namespace AI.Economics.UnitTests;

public class SurvivalTests
{
    [Fact]
    public void KaplanMeier_KnownExample_MatchesHandComputation()
    {
        // 5 клиентов: события в 2 и 5, цензурирование в 3
        var data = new List<SurvivalRecord>
        {
            new() { Time = 2, Event = true },
            new() { Time = 3, Event = false },
            new() { Time = 5, Event = true },
            new() { Time = 6, Event = false },
            new() { Time = 8, Event = true },
        };

        var km = new KaplanMeier();
        km.Fit(data);

        // S(2) = 1 - 1/5 = 0,8; под риском в момент 5 остаются трое: S(5) = 0,8 * 2/3
        Assert.Equal(0.8, km.SurvivalCurve[0], 6);
        Assert.Equal(0.8 * 2.0 / 3.0, km.SurvivalCurve[1], 6);
        Assert.Equal(0.8, km.SurvivalAt(4), 6);
    }

    [Fact]
    public void KaplanMeier_CensoringIsNotCountedAsChurn()
    {
        var allCensored = new List<SurvivalRecord>
        {
            new() { Time = 1, Event = false },
            new() { Time = 2, Event = false },
            new() { Time = 3, Event = false },
        };

        var km = new KaplanMeier();
        km.Fit(allCensored);

        Assert.Equal(1.0, km.SurvivalAt(10), 6);
    }

    [Fact]
    public void LogRank_DetectsDifferenceBetweenGroups()
    {
        var data = new List<SurvivalRecord>();
        for (int i = 0; i < 40; i++) data.Add(new SurvivalRecord { Time = 1 + (i % 4), Event = true, Group = 0 });
        for (int i = 0; i < 40; i++) data.Add(new SurvivalRecord { Time = 12 + (i % 4), Event = true, Group = 1 });

        (double chi, double p) = KaplanMeier.LogRankTest(data);

        Assert.True(chi > 10);
        Assert.True(p < 0.01);
    }

    [Fact]
    public void Cox_RecoversPositiveEffectOfRiskFactor()
    {
        // Экспоненциальные времена жизни: у группы риска интенсивность втрое
        // выше, значит истинное beta = ln 3. Цензурирование на 40-м периоде
        const double trueBeta = 1.0986122886681098;
        const double baseRate = 0.05;
        const double censorAt = 40;

        var data = new List<SurvivalRecord>();
        var rng = new Random(11);

        for (int i = 0; i < 600; i++)
        {
            double x = i % 2 == 0 ? 1.0 : 0.0;
            double rate = baseRate * Math.Exp(trueBeta * x);
            double time = -Math.Log(1.0 - rng.NextDouble()) / rate;

            data.Add(new SurvivalRecord
            {
                Time = Math.Min(time, censorAt),
                Event = time <= censorAt,
                Covariates = new Vector(x),
            });
        }

        var cox = new CoxProportionalHazards();
        cox.Fit(data, ["risky"]);

        CoxCoefficient beta = cox.Coefficients[0];
        Assert.True(beta.Beta > 0, "Фактор риска обязан увеличивать интенсивность оттока.");
        Assert.InRange(beta.Beta, trueBeta - 0.3, trueBeta + 0.3);
        Assert.InRange(3.0, beta.HazardRatioLower, beta.HazardRatioUpper);
        Assert.True(beta.PValue < 0.001);
        Assert.True(cox.ConcordanceIndex > 0.6);
    }

    [Fact]
    public void Cox_PredictedSurvivalIsMonotoneAndOrdered()
    {
        var data = new List<SurvivalRecord>();
        var rng = new Random(5);

        for (int i = 0; i < 100; i++)
        {
            double x = rng.NextDouble();
            double time = 30 * (1.0 - (0.8 * x)) * (0.5 + rng.NextDouble());
            data.Add(new SurvivalRecord { Time = time, Event = true, Covariates = new Vector(x) });
        }

        var cox = new CoxProportionalHazards();
        cox.Fit(data, ["usage"]);

        Vector low = cox.PredictSurvival(new Vector(0.0));
        Vector high = cox.PredictSurvival(new Vector(1.0));

        for (int i = 1; i < low.Count; i++)
            Assert.True(low[i] <= low[i - 1] + 1e-12, "Кривая дожития обязана быть невозрастающей.");

        Assert.True(high[^1] < low[^1], "Больший риск обязан давать меньшую выживаемость.");
    }

    [Fact]
    public void CompetingRisks_AalenJohansenIsBelowNaiveEstimate()
    {
        var data = new List<SurvivalRecord>();
        for (int i = 0; i < 60; i++)
            data.Add(new SurvivalRecord { Time = 1 + (i % 10), Event = true, Cause = (i % 2) + 1 });

        IReadOnlyList<CumulativeIncidence> cif = CompetingRisks.Analyze(
            data, new Dictionary<int, string> { [1] = "Цена", [2] = "Продукт" });

        Assert.Equal(2, cif.Count);

        // Наивная оценка завышает: она считает конкурирующий уход цензурированием
        foreach (CumulativeIncidence c in cif)
            Assert.True(c.FinalIncidence <= c.FinalNaiveIncidence + 1e-9);

        // Сумма функций инцидентности не превышает единицу
        double total = cif.Sum(c => c.FinalIncidence);
        Assert.True(total <= 1.0 + 1e-9);
        Assert.True(total > 0.9);
    }
}
