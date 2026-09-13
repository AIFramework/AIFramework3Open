using AI.Psychology.Decision;
using AI.Psychology.Learning;
using AI.Psychology.Memory;
using AI.Psychology.Psychophysics;
using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Когнитивные модели проверяются независимыми ответами: замкнутыми формулами, численным
/// интегрированием, моделированием и опубликованными данными. Площадь под ROC — трапециями по самой
/// кривой, плотность диффузии — интегралом против вероятности границы, моделирование диффузии —
/// против формул, забывание — против таблицы Эббингауза.
/// </summary>
public class CognitiveModelsTests
{
    #region Психофизика

    [Fact]
    public void SignalDetection_KnownValues_AndRoundTrip()
    {
        // z(0,8413) = 1 и z(0,1587) = −1: d′ = 2 и нейтральный критерий
        SignalDetectionResult symmetric = SignalDetection.FromRates(0.8413447, 0.1586553);

        Assert.Equal(2.0, symmetric.Sensitivity, 4);
        Assert.Equal(0.0, symmetric.Criterion, 4);
        Assert.Equal(StatInference.NormalCdf(Math.Sqrt(2)), symmetric.AreaUnderCurve, 6);

        SignalDetectionResult roundTrip = SignalDetection.FromRates(
            SignalDetectionResult.PredictedHitRate(1.3, 0.4), SignalDetectionResult.PredictedFalseAlarmRate(1.3, 0.4));

        // Φ и её квантиль в ядре обратны друг другу почти до двойной точности
        Assert.Equal(1.3, roundTrip.Sensitivity, 1e-11);
        Assert.Equal(0.4, roundTrip.Criterion, 1e-11);
        Assert.True(roundTrip.LogBeta > 0);
        Assert.Equal(0.5, SignalDetection.FromRates(0.3, 0.3).APrime, 12);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.5)]
    public void SignalDetection_AreaUnderCurve_MatchesTrapezoidsOverRoc(double sensitivity)
    {
        IReadOnlyList<(double FalseAlarmRate, double HitRate)> roc = SignalDetection.RocCurve(sensitivity, 4001);
        double area = 0;

        for (int i = 1; i < roc.Count; i++)
            area += (roc[i].FalseAlarmRate - roc[i - 1].FalseAlarmRate) * (roc[i].HitRate + roc[i - 1].HitRate) / 2;

        Assert.Equal(SignalDetection.FromRates(
            SignalDetectionResult.PredictedHitRate(sensitivity, 0),
            SignalDetectionResult.PredictedFalseAlarmRate(sensitivity, 0)).AreaUnderCurve, area, 1e-5);
    }

    [Fact]
    public void SignalDetection_ExtremeRates_AreCorrectedOrInfinite()
    {
        SignalDetectionResult logLinear = SignalDetection.FromCounts(50, 0, 10, 40);
        SignalDetectionResult halfCount = SignalDetection.FromCounts(50, 0, 10, 40, RateCorrection.HalfCount);
        SignalDetectionResult none = SignalDetection.FromCounts(50, 0, 10, 40, RateCorrection.None);

        Assert.Equal(50.5 / 51, logLinear.HitRate, 12);
        Assert.Equal(1 - (1.0 / 100), halfCount.HitRate, 12);
        Assert.True(logLinear.Corrected && halfCount.Corrected);
        Assert.Equal(double.PositiveInfinity, none.Sensitivity);
        Assert.Contains(logLinear.Interpret().Findings, f => f.Contains("поправкой", StringComparison.Ordinal));
    }

    [Fact]
    public void Weber_Fechner_Stevens_AreConsistent()
    {
        // Равные доли Вебера дают равные приросты ощущения по Фехнеру при любой силе стимула
        double stepLow = PsychophysicalLaws.FechnerSensation(10 * 1.1, 1) - PsychophysicalLaws.FechnerSensation(10, 1);
        double stepHigh = PsychophysicalLaws.FechnerSensation(1000 * 1.1, 1) - PsychophysicalLaws.FechnerSensation(1000, 1);

        Assert.Equal(stepLow, stepHigh, 12);
        Assert.Equal(0, PsychophysicalLaws.FechnerSensation(0.5, 1));
        Assert.Equal(5, PsychophysicalLaws.JustNoticeableDifference(100, 0.05), 12);

        // Оценки величины с мультипликативным шумом: показатель восстанавливается по двойным логарифмам
        var rng = new Random(1);
        double[] intensities = Enumerable.Range(1, 40).Select(i => i * 5.0).ToArray();
        double[] magnitudes = intensities
            .Select(i => PsychophysicalLaws.StevensMagnitude(i, 0.67, 2) * Math.Exp(0.1 * RandomEngine.NextGaussian(rng)))
            .ToArray();

        StevensFit fit = PsychophysicalLaws.FitStevens(intensities, magnitudes);

        Assert.Equal(0.67, fit.Exponent, 0.05);
        Assert.True(fit.RSquared > 0.95);
        Assert.Equal(0.67, PsychophysicalLaws.StevensExponents["громкость, тон 3000 Гц"]);
    }

    [Fact]
    public void HickHyman_And_Fitts()
    {
        Assert.Equal(3, ResponseTimeLaws.InformationBits(Enumerable.Repeat(1.0 / 8, 8).ToArray()), 12);
        Assert.Equal(1.5, ResponseTimeLaws.InformationBits([0.5, 0.25, 0.25]), 12);

        // Хайман: время следует информации, а не числу вариантов
        double[] bits = [0, 1, 1.5, 2, 3];
        double[] times = bits.Select(h => ResponseTimeLaws.ChoiceTime(0.2, 0.15, h)).ToArray();
        LinearLawFit fit = ResponseTimeLaws.Fit(bits, times);

        Assert.Equal(0.2, fit.Intercept, 12);
        Assert.Equal(0.15, fit.Slope, 12);
        Assert.Equal(1, fit.RSquared, 12);

        Assert.Equal(Math.Log2(17), ResponseTimeLaws.IndexOfDifficulty(256, 16), 12);
        Assert.Equal(4.1327, ResponseTimeLaws.EffectiveWidthFactor, 4);
        Assert.Equal(Math.Log2(17) / 0.8, ResponseTimeLaws.Throughput(256, 16, 0.8), 12);
    }

    [Fact]
    public void CueCombination_WeightsByReliability_AndReducesVariance()
    {
        CombinedEstimate combined = CueCombination.Optimal([(10, 1), (14, 2)]);

        Assert.Equal(0.8, combined.Weights[0], 12);
        Assert.Equal(10.8, combined.Mean, 12);
        Assert.Equal(Math.Sqrt(0.8), combined.StandardDeviation, 12);

        // Моделирование: взвешенное среднее двух шумных признаков действительно разбросано на √0,8
        var rng = new Random(2);
        double[] samples = Enumerable.Range(0, 200_000)
            .Select(_ => (0.8 * (1 * RandomEngine.NextGaussian(rng))) + (0.2 * (2 * RandomEngine.NextGaussian(rng))))
            .ToArray();
        double sd = Math.Sqrt(samples.Average(s => s * s));

        Assert.Equal(Math.Sqrt(0.8), sd, 0.01);

        CombinedEstimate withPrior = CueCombination.WithPrior(0, 1, [(10, 1)]);
        Assert.Equal(5, withPrior.Mean, 12);
    }

    #endregion

    #region Обучение

    [Fact]
    public void RescorlaWagner_AcquisitionAndExtinction_FollowClosedForm()
    {
        var model = new RescorlaWagnerModel(salience: 0.3, learningRate: 0.5);

        for (int n = 1; n <= 30; n++)
        {
            model.Trial(["A"], 1);
            Assert.Equal(RescorlaWagnerModel.AcquisitionCurve(0.3, 0.5, 1, n), model.Strength("A"), 12);
        }

        double peak = model.Strength("A");

        for (int n = 1; n <= 20; n++)
            model.Trial(["A"], 0);

        Assert.Equal(peak * Math.Pow(1 - 0.15, 20), model.Strength("A"), 12);
    }

    [Fact]
    public void RescorlaWagner_Blocking_Overshadowing_ConditionedInhibition()
    {
        // Блокирование: A заранее предсказывает подкрепление, и B в паре с ним почти ничему не учится
        var blocked = new RescorlaWagnerModel(0.3);
        blocked.Train(Enumerable.Repeat(new ConditioningTrial(["A"], 1), 60));
        blocked.Train(Enumerable.Repeat(new ConditioningTrial(["A", "B"], 1), 60));

        // Затенение: без предобучения A и B делят асимптоту поровну
        var control = new RescorlaWagnerModel(0.3);
        control.Train(Enumerable.Repeat(new ConditioningTrial(["A", "B"], 1), 120));

        Assert.True(blocked.Strength("B") < 0.01, $"B {blocked.Strength("B")}");
        Assert.Equal(0.5, control.Strength("B"), 6);

        // Условное торможение: A+ вперемешку с AX− даёт X отрицательную силу, в пределе −1
        var inhibition = new RescorlaWagnerModel(0.2);

        for (int n = 0; n < 1000; n++)
        {
            inhibition.Trial(["A"], 1);
            inhibition.Trial(["A", "X"], 0);
        }

        Assert.Equal(1, inhibition.Strength("A"), 3);
        Assert.Equal(-1, inhibition.Strength("X"), 3);
    }

    #endregion

    #region Решения

    [Fact]
    public void Softmax_IsLuceWithExponentialStrengths()
    {
        double[] values = [1, 2, 0.5];
        double[] softmax = ChoiceRules.Softmax(values, 1.7);
        double[] luce = ChoiceRules.Luce(values.Select(v => Math.Exp(1.7 * v)).ToArray());

        for (int i = 0; i < values.Length; i++)
            Assert.Equal(luce[i], softmax[i], 12);

        Assert.All(ChoiceRules.Softmax(values, 0), p => Assert.Equal(1.0 / 3, p, 12));
        Assert.True(ChoiceRules.Softmax(values, 50)[1] > 0.999);
    }

    [Theory]
    [InlineData(1.5, 1.2, 0.5)]
    [InlineData(0.8, 1.5, 0.3)]
    [InlineData(-1.0, 1.0, 0.6)]
    public void DriftDiffusion_SimulationMatchesClosedForms(double drift, double separation, double start)
    {
        var model = new DriftDiffusionModel(drift, separation, start, 0.3);
        IReadOnlyList<DiffusionTrial> trials = model.Simulate(3000, new Random(3), 1e-4);

        double accuracy = trials.Count(t => t.Upper) / 3000.0;
        double meanTime = trials.Average(t => t.ResponseTime);
        double se = Math.Sqrt(model.UpperProbability * (1 - model.UpperProbability) / 3000);

        Assert.True(Math.Abs(accuracy - model.UpperProbability) < 4 * se, $"доля {accuracy} против {model.UpperProbability}");
        Assert.Equal(model.MeanResponseTime, meanTime, 0.02);
    }

    [Fact]
    public void DriftDiffusion_VarianceMatchesSimulation()
    {
        var model = new DriftDiffusionModel(1.2, 1.4, 0.5, 0.2);
        double[] times = model.Simulate(4000, new Random(4), 1e-4).Select(t => t.ResponseTime).ToArray();
        double mean = times.Average();
        double variance = times.Sum(t => (t - mean) * (t - mean)) / (times.Length - 1);

        Assert.Equal(model.DecisionTimeVariance, variance, model.DecisionTimeVariance * 0.1);
        _ = Assert.Throws<NotSupportedException>(() => new DriftDiffusionModel(1, 1, 0.3).DecisionTimeVariance);
    }

    [Theory]
    [InlineData(1.5, 1.2, 0.5)]
    [InlineData(-0.7, 2.0, 0.35)]
    [InlineData(0.0, 1.0, 0.5)]
    public void DriftDiffusion_DensitiesIntegrateToBoundaryProbabilities(double drift, double separation, double start)
    {
        var model = new DriftDiffusionModel(drift, separation, start);

        // Интеграл плотности по времени — независимая проверка формулы вероятности и среднего
        double lower = 0, upper = 0, mean = 0;
        const double Step = 1e-4;

        for (double t = Step / 2; t < 30; t += Step)
        {
            double fl = model.LowerDensity(t), fu = model.UpperDensity(t);
            lower += fl * Step;
            upper += fu * Step;
            mean += t * (fl + fu) * Step;
        }

        Assert.Equal(1 - model.UpperProbability, lower, 4);
        Assert.Equal(model.UpperProbability, upper, 4);
        Assert.Equal(model.MeanDecisionTime, mean, 4);
    }

    [Fact]
    public void EzDiffusion_InvertsTheModel_AndRecoversFromSimulation()
    {
        var truth = new DriftDiffusionModel(1.3, 1.1, 0.5, 0.35);
        DriftDiffusionModel exact = EzDiffusion.Estimate(truth.UpperProbability, truth.DecisionTimeVariance, truth.MeanResponseTime);

        Assert.Equal(truth.Drift, exact.Drift, 8);
        Assert.Equal(truth.BoundarySeparation, exact.BoundarySeparation, 8);
        Assert.Equal(truth.NonDecisionTime, exact.NonDecisionTime, 8);

        DiffusionTrial[] correct = truth.Simulate(5000, new Random(5), 1e-4).Where(t => t.Upper).ToArray();
        double accuracy = correct.Length / 5000.0;
        double mean = correct.Average(t => t.ResponseTime);
        double variance = correct.Sum(t => (t.ResponseTime - mean) * (t.ResponseTime - mean)) / (correct.Length - 1);

        DriftDiffusionModel estimated = EzDiffusion.Estimate(accuracy, variance, mean);

        Assert.Equal(truth.Drift, estimated.Drift, 0.15);
        Assert.Equal(truth.BoundarySeparation, estimated.BoundarySeparation, 0.08);
        Assert.Equal(truth.NonDecisionTime, estimated.NonDecisionTime, 0.03);
    }

    [Fact]
    public void ProspectTheory_ReducesToExpectedValue_WhenRiskNeutral()
    {
        var rng = new Random(6);

        for (int trial = 0; trial < 50; trial++)
        {
            double[] weights = Enumerable.Range(0, 4).Select(_ => rng.NextDouble()).ToArray();
            Outcome[] prospect = weights.Select(w => new Outcome(-100 + (200 * rng.NextDouble()), w / weights.Sum())).ToArray();

            Assert.Equal(ProspectTheory.ExpectedValue(prospect),
                ProspectTheory.Evaluate(prospect, ProspectTheoryParameters.RiskNeutral), 9);
        }
    }

    [Fact]
    public void ProspectTheory_FourfoldPatternAndLossAversion()
    {
        static double Premium(double value, double probability)
        {
            Outcome[] prospect = [new(value, probability), new(0, 1 - probability)];
            return ProspectTheory.CertaintyEquivalent(prospect) - ProspectTheory.ExpectedValue(prospect);
        }

        Assert.True(Premium(100, 0.05) > 0, "маловероятный выигрыш: поиск риска, как с лотереей");
        Assert.True(Premium(100, 0.95) < 0, "почти верный выигрыш: избегание риска");
        Assert.True(Premium(-100, 0.05) < 0, "маловероятная потеря: избегание риска, как со страховкой");
        Assert.True(Premium(-100, 0.95) > 0, "почти верная потеря: поиск риска");

        // Честная монета ±100 отвергается: потери весят больше
        Assert.True(ProspectTheory.Evaluate([new(100, 0.5), new(-100, 0.5)]) < 0);

        // Обратная S-образная функция: малые вероятности переоцениваются, большие недооцениваются
        Assert.True(ProspectTheory.Weight(0.01, 0.61) > 0.01);
        Assert.True(ProspectTheory.Weight(0.99, 0.61) < 0.99);

        // Кумулятивные веса не нарушают доминирование: улучшение исхода только повышает ценность
        Assert.True(ProspectTheory.Evaluate([new(60, 0.3), new(10, 0.7)]) > ProspectTheory.Evaluate([new(50, 0.3), new(10, 0.7)]));
        Assert.Equal(42, ProspectTheory.CertaintyEquivalent([new(42, 1)]), 9);
    }

    [Fact]
    public void Discounting_HyperbolicReversesPreference_ExponentialDoesNot()
    {
        // 50 сейчас против 100 через 10 дней: вблизи берут меньшее раннее, издалека — большее позднее
        bool NearPrefersSmaller(Func<double, double, double, double> discount)
            => discount(50, 0, 0.2) > discount(100, 10, 0.2);

        bool FarPrefersSmaller(Func<double, double, double, double> discount)
            => discount(50, 30, 0.2) > discount(100, 40, 0.2);

        Assert.True(NearPrefersSmaller(Discounting.Hyperbolic) && !FarPrefersSmaller(Discounting.Hyperbolic));
        Assert.Equal(NearPrefersSmaller(Discounting.Exponential), FarPrefersSmaller(Discounting.Exponential));

        double[] delays = [1, 7, 30, 90, 365];
        double[] fractions = delays.Select(d => Discounting.Hyperbolic(1, d, 0.03)).ToArray();

        Assert.Equal(0.03, Discounting.FitHyperbolicRate(delays, fractions), 12);

        // Площадь под гиперболой на плотной сетке сходится к ln(1 + kD)/(kD)
        double[] grid = Enumerable.Range(1, 2000).Select(i => i * 365.0 / 2000).ToArray();
        double area = Discounting.AreaUnderCurve(grid, grid.Select(d => Discounting.Hyperbolic(1, d, 0.03)).ToArray());
        double kd = 0.03 * 365;

        Assert.Equal(Math.Log(1 + kd) / kd, area, 4);
    }

    #endregion

    #region Память

    [Fact]
    public void Ebbinghaus_FormulaDescribesHisOwnData()
    {
        foreach ((double minutes, double savings) in Forgetting.EbbinghausData)
            Assert.True(Math.Abs(Forgetting.EbbinghausSavings(minutes) - savings) < 0.035, $"{minutes} мин");

        // Степенная кривая теряет медленнее экспоненты с тем же начальным наклоном
        Assert.True(Forgetting.Power(100, 1, 1) > Forgetting.Exponential(100, 1));
        Assert.Equal(1, Forgetting.Exponential(0, 5), 12);
    }

    [Fact]
    public void Actr_PowerLawsOfForgettingAndPractice()
    {
        Assert.Equal(-0.5 * Math.Log(100), ActrMemory.BaseLevelActivation([100]), 12);
        Assert.Equal(Math.Log(5) - (0.5 * Math.Log(60)), ActrMemory.BaseLevelActivation(Enumerable.Repeat(60.0, 5)), 12);
        Assert.Equal(double.NegativeInfinity, ActrMemory.BaseLevelActivation([]));
        Assert.Equal(0.5, ActrMemory.RetrievalProbability(-1.2, -1.2, 0.4), 12);
        Assert.Equal(0.3 * Math.Exp(1), ActrMemory.RetrievalLatency(-1, 0.3), 12);
    }

    [Fact]
    public void Actr_SpacingEffectNeedsActivationDependentDecay()
    {
        double[] massed = [0, 1, 2, 3];
        double[] spaced = [0, 600, 1200, 1800];
        const double Week = 7 * 24 * 3600;

        // С постоянным затуханием сплошное повторение не хуже распределённого: его следы моложе
        double standardMassed = ActrMemory.BaseLevelActivation(massed.Select(t => 3 + Week - t));
        double standardSpaced = ActrMemory.BaseLevelActivation(spaced.Select(t => 1800 + Week - t));

        Assert.True(standardMassed > standardSpaced);

        // У Павлика и Андерсона распределённое повторение через неделю выигрывает
        double pavlikMassed = ActrMemory.SpacedActivation(massed, 3 + Week, 0.25, 0.18);
        double pavlikSpaced = ActrMemory.SpacedActivation(spaced, 1800 + Week, 0.25, 0.18);

        Assert.True(pavlikSpaced > pavlikMassed + 0.3, $"{pavlikSpaced} против {pavlikMassed}");
    }

    #endregion
}
