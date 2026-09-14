#nullable enable

using AI.Geometry.Constraints;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Пространственный эскиз <see cref="GeometricSketch"/>: куб из трех перпендикулярных ребер и переносов,
/// правильный тетраэдр, плоскости, угол прямой с плоскостью, поворот вокруг прямой.
/// </summary>
public class GeometricSketchSpaceTests
{
    private const double Tolerance = 1e-7;

    // Куб ABCDA1B1C1D1 с ребром a: A в начале, B на оси x, D в плоскости xy; C и C1 получены переносами
    private static GeometricSketch Cube(double a)
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("A", 0, 0, 0, isFixed: true);
        sketch.AddPoint("B", 1.5, 0, 0);
        sketch.AddPoint("D", 0.2, 1.7, 0);
        sketch.AddPoint("A1", 0.3, 0.1, 1.8);
        sketch.AddPoint("C", 1, 1, 0.3);
        sketch.AddPoint("C1", 1, 1, 1);
        sketch.SetGuess("B.y", 0, isFixed: true);
        sketch.SetGuess("B.z", 0, isFixed: true);
        sketch.SetGuess("D.z", 0, isFixed: true);
        sketch.AddLine("AB", "A", "B");
        sketch.AddLine("AD", "A", "D");
        sketch.AddLine("AA1", "A", "A1");
        sketch.AddLine("AC1", "A", "C1");
        sketch.Length("AB", a);
        sketch.Length("AD", a);
        sketch.Length("AA1", a);
        sketch.Perpendicular("AB", "AD");
        sketch.Perpendicular("AB", "AA1");
        sketch.Perpendicular("AD", "AA1");
        sketch.Translation("C", "B", "AD");
        sketch.Translation("C1", "C", "AA1");
        return sketch;
    }

    [Fact]
    public void GeometricSketch_Solve_CubeSpaceDiagonalIsRootThreeEdges()
    {
        SketchSolution solution = Cube(2).Solve();

        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(2 * Math.Sqrt(3), solution.Distance("A", "C1"), Tolerance);
        Assert.Equal(2, Math.Abs(solution.Value("C1.z")), Tolerance);
    }

    [Fact]
    public void GeometricSketch_SolveAll_RegularTetrahedronHasTwoMirrorBranchesOfOneVolume()
    {
        const double a = 3;
        var sketch = new GeometricSketch();
        sketch.AddPoint("A", 0, 0, 0, isFixed: true);
        sketch.AddPoint("B", a, 0, 0, isFixed: true);
        sketch.AddPoint("C", a / 2, a * Math.Sqrt(3) / 2, 0, isFixed: true);
        sketch.AddPoint("D", 0.4 * a, 0.3 * a, 0.5 * a);
        string[] points = ["A", "B", "C", "D"];

        for (int i = 0; i < points.Length; i++)
        {
            for (int j = i + 1; j < points.Length; j++)
                sketch.Distance(points[i], points[j], a);
        }

        IReadOnlyList<SketchSolution> branches = sketch.SolveAll();

        Assert.Equal(2, branches.Count);
        Assert.All(branches, b => Assert.True(b.Satisfied));
        Assert.All(branches, b => Assert.Equal(a * a * a / (6 * Math.Sqrt(2)), b.Volume(points), Tolerance));
        Assert.Equal(-branches[0].Value("D.z"), branches[1].Value("D.z"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_Solve_CubeVertexIsEdgeOverRootThreeFromPlaneOfNeighbours()
    {
        GeometricSketch sketch = Cube(2);
        sketch.AddPlane("n", "B", "D", "A1");
        sketch.AddPoint("H", 0.5, 0.5, 0.5);
        sketch.PointOnPlane("H", "n");
        sketch.PointOnLine("H", "AC1");

        SketchSolution solution = sketch.Solve();

        // Основание перпендикуляра из A на плоскость BDA1 лежит на диагонали AC1
        Assert.True(solution.Satisfied);
        Assert.Equal(2 / Math.Sqrt(3), solution.DistanceToPlane("A", "n"), Tolerance);
        Assert.Equal(2 / Math.Sqrt(3), solution.Distance("A", "H"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_Solve_CubeDiagonalMakesAngleWithBase()
    {
        GeometricSketch sketch = Cube(1);
        sketch.AddPlane("base", "A", "B", "D");
        sketch.AddPlane("side", "A", "B", "A1");

        SketchSolution solution = sketch.Solve();

        Assert.Equal(Math.Atan(1 / Math.Sqrt(2)), solution.Angle("AC1", "base"), Tolerance);
        Assert.Equal(Math.PI / 2, solution.Angle("base", "side"), Tolerance);
        Assert.Equal(Math.PI / 2, solution.Angle("AA1", "base"), Tolerance);
        Assert.Equal(Math.Acos(1 / Math.Sqrt(3)), solution.AngleAt("B", "A", "C1"), Tolerance);
        Assert.Equal(1, solution.Area("A", "B", "C", "D"), Tolerance);
        Assert.Equal(Math.Sqrt(3) / 2, solution.Area("B", "D", "A1"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_Solve_AngleToPlaneFixesHeight()
    {
        // Точка P над осью x на высоте h: угол прямой OP с плоскостью xy равен 30°, откуда h = tg 30°
        var sketch = new GeometricSketch();
        sketch.AddPoint("O", 0, 0, 0, isFixed: true);
        sketch.AddPoint("E", 1, 0, 0, isFixed: true);
        sketch.AddPoint("F", 0, 1, 0, isFixed: true);
        sketch.AddPoint("P", 1, 0, 0.3);
        sketch.SetGuess("P.x", 1, isFixed: true);
        sketch.SetGuess("P.y", 0, isFixed: true);
        sketch.AddPlane("xy", "O", "E", "F");
        sketch.AddLine("OP", "O", "P");
        sketch.Angle("OP", "xy", Math.PI / 6);

        SketchSolution solution = sketch.Solve();

        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(Math.Tan(Math.PI / 6), solution.Value("P.z"), Tolerance);
    }

    [Fact]
    public void GeometricSketch_SolveAll_DistanceToPlaneGivesPointsOnBothSides()
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("O", 0, 0, 0, isFixed: true);
        sketch.AddPoint("E", 1, 0, 0, isFixed: true);
        sketch.AddPoint("F", 0, 1, 0, isFixed: true);
        sketch.AddPoint("U", 0, 0, 1, isFixed: true);
        sketch.AddPoint("P", 0.2, 0.1, 1);
        sketch.AddPlane("xy", "O", "E", "F");
        sketch.AddLine("z", "O", "U");
        sketch.PointOnLine("P", "z");
        sketch.DistanceToPlane("P", "xy", 3);

        IReadOnlyList<SketchSolution> branches = sketch.SolveAll();

        Assert.Equal([-3.0, 3.0], branches.Select(b => Math.Round(b.Value("P.z"), 6)).Order().ToArray());
    }

    [Fact]
    public void GeometricSketch_Solve_RotationAboutZAxisTurnsXIntoY()
    {
        var sketch = new GeometricSketch();
        sketch.AddPoint("O", 0, 0, 0, isFixed: true);
        sketch.AddPoint("U", 0, 0, 1, isFixed: true);
        sketch.AddPoint("P", 1, 0, 0, isFixed: true);
        sketch.AddPoint("Q", 0.5, 0.5, 0.2);
        sketch.AddLine("z", "O", "U");
        sketch.Rotation("Q", "P", "z", Math.PI / 2);

        SketchSolution solution = sketch.Solve();

        Assert.Equal(SketchStatus.WellConstrained, solution.Status);
        Assert.Equal(0, solution.Value("Q.x"), Tolerance);
        Assert.Equal(1, solution.Value("Q.y"), Tolerance);
        Assert.Equal(0, solution.Value("Q.z"), Tolerance);
    }
}
