using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Solvers.Optimization;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>opt</c>: линейное, целочисленное и квадратичное программирование, минимум
/// произвольной функции.
/// </summary>
/// <remarks>
/// Задача записывается так, как её формулируют люди, а не так, как её хранит решатель: цель —
/// запись «переменная: коэффициент», ограничения — таблица, где колонка на переменную, а
/// <c>sign</c> и <c>rhs</c> — знак и правая часть. Таблицу ограничений можно прочитать из файла
/// или собрать конвейером: это данные, над которыми язык и работает. Матричная запись в стиле
/// <c>linprog</c> короче, но возвращает вектор, где третье число означает неизвестно что, — здесь
/// ответ приходит по именам.
/// <para>
/// Нижняя граница переменной по умолчанию — ноль, в обеих функциях одинаково: объём выпуска,
/// доля в портфеле, число смен отрицательными не бывают. Переменной без границы задают
/// <c>lower: { x: -inf }</c>.
/// </para>
/// <para>
/// Минимум произвольной функции ищет метод Нелдера — Мида из <c>AI.Solvers.Optimization</c> —
/// тот же, которым эконометрика и экономика оценивают свои правдоподобия, а не вторая
/// реализация ради языка.
/// </para>
/// </remarks>
[ScriptModule("opt", "Оптимизация: LP, MILP, QP; минимум произвольной функции", Version = "0.1")]
public static class OptModule
{
    /// <summary>
    /// Линейное и смешанно-целочисленное программирование.
    /// </summary>
    /// <remarks>
    /// Кроме плана возвращается таблица ограничений с зазором: какое ограничение исчерпано
    /// (<c>binding</c>), то и держит план. Именно это обычно и нужно узнать — какой ресурс
    /// докупать, — а по одним значениям переменных этого не видно.
    /// </remarks>
    [ScriptFn("lp", "Линейное и целочисленное программирование: лучший план при ограничениях",
        Example = "let plan = opt.lp({ chairs: 30, tables: 50 }, limits, maximize: true, integer: [\"chairs\", \"tables\"])")]
    public static ScriptRecord Lp(
        [ScriptParam("цель: запись «переменная: коэффициент»")] ScriptRecord objective,
        [ScriptParam("ограничения: колонки-переменные, sign (<=, >=, =), rhs и name")] ScriptTable constraints,
        [ScriptParam("максимизировать вместо минимизации")] bool maximize = false,
        [ScriptParam("целочисленные переменные")] string[]? integer = null,
        [ScriptParam("булевы переменные: 0 или 1")] string[]? binary = null,
        [ScriptParam("нижние границы; по умолчанию 0, -inf — без границы")] ScriptRecord? lower = null,
        [ScriptParam("верхние границы; по умолчанию без границы")] ScriptRecord? upper = null,
        [ScriptParam("предел узлов ветвления для целочисленных задач")] int max_nodes = 20000)
    {
        const string function = "opt.lp";

        IReadOnlyList<string> variables = Variables(objective, function);
        HashSet<string> integers = Subset(integer, variables, "integer", function);
        HashSet<string> binaries = Subset(binary, variables, "binary", function);

        ScriptData.Require(!integers.Overlaps(binaries), $"{function}: переменная не может быть сразу integer и binary");
        ScriptData.Require(max_nodes >= 1, $"{function}: узлов ветвления — хотя бы один");
        RequireKnown(lower, variables, "lower", function);
        RequireKnown(upper, variables, "upper", function);

        var program = new LinearProgram(maximize ? ObjectiveSense.Maximize : ObjectiveSense.Minimize, "план");
        var coefficients = new Vector(variables.Count);

        for (int j = 0; j < variables.Count; j++)
        {
            string name = variables[j];
            double low = Bound(lower, name, 0);
            double high = Bound(upper, name, double.PositiveInfinity);

            coefficients[j] = objective.Values[j].AsNumber($"коэффициент '{name}'");

            _ = binaries.Contains(name) ? program.AddBinaryVariable(name)
                : integers.Contains(name) ? program.AddIntegerVariable(name, low, high)
                : double.IsNegativeInfinity(low) && double.IsPositiveInfinity(high) ? program.AddFreeVariable(name)
                : program.AddVariable(name, low, high);
        }

        program.SetObjective(coefficients);

        List<Row> rows = Rows(constraints, variables, function);

        foreach (Row row in rows) _ = program.AddConstraint(row.Coefficients, row.Sign, row.Rhs, row.Name);

        LpSolution solution = LpSolver.Solve(program, new LpOptions { MaxNodes = max_nodes });
        Vector values = solution.Values;
        bool solved = (solution.Status is SolverStatus.Optimal or SolverStatus.LimitReached) && values.Count == variables.Count;

        return ScriptData.Record(solution,
            ("status", StatusName(solution.Status)),
            ("optimal", solution.IsOptimal),
            ("objective", solution.Objective),
            ("values", Values(variables, values, solved)),
            ("constraints", Activity(rows, values, solved)),
            ("gap", solution.Gap),
            ("nodes", solution.Nodes),
            ("iterations", solution.Iterations));
    }

