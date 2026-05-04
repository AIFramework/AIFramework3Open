# AI-агенты в AIFramework

## Обзор

AIFramework предоставляет полноценный агентный фреймворк с циклом **ReAct** (Reason + Act), системой инструментов, памятью и защитными механизмами. Агент автономно решает задачи, вызывая инструменты и рассуждая на каждом шаге.

## Быстрый старт

```csharp
using AI.LLM.Agents;
using AI.LLM.Clients.Base;
using AI.LLM.Services.LLM;

// 1. Настройка LLM-клиента
var api = new ChatLLMApi(new BaseLLMServerAPI("https://api.openai.com", "sk-..."), "gpt-4o");
var llm = new LLMBase(api);

// 2. Определение инструмента
var tools = new MyTools();

// 3. Создание агента
var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithSystemPrompt("Ты аналитик данных. Используй инструменты для вычислений.")
    .WithTools(tools)
    .WithMaxIterations(10)
    .Build();

// 4. Запуск
var result = await agent.RunAsync("Вычисли среднее и дисперсию для чисел: 1, 5, 3, 7, 2");
Console.WriteLine(result.Answer);
Console.WriteLine($"Шагов: {result.TotalSteps}, Время: {result.Elapsed.TotalSeconds:F1}с");
```

## Определение инструментов

Любой метод можно пометить атрибутом `[AgentTool]` — он автоматически станет доступен агенту:

```csharp
using AI.LLM.Agents.Tools;

public class MyTools
{
    [AgentTool("compute_statistics", "Вычисляет описательную статистику")]
    public string ComputeStats(
        [ToolParameter("Числа через запятую")] string numbers)
    {
        var values = numbers.Split(',').Select(double.Parse).ToArray();
        var vector = new AI.DataStructs.Algebraic.Vector(values);
        var stat = new AI.Statistics.Statistic(vector);
        return $"μ={stat.Expected:F4}, σ={stat.STD:F4}";
    }

    [AgentTool("cluster_data", "Кластеризует данные методом K-Means")]
    public string ClusterData(
        [ToolParameter("JSON-массив точек")] string dataJson,
        [ToolParameter("Число кластеров")] int k = 3)
    {
        // Использование AI.ML.KMeans...
        return "результат кластеризации";
    }
}
```

Подробнее об инструментах: [tools.md](tools.md)

## Подключение памяти

### Скользящее окно (краткосрочная)

Хранит последние N сообщений:

```csharp
using AI.LLM.Agents.Memory;

var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithMemory(new SlidingWindowMemory(maxMessages: 20))
    .Build();
```

### Векторная память (долгосрочная)

Использует эмбеддинги для поиска релевантных воспоминаний:

```csharp
var embedder = /* IEmbedderService */;
var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithMemory(new VectorMemory(embedder, topK: 5))
    .Build();
```

### Суммаризация

Автоматически сжимает историю через LLM при переполнении:

```csharp
var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithMemory(new SummarizationMemory(llm, maxMessages: 30))
    .Build();
```

### Композитная память

Объединяет краткосрочную и долгосрочную:

```csharp
var memory = new CompositeMemory(
    shortTerm: new SlidingWindowMemory(20),
    longTerm: new VectorMemory(embedder));

var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithMemory(memory)
    .Build();
```

## Защитные механизмы (GuardRails)

### HallucinationGuard

Использует AI.ExplainitALL для обнаружения галлюцинаций:

```csharp
using AI.LLM.Agents.Guards;
using AI.ExplainitALL.Metrics;

var checker = new CheckingForHallucinations(simMatrixAlg);
var guard = new HallucinationGuard(checker, threshold: 0.5);

var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithGuard(guard)
    .Build();
```

### Пользовательский Guard

Реализуйте интерфейс `IAgentGuard`:

```csharp
public class ContentPolicyGuard : IAgentGuard
{
    public Task<GuardResult> CheckAsync(string query, string answer, CancellationToken ct)
    {
        if (answer.Contains("запрещённое_слово"))
            return Task.FromResult(GuardResult.Fail("Нарушение политики контента"));
        return Task.FromResult(GuardResult.Pass());
    }
}
```

## События агента

Агент поддерживает событийную модель для мониторинга:

