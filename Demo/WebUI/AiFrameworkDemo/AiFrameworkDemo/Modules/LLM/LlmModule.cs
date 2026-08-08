using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.LLM;

public sealed class LlmModule : LibraryModuleBase
{
    public override string Id => "llm";
    public override string Name => "AI.LLM";
    public override string Description =>
        "Интеграция с LLM через OpenRouter: чат, управление контекстом, " +
        "Semantic Kernel, function calling и плагины";
    public override string Color => "emerald";
    public override string TutorialFolder => "LLM";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"
             stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
          <line x1="9" y1="9" x2="15" y2="9"/>
          <line x1="9" y1="13" x2="13" y2="13"/>
        </svg>
        """;

    #region AlgoChoice

    private static readonly AlgoChoice[] ModelChoices =
    [
        new(0, "google/gemini-2.0-flash-001"),
        new(1, "deepseek/deepseek-chat-v3-0324"),
        new(2, "anthropic/claude-sonnet-4"),
        new(3, "openai/gpt-4.1-mini"),
    ];

    private static readonly AlgoChoice[] ContextModeChoices =
    [
        new(0, "Новый диалог"),
        new(1, "System + User"),
        new(2, "Многоходовой (3 сообщения)"),
    ];

    private static readonly AlgoChoice[] SkDemoChoices =
    [
        new(0, "ChatCompletion через SK"),
        new(1, "Function Calling (погода)"),
        new(2, "Цепочка плагинов"),
    ];

    private static readonly AlgoChoice[] SsrfModeChoices =
    [
        new(0, "Default (блокировка приватных IP)"),
        new(1, "OpenAiOnly (строгий allowlist)"),
        new(2, "CustomHosts (пример кастомного списка)"),
        new(3, "Disabled (отключено)"),
    ];

    #endregion

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        #region 1. Чат с LLM

        new CategoryDef("chat", "Чат с LLM (OpenRouter)",
            "Отправка запроса к LLM через OpenRouter API. Поддержка различных моделей.",
            [
                new AlgoDef("llm_chat", "Простой чат",
                    "Отправьте сообщение любой LLM через OpenRouter и получите ответ. " +
                    "Укажите свой API-ключ, выберите модель и введите запрос.",
                    "AI.LLM.Clients.OpenRouter.OpenRouterModelApi",
                    "chat.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель через OpenRouter")
                            { Choices = ModelChoices },
                        new AlgoParam("temperature", "Temperature", 0, 20, 7, 1, "×0.1",
                            "Случайность генерации (0 = детерминированная, 20 = максимальная)"),
                        new AlgoParam("maxTokens", "Max tokens", 50, 4000, 512, 50, "tok",
                            "Максимальное кол-во токенов в ответе"),
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Ваш запрос к модели",
                            TextDefault: "Объясни кратко, что такое нейронная сеть."),
                    ]),
            ]),

        #endregion

        #region 1a. Цикл ReAct

        new CategoryDef("react", "Цикл ReAct",
            "Рассуждение → Действие → Наблюдение: модель сама решает, каким инструментом воспользоваться, и весь её след виден пошагово.",
            [
                new AlgoDef("react_loop", "ReAct с инструментами",
                    "Три детерминированных инструмента (калькулятор, дата, справочник) и полный след цикла: " +
                    "что модель подумала, что вызвала и что получила в ответ.",
                    "AI.LLM.Agents.ReAct.ReActEngine",
                    "react.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель через OpenRouter")
                            { Choices = ModelChoices },
                        new AlgoParam("policy", "Способ решений", 0, 1, 0, 1, "",
                            "Нативный function calling поддерживают не все модели; структурированный JSON работает с любой")
                            { Choices = [
                                new(0, "NativeToolCalling"),
                                new(1, "StructuredJson"),
                            ]},
                        new AlgoParam("maxIterations", "Лимит шагов", 1, 15, 6, 1, "шт.",
                            "Сколько итераций цикла разрешено до принудительной остановки"),
                        new AlgoParam("_question", "Запрос", 0, 0, 0, 0, "",
                            "Задача, для которой модель должна выбрать инструменты",
                            TextDefault: "Сколько будет 17 * 23, и какой сегодня день недели? Ответь одним предложением."),
                    ]),
            ]),

        #endregion

        #region 2. Контекст и сообщения

        new CategoryDef("context", "Управление контекстом",
            "Демонстрация работы с контекстом: системный промпт, многоходовой диалог, FixContext.",
            [
                new AlgoDef("llm_context", "Контекст и роли",
                    "Показывает, как формируется список сообщений (system, user, assistant) " +
                    "и как FixContext нормализует их для API.",
                    "AI.LLM.Utilities.Extensions.ContextExtention",
                    "context.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("ctxMode", "Режим контекста", 0, 2, 0, 1, "",
                            "Какой набор сообщений отправить")
                            { Choices = ContextModeChoices },
                        new AlgoParam("_system", "System prompt", 0, 0, 0, 0, "",
                            "Системная инструкция для модели",
                            TextDefault: "Ты — эксперт по C# и .NET. Отвечай кратко и по делу."),
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Запрос пользователя",
                            TextDefault: "Чем отличается struct от class?"),
                    ]),
            ]),

        #endregion

        #region 3. Semantic Kernel

        new CategoryDef("semantic_kernel", "Semantic Kernel",
            "Интеграция AI.LLM с Microsoft Semantic Kernel: ChatCompletion, function calling, плагины.",
            [
                new AlgoDef("sk_demo", "SK: Chat, Plugins, Functions",
                    "Демонстрация Semantic Kernel: отправка сообщений через IChatCompletionService, " +
                    "определение и вызов функций (tool calling), цепочка плагинов.",
                    "AI.LLM.Integration.SemanticKernel.SharpGPTChatCompletionService",
                    "semantic_kernel.md",
                    [
                        new AlgoParam("_apikey", "API-ключ OpenRouter", 0, 0, 0, 0, "",
                            "Ключ из https://openrouter.ai/keys",
                            TextDefault: ""),
                        new AlgoParam("model", "Модель", 0, 3, 0, 1, "", "LLM-модель")
                            { Choices = ModelChoices },
                        new AlgoParam("skMode", "Демо-сценарий", 0, 2, 0, 1, "",
                            "Что продемонстрировать")
                            { Choices = SkDemoChoices },
                        new AlgoParam("_message", "Сообщение", 0, 0, 0, 0, "",
                            "Запрос пользователя",
                            TextDefault: "Какая сейчас погода в Москве?"),
                    ]),
            ]),

        #endregion

        #region 4. Генерация изображений / SSRF Guard

        new CategoryDef("image_gen", "Генерация изображений",
            "APIImageGenerator с защитой от SSRF: белые списки хостов, блокировка приватных IP, конфигурируемые политики.",
            [
                new AlgoDef("ssrf_guard", "SSRF Guard: проверка URL",
                    "Показывает, как SsrfGuardOptions защищает APIImageGenerator от SSRF-атак. " +
                    "Выберите режим защиты и посмотрите, какие URL пропускаются, а какие блокируются. " +
                    "API-ключ не требуется — демо работает без сетевых запросов.",
                    "AI.LLM.Clients.ImageGeneration.SsrfGuardOptions",
                    "image_generation.md",
                    [
                        new AlgoParam("ssrfMode", "Режим защиты", 0, 3, 0, 1, "",
                            "Конфигурация SsrfGuardOptions")
                            { Choices = SsrfModeChoices },
                        new AlgoParam("_customUrl", "Свой URL для проверки", 0, 0, 0, 0, "",
                            "Введите любой URL — увидите, пройдёт ли он фильтр",
                            TextDefault: "https://your-cdn.example.com/image.png"),
                    ]),
            ]),

        #endregion
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings) =>
        LlmDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
