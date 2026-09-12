using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;

namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Теория линейной арифметики: совместна ли система атомов и какое её подмножество противоречиво
/// </summary>
/// <remarks>
/// <para>
/// Совместность проверяет готовый <see cref="LpSolver"/>: симплекс для вещественных переменных,
/// ветви и границы — для целых. Строгие неравенства <c>a·x &lt; b</c> записываются как
/// <c>a·x + δ ≤ b</c> с общим запасом <c>0 ≤ δ ≤ 1</c>, который максимизируется: система совместна,
/// только если наибольший запас положителен. Так строгость проверяется точно, без выбора
/// «достаточно малого» ε.
/// </para>
/// <para>
/// Противоречие объясняется фильтром удаления: литералы по одному выбрасываются, пока остаток
/// остаётся несовместным. Получается минимальное по включению противоречивое подмножество —
/// чем оно меньше, тем больше булевых подстановок запрещает одна лемма.
/// </para>
/// </remarks>
internal static class LinearArithmeticTheory
{
    private const string MarginName = "δ — запас строгих неравенств";

    /// <summary>Атом с полярностью, как его назначил булев решатель</summary>
    internal readonly record struct Literal(ArithmeticAtom Atom, int Variable, bool Positive)
    {
        /// <summary>Строго ли неравенство с учётом полярности: ¬(a ≤ b) — это a &gt; b</summary>
        public bool IsStrict => Positive ? Atom.IsStrict : !Atom.IsStrict;
    }

    /// <summary>Вердикт проверки</summary>
    internal enum Verdict
    {
        Consistent,
        Conflict,
        Unknown
    }

    /// <summary>Проверяет совместность набора литералов; при совместности возвращает значения переменных</summary>
    internal static (Verdict Verdict, double[]? Values) Check(
        IReadOnlyList<Literal> literals, IReadOnlyList<NumericVariable> variables, SmtOptions options)
    {
        var program = new LinearProgram(ObjectiveSense.Maximize, "Проверка арифметики");
        var columns = new Variable[variables.Count];

        foreach (NumericVariable variable in variables)
        {
            columns[variable.Index] = variable.Sort == SmtSort.Integer
                ? program.AddIntegerVariable(variable.Name, variable.LowerBound, variable.UpperBound)
                : program.AddVariable(variable.Name, variable.LowerBound, variable.UpperBound);
        }

        Variable? margin = literals.Any(l => l.IsStrict) ? program.AddVariable(MarginName, 0, 1) : null;
        int width = program.Variables.Count;

        if (margin is not null)
            program.SetObjective(margin, 1);

        foreach (Literal literal in literals)
        {
            // ¬(a·x ≤ b) — это a·x > b, то есть −a·x < −b
            double sign = literal.Positive ? 1 : -1;
            var coefficients = new double[width];

            foreach ((NumericVariable variable, double coefficient) in literal.Atom.Terms)
                coefficients[variable.Index] += sign * coefficient;

            if (literal.IsStrict)
                coefficients[margin!.Index] = 1;

            program.AddConstraint(new Vector(coefficients), ConstraintSign.LessOrEqual, sign * literal.Atom.Bound);
        }

        LpSolution solution = LpSolver.Solve(program, options.Lp);

        switch (solution.Status)
        {
            case SolverStatus.Infeasible:
                return (Verdict.Conflict, null);

            case SolverStatus.Optimal:
                if (margin is not null && solution[margin] <= options.StrictMargin)
                    return (Verdict.Conflict, null);

                var values = new double[variables.Count];

                foreach (NumericVariable variable in variables)
                {
                    double value = solution[columns[variable.Index]];
                    values[variable.Index] = variable.Sort == SmtSort.Integer ? Math.Round(value) : value;
                }

                return (Verdict.Consistent, values);

            default:
                // Предел итераций или ветвления, а неограниченности при ограниченной цели быть не может
                return (Verdict.Unknown, null);
        }
    }

    /// <summary>
    /// Минимальное по включению противоречивое подмножество несовместного набора литералов
    /// </summary>
    internal static List<Literal> Explain(
        IReadOnlyList<Literal> literals, IReadOnlyList<NumericVariable> variables, SmtOptions options, ref int checks)
    {
        var core = literals.ToList();

        for (int i = core.Count - 1; i >= 0; i--)
        {
            var candidate = new List<Literal>(core);
            candidate.RemoveAt(i);
            checks++;

            // Литерал выбрасывается, только если без него противоречие доказано; при неизвестном
            // исходе он остаётся — ядро от этого не перестаёт быть противоречивым
            if (Check(candidate, variables, options).Verdict == Verdict.Conflict)
                core = candidate;
        }

        return core;
    }
}
