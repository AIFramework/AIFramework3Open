namespace AI.LLM.Agents;

/// <summary>Конфигурация агента.</summary>
public sealed class AgentConfig
{
    /// <summary>Системный промпт агента.</summary>
    public string SystemPrompt { get; set; } = "Ты полезный ассистент с искусственным интеллектом.";

    /// <summary>Максимальное число итераций цикла ReAct.</summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>Максимальное число токенов в ответе LLM.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Температура генерации (0..2).</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Если true — описания инструментов вставляются в системный промпт,
    /// а вызовы парсятся из текста. Для моделей без нативного function calling.
    /// Все вызовы идут через <see cref="Core.Abstractions.ILLMClient"/>, биллинг сохраняется.
    /// </summary>
    public bool UsePromptFallback { get; set; }

    /// <summary>
    /// Запрашивать ли наблюдение через <see cref="Multimodal.IObservationProvider"/>
    /// после выполнения инструментов. Актуально для Computer Use и робототехники.
    /// </summary>
    public bool ObserveAfterToolExecution { get; set; } = true;

    /// <summary>
    /// Максимальное число изображений из наблюдения, включаемых в контекст (экономия токенов).
    /// </summary>
    public int MaxObservationImages { get; set; } = 1;
}
