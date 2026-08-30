using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Solvers.Optimization;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Линейное и смешанно-целочисленное программирование: задачи с известным ответом,
/// вырожденные и неразрешимые случаи, сверка с транспортной задачей из AI.Algorithms.
/// </summary>
public class LinearProgrammingTests
{
    private const double Tolerance = 1e-6;

    #region Непрерывные задачи

    [Fact]
    public void Lp_ProductMix_MatchesKnownOptimum()
    {
        // max 3x + 5y при x <= 4, 2y <= 12, 3x + 2y <= 18 — классическая задача Хиллиера
        var program = new LinearProgram(ObjectiveSense.Maximize, "Производственная программа");
        Variable x = program.AddVariable("x");
        Variable y = program.AddVariable("y");

        program.SetObjective(new Vector(3.0, 5.0));
        _ = program.AddConstraint(new Vector(1.0, 0.0), ConstraintSign.LessOrEqual, 4, "цех 1");
        _ = program.AddConstraint(new Vector(0.0, 2.0), ConstraintSign.LessOrEqual, 12, "цех 2");
        _ = program.AddConstraint(new Vector(3.0, 2.0), ConstraintSign.LessOrEqual, 18, "сборка");

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(36.0, solution.Objective, tolerance: Tolerance);
        Assert.Equal(2.0, solution[x], tolerance: Tolerance);
        Assert.Equal(6.0, solution[y], tolerance: Tolerance);
    }

    [Fact]
    public void Lp_Minimization_WithGreaterOrEqualConstraints()
    {
        // min 0.6x + y при x + y >= 800, 0.21x - 0.3y <= 0, 0.03x - 0.01y >= 0
        var program = new LinearProgram(ObjectiveSense.Minimize);
        _ = program.AddVariable("x");
        _ = program.AddVariable("y");

        program.SetObjective(new Vector(0.6, 1.0));
        _ = program.AddConstraint(new Vector(1.0, 1.0), ConstraintSign.GreaterOrEqual, 800);
        _ = program.AddConstraint(new Vector(0.21, -0.30), ConstraintSign.LessOrEqual, 0);
        _ = program.AddConstraint(new Vector(0.03, -0.01), ConstraintSign.GreaterOrEqual, 0);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);

