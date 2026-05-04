using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.ToolCalling;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AI.LLM.Integration.SemanticKernel.Adapters;

/// <summary>
/// Двунаправленное преобразование между <see cref="GenerateSettings"/> и SK <see cref="PromptExecutionSettings"/>.
/// </summary>
public static class SettingsAdapter
{
    /// <summary>
    /// Конвертирует SK PromptExecutionSettings в GenerateSettings (AI.LLM).
    /// Если передан <see cref="OpenAIPromptExecutionSettings"/>, извлекаются все доступные параметры.
    /// </summary>
    public static GenerateSettings FromSKSettings(PromptExecutionSettings skSettings)
    {
        if (skSettings == null)
            return new GenerateSettings();

        if (skSettings is OpenAIPromptExecutionSettings openAiSettings)
        {
            return FromOpenAISettings(openAiSettings);
        }

        // Базовый PromptExecutionSettings — извлекаем что можем из ExtensionData
        var gs = new GenerateSettings(
            temperature: GetExtensionValue<double>(skSettings, "temperature", 0.1),
            maxTokens: GetExtensionValue<int?>(skSettings, "max_tokens", null),
            topP: GetExtensionValue<double?>(skSettings, "top_p", null)
        );

        return gs;
    }

    /// <summary>
    /// Конвертирует GenerateSettings (AI.LLM) в SK OpenAIPromptExecutionSettings.
    /// Параметры, которых нет в SK (MinTokens, RepetitionPenalty, StreamId и т.д.),
    /// передаются через ExtensionData для дальнейшего доступа.
    /// </summary>
    public static OpenAIPromptExecutionSettings ToSKSettings(GenerateSettings gs)
    {
        gs ??= new GenerateSettings();

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = gs.Temperature ?? 0.1,
            TopP = gs.TopP ?? 0.95,
            MaxTokens = gs.MaxTokens,
            Logprobs = gs.LogProbs ?? false,
            TopLogprobs = gs.TopLogprobs,
        };

        // Параметры без прямого аналога — сохраняем в ExtensionData
        var extra = new Dictionary<string, object>();

        if (gs.TopK.HasValue)
            extra["top_k"] = gs.TopK.Value;
        if (gs.MinTokens.HasValue)
            extra["min_tokens"] = gs.MinTokens.Value;
        if (gs.RepetitionPenalty.HasValue)
            extra["repetition_penalty"] = gs.RepetitionPenalty.Value;
        if (gs.ReasoningSettings != null)
            extra["reasoning"] = gs.ReasoningSettings;
        if (!string.IsNullOrEmpty(gs.ReasoningEffort))
            extra["reasoning_effort"] = gs.ReasoningEffort;

        if (extra.Count > 0)
            settings.ExtensionData = extra;

        return settings;
    }

    private static GenerateSettings FromOpenAISettings(OpenAIPromptExecutionSettings s)
    {
        var gs = new GenerateSettings(
            temperature: s.Temperature ?? 0.1,
            topP: s.TopP,
            maxTokens: s.MaxTokens
        );

        gs.LogProbs = s.Logprobs;
        gs.TopLogprobs = s.TopLogprobs;

        // Извлекаем параметры AI.LLM из ExtensionData если есть
        if (s.ExtensionData != null)
        {
            if (s.ExtensionData.TryGetValue("repetition_penalty", out var rp) && rp is double rpVal)
                gs.RepetitionPenalty = rpVal;
            if (s.ExtensionData.TryGetValue("reasoning_effort", out var re) && re is string reStr)
                gs.ReasoningEffort = reStr;
            if (s.ExtensionData.TryGetValue("tools", out var tools) && tools is List<ToolDefinition> toolsList)
                gs.Tools = toolsList;
        }

        return gs;
    }

    private static T GetExtensionValue<T>(PromptExecutionSettings settings, string key, T defaultValue)
    {
        if (settings.ExtensionData != null &&
            settings.ExtensionData.TryGetValue(key, out var value) &&
            value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }
}
