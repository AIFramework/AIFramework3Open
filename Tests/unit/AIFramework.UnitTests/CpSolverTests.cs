using AI.Solvers.Constraints.Cp;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Решатель задач с ограничениями: задачи с известным числом решений,
/// проверка распространения и заведомо неразрешимые постановки.
/// </summary>
public class CpSolverTests
{
    private static CpModel Queens(int size)
    {
        var model = new CpModel($"Ферзи {size}×{size}");
        IntVariable[] queens = model.AddVariables("q", size, 0, size - 1);

        _ = model.Add(new AllDifferent(queens));

        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
            {
                // Диагонали: q[i] - q[j] ≠ ±(j - i)
                _ = model.Add(new NotEqual(queens[i], queens[j], j - i));
                _ = model.Add(new NotEqual(queens[i], queens[j], i - j));
            }
        }

        return model;
    }

    #region Задачи с известным ответом

    [Theory]
    [InlineData(4, 2)]
    [InlineData(5, 10)]
    [InlineData(6, 4)]
    [InlineData(7, 40)]
    [InlineData(8, 92)]
    public void Cp_NQueens_FindsKnownNumberOfSolutions(int size, int expected)
    {
        CpSolution solution = CpSolver.SolveAll(Queens(size));

        Assert.Equal(CpStatus.Satisfiable, solution.Status);
        Assert.Equal(expected, solution.Count);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Cp_NQueens_SmallBoardsHaveNoSolution(int size)
    {
        Assert.Equal(CpStatus.Infeasible, CpSolver.SolveAll(Queens(size)).Status);
    }

    [Fact]
    public void Cp_NQueens_SolutionIsValid()
    {
        CpSolution solution = CpSolver.Solve(Queens(8));

        Assert.True(solution.IsSatisfiable);

        var rows = new int[8];
        for (int i = 0; i < 8; i++)
            rows[i] = solution[$"q{i}"];

        for (int i = 0; i < 8; i++)
        {
            for (int j = i + 1; j < 8; j++)
            {
                Assert.NotEqual(rows[i], rows[j]);
                Assert.NotEqual(Math.Abs(rows[i] - rows[j]), j - i);
            }
        }
    }

    [Fact]
    public void Cp_Sudoku_SolvesKnownPuzzle()
    {
        int[,] puzzle =
        {
            { 5, 3, 0, 0, 7, 0, 0, 0, 0 },
            { 6, 0, 0, 1, 9, 5, 0, 0, 0 },
            { 0, 9, 8, 0, 0, 0, 0, 6, 0 },
            { 8, 0, 0, 0, 6, 0, 0, 0, 3 },
            { 4, 0, 0, 8, 0, 3, 0, 0, 1 },
            { 7, 0, 0, 0, 2, 0, 0, 0, 6 },
            { 0, 6, 0, 0, 0, 0, 2, 8, 0 },
            { 0, 0, 0, 4, 1, 9, 0, 0, 5 },
            { 0, 0, 0, 0, 8, 0, 0, 7, 9 },
        };

        var model = new CpModel("Судоку");
        var cells = new IntVariable[9, 9];

        for (int row = 0; row < 9; row++)
            for (int column = 0; column < 9; column++)
                cells[row, column] = model.AddVariable($"c{row}{column}", 1, 9);

        for (int row = 0; row < 9; row++)
            for (int column = 0; column < 9; column++)
                if (puzzle[row, column] != 0)
                    _ = model.Fix(cells[row, column], puzzle[row, column]);

        for (int row = 0; row < 9; row++)
            _ = model.Add(new AllDifferent(Enumerable.Range(0, 9).Select(c => cells[row, c]).ToArray()));

        for (int column = 0; column < 9; column++)
            _ = model.Add(new AllDifferent(Enumerable.Range(0, 9).Select(r => cells[r, column]).ToArray()));

        for (int boxRow = 0; boxRow < 3; boxRow++)
        {
            for (int boxColumn = 0; boxColumn < 3; boxColumn++)
            {
                var box = new List<IntVariable>();

                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                        box.Add(cells[(boxRow * 3) + row, (boxColumn * 3) + column]);

                _ = model.Add(new AllDifferent(box.ToArray()));
            }
        }

        CpSolution solution = CpSolver.Solve(model);

        Assert.True(solution.IsSatisfiable);

        // Известное решение этой головоломки начинается с 5 3 4 6 7 8 9 1 2
        int[] expectedFirstRow = [5, 3, 4, 6, 7, 8, 9, 1, 2];

        for (int column = 0; column < 9; column++)
            Assert.Equal(expectedFirstRow[column], solution[cells[0, column]]);
    }

    #endregion

    #region Распространение

    [Fact]
    public void Cp_LinearConstraint_NarrowsDomains()
    {
        var model = new CpModel();
        IntVariable x = model.AddVariable("x", 0, 100);
        IntVariable y = model.AddVariable("y", 0, 100);

        // x + y = 10 при x >= 7 оставляет y не больше трёх
        _ = model.AddLinear([x, y], [1, 1], LinearRelation.Equal, 10);
        _ = model.AddLinear([x], [1], LinearRelation.GreaterOrEqual, 7);

        CpSolution solution = CpSolver.SolveAll(model);

        Assert.Equal(CpStatus.Satisfiable, solution.Status);
        Assert.Equal(4, solution.Count);   // x = 7..10

        foreach (IReadOnlyList<int> assignment in solution.Solutions)
        {
            Assert.Equal(10, assignment[x.Index] + assignment[y.Index]);
            Assert.True(assignment[x.Index] >= 7);
        }
    }

    [Fact]
    public void Cp_AllDifferent_DetectsPigeonhole()
    {
        // Четыре переменные на трёх значениях: условие Холла нарушено
        var model = new CpModel();
        IntVariable[] variables = model.AddVariables("v", 4, 1, 3);

        _ = model.Add(new AllDifferent(variables));

        Assert.Equal(CpStatus.Infeasible, CpSolver.Solve(model).Status);
    }

    [Fact]
    public void Cp_AllDifferent_RemovesFixedValues()
    {
        var model = new CpModel();
        IntVariable a = model.AddVariable("a", 1, 3);
        IntVariable b = model.AddVariable("b", 1, 3);
        IntVariable c = model.AddVariable("c", 1, 3);

        _ = model.Add(new AllDifferent(a, b, c));
        _ = model.Fix(a, 1);
        _ = model.Fix(b, 2);

        CpSolution solution = CpSolver.Solve(model);

        Assert.True(solution.IsSatisfiable);
        Assert.Equal(3, solution[c]);
    }

    [Fact]
    public void Cp_NegativeDomain_IsHandled()
    {
        var model = new CpModel();
        IntVariable t = model.AddVariable("t", -10, 10);

        _ = model.AddLinear([t], [2], LinearRelation.Equal, -6);

        CpSolution solution = CpSolver.Solve(model);

        Assert.True(solution.IsSatisfiable);
        Assert.Equal(-3, solution[t]);
    }

    [Fact]
    public void Cp_ContradictoryConstraints_AreInfeasible()
    {
        var model = new CpModel();
        IntVariable x = model.AddVariable("x", 0, 10);

        _ = model.AddLinear([x], [1], LinearRelation.GreaterOrEqual, 8);
        _ = model.AddLinear([x], [1], LinearRelation.LessOrEqual, 5);

        Assert.Equal(CpStatus.Infeasible, CpSolver.Solve(model).Status);
    }

    [Fact]
    public void Cp_SolutionLimit_StopsEarly()
    {
        CpSolution solution = CpSolver.Solve(Queens(8), new CpOptions { SolutionLimit = 5 });

        Assert.Equal(5, solution.Count);
        Assert.True(solution.IsSatisfiable);
    }

    [Fact]
    public void Cp_NodeLimit_ReportsUnfinishedSearch()
    {
        CpSolution solution = CpSolver.Solve(Queens(8), new CpOptions { SolutionLimit = int.MaxValue, MaxNodes = 3 });

        Assert.Equal(CpStatus.LimitReached, solution.Status);
        Assert.True(solution.Nodes <= 3);
    }

    #endregion

    #region Прикладная задача

    [Fact]
    public void Cp_MapColouring_NeedsThreeColours()
    {
        // Цикл из пяти областей: три цвета хватает, двух нет
        Assert.True(Colourable(colours: 3));
        Assert.False(Colourable(colours: 2));
    }

    private static bool Colourable(int colours)
    {
        var model = new CpModel("Раскраска карты");
        IntVariable[] regions = model.AddVariables("r", 5, 1, colours);

        for (int i = 0; i < 5; i++)
            _ = model.Add(new NotEqual(regions[i], regions[(i + 1) % 5]));

        return CpSolver.Solve(model).IsSatisfiable;
    }

    #endregion
}
