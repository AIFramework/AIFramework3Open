using AI.DataStructs.Algebraic;

namespace AI.Solvers.Optimization;

/// <summary>Направление оптимизации</summary>
public enum ObjectiveSense
{
    /// <summary>Минимизировать</summary>
    Minimize,

    /// <summary>Максимизировать</summary>
    Maximize
}

/// <summary>Знак ограничения</summary>
public enum ConstraintSign
{
    /// <summary>Левая часть не превышает правую</summary>
    LessOrEqual,

    /// <summary>Левая часть не меньше правой</summary>
    GreaterOrEqual,

    /// <summary>Равенство</summary>
    Equal
}

/// <summary>Область значений переменной</summary>
public enum VariableDomain
{
    /// <summary>Непрерывная</summary>
    Continuous,

    /// <summary>Целочисленная</summary>
    Integer,

    /// <summary>Булева: ноль или единица</summary>
    Binary
}

/// <summary>
/// Переменная задачи: имя, границы и область значений
/// </summary>
public sealed class Variable
{
    /// <summary>Порядковый номер переменной в задаче</summary>
    public int Index { get; }

    /// <summary>Имя переменной — попадает в решение и в разбор результата</summary>
    public string Name { get; }

    /// <summary>Нижняя граница; по умолчанию нуль</summary>
    public double LowerBound { get; }

    /// <summary>Верхняя граница; <see cref="double.PositiveInfinity"/> означает отсутствие</summary>
    public double UpperBound { get; }

    /// <summary>Область значений</summary>
    public VariableDomain Domain { get; }

    internal Variable(int index, string name, double lowerBound, double upperBound, VariableDomain domain)
    {
        if (lowerBound > upperBound)
            throw new ArgumentException($"У переменной «{name}» нижняя граница больше верхней", nameof(lowerBound));

        Index = index;
        Name = name;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        Domain = domain;
    }

    /// <summary>Требует ли переменная целых значений</summary>
    public bool IsIntegral => Domain != VariableDomain.Continuous;

    /// <summary>Имя переменной</summary>
    public override string ToString() => Name;
}

/// <summary>
/// Линейное ограничение вида <c>a·x ⋛ b</c>
/// </summary>
public sealed class Constraint
{
    /// <summary>Имя ограничения</summary>
    public string Name { get; }

    /// <summary>Коэффициенты при переменных</summary>
    public Vector Coefficients { get; }

    /// <summary>Знак</summary>
    public ConstraintSign Sign { get; }

    /// <summary>Правая часть</summary>
    public double RightHandSide { get; }

    internal Constraint(string name, Vector coefficients, ConstraintSign sign, double rightHandSide)
    {
        Name = name;
        Coefficients = coefficients;
        Sign = sign;
        RightHandSide = rightHandSide;
    }

    /// <summary>Запись ограничения</summary>
    public override string ToString()
    {
        string sign = Sign switch
        {
            ConstraintSign.LessOrEqual => "<=",
            ConstraintSign.GreaterOrEqual => ">=",
            _ => "="
        };

        return $"{Name}: a·x {sign} {RightHandSide}";
    }
}

/// <summary>
/// Задача линейного или смешанно-целочисленного программирования.
/// </summary>
/// <remarks>
/// <para>
/// Модель собирается по частям: переменные объявляются с границами и областью значений,
/// ограничения задаются вектором коэффициентов. Решение — <see cref="LpSolver.Solve"/>:
/// без целочисленных переменных работает симплекс-метод, с ними — метод ветвей и границ
/// поверх него.
/// </para>
/// <para>
/// Коэффициенты хранятся плотно. Задача на тысячи переменных с редкой матрицей будет
/// решаться, но неэффективно: разреженное хранение и метод внутренней точки — отдельная работа.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var lp = new LinearProgram(ObjectiveSense.Maximize);
/// Variable x = lp.AddVariable("x", 0, 4);
/// Variable y = lp.AddVariable("y", 0, 6);
///
/// lp.SetObjective(new Vector(3.0, 5.0));
/// lp.AddConstraint(new Vector(3.0, 2.0), ConstraintSign.LessOrEqual, 18, "цех");
///
/// LpSolution solution = LpSolver.Solve(lp);
/// double profit = solution.Objective;
/// double madeX = solution[x];
/// </code>
/// </example>
public sealed class LinearProgram
{
    private readonly List<Variable> _variables = [];
    private readonly List<Constraint> _constraints = [];
    private double[] _objective = [];

    /// <summary>Создаёт задачу</summary>
    /// <param name="sense">Направление оптимизации</param>
    /// <param name="name">Имя задачи — попадает в разбор результата</param>
    public LinearProgram(ObjectiveSense sense = ObjectiveSense.Minimize, string name = "Задача линейного программирования")
    {
        Sense = sense;
        Name = name;
    }

