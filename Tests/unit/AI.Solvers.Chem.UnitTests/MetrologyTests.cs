using AI.Solvers.Chem.Metrology;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Градуировка, промахи, неопределённость, контрольные карты, прецизионность.</summary>
public class MetrologyTests
{
    private static readonly double[] Concentrations = { 0.0, 1, 2, 5, 10 };
    private static readonly double[] Signals = { 0.012, 0.105, 0.203, 0.501, 0.998 };

    private static AnalyticalCalibration Calibration() => new(Concentrations, Signals);

    [Fact]
    public void ExactLine_IsRecoveredWithoutResidual()
    {
        var fit = LinearFit.Fit(new[] { 1.0, 2, 3, 4, 5 }, new[] { 2.5, 4.0, 5.5, 7.0, 8.5 });

        Assert.Equal(1.5, fit.Slope, 1e-9);
        Assert.Equal(1.0, fit.Intercept, 1e-9);
        Assert.Equal(1.0, fit.R2, 1e-12);
        Assert.Equal(0.0, fit.ResidualStd, 1e-9);
    }

    [Fact]
    public void SlopeStandardError_FollowsDefinition()
    {
        var fit = LinearFit.Fit(new[] { 0.0, 1, 2, 3, 4, 5 }, new[] { 0.05, 2.1, 3.9, 6.05, 8.0, 9.9 });

        Assert.Equal(fit.ResidualStd / Math.Sqrt(fit.Sxx), fit.SlopeStdError, 1e-12);
        Assert.True(fit.R2 > 0.999, $"R² = {fit.R2:F5}");
        Assert.False(fit.InterceptIsSignificant());
    }

    /// <summary>Взвешивание 1/x² переносит вес на нижние точки диапазона.</summary>
    [Fact]
    public void Weighting_ChangesSlope()
    {
        double[] x = { 1.0, 10, 100 };
        double[] y = { 2.1, 19.5, 205.0 };

        var weighted = LinearFit.Fit(x, y, WeightingScheme.InverseX2);
        var plain = LinearFit.Fit(x, y);

        Assert.NotEqual(plain.Slope, weighted.Slope, 6);
    }

    [Fact]
    public void DetectionLimits_FollowIchDefinition()
    {
        AnalyticalCalibration calibration = Calibration();

        Assert.Equal(3.3 * calibration.Fit.ResidualStd / calibration.Fit.Slope, calibration.DetectionLimit, 1e-12);
        Assert.Equal(10.0 / 3.3, calibration.QuantitationLimit / calibration.DetectionLimit, 1e-9);
    }

    [Fact]
    public void InversePrediction_ReturnsConcentrationWithInterval()
    {
        ConcentrationEstimate estimate = Calibration().Concentration(0.501);

        Assert.Equal(5.0, estimate.Value, 0.05);
        Assert.True(estimate.Lower < 5 && estimate.Upper > 5,
            $"интервал [{estimate.Lower:F3}; {estimate.Upper:F3}] не накрывает 5");
        Assert.True(estimate.WithinRange);
    }

    [Fact]
    public void SignalAboveRange_IsFlagged()
        => Assert.False(Calibration().Concentration(2.5).WithinRange);

    /// <summary>
    /// Критическое значение критерия Граббса считается из квантиля Стьюдента;
    /// сверка с опубликованной таблицей для α = 0.05.
    /// </summary>
    [Theory]
    [InlineData(8, 2.126)]
    [InlineData(10, 2.290)]
    [InlineData(20, 2.709)]
    public void GrubbsCriticalValue_MatchesTable(int n, double expected)
        => Assert.Equal(expected, OutlierTests.GrubbsCritical(n), 0.01);

    [Fact]
    public void Grubbs_FindsOutlier()
    {
        OutlierResult result = OutlierTests.Grubbs(new[] { 10.1, 10.2, 10.0, 10.15, 10.05, 12.4 });

        Assert.True(result.IsOutlier);
        Assert.Equal(12.4, result.Value, 1e-9);
    }

