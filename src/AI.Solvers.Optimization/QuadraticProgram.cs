using AI.DataStructs.Algebraic;

namespace AI.Solvers.Optimization;

/// <summary>Линейное ограничение квадратичной задачи: <c>a·x ≤ b</c> или <c>a·x = b</c></summary>
public sealed class QpConstraint
{
    internal QpConstraint(string name, double[] row, double rightHandSide, bool isEquality)
    {
        Name = name;
        Row = row;
        RightHandSide = rightHandSide;
        IsEquality = isEquality;
    }

    /// <summary>Имя ограничения</summary>
    public string Name { get; }

    /// <summary>Коэффициенты при переменных</summary>
    public Vector Coefficients => new((double[])Row.Clone());

    /// <summary>Правая часть</summary>
    public double RightHandSide { get; }

    /// <summary>Равенство ли это; иначе неравенство «не больше»</summary>
    public bool IsEquality { get; }

    internal double[] Row { get; }

    /// <summary>Запись ограничения</summary>
    public override string ToString() => $"{Name}: a·x {(IsEquality ? "=" : "<=")} {RightHandSide}";
}

/// <summary>
/// Задача выпуклого квадратичного программирования:
/// <c>min ½·xᵀHx + gᵀx</c> при линейных равенствах и неравенствах.
/// </summary>
/// <remarks>
/// Гессиан H симметричен и положительно определён: задача строго выпукла и имеет единственное
/// решение. Так устроены задачи MPC, регрессии со штрафом и портфели Марковица. Линейную задачу
/// (H = 0) решайте <see cref="LpSolver"/>.
/// </remarks>
public sealed class QuadraticProgram
{
    private readonly double[,] _hessian;
    private readonly double[] _linear;
    private readonly List<QpConstraint> _constraints = [];

    /// <summary>Создаёт задачу</summary>
    /// <param name="hessian">Симметричная положительно определённая матрица H</param>
    /// <param name="linear">Вектор g линейной части</param>
    /// <param name="name">Имя задачи — попадает в разбор результата</param>
    public QuadraticProgram(Matrix hessian, Vector linear, string name = "Задача квадратичного программирования")
    {
        ArgumentNullException.ThrowIfNull(hessian);
        ArgumentNullException.ThrowIfNull(linear);

        int n = linear.Count;

        if (n == 0)
            throw new ArgumentException("Нужна хотя бы одна переменная", nameof(linear));

        if (hessian.Height != n || hessian.Width != n)
            throw new ArgumentException($"Гессиан должен быть {n}×{n} по числу переменных", nameof(hessian));

        _hessian = new double[n, n];
        _linear = new double[n];

        for (int i = 0; i < n; i++)
        {
            _linear[i] = RequireFinite(linear[i], nameof(linear));

            for (int j = 0; j < n; j++)
            {
                double value = RequireFinite(hessian[i, j], nameof(hessian));
                double mirror = hessian[j, i];

                if (Math.Abs(value - mirror) > 1e-9 * Math.Max(1, Math.Abs(value)))
                    throw new ArgumentException($"Гессиан несимметричен: H[{i},{j}] ≠ H[{j},{i}]", nameof(hessian));

                _hessian[i, j] = 0.5 * (value + mirror);
            }
        }

        Name = name;
    }

    /// <summary>Имя задачи</summary>
    public string Name { get; }

    /// <summary>Число переменных</summary>
    public int VariableCount => _linear.Length;

    /// <summary>Ограничения в порядке добавления</summary>
    public IReadOnlyList<QpConstraint> Constraints => _constraints;

    /// <summary>Гессиан H</summary>
    public Matrix Hessian => new((double[,])_hessian.Clone());

    /// <summary>Линейная часть g</summary>
    public Vector Linear => new((double[])_linear.Clone());

    internal double[,] HessianArray => _hessian;

    internal double[] LinearArray => _linear;

    /// <summary>Добавляет неравенство <c>a·x ≤ b</c></summary>
    /// <param name="coefficients">Коэффициенты a</param>
    /// <param name="rightHandSide">Правая часть b</param>
    /// <param name="name">Имя ограничения</param>
    public QpConstraint AddInequality(Vector coefficients, double rightHandSide, string? name = null)
        => Add(coefficients, rightHandSide, false, name);

    /// <summary>Добавляет равенство <c>a·x = b</c></summary>
    /// <param name="coefficients">Коэффициенты a</param>
    /// <param name="rightHandSide">Правая часть b</param>
    /// <param name="name">Имя ограничения</param>
    public QpConstraint AddEquality(Vector coefficients, double rightHandSide, string? name = null)
        => Add(coefficients, rightHandSide, true, name);

    /// <summary>Границы переменной; бесконечная граница не добавляется</summary>
    /// <param name="variable">Номер переменной</param>
    /// <param name="lower">Нижняя граница</param>
    /// <param name="upper">Верхняя граница</param>
    public void AddBounds(int variable, double lower, double upper)
    {
        if (variable < 0 || variable >= VariableCount)
            throw new ArgumentOutOfRangeException(nameof(variable));

        if (double.IsNaN(lower) || double.IsNaN(upper) || lower > upper)
            throw new ArgumentException($"Нижняя граница переменной {variable} больше верхней", nameof(lower));

        if (!double.IsNegativeInfinity(lower))
        {
            var row = new double[VariableCount];
            row[variable] = -1;
            _constraints.Add(new QpConstraint($"x{variable} ≥ {lower}", row, -lower, false));
        }

        if (!double.IsPositiveInfinity(upper))
        {
            var row = new double[VariableCount];
            row[variable] = 1;
            _constraints.Add(new QpConstraint($"x{variable} ≤ {upper}", row, upper, false));
        }
    }

    /// <summary>Значение целевой функции в точке</summary>
    /// <param name="point">Точка</param>
    public double Evaluate(Vector point)
    {
        ArgumentNullException.ThrowIfNull(point);

        return Evaluate(point.ToArray());
    }

    internal double Evaluate(double[] x)
    {
        double value = 0;

        for (int i = 0; i < x.Length; i++)
        {
            double row = 0;

            for (int j = 0; j < x.Length; j++)
                row += _hessian[i, j] * x[j];

            value += (0.5 * x[i] * row) + (_linear[i] * x[i]);
        }

        return value;
    }

    /// <summary>Краткое описание</summary>
    public override string ToString()
        => $"{Name}: переменных {VariableCount}, равенств {_constraints.Count(c => c.IsEquality)}, "
            + $"неравенств {_constraints.Count(c => !c.IsEquality)}";

    private QpConstraint Add(Vector coefficients, double rightHandSide, bool equality, string? name)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        if (coefficients.Count != VariableCount)
            throw new ArgumentException($"Ожидается {VariableCount} коэффициентов", nameof(coefficients));

        double[] row = coefficients.ToArray().Select(c => RequireFinite(c, nameof(coefficients))).ToArray();
        var constraint = new QpConstraint(name ?? $"c{_constraints.Count + 1}", row, RequireFinite(rightHandSide, nameof(rightHandSide)), equality);
        _constraints.Add(constraint);

        return constraint;
    }

    private static double RequireFinite(double value, string parameter)
        => double.IsFinite(value) ? value : throw new ArgumentException("Коэффициенты должны быть конечными числами", parameter);
}
