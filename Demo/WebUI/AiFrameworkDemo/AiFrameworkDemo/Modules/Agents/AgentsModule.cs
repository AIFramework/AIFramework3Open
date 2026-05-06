using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Agents;

public sealed class AgentsModule : LibraryModuleBase
{
    public override string Id => "agents";
    public override string Name => "AI.LLM.Agents";
    public override string Description =>
        "Агентный фреймворк: цикл ReAct, инструменты [AgentTool], " +
        "память (скользящее окно, векторная, суммаризация), " +
        "планирование (PlanGenerator), оркестрация (PlanningAgent), " +
        "GuardRails, MCP-сервер";
    public override string Color => "violet";
    public override string TutorialFolder => "LLM";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"
             stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="8" r="4"/>
          <path d="M6 20v-2a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v2"/>
          <path d="M16 4l2-2"/>
          <path d="M18 6l2-2"/>
        </svg>
        """;

    private static readonly AlgoChoice[] ModelChoices =
    [
        new(0, "google/gemini-2.0-flash-001"),
        new(1, "deepseek/deepseek-chat-v3-0324"),
        new(2, "anthropic/claude-sonnet-4"),
        new(3, "openai/gpt-4.1-mini"),
    ];

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        #region 1. Агент (ReAct)

        new CategoryDef("agent_react", "Агент (ReAct)",
            "Запуск автономного AI-агента с циклом Reason+Act.",
            [
                new AlgoDef("agent_basic", "Базовый агент",
                    "Простейший агент без инструментов. " +
                    "Один LLM-запрос с настраиваемой температурой.",
                    "AI.LLM.Agents.Agent",
                    "agents.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("temperature", "Temperature", 0, 20, 1, 1, "×0.1",
                            "Случайность генерации"),
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Запрос к агенту",
                            TextDefault: "Объясни кратко, что такое ReAct-агент."),
                    ]),

                new AlgoDef("agent_with_tools", "Агент с инструментами",
                    "Цикл ReAct с function calling. Агент сам решает, " +
                    "когда вызвать инструмент вычисления статистики.",
                    "AI.LLM.Agents.Agent",
                    "agents.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Запрос к агенту (попросите вычислить статистику)",
                            TextDefault: "Вычисли среднее и стандартное отклонение для чисел: 2, 5, 8, 11, 14, 17"),
                    ]),

                new AlgoDef("agent_sk", "Агент через Semantic Kernel",
                    "Запуск LLM через SK Kernel с сохранением биллинга. " +
                    "Инструменты зарегистрированы как KernelPlugin.",
                    "AI.LLM.Agents.Agent",
                    "agents.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Запрос к SK-агенту",
                            TextDefault: "Вычисли статистику для чисел: 3, 7, 12, 5, 9"),
                    ]),
            ]),

        #endregion

        #region 2. Инструменты

        new CategoryDef("tools", "Инструменты",
            "Система инструментов: атрибуты [AgentTool], ToolRegistry, JSON Schema.",
            [
                new AlgoDef("tool_registry", "Реестр инструментов",
                    "Автосканирование методов с атрибутом [AgentTool] " +
                    "и генерация JSON Schema для LLM.",
                    "AI.LLM.Agents.Tools.ToolRegistry",
                    "tools.md",
                    []),

                new AlgoDef("tool_execution", "Вызов инструмента",
                    "Прямой вызов инструмента через ToolRegistry " +
                    "без агентного цикла.",
                    "AI.LLM.Agents.Tools.ToolRegistry",
                    "tools.md",
                    [
                        new AlgoParam("_numbers", "Числа", 0, 0, 0, 0, "",
                            "Числа через запятую для вычисления статистики",
                            TextDefault: "1, 3, 5, 7, 9, 11, 13"),
                    ]),
            ]),

        #endregion

        #region 3. Память

        new CategoryDef("memory", "Память",
            "Стратегии памяти агента: скользящее окно, суммаризация.",
            [
                new AlgoDef("memory_sliding", "Скользящее окно",
                    "Хранит последние N сообщений диалога. " +
                    "Старые сообщения автоматически вытесняются.",
                    "AI.LLM.Agents.Memory.SlidingWindowMemory",
                    "agents.md",
                    [
                        new AlgoParam("windowSize", "Размер окна", 2, 50, 10, 2, "сообщений",
                            "Максимальное число сообщений в памяти"),
                    ]),
            ]),

        #endregion

        #region 4. Мультимодальный агент

        new CategoryDef("multimodal", "Мультимодальный агент",
            "Цикл Observe-Reason-Act: агент видит и действует.",
            [
                new AlgoDef("agent_multimodal", "Observe-Reason-Act",
                    "Мультимодальный агент с IObservationProvider. " +
                    "Получает изображение, анализирует и использует инструменты.",
                    "AI.LLM.Agents.Agent",
                    "multimodal_agents.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель (нужна vision)")
                            { Choices = ModelChoices },
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Запрос к мультимодальному агенту",
                            TextDefault: "Опиши что ты видишь на изображении и вычисли площадь прямоугольника 640×480"),
                    ]),
            ]),

        #endregion

        #region 5. Планирование

        new CategoryDef("planning", "Планирование",
            "Генератор планов на LLM с ярусным параллелизмом (алгоритм Кана).",
            [
                new AlgoDef("plan_generate", "Генерация плана",
                    "LLM разбивает задачу на шаги с зависимостями. " +
                    "TopologicalSort (Кан) формирует ярусы для параллельного выполнения.",
                    "AI.LLM.Agents.Planning.PlanGenerator",
                    "planning.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("maxSteps", "Макс. шагов", 3, 30, 15, 1, "",
                            "Максимальное число шагов в плане"),
                        new AlgoParam("_goal", "Задача", 0, 0, 0, 0, "",
                            "Задача для планирования",
                            TextDefault: "Разработай и протестируй REST API для интернет-магазина книг"),
                        new AlgoParam("_skill", "Скил (опц.)", 0, 0, 0, 0, "",
                            "Текстовая инструкция-навык (опционально)",
                            TextDefault: ""),
                    ]),

                new AlgoDef("plan_visualize", "Визуализация плана",
                    "Генерирует план через LLM и визуализирует дерево ярусов " +
                    "в SVG, Mermaid и текстовом формате.",
                    "AI.LLM.Agents.Planning.PlanTreeVisualizer",
                    "planning.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("maxSteps", "Макс. шагов", 3, 20, 10, 1, "",
                            "Максимальное число шагов"),
                        new AlgoParam("_goal", "Задача", 0, 0, 0, 0, "",
                            "Задача для планирования",
                            TextDefault: "Создай веб-приложение для чата с авторизацией и базой данных"),
                    ]),
            ]),

        #endregion

        #region 6. Оркестратор (PlanningAgent)

        new CategoryDef("orchestrator", "Оркестратор",
            "PlanningAgent: авто-план → поярусное выполнение → retry шагов → replan при провале. " +
            "Память из выполненных шагов передаётся агенту как контекст.",
            [
                new AlgoDef("planning_agent", "PlanningAgent",
                    "Высокоуровневый оркестратор: генерирует план через LLM, " +
                    "затем поярусно выполняет каждый шаг через внутренний ReAct-агент. " +
                    "При неудаче шага — повторяет до MaxStepRetries раз, " +
                    "при исчерпании попыток — перегенерирует план с контекстом ошибки. " +
                    "История выполненных шагов хранится в StepMemory и автоматически " +
                    "передаётся агенту при каждом следующем шаге.",
                    "AI.LLM.Agents.Orchestration.PlanningAgent",
                    "planning.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("maxStepRetries", "Retry на шаг", 0, 3, 2, 1, "",
                            "Максимум повторных попыток для одного шага"),
                        new AlgoParam("maxReplanAttempts", "Replan попыток", 0, 3, 2, 1, "",
                            "Максимум перепланирований при провале"),
                        new AlgoParam("_goal", "Задача", 0, 0, 0, 0, "",
                            "Задача для оркестратора",
                            TextDefault: "Вычисли среднее и сумму для рядов [1,3,5,7,9] и [2,4,6,8,10], потом найди разницу средних"),
                    ]),
            ]),

        #endregion

        #region 7. MCP

        new CategoryDef("mcp", "MCP-сервер",
            "Model Context Protocol: интерактивный вызов MCP-инструментов.",
            [
                new AlgoDef("mcp_tools_list", "MCP-инструменты",
                    "Интерактивный вызов инструментов, доступных внешним клиентам " +
                    "(Cursor, Claude Desktop) через MCP-сервер.",
                    "AI.LLM.Agents.MCP.McpToolBridge",
                    "mcp.md",
                    [
                        new AlgoParam("tool", "Инструмент", 0, 1, 0, 1, "", "Какой инструмент вызвать")
                        {
                            Choices =
                            [
                                new(0, "compute_statistics"),
                                new(1, "sum_numbers"),
                            ]
                        },
                        new AlgoParam("_args", "Аргументы", 0, 0, 0, 0, "",
                            "Числа через запятую для выбранного инструмента",
                            TextDefault: "2, 5, 8, 11, 14, 17"),
                    ]),
            ]),

        #endregion
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings) =>
        AgentsDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