    [Fact]
    public void Grubbs_AcceptsUniformSeries()
        => Assert.False(OutlierTests.Grubbs(new[] { 10.1, 10.2, 10.0, 10.15, 10.05, 10.12 }).IsOutlier);

    /// <summary>Промахи по обе стороны маскируют друг друга - известное свойство критерия.</summary>
    [Fact]
    public void Grubbs_IsMaskedByTwoSidedOutliers()
        => Assert.False(OutlierTests.Grubbs(new[] { 10.1, 10.2, 10.0, 12.4, 10.05, 7.1 }).IsOutlier);

    [Fact]
    public void GrubbsIterative_RemovesOutliersOneByOne()
    {
        var (clean, removed) = OutlierTests.GrubbsIterative(
            new[] { 10.0, 10.1, 9.9, 10.05, 9.95, 10.02, 9.98, 10.03, 10.5, 11.0 });

        Assert.Equal(2, removed.Count);
        Assert.Equal(8, clean.Length);
    }

    [Fact]
    public void DixonQ_MatchesClassicExample()
    {
        OutlierResult result = OutlierTests.Dixon(new[] { 0.189, 0.169, 0.187, 0.183 });

        Assert.Equal(0.70, result.Statistic, 0.01);
        Assert.Equal(0.829, result.CriticalValue, 1e-9);
        Assert.False(result.IsOutlier);
    }

    [Fact]
    public void DixonQ_RejectsLargeSeries()
        => Assert.Throws<ArgumentException>(() => OutlierTests.Dixon(Enumerable.Repeat(1.0, 15).ToArray()));

    [Fact]
    public void UncertaintyBudget_CombinesComponentsAsRootSumOfSquares()
    {
        var budget = new UncertaintyBudget("массовая доля", 12.5, "%")
            .Add(new UncertaintyComponent { Name = "градуировка", Value = 0.1 })
            .Add(new UncertaintyComponent { Name = "навеска", Value = 0.2 })
            .Add(new UncertaintyComponent { Name = "объём", Value = 0.2 });

        Assert.Equal(0.3, budget.CombinedStandardUncertainty, 1e-12);
        Assert.Equal(1.96, budget.CoverageFactor(), 0.01);
        Assert.Equal(0.588, budget.ExpandedUncertainty(), 0.005);
    }

    [Fact]
    public void RectangularDistribution_UsesSqrtThreeDivisor()
    {
        var budget = new UncertaintyBudget("объём", 100, "мл")
            .Add(UncertaintyComponent.FromTolerance("колба", 0.6));

        Assert.Equal(0.6 / Math.Sqrt(3), budget.CombinedStandardUncertainty, 1e-12);
    }

    [Fact]
    public void TypeAComponent_UsesStandardErrorOfMean()
    {
        double[] series = { 10.1, 10.2, 10.0, 10.15 };
        UncertaintyComponent component = UncertaintyComponent.FromSeries("повторяемость", series);

        double mean = series.Average();
        double std = Math.Sqrt(series.Sum(v => (v - mean) * (v - mean)) / (series.Length - 1));

        Assert.True(component.IsTypeA);
        Assert.Equal(3, component.DegreesOfFreedom);
        Assert.Equal(std / Math.Sqrt(series.Length), component.StandardUncertainty, 1e-12);
    }

    /// <summary>Малое число степеней свободы поднимает коэффициент охвата выше 1.96.</summary>
    [Fact]
    public void CoverageFactor_GrowsWithFewDegreesOfFreedom()
    {
        var budget = new UncertaintyBudget("результат", 1.0)
            .Add(UncertaintyComponent.FromSeries("повторяемость", new[] { 1.0, 1.1, 0.9 }));

        Assert.True(budget.CoverageFactor() > 2.5, $"k = {budget.CoverageFactor():F2}");
    }

    [Fact]
    public void ControlChart_StableProcessHasNoViolations()
    {
        var chart = new ControlChart(new[] { 10.0, 10.1, 9.9, 10.05, 9.95, 10.02, 9.98, 10.03 });

        Assert.True(chart.InControl, string.Join("; ", chart.Violations()));
        Assert.Equal(chart.MovingRanges.Average() / 1.128, chart.Sigma, 1e-12);
    }

