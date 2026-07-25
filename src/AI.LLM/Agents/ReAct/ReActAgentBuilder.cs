using AI.LLM.Agents.Guards;
using AI.LLM.Agents.Memory;
using AI.LLM.Agents.Planning;
using AI.LLM.Agents.ReAct.Interop;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Rendering;
using AI.LLM.Agents.ReAct.Synthesis;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Requests;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Сборка <see cref="ReActEngine"/>.
/// </summary>
/// <remarks>
/// В отличие от <see cref="Orchestration.PlanningAgentBuilder"/> здесь не собирается внутренний
/// <see cref="Agent"/>: цикл ReAct не надстраивается над ним, а заменяет его собственным
/// циклом, и вложенный агент означал бы цикл внутри цикла.
/// </remarks>
public sealed class ReActAgentBuilder
{
    private readonly List<IReActToolSource> _toolSources = [];
    private readonly List<IReActTool> _tools = [];
    private readonly List<IReActSkill> _skills = [];
    private readonly ReActConfig _config = new();

    private IReActPolicy _policy;
    private IReActTraceRenderer _renderer;
    private IReActPromptTemplate _template;
    private IReActSynthesizer _synthesizer;
    private IAgentGuard _guard;
    private IAgentMemory _memory;
    private string _systemPrompt;
    private int _maxObservationChars = -1;
    private int _maxTraceChars = -1;

    private ReActAgentBuilder()
    {
    }

    /// <summary>Начинает сборку.</summary>
    public static ReActAgentBuilder Create() => new();

    /// <summary>Задаёт способ принятия решений.</summary>
    /// <param name="policy">Реализация принятия решений.</param>
    public ReActAgentBuilder WithPolicy(IReActPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>Решения через нативные вызовы инструментов.</summary>
    /// <param name="llm">Клиент модели.</param>
    /// <param name="settings">Настройки генерации.</param>
    public ReActAgentBuilder WithNativeToolCalling(ILLMClient llm, GenerateSettings settings = null) =>
        WithPolicy(new NativeToolCallPolicy(llm, settings));

    /// <summary>Решения через структурированный текст (работает с любым поставщиком).</summary>
    /// <param name="llm">Клиент модели.</param>
    /// <param name="settings">Настройки генерации.</param>
    public ReActAgentBuilder WithStructuredJson(ILLMClient llm, GenerateSettings settings = null) =>
        WithPolicy(new StructuredJsonPolicy(llm, settings));

    /// <summary>Решения через произвольное обращение к модели.</summary>
    /// <param name="complete">Обращение к модели.</param>
    public ReActAgentBuilder WithStructuredJson(ReActCompletionDelegate complete) =>
        WithPolicy(new StructuredJsonPolicy(complete));

    /// <summary>Добавляет инструмент.</summary>
    /// <param name="tool">Инструмент.</param>
    public ReActAgentBuilder WithTool(IReActTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools.Add(tool);
        return this;
    }

    /// <summary>Добавляет несколько инструментов.</summary>
    /// <param name="tools">Инструменты.</param>
    public ReActAgentBuilder WithTools(IEnumerable<IReActTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        foreach (IReActTool tool in tools)
        {
            if (tool != null)
                _tools.Add(tool);
        }

        return this;
    }

    /// <summary>Добавляет источник инструментов, вычисляемый на каждый прогон.</summary>
    /// <param name="source">Источник.</param>
    public ReActAgentBuilder WithToolSource(IReActToolSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _toolSources.Add(source);
        return this;
    }

    /// <summary>Подключает инструменты, объявленные атрибутами на методах объекта.</summary>
    /// <param name="toolInstance">Объект с методами <see cref="AI.LLM.Agents.Tools.AgentToolAttribute"/>.</param>
    public ReActAgentBuilder WithAttributedTools(object toolInstance)
    {
        ArgumentNullException.ThrowIfNull(toolInstance);
        return WithToolSource(ToolRegistryToolSource.FromObjects(toolInstance));
    }

    /// <summary>Подключает готовый реестр инструментов.</summary>
    /// <param name="registry">Реестр.</param>
    public ReActAgentBuilder WithToolRegistry(AI.LLM.Agents.Tools.ToolRegistry registry) =>
        WithToolSource(new ToolRegistryToolSource(registry));

    /// <summary>Задаёт базовый системный промпт.</summary>
    /// <param name="prompt">Промпт вызывающей стороны.</param>
    public ReActAgentBuilder WithSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
        return this;
    }

