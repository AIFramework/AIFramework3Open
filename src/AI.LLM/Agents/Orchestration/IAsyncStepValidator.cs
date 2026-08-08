using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Приёмка шага, которой для проверки нужен ввод-вывод — например обращение к модели.
/// </summary>
/// <remarks>
/// Отдельный интерфейс, а не метод в <see cref="IStepValidator"/>: тот синхронный, и добавление
/// в него асинхронного метода заставило бы каждую существующую реализацию его дописывать, а
/// асинхронную — притворяться синхронной и блокировать поток. Оркестратор предпочитает этот
/// интерфейс, когда он задан, и работает по-старому, когда нет.
/// </remarks>
public interface IAsyncStepValidator
{
    /// <summary>
    /// Возвращает <c>true</c>, если шаг считается успешно выполненным.
    /// </summary>
    /// <param name="step">Шаг плана; его <see cref="PlanStep.DoneWhen"/> и есть критерий приёмки.</param>
    /// <param name="result">Результат работы агента по этому шагу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<bool> IsSuccessAsync(PlanStep step, AgentResult result, CancellationToken cancellationToken = default);
}
