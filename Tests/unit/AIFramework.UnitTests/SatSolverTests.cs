using AI.Algorithms.GraphStructure;
using AI.Solvers.Constraints.Sat;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Решатель выполнимости: подстановки проверяются независимо, невыполнимость — на задачах,
/// где она доказана математически, а на двухлитеральных задачах ответ сверяется
/// со специализированным решателем 2-SAT из AI.Algorithms.
/// </summary>
public class SatSolverTests
{
    #region Основы

    [Fact]
    public void Sat_EmptyFormula_IsSatisfiable()
    {
        Assert.Equal(SatStatus.Satisfiable, SatSolver.Solve(new CnfFormula()).Status);
    }

    [Fact]
    public void Sat_ContradictoryUnitClauses_AreUnsatisfiable()
    {
        var formula = new CnfFormula();
        _ = formula.AddClause(1).AddClause(-1);

        Assert.Equal(SatStatus.Unsatisfiable, SatSolver.Solve(formula).Status);
    }

    [Fact]
    public void Sat_UnitPropagation_FixesImpliedVariables()
    {
        // x1, x1 -> x2, x2 -> x3
        var formula = new CnfFormula();
        _ = formula.Assert(1).Implies(1, 2).Implies(2, 3);

        SatSolution solution = SatSolver.Solve(formula);

        Assert.True(solution.IsSatisfiable);
        Assert.True(solution[1]);
        Assert.True(solution[2]);
        Assert.True(solution[3]);
    }

    [Fact]
    public void Sat_ExactlyOne_AllowsSingleTrueVariable()
    {
        var formula = new CnfFormula();
        int[] variables = formula.AddVariables(5);
        _ = formula.ExactlyOne(variables);

        SatSolution solution = SatSolver.Solve(formula);

        Assert.True(solution.IsSatisfiable);
        Assert.True(solution.Verify(formula));
        Assert.Equal(1, variables.Count(v => solution[v]));
    }

    [Fact]
    public void Sat_Model_IsVerifiedAgainstFormula()
    {
        var formula = new CnfFormula();
        _ = formula.AddClause(1, 2, 3)
                   .AddClause(-1, -2)
                   .AddClause(-2, -3)
                   .AddClause(-1, -3)
                   .AddClause(1, -2, 3);

        SatSolution solution = SatSolver.Solve(formula);

        Assert.True(solution.IsSatisfiable);
        Assert.True(solution.Verify(formula), "Подстановка обязана выполнять каждый дизъюнкт");
    }

    #endregion

    #region Невыполнимость

