using AI.LLM.Agents.Tools;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Services.LLM;

namespace AI.LLM.Agents.Planning;

/// <summary>
/// Fluent-builder для создания <see cref="PlanGenerator"/>.
/// <example>
/// <code>
/// var planner = PlanGeneratorBuilder.Create()
///     .WithLLM(llm)
///     .WithTools(myTools)
///     .WithSkill(new Skill("order_pizza", "Перейди на сайт..."))
///     .WithMaxSteps(15)
///     .Build();
///
/// var plan = await planner.GenerateAsync("Закажи пиццу маргарита");
/// </code>
/// </example>
/// </summary>
public sealed class PlanGeneratorBuilder
{
    private ILLMClient _llm;
    private readonly List<object> _toolInstances = [];
    private readonly List<Skill> _skills = [];
    private readonly PlanGeneratorConfig _config = new();

    private PlanGeneratorBuilder() { }

    /// <summary>Начинает конструирование генератора планов.</summary>
    public static PlanGeneratorBuilder Create() => new();

    /// <summary>Задаёт LLM-клиент через интерфейс.</summary>
    public PlanGeneratorBuilder WithLLM(ILLMClient llm) { _llm = llm; return this; }

    /// <summary>Задаёт LLM-клиент через <see cref="LLMBase"/>.</summary>
    public PlanGeneratorBuilder WithLLM(LLMBase llm) { _llm = llm; return this; }

    /// <summary>Задаёт LLM-клиент через <see cref="ChatLLMApi"/>, обернув в <see cref="LLMBase"/>.</summary>
    public PlanGeneratorBuilder WithLLM(ChatLLMApi chatApi) { _llm = new LLMBase(chatApi); return this; }

    /// <summary>Регистрирует экземпляр с [AgentTool] методами для использования в плане.</summary>
    public PlanGeneratorBuilder WithTools(object toolInstance) { _toolInstances.Add(toolInstance); return this; }

    /// <summary>Добавляет скил (текстовую инструкцию) для LLM.</summary>
    public PlanGeneratorBuilder WithSkill(Skill skill) { _skills.Add(skill); return this; }

    /// <summary>Добавляет несколько скилов.</summary>
    public PlanGeneratorBuilder WithSkills(IEnumerable<Skill> skills) { _skills.AddRange(skills); return this; }

    /// <summary>Максимальное количество шагов в плане.</summary>
    public PlanGeneratorBuilder WithMaxSteps(int n) { _config.MaxSteps = n; return this; }

    /// <summary>Температура генерации (0..2).</summary>
    public PlanGeneratorBuilder WithTemperature(double t) { _config.Temperature = t; return this; }

    /// <summary>Максимальное число токенов в ответе LLM.</summary>
    public PlanGeneratorBuilder WithMaxTokens(int n) { _config.MaxTokens = n; return this; }

    /// <summary>Строит генератор планов.</summary>
    public PlanGenerator Build()
    {
        if (_llm == null)
            throw new InvalidOperationException("LLM-клиент не задан. Вызовите WithLLM().");

        ToolRegistry tools = null;
        if (_toolInstances.Count > 0)
            tools = ToolRegistry.FromObjects([.. _toolInstances]);

        return new PlanGenerator(_llm, tools, _skills, _config);
    }
}
