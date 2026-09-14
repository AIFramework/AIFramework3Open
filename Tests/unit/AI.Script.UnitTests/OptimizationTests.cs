using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространство <c>opt</c> над AI.Solvers.Optimization.
/// </summary>
/// <remarks>
/// Задачи — из тех, что решаются на бумаге: оптимум известен заранее, и проверяется не решатель
/// (у него свои тесты), а привязка — что колонка таблицы попала к своей переменной, знак
/// ограничения не перевернулся, а ответ вернулся под тем именем, под которым спрашивали.
/// </remarks>
public sealed class OptimizationTests
{
    private static RunResult Run(string source) => Script.RunOk(source);

    private static double Number(RunResult result, string name) => (double)result.Emitted[name]!;

    /// <summary>Стулья и столы: оптимум в вершине (20, 15), выручка 1350, оба ресурса исчерпаны.</summary>
    private const string Furniture = """
        let limits = table.of({
            name: ["wood", "labor"],
            chairs: <2, 3>,
            tables: <4, 2>,
            sign: ["<=", "<="],
            rhs: <100, 90>
        })
        """;

    [Fact]
    public void Lp_FindsKnownOptimum_ByName()
    {
        RunResult result = Run(Furniture + """

            let plan = opt.lp({ chairs: 30, tables: 50 }, limits, maximize: true)

            emit status = plan.status
            emit objective = plan.objective
            emit chairs = plan.values.chairs
            emit tables = plan.values.tables
            emit binding = len(plan.constraints |> table.filter(c => c.binding))
            emit first = plan.constraints[0].name
            """);

        Assert.Equal("optimal", result.Emitted["status"]);
        Assert.Equal(1350, Number(result, "objective"), 6);
        Assert.Equal(20, Number(result, "chairs"), 6);
        Assert.Equal(15, Number(result, "tables"), 6);
        Assert.Equal(2.0, result.Emitted["binding"]);
        Assert.Equal("wood", result.Emitted["first"]);
    }

    /// <summary>Непрерывный оптимум (3, 1.5) даёт 21, целочисленный — (4, 0) и 20.</summary>
    [Fact]
    public void Lp_Integer_DiffersFromContinuous()
    {
        const string limits = """
            let limits = table.of({ x: <6, 1>, y: <4, 2>, sign: ["<=", "<="], rhs: <24, 6> })
            """;

        RunResult result = Run(limits + """

            let relaxed = opt.lp({ x: 5, y: 4 }, limits, maximize: true)
            let whole = opt.lp({ x: 5, y: 4 }, limits, maximize: true, integer: ["x", "y"])

            emit relaxed = relaxed.objective
            emit whole = whole.objective
            emit x = whole.values.x
            emit y = whole.values.y
            """);

        Assert.Equal(21, Number(result, "relaxed"), 6);
        Assert.Equal(20, Number(result, "whole"), 6);
        Assert.Equal(4, Number(result, "x"), 6);
        Assert.Equal(0, Number(result, "y"), 6);
    }

    /// <summary>Рюкзак вместимостью 6: лучше всего b и c — ценность 20.</summary>
    [Fact]
    public void Lp_Binary_SolvesKnapsack()
    {
        RunResult result = Run("""
            let capacity = table.of({ a: <3>, b: <4>, c: <2>, sign: ["<="], rhs: <6> })
            let pick = opt.lp({ a: 10, b: 13, c: 7 }, capacity, maximize: true, binary: ["a", "b", "c"])

            emit value = pick.objective
            emit a = pick.values.a
            emit b = pick.values.b
            """);

        Assert.Equal(20, Number(result, "value"), 6);
        Assert.Equal(0, Number(result, "a"), 6);
        Assert.Equal(1, Number(result, "b"), 6);
    }

    /// <summary>Знаки «≥» и «=»: x + y ≥ 10, x − y = 2 при минимуме 2x + 3y дают (6, 4).</summary>
    [Fact]
    public void Lp_GreaterAndEqualSigns_AreRespected()
    {
        RunResult result = Run("""
            let rules = table.of({ x: <1, 1>, y: <1, -1>, sign: [">=", "="], rhs: <10, 2> })
            let plan = opt.lp({ x: 2, y: 3 }, rules)

            emit cost = plan.objective
            emit x = plan.values.x
            emit y = plan.values.y
            """);

        Assert.Equal(24, Number(result, "cost"), 6);
        Assert.Equal(6, Number(result, "x"), 6);
        Assert.Equal(4, Number(result, "y"), 6);
    }

    /// <summary>Без границы переменная уходит в минус до ограничения: без <c>-inf</c> остановилась бы на нуле.</summary>
    [Fact]
    public void Lp_FreeVariable_GoesBelowZero()
    {
        RunResult result = Run("""
            let floor = table.of({ x: <1>, sign: [">="], rhs: <-5> })

            emit bounded = opt.lp({ x: 1 }, floor).values.x
            emit free = opt.lp({ x: 1 }, floor, lower: { x: -inf }).values.x
            """);

        Assert.Equal(0, Number(result, "bounded"), 6);
        Assert.Equal(-5, Number(result, "free"), 6);
    }

    [Fact]
    public void Lp_Infeasible_IsReportedAsStatus()
    {
        RunResult result = Run("""
            let rules = table.of({ x: <1, 1>, sign: [">=", "<="], rhs: <5, 3> })
            let plan = opt.lp({ x: 1 }, rules)

            emit status = plan.status
            emit optimal = plan.optimal
            """);

        Assert.Equal("infeasible", result.Emitted["status"]);
        Assert.Equal(false, result.Emitted["optimal"]);
    }

