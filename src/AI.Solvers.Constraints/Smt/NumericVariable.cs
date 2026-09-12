namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Числовая переменная задачи SMT: целая или вещественная, с необязательными границами
/// </summary>
/// <remarks>
/// Переменная сама является выражением <c>1·x</c>, поэтому <c>x + 2*y &lt;= 10</c> пишется
/// без преобразований. Создаётся методами <see cref="SmtModel.Int"/> и <see cref="SmtModel.Real"/>.
/// </remarks>
public sealed class NumericVariable : LinearExpression
{
    private readonly Dictionary<NumericVariable, double> _self;

    internal NumericVariable(SmtModel owner, int index, string name, SmtSort sort, double lowerBound, double upperBound)
        : base(new Dictionary<NumericVariable, double>(), 0)
    {
        Owner = owner;
        Index = index;
        Name = name;
        Sort = sort;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        _self = new Dictionary<NumericVariable, double> { [this] = 1.0 };
    }

    /// <summary>Номер среди числовых переменных задачи</summary>
    public int Index { get; }

    /// <summary>Имя</summary>
    public string Name { get; }

    /// <summary>Сорт: целая или вещественная</summary>
    public SmtSort Sort { get; }

    /// <summary>Нижняя граница; минус бесконечность — без границы</summary>
    public double LowerBound { get; }

    /// <summary>Верхняя граница; плюс бесконечность — без границы</summary>
    public double UpperBound { get; }

    /// <inheritdoc />
    public override IReadOnlyDictionary<NumericVariable, double> Terms => _self;

    internal SmtModel Owner { get; }

    /// <summary>Имя переменной</summary>
    public override string ToString() => Name;
}
