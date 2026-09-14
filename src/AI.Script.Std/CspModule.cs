using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Solvers.Constraints.Cp;
using AI.Solvers.Constraints.Sat;
using AI.Solvers.Constraints.Smt;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>csp</c>: задачи на ограничения — найти допустимое, а не лучшее.
/// </summary>
/// <remarks>
/// Три решателя, три вида переменных: целые с конечной областью (<c>solve</c>), булевы
/// (<c>sat</c>) и смесь булевой логики с линейными неравенствами (<c>smt</c>). Линейные
/// ограничения записываются той же таблицей, что и в <c>opt.lp</c>, — колонка на переменную,
/// <c>sign</c> и <c>rhs</c>, — а логика — списком дизъюнктов из имён: <c>[["a", "!b"]]</c>
/// читается как «a или не b». Строка-формула с разбором внутри была бы короче, но это второй
/// язык внутри первого, которого не видит проверка до запуска.
/// </remarks>
[ScriptModule("csp", "Ограничения: целые переменные, SAT, SMT — найти допустимое", Version = "0.1")]
public static class CspModule
{
    /// <summary>
    /// Целочисленная задача с ограничениями.
    /// </summary>
    /// <remarks>
    /// Знак <c>!=</c> допускается только в виде <c>x − y ≠ c</c> — две переменные с
    /// коэффициентами 1 и −1: так записываются ферзи на диагоналях и соседи разных цветов, а
    /// общий линейный запрет решатель не распространяет. Проверка по таблице ловит это до поиска.
    /// </remarks>
    [ScriptFn("solve", "Целые переменные с ограничениями: найти одно или все решения",
        Example = "let found = csp.solve([\"x\", \"y\", \"z\"], lower: 1, upper: 9, constraints: rules, all_different: [[\"x\", \"y\", \"z\"]])")]
    public static ScriptRecord Solve(
        [ScriptParam("имена переменных")] string[] variables,
        [ScriptParam("нижняя граница каждой переменной")] int lower,
        [ScriptParam("верхняя граница каждой переменной")] int upper,
        [ScriptParam("линейные ограничения: колонки-переменные, sign (<=, >=, =, !=), rhs")] ScriptTable? constraints = null,
        [ScriptParam("группы переменных, которые обязаны различаться")] ScriptList? all_different = null,
        [ScriptParam("сколько решений искать; 0 — все")] int limit = 1,
        [ScriptParam("предел узлов перебора")] int max_nodes = 5000000)
    {
        const string function = "csp.solve";

        ScriptData.Require(variables.Length > 0, $"{function}: нет ни одной переменной");
        ScriptData.Require(lower <= upper, $"{function}: нижняя граница {lower} больше верхней {upper}");
        ScriptData.Require(limit >= 0, $"{function}: число решений не может быть отрицательным");
        ScriptData.Require(max_nodes >= 1, $"{function}: узлов перебора — хотя бы один");
        RequireDistinct(variables, function);

        var model = new CpModel("задача");
        var byName = new Dictionary<string, IntVariable>(StringComparer.Ordinal);

        foreach (string name in variables) byName[name] = model.AddVariable(name, lower, upper);

        if (constraints != null)
        {
            foreach (ScriptData.ConstraintRow row in ScriptData.ConstraintRows(constraints, variables, function,
                         column => $"переменные: {string.Join(", ", variables)}",
                         "<=", ">=", "=", "!="))
            {
                AddIntegerRow(model, row, variables, byName, function);
            }
        }

        foreach (string[] group in Groups(all_different, "all_different", function))
        {
            ScriptData.Require(group.Length >= 2, $"{function}: в группе all_different меньше двух переменных");

            _ = model.Add(new AllDifferent([.. group.Select(name => Lookup(byName, name, "all_different", function))]));
        }

        CpSolution solution = CpSolver.Solve(model, new CpOptions
        {
            SolutionLimit = limit == 0 ? int.MaxValue : limit,
            MaxNodes = max_nodes,
        });

        return ScriptData.Record(solution,
            ("status", solution.Status switch
            {
                CpStatus.Satisfiable => "satisfiable",
                CpStatus.Infeasible => "infeasible",
                _ => "limit",
            }),
            ("satisfiable", solution.IsSatisfiable),
            ("count", solution.Count),
            ("values", solution.Count > 0
                ? ScriptData.Plain([.. variables.Select((name, j) => (name, (object)(double)solution.Solutions[0][j]))])
                : ScriptData.Plain([.. variables.Select(name => (name, (object)double.NaN))])),
            ("solutions", Solutions(variables, solution.Solutions)),
            ("nodes", solution.Nodes),
            ("propagations", solution.Propagations));
    }

