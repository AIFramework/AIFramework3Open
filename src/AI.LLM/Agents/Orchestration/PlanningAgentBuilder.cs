using AI.LLM.Agents.Guards;
using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.Planning;
using AI.LLM.Agents.Tools;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Services.LLM;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Fluent-builder для создания <see cref="PlanningAgent"/>.
/// <example>
/// <code>
/// var agent = PlanningAgentBuilder.Create()
///     .WithLLM(llm)
///     .WithTools(myTools)
///     .WithSkills(MySkills.All)
///     .WithSystemPrompt("You are a helpful computer-use agent.")
///     .WithMaxStepRetries(2)
///     .WithMaxReplanAttempts(3)
///     .Build();
///
/// var result = await agent.RunAsync("Открой Notepad и напиши 'Hello'");
/// Console.WriteLine($"Success: {result.Success}, steps: {result.MemoryCells.Count}");
/// </code>
/// </example>
/// </summary>
public sealed class PlanningAgentBuilder
{
    private ILLMClient _llm;
    private readonly List<object> _toolInstances = [];
    private readonly List<Skill> _skills = [];
    private string _systemPrompt = "";
    private IStepValidator _validator;
    private IAgentGuard _guard;
    private IObservationProvider _observer;
    private readonly PlanningAgentConfig _config = new();

    private PlanningAgentBuilder() { }

    /// <summary>Начинает конструирование оркестратора.</summary>
    public static PlanningAgentBuilder Create() => new();

    /// <summary>LLM-клиент через интерфейс.</summary>
    public PlanningAgentBuilder WithLLM(ILLMClient llm) { _llm = llm; return this; }

    /// <summary>LLM-клиент через <see cref="LLMBase"/>.</summary>
    public PlanningAgentBuilder WithLLM(LLMBase llm) { _llm = llm; return this; }

    /// <summary>LLM-клиент через <see cref="ChatLLMApi"/>, оборачивается в <see cref="LLMBase"/>.</summary>
    public PlanningAgentBuilder WithLLM(ChatLLMApi chatApi) { _llm = new LLMBase(chatApi); return this; }

    /// <summary>Регистрирует экземпляр с [AgentTool] методами.</summary>
    public PlanningAgentBuilder WithTools(object instance) { _toolInstances.Add(instance); return this; }

    /// <summary>Добавляет скил для генератора планов.</summary>
    public PlanningAgentBuilder WithSkill(Skill skill) { _skills.Add(skill); return this; }

    /// <summary>Добавляет набор скилов для генератора планов.</summary>
    public PlanningAgentBuilder WithSkills(IEnumerable<Skill> skills) { _skills.AddRange(skills); return this; }

    /// <summary>Системный промпт исполняющего агента.</summary>
    public PlanningAgentBuilder WithSystemPrompt(string prompt) { _systemPrompt = prompt; return this; }

    /// <summary>Кастомный валидатор успешности шага.</summary>
    public PlanningAgentBuilder WithStepValidator(IStepValidator validator) { _validator = validator; return this; }

    /// <summary>Защитный механизм для исполняющего агента.</summary>
    public PlanningAgentBuilder WithGuard(IAgentGuard guard) { _guard = guard; return this; }

    /// <summary>Поставщик наблюдений (скриншоты, камера) для мультимодального режима.</summary>
    public PlanningAgentBuilder WithObserver(IObservationProvider observer) { _observer = observer; return this; }

    /// <summary>Максимальное число повторных попыток для одного шага (default: 2).</summary>
    public PlanningAgentBuilder WithMaxStepRetries(int n) { _config.MaxStepRetries = n; return this; }

    /// <summary>Максимальное число перепланирований за одну задачу (default: 3).</summary>
    public PlanningAgentBuilder WithMaxReplanAttempts(int n) { _config.MaxReplanAttempts = n; return this; }

    /// <summary>Параллельное выполнение шагов одного яруса (default: false).</summary>
    public PlanningAgentBuilder WithParallelTiers(bool enable = true) { _config.ExecuteParallelTiers = enable; return this; }

    /// <summary>Строит <see cref="PlanningAgent"/>.</summary>
    public PlanningAgent Build()
    {
        if (_llm is null)
            throw new InvalidOperationException("LLM not configured. Call WithLLM().");

        var memory = new StepMemory();

        // Фабрика, а не готовый агент: перегрузка RunAsync с явным списком инструментов собирает
        // на задачу СВОЙ экземпляр агента (со своим реестром и своей памятью). Реестр агента
        // фиксируется в конструкторе, поэтому подменить инструменты у уже собранного нельзя.
        var agentFactory = BuildAgent;
        var agent = BuildAgent(
            _toolInstances.Count > 0 ? ToolRegistry.FromObjects([.. _toolInstances]) : null,
            memory);

        var plannerBuilder = PlanGeneratorBuilder.Create().WithLLM(_llm);
        foreach (var t in _toolInstances) plannerBuilder.WithTools(t);
        foreach (var s in _skills)        plannerBuilder.WithSkill(s);
        var planner = plannerBuilder.Build();

        return new PlanningAgent(
            agent, agentFactory, planner, memory,
            _validator ?? new DefaultStepValidator(),
            _config);
    }

    /// <summary>Собирает исполняющий агент с готовым реестром инструментов и памятью шагов.</summary>
    /// <remarks>
    /// Именно реестром, а не экземплярами: инструменты задачи могут быть зарегистрированы с именами
    /// из рантайма (агенты каталога как инструменты), и собрать такой набор из объектов с атрибутами
    /// невозможно.
    /// </remarks>
    private Agent BuildAgent(ToolRegistry registry, StepMemory memory)
    {
        var agentBuilder = AgentBuilder.Create()
            .WithLLM(_llm)
            .WithSystemPrompt(_systemPrompt)
            .WithMemory(memory)
            .WithMaxIterations(30)
            .WithTemperature(0.15);

        if (registry is not null)  agentBuilder.WithToolRegistry(registry);
        if (_guard is not null)    agentBuilder.WithGuard(_guard);
        if (_observer is not null) agentBuilder.WithObserver(_observer);

        return agentBuilder.Build();
    }
}
