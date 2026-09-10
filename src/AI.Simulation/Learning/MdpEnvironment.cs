using AI.Simulation.Markov;

namespace AI.Simulation.Learning;

/// <summary>
/// Марковский процесс в роли среды для обучения с подкреплением
/// </summary>
/// <remarks>
/// Обучающийся видит только разыгранные исходы, а вероятности и награды остаются скрытыми.
/// Зато тот же процесс можно решить точно, и ответ обучения сверяется с ответом уравнений
/// Беллмана — так проверяется, что обучение действительно находит оптимум, а не что-то похожее.
/// </remarks>
public sealed class MdpEnvironment : IDiscreteEnvironment
{
    private readonly MarkovDecisionProcess _process;
    private readonly int? _startState;

    /// <summary>Создаёт среду</summary>
    /// <param name="process">Марковский процесс</param>
    /// <param name="startState">
    /// Начальное состояние эпизода; по умолчанию случайное — так каждое состояние регулярно
    /// посещается, и оценки не остаются начальными там, куда стратегия сама не заходит
    /// </param>
    public MdpEnvironment(MarkovDecisionProcess process, int? startState = null)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (startState is { } start && (start < 0 || start >= process.StateCount))
            throw new ArgumentOutOfRangeException(nameof(startState), $"Состояния {start} в процессе нет");

        _process = process;
        _startState = startState;
    }

    /// <inheritdoc />
    public int StateCount => _process.StateCount;

    /// <inheritdoc />
    public int ActionCount(int state) => _process.ActionCount(state);

    /// <inheritdoc />
    public int Reset(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return _startState ?? random.Next(StateCount);
    }

    /// <inheritdoc />
    public Transition Step(int state, int action, Random random)
    {
        (int next, double reward) = _process.Sample(state, action, random);

        return new Transition(next, reward, Terminal: false);
    }
}
