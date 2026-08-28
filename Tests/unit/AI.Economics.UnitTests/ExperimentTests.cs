using AI.DataStructs.Algebraic;
using AI.Economics.Experiments;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

public class ExperimentTests
{
    [Fact]
    public void SampleSize_MatchesTextbookFormula()
    {
        SampleSizeResult result = ExperimentDesign.ForProportions(
            baselineRate: 0.10, relativeEffect: 0.10, alpha: 0.05, power: 0.8);

        // p1 = 0,10, p2 = 0,11, (1,96 + 0,8416)^2 * (0,09 + 0,0979) / 0,0001
        double expected = Math.Pow(1.959963985 + 0.8416212336, 2)
                        * ((0.10 * 0.90) + (0.11 * 0.89)) / (0.01 * 0.01);

        Assert.InRange(result.PerVariant, expected * 0.99, expected * 1.01 + 1);
        Assert.Equal(result.PerVariant * 2, result.Total);
    }

    [Fact]
    public void SampleSize_MultipleVariants_TightensAlpha()
    {
        SampleSizeResult two = ExperimentDesign.ForProportions(0.1, 0.1, variants: 2);
        SampleSizeResult four = ExperimentDesign.ForProportions(0.1, 0.1, variants: 4);

        Assert.Equal(0.05, two.AdjustedAlpha, 9);
        Assert.Equal(0.05 / 3, four.AdjustedAlpha, 9);
        Assert.True(four.PerVariant > two.PerVariant,
            "Поправка на множественность обязана увеличить требуемую выборку.");
    }

    [Fact]
    public void SampleSize_DurationAndWarningsAreReported()
    {
        SampleSizeResult result = ExperimentDesign.ForProportions(
            0.02, 0.05, dailyTraffic: 500);

        Assert.True(result.DaysRequired > 0);
        var interpretation = result.Interpret();
        Assert.Contains(interpretation.Warnings, w => w.Contains("Подглядыв"));
    }

    [Fact]
    public void MinimumDetectableEffect_IsInverseOfSampleSize()
    {
        const double baseline = 0.12;
        const double effect = 0.08;

        SampleSizeResult size = ExperimentDesign.ForProportions(baseline, effect);
        double mde = ExperimentDesign.MinimumDetectableEffect(baseline, size.PerVariant);

        Assert.InRange(mde, effect * 0.9, effect * 1.1);
    }

    [Fact]
    public void Cuped_ReducesVarianceWithoutMovingTheEstimate()
    {
        var rng = RandomEngine.Create(31);
        const int n = 3000;
        const double effect = 0.5;

        var controlPre = new Vector(n);
        var controlPost = new Vector(n);
        var treatmentPre = new Vector(n);
        var treatmentPost = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            // Пользовательский уровень сохраняется между периодами:
            // именно эту часть дисперсии убирает метод
            double levelA = RandomEngine.NextGaussian(rng, 10, 3);
            double levelB = RandomEngine.NextGaussian(rng, 10, 3);

            controlPre[i] = levelA + RandomEngine.NextGaussian(rng, 0, 1);
            controlPost[i] = levelA + RandomEngine.NextGaussian(rng, 0, 1);
            treatmentPre[i] = levelB + RandomEngine.NextGaussian(rng, 0, 1);
            treatmentPost[i] = levelB + effect + RandomEngine.NextGaussian(rng, 0, 1);
        }

        CupedResult result = Cuped.Apply(controlPre, controlPost, treatmentPre, treatmentPost);

        Assert.True(result.VarianceReduction > 0.5,
            $"Снижение дисперсии {result.VarianceReduction:P0} слишком мало при сильной ковариате.");
        Assert.True(result.AdjustedStandardError < result.RawStandardError);
        Assert.InRange(result.AdjustedEffect, effect - 0.1, effect + 0.1);

