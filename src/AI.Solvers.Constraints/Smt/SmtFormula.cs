namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Формула SMT: булева структура над булевыми переменными и арифметическими атомами.
/// </summary>
/// <remarks>
/// Строится операторами <c>&amp;</c>, <c>|</c>, <c>!</c> и сравнениями линейных выражений,
/// например <c>(x + y &lt;= 10) | p</c>. Импликация, эквивалентность и равенство выражений —
/// статические методы. Формула неизменяема.
/// </remarks>
public sealed class SmtFormula
{
    private static readonly SmtFormula TrueFormula = new(FormulaKind.Constant, [], null, null, true);
    private static readonly SmtFormula FalseFormula = new(FormulaKind.Constant, [], null, null, false);

    private SmtFormula(
        FormulaKind kind,
        IReadOnlyList<SmtFormula> children,
        BooleanVariable? variable,
        ArithmeticAtom? atom,
        bool constant)
    {
        Kind = kind;
        Children = children;
        Variable = variable;
        Atom = atom;
        ConstantValue = constant;
    }

    /// <summary>Истина</summary>
    public static SmtFormula True => TrueFormula;

    /// <summary>Ложь</summary>
    public static SmtFormula False => FalseFormula;

    /// <summary>Вид узла</summary>
    public FormulaKind Kind { get; }

    /// <summary>Подформулы</summary>
    public IReadOnlyList<SmtFormula> Children { get; }

    /// <summary>Булева переменная — у узла вида <see cref="FormulaKind.Variable"/></summary>
    public BooleanVariable? Variable { get; }

    /// <summary>Атом — у узла вида <see cref="FormulaKind.Atom"/></summary>
    public ArithmeticAtom? Atom { get; }

    /// <summary>Значение — у узла вида <see cref="FormulaKind.Constant"/></summary>
    public bool ConstantValue { get; }

    /// <summary>Конъюнкция</summary>
    /// <param name="left">Первая формула</param>
    /// <param name="right">Вторая формула</param>
    public static SmtFormula operator &(SmtFormula left, SmtFormula right) => And(left, right);

    /// <summary>Дизъюнкция</summary>
    /// <param name="left">Первая формула</param>
    /// <param name="right">Вторая формула</param>
    public static SmtFormula operator |(SmtFormula left, SmtFormula right) => Or(left, right);

    /// <summary>Отрицание</summary>
    /// <param name="formula">Формула</param>
    public static SmtFormula operator !(SmtFormula formula) => Not(formula);

    /// <summary>Конъюнкция нескольких формул; пустая — истина</summary>
    /// <param name="items">Формулы</param>
    public static SmtFormula And(params SmtFormula[] items) => Junction(FormulaKind.And, items);

    /// <summary>Дизъюнкция нескольких формул; пустая — ложь</summary>
    /// <param name="items">Формулы</param>
    public static SmtFormula Or(params SmtFormula[] items) => Junction(FormulaKind.Or, items);

    /// <summary>Отрицание</summary>
    /// <param name="formula">Формула</param>
    public static SmtFormula Not(SmtFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        return formula.Kind switch
        {
            FormulaKind.Constant => formula.ConstantValue ? False : True,
            FormulaKind.Not => formula.Children[0],
            _ => new SmtFormula(FormulaKind.Not, [formula], null, null, false),
        };
    }

    /// <summary>Импликация <c>premise → conclusion</c></summary>
    /// <param name="premise">Посылка</param>
    /// <param name="conclusion">Следствие</param>
    public static SmtFormula Implies(SmtFormula premise, SmtFormula conclusion) => Or(Not(premise), conclusion);

    /// <summary>Эквивалентность</summary>
    /// <param name="left">Первая формула</param>
    /// <param name="right">Вторая формула</param>
    public static SmtFormula Iff(SmtFormula left, SmtFormula right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new SmtFormula(FormulaKind.Iff, [left, right], null, null, false);
    }

    /// <summary>Равенство выражений: <c>left ≤ right</c> и <c>left ≥ right</c></summary>
    /// <param name="left">Левая часть</param>
    /// <param name="right">Правая часть</param>
    public static SmtFormula Equal(LinearExpression left, LinearExpression right) => And(left <= right, left >= right);

    /// <summary>Неравенство выражений</summary>
    /// <param name="left">Левая часть</param>
    /// <param name="right">Правая часть</param>
    public static SmtFormula Distinct(LinearExpression left, LinearExpression right) => Not(Equal(left, right));

    /// <summary>Запись формулы</summary>
    public override string ToString() => Kind switch
    {
        FormulaKind.Constant => ConstantValue ? "истина" : "ложь",
        FormulaKind.Variable => Variable!.Name,
        FormulaKind.Atom => Atom!.ToString(),
        FormulaKind.Not => $"¬{Wrap(Children[0])}",
        FormulaKind.And => string.Join(" ∧ ", Children.Select(Wrap)),
        FormulaKind.Or => string.Join(" ∨ ", Children.Select(Wrap)),
        _ => $"{Wrap(Children[0])} ↔ {Wrap(Children[1])}",
    };

    internal static SmtFormula Of(BooleanVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        return new SmtFormula(FormulaKind.Variable, [], variable, null, false);
    }

    /// <summary>
    /// Атом <c>difference ≤ 0</c> или <c>&lt; 0</c> в каноническом виде; атом без переменных
    /// сразу вычисляется в истину или ложь
    /// </summary>
    internal static SmtFormula Compare(LinearExpression difference, bool strict)
    {
        (NumericVariable Variable, double Coefficient)[] terms = difference.Terms
            .Where(t => t.Value != 0)
            .OrderBy(t => t.Key.Index)
            .Select(t => (t.Key, t.Value))
            .ToArray();

        double bound = -difference.Constant;

        if (terms.Length == 0)
            return (strict ? 0 < bound : 0 <= bound) ? True : False;

        SmtModel owner = terms[0].Variable.Owner;

        if (terms.Any(t => !ReferenceEquals(t.Variable.Owner, owner)))
            throw new ArgumentException("Переменные атома принадлежат разным задачам", nameof(difference));

        // Отрицательный первый коэффициент: атом записывается как отрицание своей противоположности,
        // и оба делят одну булеву переменную
        if (terms[0].Coefficient < 0)
        {
            var negated = terms.Select(t => (t.Variable, -t.Coefficient)).ToArray();

            return Not(new SmtFormula(FormulaKind.Atom, [], null, new ArithmeticAtom(negated, !strict, -bound), false));
        }

        return new SmtFormula(FormulaKind.Atom, [], null, new ArithmeticAtom(terms, strict, bound), false);
    }

    private static SmtFormula Junction(FormulaKind kind, SmtFormula[] items)
    {
        ArgumentNullException.ThrowIfNull(items);

        bool absorbing = kind == FormulaKind.Or;
        var children = new List<SmtFormula>(items.Length);

        foreach (SmtFormula item in items)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.Kind == FormulaKind.Constant)
            {
                // Истина поглощает дизъюнкцию, ложь — конъюнкцию; нейтральные константы отбрасываются
                if (item.ConstantValue == absorbing)
                    return item;

                continue;
            }

            if (item.Kind == kind)
                children.AddRange(item.Children);
            else
                children.Add(item);
        }

        return children.Count switch
        {
            0 => absorbing ? False : True,
            1 => children[0],
            _ => new SmtFormula(kind, children, null, null, false),
        };
    }

    private static string Wrap(SmtFormula formula)
        => formula.Kind is FormulaKind.And or FormulaKind.Or or FormulaKind.Iff ? $"({formula})" : formula.ToString();
}
