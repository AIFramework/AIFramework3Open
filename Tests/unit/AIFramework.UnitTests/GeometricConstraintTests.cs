using AI.Geometry.Constraints;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Решатель геометрических ограничений <see cref="GeometricSketch"/>: построение задач из сущностей
/// и ограничений, анализ степеней свободы, противоречий и избыточности, поиск нескольких ветвей.
/// </summary>
public class GeometricConstraintTests
{
    private const double Tolerance = 1e-7;

    // Прямоугольник ABCD со стороной AB = 3 и AD = 4, угол A закреплен в начале координат
    private static GeometricSketch Rectangle(bool withWidth = true)
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("A", 0.1, -0.2);
        sketch.AddPoint("B", 2.5, 0.3);
        sketch.AddPoint("C", 2.7, 3.6);
        sketch.AddPoint("D", -0.2, 3.8);
        sketch.AddLine("AB", "A", "B");
        sketch.AddLine("BC", "B", "C");
        sketch.AddLine("DC", "D", "C");
        sketch.AddLine("AD", "A", "D");
        sketch.FixPoint("A", 0, 0);
        sketch.Horizontal("AB");
        sketch.Horizontal("DC");
        sketch.Vertical("AD");
        sketch.Vertical("BC");
        sketch.Length("AD", 4);

        if (withWidth)
            sketch.Length("AB", 3);

