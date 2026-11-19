using AI.Agents.Contracts.Models;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Собственные потолки агента: шаги, деньги и длительность запуска. Хост сводит их со своими рамками в
/// <see cref="IRunBudget"/>, побеждает меньший предел.
/// </summary>
/// <remarks>
/// Здесь длительность, а не срок: момент окончания (<see cref="IRunBudget.Deadline"/>) известен только при
/// запуске и живет только в бюджете.
/// </remarks>
public sealed record AgentLimits
{
    private readonly int? _steps;
    private readonly decimal? _cost;
    private readonly TimeSpan? _duration;

    /// <summary>Наибольшее число шагов; <see langword="null"/>: свой предел не задан.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Число меньше 1.</exception>
    public int? Steps
    {
        get => _steps;
        init => _steps = value is null or >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Steps), value, "Предел шагов не меньше 1.");
    }

    /// <summary>
    /// Наибольшая стоимость запуска в той же единице, что <see cref="ModelUsage.Cost"/>: валюту задает хост, контракт ее
    /// не знает. <see langword="null"/>: свой предел не задан.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение не больше нуля.</exception>
    public decimal? Cost
    {
        get => _cost;
        init => _cost = value is null || value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Cost), value, "Предел стоимости больше нуля.");
    }

    /// <summary>Наибольшая длительность запуска; <see langword="null"/>: свой предел не задан.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Длительность не больше нуля.</exception>
    public TimeSpan? Duration
    {
        get => _duration;
        init => _duration = value is null || value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Duration), value, "Предел длительности больше нуля.");
    }
}
