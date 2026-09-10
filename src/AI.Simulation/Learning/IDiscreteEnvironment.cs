namespace AI.Simulation.Learning;

/// <summary>
/// Среда с конечным числом состояний и действий, из которой обучающийся получает только исходы
/// </summary>
/// <remarks>
/// В отличие от марковского процесса, среда не раскрывает вероятностей переходов и функции
/// наград: их можно лишь испытать, сделав шаг. Это и есть постановка обучения с подкреплением.
/// </remarks>
public interface IDiscreteEnvironment
{
    /// <summary>Число состояний</summary>
    int StateCount { get; }

    /// <summary>Число действий, доступных в состоянии</summary>
    /// <param name="state">Состояние</param>
    int ActionCount(int state);

    /// <summary>Начальное состояние нового эпизода</summary>
    /// <param name="random">Генератор случайных чисел обучения</param>
    int Reset(Random random);

    /// <summary>Выполняет действие и сообщает исход</summary>
    /// <param name="state">Текущее состояние</param>
    /// <param name="action">Действие</param>
    /// <param name="random">Генератор случайных чисел обучения</param>
    Transition Step(int state, int action, Random random);
}
