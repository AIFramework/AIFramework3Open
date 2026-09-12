using AI.DataStructs.Algebraic;
using AI.Statistics;
using AI.Statistics.Distributions;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Примитивы вероятностной модели мира: распределения для углов и ограниченных величин,
/// статистика взвешенных выборок и направлений. Проверяются нормировкой плотностей, моментами
/// выборок и значениями, известными в замкнутом виде.
/// </summary>
public class ProbabilityDistributionsTests
{
    #region Фон Мизес

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(4.0)]
    [InlineData(50.0)]
    public void VonMisesDist1D_CulcProb_IntegratesToOne(double kappa)
    {
        var distribution = new VonMisesDist1D(1.0, kappa);

        Assert.Equal(1.0, Integrate(distribution.CulcProb, 0, 2 * Math.PI), 4);
    }

    [Fact]
    public void VonMisesDist1D_Sample1D_MatchesDirectionAndConcentration()
    {
        Vector samples = Draw(new VonMisesDist1D(0.1, 2.0), 20_000, seed: 1);

        Assert.InRange(CircularStatistics.DifferenceRadians(0.1, CircularStatistics.MeanDirection(samples)), -0.03, 0.03);

        // Ожидание cos(θ − μ) у VM равно I₁(κ)/I₀(κ); при κ = 2 это 1.5906369 / 2.2795853 = 0.6978
        Assert.InRange(CircularStatistics.MeanResultantLength(samples), 0.688, 0.708);
    }

    [Fact]
    public void VonMisesDist1D_Refit1D_RecoversParameters()
    {
        Vector samples = Draw(new VonMisesDist1D(5.0, 4.0), 20_000, seed: 2);

        var fitted = (VonMisesDist1D)new VonMisesDist1D(0, 1).Refit1D(samples.ToArray(), samples.Count);

        Assert.InRange(CircularStatistics.DifferenceRadians(5.0, fitted.Mu), -0.03, 0.03);
        Assert.InRange(fitted.Kappa, 3.6, 4.4);
    }

    [Fact]
    public void RandomEngine_NextVonMises_HugeConcentration_BehavesLikeNarrowNormal()
    {
        Random rng = RandomEngine.Create(3);
        double[] deviations = Enumerable.Range(0, 5_000)
            .Select(_ => CircularStatistics.DifferenceRadians(0, RandomEngine.NextVonMises(rng, 0.0, 1e6)))
            .ToArray();

        Assert.InRange(deviations.Average(), -1e-4, 1e-4);
        Assert.InRange(Math.Sqrt(deviations.Select(x => x * x).Average()), 0.00095, 0.00105);
    }

    #endregion

    #region Усечённое нормальное

    [Fact]
    public void TruncatedGaussianDist1D_Sample1D_StaysInsideAndMatchesAnalyticMean()
    {
        var distribution = new TruncatedGaussianDist1D(0, 1, 0.5, 2.0);
        Vector samples = Draw(distribution, 20_000, seed: 4);

        Assert.All(samples, x => Assert.InRange(x, 0.5, 2.0));

        // (φ(0.5) − φ(2)) / (Φ(2) − Φ(0.5)) = 0.2980743 / 0.2857874
        Assert.Equal(1.04299, distribution.Mean, 4);
        Assert.InRange(samples.Average(), 1.033, 1.053);
    }

    [Fact]
    public void TruncatedGaussianDist1D_FarTail_SamplesAndNormalizes()
    {
        var distribution = new TruncatedGaussianDist1D(0, 1, 8.0);
        Vector samples = Draw(distribution, 5_000, seed: 5);

        Assert.All(samples, x => Assert.True(x >= 8.0));

        // Среднее хвоста φ(8)/Q(8) ≈ 8 + 1/8 − 2/8³: через 1 − Φ(8) эту долю не посчитать совсем
        Assert.InRange(distribution.Mean, 8.11, 8.13);
        Assert.InRange(samples.Average(), 8.10, 8.14);
        Assert.Equal(1.0, Integrate(distribution.CulcProb, 8.0, 14.0), 4);
    }

    [Fact]
    public void TruncatedGaussianDist1D_PositiveQuantity_KeepsNormalShapeAwayFromBound()
    {
        // Масса 7 т ± 150 кг: граница у нуля отстоит на 46σ и ничего не меняет
        var mass = new TruncatedGaussianDist1D(7000, 150, 0);

        Assert.Equal(7000, mass.Mean, 6);
        Assert.Equal(new GaussianDist1D(7000, 150).CulcLogProb(7100), mass.CulcLogProb(7100), 9);
        Assert.Equal(0.0, mass.CulcProb(-1));
    }

    [Fact]
    public void TruncatedGaussianDist1D_EmptyInterval_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TruncatedGaussianDist1D(0, 1, 2, 1));
    }

    #endregion

    [Fact]
    public void StatInference_PoissonLogPmf_MatchesClosedFormAndSumsToOne()
    {
        Assert.Equal(4.5 * Math.Exp(-3), Math.Exp(StatInference.PoissonLogPmf(2, 3.0)), 10);
        Assert.Equal(1.0, Enumerable.Range(0, 80).Sum(k => Math.Exp(StatInference.PoissonLogPmf(k, 12.5))), 9);
        Assert.Equal(double.NegativeInfinity, StatInference.PoissonLogPmf(-1, 3.0));
    }

    #region Взвешенная статистика и направления

    [Fact]
    public void WeightedStatistics_EqualWeights_MatchOrdinaryStatistics()
    {
        var values = new Vector(1.0, 2.0, 3.0, 4.0, 5.0);
        var weights = new Vector(2.0, 2.0, 2.0, 2.0, 2.0);

        Assert.Equal(3.0, WeightedStatistics.Mean(values, weights), 12);
        Assert.Equal(2.0, WeightedStatistics.Variance(values, weights), 12);
        Assert.Equal(3.0, WeightedStatistics.Quantile(values, weights, 0.5));
        Assert.Equal(5.0, WeightedStatistics.EffectiveSampleSize(weights), 12);
    }

    [Fact]
    public void WeightedStatistics_AllWeightOnOneValue_SelectsIt()
    {
        var values = new Vector(10.0, 20.0, 30.0, 40.0);
        var weights = new Vector(0.0, 0.0, 1.0, 0.0);

        Assert.Equal(30.0, WeightedStatistics.Mean(values, weights), 12);
        Assert.Equal(30.0, WeightedStatistics.Quantile(values, weights, 0.0));
        Assert.Equal(30.0, WeightedStatistics.Quantile(values, weights, 1.0));
        Assert.Equal(1.0, WeightedStatistics.EffectiveSampleSize(weights), 12);
    }

    [Fact]
    public void WeightedStatistics_ZeroTotalWeight_Throws()
    {
        Assert.Throws<ArgumentException>(() => WeightedStatistics.Mean(new Vector(1.0, 2.0), new Vector(0.0, 0.0)));
    }

    [Fact]
    public void CircularStatistics_WrapAndDifference_HandleCrossingNorth()
    {
        Assert.Equal(350.0, CircularStatistics.WrapDegrees(-10.0), 12);
        Assert.Equal(0.0, CircularStatistics.WrapDegrees(720.0), 12);
        Assert.Equal(20.0, CircularStatistics.DifferenceDegrees(350.0, 10.0), 12);
        Assert.Equal(-20.0, CircularStatistics.DifferenceDegrees(10.0, 350.0), 12);
        Assert.Equal(Math.PI, CircularStatistics.DifferenceRadians(0.0, Math.PI), 12);
    }

    [Fact]
    public void CircularStatistics_MeanDirection_AcrossNorth_IsNorthNotSouth()
    {
        var angles = new Vector(Degrees(350), Degrees(10));

        Assert.InRange(CircularStatistics.DifferenceRadians(0, CircularStatistics.MeanDirection(angles)), -1e-9, 1e-9);
        Assert.Equal(Math.Cos(Degrees(10)), CircularStatistics.MeanResultantLength(angles), 12);
        Assert.Equal(0.0, CircularStatistics.MeanResultantLength(new Vector(0.0, Math.PI)), 12);
    }

    #endregion

    private static double Degrees(double degrees) => degrees * Math.PI / 180;

    private static Vector Draw(ISamplableDistribution distribution, int count, int seed)
    {
        Random rng = RandomEngine.Create(seed);
        return new Vector(Enumerable.Range(0, count).Select(_ => distribution.Sample1D(rng)));
    }

    /// <summary>Интеграл по формуле Симпсона</summary>
    private static double Integrate(Func<double, double> function, double from, double to, int intervals = 20_000)
    {
        double step = (to - from) / intervals;
        double sum = function(from) + function(to);
        for (int i = 1; i < intervals; i++)
            sum += function(from + (i * step)) * (i % 2 == 1 ? 4 : 2);

        return sum * step / 3;
    }
}
