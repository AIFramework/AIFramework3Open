using AI.Psychology.Psychometrics;
using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Психометрия проверяется восстановлением известного: пункты моделируются с заданными нагрузками и
/// надёжностью, ответы — с заданными параметрами заданий, и методы обязаны их вернуть. Факторный
/// анализ на точной корреляционной матрице восстанавливает нагрузки без выборочной ошибки. Доли
/// станайнов и стэнов сверяются с табличными.
/// </summary>
public class PsychometricsTests
{
    #region Стандартные шкалы

    [Fact]
    public void StandardScores_TableProportions()
    {
        // Доли станайнов 4-7-12-17-20-17-12-7-4 % и стэнов 2,3-4,4-9,2-15,0-19,1 % — по краям полос
        double[] stanines = [4, 7, 12, 17, 20, 17, 12, 7, 4];
        double[] stens = [2.3, 4.4, 9.2, 15.0, 19.1, 19.1, 15.0, 9.2, 4.4, 2.3];

        for (int s = 1; s <= 9; s++)
            Assert.Equal(stanines[s - 1], 100 * Share(z => StandardScores.Stanine(z) == s), 0.6);

        for (int s = 1; s <= 10; s++)
            Assert.Equal(stens[s - 1], 100 * Share(z => StandardScores.Sten(z) == s), 0.1);

        Assert.Equal(5, StandardScores.Stanine(0));
        Assert.Equal(6, StandardScores.Sten(0));
        Assert.Equal(65, StandardScores.TScore(1.5), 12);
        Assert.Equal(130, StandardScores.DeviationIq(2), 12);
        Assert.Equal(84.134, StandardScores.PercentileFromZ(1), 3);
        Assert.Equal(50, StandardScores.PercentileRank([1, 2, 3, 4], 2.5), 12);
        Assert.Equal(37.5, StandardScores.PercentileRank([1, 2, 3, 4], 2), 12);
    }

    // Доля нормального распределения, попадающая в полосу, — по значениям в центрах мелких ячеек
    private static double Share(Func<double, bool> inBand)
    {
        double total = 0;
        const double Step = 1e-4;

        for (double z = -8; z < 8; z += Step)
        {
            if (inBand(z + (Step / 2)))
                total += StatInference.NormalCdf(z + Step) - StatInference.NormalCdf(z);
        }

        return total;
    }

    #endregion

    #region Надёжность

    [Fact]
    public void Reliability_TauEquivalentItems_MatchTheory()
    {
        // Истинный балл с дисперсией 1 и шум с дисперсией 1.5: надёжность пункта 0,4, шкалы из 8 пунктов — 8·0,4/(1 + 7·0,4)
        double[,] scores = Items(new Random(1), 3000, Enumerable.Repeat(1.0, 8).ToArray(), Math.Sqrt(1.5));
        ReliabilityResult result = Reliability.Analyze(scores);
        double theory = 8 * 0.4 / (1 + (7 * 0.4));

        Assert.Equal(theory, result.CronbachAlpha, 0.02);
        Assert.Equal(result.CronbachAlpha, result.McDonaldOmega, 0.02);
        Assert.Equal(theory, result.SplitHalf, 0.03);
        Assert.Equal(IndependentAlpha(scores), result.CronbachAlpha, 12);
        Assert.Equal(Reliability.CronbachAlpha(scores), result.CronbachAlpha, 12);
    }

    [Fact]
    public void Reliability_UnequalLoadings_OmegaExceedsAlpha()
    {
        double[] raw = [1.6, 1.2, 0.8, 0.5, 0.3, 1.4];
        double[,] scores = Items(new Random(2), 3000, raw, 1.0);
        ReliabilityResult result = Reliability.Analyze(scores);

        // Теория для стандартизованных пунктов: λ = l/√(l² + 1), rᵢⱼ = λᵢλⱼ
        double[] loadings = raw.Select(l => l / Math.Sqrt((l * l) + 1)).ToArray();
        double sum = loadings.Sum(), squares = loadings.Sum(l => l * l);
        double omega = sum * sum / ((sum * sum) + loadings.Length - squares);
        double meanR = ((sum * sum) - squares) / (loadings.Length * (loadings.Length - 1));
        double standardizedAlpha = loadings.Length * meanR / (1 + ((loadings.Length - 1) * meanR));

        Assert.Equal(omega, result.McDonaldOmega, 0.02);
        Assert.Equal(standardizedAlpha, result.StandardizedAlpha, 0.02);

        // ω сравнивается со стандартизованной α: обе посчитаны для пунктов с единичной дисперсией
        Assert.True(result.McDonaldOmega > result.StandardizedAlpha + 0.01, $"ω {result.McDonaldOmega}, α {result.StandardizedAlpha}");
    }

