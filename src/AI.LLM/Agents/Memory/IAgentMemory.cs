using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Абстракция памяти агента — управление контекстом диалога.
/// </summary>
public interface IAgentMemory
{
    /// <summary>
    /// Строит контекст сообщений для отправки в LLM.
    /// </summary>
    /// <param name="query">Текущий запрос пользователя.</param>
    /// <param name="systemPrompt">Системный промпт агента.</param>
    /// <returns>Список сообщений для LLM.</returns>
    Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt);

    /// <summary>
    /// Сохраняет взаимодействие в памяти.
    /// </summary>
    /// <param name="query">Запрос пользователя.</param>
    /// <param name="answer">Ответ агента.</param>
    /// <param name="fullHistory">Полная история сообщений.</param>
    Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory);

    /// <summary>
    /// Очищает память.
    /// </summary>
    Task ClearAsync();
}
