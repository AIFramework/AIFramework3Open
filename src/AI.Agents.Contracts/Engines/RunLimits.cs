using AI.Agents.Contracts.Models;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Пределы бюджета одного хода или запуска: шаги, токены, деньги, вызовы модели и длительность. Из них хост строит
/// <see cref="IRunBudget"/>; все участники хода (цикл, подзадачи, шаги плана, инструменты) делят один бюджет.
/// </summary>
/// <remarks>
/// Каждое поле необязательно: <see langword="null"/> значит «этого предела нет», а не «бесконечно по умолчанию».
/// Рамки хоста сводятся с потолками агента (<see cref="AgentLimits"/>) через <see cref="Narrow(AgentLimits?)"/>:
/// побеждает меньший предел.
/// </remarks>
public sealed record RunLimits
{
    private readonly int? _steps;
    private readonly long? _tokens;
    private readonly decimal? _cost;
    private readonly int? _modelCalls;
    private readonly TimeSpan? _duration;

    /// <summary>Наибольшее число шагов (<see cref="IRunBudget.TryStep(out StopCode)"/>); <see langword="null"/>: без предела.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Число меньше 1.</exception>
    public int? Steps
    {
        get => _steps;
        init => _steps = value is null or >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Steps), value, "Предел шагов не меньше 1.");
    }

    /// <summary>Наибольшее число токенов моделей (запрос и ответ вместе); <see langword="null"/>: без предела.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Число меньше 1.</exception>
    public long? Tokens
    {
        get => _tokens;
        init => _tokens = value is null or >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Tokens), value, "Предел токенов не меньше 1.");
    }

    /// <summary>
    /// Наибольшая стоимость в той же единице, что <see cref="ModelUsage.Cost"/>; <see langword="null"/>: без предела.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение не больше нуля.</exception>
    public decimal? Cost
    {
        get => _cost;
        init => _cost = value is null || value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Cost), value, "Предел стоимости больше нуля.");
    }

    /// <summary>Наибольшее число вызовов модели (<see cref="IRunBudget.Spend(ModelUsage)"/>); <see langword="null"/>: без предела.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Число меньше 1.</exception>
    public int? ModelCalls
    {
        get => _modelCalls;
        init => _modelCalls = value is null or >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ModelCalls), value, "Предел вызовов модели не меньше 1.");
    }

    /// <summary>Наибольшая длительность от создания бюджета; <see langword="null"/>: без срока.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Длительность не больше нуля.</exception>
    public TimeSpan? Duration
    {
        get => _duration;
        init => _duration = value is null || value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Duration), value, "Предел длительности больше нуля.");
    }

    /// <summary>
    /// Сводит пределы хоста с потолками агента: по каждому полю побеждает меньший; предел, заданный только с одной
    /// стороны, остается.
    /// </summary>
    /// <param name="agent">Потолки агента; <see langword="null"/>: у агента своих потолков нет.</param>
    /// <returns>Новые пределы.</returns>
    public RunLimits Narrow(AgentLimits? agent) => agent == null
        ? this
        : this with
        {
            Steps = Min(Steps, agent.Steps),
            Cost = Min(Cost, agent.Cost),
            Duration = Min(Duration, agent.Duration),
        };

    private static T? Min<T>(T? a, T? b) where T : struct, IComparable<T> =>
        a is { } x && b is { } y ? (x.CompareTo(y) <= 0 ? x : y) : a ?? b;
}
