using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Точность функций распределений ядра. После сведения дубликатов из эконометрики
/// в <see cref="StatInference"/> здесь одна реализация на весь репозиторий,
/// поэтому её погрешность закреплена тестами.
/// </summary>
public class StatInferenceAccuracyTests
{
    [Theory]
    [InlineData(0.975, 1.9599639845400545)]
    [InlineData(0.99, 2.3263478740408408)]
    [InlineData(0.995, 2.5758293035489004)]
    [InlineData(0.95, 1.6448536269514722)]
    [InlineData(0.001, -3.0902323061678132)]
    public void NormalQuantile_MatchesReferenceValues(double probability, double expected)
    {
        // Алгоритм Acklam даёт относительную погрешность порядка 1e-9
        Assert.Equal(expected, StatInference.NormalQuantile(probability), tolerance: 5e-9);
    }

    [Fact]
    public void NormalQuantile_IsAntisymmetric()
    {
        foreach (double p in new[] { 0.01, 0.1, 0.3, 0.45 })
            Assert.Equal(-StatInference.NormalQuantile(p), StatInference.NormalQuantile(1 - p), tolerance: 1e-12);
    }

    [Fact]
    public void NormalQuantile_InvertsNormalCdf()
    {
        foreach (double z in new[] { -2.5, -1.0, 0.5, 1.96 })
        {
            double p = StatInference.NormalCdf(z);

            // Точность ограничена аппроксимацией функции ошибок (~1e-7)
            Assert.Equal(z, StatInference.NormalQuantile(p), tolerance: 1e-5);
        }
    }

    [Theory]
    [InlineData(0.5, 0.5723649429247001)]    // ln Г(1/2) = ln sqrt(pi)
    [InlineData(1.0, 0.0)]
    [InlineData(5.0, 3.1780538303479458)]    // ln 24
    [InlineData(10.5, 13.940625219404433)]
    public void LogGamma_MatchesReferenceValues(double x, double expected)
    {
        Assert.Equal(expected, StatInference.LogGamma(x), tolerance: 1e-10);
    }

    [Fact]
    public void LogGamma_UsesReflectionBelowOneHalf()
    {
        // Ветка отражения: прежняя реализация ядра для x < 0.5 была неточна
        Assert.Equal(2.2527126517342055, StatInference.LogGamma(0.1), tolerance: 1e-10);
        Assert.Equal(1.2880225246980774, StatInference.LogGamma(0.25), tolerance: 1e-10);
    }

    [Fact]
    public void LogGamma_SatisfiesRecurrence()
    {
        // ln Г(x+1) = ln x + ln Г(x)
        foreach (double x in new[] { 0.3, 1.7, 4.2, 12.0 })
            Assert.Equal(Math.Log(x) + StatInference.LogGamma(x), StatInference.LogGamma(x + 1), tolerance: 1e-10);
    }

    [Fact]
    public void TQuantile_ApproachesNormalForLargeDegreesOfFreedom()
    {
        double normal = StatInference.NormalQuantile(0.975);
        double student = StatInference.TQuantile(0.975, 100_000);

        Assert.Equal(normal, student, tolerance: 1e-3);
    }
}
