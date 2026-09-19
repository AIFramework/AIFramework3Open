#nullable enable

using AI.Geometry.Constraints;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Якобиан решения эскиза и чувствительность к закрепленным параметрам: треугольник по трем сторонам, где одна
/// сторона задана параметром, дает производные вершин, совпадающие с повторным решением при сдвинутом параметре.
/// </summary>
public class SketchSolutionJacobianTests
{
    // Треугольник ABC: A в начале, B на оси x, |AB| = c (параметр), |BC| = 4, |CA| = 5
    private static GeometricSketch Triangle(double c)
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("A", 0, 0, isFixed: true);
        sketch.AddPoint("B", 2.5, 0);
        sketch.AddPoint("C", 1, 3);
        sketch.SetGuess("B.y", 0, isFixed: true);
        sketch.AddParameter("c", c, isFixed: true);
        sketch.Distance("A", "B", "c", "ab");
        sketch.Distance("B", "C", 4, "bc");
        sketch.Distance("C", "A", 5, "ca");
        return sketch;
    }

    [Fact]
    public void Jacobian_HasRowPerEquationAndColumnPerFreeUnknown()
    {
        SketchSolution solution = Triangle(3).Solve();

        Assert.True(solution.Satisfied);
        Assert.Equal(["B.x", "C.x", "C.y"], solution.FreeUnknowns);
        Assert.Equal(["ab", "bc", "ca"], solution.EquationConstraints);
        Assert.Equal(3, solution.Jacobian.Height);
        Assert.Equal(3, solution.Jacobian.Width);
        Assert.Equal(3, solution.Rank);
        // Уравнение |CA| = 5 не зависит от B.x
        Assert.Equal(0, solution.Jacobian[2, 0], 12);
    }

    [Fact]
    public void Sensitivity_MatchesFiniteDifferenceOfResolve()
    {
        const double step = 1e-4;
        SketchSolution solution = Triangle(3).Solve();
        SketchSolution plus = Triangle(3 + step).Solve();
        SketchSolution minus = Triangle(3 - step).Solve();

        IReadOnlyDictionary<string, double> derivative = solution.Sensitivity("c");

        foreach (string unknown in solution.FreeUnknowns)
        {
            double expected = (plus.Value(unknown) - minus.Value(unknown)) / (2 * step);
            Assert.Equal(expected, derivative[unknown], 4);
        }

        // Сторона AB лежит на оси x от начала, поэтому B.x растет вместе с c один к одному
        Assert.Equal(1, derivative["B.x"], 6);
    }

    [Fact]
    public void Sensitivity_OfFreeUnknown_IsRefused()
    {
        SketchSolution solution = Triangle(3).Solve();

        Assert.Throws<ArgumentException>(() => solution.Sensitivity("C.x"));
    }

    [Fact]
    public void Sensitivity_UnderConstrained_GivesLeastNormDerivative()
    {
        // Только |AB| = c: C свободна, ее производная по c равна нулю, а B.x по-прежнему один к одному
        var sketch = new GeometricSketch();
        sketch.AddPoint("A", 0, 0, isFixed: true);
        sketch.AddPoint("B", 2.5, 0);
        sketch.AddPoint("C", 1, 3);
        sketch.SetGuess("B.y", 0, isFixed: true);
        sketch.AddParameter("c", 3, isFixed: true);
        sketch.Distance("A", "B", "c");
        SketchSolution solution = sketch.Solve();

        IReadOnlyDictionary<string, double> derivative = solution.Sensitivity("c");

        Assert.Equal(SketchStatus.UnderConstrained, solution.Status);
        Assert.Equal(1, derivative["B.x"], 6);
        Assert.Equal(0, derivative["C.x"], 10);
        Assert.Equal(0, derivative["C.y"], 10);
    }
}
