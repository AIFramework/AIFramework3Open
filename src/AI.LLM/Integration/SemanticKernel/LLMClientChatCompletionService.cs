using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Integration.SemanticKernel.Adapters;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AI.LLM.Integration.SemanticKernel;

/// <summary>
/// SK-совместимая обёртка над <see cref="ILLMClient"/>.
/// Все вызовы проходят через <see cref="ILLMClient"/> — биллинг, reasoning settings
/// и весь pipeline <c>LLMBase -> ChatLLMApi</c> полностью сохраняются.
/// <para>
/// Используйте совместно с <see cref="Extensions.KernelBuilderExtensions.AddSharpGPTChatCompletion(IKernelBuilder, ILLMClient, string)"/>
/// для интеграции AI.LLM в SK-сценарии (Agents, Planners, Auto Function Invocation).
/// </para>
/// </summary>
public sealed class LLMClientChatCompletionService : IChatCompletionService
{
    private readonly ILLMClient _llm;
    private readonly Dictionary<string, object> _attributes;

    /// <param name="llm">Экземпляр ILLMClient (LLMBase и любые наследники).</param>
    /// <param name="modelId">Идентификатор модели для SK-метаданных.</param>
    public LLMClientChatCompletionService(ILLMClient llm, string modelId = "aiframework")
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _attributes = new Dictionary<string, object> { ["ModelId"] = modelId };
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
        var settings = SettingsAdapter.FromSKSettings(executionSettings);

        var response = await _llm.SendFullAsync(messages, settings, cancellationToken)
            .ConfigureAwait(false);

        return ConvertResponse(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings executionSettings = null,
        Kernel kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = MessageAdapter.FromChatHistory(chatHistory);
        var settings = SettingsAdapter.FromSKSettings(executionSettings);

        var response = await _llm.SendFullAsync(messages, settings, cancellationToken)
            .ConfigureAwait(false);

        if (response?.Choices is not { Count: > 0 })
            yield break;

        var choice = response.Choices[0];
        var content = choice.Message?.Content?.ToString() ?? string.Empty;
        var metadata = BuildMetadata(response, choice);

        yield return new StreamingChatMessageContent(
            role: AuthorRole.Assistant,
            content: content,
            modelId: response.Model ?? _attributes["ModelId"]?.ToString(),
            metadata: metadata);
    }

    private IReadOnlyList<ChatMessageContent> ConvertResponse(ChatCompletionsResponse response)
    {
        if (response?.Choices is not { Count: > 0 })
            return [];

        var results = new List<ChatMessageContent>(response.Choices.Count);
        var modelId = response.Model ?? _attributes["ModelId"]?.ToString();

        foreach (var choice in response.Choices)
        {
            if (choice.Message == null) continue;

            var skMessage = MessageAdapter.ToSKMessage(choice.Message, modelId);
            skMessage.Metadata = BuildMetadata(response, choice);
            results.Add(skMessage);
        }

        return results;
    }

    private static Dictionary<string, object> BuildMetadata(
        ChatCompletionsResponse response, Choice choice)
    {
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
