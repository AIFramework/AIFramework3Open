using AI.Statistics;
using Xunit;
using AI.HighLevelFunctions;

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
    public void NormalQuantile_InvertsNormalCdf_AcrossSixSigma()
    {
        // Допуск свой в каждой точке: ни общий абсолютный, ни общий относительный здесь не годятся, потому что
        // достижимая точность меняется по сетке на восемь порядков. При z > 0 значение p = Φ(z) близко к 1 и хранится
        // с абсолютным шагом ulp(p) = 2⁻⁵³. Одно его округление сдвигает точный квантиль на ulp(p)/φ(z): производная
        // обратной функции dΦ⁻¹/dp = 1/φ (число обусловленности, Higham, «Accuracy and Stability of Numerical
        // Algorithms», 2-е изд., SIAM, 2002, гл. 1). При z = 4,75 это 2,2e-11, при z = 6 — 1,8e-8, и точнее z из такого p
        // не вернёт никакая реализация в double: прежний общий допуск 1e-11 был недостижим начиная с z = 4,75.
        // Допуск — этот сдвиг на целый ulp(p) (округление даёт не больше половины) плюс 4 ulp(z) на арифметику
        // квантиля, который после шага Галлея точен до последних разрядов (Acklam). В нижней половине и у нуля
        // первое слагаемое ничтожно, и допуск в тысячи раз строже прежних 1e-11.
        var failures = new List<string>();

        for (double z = -6; z <= 6; z += 0.25)
        {
            double p = StatInference.NormalCdf(z);
            double density = Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI);
            double tolerance = ((Math.BitIncrement(p) - p) / density) + (4 * (Math.BitIncrement(Math.Abs(z)) - Math.Abs(z)));
            double error = StatInference.NormalQuantile(p) - z;

            if (!(Math.Abs(error) <= tolerance))
                failures.Add($"z = {z}: ошибка {error:E2}, допуск {tolerance:E2}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Theory]
    [InlineData(0.99999, 4.264890793923841)]
    [InlineData(0.9999999999, 6.361340889697422)]
    [InlineData(0.99999999999999, 7.650730905155643)]
    public void NormalQuantile_UpperTail_MatchesExactQuantileOfGivenDouble(double probability, double expected)
    {
        // Эталон — точный квантиль именно этого double p (mpmath, 400 знаков), округлённый до double.
        // Пока верхняя половина считалась напрямую, здесь были ошибки 1,5e-13, 2,9e-9 и 8,3e-9 (до 1e-9 относительно).
        // Теперь точность та же, что в нижнем хвосте: erfc верна до 2,3e-15 относительно, в квантиль её ошибка
        // переходит с множителем p/(|x|·φ(x)) < 1; допуск 1e-14 — с запасом в четыре раза
        Assert.Equal(expected, StatInference.NormalQuantile(probability), tolerance: 1e-14 * expected);
    }

    [Theory]
    [InlineData(0.5, 0.5204998778130465)]
    [InlineData(1.0, 0.8427007929497149)]
    [InlineData(2.0, 0.9953222650189527)]
    [InlineData(-1.5, -0.9661051464753107)]
    public void Erf_MatchesReferenceValues(double x, double expected)
    {
        // Прежняя формула Абрамовица - Стиган 7.1.26 ошибалась здесь до 1.5e-7
        Assert.Equal(expected, SpecialFunctions.Erf(x), tolerance: 1e-15);
    }

    [Theory]
    [InlineData(1.0, 0.8413447460685429)]
    [InlineData(-1.0, 0.15865525393145707)]
    [InlineData(1.96, 0.9750021048517795)]
    [InlineData(-5.0, 2.866515718791939e-7)]
    [InlineData(-10.0, 7.619853024160527e-24)]
    public void NormalCdf_MatchesReferenceValues_InRelativeTerms(double x, double expected)
    {
        // Относительная точность: при x = −10 прежняя ½·(1 + erf) давала хвост из одних округлений
        Assert.Equal(expected, StatInference.NormalCdf(x), tolerance: 1e-13 * expected);
    }

    [Fact]
    public void Erfc_KeepsTheTail_AndLimitsAreExact()
    {
        Assert.Equal(1.5374597944280351e-12, SpecialFunctions.Erfc(5), tolerance: 1e-13 * 1.5374597944280351e-12);
        Assert.Equal(2.0884875837625447e-45, SpecialFunctions.Erfc(10), tolerance: 1e-12 * 2.0884875837625447e-45);
        Assert.Equal(1.9999779095030014, SpecialFunctions.Erfc(-3), tolerance: 1e-15);

        Assert.Equal(1, SpecialFunctions.Erf(double.PositiveInfinity));
        Assert.Equal(-1, SpecialFunctions.Erf(double.NegativeInfinity));
        Assert.Equal(0, StatInference.NormalCdf(double.NegativeInfinity));
        Assert.Equal(1, StatInference.NormalCdf(double.PositiveInfinity));
        Assert.Equal(1, SpecialFunctions.Erf(1e200));
        Assert.True(double.IsNaN(StatInference.NormalCdf(double.NaN)));

        foreach (double x in new[] { 0.3, 1.7, 4.0 })
            Assert.Equal(1, StatInference.NormalCdf(x) + StatInference.NormalCdf(-x), tolerance: 1e-15);
    }

    [Theory]
    [InlineData(0.5, 0.5723649429247001)]    // ln Г(1/2) = ln sqrt(pi)
    [InlineData(1.0, 0.0)]
    [InlineData(5.0, 3.1780538303479458)]    // ln 24
    [InlineData(10.5, 13.940625219404433)]
    public void LogGamma_MatchesReferenceValues(double x, double expected)
    {
        Assert.Equal(expected, SpecialFunctions.LogGamma(x), tolerance: 1e-10);
    }

    [Fact]
    public void LogGamma_UsesReflectionBelowOneHalf()
    {
        // Ветка отражения: прежняя реализация ядра для x < 0.5 была неточна
        Assert.Equal(2.2527126517342055, SpecialFunctions.LogGamma(0.1), tolerance: 1e-10);
        Assert.Equal(1.2880225246980774, SpecialFunctions.LogGamma(0.25), tolerance: 1e-10);
    }

    [Fact]
    public void LogGamma_SatisfiesRecurrence()
    {
        // ln Г(x+1) = ln x + ln Г(x)
        foreach (double x in new[] { 0.3, 1.7, 4.2, 12.0 })
            Assert.Equal(Math.Log(x) + SpecialFunctions.LogGamma(x), SpecialFunctions.LogGamma(x + 1), tolerance: 1e-10);
    }

    [Fact]
    public void TQuantile_ApproachesNormalForLargeDegreesOfFreedom()
    {
        double normal = StatInference.NormalQuantile(0.975);
        double student = StatInference.TQuantile(0.975, 100_000);

        Assert.Equal(normal, student, tolerance: 1e-3);
    }
}
