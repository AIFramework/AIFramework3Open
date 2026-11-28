using AI.DataStructs.Algebraic;
using AI.Econometrics;
using AI.Economics.Projects;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>
/// Экономика на общих численных методах: ставка доходности без зашитой скобки и видимый сдвиг
/// ковариационной матрицы в векторной авторегрессии.
/// </summary>
public class NumericCanonConsumersTests
{
    [Fact]
    public void InternalRateOfReturn_MatchesClosedForm()
    {
        // −100 + 60v + 60v² = 0, v = 1/(1 + r)
        double v = (-60 + Math.Sqrt(3600 + 24000)) / 120;

        Assert.Equal((1 / v) - 1, InvestmentCriteria.InternalRateOfReturn(new Vector(-100.0, 60.0, 60.0)), 11);
    }

    [Fact]
    public void InternalRateOfReturn_FindsRateOutsideFormerBracket()
    {
        // Прежде корень искали только на [−90 %; 1000 %]: поток с доходностью 1400 % получал «ставки нет»
        Assert.Equal(14.0, InvestmentCriteria.InternalRateOfReturn(new Vector(-1.0, 15.0)), 9);

        // Убыток 95 %: ставка левее прежней левой границы
        Assert.Equal(-0.95, InvestmentCriteria.InternalRateOfReturn(new Vector(-100.0, 5.0)), 11);
    }

    [Fact]
    public void InternalRateOfReturn_WithoutSignChange_IsNaN()
    {
        Assert.True(double.IsNaN(InvestmentCriteria.InternalRateOfReturn(new Vector(10.0, 20.0, 30.0))));
        Assert.True(double.IsNaN(InvestmentCriteria.InternalRateOfReturn(new Vector(-10.0, -20.0))));
    }

    [Fact]
    public void InternalRateOfReturn_WithSeveralRates_TakesNearestToTenPercent()
    {
        // −100 + 230v − 132v²: ставки 10 % и 20 %
        Assert.Equal(0.1, InvestmentCriteria.InternalRateOfReturn(new Vector(-100.0, 230.0, -132.0)), 9);
    }

    [Fact]
    public void ExtendedInternalRateOfReturn_ZeroesPresentValue()
    {
        var flows = new List<DatedCashFlow>
        {
            new(new DateOnly(2024, 1, 1), -1000),
            new(new DateOnly(2024, 2, 1), 1500),
        };

        double rate = InvestmentCriteria.ExtendedInternalRateOfReturn(flows);

        // Полуторный рост за месяц — годовая ставка около 11 700 %, далеко за прежней границей 1000 %
        Assert.True(rate > 10);
        Assert.Equal(0, InvestmentCriteria.ExtendedNetPresentValue(flows, rate), 6);
    }

    [Fact]
    public void VectorAutoregression_OnRegularData_NeedsNoCovarianceShift()
    {
        VarResult result = VectorAutoregression.Fit(Series(dependent: false), 1);

        Assert.Equal(0.0, result.CovarianceShift);
        Assert.DoesNotContain(result.Interpret().Warnings, warning => warning.Contains("вырождена", StringComparison.Ordinal));
    }

    [Fact]
    public void VectorAutoregression_OnLinearlyDependentSeries_ReportsShiftOrRefuses()
    {
        // Третий ряд повторяет первый: остатки двух уравнений совпадают, их ковариация вырождена.
        // Прежде «разложение» молча подрезало диагональ и возвращало не множитель исходной матрицы
        VarResult result;

        try
        {
            result = VectorAutoregression.Fit(Series(dependent: true), 1);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            // Отказ на вырожденных регрессорах — тоже честный исход: неудача видна вызывающему
            return;
        }

        Assert.True(result.CovarianceShift > 0, "Сдвиг диагонали обязан попасть в результат.");
        Assert.Contains(result.Interpret().Warnings, warning => warning.Contains("вырождена", StringComparison.Ordinal));

        // Сдвиг порядка 1e-12 обязан читаться в тексте, а не округляться до «0.000»
        Assert.Contains(result.Interpret().Warnings, warning => warning.Contains("E-", StringComparison.Ordinal));
    }

    private static Matrix Series(bool dependent)
    {
        Random rng = RandomEngine.Create(5);
        const int n = 200;

        var data = new Matrix(n, 3);
        double first = 0, second = 0, third = 0;

        for (int t = 0; t < n; t++)
        {
            first = (0.5 * first) + RandomEngine.NextGaussian(rng, 0, 1);
            second = (0.3 * second) + (0.2 * first) + RandomEngine.NextGaussian(rng, 0, 1);
            third = dependent ? first : (0.4 * third) + RandomEngine.NextGaussian(rng, 0, 1);

            data[t, 0] = first;
            data[t, 1] = second;
            data[t, 2] = third;
        }

        return data;
    }
}
