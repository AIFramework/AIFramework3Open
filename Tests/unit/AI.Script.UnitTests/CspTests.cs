using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространство <c>csp</c> над AI.Solvers.Constraints.
/// </summary>
/// <remarks>
/// Головоломки с единственным известным ответом: если ответ сошёлся, значит, колонки таблицы
/// попали к своим переменным, знаки не перевернулись, а литералы с «!» поняты как отрицания.
/// </remarks>
public sealed class CspTests
{
    private static RunResult Run(string source) => Script.RunOk(source);

    private static double Number(RunResult result, string name) => (double)result.Emitted[name]!;

    /// <summary>SEND + MORE = MONEY: единственное решение 9567 + 1085 = 10652.</summary>
    [Fact]
    public void Solve_SendMoreMoney_HasTheKnownUniqueSolution()
    {
        RunResult result = Run("""
            let rules = table.of({
                name: ["sum", "s_lead", "m_lead"],
                s: <1000, 1, 0>,
                e: <91, 0, 0>,
                n: <-90, 0, 0>,
                d: <1, 0, 0>,
                m: <-9000, 0, 1>,
                o: <-900, 0, 0>,
                r: <10, 0, 0>,
                y: <-1, 0, 0>,
                sign: ["=", ">=", ">="],
                rhs: <0, 1, 1>
            })

            let letters = ["s", "e", "n", "d", "m", "o", "r", "y"]
            let found = csp.solve(letters, lower: 0, upper: 9, constraints: rules, all_different: [letters], limit: 0)

            emit count = found.count
            emit send = found.values.s * 1000 + found.values.e * 100 + found.values.n * 10 + found.values.d
            emit money = found.values.m * 10000 + found.values.o * 1000 + found.values.n * 100 + found.values.e * 10 + found.values.y
            """);

        Assert.Equal(1.0, result.Emitted["count"]);
        Assert.Equal(9567.0, result.Emitted["send"]);
        Assert.Equal(10652.0, result.Emitted["money"]);
    }

    [Fact]
    public void Solve_LimitZero_EnumeratesAllSolutions()
    {
        RunResult result = Run("""
            let rule = table.of({ x: <1>, y: <1>, sign: ["="], rhs: <5> })
            let all = csp.solve(["x", "y"], lower: 0, upper: 5, constraints: rule, limit: 0)

            emit count = all.count
            emit rows = len(all.solutions)
            """);

        Assert.Equal(6.0, result.Emitted["count"]);
        Assert.Equal(6.0, result.Emitted["rows"]);
    }

    /// <summary>
    /// x − y ≠ 1 при x = 2 запрещает только y = 1. Прочитай привязка знак наоборот
    /// (y − x ≠ 1), запрещённого значения не нашлось бы, и решений было бы три.
    /// </summary>
    [Fact]
    public void Solve_NotEqual_KeepsItsDirection()
    {
        RunResult result = Run("""
            let rules = table.of({ x: <1, 1>, y: <-1, 0>, sign: ["!=", "="], rhs: <1, 2> })

            emit count = csp.solve(["x", "y"], lower: 0, upper: 2, constraints: rules, limit: 0).count
            """);

        Assert.Equal(2.0, result.Emitted["count"]);
    }

    [Fact]
    public void Solve_Infeasible_IsReportedAsStatus()
    {
        RunResult result = Run("""
            let found = csp.solve(["a", "b", "c"], lower: 0, upper: 1, all_different: [["a", "b", "c"]])

            emit status = found.status
            emit count = found.count
            """);

        Assert.Equal("infeasible", result.Emitted["status"]);
        Assert.Equal(0.0, result.Emitted["count"]);
    }