    /// <summary>Направление оптимизации</summary>
    public ObjectiveSense Sense { get; }

    /// <summary>Имя задачи</summary>
    public string Name { get; }

    /// <summary>Переменные в порядке объявления</summary>
    public IReadOnlyList<Variable> Variables => _variables;

    /// <summary>Ограничения в порядке объявления</summary>
    public IReadOnlyList<Constraint> Constraints => _constraints;

    /// <summary>Есть ли в задаче целочисленные переменные</summary>
    public bool IsMixedInteger => _variables.Any(v => v.IsIntegral);

    /// <summary>Коэффициенты целевой функции</summary>
    public Vector Objective => new(_objective);

    #region Построение

    /// <summary>
    /// Добавляет непрерывную переменную
    /// </summary>
    /// <param name="name">Имя</param>
    /// <param name="lowerBound">Нижняя граница</param>
    /// <param name="upperBound">Верхняя граница</param>
    public Variable AddVariable(string name, double lowerBound = 0, double upperBound = double.PositiveInfinity)
        => Add(name, lowerBound, upperBound, VariableDomain.Continuous);

    /// <summary>
    /// Добавляет целочисленную переменную
    /// </summary>
    /// <param name="name">Имя</param>
    /// <param name="lowerBound">Нижняя граница</param>
    /// <param name="upperBound">Верхняя граница</param>
    public Variable AddIntegerVariable(string name, double lowerBound = 0, double upperBound = double.PositiveInfinity)
        => Add(name, lowerBound, upperBound, VariableDomain.Integer);

    /// <summary>
    /// Добавляет булеву переменную
    /// </summary>
    /// <param name="name">Имя</param>
    public Variable AddBinaryVariable(string name) => Add(name, 0, 1, VariableDomain.Binary);

    /// <summary>
    /// Добавляет свободную переменную без границ
    /// </summary>
    /// <param name="name">Имя</param>
    public Variable AddFreeVariable(string name)
        => Add(name, double.NegativeInfinity, double.PositiveInfinity, VariableDomain.Continuous);

    private Variable Add(string name, double lowerBound, double upperBound, VariableDomain domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_variables.Any(v => v.Name == name))
            throw new ArgumentException($"Переменная «{name}» уже объявлена", nameof(name));

        var variable = new Variable(_variables.Count, name, lowerBound, upperBound, domain);
        _variables.Add(variable);

        return variable;
    }

    /// <summary>
    /// Задаёт целевую функцию
    /// </summary>
    /// <param name="coefficients">Коэффициенты по переменным в порядке объявления</param>
    public void SetObjective(Vector coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        EnsureLength(coefficients.Count, nameof(coefficients));

        _objective = coefficients.ToArray();
    }

    /// <summary>
    /// Задаёт коэффициент целевой функции при одной переменной
    /// </summary>
    /// <param name="variable">Переменная</param>
    /// <param name="coefficient">Коэффициент</param>
    public void SetObjective(Variable variable, double coefficient)
    {
        ArgumentNullException.ThrowIfNull(variable);

        if (_objective.Length != _variables.Count)
            Array.Resize(ref _objective, _variables.Count);

        _objective[variable.Index] = coefficient;
    }

    /// <summary>
    /// Добавляет ограничение
    /// </summary>
    /// <param name="coefficients">Коэффициенты по переменным в порядке объявления</param>
    /// <param name="sign">Знак</param>
    /// <param name="rightHandSide">Правая часть</param>
    /// <param name="name">Имя ограничения</param>
    public Constraint AddConstraint(Vector coefficients, ConstraintSign sign, double rightHandSide, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        EnsureLength(coefficients.Count, nameof(coefficients));

        var constraint = new Constraint(
            name ?? $"c{_constraints.Count + 1}",
            new Vector(coefficients.ToArray()),
            sign,
            rightHandSide);

        _constraints.Add(constraint);

        return constraint;
    }

    private void EnsureLength(int length, string parameter)
    {
        if (length != _variables.Count)
            throw new ArgumentException(
                $"Ожидается {_variables.Count} коэффициентов по числу переменных, получено {length}", parameter);
    }

    #endregion

    /// <summary>Коэффициент целевой функции при переменной</summary>
    /// <param name="variable">Переменная</param>
    internal double ObjectiveCoefficient(Variable variable)
        => variable.Index < _objective.Length ? _objective[variable.Index] : 0.0;

    /// <summary>Краткое описание задачи</summary>
    public override string ToString()
        => $"{Name}: {(Sense == ObjectiveSense.Minimize ? "min" : "max")}, "
            + $"переменных {_variables.Count} (целых {_variables.Count(v => v.IsIntegral)}), "
            + $"ограничений {_constraints.Count}";
}