        return sketch;
    }

    [Fact]
    public void GeometricSketch_Solve_TriangleWithSides345HasRightAngle()
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("A", 0, 0, isFixed: true);
        sketch.AddPoint("B", 2.5, 0.4);
        sketch.AddPoint("C", 2.0, 3.5);
        sketch.AddLine("AB", "A", "B");
        sketch.Horizontal("AB");
        sketch.Distance("A", "B", 3);
        sketch.Distance("B", "C", 4);
        sketch.Distance("C", "A", 5);

        SketchSolution solution = sketch.Solve();

        Assert.True(solution.Satisfied);
        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(0, solution.DegreesOfFreedom);
        Assert.Equal(Math.PI / 2, solution.AngleAt("A", "B", "C"), Tolerance);
        Assert.Equal(6, solution.Area("A", "B", "C"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_SolveAll_TwoCirclesIntersectInTwoBranches()
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("O1", 0, 0, isFixed: true);
        sketch.AddPoint("O2", 6, 0, isFixed: true);
        sketch.AddCircle("c1", "O1", 5, isRadiusFixed: true);
        sketch.AddCircle("c2", "O2", 5, isRadiusFixed: true);
        sketch.AddPoint("P", 3.5, 1);
        sketch.PointOnCircle("P", "c1");
        sketch.PointOnCircle("P", "c2");

        IReadOnlyList<SketchSolution> branches = sketch.SolveAll();

        Assert.Equal(2, branches.Count);
        Assert.All(branches, b => Assert.Equal(SketchStatus.WellConstrained, b.Status));
        Assert.Equal(3, branches[0].Value("P.x"), Tolerance);
        Assert.Equal(4, branches[0].Value("P.y"), Tolerance);
        Assert.Equal(3, branches[1].Value("P.x"), Tolerance);
        Assert.Equal(-4, branches[1].Value("P.y"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_SolveAll_CircleTangentToAxesThroughPointHasTwoRadii()
    {
        // Центр (r, r), точка (a, b) = (2, 1): r² - 2(a + b)r + a² + b² = 0, откуда r = a + b ± √(2ab) = 1 или 5
        var sketch = new GeometricSketch();
        sketch.AddPoint("O", 0, 0, isFixed: true);
        sketch.AddPoint("E", 1, 0, isFixed: true);
        sketch.AddPoint("F", 0, 1, isFixed: true);
        sketch.AddLine("ox", "O", "E");
        sketch.AddLine("oy", "O", "F");
        sketch.AddPoint("K", 2, 2);
        sketch.AddCircle("c", "K", 2);
        sketch.AddPoint("P", 2, 1, isFixed: true);
        sketch.Tangent("ox", "c");
        sketch.Tangent("oy", "c");
        sketch.PointOnCircle("P", "c");

        IReadOnlyList<SketchSolution> branches = sketch.SolveAll();

        Assert.Equal(2, branches.Count);
        double[] radii = branches.Select(b => b.Radius("c")).OrderBy(r => r).ToArray();
        Assert.Equal(1, radii[0], Tolerance);
        Assert.Equal(5, radii[1], Tolerance);
        Assert.All(branches, b => Assert.Equal(b.Radius("c"), b.Value("K.x"), Tolerance));
    }

    [Fact]
    public void GeometricSketch_Solve_RectangleIsWellConstrained()
    {
        SketchSolution solution = Rectangle().Solve();

        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(0, solution.DegreesOfFreedom);
        Assert.Equal(0, solution.Redundancy);
        Assert.Empty(solution.UndeterminedUnknowns);
        Assert.Equal(12, solution.Area("A", "B", "C", "D"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_Solve_RectangleWithoutWidthHasOneDegreeOfFreedom()
    {
        SketchSolution solution = Rectangle(withWidth: false).Solve();

        Assert.True(solution.Satisfied);
        Assert.Equal(SketchStatus.UnderConstrained, solution.Status);
        Assert.Equal(1, solution.DegreesOfFreedom);
        Assert.Equal(["B.x", "C.x"], solution.UndeterminedUnknowns.OrderBy(n => n).ToArray());
    }

    [Fact]
    public void GeometricSketch_Solve_ContradictoryDistanceIsInconsistentAndNamed()
    {
        GeometricSketch sketch = Rectangle();
        sketch.Distance("B", "D", 10, name: "diagonal");

        SketchSolution solution = sketch.Solve();

        Assert.False(solution.Satisfied);
        Assert.Equal(SketchStatus.Inconsistent, solution.Status);
        Assert.Equal("diagonal", solution.ViolatedConstraints[0]);
    }

    [Fact]
    public void GeometricSketch_Solve_ConsistentExtraDistanceIsRedundant()
    {
        GeometricSketch sketch = Rectangle();
        sketch.Distance("B", "D", 5);

        SketchSolution solution = sketch.Solve();

        Assert.True(solution.Satisfied);
        Assert.Equal(SketchStatus.Redundant, solution.Status);
        Assert.Equal(0, solution.DegreesOfFreedom);
        Assert.Equal(1, solution.Redundancy);
    }

    [Fact]
    public void GeometricSketch_SolveAll_TangentSegmentLengthMatchesPythagoras()
    {
        // Из точки P на расстоянии 13 от центра к окружности радиуса 5 проведена касательная: PT = √(13² - 5²) = 12
        var sketch = new GeometricSketch();
        sketch.AddPoint("P", 0, 0, isFixed: true);
        sketch.AddPoint("O", 13, 0, isFixed: true);
        sketch.AddCircle("w", "O", 5, isRadiusFixed: true);
        sketch.AddPoint("T", 10, 3);
        sketch.AddLine("PT", "P", "T");
        sketch.AddLine("OT", "O", "T");
        sketch.PointOnCircle("T", "w");
        sketch.Perpendicular("PT", "OT");

        IReadOnlyList<SketchSolution> branches = sketch.SolveAll();

        Assert.Equal(2, branches.Count);
        Assert.All(branches, b => Assert.Equal(12, b.Distance("P", "T"), Tolerance));
        Assert.True(branches[0].Value("T.y") > 0);
    }

    [Fact]
    public void GeometricSketch_Solve_ParameterFromExpressionConstraint()
    {
        // Квадрат ABCD площадью 16: сторона s неизвестна и связана с площадью выражением
        var sketch = new GeometricSketch();
        sketch.AddParameter("s", 1);
        sketch.AddPoint("A", 0, 0, isFixed: true);
        sketch.AddPoint("B", 1, 0.1);
        sketch.AddPoint("D", 0.1, 1);
        sketch.AddLine("AB", "A", "B");
        sketch.AddLine("AD", "A", "D");
        sketch.Horizontal("AB");
        sketch.Perpendicular("AB", "AD");
        sketch.Length("AB", "s");
        sketch.EqualLength("AB", "AD");
        sketch.Expression(["s"], v => (v["s"] * v["s"]) - 16, name: "area");

        SketchSolution solution = sketch.Solve();

        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(4, solution.Value("s"), Tolerance);
        Assert.Equal(4, solution.Length("AD"), Tolerance);
        Assert.Equal(Math.PI / 2, solution.Angle("AB", "AD"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_Solve_SymmetricPointReflectsAboutDiagonal()
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("O", 0, 0, isFixed: true);
        sketch.AddPoint("U", 1, 1, isFixed: true);
        sketch.AddLine("d", "O", "U");
        sketch.AddPoint("P", 1, 3, isFixed: true);
        sketch.AddPoint("Q", 2, 2);
        sketch.AddPoint("M", 0, 0);
        sketch.Symmetric("P", "Q", "d");
        sketch.Midpoint("M", "P", "Q");

        SketchSolution solution = sketch.Solve();

        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(3, solution.Value("Q.x"), Tolerance);
        Assert.Equal(1, solution.Value("Q.y"), Tolerance);
        Assert.Equal(0, solution.DistanceToLine("M", "d"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_SolveAll_ExternallyTangentCirclesOnAxis()
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("O", 0, 0, isFixed: true);
        sketch.AddPoint("E", 1, 0, isFixed: true);
        sketch.AddLine("ox", "O", "E");
        sketch.AddCircle("a", "O", 1, isRadiusFixed: true);
        sketch.AddPoint("K", 2, 0.5);
        sketch.AddCircle("b", "K", 2, isRadiusFixed: true);
        sketch.PointOnLine("K", "ox");
        sketch.TangentCircles("a", "b");

        IReadOnlyList<SketchSolution> branches = sketch.SolveAll();

        Assert.Equal(2, branches.Count);
        Assert.Equal(3, branches[0].Value("K.x"), Tolerance);
        Assert.Equal(-3, branches[1].Value("K.x"), Tolerance);
    }
}
