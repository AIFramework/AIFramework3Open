using AI.Insights;

namespace AI.Solvers.Constraints.Smt;

/// <summary>Результат решения задачи SMT</summary>
public sealed class SmtSolution : IInterpretable
{
    private readonly bool[] _booleans;
    private readonly double[] _numbers;

    internal SmtSolution(
        SmtModel model,
        SmtStatus status,
        bool[] booleans,
        double[] numbers,
        int satCalls,
        int lemmas,
        int theoryChecks,
        string? unknownReason,
        double tolerance)
    {
        Model = model;
        Status = status;
        _booleans = booleans;
        _numbers = numbers;
        SatCalls = satCalls;
        TheoryLemmas = lemmas;
        TheoryChecks = theoryChecks;
        UnknownReason = unknownReason;
        Verified = status == SmtStatus.Satisfiable && Verify(tolerance);
    }

    /// <summary>Задача</summary>
    public SmtModel Model { get; }

    /// <summary>Исход</summary>
    public SmtStatus Status { get; }

    /// <summary>Выполнима ли формула</summary>
    public bool IsSatisfiable => Status == SmtStatus.Satisfiable;

    /// <summary>Сколько раз решалась булева часть</summary>
    public int SatCalls { get; }

    /// <summary>Сколько арифметических противоречий запрещено булевой части</summary>
    public int TheoryLemmas { get; }

    /// <summary>Сколько раз проверялась совместность арифметики</summary>
    public int TheoryChecks { get; }

    /// <summary>Почему ответ неизвестен; <c>null</c> при определённом ответе</summary>
    public string? UnknownReason { get; }

    /// <summary>
    /// Прошла ли модель независимую проверку — прямое вычисление всех утверждений задачи
    /// </summary>
    public bool Verified { get; }

    /// <summary>Значение булевой переменной в модели</summary>
    /// <param name="variable">Переменная</param>
    public bool this[BooleanVariable variable]
    {
        get
        {
            RequireModel();
            ArgumentNullException.ThrowIfNull(variable);

            return _booleans[variable.Index];
        }
    }

    /// <summary>Значение числовой переменной в модели</summary>
    /// <param name="variable">Переменная</param>
    public double this[NumericVariable variable]
    {
        get
        {
            RequireModel();
            ArgumentNullException.ThrowIfNull(variable);

            return _numbers[variable.Index];
        }
    }

    /// <summary>Значение выражения в модели</summary>
    /// <param name="expression">Выражение</param>
    public double Evaluate(LinearExpression expression)
    {
        RequireModel();
        ArgumentNullException.ThrowIfNull(expression);

        return expression.Constant + expression.Terms.Sum(t => t.Value * _numbers[t.Key.Index]);
    }

    /// <summary>Истинна ли формула в модели</summary>
    /// <param name="formula">Формула</param>
    /// <param name="tolerance">Допуск для нестрогих неравенств</param>
    public bool Evaluate(SmtFormula formula, double tolerance = 1e-7)
    {
        RequireModel();
        ArgumentNullException.ThrowIfNull(formula);

        return formula.Kind switch
        {
            FormulaKind.Constant => formula.ConstantValue,
            FormulaKind.Variable => _booleans[formula.Variable!.Index],
            FormulaKind.Atom => formula.Atom!.IsSatisfiedBy(v => _numbers[v.Index], tolerance),
            FormulaKind.Not => !Evaluate(formula.Children[0], tolerance),
            FormulaKind.And => formula.Children.All(c => Evaluate(c, tolerance)),
            FormulaKind.Or => formula.Children.Any(c => Evaluate(c, tolerance)),
            _ => Evaluate(formula.Children[0], tolerance) == Evaluate(formula.Children[1], tolerance),
        };
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var builder = new InterpretationBuilder($"SMT: {Model.Name}");

        builder = Status switch
        {
            SmtStatus.Satisfiable => builder.Summary(
                $"Формула выполнима: найдена модель, и прямое вычисление всех {Model.Assertions.Count} утверждений "
                + (Verified ? "её подтвердило." : "её НЕ подтвердило — ответу нельзя доверять.")),
            SmtStatus.Unsatisfiable => builder.Summary(
                "Формула невыполнима — это доказано: булева часть вместе с запрещёнными арифметическими "
                + "противоречиями не имеет ни одной подстановки."),
            _ => builder.Summary($"Ответ неизвестен: {UnknownReason}."),
        };

        builder = builder
            .Metric("Вызовов булева решателя", SatCalls, null, "каждая лемма требует нового решения", MetricQuality.Unknown, 0)
            .Metric("Лемм теории", TheoryLemmas, null, "запрещённых противоречивых наборов атомов", MetricQuality.Unknown, 0)
            .Metric("Проверок арифметики", TheoryChecks, null, "решений задачи линейного программирования", MetricQuality.Unknown, 0);

        if (IsSatisfiable)
        {
            foreach (NumericVariable variable in Model.NumericVariables.Take(12))
                builder = builder.Metric(variable.Name, _numbers[variable.Index], null, null, MetricQuality.Unknown, 6);

            foreach (BooleanVariable variable in Model.BooleanVariables.Take(12))
                builder = builder.Metric(variable.Name, _booleans[variable.Index] ? "истина" : "ложь");
        }

        return builder
            .FindingIf(Status == SmtStatus.Satisfiable && Verified,
                "Модель проверена независимо от решателя: каждое утверждение вычислено заново при найденных значениях.")
            .FindingIf(TheoryLemmas > 0,
                $"Булева часть {TheoryLemmas} раз предлагала наборы атомов, противоречивые в арифметике. Каждое "
                + "противоречие сведено к минимальному подмножеству и запрещено навсегда — так булев поиск учится "
                + "арифметике.")
            .FindingIf(Status == SmtStatus.Unsatisfiable,
                "Невыполнимость — ответ, а не отказ: если он неожидан, ошибка в постановке, и искать её стоит "
                + "среди утверждений, участвующих в противоречии.")
            .WarningIf(Status == SmtStatus.Unknown,
                "Неизвестный ответ не означает «скорее всего невыполнимо»: предел исчерпан, ни выполнимость, "
                + "ни невыполнимость не доказаны.")
            .Warning("Арифметика ведётся в числах с плавающей точкой с допуском, а не в точных рациональных, как "
                + "в промышленных решателях: на задачах с почти совпадающими границами ответ стоит перепроверять.")
            .WarningIf(Model.HasIntegers,
                "Целочисленная арифметика проверяется ветвями и границами. Без границ переменных поиск может "
                + "исчерпать предел узлов, и тогда ответ — «неизвестно».")
            .Build();
    }

    private bool Verify(double tolerance) => Model.Assertions.All(a => Evaluate(a, tolerance));

    private void RequireModel()
    {
        if (Status != SmtStatus.Satisfiable)
            throw new InvalidOperationException("Модель есть только у выполнимой формулы");
    }
}