    [Fact]
    public void Reliability_NoiseAndReversedItems_AreFlagged()
    {
        double[,] scores = Items(new Random(3), 1500, [1, 1, 1, 1, 0, -1], 1.0);
        ReliabilityResult result = Reliability.Analyze(scores);

        Assert.True(result.AlphaIfItemDeleted[4] > result.CronbachAlpha);
        Assert.True(result.CorrectedItemTotalCorrelations[5] < 0);

        var interpretation = result.Interpret();
        Assert.Contains(interpretation.Findings, f => f.Contains("перекодировать", StringComparison.Ordinal));
    }

    [Fact]
    public void SpearmanBrown_AndRequiredLength_AreInverse()
    {
        Assert.Equal(0.75, Reliability.SpearmanBrown(0.6, 2), 12);
        Assert.Equal(2, Reliability.RequiredLengthFactor(0.6, 0.75), 12);
        Assert.Equal(0.6, Reliability.SpearmanBrown(Reliability.SpearmanBrown(0.6, 3), 1.0 / 3), 12);
    }

    // Пункты: нагрузка на общий истинный балл плюс независимый шум
    private static double[,] Items(Random rng, int respondents, double[] loadings, double noise)
    {
        var scores = new double[respondents, loadings.Length];

        for (int i = 0; i < respondents; i++)
        {
            double trueScore = RandomEngine.NextGaussian(rng);

            for (int j = 0; j < loadings.Length; j++)
                scores[i, j] = (loadings[j] * trueScore) + (noise * RandomEngine.NextGaussian(rng));
        }

        return scores;
    }

    // α через разложение дисперсии суммы: сумма всех ковариаций против суммы дисперсий
    private static double IndependentAlpha(double[,] scores)
    {
        int n = scores.GetLength(0), k = scores.GetLength(1);
        double[] means = Enumerable.Range(0, k).Select(j => Enumerable.Range(0, n).Average(i => scores[i, j])).ToArray();
        double all = 0, diagonal = 0;

        for (int a = 0; a < k; a++)
        {
            for (int b = 0; b < k; b++)
            {
                double covariance = Enumerable.Range(0, n).Sum(i => (scores[i, a] - means[a]) * (scores[i, b] - means[b])) / (n - 1);
                all += covariance;

                if (a == b)
                    diagonal += covariance;
            }
        }

        return k / (k - 1.0) * (1 - (diagonal / all));
    }

    #endregion

    #region Факторный анализ

    [Fact]
    public void PrincipalAxis_RecoversSimpleStructureFromExactCorrelations()
    {
        double[,] truth =
        {
            { 0.8, 0 }, { 0.7, 0 }, { 0.6, 0 },
            { 0, 0.8 }, { 0, 0.7 }, { 0, 0.6 }
        };

        FactorAnalysisResult result = FactorAnalysis.PrincipalAxis(Implied(truth, 0), 2, FactorRotation.Varimax);

        Assert.True(result.Converged);
        AssertLoadings(truth, result.Loadings, 1e-3);

        for (int i = 0; i < 6; i++)
            Assert.Equal((truth[i, 0] * truth[i, 0]) + (truth[i, 1] * truth[i, 1]), result.Communalities[i], 4);
    }

