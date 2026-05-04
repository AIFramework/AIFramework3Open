# Управление контекстом LLM

## Модель сообщений

Все взаимодействия с LLM строятся на списке **`LLMMessage`** — объектов с ролью и содержимым.

### Роли

| Роль | Enum | Описание |
|------|------|----------|
| `system` | `Roles.System` | Системная инструкция — задаёт поведение модели |
| `user` | `Roles.User` | Сообщение пользователя |
| `assistant` | `Roles.Assistant` | Ответ модели |
| `tool` | `Roles.Tool` | Результат вызова функции |

### Создание сообщений

```csharp
using AI.LLM.Core.Models.Common.Messages;

var system = LLMMessage.CreateMessage(Roles.System,
    "Ты — эксперт по C#. Отвечай кратко.");

var user = LLMMessage.CreateMessage(Roles.User,
    "Чем отличается struct от class?");

var messages = new List<LLMMessage> { system, user };
string answer = await client.SendToLLM(messages);
```

## Многоходовой диалог

Для поддержания контекста беседы добавляйте предыдущие сообщения:

```csharp
var history = new List<LLMMessage>
{
    LLMMessage.CreateMessage(Roles.System, "Ты — ассистент."),
    LLMMessage.CreateMessage(Roles.User, "Что такое LINQ?"),
    LLMMessage.CreateMessage(Roles.Assistant,
        "LINQ — Language Integrated Query, встроенный язык запросов в C#."),
    LLMMessage.CreateMessage(Roles.User, "Приведи пример."),
};

string answer = await client.SendToLLM(history);
```

Каждый новый запрос включает всю историю — модель «помнит» предыдущие ходы.

## FixContext — автокоррекция контекста

Метод **`ContextExtention.FixContext`** автоматически нормализует последовательность
сообщений перед отправкой в API:

1. **Чередование ролей** — OpenAI-совместимые API требуют строгого чередования
   `user` → `assistant`. Если два `assistant`-сообщения идут подряд, между ними
   вставляется пустой `user`.

2. **Tool-сообщения** — корректно группирует `tool`-ответы после соответствующих
   `assistant`-сообщений с `tool_calls`.

3. **Первое сообщение** — гарантирует, что после `system` идёт `user`.

`FixContext` вызывается автоматически в `SendDataLLM.SetMessages`, поэтому
в большинстве случаев вам не нужно думать о порядке сообщений.

## PersonaChat — контекст с персоной

Для сценариев с «персонажем» (чат-бот, NPC в игре) используйте `PersonaChat`:

```csharp
using AI.LLM.Services.Prompts;

var persona = new PersonaChat
{
    BotTag = "Ассистент",
    UserTag = "Пользователь",
};

var context = new PersonaContext();
context.AddFact("Ассистент разбирается в C# и .NET");
context.AddFact("Ассистент отвечает вежливо и кратко");

persona.AddUserMessage("Привет!");
persona.AddAssistantMessage("Здравствуйте! Чем могу помочь?");

string prompt = persona.ToString();
// Результат: структурированный текст с тегами ролей и фактами
```

## FewShot — примеры в промпте

**`FewShotManager`** собирает блок few-shot-примеров для повышения качества ответов:

```csharp
var fsm = new FewShotManager();
fsm.Add(new FewShotElement("Вопрос: 2+2?", "Ответ: 4"));
fsm.Add(new FewShotElement("Вопрос: Столица Франции?", "Ответ: Париж"));

string fewShotBlock = fsm.ToString();
// Подмешивается в системный промпт
```
