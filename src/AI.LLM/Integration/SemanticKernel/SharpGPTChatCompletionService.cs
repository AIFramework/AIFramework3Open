using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Integration.SemanticKernel.Adapters;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AI.LLM.Integration.SemanticKernel;

/// <summary>
/// SK-совместимая обёртка над <see cref="ChatLLMApi"/>.
/// Позволяет использовать инфраструктуру AI.LLM (proxy-ротация, idle timeout,
/// usage из streaming, детектор зацикленного ответа) через стандартный SK-интерфейс.
/// </summary>
public class SharpGPTChatCompletionService : IChatCompletionService
{
    private readonly ChatLLMApi _chatApi;
    private readonly Dictionary<string, object> _attributes;

    /// <summary>
    /// Создаёт SK-сервис, делегирующий работу существующему ChatLLMApi.
    /// </summary>
    /// <param name="chatApi">Настроенный экземпляр ChatLLMApi (с proxy, ключами, моделью).</param>
    public SharpGPTChatCompletionService(ChatLLMApi chatApi)
    {
        _chatApi = chatApi ?? throw new ArgumentNullException(nameof(chatApi));
        _attributes = new Dictionary<string, object>
        {
            ["ModelId"] = chatApi.ModelName,
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> Attributes => _attributes;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings executionSettings = null,
        Kernel kernel = null,
        CancellationToken cancellationToken = default)
    {
        var messages = MessageAdapter.FromChatHistory(chatHistory);
        var generateSettings = SettingsAdapter.FromSKSettings(executionSettings);

        var response = await _chatApi.SendWithContextAsync(messages, generateSettings, cancellationToken);

        return ConvertResponse(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings executionSettings = null,
        Kernel kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ChatLLMApi внутренне использует streaming для idle timeout,
        // но накапливает полный ответ. Возвращаем его как единый чанк.
        // При необходимости real-time streaming — расширить ChatLLMApi.
        var messages = MessageAdapter.FromChatHistory(chatHistory);
        var generateSettings = SettingsAdapter.FromSKSettings(executionSettings);

        var response = await _chatApi.SendWithContextAsync(messages, generateSettings, cancellationToken);

        if (response?.Choices == null || response.Choices.Count == 0)
            yield break;

        var choice = response.Choices[0];
        var content = choice.Message?.Content?.ToString() ?? string.Empty;

        var metadata = BuildMetadata(response);

        yield return new StreamingChatMessageContent(
            role: AuthorRole.Assistant,
            content: content,
            modelId: response.Model ?? _chatApi.ModelName,
            metadata: metadata);
    }

    private IReadOnlyList<ChatMessageContent> ConvertResponse(ChatCompletionsResponse response)
    {
        if (response?.Choices == null || response.Choices.Count == 0)
            return [];

        var results = new List<ChatMessageContent>(response.Choices.Count);

        foreach (var choice in response.Choices)
        {
            var msg = choice.Message;
            if (msg == null) continue;

            var skMessage = MessageAdapter.ToSKMessage(msg, response.Model ?? _chatApi.ModelName);

            // Переносим usage, logprobs и другие метаданные
            var metadata = BuildMetadata(response, choice);
            skMessage.Metadata = metadata;

            results.Add(skMessage);
        }

        return results;
    }

    private Dictionary<string, object> BuildMetadata(
        ChatCompletionsResponse response,
        Choice choice = null)
    {
        choice ??= response.Choices?.FirstOrDefault();
        var metadata = new Dictionary<string, object>();

        if (response.Usage != null)
        {
            metadata["Usage"] = new Dictionary<string, object>
            {
                ["PromptTokens"] = response.Usage.PromptTokens,
                ["CompletionTokens"] = response.Usage.CompletionTokens,
                ["TotalTokens"] = response.Usage.TotalTokens,
                ["ReasoningTokens"] = response.Usage.ReasoningTokens,
            };

            var cost = CostExtractor.TryExtract(response.Usage.Cost);
            if (cost.HasValue)
                metadata["Cost"] = cost.Value;
        }

        if (!string.IsNullOrEmpty(choice?.FinishReason))
            metadata["FinishReason"] = choice.FinishReason;

        if (!string.IsNullOrEmpty(choice?.NativeFinishReason))
            metadata["NativeFinishReason"] = choice.NativeFinishReason;

        if (!string.IsNullOrEmpty(choice?.Reasoning))
            metadata["Reasoning"] = choice.Reasoning;

        if (!string.IsNullOrEmpty(response.Provider))
            metadata["Provider"] = response.Provider;

        if (choice?.Logprobs != null)
            metadata["Logprobs"] = choice.Logprobs;

        return metadata;
    }
}
