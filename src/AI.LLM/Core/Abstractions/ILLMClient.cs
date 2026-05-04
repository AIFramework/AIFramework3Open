using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;

namespace AI.LLM.Core.Abstractions;

/// <summary>
/// Абстракция клиента LLM для отправки запросов к языковым моделям.
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Отправка текстового запроса к LLM.
    /// </summary>
    /// <param name="text">Текст запроса</param>
    /// <param name="generateSettings">Настройки генерации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ LLM в виде строки</returns>
    Task<string> SendAsync(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправка запроса к LLM с учетом контекста сообщений.
    /// </summary>
    /// <param name="messages">Последовательность сообщений LLM</param>
    /// <param name="generateSettings">Настройки генерации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ LLM в виде строки</returns>
    Task<string> SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправка запроса к LLM с возвратом полного ответа (включая tool_calls, usage, finish_reason).
    /// </summary>
    /// <param name="messages">Последовательность сообщений LLM</param>
    /// <param name="generateSettings">Настройки генерации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Полный ответ от LLM</returns>
    Task<ChatCompletionsResponse> SendFullAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Определяет число токенов в запросе.
    /// </summary>
    /// <param name="messages">Последовательность сообщений LLM</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество токенов</returns>
    Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default);
}
