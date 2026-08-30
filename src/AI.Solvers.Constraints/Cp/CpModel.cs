namespace AI.Solvers.Constraints.Cp;

/// <summary>
/// Переменная с конечной областью значений
/// </summary>
public sealed class IntVariable
{
    internal IntVariable(int index, string name, int lower, int upper)
    {
        if (upper < lower)
            throw new ArgumentException($"У переменной «{name}» верхняя граница меньше нижней", nameof(upper));

        Index = index;
        Name = name;
        Lower = lower;
        Upper = upper;
    }

    /// <summary>Порядковый номер в модели</summary>
    public int Index { get; }

    /// <summary>Имя</summary>
    public string Name { get; }

    /// <summary>Наименьшее возможное значение</summary>
    public int Lower { get; }

    /// <summary>Наибольшее возможное значение</summary>
    public int Upper { get; }

    /// <summary>Размер исходной области значений</summary>
    public int Size => Upper - Lower + 1;

    /// <summary>Имя переменной</summary>
    public override string ToString() => Name;
}

/// <summary>Знак линейного ограничения</summary>
public enum LinearRelation
{
    /// <summary>Сумма не превышает правую часть</summary>
    LessOrEqual,

    /// <summary>Сумма равна правой части</summary>
    Equal,

    /// <summary>Сумма не меньше правой части</summary>
    GreaterOrEqual
}

/// <summary>
/// Ограничение над переменными с конечными областями
/// </summary>
public interface IConstraint
{
    /// <summary>Переменные, которых касается ограничение</summary>
    IReadOnlyList<IntVariable> Scope { get; }

    /// <summary>
    /// Сужает области значений. Возвращает <c>false</c>, если ограничение стало невыполнимым.
    /// </summary>
    /// <param name="domains">Текущие области значений</param>
    bool Propagate(DomainStore domains);

    /// <summary>Название ограничения для разбора результата</summary>
    string Describe();
}

/// <summary>
/// Все переменные попарно различны
/// </summary>
/// <remarks>
/// Распространение двухступенчатое: значение зафиксированной переменной вычёркивается
/// у остальных, а затем проверяется условие Холла для подмножеств с общей областью —
/// если k переменных умещаются в объединение из менее чем k значений, решения нет.
/// Полная фильтрация по паросочетаниям здесь не делается: она дороже, а на задачах
/// вроде расстановки ферзей и судоку выигрыш от неё невелик.
/// </remarks>
public sealed class AllDifferent : IConstraint
{
    private readonly IntVariable[] _variables;

    /// <summary>Создаёт ограничение попарного различия</summary>
    /// <param name="variables">Переменные</param>
    public AllDifferent(params IntVariable[] variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        _variables = variables;
    }

    /// <inheritdoc />
    public IReadOnlyList<IntVariable> Scope => _variables;

    /// <inheritdoc />
    public bool Propagate(DomainStore domains)
    {
        foreach (IntVariable variable in _variables)
        {
            if (!domains.TryGetFixed(variable, out int value))
                continue;

            foreach (IntVariable other in _variables)
            {
                if (ReferenceEquals(other, variable))
                    continue;

                if (!domains.Remove(other, value))
                    return false;
            }
        }

        // Условие Холла в простейшем виде: суммарное число различных доступных значений
        // не может быть меньше числа переменных
        var union = new HashSet<int>();

        foreach (IntVariable variable in _variables)
            foreach (int value in domains.Values(variable))
                _ = union.Add(value);

        return union.Count >= _variables.Length;
    }

    /// <inheritdoc />
    public string Describe() => $"все различны ({_variables.Length} переменных)";
}

/// <summary>
/// Линейное ограничение <c>Σ aᵢ·xᵢ ⋛ b</c>
/// </summary>
/// <remarks>
/// Распространение по границам: для каждой переменной считаются наименьший и наибольший
/// возможный вклад остальных, из чего получается допустимый интервал самой переменной.
/// </remarks>
public sealed class LinearConstraint : IConstraint
{
    private readonly IntVariable[] _variables;
    private readonly int[] _coefficients;
    private readonly LinearRelation _relation;
    private readonly int _rightHandSide;

