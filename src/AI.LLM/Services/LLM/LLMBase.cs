using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;

namespace AI.LLM.Services.LLM;

/// <summary>
/// Базовая логика работы с LLM
/// </summary>
public class LLMBase : ILLMClient
{
    private readonly ChatLLMApi _chatLLMApi;
    protected readonly LLMOptions _llmOptions;

    /// <summary>
    /// Базовая логика работы с LLM
    /// </summary>
    /// <param name="chatLLMApi">Общий класс для работы с LLM</param>
    /// <param name="llmOptions">Настройки LLM (опционально)</param>
    public LLMBase(ChatLLMApi chatLLMApi, LLMOptions llmOptions = null)
    {
        _chatLLMApi = chatLLMApi ?? throw new ArgumentNullException(nameof(chatLLMApi));
        _llmOptions = llmOptions;
    }

    /// <summary>
    /// Создает GenerateSettings с учетом параметров из LLMOptions (температура, reasoning)
    /// </summary>
    /// <param name="baseSettings">Базовые настройки генерации</param>
    /// <returns>GenerateSettings с примененными параметрами из LLMOptions</returns>
    protected GenerateSettings ApplyReasoningSettings(GenerateSettings baseSettings)
    {
        // Копия, а не объект вызывающего: один и тот же экземпляр настроек может быть общим на
        // несколько клиентов с разными LLMOptions, и подмешивать свои значения в чужой объект
        // значит переписать его последнему вызвавшему.
        var settings = baseSettings?.Clone() ?? new GenerateSettings();

        if (_llmOptions == null)
            return settings;

        // Применяем температуру, если она задана в настройках
        if (_llmOptions.Temperature.HasValue)
            settings.Temperature = _llmOptions.Temperature.Value;

        // Создаем ReasoningSettings если reasoning включен
        if (_llmOptions.EnableReasoning)
        {
            settings.ReasoningSettings = new ReasoningSettings(
                effort: _llmOptions.ReasoningEffort,
                maxTokens: _llmOptions.ReasoningMaxTokens,
                exclude: _llmOptions.ReasoningExclude,
                enabled: true
            );
        }

        return settings;
    }

    /// <summary>
    /// Отправка запроса к LLM
    /// </summary>
    /// <param name="text">Текст запроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ LLM в виде строки</returns>
    public async Task<string> SendToLLM(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Текст запроса не может быть пустым.", nameof(text));

        // Применяем reasoning настройки
        generateSettings = ApplyReasoningSettings(generateSettings);

        // Используем ConfigureAwait для библиотечного кода.
        return await _chatLLMApi.SendWithoutContextTextAsync(text, generateSettings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Отправка запроса к LLM с учетом контекста сообщений.
    /// </summary>
    /// <param name="messages">Последовательность сообщений LLM.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ответ LLM в виде строки.</returns>
    public async Task<string> SendToLLM(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        // Применяем reasoning настройки
        generateSettings = ApplyReasoningSettings(generateSettings);

        // Передаём запрос через клиент _chatLLMApi с поддержкой контекста
        return await _chatLLMApi.SendWithContextTextAsync(messages, generateSettings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Отправка запроса к LLM с возвратом полного ответа (включая tool_calls, usage, finish_reason).
    /// Используйте этот метод для Function Calling.
    /// </summary>
    public async Task<ChatCompletionsResponse> SendToLLMFull(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        generateSettings = ApplyReasoningSettings(generateSettings);

        return await _chatLLMApi.SendWithContextAsync(messages, generateSettings, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        return await _chatLLMApi.TokenizeAsync(messages, cancellationToken).ConfigureAwait(false);
    }

    #region ILLMClient

    /// <inheritdoc />
    Task<string> ILLMClient.SendAsync(string text, GenerateSettings generateSettings, CancellationToken cancellationToken)
        => SendToLLM(text, generateSettings, cancellationToken);

    /// <inheritdoc />
    Task<string> ILLMClient.SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings, CancellationToken cancellationToken)
        => SendToLLM(messages, generateSettings, cancellationToken);

    /// <inheritdoc />
    Task<ChatCompletionsResponse> ILLMClient.SendFullAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings, CancellationToken cancellationToken)
        => SendToLLMFull(messages, generateSettings, cancellationToken);

    #endregion
}
