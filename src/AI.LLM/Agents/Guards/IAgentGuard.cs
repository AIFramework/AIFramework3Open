namespace AI.LLM.Agents.Guards;

/// <summary>
/// Защитный механизм для проверки ответов агента.
/// </summary>
public interface IAgentGuard
{
    /// <summary>
    /// Проверяет ответ агента на соответствие критериям безопасности / качества.
    /// </summary>
    /// <param name="query">Исходный запрос пользователя.</param>
    /// <param name="answer">Ответ агента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат проверки.</returns>
    Task<GuardResult> CheckAsync(string query, string answer, CancellationToken cancellationToken = default);
}
