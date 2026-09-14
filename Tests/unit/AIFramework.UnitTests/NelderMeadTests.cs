using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Метод Нелдера — Мида: задачи с известным минимумом, недопустимая область,
/// положительные параметры и честный признак несошедшегося поиска.
/// </summary>
public class NelderMeadTests
{
    [Fact]
    public void Minimize_Rosenbrock_FindsTheMinimum()
    {
        NelderMeadResult result = NelderMead.Minimize(
            p => ((1 - p[0]) * (1 - p[0])) + (100 * (p[1] - (p[0] * p[0])) * (p[1] - (p[0] * p[0]))),
            new Vector(-1.2, 1.0));

        Assert.True(result.Converged);
        Assert.Equal(1, result.Point[0], 3);
        Assert.Equal(1, result.Point[1], 3);
        Assert.True(result.Evaluations > result.Iterations);
    }

    [Fact]
    public void Minimize_Quadratic_InThreeDimensions()
    {
        NelderMeadResult result = NelderMead.Minimize(
            p => ((p[0] - 1) * (p[0] - 1)) + ((p[1] + 2) * (p[1] + 2)) + ((p[2] - 0.5) * (p[2] - 0.5)),
            new Vector(0.0, 0.0, 0.0));

        Assert.Equal(1, result.Point[0], 4);
        Assert.Equal(-2, result.Point[1], 4);
        Assert.Equal(0.5, result.Point[2], 4);
        Assert.Equal(0, result.Value, 8);
    }

    /// <summary>NaN означает недопустимую точку: симплекс обязан от неё отойти, а не застрять.</summary>
    [Fact]
    public void Minimize_NaNRegion_IsAvoided()
    {
        NelderMeadResult result = NelderMead.Minimize(
            p => p[0] < 0 ? double.NaN : (p[0] - 2) * (p[0] - 2),
            new Vector(0.5));

        Assert.Equal(2, result.Point[0], 4);
    }

    [Fact]
    public void MinimizePositive_NeverCrossesZero()
    {
        bool crossed = false;

        NelderMeadResult result = NelderMead.MinimizePositive(
            p =>
            {
                crossed |= p[0] <= 0;
                return (p[0] - 0.001) * (p[0] - 0.001);
            },
            new Vector(5.0));

        Assert.False(crossed);
        Assert.Equal(0.001, result.Point[0], 5);
    }

    /// <summary>Исчерпанный предел — не сходимость, и разбор обязан это сказать.</summary>
    [Fact]
    public void Minimize_IterationLimit_IsReportedAsNotConverged()
    {
        NelderMeadResult result = NelderMead.Minimize(
            p => (p[0] - 100) * (p[0] - 100),
            new Vector(0.0),
            new NelderMeadOptions { MaxIterations = 3 });

        Assert.False(result.Converged);
        Assert.Equal(3, result.Iterations);
        Assert.Contains(result.Interpret().Warnings, w => w.Contains("Сходимость не достигнута", StringComparison.Ordinal));
    }

    [Fact]
    public void Minimize_RejectsEmptyStartAndBadOptions()
    {
        Assert.Throws<ArgumentException>(() => NelderMead.Minimize(p => 0, new Vector(0)));
        Assert.Throws<ArgumentException>(() => NelderMead.Minimize(
            p => 0, new Vector(1.0), new NelderMeadOptions { Step = 0 }));
    }
}
