using System.Text.Json;
using AI.HighLevelFunctions;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Спецфункции против mpmath (50 знаков).
/// </summary>
/// <remarks>
/// Эталоны выгружает Tools/MathGolden/export_special_golden.py в Golden/special_functions.json;
/// Python при тестах не нужен. Допуск везде относительный: функции зовут ради хвостов, где
/// значение порядка 1e-100, и абсолютный допуск там ничего бы не проверял.
/// </remarks>
public class SpecialFunctionsGoldenTests
{
    internal static readonly Lazy<JsonElement> Data = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "special_functions.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    });

    [Fact]
    public void LogGamma_MatchesMpmath()
    {
        foreach (JsonElement item in Data.Value.GetProperty("log_gamma").EnumerateArray())
        {
            double x = item.GetProperty("x").GetDouble();

            AssertClose(item.GetProperty("value").GetDouble(), SpecialFunctions.LogGamma(x), 4e-15, $"lnΓ({x:R})");
        }
    }

    [Fact]
    public void Gamma_MatchesMpmath()
    {
        foreach (JsonElement item in Data.Value.GetProperty("gamma").EnumerateArray())
        {
            double x = item.GetProperty("x").GetDouble();

            AssertClose(item.GetProperty("value").GetDouble(), SpecialFunctions.Gamma(x), 5e-15, $"Γ({x:R})");
        }
    }

    [Fact]
    public void Gamma_OfInteger_IsExact()
    {
        Assert.Equal(120.0, SpecialFunctions.Gamma(6));
        Assert.Equal(1.0, SpecialFunctions.Gamma(1));
        Assert.Equal(2432902008176640000.0, SpecialFunctions.Gamma(21));
        Assert.Equal(double.PositiveInfinity, SpecialFunctions.Gamma(0));
        Assert.Equal(double.PositiveInfinity, SpecialFunctions.Gamma(-3));
        Assert.Equal(double.PositiveInfinity, SpecialFunctions.Gamma(200));
    }

    [Fact]
    public void RegularizedGamma_MatchesMpmath()
    {
        foreach (JsonElement item in Data.Value.GetProperty("incomplete_gamma").EnumerateArray())
        {
            double a = item.GetProperty("a").GetDouble(), x = item.GetProperty("x").GetDouble();

            double p = item.GetProperty("p").GetDouble(), q = item.GetProperty("q").GetDouble();

            AssertClose(p, SpecialFunctions.RegularizedGammaP(a, x), TailTolerance(p), $"P({a:R}, {x:R})");
            AssertClose(q, SpecialFunctions.RegularizedGammaQ(a, x), TailTolerance(q), $"Q({a:R}, {x:R})");
        }
    }

    [Fact]
    public void RegularizedBeta_MatchesMpmath()
    {
        foreach (JsonElement item in Data.Value.GetProperty("incomplete_beta").EnumerateArray())
        {
            double a = item.GetProperty("a").GetDouble(), b = item.GetProperty("b").GetDouble(), x = item.GetProperty("x").GetDouble();

            // При больших a и b множитель x^a (1 − x)^b / B(a, b) теряет знаки на вычитании логарифмов
            double tolerance = Math.Max(a, b) > 500 ? 2e-11 : 2e-13;

            AssertClose(item.GetProperty("value").GetDouble(), SpecialFunctions.RegularizedBeta(a, b, x), tolerance, $"I_{x:R}({a:R}, {b:R})");
            // Дополнение считается от 1 − x, а при малом x эта разность уже не несёт его знаков
            if (x >= 0.05)
            {
                AssertClose(item.GetProperty("complement").GetDouble(), SpecialFunctions.RegularizedBeta(b, a, 1 - x), tolerance, $"1 − I_{x:R}({a:R}, {b:R})");
            }
        }
    }

