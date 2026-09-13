using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Solvers.Optimization;

/// <summary>
/// Решение задачи квадратичного программирования
/// </summary>
public sealed class QpSolution : IInterpretable
{
    private readonly double[] _values;
    private readonly double[] _multipliers;
    private readonly QuadraticProgram _program;

    internal QpSolution(
        QuadraticProgram program, SolverStatus status, double[] values, double objective,
        double[] multipliers, int[] active, int iterations)
    {
        _program = program;
        _values = values;
        _multipliers = multipliers;
        Status = status;
        Objective = objective;
        ActiveConstraints = active.Select(i => program.Constraints[i]).ToList();
        Iterations = iterations;
    }

    /// <summary>Исход решения</summary>
    public SolverStatus Status { get; }

    /// <summary>Найден ли оптимум</summary>
    public bool IsOptimal => Status == SolverStatus.Optimal;

    /// <summary>Значение целевой функции</summary>
    public double Objective { get; }

    /// <summary>Значения переменных</summary>
    public Vector Values => new((double[])_values.Clone());

    /// <summary>Значение переменной</summary>
    /// <param name="index">Номер переменной</param>
    public double this[int index] => _values[index];

    /// <summary>
    /// Множители Лагранжа по ограничениям в порядке добавления: у активных неравенств
    /// неотрицательны, у неактивных равны нулю
    /// </summary>
    /// <remarks>
    /// Множитель — теневая цена: ослабление ограничения на единицу правой части уменьшает
    /// оптимум примерно на его величину.
    /// </remarks>
    public IReadOnlyList<double> Multipliers => _multipliers;

    /// <summary>Ограничения, выполненные в решении как равенства</summary>
    public IReadOnlyList<QpConstraint> ActiveConstraints { get; }

    /// <summary>Число итераций метода активного множества</summary>
    public int Iterations { get; }

    /// <summary>Наибольшее нарушение ограничений в найденной точке</summary>
    public double MaxViolation
    {
        get
        {
            if (_values.Any(double.IsNaN))
                return double.NaN;

            double worst = 0;

            foreach (QpConstraint constraint in _program.Constraints)
            {
                double lhs = 0;

                for (int j = 0; j < _values.Length; j++)
                    lhs += constraint.Row[j] * _values[j];

                double violation = constraint.IsEquality
                    ? Math.Abs(lhs - constraint.RightHandSide)
                    : Math.Max(0, lhs - constraint.RightHandSide);

                worst = Math.Max(worst, violation);
            }

            return worst;
        }
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var binding = _program.Constraints
            .Select((c, i) => (Constraint: c, Multiplier: _multipliers[i]))
            .Where(p => Math.Abs(p.Multiplier) > 1e-9)
            .OrderByDescending(p => Math.Abs(p.Multiplier))
            .Take(5)
            .ToList();

        return new InterpretationBuilder(_program.Name)
            .Summary(Status switch
            {
                SolverStatus.Optimal =>
                    $"Оптимум найден: целевая функция {Fmt.Num(Objective, 6)}, активных ограничений "
                    + $"{ActiveConstraints.Count} из {_program.Constraints.Count}.",
                SolverStatus.Infeasible => "Допустимых точек нет: ограничения противоречат друг другу.",
                _ => $"Достигнут предел итераций; лучшая найденная точка даёт {Fmt.Num(Objective, 6)}, оптимальность не доказана."
            })
            .Metric("Исход", Status.ToString(), null, null,
                IsOptimal ? MetricQuality.Good : Status == SolverStatus.LimitReached ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Переменных", _program.VariableCount, null, null, MetricQuality.Unknown, 0)
            .Metric("Ограничений", _program.Constraints.Count, null, null, MetricQuality.Unknown, 0)
            .Metric("Активных", ActiveConstraints.Count, null, "выполнены как равенства", MetricQuality.Unknown, 0)
            .Metric("Итераций", Iterations, null, "шагов метода активного множества", MetricQuality.Unknown, 0)
            .FindingIf(binding.Count > 0,
                "Сильнее всего оптимум держат ограничения: "
                + string.Join(", ", binding.Select(b => $"{b.Constraint.Name} (λ = {Fmt.Num(b.Multiplier, 4)})"))
                + ". Ослабление ограничения на единицу снижает оптимум примерно на λ.")
            .Warning("Решатель рассчитан на строго выпуклые задачи: гессиан положительно определён. "
                + "Для полуопределённого гессиана добавьте малую регуляризацию, для линейной задачи — LpSolver.")
            .Build();
    }

    /// <summary>Краткая запись решения</summary>
    public override string ToString() => IsOptimal ? $"оптимум: {Objective:G6}" : Status.ToString();
}
