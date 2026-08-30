using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Solvers.Optimization;

/// <summary>Исход решения задачи</summary>
public enum SolverStatus
{
    /// <summary>Найден оптимум</summary>
    Optimal,

    /// <summary>Допустимых решений нет</summary>
    Infeasible,

    /// <summary>Целевая функция не ограничена на допустимом множестве</summary>
    Unbounded,

    /// <summary>Исчерпан предел итераций или узлов; лучшее найденное решение может быть не оптимальным</summary>
    LimitReached
}

/// <summary>
/// Решение задачи линейного или смешанно-целочисленного программирования
/// </summary>
public sealed class LpSolution : IInterpretable
{
    private readonly double[] _values;
    private readonly LinearProgram _program;

    internal LpSolution(
        LinearProgram program,
        SolverStatus status,
        double objective,
        double[] values,
        int iterations,
        int nodes = 0,
        double? bound = null)
    {
        _program = program;
        _values = values;

        Status = status;
        Objective = objective;
        Iterations = iterations;
        Nodes = nodes;
        Bound = bound;
    }

    /// <summary>Исход решения</summary>
    public SolverStatus Status { get; }

    /// <summary>Найден ли оптимум</summary>
    public bool IsOptimal => Status == SolverStatus.Optimal;

    /// <summary>Значение целевой функции</summary>
    public double Objective { get; }

    /// <summary>Число итераций симплекс-метода</summary>
    public int Iterations { get; }

    /// <summary>Число просмотренных узлов ветвления; нуль для непрерывной задачи</summary>
    public int Nodes { get; }

    /// <summary>
    /// Оценка снизу (для минимума) по нерешённым узлам ветвления; <c>null</c>, если дерево пройдено целиком
    /// </summary>
    public double? Bound { get; }

    /// <summary>
    /// Разрыв между найденным решением и оценкой по дереву ветвления;
    /// нуль означает доказанный оптимум
    /// </summary>
    public double Gap => Bound is null || double.IsNaN(Objective) || double.IsInfinity(Objective)
        ? 0.0
        : Math.Abs(Objective - Bound.Value) / Math.Max(1e-10, Math.Abs(Objective));

    /// <summary>Значения переменных в порядке объявления</summary>
    public Vector Values => new(_values);

    /// <summary>Значение переменной</summary>
    /// <param name="variable">Переменная</param>
    public double this[Variable variable]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(variable);
            return _values[variable.Index];
        }
    }

    /// <summary>Значение переменной по имени</summary>
    /// <param name="name">Имя переменной</param>
    public double this[string name]
    {
        get
        {
            Variable variable = _program.Variables.FirstOrDefault(v => v.Name == name)
                ?? throw new KeyNotFoundException($"Переменной «{name}» в задаче нет");

            return _values[variable.Index];
        }
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int integral = _program.Variables.Count(v => v.IsIntegral);
        bool solved = Status == SolverStatus.Optimal;

        var builder = new InterpretationBuilder(_program.Name)
            .Summary(Status switch
            {
                SolverStatus.Optimal =>
                    $"Оптимум найден: целевая функция {Fmt.Num(Objective, 4)} при "
                    + $"{_program.Variables.Count} переменных и {_program.Constraints.Count} ограничениях."
                    + (Nodes > 0 ? $" Дерево ветвления просмотрено за {Nodes} узлов." : string.Empty),
                SolverStatus.Infeasible =>
                    "Допустимых решений нет: ограничения задачи противоречат друг другу.",
                SolverStatus.Unbounded =>
                    "Целевая функция не ограничена: допустимое множество уходит в бесконечность "
                    + "в направлении улучшения. Обычно это значит, что забыто ограничение сверху.",
                _ =>
                    $"Достигнут предел работы. Лучшее найденное значение {Fmt.Num(Objective, 4)}"
                    + (Bound is not null ? $", оценка по нерешённым узлам {Fmt.Num(Bound.Value, 4)}" : string.Empty)
                    + ": оптимальность не доказана."
            })
            .Metric("Исход", StatusName(Status), null, "результат работы решателя",
                solved ? MetricQuality.Good : Status == SolverStatus.LimitReached ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Переменных", _program.Variables.Count, null,
                integral > 0 ? $"из них целочисленных {integral}" : "все непрерывные",
                MetricQuality.Unknown, 0)
            .Metric("Ограничений", _program.Constraints.Count, null, "без учёта границ переменных", MetricQuality.Unknown, 0)
            .Metric("Итераций симплекса", Iterations, null, "шагов смены базиса", MetricQuality.Unknown, 0);

        if (solved)
            builder = builder.Metric("Целевая функция", Fmt.Num(Objective, 6), null,
                _program.Sense == ObjectiveSense.Minimize ? "минимум" : "максимум");

        if (Nodes > 0)
            builder = builder.Metric("Узлов ветвления", Nodes, null, "просмотрено в методе ветвей и границ", MetricQuality.Unknown, 0);

        if (solved)
        {
            foreach (Variable variable in _program.Variables.Take(12))
                builder = builder.Metric(variable.Name, Fmt.Num(_values[variable.Index], 4), null,
                    variable.IsIntegral ? "целочисленная" : null);
        }

        return builder
            .FindingIf(solved && Nodes > 0 && Gap <= 1e-9,
                "Целочисленное решение доказано оптимальным: дерево ветвления пройдено полностью, "
                + "необследованных узлов с лучшей оценкой не осталось.")
            .FindingIf(Status == SolverStatus.LimitReached && Bound is not null,
                $"Разрыв между решением и оценкой — {Fmt.Pct(Gap)}. В этих пределах решение может быть улучшено.")
            .FindingIf(solved && _program.Variables.Any(v => !v.IsIntegral)
                && _program.Variables.Where(v => !v.IsIntegral).All(v => Math.Abs(_values[v.Index]) < 1e-12),
                "Все непрерывные переменные обратились в нуль: стоит проверить знаки в целевой функции.")
            .WarningIf(Status == SolverStatus.Unbounded,
                "Неограниченность почти всегда означает ошибку модели, а не свойство задачи: "
                + "у реальных ресурсов есть предел.")
            .WarningIf(Status == SolverStatus.Infeasible,
                "Противоречие может быть и в данных, и в постановке. Полезно ослабить ограничения по одному, "
                + "чтобы найти виновное.")
            .WarningIf(solved && integral > 0,
                "Значения целочисленных переменных округлены до целых в пределах допуска решателя; "
                + "при сравнении с ними пользуйтесь допуском, а не точным равенством.")
            .RecommendationIf(Status == SolverStatus.LimitReached,
                "Увеличить предел узлов или задать более слабый допуск по разрыву, если решение нужно точнее.")
            .RecommendationIf(solved,
                "Проверить чувствительность: пересчитать задачу с изменёнными правыми частями, "
                + "чтобы понять, какие ограничения действительно связывают решение.")
            .Build();
    }

    private static string StatusName(SolverStatus status) => status switch
    {
        SolverStatus.Optimal => "оптимум",
        SolverStatus.Infeasible => "нет допустимых решений",
        SolverStatus.Unbounded => "не ограничена",
        _ => "предел работы"
    };

    /// <summary>Краткая запись решения</summary>
    public override string ToString()
        => Status == SolverStatus.Optimal
            ? $"{StatusName(Status)}: {Objective:G6}"
            : StatusName(Status);
}