    /// <summary>
    /// Выполнимость булевой формулы.
    /// </summary>
    /// <remarks>
    /// Переменные объявлять не нужно: ими становятся все имена, встреченные в литералах, в
    /// порядке появления. Группы «ровно одна» и «не больше одной» раскладываются в дизъюнкты
    /// решателем — руками их пишут с ошибкой в знаке чаще, чем без.
    /// </remarks>
    [ScriptFn("sat", "Выполнимость булевой формулы: дизъюнкты из имён, ! — отрицание",
        Example = "let plan = csp.sat([[\"tea\", \"coffee\"], [\"!tea\", \"!coffee\"]], exactly_one: [[\"red\", \"green\", \"blue\"]])")]
    public static ScriptRecord Sat(
        [ScriptParam("дизъюнкты: список списков имён, \"!x\" — отрицание")] ScriptList clauses,
        [ScriptParam("группы, где истинна ровно одна переменная")] ScriptList? exactly_one = null,
        [ScriptParam("группы, где истинна не больше чем одна")] ScriptList? at_most_one = null,
        [ScriptParam("предел конфликтов; 0 — без предела")] int max_conflicts = 0)
    {
        const string function = "csp.sat";

        ScriptData.Require(max_conflicts >= 0, $"{function}: предел конфликтов не может быть отрицательным");

        string[][] disjunctions = Groups(clauses, "clauses", function);
        string[][] exactlyOne = Groups(exactly_one, "exactly_one", function);
        string[][] atMostOne = Groups(at_most_one, "at_most_one", function);

        var formula = new CnfFormula();
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();

        int Literal(string text)
        {
            (string name, bool negated) = ParseLiteral(text, function);

            if (!numbers.TryGetValue(name, out int number))
            {
                number = formula.AddVariable();
                numbers[name] = number;
                order.Add(name);
            }

            return negated ? -number : number;
        }

        foreach (string[] clause in disjunctions)
        {
            ScriptData.Require(clause.Length > 0, $"{function}: пустой дизъюнкт невыполним — уберите его");
            _ = formula.AddClause([.. clause.Select(Literal)]);
        }

        foreach (string[] group in exactlyOne) _ = formula.ExactlyOne([.. group.Select(Literal)]);
        foreach (string[] group in atMostOne) _ = formula.AtMostOne([.. group.Select(Literal)]);

        ScriptData.Require(order.Count > 0, $"{function}: в формуле нет ни одной переменной");

        SatSolution solution = SatSolver.Solve(formula, new SatOptions { MaxConflicts = max_conflicts });
        bool satisfiable = solution.IsSatisfiable;

        return ScriptData.Record(solution,
            ("status", solution.Status switch
            {
                SatStatus.Satisfiable => "satisfiable",
                SatStatus.Unsatisfiable => "unsatisfiable",
                _ => "unknown",
            }),
            ("satisfiable", satisfiable),
            ("values", satisfiable
                ? ScriptData.Plain([.. order.Select(name => (name, (object)solution[numbers[name]]))])
                : ScriptData.Plain([])),
            ("verified", satisfiable && solution.Verify(formula)),
            ("decisions", solution.Decisions),
            ("conflicts", solution.Conflicts));
    }