    [Fact]
    public void Promax_RecoversFactorCorrelation()
    {
        double[,] truth =
        {
            { 0.8, 0 }, { 0.7, 0 }, { 0.75, 0 }, { 0.6, 0 },
            { 0, 0.8 }, { 0, 0.7 }, { 0, 0.75 }, { 0, 0.6 }
        };

        FactorAnalysisResult result = FactorAnalysis.PrincipalAxis(Implied(truth, 0.5), 2, FactorRotation.Promax);

        Assert.NotNull(result.FactorCorrelations);
        Assert.Equal(0.5, Math.Abs(result.FactorCorrelations![0, 1]), 0.06);
        AssertLoadings(truth, result.Loadings, 0.06);

        // Варимакс на тех же данных держит факторы ортогональными и потому размазывает нагрузки
        FactorAnalysisResult orthogonal = FactorAnalysis.PrincipalAxis(Implied(truth, 0.5), 2, FactorRotation.Varimax);
        Assert.Null(orthogonal.FactorCorrelations);
    }

    [Fact]
    public void FactorCount_ParallelAnalysisAndKaiser_OnSimulatedData()
    {
        double[,] truth =
        {
            { 0.8, 0 }, { 0.7, 0 }, { 0.6, 0 }, { 0.7, 0 },
            { 0, 0.8 }, { 0, 0.7 }, { 0, 0.6 }, { 0, 0.7 }
        };

        double[,] data = Sample(truth, 1500, new Random(4));

        Assert.Equal(2, FactorAnalysis.ParallelAnalysis(data, new Random(5)));
        Assert.Equal(2, FactorAnalysis.KaiserCount(FactorAnalysis.CorrelationMatrix(data)));

        FactorAnalysisResult result = FactorAnalysis.Fit(data, 2);
        AssertLoadings(truth, result.Loadings, 0.08);
    }

    // R = ΛΦΛᵀ с единичной диагональю: точная корреляционная матрица модели
    private static double[,] Implied(double[,] loadings, double factorCorrelation)
    {
        int p = loadings.GetLength(0);
        var r = new double[p, p];

        for (int i = 0; i < p; i++)
        {
            for (int j = 0; j < p; j++)
            {
                r[i, j] = i == j
                    ? 1
                    : (loadings[i, 0] * loadings[j, 0]) + (loadings[i, 1] * loadings[j, 1])
                      + (factorCorrelation * ((loadings[i, 0] * loadings[j, 1]) + (loadings[i, 1] * loadings[j, 0])));
            }
        }

        return r;
    }

    private static double[,] Sample(double[,] loadings, int n, Random rng)
    {
        int p = loadings.GetLength(0);
        var data = new double[n, p];

        for (int s = 0; s < n; s++)
        {
            double f1 = RandomEngine.NextGaussian(rng), f2 = RandomEngine.NextGaussian(rng);

            for (int i = 0; i < p; i++)
            {
                double common = (loadings[i, 0] * f1) + (loadings[i, 1] * f2);
                double unique = Math.Sqrt(1 - (loadings[i, 0] * loadings[i, 0]) - (loadings[i, 1] * loadings[i, 1]));
                data[s, i] = common + (unique * RandomEngine.NextGaussian(rng));
            }
        }

        return data;
    }

    // Факторы определены с точностью до порядка и знака: сравнение по лучшей перестановке
    private static void AssertLoadings(double[,] truth, double[,] actual, double tolerance)
    {
        int p = truth.GetLength(0);

        double Error(int[] order, double[] sign)
            => Enumerable.Range(0, p).Max(i => Math.Max(
                Math.Abs(truth[i, 0] - (sign[0] * actual[i, order[0]])),
                Math.Abs(truth[i, 1] - (sign[1] * actual[i, order[1]]))));

        double best = double.PositiveInfinity;

        foreach (int[] order in new[] { new[] { 0, 1 }, new[] { 1, 0 } })
            foreach (double s0 in new[] { 1.0, -1.0 })
                foreach (double s1 in new[] { 1.0, -1.0 })
                    best = Math.Min(best, Error(order, [s0, s1]));

        Assert.True(best < tolerance, $"наибольшее расхождение нагрузок {best}");

        double[] first = Enumerable.Range(0, p).Select(i => truth[i, 0]).ToArray();
        double congruence = Enumerable.Range(0, 2)
            .Max(f => Math.Abs(FactorAnalysis.Congruence(first, Enumerable.Range(0, p).Select(i => actual[i, f]).ToArray())));

        Assert.True(congruence > 0.95);
    }

