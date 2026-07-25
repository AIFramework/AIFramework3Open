# Цикл ReAct

## Обзор

`AI.LLM.Agents.ReAct` — движок цикла **Рассуждение → Действие → Наблюдение**, независимый от
поставщика модели. В отличие от `Agent` (см. [agents.md](agents.md)), он умеет работать и с
моделями без нативных вызовов инструментов, отдаёт события потоком по мере работы и не теряет
собранное при исчерпании бюджета.

```csharp
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;

var engine = ReActAgentBuilder.Create()
    .WithNativeToolCalling(llm)                 // либо .WithStructuredJson(llm)
    .WithSystemPrompt("Ты аналитик. Отвечай кратко.")
    .WithTool(DelegateReActTool.FromText(
        "web_search", "поиск в интернете", (query, ct) => SearchAsync(query, ct)))
    .WithMaxIterations(8)
    .WithLlmSynthesis(llm)
    .Build();

var result = await engine.RunAsync("что нового по теме X");
Console.WriteLine(result.Answer);
Console.WriteLine(result.StopReason);           // FinalAnswer / IterationLimit / …
```

## Два способа принимать решения

| Реализация | Когда |
|---|---|
| `NativeToolCallPolicy` | модель умеет `tool_calls` — имена и аргументы приходят разобранными |
| `StructuredJsonPolicy` | модель отвечает текстом: `{"thought","action","action_input"}` либо `{"final"}` |

Обе — проекции одного и того же следа прогона, поэтому взаимозаменяемы. Свой поставщик
подключается лямбдой, без класса-адаптера:

```csharp
.WithStructuredJson((system, user, ct) => myClient.CompleteAsync(system, user, ct))
```

## Инструменты

Инструмент — поток, чтобы длинные операции показывали прогресс. Для простых случаев есть
фабрики:

```csharp
DelegateReActTool.FromText("echo", "повторяет текст", (arg, ct) => Task.FromResult(arg));
DelegateReActTool.FromOutcome("save", "сохраняет", async (inv, ct) => ReActToolOutcome.Success("ок"));
DelegateReActTool.FromStream("build", "собирает документ", (inv, ct) => BuildAsync(inv, ct));
```

Инструменты на атрибутах (`[AgentTool]`) подключаются мостом:

```csharp
.WithAttributedTools(new MyTools())
```

**Терминальный инструмент** завершает ход собственным результатом, минуя синтез — это нужно,
когда результат инструмента и есть ответ (готовая форма, изображение, изменённый документ):

```csharp
ReActToolOutcome.Terminal("картинка готова", payload: myReply);
```

`payload` движок не интерпретирует и вернёт в `result.Payload` той же ссылкой.

**Метки** (`Tags`) позволяют вызывающей стороне принимать решения по смыслу инструмента,
а не по имени — список имён в условии однажды забудут дополнить.

## Стриминг

```csharp
await foreach (var e in engine.StreamAsync(query, ct))
{
    switch (e)
    {
        case ReActEvent.Thought t:       Show(t.Text); break;
        case ReActEvent.ToolStarted s:   Show(s.Action.ToolName); break;
        case ReActEvent.ToolProgress p:  Show(p.Payload); break;   // нагрузка потребителя
        case ReActEvent.Completed c:     Done(c.Result); break;
    }
}
```

Поток холодный и не бросает исключений, кроме отмены: сбой приходит терминальным событием
с `ReActStopReason.EngineFailure` и текстом в `Result.Error`. Экземпляр движка не хранит
состояния прогона — один движок обслуживает параллельные запуски.

## Защита от зацикливания

Настраивается билдером, работает без участия вызывающей стороны:

- **повтор действия** — вызов с тем же аргументом не исполняется заново, возвращается
  прежнее наблюдение с замечанием;
- **несуществующий инструмент** — модели уходит подсказка со списком доступных имён, ход
  продолжается (а не завершается молча);
- **неразобранный ответ** — отдельное состояние `Malformed` со своим бюджетом попыток;
- **исчерпание лимита** — ответ всё равно пишется по собранным наблюдениям;
- `WithToolTimeout`, `WithMaxDuration`, `WithMaxParallelTools`.

## Объём наблюдений

`TailBudgetTraceRenderer` режет след **с хвоста истории**: вытесняются самые старые шаги,
свежие сохраняются целиком. Обратный порядок означает, что модель перестаёт видеть результаты
собственных последних действий и начинает их повторять.

```csharp
.WithObservationLimits(maxObservationChars: 1500, maxTraceChars: 12_000)
```

## Синтез ответа

Канал принятия решений не годится для итогового текста: он работает в урезанном бюджете,
часто в JSON-режиме и при нулевой температуре. Поэтому итог пишет отдельный вызов:

```csharp
.WithLlmSynthesis(llm, mode: ReActSynthesisMode.Always)
```

`Always` нужен там, где текст из `final` следует считать только черновиком; `WhenNoAnswer`
(по умолчанию) запускает синтез, лишь когда своего текста у цикла не осталось.

## Навыки, память, guard'ы

```csharp
.WithSkill(new ReActSkill("оформление", "Оформляй ответы таблицами", ctx => ctx.Query.Contains("сравни")))
.WithMemory(new SlidingWindowMemory(20))
.WithGuard(new HallucinationGuard(checker))
```

Навык — инструкция в системный промпт с необязательным условием применимости. Память движок
вызывает сам до прогона и сохраняет взаимодействие после, но системный промпт остаётся за
циклом — иначе память перекрыла бы его своим.

## Смотри также

- [agents.md](agents.md) — агент с нативным function calling
- [planning.md](planning.md) — планировщик (Plan → Execute)
- [tools.md](tools.md) — инструменты на атрибутах
