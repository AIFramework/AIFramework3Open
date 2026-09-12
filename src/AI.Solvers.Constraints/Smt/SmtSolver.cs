using AI.Solvers.Constraints.Sat;

namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Решатель SMT для линейной арифметики над целыми и вещественными числами.
/// </summary>
/// <remarks>
/// <para>
/// Схема DPLL(T) «леммы по требованию». Формула переводится в КНФ преобразованием Цейтина,
/// арифметические атомы становятся булевыми переменными, и готовый <see cref="SatSolver"/> ищет
/// подстановку. Атомы, которые она делает истинными и ложными, проверяются на совместность
/// в теории — готовым <c>LpSolver</c>. Если арифметика противоречива, противоречие сжимается
/// до минимального подмножества, его отрицание добавляется в КНФ как лемма, и поиск повторяется.
/// </para>
/// <para>
/// Каждая лемма отсекает хотя бы одну подстановку атомов, а их конечное число, поэтому процесс
/// заканчивается. Булев решатель не инкрементальный и после каждой леммы решает КНФ заново;
/// распространения в теории нет. Это честная простая реализация для задач в сотни атомов,
/// а не замена промышленным решателям.
/// </para>
/// </remarks>
public static class SmtSolver
{
    /// <summary>Решает задачу</summary>
    /// <param name="model">Задача</param>
    /// <param name="options">Настройки</param>
    public static SmtSolution Solve(SmtModel model, SmtOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        options ??= new SmtOptions();

        var encoder = new TseitinEncoder();

        foreach (SmtFormula assertion in model.Assertions)
            encoder.Assert(assertion);

        int satCalls = 0;
        int lemmas = 0;
        int checks = 0;

        SmtSolution Result(SmtStatus status, bool[]? booleans = null, double[]? numbers = null, string? reason = null)
            => new(model, status, booleans ?? [], numbers ?? [], satCalls, lemmas, checks, reason, options.Tolerance);

        while (true)
        {
            if (lemmas >= options.MaxLemmas)
                return Result(SmtStatus.Unknown, reason: $"исчерпан предел в {options.MaxLemmas} лемм теории");

            SatSolution sat = SatSolver.Solve(encoder.Formula, options.Sat);
            satCalls++;

            if (sat.Status == SatStatus.Unknown)
                return Result(SmtStatus.Unknown, reason: "булев решатель исчерпал предел конфликтов");

            if (sat.Status == SatStatus.Unsatisfiable)
                return Result(SmtStatus.Unsatisfiable);

            var literals = encoder.Atoms
                .Select(a => new LinearArithmeticTheory.Literal(a.Atom, a.Variable, sat[a.Variable]))
                .ToList();

            checks++;
            (LinearArithmeticTheory.Verdict verdict, double[]? values) =
                LinearArithmeticTheory.Check(literals, model.NumericVariables, options);

            if (verdict == LinearArithmeticTheory.Verdict.Unknown)
                return Result(SmtStatus.Unknown, reason: "проверка арифметики исчерпала предел итераций или ветвления");

            if (verdict == LinearArithmeticTheory.Verdict.Consistent)
            {
                var booleans = new bool[model.BooleanVariables.Count];

                foreach (BooleanVariable variable in model.BooleanVariables)
                    booleans[variable.Index] = encoder.Booleans.TryGetValue(variable, out int literal) && sat[literal];

                return Result(SmtStatus.Satisfiable, booleans, values);
            }

            List<LinearArithmeticTheory.Literal> core =
                LinearArithmeticTheory.Explain(literals, model.NumericVariables, options, ref checks);

            // Пустое ядро: противоречивы сами границы переменных, и никакая подстановка не поможет
            if (core.Count == 0)
                return Result(SmtStatus.Unsatisfiable);

            encoder.Formula.AddClause(core.Select(l => l.Positive ? -l.Variable : l.Variable).ToArray());
            lemmas++;
        }
    }
}
