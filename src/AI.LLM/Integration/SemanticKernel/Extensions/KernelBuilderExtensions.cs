using System.Net;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;

namespace AI.LLM.Integration.SemanticKernel.Extensions;

#pragma warning disable SKEXP0001

/// <summary>
/// Методы расширения для регистрации сервисов AI.LLM в SK Kernel.
/// </summary>
public static class KernelBuilderExtensions
{
    /// <summary>
    /// Регистрирует <see cref="SharpGPTChatCompletionService"/> как SK IChatCompletionService,
    /// используя готовый экземпляр <see cref="ChatLLMApi"/>.
    /// <example>
    /// <code>
    /// var chatApi = new ChatLLMApi(apiKey, "claude-4.5-sonnet", systemPrompt, proxies: myProxies);
    /// chatApi.ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
    ///
    /// var kernel = Kernel.CreateBuilder()
    ///     .AddSharpGPTChatCompletion(chatApi)
    ///     .Build();
    /// </code>
    /// </example>
    /// </summary>
    public static IKernelBuilder AddSharpGPTChatCompletion(
        this IKernelBuilder builder,
        ChatLLMApi chatApi)
    {
        builder.Services.AddSingleton<IChatCompletionService>(
            new SharpGPTChatCompletionService(chatApi));

        return builder;
    }

    /// <summary>
    /// Регистрирует <see cref="SharpGPTChatCompletionService"/>, создавая ChatLLMApi
    /// из переданных параметров.
    /// </summary>
    public static IKernelBuilder AddSharpGPTChatCompletion(
        this IKernelBuilder builder,
        string apiKey,
        string modelName,
        string apiUrl = null,
        string systemPrompt = null,
        IEnumerable<WebProxy> proxies = null)
    {
        var chatApi = new ChatLLMApi(apiKey, modelName, systemPrompt, proxies: proxies);
        if (!string.IsNullOrEmpty(apiUrl))
            chatApi.ApiUrl = apiUrl;

        builder.Services.AddSingleton<IChatCompletionService>(
            new SharpGPTChatCompletionService(chatApi));

        return builder;
    }

    /// <summary>
    /// Регистрирует <see cref="LLMClientChatCompletionService"/> как SK IChatCompletionService,
    /// используя готовый экземпляр <see cref="ILLMClient"/>.
    /// Все вызовы проходят через ILLMClient -> LLMBase -> ChatLLMApi — биллинг сохраняется.
    /// <example>
    /// <code>
    /// var llm = new LLMBase(new OpenRouterModelApi(apiKey, "gpt-4o"));
    ///
    /// var kernel = Kernel.CreateBuilder()
    ///     .AddSharpGPTChatCompletion(llm)
    ///     .Build();
    /// </code>
    /// </example>
    /// </summary>
    public static IKernelBuilder AddSharpGPTChatCompletion(
        this IKernelBuilder builder,
        ILLMClient llmClient,
        string modelId = "aiframework")
    {
        builder.Services.AddSingleton<IChatCompletionService>(
            new LLMClientChatCompletionService(llmClient, modelId));

        return builder;
    }

    /// <summary>
    /// Регистрирует <see cref="SharpGPTEmbeddingService"/> как SK ITextEmbeddingGenerationService,
    /// используя готовый экземпляр <see cref="IEmbedderService"/>.
    /// <example>
    /// <code>
    /// var embedder = new InfinityEmbedder("http://localhost:7997", "bge-m3");
    ///
    /// var kernel = Kernel.CreateBuilder()
    ///     .AddSharpGPTChatCompletion(chatApi)
    ///     .AddSharpGPTEmbedding(embedder)
    ///     .Build();
    /// </code>
    /// </example>
    /// </summary>
    public static IKernelBuilder AddSharpGPTEmbedding(
        this IKernelBuilder builder,
        IEmbedderService embedder,
        string modelId = "sharpgpt-embedding")
    {
        builder.Services.AddSingleton<ITextEmbeddingGenerationService>(
            new SharpGPTEmbeddingService(embedder, modelId));

        return builder;
    }
}

#pragma warning restore SKEXP0001