    [Fact]
    public void ControlChart_DetectsPointBeyondThreeSigma()
    {
        var chart = new ControlChart(new[] { 10.0, 10.1, 9.9, 10.05, 9.95, 10.02, 9.98, 10.03, 12.5 });

        Assert.Contains(chart.Violations(), v => v.Rule.Contains("3σ", StringComparison.Ordinal));
    }

    [Fact]
    public void ControlChart_DetectsTrend()
    {
        var chart = new ControlChart(new[] { 9.0, 9.2, 9.4, 9.6, 9.8, 10.0, 10.2, 10.4 });

        Assert.Contains(chart.Violations(), v => v.Rule.Contains("возрастают", StringComparison.Ordinal));
    }

    /// <summary>
    /// Серии без внутреннего разброса: повторяемость нулевая, межсерийное СКО
    /// равно разбросу средних.
    /// </summary>
    [Fact]
    public void Precision_SeparatesWithinAndBetweenSeries()
    {
        PrecisionResult result = MethodValidation.Precision(new[]
        {
            new[] { 10.0, 10.0, 10.0 },
            new[] { 12.0, 12.0, 12.0 },
            new[] { 14.0, 14.0, 14.0 }
        });

        Assert.Equal(0.0, result.RepeatabilityStd, 1e-9);
        Assert.Equal(2.0, result.BetweenGroupStd, 1e-9);
        Assert.Equal(12.0, result.GrandMean, 1e-9);
    }

    [Fact]
    public void Precision_IntermediateIsNotBelowRepeatability()
    {
        PrecisionResult result = MethodValidation.Precision(new[]
        {
            new[] { 10.1, 10.2, 10.0 },
            new[] { 10.3, 10.2, 10.4 },
            new[] { 9.9, 10.0, 10.1 }
        });

        Assert.True(result.IntermediateStd >= result.RepeatabilityStd - 1e-12);
        Assert.Equal(2.8 * result.RepeatabilityStd, result.RepeatabilityLimit, 1e-12);
    }

    [Fact]
    public void Precision_RequiresAtLeastTwoSeries()
        => Assert.Throws<ArgumentException>(() => MethodValidation.Precision(new[] { new[] { 1.0, 2.0 } }));

    [Fact]
    public void Recovery_AcceptsUnbiasedMethod()
    {
        RecoveryResult result = MethodValidation.Recovery(
            new[] { 9.8, 10.1, 9.9, 10.2 }, new[] { 10.0, 10.0, 10.0, 10.0 });

        Assert.Equal(100.0, result.MeanRecoveryPercent, 1.0);
        Assert.False(result.BiasSignificant);
    }

    [Fact]
    public void Recovery_DetectsSystematicLoss()
    {
        RecoveryResult result = MethodValidation.Recovery(
            new[] { 8.0, 8.1, 7.9, 8.05 }, new[] { 10.0, 10.0, 10.0, 10.0 });

        Assert.True(result.BiasSignificant);
        Assert.True(result.MeanRecoveryPercent < 85, $"{result.MeanRecoveryPercent:F1}%");
    }

    [Fact]
    public void DetectionLimitFromBlank_FollowsDefinition()
    {
        double[] blank = { 0.01, 0.012, 0.009, 0.011 };
        double mean = blank.Average();
        double std = Math.Sqrt(blank.Sum(v => (v - mean) * (v - mean)) / (blank.Length - 1));

        Assert.Equal(3 * std / 0.1, MethodValidation.DetectionLimitFromBlank(blank, 0.1), 1e-9);
    }

    [Fact]
    public void CalibrationCheck_ReportsLinearity()
    {
        CalibrationCheck check = Calibration().Check();

        Assert.True(check.Linear);
        Assert.True(check.R2 > 0.999, $"R² = {check.R2:F5}");
    }
}