    #endregion

    #region Теория ответа на задание

    [Fact]
    public void ItemInformation_PeaksWhereTheoryPredicts()
    {
        var twoParameter = new IrtItem(1.7, 0.4);
        Assert.Equal(1.7 * 1.7 / 4, twoParameter.Information(0.4), 12);

        // Бирнбаум: у 3PL максимум сдвинут вверх на ln((1 + √(1 + 8c))/2)/a
        var threeParameter = new IrtItem(1.5, 0.2, 0.25);
        double peak = Enumerable.Range(0, 200_001).Select(i => -4 + (i * 8e-5)).MaxBy(threeParameter.Information);
        double theory = 0.2 + (Math.Log((1 + Math.Sqrt(1 + (8 * 0.25))) / 2) / 1.5);

        Assert.Equal(theory, peak, 3);
    }

    [Fact]
    public void Rasch_SumScoreIsSufficient_TwoParameterIsNot()
    {
        IrtItem[] rasch = [new(1, -1), new(1, 0), new(1, 0.5), new(1, 1.5)];
        IrtItem[] twoParameter = [new(0.5, -1), new(2, 0), new(1, 0.5), new(1.5, 1.5)];

        double a = ItemResponse.MaximumLikelihood(rasch, [1, 1, 0, 0]).Ability;
        double b = ItemResponse.MaximumLikelihood(rasch, [0, 0, 1, 1]).Ability;

        Assert.Equal(a, b, 8);

        // У 2PL достаточна взвешенная сумма Σaᵢuᵢ: 0,5 + 2 = 1 + 1,5 даёт одну оценку, а 0,5 + 1,5 и 2 + 1 — разные
        Assert.Equal(ItemResponse.MaximumLikelihood(twoParameter, [1, 1, 0, 0]).Ability,
            ItemResponse.MaximumLikelihood(twoParameter, [0, 0, 1, 1]).Ability, 8);
        Assert.NotEqual(ItemResponse.MaximumLikelihood(twoParameter, [1, 0, 0, 1]).Ability,
            ItemResponse.MaximumLikelihood(twoParameter, [0, 1, 1, 0]).Ability, 3);
    }

    [Fact]
    public void AbilityEstimates_MaximumLikelihoodAndPosterior()
    {
        IrtItem[] items = Enumerable.Range(0, 10).Select(i => new IrtItem(1.2, -2 + (i * 0.45))).ToArray();

        AbilityEstimate perfect = ItemResponse.MaximumLikelihood(items, Enumerable.Repeat(1, 10).ToArray());
        Assert.False(perfect.IsFinite);
        Assert.True(ItemResponse.ExpectedAPosteriori(items, Enumerable.Repeat(1, 10).ToArray()).IsFinite);

        int[] responses = [1, 1, 1, 1, 1, 1, 1, 1, 0, 1];
        AbilityEstimate ml = ItemResponse.MaximumLikelihood(items, responses);
        AbilityEstimate eap = ItemResponse.ExpectedAPosteriori(items, responses);

        Assert.True(Math.Abs(eap.Ability) < Math.Abs(ml.Ability), "априорное распределение тянет оценку к среднему");

        // Длинный тест: оценка в пределах трёх стандартных ошибок от истины. Правдоподобие здесь почти
        // нормально, поэтому апостериорное среднее при плоском априорном распределении совпадает с его модой;
        // на коротком тесте со скошенным правдоподобием они законно расходятся
        var rng = new Random(6);
        IrtItem[] bank = Enumerable.Range(0, 200).Select(_ => new IrtItem(0.8 + rng.NextDouble(), -3 + (6 * rng.NextDouble()))).ToArray();
        int[,] answers = ItemResponse.Simulate(bank, [1.2], rng);
        int[] pattern = Enumerable.Range(0, 200).Select(i => answers[0, i]).ToArray();
        AbilityEstimate estimate = ItemResponse.MaximumLikelihood(bank, pattern);
        AbilityEstimate flat = ItemResponse.ExpectedAPosteriori(bank, pattern, 0, 50, 4001);

        Assert.True(Math.Abs(estimate.Ability - 1.2) < 3 * estimate.StandardError);
        Assert.Equal(estimate.Ability, flat.Ability, 0.03);
        Assert.Equal(estimate.StandardError, flat.StandardError, 0.02);
    }

