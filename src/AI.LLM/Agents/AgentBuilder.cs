using AI.LLM.Agents.Guards;
using AI.LLM.Agents.Memory;
using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.Tools;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Services.LLM;

namespace AI.LLM.Agents;

/// <summary>
/// Fluent-builder для создания <see cref="Agent"/>.
/// Все LLM-вызовы проходят через <see cref="ILLMClient"/> — биллинг сохраняется.
/// </summary>
public sealed class AgentBuilder
{
    private ILLMClient _llm;
    private readonly List<object> _toolInstances = [];
    private IAgentMemory _memory;
    private IAgentGuard _guard;
    private IObservationProvider _observer;
    private readonly AgentConfig _config = new();

    private AgentBuilder() { }

    /// <summary>Начинает конструирование нового агента.</summary>
    public static AgentBuilder Create() => new();

    /// <summary>Задаёт LLM-клиент через интерфейс.</summary>
    public AgentBuilder WithLLM(ILLMClient llm) { _llm = llm; return this; }

    /// <summary>Задаёт LLM-клиент через <see cref="LLMBase"/>.</summary>
    public AgentBuilder WithLLM(LLMBase llm) { _llm = llm; return this; }

    /// <summary>Задаёт LLM-клиент через <see cref="ChatLLMApi"/>, обернув в <see cref="LLMBase"/>.</summary>
    public AgentBuilder WithLLM(ChatLLMApi chatApi) { _llm = new LLMBase(chatApi); return this; }

    /// <summary>Задаёт системный промпт.</summary>
    public AgentBuilder WithSystemPrompt(string prompt) { _config.SystemPrompt = prompt; return this; }

    /// <summary>Регистрирует экземпляр с методами <see cref="AgentToolAttribute"/>.</summary>
    public AgentBuilder WithTools(object toolInstance) { _toolInstances.Add(toolInstance); return this; }

    /// <summary>Подключает память агента.</summary>
    public AgentBuilder WithMemory(IAgentMemory memory) { _memory = memory; return this; }

    /// <summary>Подключает защитный механизм.</summary>
    public AgentBuilder WithGuard(IAgentGuard guard) { _guard = guard; return this; }

    /// <summary>
    /// Подключает поставщик наблюдений для мультимодального цикла Observe-Reason-Act.
    /// После выполнения инструментов агент запрашивает наблюдение (скриншот, камера)
    /// и передаёт изображения в следующий LLM-вызов.
    /// </summary>
    public AgentBuilder WithObserver(IObservationProvider observer) { _observer = observer; return this; }

    /// <summary>Включает/выключает автонаблюдение после выполнения инструментов.</summary>
    public AgentBuilder WithObserveAfterTools(bool enable = true) { _config.ObserveAfterToolExecution = enable; return this; }

    /// <summary>Максимальное число изображений из наблюдения в контексте.</summary>
    public AgentBuilder WithMaxObservationImages(int n) { _config.MaxObservationImages = n; return this; }

    /// <summary>Максимальное число итераций цикла ReAct.</summary>
    public AgentBuilder WithMaxIterations(int n) { _config.MaxIterations = n; return this; }

    /// <summary>Температура генерации.</summary>
    public AgentBuilder WithTemperature(double t) { _config.Temperature = t; return this; }

    /// <summary>Максимальное число токенов в ответе.</summary>
    public AgentBuilder WithMaxTokens(int n) { _config.MaxTokens = n; return this; }

    /// <summary>
    /// Включает prompt-based fallback для моделей без нативного function calling.
    /// Все вызовы по-прежнему идут через ILLMClient — биллинг сохраняется.
    /// </summary>
    public AgentBuilder WithPromptFallback(bool enable = true) { _config.UsePromptFallback = enable; return this; }

    /// <summary>Строит агент.</summary>
    public Agent Build()
    {
        if (_llm == null)
            throw new InvalidOperationException("LLM-клиент не задан. Вызовите WithLLM().");

        ToolRegistry tools = null;
        if (_toolInstances.Count > 0)
            tools = ToolRegistry.FromObjects([.. _toolInstances]);

        return new Agent(_llm, tools, _memory, _guard, _observer, _config);
    }
}
