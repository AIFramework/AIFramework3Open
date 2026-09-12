using AI.Solvers.Constraints.Sat;
using AI.Solvers.Constraints.Smt;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// SMT проверяется тем, что известно независимо от него: чисто булевы формулы — ответом
/// SAT-решателя, целочисленные — полным перебором, расписание — точным порогом по длительности,
/// а каждая найденная модель — прямым вычислением всех утверждений.
/// </summary>
public class SmtSolverTests
{
    #region Арифметика

    [Fact]
    public void RealConflict_IsProvenUnsatisfiable()
    {
        var model = new SmtModel();
        NumericVariable x = model.Real("x");
        NumericVariable y = model.Real("y");

        model.Assert(x + y <= 3).Assert(x >= 2).Assert(y >= 2);

        SmtSolution solution = SmtSolver.Solve(model);

        Assert.Equal(SmtStatus.Unsatisfiable, solution.Status);
        Assert.Contains(solution.Interpret().Findings, f => f.Contains("ответ, а не отказ", StringComparison.Ordinal));
    }

    [Fact]
    public void Disjunction_TakesTheOnlyFeasibleBranch()
    {
        var model = new SmtModel();
        NumericVariable x = model.Real("x");
        NumericVariable y = model.Real("y", 0);

        // Ветвь x ≥ 5 противоречит x + y ≤ 3 при y ≥ 0 — остаётся y ≥ 2
        model.Assert(x + y <= 3).Assert((x >= 5) | (y >= 2));

        SmtSolution solution = SmtSolver.Solve(model);

        Assert.True(solution.IsSatisfiable);
        Assert.True(solution.Verified);
        Assert.True(solution[y] >= 2 - 1e-9);
        Assert.True(solution[x] + solution[y] <= 3 + 1e-9);
    }

    [Fact]
    public void Integrality_ChangesTheAnswer()
    {
        // 2x = 1 решается в вещественных числах и не решается в целых
        var reals = new SmtModel();
        NumericVariable x = reals.Real("x");
        reals.Assert(SmtFormula.Equal(2 * x, 1));

        var integers = new SmtModel();
        NumericVariable n = integers.Int("n");
        integers.Assert(SmtFormula.Equal(2 * n, 1));

        SmtSolution real = SmtSolver.Solve(reals);

        Assert.True(real.IsSatisfiable);
        Assert.Equal(0.5, real[x], 1e-9);
        Assert.Equal(SmtStatus.Unsatisfiable, SmtSolver.Solve(integers).Status);
    }