        // Связаны первое и второе ограничения: y = 800 - x и y = 0.7x дают x = 8000/17
        Assert.Equal(611.764706, solution.Objective, tolerance: 1e-5);
        Assert.Equal(470.588235, solution["x"], tolerance: 1e-5);
        Assert.Equal(329.411765, solution["y"], tolerance: 1e-5);
    }

    [Fact]
    public void Lp_EqualityConstraints_AreRespected()
    {
        var program = new LinearProgram(ObjectiveSense.Minimize);
        _ = program.AddVariable("a");
        _ = program.AddVariable("b");

        program.SetObjective(new Vector(1.0, 1.0));
        _ = program.AddConstraint(new Vector(1.0, 1.0), ConstraintSign.Equal, 10);
        _ = program.AddConstraint(new Vector(1.0, -1.0), ConstraintSign.Equal, 2);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(6.0, solution["a"], tolerance: Tolerance);
        Assert.Equal(4.0, solution["b"], tolerance: Tolerance);
    }

    [Fact]
    public void Lp_VariableBounds_AreEnforced()
    {
        var program = new LinearProgram(ObjectiveSense.Maximize);
        _ = program.AddVariable("x", lowerBound: 2, upperBound: 5);
        _ = program.AddVariable("y", lowerBound: 0, upperBound: 3);

        program.SetObjective(new Vector(1.0, 1.0));
        _ = program.AddConstraint(new Vector(1.0, 1.0), ConstraintSign.LessOrEqual, 100);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(5.0, solution["x"], tolerance: Tolerance);
        Assert.Equal(3.0, solution["y"], tolerance: Tolerance);
        Assert.Equal(8.0, solution.Objective, tolerance: Tolerance);
    }

    [Fact]
    public void Lp_FreeVariable_MayGoNegative()
    {
        var program = new LinearProgram(ObjectiveSense.Minimize);
        _ = program.AddFreeVariable("t");

        program.SetObjective(new Vector(1.0));
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.GreaterOrEqual, -7);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(-7.0, solution["t"], tolerance: Tolerance);
    }

    [Fact]
    public void Lp_NegativeLowerBound_IsShiftedCorrectly()
    {
        var program = new LinearProgram(ObjectiveSense.Minimize);
        _ = program.AddVariable("x", lowerBound: -10, upperBound: 10);

        program.SetObjective(new Vector(2.0));
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.GreaterOrEqual, -4);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(-4.0, solution["x"], tolerance: Tolerance);
        Assert.Equal(-8.0, solution.Objective, tolerance: Tolerance);
    }

    [Fact]
    public void Lp_Infeasible_IsReported()
    {
        var program = new LinearProgram(ObjectiveSense.Minimize);
        _ = program.AddVariable("x");

        program.SetObjective(new Vector(1.0));
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.GreaterOrEqual, 10);
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.LessOrEqual, 5);

        Assert.Equal(SolverStatus.Infeasible, LpSolver.Solve(program).Status);
    }

    [Fact]
    public void Lp_Unbounded_IsReported()
    {
        var program = new LinearProgram(ObjectiveSense.Maximize);
        _ = program.AddVariable("x");

        program.SetObjective(new Vector(1.0));
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.GreaterOrEqual, 1);

        Assert.Equal(SolverStatus.Unbounded, LpSolver.Solve(program).Status);
    }

    [Fact]
    public void Lp_DegenerateProblem_TerminatesWithOptimum()
    {
        // Вырожденная задача: три ограничения пересекаются в одной точке
        var program = new LinearProgram(ObjectiveSense.Maximize);
        _ = program.AddVariable("x");
        _ = program.AddVariable("y");

        program.SetObjective(new Vector(1.0, 1.0));
        _ = program.AddConstraint(new Vector(1.0, 1.0), ConstraintSign.LessOrEqual, 4);
        _ = program.AddConstraint(new Vector(1.0, 0.0), ConstraintSign.LessOrEqual, 2);
        _ = program.AddConstraint(new Vector(0.0, 1.0), ConstraintSign.LessOrEqual, 2);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(4.0, solution.Objective, tolerance: Tolerance);
    }

    [Fact]
    public void Lp_FromMatrix_BuildsEquivalentProblem()
    {
        var objective = new Vector(3.0, 5.0);
        var constraints = new Matrix(3, 2);
        constraints[0, 0] = 1; constraints[0, 1] = 0;
        constraints[1, 0] = 0; constraints[1, 1] = 2;
        constraints[2, 0] = 3; constraints[2, 1] = 2;

        LinearProgram program = LpSolver.FromMatrix(
            objective, constraints,
            [ConstraintSign.LessOrEqual, ConstraintSign.LessOrEqual, ConstraintSign.LessOrEqual],
            new Vector(4.0, 12.0, 18.0),
            ObjectiveSense.Maximize);

        Assert.Equal(36.0, LpSolver.Solve(program).Objective, tolerance: Tolerance);
    }

    #endregion

    #region Целочисленные задачи

    [Fact]
    public void Milp_Knapsack_FindsBestCombination()
    {
        // Рюкзак: веса 12, 7, 11, 8, 9 при пределе 26; ценности 24, 13, 23, 15, 16
        double[] weights = [12, 7, 11, 8, 9];
        double[] values = [24, 13, 23, 15, 16];

        var program = new LinearProgram(ObjectiveSense.Maximize, "Рюкзак");

        foreach (int i in Enumerable.Range(0, weights.Length))
            _ = program.AddBinaryVariable($"take{i + 1}");

        program.SetObjective(new Vector(values));
        _ = program.AddConstraint(new Vector(weights), ConstraintSign.LessOrEqual, 26, "вместимость");

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(51.0, solution.Objective, tolerance: Tolerance);

        double weight = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            double take = solution[$"take{i + 1}"];
            Assert.True(Math.Abs(take) < Tolerance || Math.Abs(take - 1) < Tolerance, "Булева переменная должна быть 0 или 1");
            weight += take * weights[i];
        }

        Assert.True(weight <= 26 + Tolerance);
    }

    [Fact]
    public void Milp_IntegerSolution_DiffersFromRelaxation()
    {
        // max x + y при 2x + 2y <= 5: непрерывный оптимум 2.5, целочисленный 2
        var program = new LinearProgram(ObjectiveSense.Maximize);
        _ = program.AddIntegerVariable("x");
        _ = program.AddIntegerVariable("y");

        program.SetObjective(new Vector(1.0, 1.0));
        _ = program.AddConstraint(new Vector(2.0, 2.0), ConstraintSign.LessOrEqual, 5);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(2.0, solution.Objective, tolerance: Tolerance);
        Assert.True(solution.Nodes > 0, "Целочисленная задача должна пройти через ветвление");
    }

    [Fact]
    public void Milp_MixedProblem_KeepsContinuousVariableFractional()
    {
        var program = new LinearProgram(ObjectiveSense.Maximize);
        Variable count = program.AddIntegerVariable("count", 0, 10);
        Variable share = program.AddVariable("share", 0, 1);

        program.SetObjective(new Vector(1.0, 1.0));
        _ = program.AddConstraint(new Vector(1.0, 1.0), ConstraintSign.LessOrEqual, 4.5);

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(4.5, solution.Objective, tolerance: Tolerance);
        Assert.Equal(Math.Round(solution[count]), solution[count], tolerance: 1e-7);
        Assert.Equal(0.5, solution[share], tolerance: Tolerance);
    }

    [Fact]
    public void Milp_Assignment_ProducesPermutation()
    {
        // Задача о назначениях 3×3: у оптимума ровно одна единица в каждой строке и столбце
        double[,] cost =
        {
            { 4, 2, 8 },
            { 4, 3, 7 },
            { 3, 1, 6 },
        };

        var program = new LinearProgram(ObjectiveSense.Minimize, "Назначения");

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                _ = program.AddBinaryVariable($"x{i}{j}");

        var objective = new Vector(9);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                objective[(i * 3) + j] = cost[i, j];

        program.SetObjective(objective);

        for (int i = 0; i < 3; i++)
        {
            var row = new Vector(9);
            for (int j = 0; j < 3; j++) row[(i * 3) + j] = 1;
            _ = program.AddConstraint(row, ConstraintSign.Equal, 1, $"исполнитель {i}");
        }

        for (int j = 0; j < 3; j++)
        {
            var column = new Vector(9);
            for (int i = 0; i < 3; i++) column[(i * 3) + j] = 1;
            _ = program.AddConstraint(column, ConstraintSign.Equal, 1, $"работа {j}");
        }

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);
        Assert.Equal(12.0, solution.Objective, tolerance: Tolerance);

        for (int i = 0; i < 3; i++)
        {
            double rowSum = 0;
            for (int j = 0; j < 3; j++) rowSum += solution[$"x{i}{j}"];
            Assert.Equal(1.0, rowSum, tolerance: Tolerance);
        }
    }

    [Fact]
    public void Milp_NodeLimit_ReportsUnprovenSolution()
    {
        var program = new LinearProgram(ObjectiveSense.Maximize);

        for (int i = 0; i < 12; i++)
            _ = program.AddBinaryVariable($"b{i}");

        var weights = new Vector(12);
        var values = new Vector(12);

        for (int i = 0; i < 12; i++)
        {
            weights[i] = 7 + (i * 3);
            values[i] = 11 + (i * 5);
        }

        program.SetObjective(values);
        _ = program.AddConstraint(weights, ConstraintSign.LessOrEqual, 60);

        LpSolution solution = LpSolver.Solve(program, new LpOptions { MaxNodes = 3 });

        Assert.Equal(SolverStatus.LimitReached, solution.Status);
        Assert.True(solution.Nodes <= 3);
    }

    #endregion

    #region Сверка со специализированным решателем

    [Fact]
    public void Lp_TransportProblem_MatchesSpecialisedSolver()
    {
        // Транспортная задача решается в AI.Algorithms методом потенциалов;
        // общий решатель обязан давать ту же стоимость.
        double[] supply = [30, 40, 20];
        double[] demand = [20, 30, 40];
        double[,] cost =
        {
            { 2, 3, 4 },
            { 3, 2, 1 },
            { 4, 3, 2 },
        };

        var program = new LinearProgram(ObjectiveSense.Minimize, "Транспортная задача");

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                _ = program.AddVariable($"x{i}{j}");

        var objective = new Vector(9);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                objective[(i * 3) + j] = cost[i, j];

        program.SetObjective(objective);

        for (int i = 0; i < 3; i++)
        {
            var row = new Vector(9);
            for (int j = 0; j < 3; j++) row[(i * 3) + j] = 1;
            _ = program.AddConstraint(row, ConstraintSign.Equal, supply[i], $"поставщик {i}");
        }

        for (int j = 0; j < 3; j++)
        {
            var column = new Vector(9);
            for (int i = 0; i < 3; i++) column[(i * 3) + j] = 1;
            _ = program.AddConstraint(column, ConstraintSign.Equal, demand[j], $"потребитель {j}");
        }

        LpSolution solution = LpSolver.Solve(program);

        Assert.Equal(SolverStatus.Optimal, solution.Status);

        // Оптимум задачи: 170
        Assert.Equal(170.0, solution.Objective, tolerance: Tolerance);

        for (int i = 0; i < 3; i++)
        {
            double shipped = 0;
            for (int j = 0; j < 3; j++) shipped += solution[$"x{i}{j}"];
            Assert.Equal(supply[i], shipped, tolerance: Tolerance);
        }
    }

    #endregion

    #region Объяснимость

    [Fact]
    public void Interpret_Optimal_ExplainsResult()
    {
        var program = new LinearProgram(ObjectiveSense.Maximize, "Производственная программа");
        _ = program.AddVariable("x");
        _ = program.AddVariable("y");

        program.SetObjective(new Vector(3.0, 5.0));
        _ = program.AddConstraint(new Vector(3.0, 2.0), ConstraintSign.LessOrEqual, 18);

        Interpretation interpretation = LpSolver.Solve(program).Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Исход" && m.Value == "оптимум");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Целевая функция");
        Assert.Contains(interpretation.Recommendations, r => r.Contains("чувствительность", StringComparison.Ordinal));
    }

    [Fact]
    public void Interpret_Unbounded_WarnsAboutModelError()
    {
        var program = new LinearProgram(ObjectiveSense.Maximize);
        _ = program.AddVariable("x");

        program.SetObjective(new Vector(1.0));
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.GreaterOrEqual, 1);

        Interpretation interpretation = LpSolver.Solve(program).Interpret();

        Assert.Contains(interpretation.Warnings, w => w.Contains("ошибку модели", StringComparison.Ordinal));
    }

    [Fact]
    public void Interpret_Infeasible_SuggestsRelaxingConstraints()
    {
        var program = new LinearProgram(ObjectiveSense.Minimize);
        _ = program.AddVariable("x");

        program.SetObjective(new Vector(1.0));
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.GreaterOrEqual, 10);
        _ = program.AddConstraint(new Vector(1.0), ConstraintSign.LessOrEqual, 5);

        Interpretation interpretation = LpSolver.Solve(program).Interpret();

        Assert.Contains(interpretation.Warnings, w => w.Contains("ослабить ограничения", StringComparison.Ordinal));
    }

    #endregion
}