    [Fact]
    public void Solve_GeneralNotEqual_IsRejectedBeforeSearch()
    {
        Diagnostic error = Script.FailsWith("""
            let rules = table.of({ x: <1>, y: <1>, z: <1>, sign: ["!="], rhs: <3> })
            emit r = csp.solve(["x", "y", "z"], lower: 0, upper: 3, constraints: rules)
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("x - y != c", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_FractionalCoefficient_PointsToSmt()
    {
        Diagnostic error = Script.FailsWith("""
            let rules = table.of({ x: <0.5>, sign: ["<="], rhs: <3> })
            emit r = csp.solve(["x"], lower: 0, upper: 9, constraints: rules)
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("csp.smt", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_UnknownNameInGroup_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = csp.solve([\"x\", \"y\"], lower: 0, upper: 3, all_different: [[\"x\", \"z\"]])");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("'z'", error.Message, StringComparison.Ordinal);
    }

    // --- sat ---

    [Fact]
    public void Sat_ReadsNegation()
    {
        RunResult result = Run("""
            let found = csp.sat([["a", "b"], ["!a"]])

            emit a = found.values.a
            emit b = found.values.b
            emit verified = found.verified
            """);

        Assert.Equal(false, result.Emitted["a"]);
        Assert.Equal(true, result.Emitted["b"]);
        Assert.Equal(true, result.Emitted["verified"]);
    }

    /// <summary>Три голубя, две клетки, в клетке не больше одного: невыполнимо.</summary>
    [Fact]
    public void Sat_Pigeonhole_IsUnsatisfiable()
    {
        RunResult result = Run("""
            let found = csp.sat(
                [["p1_a", "p1_b"], ["p2_a", "p2_b"], ["p3_a", "p3_b"]],
                at_most_one: [["p1_a", "p2_a", "p3_a"], ["p1_b", "p2_b", "p3_b"]])

            emit status = found.status
            """);

        Assert.Equal("unsatisfiable", result.Emitted["status"]);
    }

    [Fact]
    public void Sat_ExactlyOne_PicksOne()
    {
        RunResult result = Run("""
            let found = csp.sat([["!red"]], exactly_one: [["red", "green", "blue"]])
            let v = found.values

            emit red = v.red
            emit total = (if v.green { 1 } else { 0 }) + (if v.blue { 1 } else { 0 })
            """);

        Assert.Equal(false, result.Emitted["red"]);
        Assert.Equal(1.0, result.Emitted["total"]);
    }

    // --- smt ---

    /// <summary>x ≤ 3 и y ≤ 5 не дают x + y ≥ 10; без одного из них — дают.</summary>
    private const string Terms = """
        let terms = table.of({
            name: ["enough", "small_x", "small_y"],
            x: <1, 1, 0>,
            y: <1, 0, 1>,
            sign: [">=", "<=", "<="],
            rhs: <10, 3, 5>
        })
        let kinds = { x: "int", y: "real", flag: "bool" }
        """;

    [Fact]
    public void Smt_AllHardConditions_AreUnsatisfiable()
    {
        RunResult result = Run(Terms + """

            emit status = csp.smt(kinds, terms, [["enough"], ["small_x"], ["small_y"]]).status
            """);

        Assert.Equal("unsatisfiable", result.Emitted["status"]);
    }

    /// <summary>Флаг запрещён, значит, из «флаг или малый x» остаётся малый x.</summary>
    [Fact]
    public void Smt_MixesFlagsAndConditions()
    {
        RunResult result = Run(Terms + """

            let found = csp.smt(kinds, terms, [["enough"], ["flag", "small_x"], ["!flag"], ["small_y", "!small_y"]])

            emit status = found.status
            emit x = found.values.x
            emit flag = found.values.flag
            emit enough = (found.conditions |> table.filter(c => c.name == "enough"))[0].holds
            """);

        Assert.Equal("satisfiable", result.Emitted["status"]);
        Assert.True(Number(result, "x") <= 3);
        Assert.Equal(false, result.Emitted["flag"]);
        Assert.Equal(true, result.Emitted["enough"]);
    }

    [Fact]
    public void Smt_UnusedCondition_IsRejected()
    {
        Diagnostic error = Script.FailsWith(Terms + "\nemit r = csp.smt(kinds, terms, [[\"enough\"], [\"small_x\"]])");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("small_y", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Smt_UnknownLiteral_ListsKnownNames()
    {
        Diagnostic error = Script.FailsWith(Terms + "\nemit r = csp.smt(kinds, terms, [[\"enough\", \"smal_x\"], [\"small_x\"], [\"small_y\"]])");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("small_x", error.Hint, StringComparison.Ordinal);
    }
}
