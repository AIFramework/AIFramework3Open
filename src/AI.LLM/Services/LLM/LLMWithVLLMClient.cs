using AI.LLM.Clients.Base;
using AI.LLM.Clients.VLLM;

namespace AI.LLM.Services.LLM;

/// <summary>
/// LLM на базе vLLM клиента
/// </summary>
public class LLMWithVLLMClient : LLMBase
{
    /// <summary>
    /// LLM на базе vLLM клиента
    /// </summary>
    /// <param name="settingsLLM">Настройки LLM</param>
    public LLMWithVLLMClient(LLMOptions settingsLLM)
        : base(Init(settingsLLM), settingsLLM) { }

    // Инициализация для конструктора
    private static ChatLLMApi Init(LLMOptions vLLMSettings)
    {
        VLLMClient client = new VLLMClient(
            vLLMSettings.ModelName,
            vLLMSettings.SystemPrompt,
            vLLMSettings.Host,
            vLLMSettings.ApiKey
            );

        return client;
    }
}
