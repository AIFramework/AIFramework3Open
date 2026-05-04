# Интеграция с Semantic Kernel

## Обзор

**AI.LLM** предоставляет адаптеры для
[Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) —
фреймворка оркестрации LLM с поддержкой плагинов, function calling, RAG и агентов.

Интеграция реализована в пространстве имён `AI.LLM.Integration.SemanticKernel`.

## Быстрый старт

```csharp
using AI.LLM.Integration.SemanticKernel.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var kernel = Kernel.CreateBuilder()
    .AddSharpGPTChatCompletion(
        apiKey: "sk-or-...",
        modelName: "google/gemini-2.0-flash-001",
        apiUrl: "https://openrouter.ai/api/v1/chat/completions",
        systemPrompt: "Ты — полезный ассистент.")
    .Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddUserMessage("Что такое нейронная сеть?");

var result = await chat.GetChatMessageContentsAsync(history);
Console.WriteLine(result.First().Content);
```

## Компоненты интеграции

### SharpGPTChatCompletionService

Реализует **`IChatCompletionService`** из Semantic Kernel:

- `GetChatMessageContentsAsync` — отправка `ChatHistory` через `ChatLLMApi`
- `GetStreamingChatMessageContentsAsync` — потоковая генерация

Внутренний адаптер **`MessageAdapter`** конвертирует:
- `ChatHistory` → `List<LLMMessage>` (перед отправкой)
- `ChatCompletionsResponse` → `ChatMessageContent` (после получения)

### SharpGPTEmbeddingService

Реализует **`ITextEmbeddingGenerationService`** для RAG-сценариев:

```csharp
kernel = Kernel.CreateBuilder()
    .AddSharpGPTChatCompletion(chatApi)
    .AddSharpGPTEmbedding(embedderService)
    .Build();
```

## Function Calling (Tool Use)

AI.LLM поддерживает OpenAI-совместимый протокол function calling через `ToolDefinition`:

```csharp
var tool = ToolDefinition.Create(
    "get_weather",
    "Получает погоду в городе",
    """
    {
      "type": "object",
      "properties": {
        "city": { "type": "string", "description": "Город" }
      },
      "required": ["city"]
    }
    """);

var settings = new GenerateSettings
{
    Tools = new List<ToolDefinition> { tool }
};

var response = await chatApi.SendWithContextAsync(messages, settings);

// Проверяем tool_calls в ответе
var toolCalls = response.Choices[0].Message.ToolCalls;
if (toolCalls?.Count > 0)
{
    var call = toolCalls[0];
    // call.Function.Name == "get_weather"
    // call.Function.Arguments == "{\"city\":\"Москва\"}"

    // Выполняем функцию и отправляем результат обратно
    messages.Add(response.Choices[0].Message);   // assistant с tool_calls
    messages.Add(LLMMessage.CreateToolResult(
        call.Id,
        "Погода в Москве: +18°C, переменная облачность"));

    var final = await chatApi.SendWithContextTextAsync(messages);
}
```

## Плагины Semantic Kernel

SK позволяет определять плагины как обычные C# классы с атрибутами:

```csharp
using System.ComponentModel;
using Microsoft.SemanticKernel;

public class WeatherPlugin
{
    [KernelFunction("get_weather")]
    [Description("Получает текущую погоду в городе")]
    public string GetWeather(
        [Description("Название города")] string city)
    {
        // Реальный HTTP-запрос к API погоды
        return $"Погода в {city}: +18°C, облачно";
    }
}

// Регистрация
kernel.ImportPluginFromType<WeatherPlugin>();

// Автоматический вызов плагинов
var settings = new PromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var result = await chat.GetChatMessageContentsAsync(
    history, settings, kernel);
```

При `FunctionChoiceBehavior.Auto()` Semantic Kernel автоматически:
1. Передаёт описания функций в LLM как `tools`
2. Распознаёт `tool_calls` в ответе модели
3. Вызывает соответствующий C# метод
4. Отправляет результат обратно в модель
5. Возвращает финальный ответ пользователю

## Цепочка плагинов

Можно регистрировать несколько плагинов — модель сама решает, какие вызвать:

```csharp
kernel.ImportPluginFromType<WeatherPlugin>();
kernel.ImportPluginFromType<MathPlugin>();
kernel.ImportPluginFromType<DatabasePlugin>();

// Модель может вызвать несколько функций за один запрос
var result = await chat.GetChatMessageContentsAsync(
    history,
    new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
    kernel);
```

## Адаптеры

| Класс | Направление | Описание |
|-------|------------|----------|
| `MessageAdapter` | SK <-> LLM | Конвертация `ChatHistory` <-> `LLMMessage[]` |
| `SettingsAdapter` | SK → LLM | `PromptExecutionSettings` → `GenerateSettings` |
| `SharpGPTChatCompletionService` | SK ← LLM | `IChatCompletionService` обёртка |
| `SharpGPTEmbeddingService` | SK ← LLM | `ITextEmbeddingGenerationService` обёртка |

## Tavily — веб-поиск

AI.LLM включает клиент **Tavily** для поиска и извлечения контента из веба:

```csharp
using AI.LLM.Clients.Tavily;

var tavily = new TavilyClient("tvly-...");
var results = await tavily.SearchAsync(new SearchArgs
{
    Query = "последние новости о .NET 10",
    SearchDepth = SearchDepth.Basic,
    MaxResults = 5,
});

foreach (var r in results.Results)
    Console.WriteLine($"{r.Title}: {r.Url}");
```

Tavily особенно полезен в RAG-пайплайнах совместно с Semantic Kernel.
