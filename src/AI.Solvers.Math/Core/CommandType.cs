namespace AI.Solvers.Math.Core;

public enum CommandType
{
    // Интегрирование
    IndefiniteIntegral,
    DefiniteIntegral,
    DoubleIntegral,

    // Дифференцирование
    FirstDerivative,
    SecondDerivative,
    NthDerivative,
    PartialDerivative,

    // Дифференциальные уравнения
    ODE,
    ODEWithInitialConditions,
    SystemODE,
    PDE,

    // Дополнительные операции
    Limit,
    TaylorSeries,
    LaplaceTransform,
    LaplaceTable,        // Показать таблицу преобразований Лапласа
    FourierTransform,
    Solve,

    // Неизвестная команда
    Unknown
}