    /// <summary>
    /// Булева логика над линейными условиями.
    /// </summary>
    /// <remarks>
    /// Условия — строки таблицы с именами; в дизъюнктах они стоят рядом с булевыми
    /// переменными: <c>[["cheap", "express"]]</c> — «дёшево или экспресс». Дизъюнкт из одного
    /// имени — жёсткое условие. Условие, не вошедшее ни в один дизъюнкт, — отказ: иначе строка
    /// таблицы молча ничего бы не значила, а автор думал бы, что она соблюдена.
    /// </remarks>
    [ScriptFn("smt", "Булевы условия над линейными неравенствами: целые, вещественные, флаги",
        Example = "let found = csp.smt({ cost: \"real\", days: \"int\", express: \"bool\" }, terms, [[\"cheap\", \"express\"], [\"budget\"]])")]
    public static ScriptRecord Smt(
        [ScriptParam("переменные: запись «имя: \"int\", \"real\" или \"bool\"»")] ScriptRecord variables,
        [ScriptParam("именованные линейные условия: name, колонки-переменные, sign, rhs")] ScriptTable conditions,
        [ScriptParam("дизъюнкты из имён условий и булевых переменных, \"!x\" — отрицание")] ScriptList clauses,
        [ScriptParam("нижние границы числовых переменных; по умолчанию без границы")] ScriptRecord? lower = null,
        [ScriptParam("верхние границы числовых переменных; по умолчанию без границы")] ScriptRecord? upper = null)
    {
        const string function = "csp.smt";

        ScriptData.Require(variables.Count > 0, $"{function}: нет ни одной переменной");

        if (!conditions.TryGet("name", out _))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: у условий нет имён",
                "добавьте колонку name — по этим именам условия стоят в дизъюнктах");
        }

        var model = new SmtModel("задача");
        var numeric = new Dictionary<string, NumericVariable>(StringComparer.Ordinal);
        var flags = new Dictionary<string, BooleanVariable>(StringComparer.Ordinal);

        for (int i = 0; i < variables.Count; i++)
        {
            string name = variables.Keys[i];
            string sort = ScriptData.Kind(variables.Values[i].AsString($"вид переменной '{name}'"), "вид", function,
                ("int", "int"), ("real", "real"), ("bool", "bool"));

            if (sort == "bool")
            {
                flags[name] = model.Bool(name);
                continue;
            }

            double low = Bound(lower, name, double.NegativeInfinity);
            double high = Bound(upper, name, double.PositiveInfinity);

            numeric[name] = sort == "int" ? model.Int(name, low, high) : model.Real(name, low, high);
        }

        RequireNumeric(lower, numeric, "lower", function);
        RequireNumeric(upper, numeric, "upper", function);

        string[] numericNames = [.. numeric.Keys];
        var atoms = new Dictionary<string, SmtFormula>(StringComparer.Ordinal);

        foreach (ScriptData.ConstraintRow row in ScriptData.ConstraintRows(conditions, numericNames, function,
                     column => flags.ContainsKey(column)
                         ? $"'{column}' — булева переменная; она ставится в дизъюнкты, а не в колонки"
                         : $"числовые переменные: {string.Join(", ", numericNames)}",
                     "<=", "<", ">=", ">", "=", "!="))
        {
            ScriptData.Require(!atoms.ContainsKey(row.Name), $"{function}: условие '{row.Name}' названо дважды");
            ScriptData.Require(!variables.Has(row.Name), $"{function}: условие '{row.Name}' названо так же, как переменная");

            atoms[row.Name] = Atom(row, numericNames, numeric);
        }

        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (string[] clause in Groups(clauses, "clauses", function))
        {
            ScriptData.Require(clause.Length > 0, $"{function}: пустой дизъюнкт невыполним — уберите его");

            var literals = new SmtFormula[clause.Length];

            for (int i = 0; i < clause.Length; i++)
            {
                (string name, bool negated) = ParseLiteral(clause[i], function);

                SmtFormula formula = atoms.TryGetValue(name, out SmtFormula? atom) ? atom
                    : flags.TryGetValue(name, out BooleanVariable? flag) ? flag
                    : throw new ScriptError(
                        DiagnosticCodes.UnknownArgument,
                        $"{function}: в дизъюнкте имя '{name}', которого нет ни среди условий, ни среди булевых переменных",
                        $"условия: {string.Join(", ", atoms.Keys)}; булевы: {string.Join(", ", flags.Keys)}");

                used.Add(name);
                literals[i] = negated ? SmtFormula.Not(formula) : formula;
            }

            _ = model.Assert(literals.Length == 1 ? literals[0] : SmtFormula.Or(literals));
        }

        foreach (string name in atoms.Keys)
        {
            if (used.Contains(name)) continue;

            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: условие '{name}' не входит ни в один дизъюнкт",
                $"жёсткое условие — дизъюнкт из одного имени: [[\"{name}\"]]");
        }

        SmtSolution solution = SmtSolver.Solve(model);
        bool satisfiable = solution.IsSatisfiable;
        var values = new List<(string Name, object Value)>(variables.Count);

        if (satisfiable)
        {
            foreach (string name in variables.Keys)
            {
                values.Add(flags.TryGetValue(name, out BooleanVariable? flag)
                    ? (name, solution[flag])
                    : (name, solution[numeric[name]]));
            }
        }

        return ScriptData.Record(solution,
            ("status", solution.Status switch
            {
                SmtStatus.Satisfiable => "satisfiable",
                SmtStatus.Unsatisfiable => "unsatisfiable",
                _ => "unknown",
            }),
            ("satisfiable", satisfiable),
            ("values", ScriptData.Plain([.. values])),
            ("conditions", ScriptData.Table([.. atoms],
                ("name", pair => pair.Key),
                ("holds", pair => satisfiable && solution.Evaluate(pair.Value)))),
            ("verified", solution.Verified),
            ("sat_calls", solution.SatCalls),
            ("lemmas", solution.TheoryLemmas));
    }

    // --- внутреннее ---

    /// <summary>Строка таблицы как целочисленное ограничение решателя.</summary>
    private static void AddIntegerRow(
        CpModel model,
        ScriptData.ConstraintRow row,
        IReadOnlyList<string> variables,
        Dictionary<string, IntVariable> byName,
        string function)
    {
        var scope = new List<IntVariable>();
        var coefficients = new List<int>();

        for (int j = 0; j < variables.Count; j++)
        {
            double coefficient = row.Coefficients[j];

            if (coefficient == 0) continue;

            scope.Add(byName[variables[j]]);
            coefficients.Add(Integer(coefficient, $"коэффициент '{variables[j]}' в ограничении '{row.Name}'", function));
        }

        int rhs = Integer(row.Rhs, $"правая часть ограничения '{row.Name}'", function);

        if (row.Sign == "!=")
        {
            // x − y ≠ c ⇔ запрет y + c = x: решатель запрещает left + offset = right, отсюда порядок.
            int positive = coefficients.IndexOf(1), negative = coefficients.IndexOf(-1);

            if (scope.Count != 2 || positive < 0 || negative < 0)
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"{function}: ограничение '{row.Name}' со знаком != не вида x - y != c",
                    "запрет записывается двумя переменными с коэффициентами 1 и -1: x - y != c");
            }

            _ = model.Add(new NotEqual(scope[negative], scope[positive], rhs));
            return;
        }

        ScriptData.Require(scope.Count > 0, $"{function}: в ограничении '{row.Name}' нет ни одной переменной");

        LinearRelation relation = row.Sign switch
        {
            "<=" => LinearRelation.LessOrEqual,
            ">=" => LinearRelation.GreaterOrEqual,
            _ => LinearRelation.Equal,
        };

        _ = model.AddLinear([.. scope], [.. coefficients], relation, rhs);
    }

    private static int Integer(double value, string what, string function)
    {
        if (value != Math.Round(value) || Math.Abs(value) > int.MaxValue)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: {what} — {value}, а переменные целые",
                "в целочисленной задаче коэффициенты и правые части — целые числа; дробные условия решает csp.smt");
        }

        return (int)value;
    }

    /// <summary>Линейное условие SMT из строки таблицы.</summary>
    private static SmtFormula Atom(
        ScriptData.ConstraintRow row, IReadOnlyList<string> names, Dictionary<string, NumericVariable> numeric)
    {
        var terms = new List<LinearExpression>();

        for (int j = 0; j < names.Count; j++)
        {
            if (row.Coefficients[j] != 0) terms.Add(row.Coefficients[j] * (LinearExpression)numeric[names[j]]);
        }

        LinearExpression sum = LinearExpression.Sum(terms);
        LinearExpression rhs = row.Rhs;

        return row.Sign switch
        {
            "<=" => sum <= rhs,
            "<" => sum < rhs,
            ">=" => sum >= rhs,
            ">" => sum > rhs,
            "=" => SmtFormula.Equal(sum, rhs),
            _ => SmtFormula.Distinct(sum, rhs),
        };
    }

    /// <summary>Таблица решений: строка на решение, колонка на переменную.</summary>
    private static ScriptTable Solutions(IReadOnlyList<string> variables, IReadOnlyList<IReadOnlyList<int>> solutions)
    {
        var columns = new ScriptColumn[variables.Count];

        for (int j = 0; j < variables.Count; j++)
        {
            var values = new ScriptValue[solutions.Count];

            for (int i = 0; i < solutions.Count; i++) values[i] = ScriptValue.Num(solutions[i][j]);

            columns[j] = ScriptColumn.Own(variables[j], values);
        }

        return ScriptTable.Create(columns);
    }

    /// <summary>Список списков имён: группы и дизъюнкты.</summary>
    private static string[][] Groups(ScriptList? groups, string parameter, string function)
    {
        if (groups == null) return [];

        var result = new string[groups.Count][];

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Type != ScriptType.List)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"{function}: {parameter}[{i}] — не список имён",
                    $"{parameter} — список списков: [[\"a\", \"b\"], [\"c\"]]");
            }

            ScriptList group = groups[i].AsList();

            result[i] = new string[group.Count];

            for (int k = 0; k < group.Count; k++) result[i][k] = group[k].AsString($"{parameter}[{i}][{k}]");
        }

        return result;
    }

    private static (string Name, bool Negated) ParseLiteral(string text, string function)
    {
        string literal = text.Trim();
        bool negated = literal.StartsWith('!');
        string name = negated ? literal[1..].Trim() : literal;

        ScriptData.Require(name.Length > 0, $"{function}: пустое имя в дизъюнкте");

        return (name, negated);
    }

    private static IntVariable Lookup(Dictionary<string, IntVariable> byName, string name, string parameter, string function) =>
        byName.TryGetValue(name, out IntVariable? variable)
            ? variable
            : throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: в {parameter} переменная '{name}', которой нет в variables",
                $"переменные: {string.Join(", ", byName.Keys)}");

    private static void RequireDistinct(string[] names, string function)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string name in names)
            ScriptData.Require(seen.Add(name), $"{function}: переменная '{name}' объявлена дважды");
    }

    private static void RequireNumeric(
        ScriptRecord? bounds, Dictionary<string, NumericVariable> numeric, string parameter, string function)
    {
        if (bounds == null) return;

        foreach (string name in bounds.Keys)
        {
            if (numeric.ContainsKey(name)) continue;

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: в {parameter} '{name}' — не числовая переменная",
                $"числовые переменные: {string.Join(", ", numeric.Keys)}");
        }
    }

    private static double Bound(ScriptRecord? bounds, string name, double fallback) =>
        bounds != null && bounds.TryGet(name, out ScriptValue value) ? value.AsNumber($"граница '{name}'") : fallback;
}