    /// <summary>Создаёт линейное ограничение</summary>
    /// <param name="variables">Переменные</param>
    /// <param name="coefficients">Коэффициенты</param>
    /// <param name="relation">Знак</param>
    /// <param name="rightHandSide">Правая часть</param>
    public LinearConstraint(IntVariable[] variables, int[] coefficients, LinearRelation relation, int rightHandSide)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(coefficients);

        if (variables.Length != coefficients.Length)
            throw new ArgumentException("Число переменных и коэффициентов должно совпадать", nameof(coefficients));

        _variables = variables;
        _coefficients = coefficients;
        _relation = relation;
        _rightHandSide = rightHandSide;
    }

    /// <inheritdoc />
    public IReadOnlyList<IntVariable> Scope => _variables;

    /// <inheritdoc />
    public bool Propagate(DomainStore domains)
    {
        if (_relation is LinearRelation.LessOrEqual or LinearRelation.Equal)
        {
            if (!Filter(domains, upperBound: _rightHandSide))
                return false;
        }

        if (_relation is LinearRelation.GreaterOrEqual or LinearRelation.Equal)
        {
            // Смена знака превращает «не меньше» в «не больше»
            if (!Filter(domains, upperBound: -_rightHandSide, negate: true))
                return false;
        }

        return true;
    }

    private bool Filter(DomainStore domains, int upperBound, bool negate = false)
    {
        int count = _variables.Length;
        var minimum = new int[count];
        var maximum = new int[count];
        long total = 0;

        for (int i = 0; i < count; i++)
        {
            int coefficient = negate ? -_coefficients[i] : _coefficients[i];
            int low = domains.Minimum(_variables[i]);
            int high = domains.Maximum(_variables[i]);

            minimum[i] = coefficient >= 0 ? coefficient * low : coefficient * high;
            maximum[i] = coefficient >= 0 ? coefficient * high : coefficient * low;
            total += minimum[i];
        }

        if (total > upperBound)
            return false;

        for (int i = 0; i < count; i++)
        {
            int coefficient = negate ? -_coefficients[i] : _coefficients[i];

            if (coefficient == 0)
                continue;

            long othersMinimum = total - minimum[i];
            long slack = upperBound - othersMinimum;

            if (coefficient > 0)
            {
                int limit = (int)Math.Floor(slack / (double)coefficient);

                if (!domains.RemoveAbove(_variables[i], limit))
                    return false;
            }
            else
            {
                int limit = (int)Math.Ceiling(slack / (double)coefficient);

                if (!domains.RemoveBelow(_variables[i], limit))
                    return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public string Describe()
    {
        string sign = _relation switch
        {
            LinearRelation.LessOrEqual => "<=",
            LinearRelation.GreaterOrEqual => ">=",
            _ => "="
        };

        return $"линейное: сумма {sign} {_rightHandSide}";
    }
}

/// <summary>
/// Ограничение <c>x + offset ≠ y</c>
/// </summary>
/// <remarks>
/// Смещение делает ограничение пригодным для диагоналей: расстановка ферзей выражается
/// через <c>x - y ≠ d</c> без вспомогательных переменных.
/// </remarks>
public sealed class NotEqual : IConstraint
{
    private readonly IntVariable _left;
    private readonly IntVariable _right;
    private readonly int _offset;

    /// <summary>Создаёт ограничение неравенства</summary>
    /// <param name="left">Левая переменная</param>
    /// <param name="right">Правая переменная</param>
    /// <param name="offset">Смещение: запрещается <c>left + offset = right</c></param>
    public NotEqual(IntVariable left, IntVariable right, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        _left = left;
        _right = right;
        _offset = offset;
    }

    /// <inheritdoc />
    public IReadOnlyList<IntVariable> Scope => [_left, _right];

    /// <inheritdoc />
    public bool Propagate(DomainStore domains)
    {
        if (domains.TryGetFixed(_left, out int leftValue))
            if (!domains.Remove(_right, leftValue + _offset))
                return false;

        if (domains.TryGetFixed(_right, out int rightValue))
            if (!domains.Remove(_left, rightValue - _offset))
                return false;

        return true;
    }

    /// <inheritdoc />
    public string Describe() => _offset == 0
        ? $"{_left.Name} ≠ {_right.Name}"
        : $"{_left.Name} + {_offset} ≠ {_right.Name}";
}

/// <summary>
/// Модель задачи удовлетворения ограничений
/// </summary>
/// <example>
/// <code>
/// var model = new CpModel("Восемь ферзей");
/// IntVariable[] queens = model.AddVariables("q", 8, 0, 7);
///
/// model.Add(new AllDifferent(queens));
///
/// for (int i = 0; i &lt; 8; i++)
///     for (int j = i + 1; j &lt; 8; j++)
///     {
///         model.Add(new NotEqual(queens[i], queens[j], j - i));
///         model.Add(new NotEqual(queens[i], queens[j], i - j));
///     }
///
/// CpSolution solution = CpSolver.Solve(model);
/// </code>
/// </example>
public sealed class CpModel
{
    private readonly List<IntVariable> _variables = [];
    private readonly List<IConstraint> _constraints = [];

    /// <summary>Создаёт модель</summary>
    /// <param name="name">Имя задачи</param>
    public CpModel(string name = "Задача удовлетворения ограничений") => Name = name;

    /// <summary>Имя задачи</summary>
    public string Name { get; }

    /// <summary>Переменные</summary>
    public IReadOnlyList<IntVariable> Variables => _variables;

    /// <summary>Ограничения</summary>
    public IReadOnlyList<IConstraint> Constraints => _constraints;

    /// <summary>
    /// Объявляет переменную
    /// </summary>
    /// <param name="name">Имя</param>
    /// <param name="lower">Наименьшее значение</param>
    /// <param name="upper">Наибольшее значение</param>
    public IntVariable AddVariable(string name, int lower, int upper)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var variable = new IntVariable(_variables.Count, name, lower, upper);
        _variables.Add(variable);

        return variable;
    }

    /// <summary>
    /// Объявляет набор однотипных переменных с именами вида <c>q0, q1, …</c>
    /// </summary>
    /// <param name="prefix">Приставка имени</param>
    /// <param name="count">Сколько переменных</param>
    /// <param name="lower">Наименьшее значение</param>
    /// <param name="upper">Наибольшее значение</param>
    public IntVariable[] AddVariables(string prefix, int count, int lower, int upper)
    {
        var created = new IntVariable[count];

        for (int i = 0; i < count; i++)
            created[i] = AddVariable($"{prefix}{i}", lower, upper);

        return created;
    }

    /// <summary>Добавляет ограничение</summary>
    /// <param name="constraint">Ограничение</param>
    public CpModel Add(IConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        _constraints.Add(constraint);

        return this;
    }

    /// <summary>Добавляет линейное ограничение</summary>
    /// <param name="variables">Переменные</param>
    /// <param name="coefficients">Коэффициенты</param>
    /// <param name="relation">Знак</param>
    /// <param name="rightHandSide">Правая часть</param>
    public CpModel AddLinear(IntVariable[] variables, int[] coefficients, LinearRelation relation, int rightHandSide)
        => Add(new LinearConstraint(variables, coefficients, relation, rightHandSide));

    /// <summary>Закрепляет за переменной значение</summary>
    /// <param name="variable">Переменная</param>
    /// <param name="value">Значение</param>
    public CpModel Fix(IntVariable variable, int value)
        => AddLinear([variable], [1], LinearRelation.Equal, value);

    /// <summary>Краткое описание модели</summary>
    public override string ToString()
        => $"{Name}: переменных {_variables.Count}, ограничений {_constraints.Count}";
}