    [Fact]
    public void Sat_AllCombinationsForbidden_IsUnsatisfiable()
    {
        // Все восемь наборов из трёх переменных запрещены явными дизъюнктами
        var formula = new CnfFormula();
        _ = formula.AddVariables(3);

        for (int mask = 0; mask < 8; mask++)
        {
            var clause = new int[3];

            for (int bit = 0; bit < 3; bit++)
                clause[bit] = (mask & (1 << bit)) != 0 ? -(bit + 1) : bit + 1;

            _ = formula.AddClause(clause);
        }

        Assert.Equal(SatStatus.Unsatisfiable, SatSolver.Solve(formula).Status);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Sat_PigeonholePrinciple_IsUnsatisfiable(int holes)
    {
        // holes + 1 голубей в holes ящиков: классическая невыполнимая задача
        var formula = new CnfFormula();
        int pigeons = holes + 1;

        int Variable(int pigeon, int hole) => (pigeon * holes) + hole + 1;

        _ = formula.AddVariables(pigeons * holes);

        for (int pigeon = 0; pigeon < pigeons; pigeon++)
        {
            var places = new int[holes];

            for (int hole = 0; hole < holes; hole++)
                places[hole] = Variable(pigeon, hole);

            _ = formula.AtLeastOne(places);
        }

        for (int hole = 0; hole < holes; hole++)
            for (int first = 0; first < pigeons; first++)
                for (int second = first + 1; second < pigeons; second++)
                    _ = formula.AddClause(-Variable(first, hole), -Variable(second, hole));

        Assert.Equal(SatStatus.Unsatisfiable, SatSolver.Solve(formula).Status);
    }

    [Fact]
    public void Sat_ConflictLimit_ReportsUnknown()
    {
        var formula = new CnfFormula();
        int holes = 7;
        int pigeons = holes + 1;

        int Variable(int pigeon, int hole) => (pigeon * holes) + hole + 1;

        _ = formula.AddVariables(pigeons * holes);

        for (int pigeon = 0; pigeon < pigeons; pigeon++)
            _ = formula.AtLeastOne(Enumerable.Range(0, holes).Select(h => Variable(pigeon, h)).ToArray());

        for (int hole = 0; hole < holes; hole++)
            for (int first = 0; first < pigeons; first++)
                for (int second = first + 1; second < pigeons; second++)
                    _ = formula.AddClause(-Variable(first, hole), -Variable(second, hole));

        SatSolution solution = SatSolver.Solve(formula, new SatOptions { MaxConflicts = 5 });

        Assert.Equal(SatStatus.Unknown, solution.Status);
    }

    #endregion

    #region Сверка со специализированным решателем

    [Theory]
    [InlineData(11)]
    [InlineData(23)]
    [InlineData(47)]
    public void Sat_TwoLiteralFormulas_AgreeWithSpecialisedTwoSat(int seed)
    {
        // 2-SAT решается за линейное время на графе импликаций; общий решатель
        // обязан давать тот же ответ о выполнимости
        var random = new Random(seed);
        const int Variables = 12;
        int clauses = 24;

        var formula = new CnfFormula();
        _ = formula.AddVariables(Variables);

        var twoSat = new TwoSAT(Variables);

        for (int i = 0; i < clauses; i++)
        {
            int first = Literal(random, Variables);
            int second = Literal(random, Variables);

            _ = formula.AddClause(first, second);
            twoSat.AddClause(first, second);
        }

        bool specialised = twoSat.Solve();
        SatSolution general = SatSolver.Solve(formula);

        Assert.Equal(specialised, general.IsSatisfiable);

        if (general.IsSatisfiable)
            Assert.True(general.Verify(formula));
    }

    private static int Literal(Random random, int variables)
    {
        int variable = random.Next(1, variables + 1);
        return random.Next(2) == 0 ? variable : -variable;
    }

    #endregion

    #region Прикладная задача

    [Fact]
    public void Sat_GraphColouring_FindsProperColouring()
    {
        // Цикл из пяти вершин: три цвета достаточно, два — нет
        int[][] edges = [[0, 1], [1, 2], [2, 3], [3, 4], [4, 0]];

        Assert.True(TryColour(edges, vertices: 5, colours: 3));
        Assert.False(TryColour(edges, vertices: 5, colours: 2));
    }

    private static bool TryColour(int[][] edges, int vertices, int colours)
    {
        var formula = new CnfFormula();
        _ = formula.AddVariables(vertices * colours);

        int Variable(int vertex, int colour) => (vertex * colours) + colour + 1;

        for (int vertex = 0; vertex < vertices; vertex++)
            _ = formula.ExactlyOne(Enumerable.Range(0, colours).Select(c => Variable(vertex, c)).ToArray());

        foreach (int[] edge in edges)
            for (int colour = 0; colour < colours; colour++)
                _ = formula.AddClause(-Variable(edge[0], colour), -Variable(edge[1], colour));

        SatSolution solution = SatSolver.Solve(formula);

        if (solution.IsSatisfiable)
            Assert.True(solution.Verify(formula));

        return solution.IsSatisfiable;
    }

    [Fact]
    public void Sat_Dimacs_RoundTripsThroughText()
    {
        var formula = new CnfFormula();
        _ = formula.AddClause(1, -2).AddClause(2, 3);

        string dimacs = formula.ToDimacs();

        Assert.Contains("p cnf 3 2", dimacs, StringComparison.Ordinal);
        Assert.Contains("1 -2 0", dimacs, StringComparison.Ordinal);
    }

    #endregion
}
