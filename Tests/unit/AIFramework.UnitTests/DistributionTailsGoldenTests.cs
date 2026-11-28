using System.Text.Json;
using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Хвосты и квантили распределений против mpmath (50 знаков).
/// </summary>
/// <remarks>
/// Проверяются именно хвосты: p-значение сильного эффекта и квантиль при малой вероятности.
/// Разность <c>1 − CDF</c> там обращается в ноль, а квантиль с абсолютным критерием остановки
/// ошибается на порядки. Эталоны — Golden/special_functions.json.
/// </remarks>
public class DistributionTailsGoldenTests
{
    private static JsonElement Tails => SpecialFunctionsGoldenTests.Data.Value.GetProperty("tails");

    private static JsonElement Quantiles => SpecialFunctionsGoldenTests.Data.Value.GetProperty("quantiles");

    [Fact]
    public void NormalSf_MatchesMpmath()
    {
        foreach (JsonElement item in Tails.GetProperty("normal_sf").EnumerateArray())
        {
            double x = item.GetProperty("x").GetDouble();

            // Аргумент erfc — x/√2, и его округление входит в показатель e^(−x²/2): при x = 37 это шесть единиц 14-го знака
            SpecialFunctionsGoldenTests.AssertClose(item.GetProperty("value").GetDouble(), StatInference.NormalSf(x), 1e-13, $"Sf_N({x:R})");
        }
    }

    [Fact]
    public void TSf_MatchesMpmath()
    {
        foreach (JsonElement item in Tails.GetProperty("t_sf").EnumerateArray())
        {
            double t = item.GetProperty("t").GetDouble(), df = item.GetProperty("df").GetDouble();
            double expected = item.GetProperty("value").GetDouble();

            SpecialFunctionsGoldenTests.AssertClose(expected, StatInference.TSf(t, df), 2e-13, $"Sf_t({t:R}; {df:R})");
            SpecialFunctionsGoldenTests.AssertClose(expected, StatInference.TCdf(-t, df), 2e-13, $"F_t({-t:R}; {df:R})");
        }
    }

    [Fact]
    public void ChiSquaredTails_MatchMpmath()
    {
        foreach (JsonElement item in Tails.GetProperty("chi2_sf").EnumerateArray())
        {
            double x = item.GetProperty("x").GetDouble(), df = item.GetProperty("df").GetDouble();
            double sf = item.GetProperty("value").GetDouble(), cdf = item.GetProperty("cdf").GetDouble();

            SpecialFunctionsGoldenTests.AssertClose(sf, StatInference.ChiSquaredSf(x, df), SpecialFunctionsGoldenTests.TailTolerance(sf), $"Sf_χ²({x:R}; {df:R})");
            SpecialFunctionsGoldenTests.AssertClose(cdf, StatInference.ChiSquaredCdf(x, df), SpecialFunctionsGoldenTests.TailTolerance(cdf), $"F_χ²({x:R}; {df:R})");
        }
    }

    [Fact]
    public void FSf_MatchesMpmath()
    {
        foreach (JsonElement item in Tails.GetProperty("f_sf").EnumerateArray())
        {
            double x = item.GetProperty("x").GetDouble();
            double df1 = item.GetProperty("df1").GetDouble(), df2 = item.GetProperty("df2").GetDouble();
            double expected = item.GetProperty("value").GetDouble();

            SpecialFunctionsGoldenTests.AssertClose(expected, StatInference.FSf(x, df1, df2), 2e-13, $"Sf_F({x:R}; {df1:R}, {df2:R})");

            // Левый хвост проверяется там, где он не съеден вычитанием в самом эталоне
            if (expected < 0.9) SpecialFunctionsGoldenTests.AssertClose(1 - expected, StatInference.FCdf(x, df1, df2), 2e-13, $"F_F({x:R}; {df1:R}, {df2:R})");
        }
    }

