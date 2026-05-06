using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Валидатор успешности выполнения шага плана.
/// </summary>
public interface IStepValidator
{
    /// <summary>
    /// Возвращает true, если шаг считается успешно выполненным.
    /// </summary>
    /// <param name="step">Шаг плана.</param>
    /// <param name="result">Результат работы агента по данному шагу.</param>
    bool IsSuccess(PlanStep step, AgentResult result);
}