    [Fact]
    public void Calibration_TwoParameter_RecoversItems()
    {
        var rng = new Random(7);
        IrtItem[] truth = Enumerable.Range(0, 15).Select(i => new IrtItem(0.7 + (0.09 * i), -2 + (i * 4.0 / 14))).ToArray();
        double[] abilities = Enumerable.Range(0, 2000).Select(_ => RandomEngine.NextGaussian(rng)).ToArray();

        IrtCalibration calibration = ItemResponse.Calibrate(ItemResponse.Simulate(truth, abilities, rng));

        Assert.True(calibration.Converged);
        Assert.True(Rmse(truth.Select(i => i.Difficulty), calibration.Items.Select(i => i.Difficulty)) < 0.15);
        Assert.True(Rmse(truth.Select(i => i.Discrimination), calibration.Items.Select(i => i.Discrimination)) < 0.2);
    }

    [Fact]
    public void Calibration_Rasch_AbsorbsAbilitySpreadIntoCommonSlope()
    {
        // Раш с разбросом способности 1,5: при шкале N(0, 1) общий наклон становится 1,5, трудности делятся на 1,5
        var rng = new Random(8);
        IrtItem[] truth = Enumerable.Range(0, 12).Select(i => new IrtItem(1, -2 + (i * 4.0 / 11))).ToArray();
        double[] abilities = Enumerable.Range(0, 3000).Select(_ => 1.5 * RandomEngine.NextGaussian(rng)).ToArray();

        IrtCalibration calibration = ItemResponse.Calibrate(ItemResponse.Simulate(truth, abilities, rng), IrtModel.Rasch);

        Assert.All(calibration.Items, item => Assert.Equal(calibration.Items[0].Discrimination, item.Discrimination, 12));
        Assert.Equal(1.5, calibration.Items[0].Discrimination, 0.1);
        Assert.True(Rmse(truth.Select(i => i.Difficulty / 1.5), calibration.Items.Select(i => i.Difficulty)) < 0.1);
    }

    [Fact]
    public void AdaptiveTesting_IsMorePreciseThanFixedTest()
    {
        var rng = new Random(9);
        IrtItem[] bank = Enumerable.Range(0, 80).Select(i => new IrtItem(1.3, -3 + (i * 6.0 / 79))).ToArray();

        Assert.Equal(40, ItemResponse.SelectNextItem(bank, new HashSet<int>(), 0.04));

        double adaptiveError = 0, fixedError = 0;

        for (int person = 0; person < 200; person++)
        {
            double ability = 1.5 * RandomEngine.NextGaussian(rng);
            var given = new HashSet<int>();
            var responses = new List<int>();
            double estimate = 0;

            for (int step = 0; step < 12; step++)
            {
                int next = ItemResponse.SelectNextItem(bank, given, estimate);
                given.Add(next);
                responses.Add(rng.NextDouble() < bank[next].Probability(ability) ? 1 : 0);
                estimate = ItemResponse.ExpectedAPosteriori(given.Select(i => bank[i]).ToArray(), responses).Ability;
            }

            IrtItem[] fixedItems = Enumerable.Range(0, 12).Select(i => bank[i * 6]).ToArray();
            int[] fixedResponses = fixedItems.Select(item => rng.NextDouble() < item.Probability(ability) ? 1 : 0).ToArray();

            adaptiveError += Math.Pow(estimate - ability, 2);
            fixedError += Math.Pow(ItemResponse.ExpectedAPosteriori(fixedItems, fixedResponses).Ability - ability, 2);
        }

        Assert.True(adaptiveError < fixedError, $"адаптивный {adaptiveError}, фиксированный {fixedError}");
    }

    private static double Rmse(IEnumerable<double> expected, IEnumerable<double> actual)
        => Math.Sqrt(expected.Zip(actual, (e, a) => (e - a) * (e - a)).Average());

    #endregion
}
