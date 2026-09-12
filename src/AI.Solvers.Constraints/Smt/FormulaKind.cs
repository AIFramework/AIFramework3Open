namespace AI.Solvers.Constraints.Smt;

/// <summary>Вид узла формулы</summary>
public enum FormulaKind
{
    /// <summary>Истина или ложь</summary>
    Constant,

    /// <summary>Булева переменная</summary>
    Variable,

    /// <summary>Арифметический атом <c>Σ aᵢxᵢ ≤ b</c> или <c>&lt; b</c></summary>
    Atom,

    /// <summary>Отрицание</summary>
    Not,

    /// <summary>Конъюнкция</summary>
    And,

    /// <summary>Дизъюнкция</summary>
    Or,

    /// <summary>Эквивалентность</summary>
    Iff
}