    /// <summary>
    /// Квадратичное программирование: минимум ½xᵀQx + cᵀx.
    /// </summary>
    /// <remarks>
    /// Порядок строк и столбцов <c>Q</c> задаётся порядком полей линейной части — матрица сама
    /// имён не несёт, и запись с именами остаётся единственным местом, где видно, какая строка
    /// какой переменной. Типичная задача — портфель: <c>Q</c> — удвоенная ковариация, линейная
    /// часть — минус ожидаемая доходность, ограничение — сумма долей равна единице.
    /// </remarks>
    [ScriptFn("qp", "Квадратичное программирование: минимум ½xᵀQx + cᵀx при ограничениях",
        Example = "let weights = opt.qp(cov, { a: 0, b: 0, c: 0 }, constraints: budget)")]
    public static ScriptRecord Qp(
        [ScriptParam("матрица Q: симметричная, положительно определённая")] Matrix quadratic,
        [ScriptParam("линейная часть: запись «переменная: коэффициент»; порядок задаёт строки Q")] ScriptRecord linear,
        [ScriptParam("ограничения: колонки-переменные, sign (<=, >=, =), rhs и name")] ScriptTable? constraints = null,
        [ScriptParam("нижние границы; по умолчанию 0, -inf — без границы")] ScriptRecord? lower = null,
        [ScriptParam("верхние границы; по умолчанию без границы")] ScriptRecord? upper = null)
    {
        const string function = "opt.qp";

        IReadOnlyList<string> variables = Variables(linear, function);

        if (quadratic.Height != variables.Count || quadratic.Width != variables.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{function}: матрица Q {quadratic.Height}×{quadratic.Width}, а переменных {variables.Count}",
                "строки и столбцы Q идут в порядке полей линейной части");
        }

        RequireKnown(lower, variables, "lower", function);
        RequireKnown(upper, variables, "upper", function);

        var linearPart = new Vector(variables.Count);

        for (int j = 0; j < variables.Count; j++)
            linearPart[j] = linear.Values[j].AsNumber($"коэффициент '{variables[j]}'");

        var program = new QuadraticProgram(quadratic, linearPart, "задача");
        List<Row> rows = constraints == null ? [] : Rows(constraints, variables, function);

        foreach (Row row in rows)
        {
            _ = row.Sign switch
            {
                ConstraintSign.LessOrEqual => program.AddInequality(row.Coefficients, row.Rhs, row.Name),
                ConstraintSign.GreaterOrEqual => program.AddInequality(Negated(row.Coefficients), -row.Rhs, row.Name),
                _ => program.AddEquality(row.Coefficients, row.Rhs, row.Name),
            };
        }

