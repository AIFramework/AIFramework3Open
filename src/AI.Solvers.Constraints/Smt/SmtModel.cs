namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Задача SMT: переменные и утверждения, которые должны выполняться одновременно
/// </summary>
/// <example>
/// <code>
/// var model = new SmtModel("Расписание");
/// NumericVariable a = model.Int("a", 0, 100);
/// NumericVariable b = model.Int("b", 0, 100);
///
/// // Две работы на одном станке: одна раньше другой
/// model.Assert((a + 3 &lt;= b) | (b + 4 &lt;= a));
/// model.Assert((a + 3 &lt;= 7) &amp; (b + 4 &lt;= 7));
///
/// SmtSolution solution = SmtSolver.Solve(model);
/// </code>
/// </example>
public sealed class SmtModel
{
    private readonly List<NumericVariable> _numbers = [];
    private readonly List<BooleanVariable> _booleans = [];
    private readonly List<SmtFormula> _assertions = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    /// <summary>Создаёт задачу</summary>
    /// <param name="name">Имя — попадает в разбор результата</param>
    public SmtModel(string name = "Задача SMT") => Name = name;

    /// <summary>Имя задачи</summary>
    public string Name { get; }

    /// <summary>Числовые переменные</summary>
    public IReadOnlyList<NumericVariable> NumericVariables => _numbers;

    /// <summary>Булевы переменные</summary>
    public IReadOnlyList<BooleanVariable> BooleanVariables => _booleans;

    /// <summary>Утверждения</summary>
    public IReadOnlyList<SmtFormula> Assertions => _assertions;

    /// <summary>Есть ли целочисленные переменные</summary>
    public bool HasIntegers => _numbers.Any(v => v.Sort == SmtSort.Integer);

    /// <summary>Объявляет целочисленную переменную</summary>
    /// <param name="name">Имя</param>
    /// <param name="lowerBound">Нижняя граница</param>
    /// <param name="upperBound">Верхняя граница</param>
    public NumericVariable Int(string name, double lowerBound = double.NegativeInfinity, double upperBound = double.PositiveInfinity)
        => AddNumber(name, SmtSort.Integer, lowerBound, upperBound);

    /// <summary>Объявляет вещественную переменную</summary>
    /// <param name="name">Имя</param>
    /// <param name="lowerBound">Нижняя граница</param>
    /// <param name="upperBound">Верхняя граница</param>
    public NumericVariable Real(string name, double lowerBound = double.NegativeInfinity, double upperBound = double.PositiveInfinity)
        => AddNumber(name, SmtSort.Real, lowerBound, upperBound);

    /// <summary>Объявляет булеву переменную</summary>
    /// <param name="name">Имя</param>
    public BooleanVariable Bool(string name)
    {
        RegisterName(name);

        var variable = new BooleanVariable(this, _booleans.Count, name);
        _booleans.Add(variable);

        return variable;
    }

    /// <summary>Добавляет утверждение</summary>
    /// <param name="formula">Формула, которая должна выполняться</param>
    public SmtModel Assert(SmtFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        RequireOwnership(formula);

        _assertions.Add(formula);

        return this;
    }

    /// <summary>Краткое описание задачи</summary>
    public override string ToString()
        => $"{Name}: числовых переменных {_numbers.Count}, булевых {_booleans.Count}, утверждений {_assertions.Count}";

    private NumericVariable AddNumber(string name, SmtSort sort, double lowerBound, double upperBound)
    {
        if (double.IsNaN(lowerBound) || double.IsNaN(upperBound) || lowerBound > upperBound)
            throw new ArgumentException($"У переменной «{name}» нижняя граница больше верхней или не определена", nameof(lowerBound));

        RegisterName(name);

        var variable = new NumericVariable(this, _numbers.Count, name, sort, lowerBound, upperBound);
        _numbers.Add(variable);

        return variable;
    }

    private void RegisterName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_names.Add(name))
            throw new ArgumentException($"Переменная «{name}» уже объявлена", nameof(name));
    }

    private void RequireOwnership(SmtFormula formula)
    {
        switch (formula.Kind)
        {
            case FormulaKind.Variable when !ReferenceEquals(formula.Variable!.Owner, this):
            case FormulaKind.Atom when !ReferenceEquals(formula.Atom!.Terms[0].Variable.Owner, this):
                throw new ArgumentException("Формула использует переменную другой задачи", nameof(formula));
        }

        foreach (SmtFormula child in formula.Children)
            RequireOwnership(child);
    }
}