    /// <summary>Заменяет тексты цикла собственными.</summary>
    /// <param name="template">Шаблон текстов.</param>
    public ReActAgentBuilder WithPromptTemplate(IReActPromptTemplate template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
        return this;
    }

    /// <summary>Добавляет навык.</summary>
    /// <param name="skill">Навык.</param>
    public ReActAgentBuilder WithSkill(IReActSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills.Add(skill);
        return this;
    }

    /// <summary>Добавляет навыки планировщика, приводя их к навыкам цикла.</summary>
    /// <param name="skills">Навыки планировщика.</param>
    public ReActAgentBuilder WithSkills(IEnumerable<Skill> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        foreach (Skill skill in skills)
        {
            if (skill != null)
                _skills.Add(ReActSkill.FromPlanningSkill(skill));
        }

        return this;
    }

    /// <summary>
    /// Подключает проверку итогового ответа. Несоответствие логируется — guard'ы этого
    /// контракта предупреждают, но не блокируют и не переписывают ответ.
    /// </summary>
    /// <param name="guard">Проверка ответа.</param>
    public ReActAgentBuilder WithGuard(IAgentGuard guard)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        return this;
    }

    /// <summary>
    /// Подключает память диалога. Движок сам получит из неё историю перед прогоном и сохранит
    /// взаимодействие после — при этом системный промпт остаётся за движком, память его не
    /// перекрывает.
    /// </summary>
    /// <param name="memory">Память диалога.</param>
    public ReActAgentBuilder WithMemory(IAgentMemory memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        return this;
    }

    /// <summary>Заменяет способ рендеринга следа.</summary>
    /// <param name="renderer">Рендерер.</param>
    public ReActAgentBuilder WithTraceRenderer(IReActTraceRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        return this;
    }

    /// <summary>Задаёт лимиты объёма наблюдений.</summary>
    /// <param name="maxObservationChars">Предел одного наблюдения.</param>
    /// <param name="maxTraceChars">Предел всего следа.</param>
    public ReActAgentBuilder WithObservationLimits(int maxObservationChars, int maxTraceChars)
    {
        _maxObservationChars = maxObservationChars;
        _maxTraceChars = maxTraceChars;
        _config.MaxObservationChars = maxObservationChars;
        _config.MaxScratchpadChars = maxTraceChars;
        return this;
    }

    /// <summary>Подключает синтез итогового ответа.</summary>
    /// <param name="synthesizer">Синтез.</param>
    /// <param name="mode">Когда его запускать.</param>
    public ReActAgentBuilder WithSynthesizer(
        IReActSynthesizer synthesizer, ReActSynthesisMode mode = ReActSynthesisMode.WhenNoAnswer)
    {
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
        _config.SynthesisMode = mode;
        return this;
    }

    /// <summary>Подключает синтез поверх клиента библиотеки.</summary>
    /// <param name="llm">Клиент модели.</param>
    /// <param name="settings">Настройки генерации.</param>
    /// <param name="instruction">Инструкция синтеза.</param>
    /// <param name="mode">Когда запускать синтез.</param>
    public ReActAgentBuilder WithLlmSynthesis(
        ILLMClient llm,
        GenerateSettings settings = null,
        string instruction = null,
        ReActSynthesisMode mode = ReActSynthesisMode.WhenNoAnswer) =>
        WithSynthesizer(DelegateReActSynthesizer.FromLlm(llm, settings, instruction), mode);

    /// <summary>Задаёт предельное число шагов.</summary>
    /// <param name="count">Число шагов.</param>
    public ReActAgentBuilder WithMaxIterations(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Число шагов должно быть положительным.");

        _config.MaxIterations = count;
        return this;
    }

    /// <summary>Задаёт предел времени на прогон.</summary>
    /// <param name="duration">Предел времени.</param>
    public ReActAgentBuilder WithMaxDuration(TimeSpan duration)
    {
        _config.MaxDuration = duration;
        return this;
    }

    /// <summary>Задаёт предел времени на один вызов инструмента.</summary>
    /// <param name="timeout">Предел времени.</param>
    public ReActAgentBuilder WithToolTimeout(TimeSpan timeout)
    {
        _config.ToolTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Разрешает исполнять несколько инструментов одновременно. По умолчанию один:
    /// инструменты не обязаны быть потокобезопасными.
    /// </summary>
    /// <param name="count">Предел одновременных вызовов.</param>
    public ReActAgentBuilder WithMaxParallelTools(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Предел параллелизма должен быть положительным.");

        _config.MaxParallelTools = count;
        return this;
    }

    /// <summary>Настраивает защиту от зацикливания.</summary>
    /// <param name="maxRepeats">Сколько повторов одного действия допускается.</param>
    /// <param name="maxConsecutiveFailures">Сколько падений одного инструмента подряд допускается.</param>
    public ReActAgentBuilder WithRepeatedActionPolicy(int maxRepeats, int maxConsecutiveFailures)
    {
        _config.MaxRepeatedActions = Math.Max(0, maxRepeats);
        _config.MaxConsecutiveFailures = Math.Max(1, maxConsecutiveFailures);
        return this;
    }

    /// <summary>Сколько раз подсказывать модели правильное имя инструмента.</summary>
    /// <param name="count">Бюджет подсказок.</param>
    public ReActAgentBuilder WithUnknownToolBudget(int count)
    {
        _config.UnknownToolBudget = Math.Max(0, count);
        return this;
    }

    /// <summary>Сколько раз просить модель переформулировать неразобранный ответ.</summary>
    /// <param name="count">Бюджет попыток.</param>
    public ReActAgentBuilder WithMalformedDecisionBudget(int count)
    {
        _config.MalformedDecisionBudget = Math.Max(0, count);
        return this;
    }

    /// <summary>Собирает движок.</summary>
    /// <exception cref="InvalidOperationException">Не задан способ принятия решений.</exception>
    public ReActEngine Build()
    {
        if (_policy == null)
            throw new InvalidOperationException(
                "Способ принятия решений не задан. Вызовите WithPolicy(), WithNativeToolCalling() или WithStructuredJson().");

        var sources = new List<IReActToolSource>(_toolSources);
        if (_tools.Count > 0)
            sources.Insert(0, new StaticToolSource(_tools));

        IReActTraceRenderer renderer = _renderer ?? new TailBudgetTraceRenderer(
            _maxObservationChars > 0 ? _maxObservationChars : _config.MaxObservationChars,
            _maxTraceChars > 0 ? _maxTraceChars : _config.MaxScratchpadChars);

        return new ReActEngine(
            _policy,
            sources,
            _skills,
            renderer,
            _template ?? new DefaultReActPromptTemplate(),
            _synthesizer,
            _guard,
            _memory,
            _config,
            _systemPrompt);
    }

    /// <summary>Источник для инструментов, заданных при сборке и одинаковых на всех прогонах.</summary>
    private sealed class StaticToolSource(IReadOnlyList<IReActTool> tools) : IReActToolSource
    {
        private readonly IReadOnlyList<IReActTool> _tools = tools;

        public IEnumerable<IReActTool> GetTools(ReActRunContext context) => _tools;
    }
}
