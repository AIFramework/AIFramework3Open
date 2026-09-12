namespace AI.Solvers.Constraints.Smt;

/// <summary>Исход решения задачи SMT</summary>
public enum SmtStatus
{
    /// <summary>Формула выполнима, найдена модель</summary>
    Satisfiable,

    /// <summary>Формула невыполнима — доказано</summary>
    Unsatisfiable,

    /// <summary>Исчерпан предел: ответ неизвестен</summary>
    Unknown
}
