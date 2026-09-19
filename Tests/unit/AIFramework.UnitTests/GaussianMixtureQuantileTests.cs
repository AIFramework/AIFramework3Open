#nullable enable

using AI.Statistics.MixtureModeling;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Функция распределения и квантили одномерной гауссовой смеси: одна компонента дает квантили нормального закона,
/// две равные компоненты дают медиану посередине, квантиль обращает функцию распределения.
/// </summary>
public class GaussianMixtureQuantileTests
{
    [Fact]
    public void SingleComponent_MatchesNormalQuantiles()
    {
        GaussianMixture mixture = GaussianMixture.From1D([1], [10], [2]);

        Assert.Equal(10, mixture.Median, 8);
        Assert.Equal(10 + (1.959964 * 2), mixture.Quantile(0.975), 4);
        Assert.Equal(10 - (1.959964 * 2), mixture.Quantile(0.025), 4);
        Assert.Equal(0.5, mixture.Cdf(10), 12);
    }

    [Fact]
    public void TwoEqualComponents_HaveMedianBetweenThem()
    {
        GaussianMixture mixture = GaussianMixture.From1D([0.5, 0.5], [0, 10], [1, 1]);

        Assert.Equal(5, mixture.Median, 8);
        Assert.Equal(0.5, mixture.Cdf(5), 12);
        // Хвост смеси: четверть массы лежит ниже нуля с точностью до хвоста второй компоненты
        Assert.Equal(0.25, mixture.Cdf(0), 6);
    }

    [Fact]
    public void Quantile_InvertsCdf()
    {
        GaussianMixture mixture = GaussianMixture.From1D([0.2, 0.5, 0.3], [-3, 1, 8], [0.5, 2, 1]);

        foreach (double x in new[] { -3.5, 0.0, 2.5, 7.9 })
            Assert.Equal(x, mixture.Quantile(mixture.Cdf(x)), 6);
    }

    [Fact]
    public void Quantile_AtEnds_IsInfinite()
    {
        GaussianMixture mixture = GaussianMixture.From1D([1], [0], [1]);

        Assert.Equal(double.NegativeInfinity, mixture.Quantile(0));
        Assert.Equal(double.PositiveInfinity, mixture.Quantile(1));
    }
}
