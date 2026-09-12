namespace AI.Solvers.Constraints.Smt;

/// <summary>Булева переменная задачи SMT</summary>
/// <remarks>Создаётся методом <see cref="SmtModel.Bool"/>.</remarks>
public sealed class BooleanVariable
{
    internal BooleanVariable(SmtModel owner, int index, string name)
    {
        Owner = owner;
        Index = index;
        Name = name;
    }

    /// <summary>Номер среди булевых переменных задачи</summary>
    public int Index { get; }

    /// <summary>Имя</summary>
    public string Name { get; }

    internal SmtModel Owner { get; }

    /// <summary>Переменная как формула</summary>
    /// <param name="variable">Переменная</param>
    public static implicit operator SmtFormula(BooleanVariable variable) => SmtFormula.Of(variable);

    /// <summary>Конъюнкция двух переменных</summary>
    /// <param name="left">Первая</param>
    /// <param name="right">Вторая</param>
    public static SmtFormula operator &(BooleanVariable left, BooleanVariable right) => SmtFormula.And(left, right);

    /// <summary>Дизъюнкция двух переменных</summary>
    /// <param name="left">Первая</param>
    /// <param name="right">Вторая</param>
    public static SmtFormula operator |(BooleanVariable left, BooleanVariable right) => SmtFormula.Or(left, right);

    /// <summary>Отрицание переменной</summary>
    /// <param name="variable">Переменная</param>
    public static SmtFormula operator !(BooleanVariable variable) => SmtFormula.Not(variable);

    /// <summary>Имя переменной</summary>
    public override string ToString() => Name;
}
