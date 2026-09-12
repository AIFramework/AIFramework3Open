using AI.Simulation.Inference;
using AI.Statistics;
using AI.Statistics.Distributions;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Облако частиц проверяется задачами с известным ответом: сопряжённая гауссова модель даёт точный
/// апостериорный закон, а пеленг на дорогу обязан превратить кольцо возможных положений в дугу.
/// </summary>
public class ParticleSetTests
{
    [Fact]
    public void ParticleSet_Reweight_BearingTurnsRingIntoArc()
    {
        Random random = RandomEngine.Create(7);

        // «Дорога в 30 км»: расстояние до ближайшей точки 30 ± 3 км, направление не задано
        var roads = ParticleSet<Road>.FromPrior(5_000, r => new Road(
            RandomEngine.NextTruncatedGaussian(r, 30, 3, 0, double.PositiveInfinity),
            RandomEngine.NextVonMises(r, 0, 0)), random);

        Assert.True(roads.MeanResultantLength(road => road.Bearing) < 0.05);

        // «К дороге идти на север», ±30°
        var north = VonMisesDist1D.FromStandardDeviation(0, Math.PI / 6);
        roads.Reweight(road => north.CulcLogProb(road.Bearing));
        roads.Resample(random);

        Assert.InRange(CircularStatistics.DifferenceRadians(0, roads.MeanDirection(road => road.Bearing)), -0.05, 0.05);
        Assert.True(roads.MeanResultantLength(road => road.Bearing) > 0.8);
        Assert.InRange(roads.Mean(road => road.DistanceKm), 29.7, 30.3);
    }

    [Fact]
    public void ParticleSet_Reweight_ConjugateGaussian_MatchesExactPosterior()
    {
        // Априори N(0, 1), наблюдение y = 1 с шумом N(0, 1) → апостериори N(0.5, 0.5)
        Random random = RandomEngine.Create(11);
        var cloud = ParticleSet<double>.FromPrior(20_000, RandomEngine.NextGaussian, random);
        var noise = new GaussianDist1D(0, 1);

        cloud.Reweight(x => noise.CulcLogProb(1 - x));

        Assert.InRange(cloud.Mean(x => x), 0.48, 0.52);
        Assert.InRange(cloud.Variance(x => x), 0.48, 0.52);
    }

    [Fact]
    public void ParticleSet_Reweight_UninformativeObservation_ReturnsEvidenceAndKeepsWeights()
    {
        var cloud = new ParticleSet<int>(Enumerable.Range(0, 100));

        double evidence = cloud.Reweight(_ => Math.Log(0.25));

        Assert.Equal(Math.Log(0.25), evidence, 12);
        Assert.Equal(100, cloud.EffectiveSampleSize, 9);
    }

    [Fact]
    public void ParticleSet_Reweight_ContradictingEveryParticle_ThrowsAndKeepsBelief()
    {
        var cloud = new ParticleSet<int>(Enumerable.Range(0, 10));
        cloud.Reweight(x => x < 5 ? 0 : double.NegativeInfinity);

        Assert.Throws<InvalidOperationException>(() => cloud.Reweight(x => x < 5 ? double.NegativeInfinity : 0));
        Assert.Equal(1.0, cloud.Probability(x => x < 5), 12);
    }

    [Fact]
    public void ParticleSet_Resample_EqualizesWeightsAndKeepsExpectation()
    {
        Random random = RandomEngine.Create(13);
        var cloud = ParticleSet<double>.FromPrior(10_000, RandomEngine.NextGaussian, random);
        cloud.Reweight(x => -0.5 * (1 - x) * (1 - x));
        double before = cloud.Mean(x => x);

        Assert.True(cloud.EffectiveSampleSize < cloud.Count);

        cloud.Resample(random);

        Assert.Equal(cloud.Count, cloud.EffectiveSampleSize, 6);
        Assert.InRange(cloud.Mean(x => x) - before, -0.02, 0.02);
    }

    [Fact]
    public void ParticleSet_Rejuvenate_RestoresDiversityWithoutBiasingPosterior()
    {
        // Априори N(0, 1), точное наблюдение y = 1 с шумом 0.1 → апостериори N(0.990, 0.0099)
        Random random = RandomEngine.Create(17);
        var cloud = ParticleSet<double>.FromPrior(5_000, RandomEngine.NextGaussian, random);
        var prior = new GaussianDist1D(0, 1);
        var sharp = new GaussianDist1D(0, 0.1);

        cloud.Reweight(x => sharp.CulcLogProb(1 - x));
        cloud.Resample(random);
        int distinctBefore = cloud.States.Distinct().Count();

        double acceptance = cloud.Rejuvenate(
            (x, r) => x + RandomEngine.NextGaussian(r, 0, 0.1),
            x => prior.CulcLogProb(x) + sharp.CulcLogProb(1 - x),
            random,
            steps: 5);

        Assert.True(cloud.States.Distinct().Count() > distinctBefore);
        Assert.InRange(acceptance, 0.2, 0.9);
        Assert.InRange(cloud.Mean(x => x), 0.97, 1.01);
        Assert.InRange(cloud.Variance(x => x), 0.007, 0.013);
    }

    [Fact]
    public void ParticleSet_Propagate_UncertainSpeedSpreadsPosition()
    {
        Random random = RandomEngine.Create(19);
        var positions = new ParticleSet<double>(new double[5_000]);

        // Час езды со скоростью 40 ± 4 км/ч
        positions.Propagate((x, r) => x + RandomEngine.NextGaussian(r, 40, 4), random);

        Assert.InRange(positions.Mean(x => x), 39.8, 40.2);
        Assert.InRange(Math.Sqrt(positions.Variance(x => x)), 3.8, 4.2);
    }

    [Fact]
    public void ParticleSet_Draw_ReturnsStatesProportionalToWeights()
    {
        var cloud = new ParticleSet<string>(["A", "B"]);
        cloud.Reweight(state => state == "A" ? Math.Log(3) : 0);

        IReadOnlyList<string> drawn = cloud.Draw(4, RandomEngine.Create(23));

        Assert.Equal(3, drawn.Count(state => state == "A"));
        Assert.Equal("A", cloud.MostProbable);
    }

    /// <summary>Гипотеза о дороге: расстояние до ближайшей точки и направление на неё</summary>
    private sealed record Road(double DistanceKm, double Bearing);
}