        for (int j = 0; j < variables.Count; j++)
            program.AddBounds(j, Bound(lower, variables[j], 0), Bound(upper, variables[j], double.PositiveInfinity));

        QpSolution solution = QpSolver.Solve(program);
        Vector values = solution.Values;
        bool solved = solution.IsOptimal && values.Count == variables.Count;

        return ScriptData.Record(solution,
            ("status", StatusName(solution.Status)),
            ("optimal", solution.IsOptimal),
            ("objective", solution.Objective),
            ("values", Values(variables, values, solved)),
            ("constraints", Activity(rows, values, solved)),
            ("max_violation", solution.MaxViolation),
            ("iterations", solution.Iterations));
    }

    /// <summary>
    /// Минимум функции многих переменных методом Нелдера — Мида.
    /// </summary>
    /// <remarks>
    /// Функция скрипта получает вектор параметров и возвращает число, поэтому подобрать можно что
    /// угодно, что язык умеет посчитать: параметры кривой по ошибке подгонки, настройки фильтра по
    /// качеству, цену по выручке. Недопустимую точку функция обозначает значением <c>nan</c> —
    /// симплекс отойдёт от неё. Для строго положительных параметров есть <c>positive: true</c>:
    /// поиск идёт по логарифмам, и граница нуля недостижима, а не штрафуется.
    /// </remarks>
    [ScriptFn("minimize", "Минимум функции многих переменных методом Нелдера — Мида",
        Example = "let fit = opt.minimize(p => stat.rmse(y, p[0] * x + p[1]), <1, 0>)")]
    public static ScriptRecord Minimize(
        IScriptContext context,
        [ScriptParam("функция вектора параметров, возвращающая число")] ScriptCallable f,
        [ScriptParam("начальная точка")] Vector start,
        [ScriptParam("только положительные параметры: поиск по логарифмам")] bool positive = false,
        [ScriptParam("масштаб начального симплекса; 0 — по умолчанию метода")] double step = 0,
        [ScriptParam("предел итераций")] int max_iter = 4000,
        [ScriptParam("порог сходимости по разбросу значений в симплексе")] double tolerance = 1e-10)
    {
        const string function = "opt.minimize";

        ScriptData.Require(start.Count > 0, $"{function}: начальная точка пуста");
        ScriptData.Require(step >= 0, $"{function}: масштаб симплекса не может быть отрицательным");
        ScriptData.Require(max_iter >= 1, $"{function}: итераций — хотя бы одна");
        ScriptData.Require(tolerance > 0, $"{function}: порог сходимости должен быть положительным");

        if (positive)
        {
            for (int i = 0; i < start.Count; i++)
                ScriptData.Require(start[i] > 0, $"{function}: при positive: true начальная точка должна быть положительной");
        }

        double Objective(Vector point) =>
            ScriptCallbacks.Invoke(context, f, ScriptValue.Vec(point)).AsNumber($"{function}: значение функции");

        var options = new NelderMeadOptions
        {
            Step = step > 0 ? step : positive ? 0.35 : 0.25,
            MaxIterations = max_iter,
            Tolerance = tolerance,
        };

        NelderMeadResult result = positive
            ? NelderMead.MinimizePositive(Objective, start, options)
            : NelderMead.Minimize(Objective, start, options);

        return ScriptData.Record(result,
            ("point", result.Point),
            ("value", result.Value),
            ("converged", result.Converged),
            ("iterations", result.Iterations),
            ("evaluations", result.Evaluations));
    }

    // --- внутреннее ---

    /// <summary>Ограничение задачи: имя, коэффициенты в порядке переменных, знак и правая часть.</summary>
    private sealed record Row(string Name, Vector Coefficients, ConstraintSign Sign, double Rhs);

    /// <summary>Переменные — поля записи цели в порядке объявления.</summary>
    private static IReadOnlyList<string> Variables(ScriptRecord objective, string function)
    {
        ScriptData.Require(objective.Count > 0, $"{function}: в цели нет ни одной переменной");

        return objective.Keys;
    }

    /// <summary>Ограничения из таблицы в общем формате — см. <see cref="ScriptData.ConstraintRows"/>.</summary>
    private static List<Row> Rows(ScriptTable table, IReadOnlyList<string> variables, string function) =>
    [
        .. ScriptData.ConstraintRows(table, variables, function,
                column => $"переменные: {string.Join(", ", variables)}; с нулевым коэффициентом в цели — {column}: 0",
                "<=", ">=", "=")
            .Select(row => new Row(row.Name, row.Coefficients, row.Sign switch
            {
                "<=" => ConstraintSign.LessOrEqual,
                ">=" => ConstraintSign.GreaterOrEqual,
                _ => ConstraintSign.Equal,
            }, row.Rhs)),
    ];

    /// <summary>
    /// Таблица ограничений в найденной точке: левая часть, зазор и исчерпанность.
    /// </summary>
    /// <remarks>
    /// Зазор считается в сторону допустимости: для «≤» — сколько ещё можно добавить, для «≥» —
    /// насколько перевыполнено. Отрицательный зазор — нарушение.
    /// </remarks>
    private static ScriptTable Activity(IReadOnlyList<Row> rows, Vector values, bool solved)
    {
        var activity = new List<(Row Row, double Lhs, double Slack)>(rows.Count);

        foreach (Row row in rows)
        {
            double lhs = double.NaN;

            if (solved)
            {
                lhs = 0;

                for (int j = 0; j < row.Coefficients.Count; j++) lhs += row.Coefficients[j] * values[j];
            }

            double slack = row.Sign == ConstraintSign.GreaterOrEqual ? lhs - row.Rhs : row.Rhs - lhs;

            activity.Add((row, lhs, slack));
        }

        return ScriptData.Table(activity,
            ("name", a => a.Row.Name),
            ("lhs", a => a.Lhs),
            ("rhs", a => a.Row.Rhs),
            ("slack", a => a.Slack),
            ("binding", a => Math.Abs(a.Slack) <= 1e-7 * Math.Max(1, Math.Abs(a.Row.Rhs))));
    }

    private static ScriptRecord Values(IReadOnlyList<string> variables, Vector values, bool solved) =>
        ScriptData.Plain([.. variables.Select((name, j) => (name, (object)(solved ? values[j] : double.NaN)))]);

    private static HashSet<string> Subset(string[]? names, IReadOnlyList<string> variables, string parameter, string function)
    {
        var subset = new HashSet<string>(StringComparer.Ordinal);

        foreach (string name in names ?? [])
        {
            if (!variables.Contains(name)) throw Unknown(name, variables, parameter, function);

            subset.Add(name);
        }

        return subset;
    }

    private static void RequireKnown(ScriptRecord? bounds, IReadOnlyList<string> variables, string parameter, string function)
    {
        if (bounds == null) return;

        foreach (string name in bounds.Keys)
        {
            if (!variables.Contains(name)) throw Unknown(name, variables, parameter, function);
        }
    }

    private static ScriptError Unknown(string name, IReadOnlyList<string> variables, string parameter, string function) =>
        new(DiagnosticCodes.UnknownArgument,
            $"{function}: в {parameter} переменная '{name}', которой нет в цели",
            $"переменные: {string.Join(", ", variables)}");

    private static double Bound(ScriptRecord? bounds, string name, double fallback) =>
        bounds != null && bounds.TryGet(name, out ScriptValue value) ? value.AsNumber($"граница '{name}'") : fallback;

    private static Vector Negated(Vector vector)
    {
        var negated = new Vector(vector.Count);

        for (int i = 0; i < vector.Count; i++) negated[i] = -vector[i];

        return negated;
    }

    private static string StatusName(SolverStatus status) => status switch
    {
        SolverStatus.Optimal => "optimal",
        SolverStatus.Infeasible => "infeasible",
        SolverStatus.Unbounded => "unbounded",
        _ => "limit",
    };
}