    [Fact]
    public void StrictInequalities_AreCheckedExactly()
    {
        // 0 < x < 1: в вещественных числах решение есть, в целых — нет
        var reals = new SmtModel();
        NumericVariable x = reals.Real("x");
        reals.Assert((x > 0) & (x < 1));

        var integers = new SmtModel();
        NumericVariable n = integers.Int("n");
        integers.Assert((n > 0) & (n < 1));

        SmtSolution real = SmtSolver.Solve(reals);

        Assert.True(real.IsSatisfiable && real.Verified);
        Assert.InRange(real[x], 1e-12, 1 - 1e-12);
        Assert.Equal(SmtStatus.Unsatisfiable, SmtSolver.Solve(integers).Status);

        // Нестрогие границы совпадают — точка есть; строгая и нестрогая — точки нет
        var touching = new SmtModel();
        NumericVariable t = touching.Real("t");
        touching.Assert((t <= 0) & (t >= 0));
        Assert.True(SmtSolver.Solve(touching).IsSatisfiable);

        var open = new SmtModel();
        NumericVariable o = open.Real("o");
        open.Assert((o < 0) & (o >= 0));
        Assert.Equal(SmtStatus.Unsatisfiable, SmtSolver.Solve(open).Status);
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void SingleMachineSchedule_HasExactMakespanThreshold(int horizon, bool feasible)
    {
        // Три работы 2, 3 и 4 на одном станке: уложиться можно ровно за 9, но не за 8
        SmtModel model = Schedule([2, 3, 4], horizon, out NumericVariable[] start);
        SmtSolution solution = SmtSolver.Solve(model);

        Assert.Equal(feasible, solution.IsSatisfiable);

        if (!feasible)
        {
            Assert.Equal(SmtStatus.Unsatisfiable, solution.Status);
            Assert.True(solution.TheoryLemmas > 0);
            return;
        }

        Assert.True(solution.Verified);

        int[] durations = [2, 3, 4];

        for (int i = 0; i < 3; i++)
        {
            for (int j = i + 1; j < 3; j++)
            {
                double si = solution[start[i]];
                double sj = solution[start[j]];

                Assert.True(si + durations[i] <= sj + 1e-9 || sj + durations[j] <= si + 1e-9);
            }
        }
    }

    #endregion

    #region Булева часть

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void PureBooleanFormulas_AgreeWithSatSolver(int seed)
    {
        const int Count = 8;
        const int Clauses = 36;
        var random = new Random(seed);

        var cnf = new CnfFormula();
        int[] variables = cnf.AddVariables(Count);

        var model = new SmtModel();
        BooleanVariable[] booleans = Enumerable.Range(0, Count).Select(i => model.Bool($"p{i}")).ToArray();

        for (int c = 0; c < Clauses; c++)
        {
            int[] chosen = Enumerable.Range(0, Count).OrderBy(_ => random.Next()).Take(3).ToArray();
            bool[] positive = chosen.Select(_ => random.Next(2) == 0).ToArray();

            cnf.AddClause(chosen.Select((v, k) => positive[k] ? variables[v] : -variables[v]).ToArray());
            model.Assert(SmtFormula.Or(chosen.Select((v, k) => positive[k] ? (SmtFormula)booleans[v] : !booleans[v]).ToArray()));
        }

        SmtSolution smt = SmtSolver.Solve(model);

        Assert.Equal(SatSolver.Solve(cnf).IsSatisfiable, smt.IsSatisfiable);

        if (smt.IsSatisfiable)
            Assert.True(smt.Verified);
    }

    [Fact]
    public void Iff_LinksBooleanToArithmetic()
    {
        var model = new SmtModel();
        NumericVariable x = model.Int("x", 0, 10);
        BooleanVariable large = model.Bool("large");

        model.Assert(SmtFormula.Iff(large, x >= 5)).Assert(x <= 3);

        SmtSolution solution = SmtSolver.Solve(model);

        Assert.True(solution.IsSatisfiable);
        Assert.False(solution[large]);

        model.Assert(large);
        Assert.Equal(SmtStatus.Unsatisfiable, SmtSolver.Solve(model).Status);
    }

    #endregion

    #region Сверка с перебором

    [Fact]
    public void RandomIntegerFormulas_AgreeWithBruteForce()
    {
        int satisfiable = 0;
        int unsatisfiable = 0;

        for (int seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            var model = new SmtModel();
            NumericVariable x = model.Int("x", 0, 4);
            NumericVariable y = model.Int("y", 0, 4);

            // Четыре дизъюнкта по два атома a·x + b·y ≤ c или < c
            var clauses = new List<(int A, int B, int C, bool Strict)[]>();

            for (int c = 0; c < 4; c++)
            {
                var clause = new (int A, int B, int C, bool Strict)[2];

                for (int k = 0; k < 2; k++)
                    clause[k] = (random.Next(-3, 4), random.Next(-3, 4), random.Next(-4, 9), random.Next(2) == 0);

                clauses.Add(clause);
                model.Assert(SmtFormula.Or(clause.Select(a => a.Strict ? a.A * x + a.B * y < a.C : a.A * x + a.B * y <= a.C).ToArray()));
            }

            bool Holds(int vx, int vy) => clauses.All(clause => clause.Any(a =>
                a.Strict ? (a.A * vx) + (a.B * vy) < a.C : (a.A * vx) + (a.B * vy) <= a.C));

            bool expected = Enumerable.Range(0, 5).Any(vx => Enumerable.Range(0, 5).Any(vy => Holds(vx, vy)));
            SmtSolution solution = SmtSolver.Solve(model);

            Assert.True(expected == solution.IsSatisfiable, $"Зерно {seed}: перебор {expected}, SMT {solution.Status}");

            if (solution.IsSatisfiable)
            {
                Assert.True(solution.Verified, $"Зерно {seed}: модель не прошла проверку");
                Assert.True(Holds((int)solution[x], (int)solution[y]), $"Зерно {seed}: модель не выполняет формулу");
                satisfiable++;
            }
            else
            {
                unsatisfiable++;
            }
        }

        // Выборка должна содержать оба исхода, иначе сверка ничего не проверяет
        Assert.True(satisfiable >= 5 && unsatisfiable >= 5, $"выполнимых {satisfiable}, невыполнимых {unsatisfiable}");
    }

    #endregion

    #region Пределы и ошибки

    [Fact]
    public void LemmaLimit_ReportsUnknownHonestly()
    {
        SmtModel model = Schedule([2, 3, 4], 8, out _);
        SmtSolution solution = SmtSolver.Solve(model, new SmtOptions { MaxLemmas = 1 });

        Assert.Equal(SmtStatus.Unknown, solution.Status);
        Assert.Contains(solution.Interpret().Warnings, w => w.Contains("не означает", StringComparison.Ordinal));
    }

    [Fact]
    public void Formula_FromAnotherModel_IsRejected()
    {
        var first = new SmtModel();
        NumericVariable x = first.Real("x");
        var second = new SmtModel();

        _ = Assert.Throws<ArgumentException>(() => second.Assert(x <= 1));
        _ = Assert.Throws<ArgumentException>(() => first.Real("x"));
        Assert.Equal("x + 2·y ≤ 10", (x + (2 * first.Real("y")) <= 10).ToString());
    }

    #endregion

    private static SmtModel Schedule(int[] durations, int horizon, out NumericVariable[] start)
    {
        var model = new SmtModel("Один станок");
        start = durations.Select((_, i) => model.Int($"s{i}", 0, horizon)).ToArray();

        for (int i = 0; i < durations.Length; i++)
        {
            model.Assert(start[i] + durations[i] <= horizon);

            for (int j = i + 1; j < durations.Length; j++)
                model.Assert((start[i] + durations[i] <= start[j]) | (start[j] + durations[j] <= start[i]));
        }

        return model;
    }
}
