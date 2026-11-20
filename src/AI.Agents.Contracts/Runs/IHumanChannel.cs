using AI.Agents.Contracts.Engines;

namespace AI.Agents.Contracts.Runs;

/// <summary>
/// Синхронный канал к человеку: задать вопрос и дождаться ответа внутри вызова.
/// </summary>
/// <remarks>
/// Временная реализация <see cref="Tools.ToolStatus.AwaitsUser"/> для хостов в одном процессе (настольное
/// приложение, консоль), пока движок не умеет продолжать запуск с места. Когда движок начнет принимать
/// <see cref="EngineInput.State"/>, его заменит пауза: итог <see cref="EngineStatus.AwaitsUser"/>, сохранение
/// <see cref="RunState"/> через <see cref="IRunStore"/> и продолжение с ответом в <see cref="EngineTask.Answer"/>.
/// </remarks>
public interface IHumanChannel
{
    /// <summary>Задать вопрос и дождаться ответа.</summary>
    /// <param name="question">Вопрос.</param>
    /// <param name="cancellationToken">Отмена ожидания.</param>
    /// <returns>Ответ на этот вопрос (<see cref="Answer.QuestionId"/> = <see cref="Question.Id"/>).</returns>
    Task<Answer> AskAsync(Question question, CancellationToken cancellationToken = default);
}
