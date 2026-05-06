using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Эвристический валидатор: шаг считается провальным,
/// если финальный ответ агента содержит характерные маркеры ошибки.
/// Для точного контроля замените на собственную реализацию <see cref="IStepValidator"/>.
/// </summary>
public sealed class DefaultStepValidator : IStepValidator
{
    private static readonly string[] FailureMarkers =
    [
        "error:", "failed", "timeout", "not found", "blocked",
        "exception", "unable to", "cannot", "could not"
    ];

    /// <inheritdoc/>
    public bool IsSuccess(PlanStep step, AgentResult result)
    {
        if (result is null) return false;

        var lower = (result.Answer ?? "").ToLowerInvariant();
        return !FailureMarkers.Any(marker => lower.Contains(marker));
    }
}
