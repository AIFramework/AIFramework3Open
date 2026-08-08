using AI.LLM.Clients.Base;
using AI.LLM.Clients.OpenRouter;

namespace AI.LLM.Services.LLM;

/// <summary>
/// LLM на базе OpenRouter клиента
/// </summary>
public class LLMWithOpenRouterClient : LLMBase
{
    /// <summary>
    /// LLM на базе OpenRouter клиента
    /// </summary>
    /// <param name="settingsLLM">Настройки LLM</param>
    public LLMWithOpenRouterClient(LLMOptions settingsLLM)
        : base(Init(settingsLLM), settingsLLM) { }

    // Инициализация для конструктора
    private static ChatLLMApi Init(LLMOptions openRouterSettings)
    {
        OpenRouterModelApi client = new OpenRouterModelApi(
            apiKey: openRouterSettings.ApiKey,
            modelName: openRouterSettings.ModelName,
            prompt: openRouterSettings.SystemPrompt
            );

        // Применяем провайдера если указан
        if (openRouterSettings.PreferredProvider != null)
            client.PreferredProvider = openRouterSettings.PreferredProvider;

        return client;
    }
}