    [Fact]
    public void ErfAndErfc_MatchMpmath()
    {
        foreach (JsonElement item in Data.Value.GetProperty("erf").EnumerateArray())
        {
            double x = item.GetProperty("x").GetDouble();
            double erfc = item.GetProperty("erfc").GetDouble();

            AssertClose(item.GetProperty("erf").GetDouble(), SpecialFunctions.Erf(x), 4e-15, $"erf({x:R})");

            // Ниже наименьшего нормального числа у double остаётся меньше 53 разрядов — точность падает сама
            AssertClose(erfc, SpecialFunctions.Erfc(x), erfc < 1e-300 ? 1e-8 : 4e-15, $"erfc({x:R})");
        }
    }

    [Fact]
    public void Erf_OfTinyArgument_IsNotZero()
    {
        // x² обращается в ноль уже при 1.5e-154; первый член ряда 2x/√π точен
        Assert.Equal(1.1283791670955126e-200, SpecialFunctions.Erf(1e-200), 1e-215);
        Assert.Equal(-1.1283791670955126e-200, SpecialFunctions.Erf(-1e-200), 1e-215);
    }

    [Fact]
    public void Factorial_MatchesMpmath_AndDoesNotOverflow()
    {
        foreach (JsonElement item in Data.Value.GetProperty("factorial").EnumerateArray())
        {
            int n = item.GetProperty("n").GetInt32();
            double expected = item.GetProperty("value").GetDouble();

            Assert.Equal(expected, SpecialFunctions.Factorial(n));
            AssertClose(Math.Log(expected), SpecialFunctions.LogFactorial(n), 1e-15, $"ln {n}!");
        }

        Assert.Equal(51090942171709440000.0, SpecialFunctions.Factorial(21));
        Assert.Equal(double.PositiveInfinity, SpecialFunctions.Factorial(171));
        Assert.Equal(2611.3304584601556, SpecialFunctions.LogFactorial(500), 1e-10);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SpecialFunctions.Factorial(-1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    public void LogGamma_OutsideDomain_IsNaN(double x) => Assert.True(double.IsNaN(SpecialFunctions.LogGamma(x)));

    [Fact]
    public void IncompleteFunctions_OutsideDomain_AreNaN_AndEdgesAreExact()
    {
        Assert.True(double.IsNaN(SpecialFunctions.RegularizedGammaP(-1, 1)));
        Assert.True(double.IsNaN(SpecialFunctions.RegularizedGammaQ(1, -1)));
        Assert.True(double.IsNaN(SpecialFunctions.RegularizedBeta(0, 1, 0.5)));
        Assert.Equal(0, SpecialFunctions.RegularizedGammaP(2, 0));
        Assert.Equal(1, SpecialFunctions.RegularizedGammaQ(2, 0));
        Assert.Equal(1, SpecialFunctions.RegularizedGammaP(2, double.PositiveInfinity));
        Assert.Equal(0, SpecialFunctions.RegularizedBeta(2, 3, 0));
        Assert.Equal(1, SpecialFunctions.RegularizedBeta(2, 3, 1));
    }

    /// <summary>
    /// Допуск для значения, полученного экспонентой: показатель порядка ln(value) несёт относительную
    /// ошибку округления, и в результате она умножается на сам показатель.
    /// </summary>
    internal static double TailTolerance(double value) =>
        value > 0 && value < 1e-100 ? 2e-12 : 2e-13;

    /// <summary>Относительное отклонение не больше допуска; ноль и бесконечность — точное совпадение.</summary>
    internal static void AssertClose(double expected, double actual, double tolerance, string what)
    {
        if (expected == 0 || double.IsInfinity(expected))
        {
            Assert.True(expected == actual, $"{what}: ожидалось {expected:R}, получено {actual:R}");

            return;
        }

        double error = Math.Abs(actual - expected) / Math.Abs(expected);

        Assert.True(error <= tolerance, $"{what}: ожидалось {expected:R}, получено {actual:R}, отн. отклонение {error:E2}");
    }
}