        // Метод уточняет оценку, а не сдвигает её: расхождение с исходной
        // должно укладываться в её же стандартную ошибку
        Assert.True(Math.Abs(result.AdjustedEffect - result.RawEffect) < 3 * result.RawStandardError,
            $"Сдвиг оценки {result.AdjustedEffect - result.RawEffect:F4} превысил три стандартные ошибки.");
        Assert.True(result.EffectiveSampleGain > 2);
    }

    [Fact]
    public void Cuped_WeakCovariate_GivesSmallGainAndWarns()
    {
        var rng = RandomEngine.Create(5);
        const int n = 1000;

        var controlPre = new Vector(n);
        var controlPost = new Vector(n);
        var treatmentPre = new Vector(n);
        var treatmentPost = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            controlPre[i] = RandomEngine.NextGaussian(rng, 10, 3);
            controlPost[i] = RandomEngine.NextGaussian(rng, 10, 3);
            treatmentPre[i] = RandomEngine.NextGaussian(rng, 10, 3);
            treatmentPost[i] = RandomEngine.NextGaussian(rng, 10.2, 3);
        }

        CupedResult result = Cuped.Apply(controlPre, controlPost, treatmentPre, treatmentPost);

        Assert.True(Math.Abs(result.Correlation) < 0.15);
        Assert.True(result.VarianceReduction < 0.1);
        Assert.Contains(result.Interpret().Warnings, w => w.Contains("ковариат"));
    }

    [Fact]
    public void SequentialTest_DetectsRealEffectEarly()
    {
        var rng = RandomEngine.Create(77);
        const int n = 4000;

        var control = new Vector(n);
        var treatment = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            control[i] = rng.NextDouble() < 0.10 ? 1 : 0;
            treatment[i] = rng.NextDouble() < 0.14 ? 1 : 0;
        }

        SequentialTestResult result = SequentialTest.Run(control, treatment, tau: 0.05, alpha: 0.05);

        Assert.True(result.StoppingPoint > 0, "Реальный эффект обязан быть обнаружен.");
        Assert.True(result.StoppingPoint < n, "Остановка должна произойти раньше конца выборки.");
        Assert.True(result.ObservationsSaved > 0);
        Assert.True(result.FinalPValue < 0.05);
    }

    [Fact]
    public void SequentialTest_HoldsUnderNoEffect()
    {
        var rng = RandomEngine.Create(101);
        const int n = 4000;

        var control = new Vector(n);
        var treatment = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            control[i] = rng.NextDouble() < 0.12 ? 1 : 0;
            treatment[i] = rng.NextDouble() < 0.12 ? 1 : 0;
        }

        SequentialTestResult result = SequentialTest.Run(control, treatment, tau: 0.05, alpha: 0.05);

        // Всегда допустимое p-значение не растёт от подглядывания: оно
        // монотонно невозрастающее по построению
        for (int i = 1; i < result.PValues.Count; i++)
            Assert.True(result.PValues[i] <= result.PValues[i - 1] + 1e-12);

        Assert.True(result.FinalPValue > 0.01,
            $"При отсутствии эффекта p-значение {result.FinalPValue:F4} слишком мало.");
    }

    [Fact]
    public void BayesianAbTest_FavoursBetterVariant()
    {
        BayesianAbResult result = SequentialTest.Bayesian(
            successesA: 100, trialsA: 1000, successesB: 150, trialsB: 1000, draws: 20_000);

        Assert.True(result.ProbabilityBetter > 0.99);
        Assert.True(result.ExpectedLossChoosingB < result.ExpectedLossChoosingA);
        Assert.InRange(result.PosteriorMeanA, 0.09, 0.11);
        Assert.InRange(result.PosteriorMeanB, 0.14, 0.16);
        Assert.True(result.CredibleLow > 0, "Интервал разности не должен накрывать ноль.");
    }

    [Fact]
    public void BayesianAbTest_UndecidedOnEqualVariants()
    {
        BayesianAbResult result = SequentialTest.Bayesian(100, 1000, 102, 1000, draws: 20_000);

        Assert.InRange(result.ProbabilityBetter, 0.3, 0.8);
        Assert.True(result.CredibleLow < 0 && result.CredibleHigh > 0);
        Assert.Contains(result.Interpret().Findings, f => f.Contains("ноль"));
    }

    [Fact]
    public void Bandits_AdaptivePoliciesBeatEqualSplit()
    {
        var rates = new Vector(0.05, 0.08, 0.12);
        string[] names = ["A", "B", "C"];

        IReadOnlyList<BanditSimulationResult> all = Bandits.CompareAll(names, rates, rounds: 20_000, seed: 9);

        BanditSimulationResult equal = all.First(r => r.Policy == BanditPolicy.EqualSplit);
        BanditSimulationResult thompson = all.First(r => r.Policy == BanditPolicy.ThompsonSampling);

        Assert.True(thompson.Regret < equal.Regret,
            $"Сэмплирование Томпсона ({thompson.Regret:F0}) обязано терять меньше равномерного деления ({equal.Regret:F0}).");
        Assert.True(thompson.BestArmShare > 0.7);
        Assert.True(thompson.IdentifiedBestArm);
    }

    [Fact]
    public void Bandits_EqualSplitDistributesTrafficEvenly()
    {
        var rates = new Vector(0.05, 0.12);
        BanditSimulationResult result = Bandits.Simulate(
            ["A", "B"], rates, BanditPolicy.EqualSplit, rounds: 10_000, seed: 4);

        Assert.All(result.Arms, arm => Assert.InRange(arm.TrafficShare, 0.45, 0.55));
        Assert.True(result.Regret > 0, "Равномерное деление обязано терять на худшем варианте.");
        Assert.Contains(result.Interpret().Findings, f => f.Contains("Равномерное деление"));
    }

    [Fact]
    public void Bandits_RegretPathIsMonotone()
    {
        BanditSimulationResult result = Bandits.Simulate(
            ["A", "B", "C"], new Vector(0.05, 0.08, 0.12),
            BanditPolicy.UpperConfidenceBound, rounds: 5000, seed: 12);

        for (int i = 1; i < result.RegretPath.Count; i++)
            Assert.True(result.RegretPath[i] >= result.RegretPath[i - 1] - 1e-12,
                "Накопленные потери не могут убывать.");
    }
}