```csharp
var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithTools(tools)
    .Build();

agent.OnStepCompleted += (s, step) =>
    Console.WriteLine($"Шаг {step.StepNumber}: {step.FinishReason}");

agent.OnToolExecuted += (s, result) =>
    Console.WriteLine($"Инструмент {result.ToolName}: {(result.IsSuccess ? "OK" : "ОШИБКА")} ({result.Elapsed.TotalMilliseconds:F0}ms)");

agent.OnCompleted += (s, result) =>
    Console.WriteLine($"Готово за {result.Elapsed.TotalSeconds:F1}с, шагов: {result.TotalSteps}");

await agent.RunAsync("Найди информацию о нейронных сетях");
```

## Полный пример

```csharp
using AI.LLM.Agents;
using AI.LLM.Agents.Guards;
using AI.LLM.Agents.Memory;
using AI.LLM.Agents.Tools;
using AI.LLM.Agents.Tools.Builtin;
using AI.LLM.Clients.Base;
using AI.LLM.Clients.Tavily;
using AI.LLM.Services.LLM;

// LLM
var api = new ChatLLMApi(new BaseLLMServerAPI("https://api.openai.com", "sk-..."), "gpt-4o");
var llm = new LLMBase(api);

// Инструменты
var tavilyClient = new TavilyClient("tvly-...");
var searchTool = new TavilySearchTool(tavilyClient);
var myTools = new MyStatisticsTools();

// Память
var memory = new SlidingWindowMemory(maxMessages: 30);

// Агент
var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithSystemPrompt("Ты исследователь. Ищи информацию и анализируй данные.")
    .WithTools(searchTool)
    .WithTools(myTools)
    .WithMemory(memory)
    .WithMaxIterations(15)
    .WithTemperature(0.2)
    .Build();

var result = await agent.RunAsync("Найди последние данные о ВВП России и вычисли среднегодовой рост");
Console.WriteLine(result.Answer);
```

## Архитектура

```
+---------------------------------------------+
|                  Agent                       |
|  +-------------+  +--------------------+    |
|  |  AgentConfig |  |  Events            |    |
|  +-------------+  |  OnStepCompleted   |    |
|                    |  OnToolExecuted    |    |
|  +-------------+  |  OnCompleted       |    |
|  |  ILLMClient  |  +--------------------+    |
|  +-------------+                             |
|  +-------------+  +--------------------+    |
|  | ToolRegistry |  |  IAgentMemory      |    |
|  +-------------+  +--------------------+    |
|  +-------------+                             |
|  |  IAgentGuard |                            |
|  +-------------+                             |
+--------------┬------------------------------+
               | ReAct loop
               v
    +------------------+
    | 1. Send to LLM   |
    | 2. Parse response |<----------+
    | 3. tool_calls?   |            |
    |    YES -> Execute  |------------+
    |    NO  -> Return   |
    +------------------+
```

## Модели без Function Calling (Prompt Fallback)

Для моделей без нативного function calling агент предоставляет
**prompt-based fallback**. Описания инструментов вставляются в системный промпт,
а вызовы парсятся из JSON-блоков в ответе (fenced и inline).

**Все вызовы идут через `ILLMClient`** — биллинг полностью сохраняется.

```csharp
var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithTools(myTools)
    .WithPromptFallback()   // <- prompt-based FC
    .Build();

var result = await agent.RunAsync("Вычисли статистику для 1, 2, 3, 4, 5");
```

## Интеграция с Semantic Kernel

`LLMClientChatCompletionService` — SK-обёртка над `ILLMClient`.
Все вызовы проходят: **SK -> LLMClientChatCompletionService -> ILLMClient -> ChatLLMApi**.
Биллинг сохранён, reasoning settings применяются.

```csharp
using AI.LLM.Integration.SemanticKernel;
using AI.LLM.Integration.SemanticKernel.Extensions;
using Microsoft.SemanticKernel;

// 1. Создаём Kernel через ILLMClient (биллинг сохранён)
var kernel = Kernel.CreateBuilder()
    .AddSharpGPTChatCompletion(llm)  // ILLMClient
    .Build();

// 2. Регистрируем инструменты как KernelPlugin
kernel.Plugins.Add(
    ToolRegistry.FromObjects(myTools).ToKernelPlugin());

// 3. Используем SK API
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory("Ты аналитик.");
history.AddUserMessage("Вычисли статистику для 1, 2, 3");

var result = await chatService.GetChatMessageContentsAsync(
    history, kernel: kernel);
```

Это открывает доступ ко всей экосистеме SK:
планировщики, фильтры, multi-agent, плагины — с биллингом через ваш `ILLMClient`.

## См. также

- [tools.md](tools.md) — справочник по инструментам
- [mcp.md](mcp.md) — MCP-сервер для внешних клиентов