    [Fact]
    public void Lp_Unbounded_IsReportedAsStatus()
    {
        RunResult result = Run("""
            let rules = table.of({ x: <1>, sign: [">="], rhs: <1> })

            emit status = opt.lp({ x: 1 }, rules, maximize: true).status
            """);

        Assert.Equal("unbounded", result.Emitted["status"]);
    }

    /// <summary>Опечатка в колонке — отказ, а не молча выброшенный коэффициент.</summary>
    [Fact]
    public void Lp_ColumnThatIsNotAVariable_IsRejected()
    {
        Diagnostic error = Script.FailsWith(Furniture + "\nemit p = opt.lp({ chairs: 30, tabels: 50 }, limits)");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("tables", error.Message, StringComparison.Ordinal);
        Assert.Contains("tabels", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Lp_UnknownSign_ListsKnown()
    {
        Diagnostic error = Script.FailsWith("""
            let rules = table.of({ x: <1>, sign: ["=<"], rhs: <1> })
            emit p = opt.lp({ x: 1 }, rules)
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("\"<=\"", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Lp_IntegerNameNotInObjective_IsRejected()
    {
        Diagnostic error = Script.FailsWith(Furniture + "\nemit p = opt.lp({ chairs: 30, tables: 50 }, limits, integer: [\"desks\"])");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("desks", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ближайшая к (1, 2) точка полуплоскости x + y ≤ 2 — (0.5, 1.5): минимум
    /// (x − 1)² + (y − 2)², то есть Q = 2I и c = (−2, −4).
    /// </summary>
    [Fact]
    public void Qp_ProjectsOntoHalfPlane()
    {
        RunResult result = Run("""
            let q = mat.of([<2, 0>, <0, 2>])
            let rule = table.of({ x: <1>, y: <1>, sign: ["<="], rhs: <2> })
            let point = opt.qp(q, { x: -2, y: -4 }, constraints: rule)

            emit status = point.status
            emit x = point.values.x
            emit y = point.values.y
            emit binding = point.constraints[0].binding
            """);

        Assert.Equal("optimal", result.Emitted["status"]);
        Assert.Equal(0.5, Number(result, "x"), 6);
        Assert.Equal(1.5, Number(result, "y"), 6);
        Assert.Equal(true, result.Emitted["binding"]);
    }

    /// <summary>Два некоррелированных актива с дисперсиями 1 и 4: доли минимальной дисперсии — 0.8 и 0.2.</summary>
    [Fact]
    public void Qp_MinimumVariancePortfolio()
    {
        RunResult result = Run("""
            let q = mat.of([<2, 0>, <0, 8>])
            let budget = table.of({ a: <1>, b: <1>, sign: ["="], rhs: <1> })
            let weights = opt.qp(q, { a: 0, b: 0 }, constraints: budget)

            emit a = weights.values.a
            emit b = weights.values.b
            """);

        Assert.Equal(0.8, Number(result, "a"), 6);
        Assert.Equal(0.2, Number(result, "b"), 6);
    }

    [Fact]
    public void Qp_MatrixSizeMismatch_NamesTheOrder()
    {
        Diagnostic error = Script.FailsWith("emit p = opt.qp(mat.eye(3), { x: 0, y: 0 })");

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("порядке полей", error.Hint, StringComparison.Ordinal);
    }

    // --- minimize ---

    /// <summary>Функция Розенброка: узкий изогнутый овраг с минимумом в (1, 1).</summary>
    [Fact]
    public void Minimize_Rosenbrock_ReachesTheValley()
    {
        RunResult result = Run("""
            let fit = opt.minimize(p => (1 - p[0]) * (1 - p[0]) + 100 * (p[1] - p[0] * p[0]) * (p[1] - p[0] * p[0]), <-1.2, 1>)

            emit x = fit.point[0]
            emit y = fit.point[1]
            emit converged = fit.converged
            """);

        Assert.Equal(1, Number(result, "x"), 3);
        Assert.Equal(1, Number(result, "y"), 3);
        Assert.Equal(true, result.Emitted["converged"]);
    }

    /// <summary>Подгонка прямой по ошибке: функция скрипта зовёт stat.rmse, метод находит 2x + 1.</summary>
    [Fact]
    public void Minimize_FitsLineThroughScriptFunction()
    {
        RunResult result = Run("""
            let x = vec.linspace(0, 1, n: 20)
            let y = 2 * x + 1
            let fit = opt.minimize(p => stat.rmse(y, p[0] * x + p[1]), <0, 0>)

            emit slope = fit.point[0]
            emit intercept = fit.point[1]
            """);

        Assert.Equal(2, Number(result, "slope"), 4);
        Assert.Equal(1, Number(result, "intercept"), 4);
    }

    [Fact]
    public void Minimize_Positive_StaysAboveZero()
    {
        RunResult result = Run("emit p = opt.minimize(p => (p[0] - 3) * (p[0] - 3), <1>, positive: true).point[0]");

        Assert.Equal(3, Number(result, "p"), 4);
    }

    [Fact]
    public void Minimize_Positive_RejectsNonPositiveStart() =>
        Assert.Equal(DiagnosticCodes.BadOperand,
            Script.FailsWith("emit p = opt.minimize(p => p[0] * p[0], <0>, positive: true)").Code);

    [Fact]
    public void Minimize_FunctionReturningText_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit p = opt.minimize(p => \"много\", <1>)");

        Assert.Contains("opt.minimize", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Qp_NotPositiveDefinite_IsReportedWithFunctionName()
    {
        Diagnostic error = Script.FailsWith("emit p = opt.qp(mat.of([<-1, 0>, <0, 1>]), { x: 0, y: 0 }, lower: { x: -inf, y: -inf })");

        Assert.Contains("opt.qp", error.Message, StringComparison.Ordinal);
    }
}