    [Fact]
    public void TQuantile_MatchesMpmath_IncludingFarTails()
    {
        foreach (JsonElement item in Quantiles.GetProperty("t").EnumerateArray())
        {
            double p = item.GetProperty("p").GetDouble(), df = item.GetProperty("df").GetDouble();

            // Вероятность 0.999999 сама задана с погрешностью в единицу 16-го знака, а хвост равен 1e-6:
            // относительная погрешность хвоста 1e-10 и есть предел для квантиля
            double tolerance = p > 0.99 ? 1e-9 : 2e-13;

            SpecialFunctionsGoldenTests.AssertClose(item.GetProperty("value").GetDouble(), StatInference.TQuantile(p, df), tolerance, $"t({p:R}; {df:R})");
        }
    }

    [Fact]
    public void ChiSquaredQuantile_MatchesMpmath_IncludingFarTails()
    {
        foreach (JsonElement item in Quantiles.GetProperty("chi2").EnumerateArray())
        {
            double p = item.GetProperty("p").GetDouble(), df = item.GetProperty("df").GetDouble();
            double tolerance = p > 0.99 ? 1e-7 : 5e-13;
            double expected = item.GetProperty("value").GetDouble();
            double actual = StatInference.ChiSquaredQuantile(p, df);

            if (expected == 0)
            {
                // Истинный квантиль 1.6e-600 меньше наименьшего double: годится и ноль, и наименьшее денормализованное
                Assert.InRange(actual, 0, 1e-320);

                continue;
            }

            SpecialFunctionsGoldenTests.AssertClose(expected, actual, tolerance, $"χ²({p:R}; {df:R})");
        }
    }

    /// <summary>Случаи из разведки: абсолютный критерий остановки и переход к 1 − p.</summary>
    [Fact]
    public void Quantiles_OfTinyProbability_AreNotLostToAbsoluteTolerance()
    {
        // Было 3.3e-13 вместо 1.57e-20: скобка сужалась до 1e-12 и останавливалась
        Assert.Equal(1.5707963267948968e-20, StatInference.ChiSquaredQuantile(1e-10, 1), 1e-32);

        // Было −597.3 только у целых степеней свободы; копулы давали −98.9 из-за зажима p и округления df
        Assert.Equal(-597.2854368003489, StatInference.TQuantile(1e-12, 4.6), 1e-9);
    }

    [Fact]
    public void PValues_OfStrongEffects_AreNotZero()
    {
        // 1 − Φ(10) в double равно нулю; хвост — 7.6e-24
        Assert.Equal(7.619853024160527e-24, StatInference.NormalSf(10), 1e-37);
        Assert.True(StatInference.TSf(50, 10) > 0);
        Assert.True(StatInference.ChiSquaredSf(400, 5) > 0);
        Assert.True(StatInference.FSf(300, 3, 40) > 0);
        Assert.Equal(1.0, StatInference.ZTest(0, 1, 100, 0).PValue, 15);
        Assert.True(StatInference.ZTest(1, 1, 100, 0).PValue is > 0 and < 1e-20);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(double.NaN)]
    public void InvalidDegreesOfFreedom_GiveNaN_NotSilentSubstitute(double df)
    {
        Assert.True(double.IsNaN(StatInference.TQuantile(0.9, df)));
        Assert.True(double.IsNaN(StatInference.TSf(1, df)));
        Assert.True(double.IsNaN(StatInference.ChiSquaredQuantile(0.9, df)));
        Assert.True(double.IsNaN(StatInference.ChiSquaredSf(1, df)));
        Assert.True(double.IsNaN(StatInference.FSf(1, df, 3)));
    }

    [Fact]
    public void Quantile_Edges()
    {
        Assert.Equal(double.NegativeInfinity, StatInference.TQuantile(0, 5));
        Assert.Equal(double.PositiveInfinity, StatInference.TQuantile(1, 5));
        Assert.Equal(0, StatInference.TQuantile(0.5, 5));
        Assert.Equal(0, StatInference.ChiSquaredQuantile(0, 5));
        Assert.Equal(double.PositiveInfinity, StatInference.ChiSquaredQuantile(1, 5));
    }
}
