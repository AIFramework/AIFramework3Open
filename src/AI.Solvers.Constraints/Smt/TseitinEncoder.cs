using AI.Solvers.Constraints.Sat;

namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Перевод формулы в КНФ преобразованием Цейтина: каждая составная подформула получает
/// свою переменную, связанную с подформулами несколькими дизъюнктами
/// </summary>
/// <remarks>
/// Размер КНФ линеен по размеру формулы — в отличие от раскрытия скобок, которое раздувает
/// её экспоненциально. Одинаковые атомы делят одну переменную, одинаковые узлы — тоже.
/// </remarks>
internal sealed class TseitinEncoder
{
    private readonly Dictionary<string, int> _atomVariables = new(StringComparer.Ordinal);
    private readonly Dictionary<BooleanVariable, int> _booleanVariables = new();
    private readonly Dictionary<SmtFormula, int> _nodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<(ArithmeticAtom Atom, int Variable)> _atoms = [];
    private int _trueVariable;

    /// <summary>Получившаяся КНФ; к ней дописываются леммы теории</summary>
    public CnfFormula Formula { get; } = new();

    /// <summary>Арифметические атомы и их переменные</summary>
    public IReadOnlyList<(ArithmeticAtom Atom, int Variable)> Atoms => _atoms;

    /// <summary>Булевы переменные задачи и их переменные в КНФ</summary>
    public IReadOnlyDictionary<BooleanVariable, int> Booleans => _booleanVariables;

    /// <summary>Добавляет утверждение</summary>
    public void Assert(SmtFormula formula)
    {
        // Конъюнкция верхнего уровня — это несколько утверждений, лишняя переменная ей не нужна
        if (formula.Kind == FormulaKind.And)
        {
            foreach (SmtFormula child in formula.Children)
                Assert(child);

            return;
        }

        Formula.AddClause(Literal(formula));
    }

    private int Literal(SmtFormula formula)
    {
        switch (formula.Kind)
        {
            case FormulaKind.Constant:
                return formula.ConstantValue ? TrueLiteral() : -TrueLiteral();

            case FormulaKind.Variable:
                return BooleanLiteral(formula.Variable!);

            case FormulaKind.Atom:
                return AtomLiteral(formula.Atom!);

            case FormulaKind.Not:
                return -Literal(formula.Children[0]);
        }

        if (_nodes.TryGetValue(formula, out int known))
            return known;

        int[] children = formula.Children.Select(Literal).ToArray();
        int node = Formula.AddVariable();

        switch (formula.Kind)
        {
            case FormulaKind.And:
                foreach (int child in children)
                    Formula.AddClause(-node, child);

                Formula.AddClause([node, .. children.Select(c => -c)]);
                break;

            case FormulaKind.Or:
                foreach (int child in children)
                    Formula.AddClause(node, -child);

                Formula.AddClause([-node, .. children]);
                break;

            default:
                int a = children[0];
                int b = children[1];
                Formula.AddClause(-node, -a, b);
                Formula.AddClause(-node, a, -b);
                Formula.AddClause(node, a, b);
                Formula.AddClause(node, -a, -b);
                break;
        }

        _nodes[formula] = node;

        return node;
    }

    private int TrueLiteral()
    {
        if (_trueVariable == 0)
        {
            _trueVariable = Formula.AddVariable();
            Formula.AddClause(_trueVariable);
        }

        return _trueVariable;
    }

    private int BooleanLiteral(BooleanVariable variable)
    {
        if (!_booleanVariables.TryGetValue(variable, out int literal))
        {
            literal = Formula.AddVariable();
            _booleanVariables[variable] = literal;
        }

        return literal;
    }

    private int AtomLiteral(ArithmeticAtom atom)
    {
        if (!_atomVariables.TryGetValue(atom.Key, out int literal))
        {
            literal = Formula.AddVariable();
            _atomVariables[atom.Key] = literal;
            _atoms.Add((atom, literal));
        }

        return literal;
    }
}
