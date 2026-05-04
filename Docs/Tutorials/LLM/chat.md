# Чат с LLM через OpenRouter

## Обзор

**AI.LLM** предоставляет унифицированный интерфейс для работы с любыми LLM через
[OpenRouter](https://openrouter.ai/) — прокси-сервис, агрегирующий сотни моделей
(OpenAI, Anthropic, Google, DeepSeek, Meta и др.) под единым API.

## Архитектура

```
Ваше приложение
    └─ LLMWithOpenRouterClient (LLMBase)
           └─ OpenRouterModelApi (ChatLLMApi)
                  └─ HTTP POST → https://openrouter.ai/api/v1/chat/completions
```

- **`ChatLLMApi`** — базовый класс для всех LLM-клиентов. Реализует OpenAI-совместимый
  протокол: отправка `messages`, разбор SSE-стрима, поддержка `tool_calls`.
- **`OpenRouterModelApi`** — наследник `ChatLLMApi` с предустановленным URL OpenRouter.
- **`LLMWithOpenRouterClient`** — высокоуровневая обёртка с `LLMOptions`.

## Быстрый старт

```csharp
using AI.LLM.Services.LLM;

var options = new LLMOptions
{
    ApiKey    = "sk-or-...",              // ключ OpenRouter
    ModelName = "google/gemini-2.0-flash-001",
    Temperature = 0.7,
};

var client = new LLMWithOpenRouterClient(options);

string answer = await client.SendToLLM("Что такое нейронная сеть?");
Console.WriteLine(answer);
```

## Параметры генерации

Класс `GenerateSettings` позволяет тонко настроить генерацию:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Temperature` | `double?` | Случайность (0.0–2.0) |
| `TopP` | `double?` | Nucleus sampling |
| `MaxTokens` | `int?` | Лимит длины ответа |
| `RepetitionPenalty` | `double?` | Штраф за повторения |
| `Tools` | `List<ToolDefinition>` | Определения функций для tool calling |
| `ResponseFormat` | `ResponseFormat?` | Структурированный вывод (JSON Schema) |

## Поддерживаемые модели

Через OpenRouter доступны сотни моделей. Примеры:

- `google/gemini-2.0-flash-001` — быстрая и бесплатная модель Google
- `deepseek/deepseek-chat-v3-0324` — мощная open-source модель
- `anthropic/claude-sonnet-4` — Claude от Anthropic
- `openai/gpt-4.1-mini` — компактная модель OpenAI

## Стриминг

Для потоковой обработки ответа реализуйте интерфейс `IStreamHandler`:

```csharp
public class MyStreamHandler : IStreamHandler
{
    public Task<string> StartAsync(...) { /* начало стрима */ }
    public Task<bool> SendAsync(...)    { /* очередной чанк */ }
}

var client = new LLMWithOpenRouterClient(options, new MyStreamHandler());
```

## Провайдеры

OpenRouter позволяет выбрать конкретного провайдера для модели:

```csharp
options.PreferredProvider = new ProviderPreference
{
    Order = ["DeepInfra", "Together"]
};
```

Класс `OpenRouterProviders` содержит справочник рекомендованных провайдеров.
